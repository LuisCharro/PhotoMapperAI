using PhotoMapperAI.Models;

namespace PhotoMapperAI.Services.AI;

/// <summary>
/// Placeholder Anthropic implementation for name matching.
/// </summary>
public class AnthropicNameMatchingService : INameMatchingService
{
    private readonly string _modelName;
    private readonly double _confidenceThreshold;

    public AnthropicNameMatchingService(string modelName = "claude-3-5-sonnet", double confidenceThreshold = 0.9)
    {
        _modelName = modelName;
        _confidenceThreshold = confidenceThreshold;
    }

    public string ModelName => $"anthropic:{_modelName}";

    public Task<MatchResult> CompareNamesAsync(string name1, string name2)
    {
        var keyPresent = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));
        var message = keyPresent
            ? "Anthropic provider wiring not implemented yet in this build."
            : "ANTHROPIC_API_KEY is missing. Anthropic provider is not configured.";

        return Task.FromResult(new MatchResult
        {
            Confidence = 0,
            IsMatch = false,
            Metadata = new Dictionary<string, string>
            {
                { "provider", "anthropic" },
                { "model", _modelName },
                { "threshold", _confidenceThreshold.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) },
                { "error", message }
            }
        });
    }

    public async Task<List<MatchResult>> CompareNamesBatchAsync(string baseName, List<string> candidateNames)
    {
        var results = new List<MatchResult>(candidateNames.Count);
        foreach (var candidate in candidateNames)
        {
            results.Add(await CompareNamesAsync(baseName, candidate));
        }

        return results;
    }
}
