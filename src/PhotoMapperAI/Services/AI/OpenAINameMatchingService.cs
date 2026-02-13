using PhotoMapperAI.Models;
using System.Net.Http.Headers;
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

    public OpenAINameMatchingService(string modelName = "gpt-4o-mini", double confidenceThreshold = 0.9)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("OPENAI_API_KEY is missing. OpenAI provider is not configured.");

        _baseUrl = Environment.GetEnvironmentVariable("OPENAI_BASE_URL")?.TrimEnd('/') ?? "https://api.openai.com";
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
            return NameComparisonResultParser.Parse(text, _confidenceThreshold, BuildMetadata(), _jsonOptions);
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
        { "provider", "openai" },
        { "model", _modelName }
    };

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

}
