using PhotoMapperAI.Models;

namespace PhotoMapperAI.Services.AI;

/// <summary>
/// Placeholder OpenAI implementation for name matching.
/// </summary>
public class OpenAINameMatchingService : INameMatchingService
{
    private readonly string _modelName;
    private readonly double _confidenceThreshold;

    public OpenAINameMatchingService(string modelName = "gpt-4o-mini", double confidenceThreshold = 0.9)
    {
        _modelName = modelName;
        _confidenceThreshold = confidenceThreshold;
    }

    public string ModelName => $"openai:{_modelName}";

    public Task<MatchResult> CompareNamesAsync(string name1, string name2)
    {
        var keyPresent = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
        var message = keyPresent
            ? "OpenAI provider wiring not implemented yet in this build."
            : "OPENAI_API_KEY is missing. OpenAI provider is not configured.";

        return Task.FromResult(new MatchResult
        {
            Confidence = 0,
            IsMatch = false,
            Metadata = new Dictionary<string, string>
            {
                { "provider", "openai" },
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
