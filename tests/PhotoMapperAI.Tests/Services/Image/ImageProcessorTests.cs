using PhotoMapperAI.Models;
using PhotoMapperAI.Services.Image;
using SixLabors.ImageSharp;
using Xunit;

namespace PhotoMapperAI.Tests.Services.Image;

public class ImageProcessorTests
{
    [Fact]
    public async Task CropPortraitAsync_ShouldReturnExactRequestedSize()
    {
        // Arrange
        var processor = new ImageProcessor();
        using var source = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(1200, 2000);

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
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(64, 64);

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
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
