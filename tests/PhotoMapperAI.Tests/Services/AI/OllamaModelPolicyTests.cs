using PhotoMapperAI.Services.AI;

namespace PhotoMapperAI.Tests.Services.AI;

public class OllamaModelPolicyTests
{
    [Theory]
    [InlineData("gemini-3-flash-preview:cloud", true)]
    [InlineData("kimi-k2.5:cloud", true)]
    [InlineData("qwen3:8b", false)]
    [InlineData("llava:7b", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsCloudModel_WorksAsExpected(string? modelName, bool expected)
    {
        var actual = OllamaModelPolicy.IsCloudModel(modelName);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GetLocalModelsToUnload_RequiredLocal_IgnoresCloudAndUnloadsOtherLocal()
    {
        var running = new List<string>
        {
            "qwen3:8b",
            "gemini-3-flash-preview:cloud",
            "llava:7b"
        };

        var toUnload = OllamaModelPolicy.GetLocalModelsToUnload(running, "qwen3:8b");

        Assert.Single(toUnload);
        Assert.Equal("llava:7b", toUnload[0]);
    }

    [Fact]
    public void GetLocalModelsToUnload_RequiredCloud_UnloadsNothing()
    {
        var running = new List<string>
        {
            "qwen3:8b",
            "llava:7b",
            "gemini-3-flash-preview:cloud"
        };

        var toUnload = OllamaModelPolicy.GetLocalModelsToUnload(running, "gemini-3-flash-preview:cloud");

        Assert.Empty(toUnload);
    }

    [Fact]
    public void GetLocalModelsToUnload_OnlyCloudRunning_UnloadsNothing()
    {
        var running = new List<string>
        {
            "gemini-3-flash-preview:cloud",
            "qwen3-coder-next:cloud"
        };

        var toUnload = OllamaModelPolicy.GetLocalModelsToUnload(running, "qwen3:8b");

        Assert.Empty(toUnload);
    }

    [Fact]
    public void GetLocalModelsToUnload_RequiredAlreadyOnlyLocal_UnloadsNothing()
    {
        var running = new List<string>
        {
            "qwen3:8b"
        };

        var toUnload = OllamaModelPolicy.GetLocalModelsToUnload(running, "qwen3:8b");

        Assert.Empty(toUnload);
    }
}
