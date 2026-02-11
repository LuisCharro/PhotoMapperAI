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
        // Target aspect ratio is portrait (e.g., 200:300 = 2:3)
        var targetAspectRatio = (double)portraitWidth / portraitHeight; // e.g., 0.667
        
        // Calculate crop size based on source image dimensions
        // We want head + neck + upper chest (proper portrait)
        // For a typical portrait, the face should occupy ~45-50% of the height
        var cropHeight = (int)(imageHeight * 0.35); // 35% of source = head + neck + chest
        var cropWidth = (int)(cropHeight * targetAspectRatio);
        
        // Ensure crop doesn't exceed image bounds
        if (cropWidth > imageWidth)
        {
            cropWidth = imageWidth;
            cropHeight = (int)(cropWidth / targetAspectRatio);
        }

        // Determine the center point for cropping
        int centerX, eyeY;
        
        // Case 1: Both eyes detected (best centering)
        if (landmarks.BothEyesDetected && landmarks.EyeMidpoint != null)
        {
            centerX = landmarks.EyeMidpoint.X;
            eyeY = landmarks.EyeMidpoint.Y;
        }
        
        // Case 2: One eye detected - use it and estimate horizontal center
        else if (landmarks.LeftEye != null || landmarks.RightEye != null)
        {
            var eye = landmarks.LeftEye ?? landmarks.RightEye!;
            centerX = eye.X;
            eyeY = eye.Y;
            
            // If only one eye detected, adjust center towards the other side
            // (eyes are symmetric, so center is offset from single eye)
            if (landmarks.LeftEye != null)
            {
                centerX = eye.X + (int)(cropWidth * 0.15); // Shift right
            }
            else
            {
                centerX = eye.X - (int)(cropWidth * 0.15); // Shift left
            }
        }
        
        // Case 3: No eyes but face detected - ESTIMATE eye position
        else if (landmarks.FaceDetected && landmarks.FaceRect != null)
        {
            var faceRect = landmarks.FaceRect;
            
            // Eyes are typically in the upper 1/3 of the face rectangle
            // Horizontal center of face = center between eyes
            centerX = faceRect.X + faceRect.Width / 2;
            eyeY = faceRect.Y + (int)(faceRect.Height * 0.35); // Eyes at 35% from top of face
        }
        
        // Case 4: No face detected - use upper portion of image
        else
        {
            // Center horizontally
            centerX = imageWidth / 2;
            // Estimate eyes at ~15% from top of image (typical for full-body sports photos)
            eyeY = (int)(imageHeight * 0.15);
        }
        
        // Calculate crop rectangle
        // Eyes should be at ~35% from top of the portrait (standard portrait composition)
        var cropX = centerX - (cropWidth / 2);
        var cropY = eyeY - (int)(cropHeight * 0.35); // Eyes at 35% from top
        
        // Ensure crop stays within image bounds
        cropX = Math.Max(0, Math.Min(cropX, imageWidth - cropWidth));
        cropY = Math.Max(0, Math.Min(cropY, imageHeight - cropHeight));
        
        // Adjust if crop goes beyond image bounds
        if (cropX + cropWidth > imageWidth)
        {
            cropX = imageWidth - cropWidth;
        }
        if (cropY + cropHeight > imageHeight)
        {
            cropY = imageHeight - cropHeight;
        }

        return new PhotoMapperAI.Models.Rectangle(
            cropX,
            cropY,
            cropWidth,
            cropHeight
        );
    }

    #endregion
}
