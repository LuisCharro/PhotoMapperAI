using PhotoMapperAI.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        Console.WriteLine($"    Comparing: '{name1}' vs '{name2}'...");
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
        return $@"Compare these two names of sports players and determine if they refer to the same person.
Names can have different orders (First Last vs Last First), missing middle names, or transliteration differences.

Name 1: {name1}
Name 2: {name2}

Return a JSON object with:
{{
  ""confidence"": 0.0 to 1.0,
  ""reason"": ""short explanation""
}}";
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
                Console.WriteLine($"    Extracted JSON: {json}");
                var data = JsonSerializer.Deserialize<NameComparisonResponse>(json, _jsonOptions);
                if (data != null)
                {
                    return new MatchResult
                    {
                        Confidence = data.Confidence,
                        IsMatch = data.Confidence >= _confidenceThreshold,
                        Metadata = new Dictionary<string, string> { { "reason", data.Reason } }
                    };
                }
            }
        }
        catch { }

        return new MatchResult { Confidence = 0, IsMatch = false };
    }

    private class NameComparisonResponse
    {
        public double Confidence { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
