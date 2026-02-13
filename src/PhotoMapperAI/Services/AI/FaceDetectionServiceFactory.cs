namespace PhotoMapperAI.Services.AI;

/// <summary>
/// Centralized factory for face detection service creation.
/// </summary>
public static class FaceDetectionServiceFactory
{
    /// <summary>
    /// Creates a face detection service for the provided model identifier.
    /// </summary>
    /// <param name="model">Model name (or comma-separated fallback chain)</param>
    /// <param name="allowFallbackChain">If true, comma-separated model chains create FallbackFaceDetectionService</param>
    /// <param name="fallbackToOllamaOnUnknown">If true, unknown model names default to OllamaFaceDetectionService</param>
    public static IFaceDetectionService Create(
        string model,
        bool allowFallbackChain = true,
        bool fallbackToOllamaOnUnknown = false)
    {
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Face detection model cannot be empty.", nameof(model));

        var normalized = model.Trim();

        if (allowFallbackChain && normalized.Contains(','))
            return new FallbackFaceDetectionService(normalized);

        var lowered = normalized.ToLowerInvariant();
        return lowered switch
        {
            "opencv-dnn" => new OpenCVDNNFaceDetectionService(),
            "yolov8-face" => new OpenCVDNNFaceDetectionService(),
            "haar-cascade" or "haar" => new HaarCascadeFaceDetectionService(),
            "center" => new CenterCropFallbackService(),
            var ollamaModel when ollamaModel.Contains("llava") || ollamaModel.Contains("qwen3-vl")
                => new OllamaFaceDetectionService(modelName: normalized),
            _ when fallbackToOllamaOnUnknown
                => new OllamaFaceDetectionService(modelName: normalized),
            _ => throw new ArgumentException($"Unknown face detection model: {model}")
        };
    }
}
