using OpenCvSharp4;
using OpenCvSharp4.Extensions;
using PhotoMapperAI.Models;
using System.Text.Json;

namespace PhotoMapperAI.Services.AI;

/// <summary>
/// Face detection service using OpenCV DNN model.
/// </summary>
public class OpenCVDNNFaceDetectionService : IFaceDetectionService
{
    private readonly string _modelPath;
    private readonly string _weightsPath;
    private readonly double _confidenceThreshold;

    private Net? _faceNet;
    private CascadeClassifier? _faceCascade;
    private CascadeClassifier? _eyeCascade;
    private CascadeClassifier? _leftEyeCascade;
    private CascadeClassifier? _rightEyeCascade;

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
        _modelPath = modelsPath;
        _confidenceThreshold = confidenceThreshold;

        // Set model paths based on type
        if (modelType == "dnn")
        {
            _modelPath = Path.Combine(modelsPath, "res10_ssd_deploy.prototxt");
            _weightsPath = Path.Combine(modelsPath, "res10_300x300_ssd_iter_140000.caffemodel");
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
                    _faceNet.SetPreferableBackend(DnnBackend.OPENCV);
                    _faceNet.SetPreferableTarget(DnnTarget.CPU);
                }

                _initialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing OpenCV DNN: {ex.Message}");
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

            // Read image
            using var image = Cv2.ImRead(imagePath);
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
            var blob = CvDnn.BlobFromImage(image, 1.0, new Size(300, 300), Scalar.Mean(104.0, 177.0, 123.0), true, false);
            _faceNet?.SetInput(blob);

            var detections = _faceNet?.Forward();
            var output = detections.Reshape(new[] { 1, detections.Cols - 7 });

            var landmarks = new FaceLandmarks
            {
                ModelUsed = ModelName
            };

            // Process detections
            for (int i = 0; i < detections.Rows; i++)
            {
                var confidence = output.At<float>(i, 2);

                if (confidence < _confidenceThreshold)
                    continue;

                var left = output.At<float>(i, 3) * image.Cols;
                var top = output.At<float>(i, 4) * image.Rows;
                var right = output.At<float>(i, 5) * image.Cols;
                var bottom = output.At<float>(i, 6) * image.Rows;

                var faceRect = new Rectangle((int)left, (int)top, (int)(right - left), (int)(bottom - top));

                landmarks.FaceDetected = true;
                landmarks.FaceRect = new Rectangle(faceRect.X, faceRect.Y, faceRect.Width, faceRect.Height);
                landmarks.FaceConfidence = confidence;
                landmarks.FaceCenter = new Point(faceRect.X + faceRect.Width / 2, faceRect.Y + faceRect.Height / 2);

                // Extract face ROI for eye detection
                var faceRoi = new Mat(image, faceRect);

                // Detect eyes using Haar cascades (TODO: Load cascades separately)
                // For now, return face detection only
                break; // Use first face
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
        _faceCascade?.Dispose();
        _eyeCascade?.Dispose();
        _leftEyeCascade?.Dispose();
        _rightEyeCascade?.Dispose();
        Cv2.DestroyAllWindows();
    }
}
