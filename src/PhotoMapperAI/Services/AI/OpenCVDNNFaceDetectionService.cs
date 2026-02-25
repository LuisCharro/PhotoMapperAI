using OpenCvSharp;
using OpenCvSharp.Dnn;
using PhotoMapperAI.Models;
using System.Text.Json;

namespace PhotoMapperAI.Services.AI;

/// <summary>
/// Face detection service using OpenCV DNN model.
/// </summary>
public class OpenCVDNNFaceDetectionService : IFaceDetectionService
{
    private readonly string _modelsPath;
    private readonly string _modelPath;
    private readonly string _weightsPath;
    private readonly double _confidenceThreshold;

    private Net? _faceNet;

    private bool _initialized = false;

    /// <summary>
    /// Creates a new OpenCV DNN face detection service.
    /// </summary>
    /// <param name="modelsPath">Path to models directory</param>
    /// <param name="modelType">Type of OpenCV model (dnn, haar)</param>
    /// <param name="confidenceThreshold">Minimum confidence for face detection (default: 0.7)</param>
    public OpenCVDNNFaceDetectionService(
        string modelsPath = "./models",
        string modelType = "dnn",
        double confidenceThreshold = 0.7)
    {
        _confidenceThreshold = confidenceThreshold;

        // Set model paths based on type
        if (modelType == "dnn")
        {
            _modelsPath = ResolveModelsDirectory(modelsPath);
            _modelPath = ResolveFirstExistingFile(
                _modelsPath,
                "res10_ssd_deploy.prototxt",
                "res10_300x300_ssd_iter_140000.prototxt",
                "deploy.prototxt");
            _weightsPath = ResolveFirstExistingFile(
                _modelsPath,
                "res10_300x300_ssd_iter_140000.caffemodel");
        }
        else
        {
            _modelsPath = string.Empty;
            _modelPath = string.Empty;
            _weightsPath = string.Empty;
        }
    }

    /// <summary>
    /// Gets the model name.
    /// </summary>
    public string ModelName => "opencv-dnn";

    /// <summary>
    /// Initializes OpenCV face detection models.
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                // Initialize DNN face detector
                if (File.Exists(_modelPath) && File.Exists(_weightsPath))
                {
                    _faceNet = CvDnn.ReadNetFromCaffe(_modelPath, _weightsPath);
                    if (_faceNet != null)
                    {
                        _faceNet.SetPreferableBackend(Backend.OPENCV);
                        _faceNet.SetPreferableTarget(Target.CPU);
                    }
                }
                else
                {
                    Console.WriteLine(
                        $"OpenCV DNN model files not found. ModelsPath='{_modelsPath}', Prototxt='{_modelPath}', Weights='{_weightsPath}'");
                }

                _initialized = _faceNet != null;
                return _initialized;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing OpenCV DNN: {ex}");
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

            if (!_initialized)
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

            // Read image (ImRead can fail on non-ASCII paths on Windows)
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

            // Detect faces using DNN
            var blob = CvDnn.BlobFromImage(image, 1.0, new OpenCvSharp.Size(300, 300), new Scalar(104.0, 177.0, 123.0), false, false);
            if (_faceNet == null) return new FaceLandmarks { FaceDetected = false, ModelUsed = ModelName };
            
            _faceNet.SetInput(blob);

            var detections = _faceNet.Forward();
            if (detections == null || detections.Empty())
            {
                return new FaceLandmarks 
                { 
                    FaceDetected = false, 
                    ModelUsed = ModelName,
                    Metadata = new Dictionary<string, string>
                    {
                        { "error", "No detections returned" }
                    }
                };
            }

            // Detections shape: [1, 1, N, 7]
            var landmarks = new FaceLandmarks
            {
                ModelUsed = ModelName
            };

            // Access data directly from Mat
            // OpenCV DNN detections format: 7 values per detection [batch, class, confidence, left, top, right, bottom]
            int numDetections = detections.Size(2);
            float maxConfidence = 0;
            
            for (int i = 0; i < numDetections; i++)
            {
                float confidence = detections.At<float>(0, 0, i, 2);

                if (confidence < _confidenceThreshold)
                    continue;

                if (confidence > maxConfidence)
                {
                    maxConfidence = confidence;
                    float left = detections.At<float>(0, 0, i, 3) * image.Cols;
                    float top = detections.At<float>(0, 0, i, 4) * image.Rows;
                    float right = detections.At<float>(0, 0, i, 5) * image.Cols;
                    float bottom = detections.At<float>(0, 0, i, 6) * image.Rows;

                    var faceRect = new PhotoMapperAI.Models.Rectangle((int)left, (int)top, (int)(right - left), (int)(bottom - top));

                    landmarks.FaceDetected = true;
                    landmarks.FaceRect = faceRect;
                    landmarks.FaceConfidence = confidence;
                    landmarks.FaceCenter = new PhotoMapperAI.Models.Point(faceRect.X + faceRect.Width / 2, faceRect.Y + faceRect.Height / 2);
                }
            }

            landmarks.ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

            return landmarks;
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
        return await Task.Run(() =>
        {
            return File.Exists(_modelPath) && File.Exists(_weightsPath);
        });
    }

    /// <summary>
    /// Cleanup resources.
    /// </summary>
    public void Dispose()
    {
        _faceNet?.Dispose();
    }

    public static (string ModelsPath, string PrototxtPath, string WeightsPath) GetResolvedModelPaths(
        string modelsPath = "./models")
    {
        var resolvedModelsPath = ResolveModelsDirectory(modelsPath);
        var prototxtPath = ResolveFirstExistingFile(
            resolvedModelsPath,
            "res10_ssd_deploy.prototxt",
            "res10_300x300_ssd_iter_140000.prototxt",
            "deploy.prototxt");
        var weightsPath = ResolveFirstExistingFile(
            resolvedModelsPath,
            "res10_300x300_ssd_iter_140000.caffemodel");

        return (resolvedModelsPath, prototxtPath, weightsPath);
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
            // Fallback for Unicode paths that OpenCV cannot marshal.
            var imageBytes = File.ReadAllBytes(imagePath);
            return Cv2.ImDecode(imageBytes, ImreadModes.Color);
        }
    }
}
