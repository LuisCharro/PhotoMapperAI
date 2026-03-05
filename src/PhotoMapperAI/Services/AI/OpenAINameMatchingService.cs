using PhotoMapperAI.Models;
using System.Net.Http.Headers;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhotoMapperAI.Services.AI;

public class OpenAINameMatchingService : INameMatchingService
{
    private readonly HttpClient _httpClient;
    private readonly string _modelName;
    private readonly double _confidenceThreshold;
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions;

    public OpenAINameMatchingService(
        string modelName = "gpt-5-mini",
        double confidenceThreshold = 0.9,
        string? apiKey = null,
        string? baseUrl = null)
    {
        apiKey ??= Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("OPENAI_API_KEY is missing. OpenAI provider is not configured.");

        _baseUrl = string.IsNullOrWhiteSpace(baseUrl)
            ? (Environment.GetEnvironmentVariable("OPENAI_BASE_URL")?.TrimEnd('/') ?? "https://api.openai.com")
            : baseUrl.TrimEnd('/');
        _modelName = modelName;
        _confidenceThreshold = confidenceThreshold;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    public string ModelName => $"openai:{_modelName}";

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
                    { "provider", "openai" },
                    { "model", _modelName },
                    { "precheck_applied", "true" },
                    { "reason", "Identical token sets (pre-check)" }
                }
            };
        }

        var requestBody = new
        {
            model = _modelName,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature = 0.0
        };

        try
        {
            var json = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync($"{_baseUrl}/v1/chat/completions", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return NameComparisonResultParser.BuildError(
                    $"OpenAI request failed (HTTP {(int)response.StatusCode}): {Compact(responseBody)}",
                    _confidenceThreshold,
                    BuildMetadata());
            }

            var data = JsonSerializer.Deserialize<OpenAIResponse>(responseBody, _jsonOptions);
            var text = data?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
            var metadata = BuildMetadata();
            if (data?.Usage != null)
            {
                metadata["usage_prompt_tokens"] = data.Usage.PromptTokens.ToString();
                metadata["usage_completion_tokens"] = data.Usage.CompletionTokens.ToString();
                metadata["usage_total_tokens"] = data.Usage.TotalTokens.ToString();
            }

            return NameComparisonResultParser.Parse(text, _confidenceThreshold, metadata, _jsonOptions);
        }
        catch (Exception ex)
        {
            return NameComparisonResultParser.BuildError(ex.Message, _confidenceThreshold, BuildMetadata());
        }
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
                        { "provider", "openai" },
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
        var requestBody = new
        {
            model = _modelName,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature = 0.0
        };

        try
        {
            var json = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync($"{_baseUrl}/v1/chat/completions", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return await FallbackToIndividualAsync(results, comparisons, pending, responseBody);
            }

            var data = JsonSerializer.Deserialize<OpenAIResponse>(responseBody, _jsonOptions);
            var text = data?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
            var metadata = BuildMetadata();
            if (data?.Usage != null)
            {
                metadata["usage_prompt_tokens"] = data.Usage.PromptTokens.ToString();
                metadata["usage_completion_tokens"] = data.Usage.CompletionTokens.ToString();
                metadata["usage_total_tokens"] = data.Usage.TotalTokens.ToString();
            }

            var parsed = NameComparisonBatchResultParser.Parse(
                text,
                _confidenceThreshold,
                metadata,
                _jsonOptions,
                out var parseError,
                out var rawJson);

            if (!string.IsNullOrWhiteSpace(parseError))
            {
                return await FallbackToIndividualAsync(results, comparisons, pending, rawJson ?? text);
            }

            foreach (var item in parsed)
            {
                results[item.Key] = item.Value;
            }

            var usageCalls = 1;
            var promptTokens = data?.Usage?.PromptTokens ?? 0;
            var completionTokens = data?.Usage?.CompletionTokens ?? 0;
            var totalTokens = data?.Usage?.TotalTokens ?? 0;

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

            FillMissingResults(results, comparisons.Count, metadata, _confidenceThreshold, rawJson ?? text);

            return new NameComparisonBatchResult(
                results.ToList(),
                usageCalls,
                promptTokens,
                completionTokens,
                totalTokens);
        }
        catch
        {
            return await FallbackToIndividualAsync(results, comparisons, pending, "OpenAI batch call failed.");
        }
    }

    #region Pre-check helpers

    private static List<string> GetTokensFromPrompt(string prompt, string fieldName)
    {
        // Extract tokens from JSON in prompt
        var match = System.Text.RegularExpressions.Regex.Match(prompt, $"\"{fieldName}\":\\s*\\[(.*?)\\]");
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

    private Dictionary<string, string> BuildMetadata() => new()
    {
        { "provider", "openai" },
        { "model", _modelName }
    };

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

    private static string Compact(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "no response body";

        return text.Replace('\n', ' ').Replace('\r', ' ').Trim();
    }

    private class OpenAIResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenAIChoice>? Choices { get; set; }

        [JsonPropertyName("usage")]
        public OpenAIUsage? Usage { get; set; }
    }

    private class OpenAIChoice
    {
        [JsonPropertyName("message")]
        public OpenAIMessage? Message { get; set; }
    }

    private class OpenAIMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private class OpenAIUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }

}
