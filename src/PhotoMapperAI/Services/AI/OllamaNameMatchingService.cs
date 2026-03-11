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

    public async Task<NameComparisonBatchResult> CompareNamePairsBatchAsync(IReadOnlyList<NameComparisonPair> comparisons)
    {
        if (comparisons.Count == 0)
            return new NameComparisonBatchResult(new List<MatchResult>(), 0, 0, 0, 0);

        var results = new MatchResult[comparisons.Count];
        var pending = new List<NameComparisonPromptBuilder.BatchComparison>(comparisons.Count);

        for (var i = 0; i < comparisons.Count; i++)
        {
            var comparison = comparisons[i];
            var tokens1 = NameComparisonPromptBuilder.ToCoreTokens(comparison.Name1);
            var tokens2 = NameComparisonPromptBuilder.ToCoreTokens(comparison.Name2);
            if (TokensAreIdentical(tokens1, tokens2))
            {
                results[i] = new MatchResult
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
            else
            {
                pending.Add(new NameComparisonPromptBuilder.BatchComparison(i, comparison.Name1, comparison.Name2));
            }
        }

        if (pending.Count == 0)
            return new NameComparisonBatchResult(results.ToList(), 0, 0, 0, 0);

        var prompt = NameComparisonPromptBuilder.BuildBatch(pending);

        try
        {
            var chat = await _client.ChatWithUsageAsync(_modelName, prompt, temperature: 0.0);
            var metadata = new Dictionary<string, string>
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
            };

            var parsed = NameComparisonBatchResultParser.Parse(
                chat.Content,
                _confidenceThreshold,
                metadata,
                _jsonOptions,
                out var parseError,
                out var rawJson);

            if (!string.IsNullOrWhiteSpace(parseError))
            {
                return await FallbackToIndividualAsync(results, comparisons, pending, rawJson ?? chat.Content);
            }

            foreach (var item in parsed)
            {
                results[item.Key] = item.Value;
            }

            var usageCalls = 1;
            var promptTokens = chat.PromptEvalCount;
            var completionTokens = chat.EvalCount;
            var totalTokens = chat.TotalTokens;

            var missing = pending.Where(p => results[p.Index] == null).ToList();
            if (missing.Count > 0)
            {
                foreach (var missingItem in missing)
                {
                    var comparison = comparisons[missingItem.Index];
                    var match = await CompareNamesAsync(comparison.Name1, comparison.Name2);
                    results[missingItem.Index] = match;
                    AddUsage(match, ref usageCalls, ref promptTokens, ref completionTokens, ref totalTokens);
                }
            }

            FillMissingResults(results, comparisons.Count, metadata, _confidenceThreshold, rawJson ?? chat.Content);

            return new NameComparisonBatchResult(
                results.ToList(),
                usageCalls,
                promptTokens,
                completionTokens,
                totalTokens);
        }
        catch (OllamaQuotaExceededException)
        {
            throw;
        }
        catch (Exception)
        {
            return await FallbackToIndividualAsync(results, comparisons, pending, "Ollama batch call failed.");
        }
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

    private async Task<NameComparisonBatchResult> FallbackToIndividualAsync(
        MatchResult[] results,
        IReadOnlyList<NameComparisonPair> comparisons,
        List<NameComparisonPromptBuilder.BatchComparison> pending,
        string error)
    {
        var usageCalls = 0;
        var promptTokens = 0;
        var completionTokens = 0;
        var totalTokens = 0;

        foreach (var pendingItem in pending)
        {
            var comparison = comparisons[pendingItem.Index];
            var match = await CompareNamesAsync(comparison.Name1, comparison.Name2);
            results[pendingItem.Index] = match;
            AddUsage(match, ref usageCalls, ref promptTokens, ref completionTokens, ref totalTokens);
        }

        FillMissingResults(results, comparisons.Count, BuildMetadata(), _confidenceThreshold, error);

        return new NameComparisonBatchResult(results.ToList(), usageCalls, promptTokens, completionTokens, totalTokens);
    }

    private static void AddUsage(
        MatchResult match,
        ref int usageCalls,
        ref int promptTokens,
        ref int completionTokens,
        ref int totalTokens)
    {
        if (match.Metadata == null || match.Metadata.Count == 0)
            return;

        var hasPrompt = TryGetInt(match.Metadata, "usage_prompt_tokens", out var prompt);
        var hasCompletion = TryGetInt(match.Metadata, "usage_completion_tokens", out var completion);
        var hasTotal = TryGetInt(match.Metadata, "usage_total_tokens", out var total);
        if (!hasPrompt && !hasCompletion && !hasTotal)
            return;

        usageCalls++;
        promptTokens += hasPrompt ? prompt : 0;
        completionTokens += hasCompletion ? completion : 0;
        totalTokens += hasTotal ? total : (hasPrompt ? prompt : 0) + (hasCompletion ? completion : 0);
    }

    private static bool TryGetInt(IDictionary<string, string> metadata, string key, out int value)
    {
        value = 0;
        return metadata.TryGetValue(key, out var raw) && int.TryParse(raw, out value);
    }

    private static void FillMissingResults(
        MatchResult[] results,
        int totalCount,
        Dictionary<string, string> baseMetadata,
        double confidenceThreshold,
        string rawResponse)
    {
        for (var i = 0; i < totalCount; i++)
        {
            if (results[i] != null)
                continue;

            results[i] = NameComparisonBatchResultParser.BuildError(
                "Missing batch result.",
                confidenceThreshold,
                baseMetadata,
                rawResponse);
        }
    }

    private Dictionary<string, string> BuildMetadata() => new()
    {
        { "provider", "ollama" },
        { "model", _modelName }
    };

    #endregion
}
