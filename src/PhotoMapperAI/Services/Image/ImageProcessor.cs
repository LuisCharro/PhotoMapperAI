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
    /// Uses face-based dimensions for consistent portrait composition.
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

        int cropWidth, cropHeight;
        int centerX, eyeY;

        // Case 1: Both eyes detected (best centering)
        if (landmarks.BothEyesDetected && landmarks.EyeMidpoint != null && landmarks.FaceRect != null)
        {
            var faceRect = landmarks.FaceRect;
            
            // Calculate crop dimensions based on FACE size (not image size)
            // This ensures consistent portrait composition regardless of image resolution
            cropWidth = (int)(faceRect.Width * 2.0);   // 2x face width
            cropHeight = (int)(faceRect.Height * 3.0); // 3x face height
            
            centerX = landmarks.EyeMidpoint.X;
            eyeY = landmarks.EyeMidpoint.Y;
        }
        
        // Case 2: One eye detected - use it and estimate horizontal center
        else if (landmarks.LeftEye != null || landmarks.RightEye != null)
        {
            var faceRect = landmarks.FaceRect;
            var eye = landmarks.LeftEye ?? landmarks.RightEye!;
            
            if (faceRect != null)
            {
                cropWidth = (int)(faceRect.Width * 2.0);
                cropHeight = (int)(faceRect.Height * 3.0);
            }
            else
            {
                // Fallback to image-based dimensions
                cropHeight = (int)(imageHeight * 0.35);
                cropWidth = (int)(cropHeight * targetAspectRatio);
            }
            
            centerX = eye.X;
            eyeY = eye.Y;
            
            // If only one eye detected, adjust center towards the other side
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
            
            // Calculate crop dimensions based on FACE size
            cropWidth = (int)(faceRect.Width * 2.0);   // 2x face width
            cropHeight = (int)(faceRect.Height * 3.0); // 3x face height
            
            // Eyes are typically in the upper 35% of the face rectangle
            centerX = faceRect.X + faceRect.Width / 2;
            eyeY = faceRect.Y + (int)(faceRect.Height * 0.35);
        }
        
        // Case 4: No face detected - use upper portion of image (center crop mode)
        else
        {
            // For full-body sports photos, the face is typically in the upper 20% of the image.
            // We want a portrait showing: bit of space + head + neck + bit of chest
            // This is approximately 20-25% of the total image height for full-body photos.
            
            // Use a smaller crop height for proper portrait composition
            cropHeight = (int)(imageHeight * 0.22);  // 22% of image height
            cropWidth = (int)(cropHeight * targetAspectRatio);
            
            // Center horizontally
            centerX = imageWidth / 2;
            
            // Position crop at upper portion of image
            // For full-body photos, face is typically at 10-15% from top
            // We want the crop to start near the top to capture head + neck + bit of chest
            eyeY = (int)(imageHeight * 0.12);  // Eyes at ~12% from top
        }

        // Ensure crop doesn't exceed image bounds
        if (cropWidth > imageWidth)
        {
            cropWidth = imageWidth;
            cropHeight = (int)(cropWidth / targetAspectRatio);
        }
        if (cropHeight > imageHeight)
        {
            cropHeight = imageHeight;
            cropWidth = (int)(cropHeight * targetAspectRatio);
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
