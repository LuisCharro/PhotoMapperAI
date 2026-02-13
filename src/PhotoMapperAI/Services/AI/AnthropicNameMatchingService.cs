using PhotoMapperAI.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhotoMapperAI.Services.AI;

public class AnthropicNameMatchingService : INameMatchingService
{
    private readonly HttpClient _httpClient;
    private readonly string _modelName;
    private readonly double _confidenceThreshold;
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions;

    public AnthropicNameMatchingService(
        string modelName = "claude-3-5-sonnet",
        double confidenceThreshold = 0.9,
        string? apiKey = null,
        string? baseUrl = null)
    {
        apiKey ??= Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("ANTHROPIC_API_KEY is missing. Anthropic provider is not configured.");

        _baseUrl = string.IsNullOrWhiteSpace(baseUrl)
            ? (Environment.GetEnvironmentVariable("ANTHROPIC_BASE_URL")?.TrimEnd('/') ?? "https://api.anthropic.com")
            : baseUrl.TrimEnd('/');
        _modelName = modelName;
        _confidenceThreshold = confidenceThreshold;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    public string ModelName => $"anthropic:{_modelName}";

    public async Task<MatchResult> CompareNamesAsync(string name1, string name2)
    {
        var prompt = NameComparisonPromptBuilder.Build(name1, name2);

        var requestBody = new
        {
            model = _modelName,
            max_tokens = 300,
            temperature = 0.0,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        try
        {
            var json = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync($"{_baseUrl}/v1/messages", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return NameComparisonResultParser.BuildError(
                    $"Anthropic request failed (HTTP {(int)response.StatusCode}): {Compact(responseBody)}",
                    _confidenceThreshold,
                    BuildMetadata());
            }

            var data = JsonSerializer.Deserialize<AnthropicResponse>(responseBody, _jsonOptions);
            var text = data?.Content?.FirstOrDefault(c => string.Equals(c.Type, "text", StringComparison.OrdinalIgnoreCase))?.Text ?? string.Empty;
            var metadata = BuildMetadata();
            if (data?.Usage != null)
            {
                metadata["usage_prompt_tokens"] = data.Usage.InputTokens.ToString();
                metadata["usage_completion_tokens"] = data.Usage.OutputTokens.ToString();
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

    private Dictionary<string, string> BuildMetadata() => new()
    {
        { "provider", "anthropic" },
        { "model", _modelName }
    };

    private static string Compact(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "no response body";

        return text.Replace('\n', ' ').Replace('\r', ' ').Trim();
    }

    private class AnthropicResponse
    {
        [JsonPropertyName("content")]
        public List<AnthropicContent>? Content { get; set; }

        [JsonPropertyName("usage")]
        public AnthropicUsage? Usage { get; set; }
    }

    private class AnthropicContent
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private class AnthropicUsage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; set; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; set; }
    }

}
