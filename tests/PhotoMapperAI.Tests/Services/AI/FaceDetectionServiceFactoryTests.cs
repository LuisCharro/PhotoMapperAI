using PhotoMapperAI.Services.AI;

namespace PhotoMapperAI.Tests.Services.AI;

/// <summary>
/// Unit tests for FaceDetectionServiceFactory.
/// </summary>
public class FaceDetectionServiceFactoryTests
{
    [Fact]
    public void Create_WithOpenCvModel_ReturnsOpenCvService()
    {
        var service = FaceDetectionServiceFactory.Create("opencv-dnn");

        Assert.IsType<OpenCVDNNFaceDetectionService>(service);
    }

    [Fact]
    public void Create_WithHaarModel_ReturnsHaarService()
    {
        var service = FaceDetectionServiceFactory.Create("haar-cascade");

        Assert.IsType<HaarCascadeFaceDetectionService>(service);
    }

    [Fact]
    public void Create_WithCenterModel_ReturnsCenterFallbackService()
    {
        var service = FaceDetectionServiceFactory.Create("center");

        Assert.IsType<CenterCropFallbackService>(service);
    }

    [Fact]
    public void Create_WithOllamaModel_ReturnsOllamaService()
    {
        var service = FaceDetectionServiceFactory.Create("llava:7b");

        Assert.IsType<OllamaFaceDetectionService>(service);
    }

    [Fact]
    public void Create_WithFallbackChain_ReturnsFallbackService()
    {
        var service = FaceDetectionServiceFactory.Create("llava:7b,qwen3-vl");

        Assert.IsType<FallbackFaceDetectionService>(service);
    }

    [Fact]
    public void Create_UnknownModel_ThrowsWhenFallbackDisabled()
    {
        Assert.Throws<ArgumentException>(() =>
            FaceDetectionServiceFactory.Create("custom-model", fallbackToOllamaOnUnknown: false));
    }

    [Fact]
    public void Create_UnknownModel_ReturnsOllamaWhenFallbackEnabled()
    {
        var service = FaceDetectionServiceFactory.Create("custom-model", fallbackToOllamaOnUnknown: true);

        Assert.IsType<OllamaFaceDetectionService>(service);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_EmptyModel_ThrowsArgumentException(string model)
    {
        Assert.Throws<ArgumentException>(() => FaceDetectionServiceFactory.Create(model));
    }
}
