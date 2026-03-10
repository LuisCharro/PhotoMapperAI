using PhotoMapperAI.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PhotoMapperAI.Services.Image;

/// <summary>
/// Image processing service using ImageSharp for portrait cropping.
/// </summary>
public class ImageProcessor : IImageProcessor
{
    private const int DefaultPortraitWidth = 200;
    private const int DefaultPortraitHeight = 300;
    private const double EyeLinePercentFromTop = 0.35;
    private const double TargetFaceWidthPercent = 0.40;
    private const double TargetFaceHeightPercent = 0.28;

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
        int portraitHeight,
        int? cropFrameWidth = null,
        int? cropFrameHeight = null,
        CropOffsetPreset? cropOffset = null)
    {
        return await Task.Run(() =>
        {
            // Calculate crop rectangle based on face landmarks
            var cropRect = CalculatePortraitCrop(
                landmarks,
                image.Width,
                image.Height,
                cropFrameWidth ?? portraitWidth,
                cropFrameHeight ?? portraitHeight,
                cropOffset
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

            // Normalize to target aspect ratio first (top-centered) to avoid stretching
            // while still preserving headroom.
            var targetAspectRatio = (double)portraitWidth / portraitHeight;
            var currentAspectRatio = (double)cropped.Width / cropped.Height;

            if (Math.Abs(currentAspectRatio - targetAspectRatio) > 0.01)
            {
                if (currentAspectRatio < targetAspectRatio)
                {
                    // Too narrow: trim height (prefer trimming from bottom by anchoring near top).
                    var desiredHeight = Math.Max(1, (int)Math.Round(cropped.Width / targetAspectRatio));
                    desiredHeight = Math.Min(desiredHeight, cropped.Height);
                    var aspectCropY = 0; // keep top hair/headroom
                    cropped.Mutate(img => img.Crop(new SixLabors.ImageSharp.Rectangle(0, aspectCropY, cropped.Width, desiredHeight)));
                }
                else
                {
                    // Too wide: trim width symmetrically.
                    var desiredWidth = Math.Max(1, (int)Math.Round(cropped.Height * targetAspectRatio));
                    desiredWidth = Math.Min(desiredWidth, cropped.Width);
                    var aspectCropX = Math.Max(0, (cropped.Width - desiredWidth) / 2);
                    cropped.Mutate(img => img.Crop(new SixLabors.ImageSharp.Rectangle(aspectCropX, 0, desiredWidth, cropped.Height)));
                }
            }

            // Final exact resize (aspect already aligned, so no visible distortion).
            if (cropped.Width != portraitWidth || cropped.Height != portraitHeight)
            {
                cropped.Mutate(img => img.Resize(new ResizeOptions
                {
                    Size = new SixLabors.ImageSharp.Size(portraitWidth, portraitHeight),
                    Mode = ResizeMode.Stretch,
                    Sampler = KnownResamplers.Lanczos3,
                    Compand = true
                }));
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
            var normalized = (format ?? "jpg").Trim().ToLowerInvariant();

            switch (normalized)
            {
                case "png":
                    image.Save(outputPath, new PngEncoder());
                    break;

                case "jpg":
                case "jpeg":
                default:
                    // Handle transparency: flatten against white for consistent JPEG output.
                    if (HasTransparency(image))
                    {
                        using var flattened = new SixLabors.ImageSharp.Image<Rgb24>(
                            image.Width,
                            image.Height,
                            SixLabors.ImageSharp.Color.White);

                        flattened.Mutate(ctx => ctx.DrawImage(
                            image,
                            new SixLabors.ImageSharp.Point(0, 0),
                            1f));

                        flattened.Save(outputPath, new JpegEncoder { Quality = 92 });
                    }
                    else
                    {
                        image.Save(outputPath, new JpegEncoder { Quality = 92 });
                    }
                    break;
            }
        });
    }

    /// <summary>
    /// Checks if an image has transparency (alpha channel).
    /// </summary>
    private bool HasTransparency(SixLabors.ImageSharp.Image image)
    {
        if (image is SixLabors.ImageSharp.Image<Rgba32> rgba)
        {
            return HasTransparencyRgba(rgba);
        }

        if (image is SixLabors.ImageSharp.Image<Argb32> argb)
        {
            return HasTransparencyArgb(argb);
        }

        if (image is SixLabors.ImageSharp.Image<Bgra32> bgra)
        {
            return HasTransparencyBgra(bgra);
        }

        return false;
    }

    private static bool HasTransparencyRgba(SixLabors.ImageSharp.Image<Rgba32> image)
    {
        var step = image.Width * image.Height > 10000 ? 5 : 1;

        for (int y = 0; y < image.Height; y += step)
        {
            for (int x = 0; x < image.Width; x += step)
            {
                if (image[x, y].A < 255)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasTransparencyArgb(SixLabors.ImageSharp.Image<Argb32> image)
    {
        var step = image.Width * image.Height > 10000 ? 5 : 1;

        for (int y = 0; y < image.Height; y += step)
        {
            for (int x = 0; x < image.Width; x += step)
            {
                if (image[x, y].A < 255)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasTransparencyBgra(SixLabors.ImageSharp.Image<Bgra32> image)
    {
        var step = image.Width * image.Height > 10000 ? 5 : 1;

        for (int y = 0; y < image.Height; y += step)
        {
            for (int x = 0; x < image.Width; x += step)
            {
                if (image[x, y].A < 255)
                {
                    return true;
                }
            }
        }

        return false;
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

    /// <summary>
    /// Resizes an image to the specified dimensions.
    /// </summary>
    public async Task<SixLabors.ImageSharp.Image> ResizeAsync(SixLabors.ImageSharp.Image image, int targetWidth, int targetHeight)
    {
        return await Task.Run(() =>
        {
            var resized = image.Clone(img => img.Resize(new ResizeOptions
            {
                Size = new SixLabors.ImageSharp.Size(targetWidth, targetHeight),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Lanczos3,
                Compand = true
            }));
            return resized;
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
        int portraitHeight,
        CropOffsetPreset? cropOffset)
    {
        // Keep crop composition anchored to the default portrait framing.
        // Custom dimensions should resize the crop window itself.
        var referenceAspectRatio = (double)DefaultPortraitWidth / DefaultPortraitHeight;
        var widthScale = Math.Max(0.05d, (double)portraitWidth / DefaultPortraitWidth);
        var heightScale = Math.Max(0.05d, (double)portraitHeight / DefaultPortraitHeight);

        int cropWidth, cropHeight;
        int centerX, eyeY;

        // Case 1: Both eyes detected (best centering)
        if (landmarks.BothEyesDetected && landmarks.EyeMidpoint != null && landmarks.FaceRect != null)
        {
            var faceRect = landmarks.FaceRect;

            (cropWidth, cropHeight) = CalculateFaceBasedCropSize(faceRect, referenceAspectRatio, widthScale, heightScale);
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
                (cropWidth, cropHeight) = CalculateFaceBasedCropSize(faceRect, referenceAspectRatio, widthScale, heightScale);
            }
            else
            {
                // Fallback to image-based dimensions
                var baseCropHeight = Math.Max(1, (int)Math.Round(imageHeight * 0.35));
                var baseCropWidth = Math.Max(1, (int)Math.Round(baseCropHeight * referenceAspectRatio));
                cropWidth = Math.Max(1, (int)Math.Round(baseCropWidth * widthScale));
                cropHeight = Math.Max(1, (int)Math.Round(baseCropHeight * heightScale));
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

            (cropWidth, cropHeight) = CalculateFaceBasedCropSize(faceRect, referenceAspectRatio, widthScale, heightScale);
            // Eyes are typically in the upper 40% of the face rectangle
            centerX = faceRect.X + faceRect.Width / 2;
            eyeY = faceRect.Y + (int)(faceRect.Height * 0.40);
        }
        
        // Case 4: No face detected - use upper portion of image (center crop mode)
        else
        {
            // For full-body sports photos, the face is typically in the upper 20% of the image.
            // We want a portrait showing: bit of space + head + neck + bit of chest
            // This is approximately 20-25% of the total image height for full-body photos.
            
            // Use a smaller crop height for proper portrait composition
            var baseCropHeight = Math.Max(1, (int)Math.Round(imageHeight * 0.22));  // 22% of image height
            var baseCropWidth = Math.Max(1, (int)Math.Round(baseCropHeight * referenceAspectRatio));
            cropWidth = Math.Max(1, (int)Math.Round(baseCropWidth * widthScale));
            cropHeight = Math.Max(1, (int)Math.Round(baseCropHeight * heightScale));
            
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
        }
        if (cropHeight > imageHeight)
        {
            cropHeight = imageHeight;
        }
        
        // Calculate crop rectangle
        // Keep more headroom so top hair is not cut (closer to legacy framing).
        var cropX = centerX - (cropWidth / 2);

        // Keep the eye line in the upper third of the portrait for consistent legacy-style framing.
        var cropY = eyeY - (int)Math.Round(cropHeight * EyeLinePercentFromTop);
        
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

        if (cropOffset != null)
        {
            var offsetX = (int)Math.Round(cropWidth * (cropOffset.HorizontalPercent / 100.0));
            var offsetY = (int)Math.Round(cropHeight * (cropOffset.VerticalPercent / 100.0));
            cropX += offsetX;
            cropY += offsetY;

            cropX = Math.Max(0, Math.Min(cropX, imageWidth - cropWidth));
            cropY = Math.Max(0, Math.Min(cropY, imageHeight - cropHeight));
        }

        return new PhotoMapperAI.Models.Rectangle(
            cropX,
            cropY,
            cropWidth,
            cropHeight
        );
    }

    private static (int Width, int Height) CalculateFaceBasedCropSize(
        PhotoMapperAI.Models.Rectangle faceRect,
        double referenceAspectRatio,
        double widthScale,
        double heightScale)
    {
        var minWidth = Math.Max(1, (int)Math.Ceiling(faceRect.Width / TargetFaceWidthPercent));
        var minHeight = Math.Max(1, (int)Math.Ceiling(faceRect.Height / TargetFaceHeightPercent));

        var baseCropHeight = Math.Max(minHeight, (int)Math.Ceiling(minWidth / referenceAspectRatio));
        var baseCropWidth = Math.Max(minWidth, (int)Math.Round(baseCropHeight * referenceAspectRatio));
        var cropWidth = Math.Max(minWidth, (int)Math.Round(baseCropWidth * widthScale));
        var cropHeight = Math.Max(minHeight, (int)Math.Round(baseCropHeight * heightScale));

        return (cropWidth, cropHeight);
    }

    #endregion
}
