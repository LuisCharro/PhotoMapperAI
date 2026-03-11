using PhotoMapperAI.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhotoMapperAI.Services.AI;

/// <summary>
/// MiniMax name matching service using the Anthropic-compatible API.
/// For Coding Plan subscribers, use: https://api.minimax.io/anthropic/v1/messages
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

        // For Coding Plan, use Anthropic-compatible endpoint
        // Fallback to pay-per-use endpoint if explicitly configured
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            _baseUrl = baseUrl.TrimEnd('/');
        }
        else
        {
            var envBaseUrl = Environment.GetEnvironmentVariable("MINIMAX_BASE_URL");
            _baseUrl = string.IsNullOrWhiteSpace(envBaseUrl)
                ? "https://api.minimax.io/anthropic/v1"  // Anthropic-compatible API (for Coding Plan)
                : envBaseUrl.TrimEnd('/');
        }
        
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

        // Use Anthropic-compatible API format for Coding Plan
        var requestBody = new
        {
            model = _modelName,
            max_tokens = 1024,
            messages = new[]
            {
                new { role = "user", content = new[] { new { type = "text", text = prompt } } }
            },
            temperature = 1.0  // Required for Anthropic API (range (0.0, 1.0])
        };

        try
        {
            var json = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync($"{_baseUrl}/messages", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return NameComparisonResultParser.BuildError(
                    $"MiniMax request failed (HTTP {(int)response.StatusCode}): {Compact(responseBody)}",
                    _confidenceThreshold,
                    BuildMetadata());
            }

            var data = JsonSerializer.Deserialize<AnthropicResponse>(responseBody, _jsonOptions);
            
            // Extract text content from Anthropic response format
            var text = ExtractTextContent(data);
            
            var metadata = BuildMetadata();
            if (data?.Usage != null)
            {
                metadata["usage_input_tokens"] = data.Usage.InputTokens.ToString();
                metadata["usage_output_tokens"] = data.Usage.OutputTokens.ToString();
                metadata["usage_total_tokens"] = (data.Usage.InputTokens + data.Usage.OutputTokens).ToString();
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

        var results = new List<MatchResult>(comparisons.Count);
        var usageCalls = 0;
        var promptTokens = 0;
        var completionTokens = 0;
        var totalTokens = 0;

        foreach (var comparison in comparisons)
        {
            var match = await CompareNamesAsync(comparison.Name1, comparison.Name2);
            results.Add(match);
            AddUsage(match, ref usageCalls, ref promptTokens, ref completionTokens, ref totalTokens);
        }

        return new NameComparisonBatchResult(results, usageCalls, promptTokens, completionTokens, totalTokens);
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

    private static void AddUsage(
        MatchResult match,
        ref int usageCalls,
        ref int promptTokens,
        ref int completionTokens,
        ref int totalTokens)
    {
        if (match.Metadata == null || match.Metadata.Count == 0)
            return;

        var hasPrompt = TryGetInt(match.Metadata, "usage_prompt_tokens", out var prompt)
            || TryGetInt(match.Metadata, "usage_input_tokens", out prompt);
        var hasCompletion = TryGetInt(match.Metadata, "usage_completion_tokens", out var completion)
            || TryGetInt(match.Metadata, "usage_output_tokens", out completion);
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

    #endregion

    private Dictionary<string, string> BuildMetadata() => new()
    {
        { "provider", "minimax" },
        { "model", _modelName },
        { "api_endpoint", _baseUrl }
    };

    private static string ExtractTextContent(AnthropicResponse? response)
    {
        if (response?.Content == null)
            return string.Empty;

        foreach (var block in response.Content)
        {
            if (block.Type == "text" && !string.IsNullOrWhiteSpace(block.Text))
                return block.Text;
        }

        return string.Empty;
    }

    private static string Compact(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "no response body";

        return text.Replace('\n', ' ').Replace('\r', ' ').Trim();
    }

    // Anthropic-compatible response classes
    private class AnthropicResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("content")]
        public List<AnthropicContentBlock>? Content { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("stop_reason")]
        public string? StopReason { get; set; }

        [JsonPropertyName("usage")]
        public AnthropicUsage? Usage { get; set; }
    }

    private class AnthropicContentBlock
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("thinking")]
        public string? Thinking { get; set; }
    }

    private class AnthropicUsage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; set; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; set; }
    }

}
