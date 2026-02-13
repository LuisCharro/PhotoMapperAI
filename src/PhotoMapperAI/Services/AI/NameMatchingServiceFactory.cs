namespace PhotoMapperAI.Services.AI;

/// <summary>
/// Factory for creating name matching services from a provider/model identifier.
/// </summary>
public static class NameMatchingServiceFactory
{
    /// <summary>
    /// Creates a name matching service.
    /// Supported formats:
    /// - "qwen2.5:7b" (defaults to Ollama provider)
    /// - "ollama:qwen2.5:7b"
    /// - "openai:gpt-4o-mini"
    /// - "anthropic:claude-3-5-sonnet"
    /// </summary>
    public static INameMatchingService Create(
        string modelIdentifier,
        double confidenceThreshold = 0.9,
        string? openAiApiKey = null,
        string? anthropicApiKey = null)
    {
        if (string.IsNullOrWhiteSpace(modelIdentifier))
            throw new ArgumentException("Name matching model cannot be empty.", nameof(modelIdentifier));

        var normalized = modelIdentifier.Trim();
        var (provider, modelName) = ParseProviderAndModel(normalized);

        return provider switch
        {
            "ollama" => new OllamaNameMatchingService(modelName: modelName, confidenceThreshold: confidenceThreshold),
            "openai" => new OpenAINameMatchingService(
                modelName: modelName,
                confidenceThreshold: confidenceThreshold,
                apiKey: openAiApiKey),
            "anthropic" or "claude" => new AnthropicNameMatchingService(
                modelName: modelName,
                confidenceThreshold: confidenceThreshold,
                apiKey: anthropicApiKey),
            _ => throw new ArgumentException($"Unknown name matching provider: {provider}")
        };
    }

    private static (string Provider, string ModelName) ParseProviderAndModel(string modelIdentifier)
    {
        var separatorIndex = modelIdentifier.IndexOf(':');
        if (separatorIndex <= 0)
            return ("ollama", modelIdentifier);

        var possibleProvider = modelIdentifier[..separatorIndex].Trim().ToLowerInvariant();
        var remainder = modelIdentifier[(separatorIndex + 1)..].Trim();

        var knownProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ollama",
            "openai",
            "anthropic",
            "claude"
        };

        if (!knownProviders.Contains(possibleProvider))
            return ("ollama", modelIdentifier);

        if (string.IsNullOrWhiteSpace(remainder))
            throw new ArgumentException($"Model name is missing for provider '{possibleProvider}'.");

        return (possibleProvider, remainder);
    }
}
