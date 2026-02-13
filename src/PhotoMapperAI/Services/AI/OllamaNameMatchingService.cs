using PhotoMapperAI.Models;
using System.Globalization;
using System.Text.Json;

namespace PhotoMapperAI.Services.AI;

/// <summary>
/// AI-powered name matching service using Ollama LLMs.
/// Conservative: favors correctness over coverage.
/// </summary>
public class OllamaNameMatchingService : INameMatchingService
{
    private readonly OllamaClient _client;
    private readonly string _modelName;
    private readonly double _confidenceThreshold;
    private readonly JsonSerializerOptions _jsonOptions;

    public OllamaNameMatchingService(
        string ollamaBaseUrl = "http://localhost:11434",
        string modelName = "qwen2.5:7b",
        double confidenceThreshold = 0.9)
    {
        _client = new OllamaClient(ollamaBaseUrl);
        _modelName = modelName;
        _confidenceThreshold = confidenceThreshold;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public string ModelName => _modelName;

    public async Task<MatchResult> CompareNamesAsync(string name1, string name2)
    {
        var prompt = NameComparisonPromptBuilder.Build(name1, name2);

        try
        {
            // Conservative + deterministic.
            var response = await _client.ChatAsync(_modelName, prompt, temperature: 0.0);
            return NameComparisonResultParser.Parse(
                response,
                _confidenceThreshold,
                new Dictionary<string, string>
                {
                    { "provider", "ollama" },
                    { "model", _modelName }
                },
                _jsonOptions);
        }
        catch (OllamaQuotaExceededException)
        {
            // Fail fast: continuing would only produce repeated quota errors.
            throw;
        }
        catch (Exception ex)
        {
            return NameComparisonResultParser.BuildError(
                ex.Message,
                _confidenceThreshold,
                new Dictionary<string, string>
                {
                    { "provider", "ollama" },
                    { "model", _modelName }
                });
        }
    }

    public async Task<List<MatchResult>> CompareNamesBatchAsync(string baseName, List<string> candidateNames)
    {
        var results = new List<MatchResult>();
        foreach (var candidate in candidateNames)
        {
            var result = await CompareNamesAsync(baseName, candidate);
            results.Add(result);
        }

        // Optional extra guard against "wrong remaining candidate":
        // If top is a match but runner-up is close, treat as ambiguous (return no match upstream).
        // Keep as metadata so your caller can decide.
        var ordered = results.OrderByDescending(r => r.Confidence).ToList();
        if (ordered.Count >= 2)
        {
            var top = ordered[0];
            var second = ordered[1];
            if (top.Confidence >= _confidenceThreshold && (top.Confidence - second.Confidence) < 0.07)
            {
                top.Metadata ??= new Dictionary<string, string>();
                top.Metadata["ambiguous_top_two"] = "true";
                top.Metadata["top_minus_second"] = (top.Confidence - second.Confidence).ToString(CultureInfo.InvariantCulture);
            }
        }

        return ordered;
    }


}
