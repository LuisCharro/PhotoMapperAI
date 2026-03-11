using OpenCvSharp;
using OpenCvSharp.Dnn;
using PhotoMapperAI.Models;
using System.Linq;
using System.Runtime.InteropServices;

namespace PhotoMapperAI.Services.AI;

/// <summary>
/// Face detection service using OpenCV YuNet (returns face + eye landmarks).
/// </summary>
public class OpenCVYuNetFaceDetectionService : IFaceDetectionService, IDisposable
{
    private static readonly int[] Strides = { 8, 16, 32 };

    private readonly string _modelsPath;
    private readonly string _modelPath;
    private readonly float _scoreThreshold;
    private readonly float _nmsThreshold;
    private readonly int _topK;

    private Net? _net;
    private IReadOnlyList<string>? _outputNames;
    private bool _initialized;
    private int _inputWidth;
    private int _inputHeight;
    private static readonly OpenCvSharp.Size DefaultInputSize = new(320, 320);

    /// <summary>
    /// Creates a new OpenCV YuNet face detection service.
    /// </summary>
    public OpenCVYuNetFaceDetectionService(
        string modelsPath = "./models",
        float scoreThreshold = 0.9f,
        float nmsThreshold = 0.3f,
        int topK = 5000)
    {
        _scoreThreshold = scoreThreshold;
        _nmsThreshold = nmsThreshold;
        _topK = topK;

        _modelsPath = ResolveModelsDirectory(modelsPath);
        _modelPath = ResolveFirstExistingFile(
            _modelsPath,
            "face_detection_yunet_2023mar.onnx");
    }

    /// <summary>
    /// Gets the model name.
    /// </summary>
    public string ModelName => "opencv-yunet";

    /// <summary>
    /// Initializes YuNet model.
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!File.Exists(_modelPath))
                {
                    Console.WriteLine($"OpenCV YuNet model file not found. ModelsPath='{_modelsPath}', Model='{_modelPath}'");
                    _initialized = false;
                    return false;
                }

                var net = CvDnn.ReadNetFromOnnx(_modelPath);
                if (net == null)
                {
                    Console.WriteLine($"OpenCV YuNet failed to load model: {_modelPath}");
                    _initialized = false;
                    return false;
                }

                net.SetPreferableBackend(Backend.OPENCV);
                net.SetPreferableTarget(Target.CPU);
                _net = net;
                var names = net.GetUnconnectedOutLayersNames();
                _outputNames = names
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name!)
                    .ToArray();


                _initialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing OpenCV YuNet: {ex}");
                _initialized = false;
                return false;
            }
        });
    }

    /// <summary>
    /// Detects face and eye landmarks in a photo.
    /// </summary>
    public async Task<FaceLandmarks> DetectFaceLandmarksAsync(string imagePath)
    {
        return await Task.Run(() =>
        {
            var startTime = DateTime.UtcNow;

            if (!_initialized || _net == null)
            {
                return new FaceLandmarks
                {
                    FaceDetected = false,
                    ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                    ModelUsed = ModelName,
                    Metadata = new Dictionary<string, string>
                    {
                        { "error", "Model not initialized" }
                    }
                };
            }

            using var image = ReadImage(imagePath);
            if (image.Empty())
            {
                return new FaceLandmarks
                {
                    FaceDetected = false,
                    ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                    ModelUsed = ModelName
                };
            }

            SetInputSize(DefaultInputSize);
            using var blob = CvDnn.BlobFromImage(image, 1.0, DefaultInputSize, new Scalar(), false, false);

            var outputNames = _outputNames;
            if (outputNames == null || outputNames.Count == 0)
            {
                outputNames = new[]
                {
                    "cls_8", "cls_16", "cls_32",
                    "obj_8", "obj_16", "obj_32",
                    "bbox_8", "bbox_16", "bbox_32",
                    "kps_8", "kps_16", "kps_32"
                };
            }

            var outputBlobs = outputNames.Select(_ => new Mat()).ToList();
            _net.SetInput(blob);
            _net.Forward(outputBlobs, outputNames);

            var outputByName = new Dictionary<string, Mat>(StringComparer.OrdinalIgnoreCase);
            var outputCount = Math.Min(outputNames.Count, outputBlobs.Count);
            for (var i = 0; i < outputCount; i++)
            {
                outputByName[outputNames[i]] = outputBlobs[i];
            }

            var candidates = PostProcess(outputByName, image.Size());
            foreach (var mat in outputBlobs)
            {
                mat.Dispose();
            }

            if (candidates.Count == 0)
            {
                return new FaceLandmarks
                {
                    FaceDetected = false,
                    ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                    ModelUsed = ModelName
                };
            }

            var best = candidates.OrderByDescending(c => c.Score).First();
            var faceRect = new PhotoMapperAI.Models.Rectangle(
                Clamp((int)Math.Round(best.X), 0, image.Cols - 1),
                Clamp((int)Math.Round(best.Y), 0, image.Rows - 1),
                Math.Max(1, (int)Math.Round(best.Width)),
                Math.Max(1, (int)Math.Round(best.Height)));

            var leftEye = new PhotoMapperAI.Models.Point(
                Clamp((int)Math.Round(best.LeftEyeX), 0, image.Cols - 1),
                Clamp((int)Math.Round(best.LeftEyeY), 0, image.Rows - 1));

            var rightEye = new PhotoMapperAI.Models.Point(
                Clamp((int)Math.Round(best.RightEyeX), 0, image.Cols - 1),
                Clamp((int)Math.Round(best.RightEyeY), 0, image.Rows - 1));

            return new FaceLandmarks
            {
                FaceDetected = true,
                BothEyesDetected = true,
                FaceRect = faceRect,
                FaceCenter = new PhotoMapperAI.Models.Point(
                    faceRect.X + faceRect.Width / 2,
                    faceRect.Y + faceRect.Height / 2),
                LeftEye = leftEye,
                RightEye = rightEye,
                FaceConfidence = best.Score,
                ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                ModelUsed = ModelName
            };
        });
    }

    /// <summary>
    /// Batch detects faces in multiple photos.
    /// </summary>
    public async Task<List<FaceLandmarks>> DetectFaceLandmarksBatchAsync(List<string> imagePaths)
    {
        var results = new List<FaceLandmarks>();
        foreach (var imagePath in imagePaths)
        {
            var result = await DetectFaceLandmarksAsync(imagePath);
            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// Checks if service is available.
    /// </summary>
    public async Task<bool> IsAvailableAsync()
    {
        return await Task.Run(() => File.Exists(_modelPath) && IsYuNetAvailable());
    }

    /// <summary>
    /// Cleanup resources.
    /// </summary>
    public void Dispose()
    {
        _net?.Dispose();
    }

    public static (string ModelsPath, string ModelPath) GetResolvedModelPaths(string modelsPath = "./models")
    {
        var resolvedModelsPath = ResolveModelsDirectory(modelsPath);
        var modelPath = ResolveFirstExistingFile(
            resolvedModelsPath,
            "face_detection_yunet_2023mar.onnx");

        return (resolvedModelsPath, modelPath);
    }

    private void SetInputSize(OpenCvSharp.Size size)
    {
        _inputWidth = size.Width;
        _inputHeight = size.Height;
    }

    private List<FaceCandidate> PostProcess(IReadOnlyDictionary<string, Mat> outputBlobs, OpenCvSharp.Size imageSize)
    {
        if (outputBlobs.Count < 12)
            return new List<FaceCandidate>();

        var candidates = new List<FaceCandidate>();

        for (var strideIndex = 0; strideIndex < Strides.Length; strideIndex++)
        {
            var stride = Strides[strideIndex];
            if (!outputBlobs.TryGetValue($"cls_{stride}", out var clsMat)
                || !outputBlobs.TryGetValue($"obj_{stride}", out var objMat)
                || !outputBlobs.TryGetValue($"bbox_{stride}", out var bboxMat)
                || !outputBlobs.TryGetValue($"kps_{stride}", out var kpsMat))
            {
                return new List<FaceCandidate>();
            }

            var rows = _inputHeight / stride;
            var cols = _inputWidth / stride;

            var cls = ExtractFloatArray(clsMat);
            var obj = ExtractFloatArray(objMat);
            var bbox = ExtractFloatArray(bboxMat);
            var kps = ExtractFloatArray(kpsMat);


            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < cols; c++)
                {
                    var idx = r * cols + c;
                    var clsScore = Clamp01(cls[idx]);
                    var objScore = Clamp01(obj[idx]);
                    var score = MathF.Sqrt(clsScore * objScore);
                    if (score < _scoreThreshold)
                        continue;

                    var boxIndex = idx * 4;
                    var cx = (c + bbox[boxIndex]) * stride;
                    var cy = (r + bbox[boxIndex + 1]) * stride;
                    var w = MathF.Exp(bbox[boxIndex + 2]) * stride;
                    var h = MathF.Exp(bbox[boxIndex + 3]) * stride;
                    var x1 = cx - w / 2f;
                    var y1 = cy - h / 2f;

                    var kpsIndex = idx * 10;
                    var rightEyeX = (kps[kpsIndex] + c) * stride;
                    var rightEyeY = (kps[kpsIndex + 1] + r) * stride;
                    var leftEyeX = (kps[kpsIndex + 2] + c) * stride;
                    var leftEyeY = (kps[kpsIndex + 3] + r) * stride;

                    var scaleX = imageSize.Width / (float)_inputWidth;
                    var scaleY = imageSize.Height / (float)_inputHeight;

                    candidates.Add(new FaceCandidate(
                        Clamp(x1 * scaleX, 0, imageSize.Width - 1),
                        Clamp(y1 * scaleY, 0, imageSize.Height - 1),
                        Clamp(w * scaleX, 1, imageSize.Width),
                        Clamp(h * scaleY, 1, imageSize.Height),
                        Clamp(rightEyeX * scaleX, 0, imageSize.Width - 1),
                        Clamp(rightEyeY * scaleY, 0, imageSize.Height - 1),
                        Clamp(leftEyeX * scaleX, 0, imageSize.Width - 1),
                        Clamp(leftEyeY * scaleY, 0, imageSize.Height - 1),
                        score));
                }
            }
        }

        if (candidates.Count <= 1)
            return candidates;

        return ApplyNms(candidates, _nmsThreshold, _topK);
    }

    private static List<FaceCandidate> ApplyNms(List<FaceCandidate> candidates, float nmsThreshold, int topK)
    {
        var ordered = candidates
            .OrderByDescending(c => c.Score)
            .ToList();

        var kept = new List<FaceCandidate>();
        foreach (var candidate in ordered)
        {
            if (topK > 0 && kept.Count >= topK)
                break;

            var current = new Rect2d(candidate.X, candidate.Y, candidate.Width, candidate.Height);
            var keep = true;
            foreach (var existing in kept)
            {
                var existingRect = new Rect2d(existing.X, existing.Y, existing.Width, existing.Height);
                if (IoU(current, existingRect) >= nmsThreshold)
                {
                    keep = false;
                    break;
                }
            }

            if (keep)
                kept.Add(candidate);
        }

        return kept;
    }

    private static double IoU(Rect2d a, Rect2d b)
    {
        var x1 = Math.Max(a.X, b.X);
        var y1 = Math.Max(a.Y, b.Y);
        var x2 = Math.Min(a.X + a.Width, b.X + b.Width);
        var y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

        var interWidth = Math.Max(0, x2 - x1);
        var interHeight = Math.Max(0, y2 - y1);
        var interArea = interWidth * interHeight;

        var areaA = a.Width * a.Height;
        var areaB = b.Width * b.Height;
        var union = areaA + areaB - interArea;
        if (union <= 0)
            return 0;

        return interArea / union;
    }

    private static float[] ExtractFloatArray(Mat mat)
    {
        var total = (int)mat.Total();
        var data = new float[total];
        Marshal.Copy(mat.Data, data, 0, total);
        return data;
    }

    private static float Clamp01(float value)
    {
        if (value < 0f)
            return 0f;
        if (value > 1f)
            return 1f;
        return value;
    }

    private static float Clamp(float value, float min, float max)
    {
        if (value < min)
            return min;
        if (value > max)
            return max;
        return value;
    }

    private static string ResolveModelsDirectory(string configuredModelsPath)
    {
        if (Directory.Exists(configuredModelsPath))
            return configuredModelsPath;

        var envPath = Environment.GetEnvironmentVariable("PHOTOMAPPERAI_MODELS_PATH");
        if (!string.IsNullOrWhiteSpace(envPath) && Directory.Exists(envPath))
            return envPath;

        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "models"),
            Path.Combine(Directory.GetCurrentDirectory(), "models")
        };

        candidates.AddRange(BuildParentCandidates(AppContext.BaseDirectory, "models", maxDepth: 6));
        candidates.AddRange(BuildParentCandidates(Directory.GetCurrentDirectory(), "models", maxDepth: 6));

        return candidates.FirstOrDefault(Directory.Exists) ?? configuredModelsPath;
    }

    private static IEnumerable<string> BuildParentCandidates(string startPath, string childName, int maxDepth)
    {
        var current = new DirectoryInfo(startPath);
        for (var i = 0; i < maxDepth && current != null; i++)
        {
            yield return Path.Combine(current.FullName, childName);
            current = current.Parent;
        }
    }

    private static string ResolveFirstExistingFile(string directoryPath, params string[] fileNames)
    {
        foreach (var fileName in fileNames)
        {
            var candidate = Path.Combine(directoryPath, fileName);
            if (File.Exists(candidate))
                return candidate;
        }

        return Path.Combine(directoryPath, fileNames[0]);
    }

    private static Mat ReadImage(string imagePath)
    {
        if (!File.Exists(imagePath))
            return new Mat();

        try
        {
            return Cv2.ImRead(imagePath, ImreadModes.Color);
        }
        catch (ArgumentException)
        {
            var imageBytes = File.ReadAllBytes(imagePath);
            return Cv2.ImDecode(imageBytes, ImreadModes.Color);
        }
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min)
            return min;
        if (value > max)
            return max;
        return value;
    }

    internal static bool IsYuNetAvailable()
    {
        return true;
    }

    private readonly record struct FaceCandidate(
        float X,
        float Y,
        float Width,
        float Height,
        float RightEyeX,
        float RightEyeY,
        float LeftEyeX,
        float LeftEyeY,
        float Score);
}
