using PhotoMapperAI.Models;

namespace PhotoMapperAI.Services;

/// <summary>
/// Interface for image processing operations (portrait cropping).
/// </summary>
public interface IImageProcessor
{
    /// <summary>
    /// Gets the processor name for display/logging.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Loads an image from file.
    /// </summary>
    /// <param name="imagePath">Path to the image file</param>
    /// <returns>Image object</returns>
    Task<SixLabors.ImageSharp.Image> LoadImageAsync(string imagePath);

    /// <summary>
    /// Crops a portrait region from an image based on face landmarks.
    /// </summary>
    /// <param name="image">Source image</param>
    /// <param name="landmarks">Face landmarks for cropping</param>
    /// <param name="portraitWidth">Target portrait width</param>
    /// <param name="portraitHeight">Target portrait height</param>
    /// <returns>Cropped portrait image</returns>
    Task<SixLabors.ImageSharp.Image> CropPortraitAsync(
        SixLabors.ImageSharp.Image image,
        FaceLandmarks landmarks,
        int portraitWidth,
        int portraitHeight);

    /// <summary>
    /// Saves an image to file.
    /// </summary>
    /// <param name="image">Image to save</param>
    /// <param name="outputPath">Output file path</param>
    /// <param name="format">Image format (jpg, png, etc.)</param>
    Task SaveImageAsync(SixLabors.ImageSharp.Image image, string outputPath, string format);

    /// <summary>
    /// Gets image dimensions.
    /// </summary>
    /// <param name="imagePath">Path to the image file</param>
    /// <returns>Width and height</returns>
    Task<(int Width, int Height)> GetImageDimensionsAsync(string imagePath);

    /// <summary>
    /// Resizes an image to the specified dimensions.
    /// </summary>
    /// <param name="image">Source image</param>
    /// <param name="targetWidth">Target width</param>
    /// <param name="targetHeight">Target height</param>
    /// <returns>Resized image</returns>
    Task<SixLabors.ImageSharp.Image> ResizeAsync(SixLabors.ImageSharp.Image image, int targetWidth, int targetHeight);
}
