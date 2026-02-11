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
    public async Task<Image> LoadImageAsync(string imagePath)
    {
        return await Task.Run(() =>
        {
            var image = Image.Load(imagePath);
            return image;
        });
    }

    /// <summary>
    /// Crops a portrait region based on face landmarks.
    /// </summary>
    public async Task<Image> CropPortraitAsync(
        Image image,
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

            // Ensure crop is within image bounds
            cropRect.X = Math.Max(0, cropRect.X);
            cropRect.Y = Math.Max(0, cropRect.Y);
            cropRect.Width = Math.Min(cropRect.Width, image.Width - cropRect.X);
            cropRect.Height = Math.Min(cropRect.Height, image.Height - cropRect.Y);

            // Create rectangle for cropping
            var rect = new Rectangle(
                cropRect.X,
                cropRect.Y,
                cropRect.Width,
                cropRect.Height
            );

            // Crop and resize
            var cropped = image.Clone(x => x.Crop(rect));

            // Resize to exact portrait dimensions
            if (cropped.Width != portraitWidth || cropped.Height != portraitHeight)
            {
                cropped.Mutate(x => x.Resize(portraitWidth, portraitHeight));
            }

            return cropped;
        });
    }

    /// <summary>
    /// Saves an image to file.
    /// </summary>
    public async Task SaveImageAsync(Image image, string outputPath, string format)
    {
        await Task.Run(() =>
        {
            var extension = format.ToLower() switch
            {
                "jpg" or "jpeg" => "jpg",
                "png" => "png",
                "bmp" => "bmp",
                _ => "jpg"
            };

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
            using var image = Image.Load(imagePath);
            return (image.Width, image.Height);
        });
    }

    #region Private Methods

    /// <summary>
    /// Calculates portrait crop rectangle based on face landmarks.
    /// </summary>
    private static Rectangle CalculatePortraitCrop(
        FaceLandmarks landmarks,
        int imageWidth,
        int imageHeight,
        int portraitWidth,
        int portraitHeight)
    {
        // Case 1: Both eyes detected (best centering)
        if (landmarks.BothEyesDetected && landmarks.EyeMidpoint.HasValue)
        {
            var eyeMidpoint = landmarks.EyeMidpoint.Value;
            var cropWidth = (int)(portraitWidth * 1.2); // 20% wider than target
            var cropHeight = (int)(portraitHeight * 1.5); // 50% taller than target

            return new Rectangle(
                eyeMidpoint.X - (cropWidth / 2),
                eyeMidpoint.Y - (int)(cropHeight * 0.7), // Face is in top 70%
                cropWidth,
                cropHeight
            );
        }

        // Case 2: One eye detected
        else if (landmarks.LeftEye.HasValue || landmarks.RightEye.HasValue)
        {
            var eye = landmarks.LeftEye ?? landmarks.RightEye ?? new Point(0, 0);

            if (landmarks.FaceCenter.HasValue)
            {
                var cropWidth = (int)(portraitWidth * 1.3);
                var cropHeight = (int)(portraitHeight * 1.6);

                return new Rectangle(
                    eye.X - (cropWidth / 2),
                    eye.Y - (int)(cropHeight * 0.6),
                    cropWidth,
                    cropHeight
                );
            }
        }

        // Case 3: No eyes but face detected
        else if (landmarks.FaceDetected && landmarks.FaceRect.HasValue)
        {
            var faceRect = landmarks.FaceRect.Value;
            var cropWidth = (int)(portraitWidth * 1.5);
            var cropHeight = (int)(portraitHeight * 1.8);

            return new Rectangle(
                faceRect.X - (cropWidth / 2) + (faceRect.Width / 2),
                faceRect.Y - (int)(cropHeight * 0.5),
                cropWidth,
                cropHeight
            );
        }

        // Case 4: No face detected (center crop fallback)
        else
        {
            var cropWidth = (int)(portraitWidth * 2.0);
            var cropHeight = (int)(portraitHeight * 2.0);

            return new Rectangle(
                (imageWidth - cropWidth) / 2,
                (imageHeight - cropHeight) / 2,
                cropWidth,
                cropHeight
            );
        }
    }

    #endregion
}
