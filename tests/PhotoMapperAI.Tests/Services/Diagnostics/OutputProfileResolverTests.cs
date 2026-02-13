using PhotoMapperAI.Services.Diagnostics;

namespace PhotoMapperAI.Tests.Services.Diagnostics;

public class OutputProfileResolverTests
{
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
    public void Resolve_InvalidProfile_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => OutputProfileResolver.Resolve("staging", "/tmp/out"));
        Assert.Contains("Unsupported output profile", ex.Message);
    }
}
