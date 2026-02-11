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
        _confidenceThreshold = confidenceThreshold;

        // Set model paths based on type
        if (modelType == "dnn")
        {
            _modelPath = Path.Combine(modelsPath, "res10_ssd_deploy.prototxt");
            _weightsPath = Path.Combine(modelsPath, "res10_300x300_ssd_iter_140000.caffemodel");
        }
        else
        {
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
                    _faceNet.SetPreferableBackend(Backend.OPENCV);
                    _faceNet.SetPreferableTarget(Target.CPU);
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
            var blob = CvDnn.BlobFromImage(image, 1.0, new OpenCvSharp.Size(300, 300), new Scalar(104.0, 177.0, 123.0), false, false);
            if (_faceNet == null) return new FaceLandmarks { FaceDetected = false, ModelUsed = ModelName };
            
            _faceNet.SetInput(blob);

            var detections = _faceNet.Forward();
            if (detections == null)
            {
                return new FaceLandmarks { FaceDetected = false, ModelUsed = ModelName };
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
        _faceCascade?.Dispose();
        _eyeCascade?.Dispose();
        _leftEyeCascade?.Dispose();
        _rightEyeCascade?.Dispose();
    }
}
