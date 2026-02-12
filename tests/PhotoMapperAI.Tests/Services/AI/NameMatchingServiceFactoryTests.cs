using PhotoMapperAI.Services.AI;

namespace PhotoMapperAI.Tests.Services.AI;

public class NameMatchingServiceFactoryTests
{
    [Fact]
    public void Create_WithoutProviderPrefix_DefaultsToOllama()
    {
        var service = NameMatchingServiceFactory.Create("qwen2.5:7b");
        Assert.IsType<OllamaNameMatchingService>(service);
    }

    [Fact]
    public void Create_OllamaProvider_ReturnsOllamaService()
    {
        var service = NameMatchingServiceFactory.Create("ollama:qwen2.5:7b");
        Assert.IsType<OllamaNameMatchingService>(service);
    }

    [Fact]
    public void Create_OpenAIProvider_ReturnsOpenAIService()
    {
        var service = NameMatchingServiceFactory.Create("openai:gpt-4o-mini");
        Assert.IsType<OpenAINameMatchingService>(service);
    }

    [Fact]
    public void Create_AnthropicProvider_ReturnsAnthropicService()
    {
        var service = NameMatchingServiceFactory.Create("anthropic:claude-3-5-sonnet");
        Assert.IsType<AnthropicNameMatchingService>(service);
    }

    [Fact]
    public void Create_ClaudeAliasProvider_ReturnsAnthropicService()
    {
        var service = NameMatchingServiceFactory.Create("claude:claude-3-5-sonnet");
        Assert.IsType<AnthropicNameMatchingService>(service);
    }

    [Fact]
    public void Create_UnknownProviderToken_TreatedAsOllamaModel()
    {
        var service = NameMatchingServiceFactory.Create("qwen2.5:7b:instruct");
        Assert.IsType<OllamaNameMatchingService>(service);
    }

    [Fact]
    public void Create_EmptyModel_Throws()
    {
        Assert.Throws<ArgumentException>(() => NameMatchingServiceFactory.Create(" "));
    }

    [Fact]
    public void Create_ProviderWithoutModel_Throws()
    {
        Assert.Throws<ArgumentException>(() => NameMatchingServiceFactory.Create("openai:"));
    }
}
