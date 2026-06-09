using PhotoMapperAI.Commands;

namespace PhotoMapperAI.Tests.Commands;

public class GeneratePhotosPhotoLookupTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"genphotos-lookup-{Guid.NewGuid():N}");

    public GeneratePhotosPhotoLookupTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            try { Directory.Delete(_dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void FindPlayerPhotoFiles_WcHyphenDelimitedId_FindsPhoto()
    {
        var target = Path.Combine(_dir, "BRA-H-01-M1-PPROFILE-308370-MK.png");
        File.WriteAllText(target, "x");
        File.WriteAllText(Path.Combine(_dir, "BRA-H-10-M1-PPROFILE-314197-MK.png"), "y");

        var result = GeneratePhotosCommandLogic.FindPlayerPhotoFiles(_dir, "308370");

        Assert.Single(result);
        Assert.Equal(target, result[0]);
    }

    [Fact]
    public void FindPlayerPhotoFiles_LegacyIdOnlyFilename_StillFinds()
    {
        var target = Path.Combine(_dir, "308370.jpg");
        File.WriteAllText(target, "x");

        var result = GeneratePhotosCommandLogic.FindPlayerPhotoFiles(_dir, "308370");

        Assert.Single(result);
        Assert.Equal(target, result[0]);
    }

    [Fact]
    public void FindPlayerPhotoFiles_UnderscoreSuffixFilename_StillFinds()
    {
        var target = Path.Combine(_dir, "Joselu_250005193.jpg");
        File.WriteAllText(target, "x");

        var result = GeneratePhotosCommandLogic.FindPlayerPhotoFiles(_dir, "250005193");

        Assert.Single(result);
        Assert.Equal(target, result[0]);
    }
}
