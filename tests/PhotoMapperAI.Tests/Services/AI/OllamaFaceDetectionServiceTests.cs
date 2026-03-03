using System.Reflection;
using PhotoMapperAI.Models;
using PhotoMapperAI.Services.AI;
using Xunit;

namespace PhotoMapperAI.Tests.Services.AI;

public class OllamaFaceDetectionServiceTests
{
    [Fact]
    public void ParseFaceDetectionResponse_ShouldRejectOutOfBoundsFaceRect()
    {
        var response = """
        {
          "faceDetected": true,
          "bothEyesDetected": false,
          "faceRect": { "x": -10, "y": 20, "width": 120, "height": 160 },
          "confidence": 0.85
        }
        """;

        var result = InvokeParse(response, 400, 800);

        Assert.False(result.FaceDetected);
        Assert.True(result.Metadata.ContainsKey("validation_error"));
        Assert.Equal("face_rect_out_of_bounds", result.Metadata["validation_error"]);
    }

    [Fact]
    public void ParseFaceDetectionResponse_ShouldRejectInvalidEyeOrder()
    {
        var response = """
        {
          "faceDetected": true,
          "bothEyesDetected": true,
          "faceRect": { "x": 100, "y": 100, "width": 120, "height": 160 },
          "leftEye": { "x": 180, "y": 170 },
          "rightEye": { "x": 130, "y": 170 },
          "confidence": 0.90
        }
        """;

        var result = InvokeParse(response, 400, 800);

        Assert.False(result.FaceDetected);
        Assert.True(result.Metadata.ContainsKey("validation_error"));
        Assert.Equal("eye_order_invalid", result.Metadata["validation_error"]);
    }

    [Fact]
    public void ParseFaceDetectionResponse_ShouldAcceptValidFaceRect()
    {
        var response = """
        {
          "faceDetected": true,
          "bothEyesDetected": true,
          "faceRect": { "x": 120, "y": 140, "width": 100, "height": 130 },
          "leftEye": { "x": 150, "y": 185 },
          "rightEye": { "x": 190, "y": 186 },
          "faceCenter": { "x": 170, "y": 205 },
          "confidence": 0.93
        }
        """;

        var result = InvokeParse(response, 400, 800);

        Assert.True(result.FaceDetected);
        Assert.NotNull(result.FaceRect);
        Assert.Equal(100, result.FaceRect!.Width);
        Assert.Equal(130, result.FaceRect.Height);
    }

    private static FaceLandmarks InvokeParse(string response, int width, int height)
    {
        var type = typeof(OllamaFaceDetectionService);
        var method = type.GetMethod("ParseFaceDetectionResponse", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var result = method!.Invoke(null, new object[] { response, width, height });
        Assert.NotNull(result);

        return Assert.IsType<FaceLandmarks>(result);
    }
}
