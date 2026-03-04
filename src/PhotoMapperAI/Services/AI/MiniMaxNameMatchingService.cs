using PhotoMapperAI.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhotoMapperAI.Services.AI;

/// <summary>
/// MiniMax name matching service using the Anthropic-compatible API.
/// MiniMax provides GLM models via API with Anthropic-compatible interface.
/// </summary>
public class MiniMaxNameMatchingService : INameMatchingService
{
    private readonly HttpClient _httpClient;
    private readonly string _modelName;
    private readonly double _confidenceThreshold;
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions;

    public MiniMaxNameMatchingService(
        string modelName = "MiniMax-M2.5",
        double confidenceThreshold = 0.9,
        string? apiKey = null,
        string? baseUrl = null)
    {
        apiKey ??= Environment.GetEnvironmentVariable("MINIMAX_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("MINIMAX_API_KEY is missing. MiniMax provider is not configured.");

        _baseUrl = string.IsNullOrWhiteSpace(baseUrl)
            ? (Environment.GetEnvironmentVariable("MINIMAX_BASE_URL")?.TrimEnd('/') ?? "https://api.minimax.io/v1")
            : baseUrl.TrimEnd('/');
        _modelName = modelName;
        _confidenceThreshold = confidenceThreshold;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    public string ModelName => $"minimax:{_modelName}";

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
                    { "provider", "minimax" },
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
            using var response = await _httpClient.PostAsync($"{_baseUrl}/chat/completions", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return NameComparisonResultParser.BuildError(
                    $"MiniMax request failed (HTTP {(int)response.StatusCode}): {Compact(responseBody)}",
                    _confidenceThreshold,
                    BuildMetadata());
            }

            var data = JsonSerializer.Deserialize<MiniMaxResponse>(responseBody, _jsonOptions);
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
        { "provider", "minimax" },
        { "model", _modelName }
    };

    private static string Compact(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "no response body";

        return text.Replace('\n', ' ').Replace('\r', ' ').Trim();
    }

    private class MiniMaxResponse
    {
        [JsonPropertyName("choices")]
        public List<MiniMaxChoice>? Choices { get; set; }

        [JsonPropertyName("usage")]
        public MiniMaxUsage? Usage { get; set; }
    }

    private class MiniMaxChoice
    {
        [JsonPropertyName("message")]
        public MiniMaxMessage? Message { get; set; }
    }

    private class MiniMaxMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private class MiniMaxUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }

}
