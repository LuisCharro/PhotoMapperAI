using System;
using System.IO;
using System.Threading.Tasks;
using PhotoMapperAI.Models;
using PhotoMapperAI.Services.Image;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace PhotoMapperAI.Tests.Services.Image;

public class ImageProcessorTests
{
    [Fact]
    public async Task CropPortraitAsync_ShouldReturnExactRequestedSize()
    {
        // Arrange
        var processor = new ImageProcessor();
        using var source = new Image<Rgba32>(1200, 2000);

        var landmarks = new FaceLandmarks
        {
            FaceDetected = true,
            BothEyesDetected = true,
            FaceRect = new PhotoMapperAI.Models.Rectangle(450, 350, 260, 320),
            LeftEye = new PhotoMapperAI.Models.Point(520, 470),
            RightEye = new PhotoMapperAI.Models.Point(640, 472)
        };

        // Act
        using var portrait = await processor.CropPortraitAsync(source, landmarks, 200, 300);

        // Assert
        Assert.Equal(200, portrait.Width);
        Assert.Equal(300, portrait.Height);
    }

    [Fact]
    public async Task SaveImageAsync_ShouldHonorPngFormat()
    {
        // Arrange
        var processor = new ImageProcessor();
        using var image = new Image<Rgba32>(64, 64);

        var tempPath = Path.Combine(Path.GetTempPath(), $"photomapperai-test-{Guid.NewGuid():N}.png");

        try
        {
            // Act
            await processor.SaveImageAsync(image, tempPath, "png");

            // Assert
            Assert.True(File.Exists(tempPath));

            await using var stream = File.OpenRead(tempPath);
            var signature = new byte[8];
            var read = await stream.ReadAsync(signature, 0, signature.Length);
            Assert.Equal(8, read);

            // PNG signature: 89 50 4E 47 0D 0A 1A 0A
            var pngSignature = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            Assert.Equal(pngSignature, signature);
        }
        finally
        {
            SafeDelete(tempPath);
        }
    }

    [Fact]
    public async Task SaveImageAsync_ShouldFillTransparentPngWithWhiteWhenSavingAsJpg()
    {
        // Why this test is written this way:
        // JPEG is lossy, so checking a single pixel with a very high threshold (e.g. >240) can be flaky across OS/encoders.
        // Instead we check the *average* color of a small region.

        // Arrange
        var processor = new ImageProcessor();

        using var transparentImage = new Image<Rgba32>(100, 100);

        // Left half = opaque red
        for (int y = 0; y < 100; y++)
        {
            for (int x = 0; x < 50; x++)
            {
                transparentImage[x, y] = new Rgba32(255, 0, 0, 255);
            }
        }

        // Right half = fully transparent
        for (int y = 0; y < 100; y++)
        {
            for (int x = 50; x < 100; x++)
            {
                transparentImage[x, y] = new Rgba32(0, 0, 0, 0);
            }
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"photomapperai-test-{Guid.NewGuid():N}.jpg");

        try
        {
            // Act
            await processor.SaveImageAsync(transparentImage, tempPath, "jpg");

            // Assert
            Assert.True(File.Exists(tempPath));

            using var savedImage = SixLabors.ImageSharp.Image.Load<Rgb24>(tempPath);

            // Sample a small region in the middle of each half to avoid edge artifacts
            var leftAvg = GetAverageRgb(savedImage, startX: 20, startY: 40, width: 10, height: 10);
            var rightAvg = GetAverageRgb(savedImage, startX: 70, startY: 40, width: 10, height: 10);

            // Left half should remain strongly red-ish
            Assert.True(leftAvg.R > 180, $"Left region should be red-ish. Avg={leftAvg}");
            Assert.True(leftAvg.G < 90,  $"Left region should be red-ish. Avg={leftAvg}");
            Assert.True(leftAvg.B < 90,  $"Left region should be red-ish. Avg={leftAvg}");

            // Right half should be white-ish (certainly not black)
            Assert.True(rightAvg.R > 210, $"Right region should be white-ish. Avg={rightAvg}");
            Assert.True(rightAvg.G > 210, $"Right region should be white-ish. Avg={rightAvg}");
            Assert.True(rightAvg.B > 210, $"Right region should be white-ish. Avg={rightAvg}");
        }
        finally
        {
            SafeDelete(tempPath);
        }
    }

    [Fact]
    public async Task SaveImageAsync_ShouldNotAffectOpaquePngWhenSavingAsJpg()
    {
        // Arrange
        var processor = new ImageProcessor();

        using var opaqueImage = new Image<Rgba32>(100, 100);

        // Entire image = opaque blue
        for (int y = 0; y < 100; y++)
        {
            for (int x = 0; x < 100; x++)
            {
                opaqueImage[x, y] = new Rgba32(0, 0, 255, 255);
            }
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"photomapperai-test-{Guid.NewGuid():N}.jpg");

        try
        {
            // Act
            await processor.SaveImageAsync(opaqueImage, tempPath, "jpg");

            // Assert
            Assert.True(File.Exists(tempPath));

            using var savedImage = SixLabors.ImageSharp.Image.Load<Rgb24>(tempPath);

            var centerAvg = GetAverageRgb(savedImage, startX: 45, startY: 45, width: 10, height: 10);

            Assert.True(centerAvg.B > 180, $"Image should remain blue-ish. Avg={centerAvg}");
            Assert.True(centerAvg.R < 90,  $"Image should remain blue-ish. Avg={centerAvg}");
            Assert.True(centerAvg.G < 90,  $"Image should remain blue-ish. Avg={centerAvg}");
        }
        finally
        {
            SafeDelete(tempPath);
        }
    }

    [Fact]
    public async Task SaveImageAsync_ShouldHandlePartialTransparencyWhenSavingAsJpg()
    {
        // Arrange
        var processor = new ImageProcessor();

        using var partialTransparentImage = new Image<Rgba32>(100, 100);

        // Entire image = semi-transparent red (50% alpha)
        for (int y = 0; y < 100; y++)
        {
            for (int x = 0; x < 100; x++)
            {
                partialTransparentImage[x, y] = new Rgba32(255, 0, 0, 128);
            }
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"photomapperai-test-{Guid.NewGuid():N}.jpg");

        try
        {
            // Act
            await processor.SaveImageAsync(partialTransparentImage, tempPath, "jpg");

            // Assert
            Assert.True(File.Exists(tempPath));

            using var savedImage = SixLabors.ImageSharp.Image.Load<Rgb24>(tempPath);

            var avg = GetAverageRgb(savedImage, startX: 45, startY: 45, width: 10, height: 10);

            // Red blended over white should look pink-ish:
            // - strong red component
            // - some green/blue from the white background
            Assert.True(avg.R > 170, $"Should have strong red component. Avg={avg}");
            Assert.True(avg.G > 80,  $"Should have some green from white blend. Avg={avg}");
            Assert.True(avg.B > 80,  $"Should have some blue from white blend. Avg={avg}");
        }
        finally
        {
            SafeDelete(tempPath);
        }
    }

    [Fact]
    public async Task ResizeAsync_ShouldResizeToExactDimensions()
    {
        // Arrange
        var processor = new ImageProcessor();
        using var source = new Image<Rgba32>(400, 600);

        // Act
        using var resized = await processor.ResizeAsync(source, 200, 300);

        // Assert
        Assert.Equal(200, resized.Width);
        Assert.Equal(300, resized.Height);
    }

    [Fact]
    public async Task ResizeAsync_ShouldResizeLargerImageToSmaller()
    {
        // Arrange
        var processor = new ImageProcessor();
        using var source = new Image<Rgba32>(800, 1200);

        // Act
        using var resized = await processor.ResizeAsync(source, 100, 150);

        // Assert
        Assert.Equal(100, resized.Width);
        Assert.Equal(150, resized.Height);
    }

    [Fact]
    public async Task ResizeAsync_ShouldResizeSmallerImageToLarger()
    {
        // Arrange
        var processor = new ImageProcessor();
        using var source = new Image<Rgba32>(50, 75);

        // Act
        using var resized = await processor.ResizeAsync(source, 200, 300);

        // Assert
        Assert.Equal(200, resized.Width);
        Assert.Equal(300, resized.Height);
    }

    [Fact]
    public async Task PlaceholderWorkflow_ShouldCopyPlaceholderWithPlayerIdAsFilename()
    {
        // This test verifies the placeholder image workflow:
        // 1. A placeholder image exists (pre-sized to match target dimensions)
        // 2. When a player has no photo, the placeholder is copied
        // 3. The output filename should be the player's ID

        // Arrange - create a pre-sized placeholder (200x300 like default target)
        var processor = new ImageProcessor();
        using var placeholder = new Image<Rgba32>(200, 300);
        
        // Fill with a distinctive color (blue) so we can verify it was copied
        for (int y = 0; y < 300; y++)
        {
            for (int x = 0; x < 200; x++)
            {
                placeholder[x, y] = new Rgba32(0, 0, 255, 255); // Blue
            }
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"photomapperai-placeholder-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var placeholderPath = Path.Combine(tempDir, "placeholder-200x300.jpg");
        var playerId = "12345";
        var expectedOutputPath = Path.Combine(tempDir, $"{playerId}.jpg");

        try
        {
            // Act - save placeholder (simulating what happens when player has no photo)
            await processor.SaveImageAsync(placeholder, expectedOutputPath, "jpg");

            // Assert - file exists with correct name (player ID as filename)
            Assert.True(File.Exists(expectedOutputPath), "Placeholder should be saved with player ID as filename");

            // Verify the content is from our placeholder (blue image)
            using var savedImage = SixLabors.ImageSharp.Image.Load<Rgb24>(expectedOutputPath);
            Assert.Equal(200, savedImage.Width);
            Assert.Equal(300, savedImage.Height);

            // Sample center pixel - should be blue (our placeholder color)
            var centerPixel = savedImage[100, 150];
            Assert.True(centerPixel.R < 50 && centerPixel.G < 50 && centerPixel.B > 200, 
                $"Center pixel should be blue but was RGB({centerPixel.R},{centerPixel.G},{centerPixel.B})");
        }
        finally
        {
            // Cleanup
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup; don't fail the test if the OS temporarily locks the file.
        }
    }

    private static (int R, int G, int B) GetAverageRgb(Image<Rgb24> img, int startX, int startY, int width, int height)
    {
        long sumR = 0;
        long sumG = 0;
        long sumB = 0;
        long count = 0;

        var endX = Math.Min(startX + width, img.Width);
        var endY = Math.Min(startY + height, img.Height);

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                var p = img[x, y];
                sumR += p.R;
                sumG += p.G;
                sumB += p.B;
                count++;
            }
        }

        if (count == 0) return (0, 0, 0);

        return ((int)(sumR / count), (int)(sumG / count), (int)(sumB / count));
    }
}
