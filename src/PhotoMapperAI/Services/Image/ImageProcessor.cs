using PhotoMapperAI.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace PhotoMapperAI.Services.Image;

/// <summary>
/// Image processing service using ImageSharp for portrait cropping.
/// </summary>
public class ImageProcessor : IImageProcessor
{
    public string Name => "ImageSharp";

    /// <summary>
    /// Loads an image from file.
    /// </summary>
    public async Task<SixLabors.ImageSharp.Image> LoadImageAsync(string imagePath)
    {
        return await Task.Run(() =>
        {
            var image = SixLabors.ImageSharp.Image.Load(imagePath);
            return image;
        });
    }

    /// <summary>
    /// Crops a portrait region based on face landmarks.
    /// </summary>
    public async Task<SixLabors.ImageSharp.Image> CropPortraitAsync(
        SixLabors.ImageSharp.Image image,
        FaceLandmarks landmarks,
        int portraitWidth,
        int portraitHeight)
    {
        return await Task.Run(() =>
        {
            // Calculate crop rectangle based on face landmarks
            var cropRect = CalculatePortraitCrop(
                landmarks,
                image.Width,
                image.Height,
                portraitWidth,
                portraitHeight
            );

            // Ensure crop is within image bounds and recalculate if needed
            var x = Math.Max(0, cropRect.X);
            var y = Math.Max(0, cropRect.Y);
            var width = Math.Min(cropRect.Width, image.Width - x);
            var height = Math.Min(cropRect.Height, image.Height - y);

            // Create rectangle for cropping (using ImageSharp's Rectangle)
            var rect = new SixLabors.ImageSharp.Rectangle(x, y, width, height);

            // Crop
            var cropped = image.Clone(img => img.Crop(rect));

            // Resize to exact portrait dimensions
            if (cropped.Width != portraitWidth || cropped.Height != portraitHeight)
            {
                cropped.Mutate(img => img.Resize(portraitWidth, portraitHeight));
            }

            return cropped;
        });
    }

    /// <summary>
    /// Saves an image to file.
    /// </summary>
    public async Task SaveImageAsync(SixLabors.ImageSharp.Image image, string outputPath, string format)
    {
        await Task.Run(() =>
        {
            image.Save(outputPath, new JpegEncoder { Quality = 90 });
        });
    }

    /// <summary>
    /// Gets image dimensions.
    /// </summary>
    public async Task<(int Width, int Height)> GetImageDimensionsAsync(string imagePath)
    {
        return await Task.Run(() =>
        {
            using var image = SixLabors.ImageSharp.Image.Load(imagePath);
            return (image.Width, image.Height);
        });
    }

    #region Private Methods

    /// <summary>
    /// Calculates portrait crop rectangle based on face landmarks.
    /// </summary>
    private static PhotoMapperAI.Models.Rectangle CalculatePortraitCrop(
        FaceLandmarks landmarks,
        int imageWidth,
        int imageHeight,
        int portraitWidth,
        int portraitHeight)
    {
        // Case 1: Both eyes detected (best centering)
        if (landmarks.BothEyesDetected && landmarks.EyeMidpoint != null)
        {
            var eyeMidpoint = landmarks.EyeMidpoint;
            var cropWidth = (int)(portraitWidth * 1.2); // 20% wider than target
            var cropHeight = (int)(portraitHeight * 1.5); // 50% taller than target

            return new PhotoMapperAI.Models.Rectangle(
                eyeMidpoint.X - (cropWidth / 2),
                eyeMidpoint.Y - (int)(cropHeight * 0.35), // Eyes at 35% from top
                cropWidth,
                cropHeight
            );
        }

        // Case 2: One eye detected
        else if (landmarks.LeftEye != null || landmarks.RightEye != null)
        {
            var eye = landmarks.LeftEye ?? landmarks.RightEye ?? new PhotoMapperAI.Models.Point(0, 0);

            var cropWidth = (int)(portraitWidth * 1.3);
            var cropHeight = (int)(portraitHeight * 1.6);

            return new PhotoMapperAI.Models.Rectangle(
                eye.X - (cropWidth / 2),
                eye.Y - (int)(cropHeight * 0.4),
                cropWidth,
                cropHeight
            );
        }

        // Case 3: No eyes but face detected
        else if (landmarks.FaceDetected && landmarks.FaceRect != null)
        {
            var faceRect = landmarks.FaceRect;
            var cropWidth = (int)(portraitWidth * 1.5);
            var cropHeight = (int)(portraitHeight * 1.8);

            return new PhotoMapperAI.Models.Rectangle(
                faceRect.X - (cropWidth - faceRect.Width) / 2,
                faceRect.Y - (int)(cropHeight * 0.3),
                cropWidth,
                cropHeight
            );
        }

        // Case 4: No face detected (upper-body crop fallback)
        // For sports photos (full-body shots), crop from upper part of image
        // Expected portrait: head + neck + bit of chest (not full body)
        else
        {
            var cropWidth = (int)(portraitWidth * 2.0);
            var cropHeight = (int)(portraitHeight * 2.0);

            // Position crop in upper portion of image (top 40% instead of center)
            // This captures head, neck, and chest area for portrait-style crops
            var cropY = (int)(imageHeight * 0.2) - (cropHeight / 2); // Start at 20% from top
            cropY = Math.Max(0, cropY); // Ensure we don't go negative

            return new PhotoMapperAI.Models.Rectangle(
                (imageWidth - cropWidth) / 2,  // Center horizontally
                cropY,                         // Upper portion (not center)
                cropWidth,
                cropHeight
            );
        }
    }

    #endregion
}
