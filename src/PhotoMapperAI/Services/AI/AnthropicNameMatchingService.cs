using PhotoMapperAI.Models;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PhotoMapperAI.Services.AI;

public class AnthropicNameMatchingService : INameMatchingService
{
    private readonly HttpClient _httpClient;
    private readonly string _modelName;
    private readonly double _confidenceThreshold;
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions;

    public AnthropicNameMatchingService(string modelName = "claude-3-5-sonnet", double confidenceThreshold = 0.9)
    {
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("ANTHROPIC_API_KEY is missing. Anthropic provider is not configured.");

        _baseUrl = Environment.GetEnvironmentVariable("ANTHROPIC_BASE_URL")?.TrimEnd('/') ?? "https://api.anthropic.com";
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
                return BuildErrorResult($"Anthropic request failed (HTTP {(int)response.StatusCode}): {Compact(responseBody)}");
            }

            var data = JsonSerializer.Deserialize<AnthropicResponse>(responseBody, _jsonOptions);
            var text = data?.Content?.FirstOrDefault(c => string.Equals(c.Type, "text", StringComparison.OrdinalIgnoreCase))?.Text ?? string.Empty;
            return ParseNameComparisonResponse(text);
        }
        catch (Exception ex)
        {
            return BuildErrorResult(ex.Message);
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

    private MatchResult ParseNameComparisonResponse(string response)
    {
        try
        {
            var start = response.IndexOf('{');
            var end = response.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                var json = response.Substring(start, end - start + 1);
                var data = JsonSerializer.Deserialize<NameComparisonResponse>(json, _jsonOptions);
                if (data != null)
                {
                    var isMatch = data.Confidence >= _confidenceThreshold;
                    return new MatchResult
                    {
                        Confidence = data.Confidence,
                        IsMatch = isMatch,
                        Metadata = new Dictionary<string, string>
                        {
                            { "provider", "anthropic" },
                            { "model", _modelName },
                            { "reason", data.Reason ?? string.Empty },
                            { "raw_json", json },
                            { "model_isMatch", data.IsMatch.ToString() }
                        }
                    };
                }
            }

            var confidenceMatch = Regex.Match(response, @"""confidence"":\s*([0-9\.]+)");
            if (confidenceMatch.Success &&
                double.TryParse(confidenceMatch.Groups[1].Value, CultureInfo.InvariantCulture, out var confidence))
            {
                return new MatchResult
                {
                    Confidence = confidence,
                    IsMatch = confidence >= _confidenceThreshold,
                    Metadata = new Dictionary<string, string>
                    {
                        { "provider", "anthropic" },
                        { "model", _modelName },
                        { "raw_response", response }
                    }
                };
            }
        }
        catch (Exception ex)
        {
            return BuildErrorResult(ex.Message, response);
        }

        return BuildErrorResult("Could not parse model response.", response);
    }

    private MatchResult BuildErrorResult(string error, string? rawResponse = null)
    {
        var metadata = new Dictionary<string, string>
        {
            { "provider", "anthropic" },
            { "model", _modelName },
            { "threshold", _confidenceThreshold.ToString("0.###", CultureInfo.InvariantCulture) },
            { "error", error }
        };
        if (!string.IsNullOrWhiteSpace(rawResponse))
            metadata["raw_response"] = rawResponse;

        return new MatchResult
        {
            Confidence = 0,
            IsMatch = false,
            Metadata = metadata
        };
    }

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
    }

    private class AnthropicContent
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private class NameComparisonResponse
    {
        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("isMatch")]
        public bool IsMatch { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }
}
