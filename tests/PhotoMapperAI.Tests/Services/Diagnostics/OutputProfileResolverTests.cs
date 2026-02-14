using PhotoMapperAI.Services.Diagnostics;

namespace PhotoMapperAI.Tests.Services.Diagnostics;

public class OutputProfileResolverTests
{
    private static void WithEnv(string key, string? value, Action action)
    {
        var previous = Environment.GetEnvironmentVariable(key);
        try
        {
            Environment.SetEnvironmentVariable(key, value);
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, previous);
        }
    }

    [Fact]
    public void Resolve_TestProfile_UsesFallbackWhenEnvMissing()
    {
        var basePath = "/tmp/portraits";
        var resolved = OutputProfileResolver.Resolve("test", basePath);
        Assert.Equal(Path.Combine(basePath, "test"), resolved);
    }

    [Fact]
    public void Resolve_ProdProfile_UsesFallbackWhenEnvMissing()
    {
        var basePath = "/tmp/portraits";
        var resolved = OutputProfileResolver.Resolve("prod", basePath);
        Assert.Equal(Path.Combine(basePath, "prod"), resolved);
    }

    [Fact]
    public void Resolve_TestProfile_UsesEnvOverrideWhenSet()
    {
        WithEnv("PHOTOMAPPER_OUTPUT_TEST", "/custom/test-output", () =>
        {
            var resolved = OutputProfileResolver.Resolve("test", "/tmp/portraits");
            Assert.Equal("/custom/test-output", resolved);
        });
    }

    [Fact]
    public void Resolve_ProdProfile_UsesEnvOverrideWhenSet()
    {
        WithEnv("PHOTOMAPPER_OUTPUT_PROD", "/custom/prod-output", () =>
        {
            var resolved = OutputProfileResolver.Resolve("prod", "/tmp/portraits");
            Assert.Equal("/custom/prod-output", resolved);
        });
    }

    [Fact]
    public void Resolve_InvalidProfile_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => OutputProfileResolver.Resolve("staging", "/tmp/out"));
        Assert.Contains("Unsupported output profile", ex.Message);
    }
}
