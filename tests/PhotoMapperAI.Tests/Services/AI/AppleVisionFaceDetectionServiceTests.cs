using PhotoMapperAI.Services.AI;

namespace PhotoMapperAI.Tests.Services.AI;

public class AppleVisionFaceDetectionServiceTests
{
    [Fact(Skip = "Obsolete Apple Vision integration test. Depends on local fixture/runtime assumptions that are not maintained in CI.")]
    public async Task DetectFaceLandmarksAsync_OnMacOS_DetectsFace()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var imagePath = ResolveTestImagePath("4.jpg");
        Assert.True(File.Exists(imagePath), $"Test image not found: {imagePath}");

        var service = new AppleVisionFaceDetectionService();
        var initialized = await service.InitializeAsync();

        Assert.True(initialized);

        var result = await service.DetectFaceLandmarksAsync(imagePath);

        Assert.True(result.FaceDetected);
        Assert.NotNull(result.FaceRect);
        Assert.NotNull(result.FaceCenter);
        Assert.True(result.FaceConfidence > 0);
    }

    private static string ResolveTestImagePath(string fileName)
    {
        var candidates = new List<string>
        {
            Path.Combine(Directory.GetCurrentDirectory(), "tests", "Data", "FaceDetection", fileName),
            Path.Combine(AppContext.BaseDirectory, "tests", "Data", "FaceDetection", fileName)
        };

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            candidates.Add(Path.Combine(current.FullName, "tests", "Data", "FaceDetection", fileName));
            current = current.Parent;
        }

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }
}
