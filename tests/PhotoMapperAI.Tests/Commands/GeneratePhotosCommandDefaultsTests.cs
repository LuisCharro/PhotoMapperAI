using PhotoMapperAI.Commands;

namespace PhotoMapperAI.Tests.Commands;

public class GeneratePhotosCommandDefaultsTests
{
    [Fact]
    public void DefaultFaceDetection_IsPlatformAppropriateAndRobust()
    {
        var command = new GeneratePhotosCommand();

        if (OperatingSystem.IsMacOS())
        {
            // macOS uses the native detector.
            Assert.Equal("apple-vision", command.FaceDetection);
        }
        else
        {
            // Windows/Linux: opencv-dnn is the strongest single detector on
            // high-resolution photos, with opencv-yunet rescuing its misses and
            // center as a last-resort fallback.
            Assert.Equal("opencv-dnn,opencv-yunet,center", command.FaceDetection);
        }
    }
}
