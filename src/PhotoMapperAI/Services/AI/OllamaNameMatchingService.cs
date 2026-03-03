using PhotoMapperAI.Models;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;

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

        // Pre-check: Compare tokens directly for identical case
        var tokens1 = GetTokensFromPrompt(prompt, "name1_core_tokens");
        var tokens2 = GetTokensFromPrompt(prompt, "name2_core_tokens");

        if (TokensAreIdentical(tokens1, tokens2))
        {
            return new MatchResult
            {
                IsMatch = true,
                Confidence = 0.99,
                Metadata = new Dictionary<string, string>
                {
                    { "provider", "ollama" },
                    { "model", _modelName },
                    { "precheck_applied", "true" },
                    { "reason", "Identical token sets (pre-check)" }
                }
            };
        }

        try
        {
            // Conservative + deterministic.
            var chat = await _client.ChatWithUsageAsync(_modelName, prompt, temperature: 0.0);
            return NameComparisonResultParser.Parse(
                chat.Content,
                _confidenceThreshold,
                new Dictionary<string, string>
                {
                    { "provider", "ollama" },
                    { "model", _modelName },
                    { "usage_prompt_tokens", chat.PromptEvalCount.ToString() },
                    { "usage_completion_tokens", chat.EvalCount.ToString() },
                    { "usage_total_tokens", chat.TotalTokens.ToString() },
                    { "usage_total_duration_ns", chat.TotalDurationNs.ToString() },
                    { "usage_prompt_eval_duration_ns", chat.PromptEvalDurationNs.ToString() },
                    { "usage_eval_duration_ns", chat.EvalDurationNs.ToString() },
                    { "usage_load_duration_ns", chat.LoadDurationNs.ToString() }
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


    #region Pre-check helpers

    private static List<string> GetTokensFromPrompt(string prompt, string fieldName)
    {
        // Extract tokens from JSON in prompt
        var match = Regex.Match(prompt, $"\"{fieldName}\":\\s*\\[(.*?)\\]");
        if (!match.Success)
            return new List<string>();

        var tokensJson = match.Groups[1].Value;
        var tokens = new List<string>();

        foreach (var token in tokensJson.Split('"', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = token.Trim();
            if (trimmed != "," && !string.IsNullOrWhiteSpace(trimmed))
                tokens.Add(trimmed);
        }

        return tokens;
    }

    private static bool TokensAreIdentical(List<string> tokens1, List<string> tokens2)
    {
        if (tokens1.Count != tokens2.Count)
            return false;

        var sorted1 = tokens1.OrderBy(t => t).ToList();
        var sorted2 = tokens2.OrderBy(t => t).ToList();

        return sorted1.SequenceEqual(sorted2);
    }

    #endregion
}
