using PhotoMapperAI.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace PhotoMapperAI.Services.AI;

/// <summary>
/// AI-powered name matching service using Ollama LLMs.
/// </summary>
public class OllamaNameMatchingService : INameMatchingService
{
    private readonly OllamaClient _client;
    private readonly string _modelName;
    private readonly double _confidenceThreshold;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Creates a new Ollama name matching service.
    /// </summary>
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

    /// <summary>
    /// Compares two names using Ollama LLM.
    /// </summary>
    public async Task<MatchResult> CompareNamesAsync(string name1, string name2)
    {
        var prompt = BuildNameComparisonPrompt(name1, name2);
        
        try
        {
            var response = await _client.ChatAsync(_modelName, prompt, temperature: 0.1);
            return ParseNameComparisonResponse(response);
        }
        catch (Exception ex)
        {
            return new MatchResult
            {
                Confidence = 0,
                IsMatch = false,
                Metadata = new Dictionary<string, string> { { "error", ex.Message } }
            };
        }
    }

    /// <summary>
    /// Batch compares names.
    /// </summary>
    public async Task<List<MatchResult>> CompareNamesBatchAsync(string baseName, List<string> candidateNames)
    {
        var results = new List<MatchResult>();
        foreach (var candidate in candidateNames)
        {
            var result = await CompareNamesAsync(baseName, candidate);
            results.Add(result);
        }
        return results.OrderByDescending(r => r.Confidence).ToList();
    }

    private static string BuildNameComparisonPrompt(string name1, string name2)
    {
        return $@"You are a name matching expert. Compare these two player names and determine if they refer to the same person.

Consider:
- Different name orders (First Last vs Last First)
- Missing middle names or initials
- Transliteration differences (á vs a, ü vs u)
- Common nicknames

Name 1: {name1}
Name 2: {name2}

IMPORTANT: Return ONLY valid JSON in this exact format, no other text:
{{
  ""confidence"": 0.95,
  ""isMatch"": true,
  ""reason"": ""brief explanation""
}}

Where:
- confidence: number from 0.0 to 1.0 (1.0 = certain same person)
- isMatch: true if confidence >= 0.9, false otherwise
- reason: short 1-sentence explanation";
    }

    private MatchResult ParseNameComparisonResponse(string response)
    {
        try
        {
            // Simple JSON extraction (handles markdown blocks too)
            var start = response.IndexOf('{');
            var end = response.LastIndexOf('}');
            
            if (start >= 0 && end > start)
            {
                var json = response.Substring(start, end - start + 1);
                
                // Remove potential markdown language specifier if it got caught in the substring
                if (json.StartsWith("json")) json = json.Substring(4).Trim();
                
                var data = JsonSerializer.Deserialize<NameComparisonResponse>(json, _jsonOptions);
                
                if (data != null)
                {
                    return new MatchResult
                    {
                        Confidence = data.Confidence,
                        IsMatch = data.IsMatch || data.Confidence >= _confidenceThreshold,
                        Metadata = new Dictionary<string, string>
                        {
                            { "reason", data.Reason },
                            { "raw_json", json }
                        }
                    };
                }
            }

            // Fallback: search for confidence number in text
            var confidenceMatch = System.Text.RegularExpressions.Regex.Match(response, @"""confidence"":\s*([0-9\.]+)");
            if (confidenceMatch.Success && double.TryParse(confidenceMatch.Groups[1].Value, CultureInfo.InvariantCulture, out var confidence))
            {
                return new MatchResult
                {
                    Confidence = confidence,
                    IsMatch = confidence >= _confidenceThreshold,
                    Metadata = new Dictionary<string, string> { { "method", "regex-extract" } }
                };
            }
        }
        catch (Exception ex)
        {
            return new MatchResult
            {
                Confidence = 0,
                IsMatch = false,
                Metadata = new Dictionary<string, string> { { "error", ex.Message }, { "raw_response", response } }
            };
        }

        return new MatchResult { Confidence = 0, IsMatch = false, Metadata = new Dictionary<string, string> { { "raw_response", response } } };
    }

    private class NameComparisonResponse
    {
        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("isMatch")]
        public bool IsMatch { get; set; }

        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;
    }
}
