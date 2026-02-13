using PhotoMapperAI.Models;

namespace PhotoMapperAI.Services.AI;

/// <summary>
/// Interface for face detection services (supports OpenCV and Ollama Vision models).
/// </summary>
public interface IFaceDetectionService
{
    /// <summary>
    /// Gets the model name for display/logging.
    /// </summary>
    string ModelName { get; }

    /// <summary>
    /// Detects face and eye landmarks in a photo.
    /// </summary>
    /// <param name="imagePath">Path to the image file</param>
    /// <returns>Face landmarks with detection results</returns>
    Task<FaceLandmarks> DetectFaceLandmarksAsync(string imagePath);

    /// <summary>
    /// Batch detect faces in multiple photos.
    /// </summary>
    /// <param name="imagePaths">List of image paths</param>
    /// <returns>List of face landmarks</returns>
    Task<List<FaceLandmarks>> DetectFaceLandmarksBatchAsync(List<string> imagePaths);

    /// <summary>
    /// Checks if the service is available/ready.
    /// </summary>
    /// <returns>True if service can be used</returns>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Initializes the service (loads models, etc).
    /// </summary>
    /// <returns>True if initialization succeeded</returns>
    Task<bool> InitializeAsync();
}
