using System.Runtime.InteropServices;

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

        var normalized = NormalizeOllamaAlias(model.Trim());

        if (allowFallbackChain && normalized.Contains(','))
            return new FallbackFaceDetectionService(normalized);

        var lowered = normalized.ToLowerInvariant();

        // Check platform compatibility for OpenCV models
        if (IsOpenCVModel(lowered) && IsMacOS())
        {
            throw new PlatformNotSupportedException(
                "OpenCV face detection models (opencv-yunet, opencv-dnn, yolov8-face, haar-cascade) are not supported on macOS due to native library dependency issues. " +
                $"Use 'center', 'llava:7b', or 'qwen3-vl' instead. Example: photomapperai generatephotos -d center"
            );
        }

        return lowered switch
        {
            "opencv-yunet" or "yunet" => OpenCVYuNetFaceDetectionService.IsYuNetAvailable()
                ? new OpenCVYuNetFaceDetectionService()
                : CreateYuNetFallback(),
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

    private static bool IsOpenCVModel(string model)
    {
        return model is "opencv-dnn" or "opencv-yunet" or "yunet" or "yolov8-face" or "haar-cascade" or "haar";
    }

    private static bool IsMacOS()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    }

    private static string NormalizeOllamaAlias(string model)
    {
        if (string.Equals(model, "qwen3-vl", StringComparison.OrdinalIgnoreCase))
            return "qwen3-vl:latest";

        return model;
    }

    private static IFaceDetectionService CreateYuNetFallback()
    {
        Console.WriteLine("[FaceDetection] YuNet is not available in the current OpenCvSharp runtime. Falling back to opencv-dnn.");
        return new OpenCVDNNFaceDetectionService();
    }
}
