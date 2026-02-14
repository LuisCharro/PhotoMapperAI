using PhotoMapperAI.Services.Diagnostics;

namespace PhotoMapperAI.Tests.Services.Diagnostics;

public class SizeProfileLoaderTests
{
    private static string FindInParents(string startDir, string relativePath)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate '{relativePath}' from '{startDir}'.");
    }

    [Fact]
    public void LoadFromFile_ShouldLoadDefaultProfile()
    {
        var profilePath = FindInParents(AppContext.BaseDirectory, Path.Combine("samples", "size_profiles.default.json"));

        var profile = SizeProfileLoader.LoadFromFile(profilePath);

        Assert.NotNull(profile);
        Assert.Equal("legacy-default", profile.Name);
        Assert.NotEmpty(profile.Variants);
        Assert.Equal("small", profile.Variants[0].Key);
        Assert.Equal(34, profile.Variants[0].Width);
        Assert.Equal(50, profile.Variants[0].Height);
    }

    [Fact]
    public void Validate_ShouldThrow_WhenNoVariants()
    {
        var profile = new PhotoMapperAI.Models.SizeProfile
        {
            Name = "invalid",
            Variants = new List<PhotoMapperAI.Models.SizeVariant>()
        };

        var ex = Assert.Throws<InvalidOperationException>(() => SizeProfileLoader.Validate(profile));
        Assert.Contains("at least one size variant", ex.Message);
    }
}
