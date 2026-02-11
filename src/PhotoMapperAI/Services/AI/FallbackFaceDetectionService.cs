using PhotoMapperAI.Models;
using System.Diagnostics;

namespace PhotoMapperAI.Services.AI;

/// <summary>
/// Face detection service with automatic fallback to multiple models.
/// Tries models in order of speed/availability, falling back as needed.
/// </summary>
public class FallbackFaceDetectionService : IFaceDetectionService
{
    private readonly IFaceDetectionService[] _services;
    private readonly List<string> _fallbackLog = new();

    /// <summary>
    /// Gets the model name for display/logging.
    /// </summary>
    public string ModelName => "Fallback";

    /// <summary>
    /// Creates a new fallback face detection service.
    /// </summary>
    /// <param name="models">Comma-separated list of models in fallback order</param>
    public FallbackFaceDetectionService(string models)
    {
        var modelList = models.Split(',', StringSplitOptions.RemoveEmptyEntries);
        _services = new IFaceDetectionService[modelList.Length];

        for (int i = 0; i < modelList.Length; i++)
        {
            _services[i] = CreateFaceDetectionService(modelList[i].Trim());
        }
    }

    /// <summary>
    /// Detects face landmarks using the first available model, falling back as needed.
    /// </summary>
    public async Task<FaceLandmarks> DetectFaceLandmarksAsync(string imagePath)
    {
        _fallbackLog.Clear();

        foreach (var service in _services)
        {
            var modelName = service.ModelName;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _fallbackLog.Add($"Trying {modelName}...");

                var result = await service.DetectFaceLandmarksAsync(imagePath);

                stopwatch.Stop();

                if (result.FaceDetected)
                {
                    _fallbackLog.Add($"✓ {modelName} succeeded ({stopwatch.ElapsedMilliseconds}ms)");
                    return result;
                }

                stopwatch.Stop();
                _fallbackLog.Add($"✗ {modelName} failed: No face detected ({stopwatch.ElapsedMilliseconds}ms)");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _fallbackLog.Add($"✗ {modelName} failed: {ex.Message} ({stopwatch.ElapsedMilliseconds}ms)");
            }
        }

        // All models failed - return undetected (will use center crop fallback)
        _fallbackLog.Add("✗ All face detection models failed - using center crop fallback");
        return new FaceLandmarks { FaceDetected = false };
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
    /// Checks if the fallback service is available (at least one model must be available).
    /// </summary>
    public async Task<bool> IsAvailableAsync()
    {
        foreach (var service in _services)
        {
            if (await service.IsAvailableAsync())
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Initializes all fallback services.
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        bool anySucceeded = false;
        foreach (var service in _services)
        {
            if (await service.InitializeAsync())
            {
                anySucceeded = true;
            }
        }
        return anySucceeded;
    }

    /// <summary>
    /// Gets the fallback log for debugging.
    /// </summary>
    public IReadOnlyList<string> GetFallbackLog() => _fallbackLog.AsReadOnly();

    /// <summary>
    /// Creates appropriate face detection service based on model name.
    /// </summary>
    private IFaceDetectionService CreateFaceDetectionService(string model)
    {
        return model.ToLower() switch
        {
            "opencv-dnn" => new OpenCVDNNFaceDetectionService(),
            "yolov8-face" => new OpenCVDNNFaceDetectionService(),
            var ollamaModel when ollamaModel.Contains("llava") => new OllamaFaceDetectionService(modelName: ollamaModel),
            var ollamaModel when ollamaModel.Contains("qwen3-vl") => new OllamaFaceDetectionService(modelName: ollamaModel),
            "center" => new CenterCropFallbackService(),
            _ => new OllamaFaceDetectionService(modelName: model) // Default to Ollama
        };
    }
}

/// <summary>
/// Fallback service that always uses center crop (no face detection).
/// </summary>
public class CenterCropFallbackService : IFaceDetectionService
{
    /// <summary>
    /// Gets the model name for display/logging.
    /// </summary>
    public string ModelName => "CenterCrop";

    /// <summary>
    /// Always returns undetected (triggers center crop).
    /// </summary>
    public Task<FaceLandmarks> DetectFaceLandmarksAsync(string imagePath)
    {
        // Always returns undetected to trigger center crop fallback
        return Task.FromResult(new FaceLandmarks { FaceDetected = false });
    }

    /// <summary>
    /// Batch process - always returns undetected.
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
    /// Always available.
    /// </summary>
    public Task<bool> IsAvailableAsync()
    {
        return Task.FromResult(true);
    }

    /// <summary>
    /// Always succeeds.
    /// </summary>
    public Task<bool> InitializeAsync()
    {
        return Task.FromResult(true);
    }
}
