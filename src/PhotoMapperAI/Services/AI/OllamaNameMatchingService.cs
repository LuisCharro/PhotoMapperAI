using PhotoMapperAI.Models;

namespace PhotoMapperAI.Services.AI;

/// <summary>
/// Name matching service using Ollama LLM.
/// </summary>
public class OllamaNameMatchingService : INameMatchingService
{
    private readonly OllamaClient _client;
    private readonly string _modelName;
    private readonly double _confidenceThreshold;

    /// <summary>
    /// Creates a new Ollama name matching service.
    /// </summary>
    /// <param name="ollamaBaseUrl">Ollama server URL (default: http://localhost:11434)</param>
    /// <param name="modelName">Ollama model to use (e.g., qwen2.5:7b)</param>
    /// <param name="confidenceThreshold">Minimum confidence for valid match (default: 0.9)</param>
    public OllamaNameMatchingService(
        string ollamaBaseUrl = "http://localhost:11434",
        string modelName = "qwen2.5:7b",
        double confidenceThreshold = 0.9)
    {
        _client = new OllamaClient(ollamaBaseUrl);
        _modelName = modelName;
        _confidenceThreshold = confidenceThreshold;
    }

    /// <summary>
    /// Gets the model name.
    /// </summary>
    public string ModelName => _modelName;

    /// <summary>
    /// Compares two names using Ollama LLM.
    /// </summary>
    public async Task<MatchResult> CompareNamesAsync(string name1, string name2)
    {
        var startTime = DateTime.UtcNow;

        // Build prompt for name comparison
        var prompt = BuildNameComparisonPrompt(name1, name2);

        // System prompt for consistent behavior
        var systemPrompt = @"You are a name matching expert for sports player databases.
Compare two names and determine if they refer to the same person.
Consider:
- Nicknames (e.g., Isco = Francisco Román Alarcón)
- Name order variations (e.g., Messi Lionel = Lionel Messi)
- Composite names (e.g., Silva David = David Silva dos Santos)
- Accent differences (José = Jose)
Return your answer as a JSON object with:
- ""samePerson"": true/false
- ""confidence"": number between 0.0 and 1.0
- ""reason"": brief explanation";

        try
        {
            var response = await _client.ChatAsync(_modelName, prompt, systemPrompt, temperature: 0.2);
            var result = ParseMatchResult(response, name1, name2);
            result.ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            return result;
        }
        catch (Exception ex)
        {
            return new MatchResult
            {
                Confidence = 0.0,
                ConfidenceThreshold = _confidenceThreshold,
                Method = MatchMethod.AiNameMatching,
                ModelUsed = _modelName,
                ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                Metadata = new Dictionary<string, string>
                {
                    { "error", ex.Message }
                }
            };
        }
    }

    /// <summary>
    /// Batch compares names.
    /// </summary>
    public async Task<List<MatchResult>> CompareNamesBatchAsync(string baseName, List<string> candidateNames)
    {
        var results = new List<MatchResult>();

        foreach (var candidateName in candidateNames)
        {
            var result = await CompareNamesAsync(baseName, candidateName);
            results.Add(result);
        }

        // Sort by confidence (descending)
        return results.OrderByDescending(r => r.Confidence).ToList();
    }

    /// <summary>
    /// Checks if the service is available.
    /// </summary>
    public async Task<bool> IsAvailableAsync()
    {
        return await _client.IsAvailableAsync();
    }

    #region Private Methods

    /// <summary>
    /// Builds a prompt for name comparison.
    /// </summary>
    private static string BuildNameComparisonPrompt(string name1, string name2)
    {
        return $@"Compare these two names and determine if they refer to the same person:

Name 1: {name1}
Name 2: {name2}

Consider:
- Nicknames (Isco, Xavi, etc.)
- Name ordering (First Last vs Last First)
- Composite names (with multiple surnames)
- Accent variations

Return your answer as JSON: {{""samePerson"": true/false, ""confidence"": 0.0-1.0, ""reason"": ""explanation""}}";
    }

    /// <summary>
    /// Parses LLM response into MatchResult.
    /// </summary>
    private static MatchResult ParseMatchResult(string response, string name1, string name2)
    {
        try
        {
            // Extract JSON from response (LLM may add extra text)
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart < 0 || jsonEnd < 0)
            {
                return new MatchResult
                {
                    Confidence = 0.0,
                    Method = MatchMethod.AiNameMatching,
                    Metadata = new Dictionary<string, string>
                    {
                        { "parse_error", "Could not extract JSON from response" },
                        { "raw_response", response }
                    }
                };
            }

            var jsonString = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
            var data = System.Text.Json.JsonSerializer.Deserialize<JsonResponse>(jsonString);

            if (data == null)
            {
                return new MatchResult { Confidence = 0.0, Method = MatchMethod.AiNameMatching };
            }

            return new MatchResult
            {
                Confidence = data.Confidence,
                Method = MatchMethod.AiNameMatching,
                Metadata = new Dictionary<string, string>
                {
                    { "reason", data.Reason ?? string.Empty }
                }
            };
        }
        catch (Exception)
        {
            return new MatchResult
            {
                Confidence = 0.0,
                Method = MatchMethod.AiNameMatching,
                Metadata = new Dictionary<string, string>
                {
                    { "parse_error", "Failed to parse JSON" },
                    { "raw_response", response }
                }
            };
        }
    }

    #endregion

    #region JSON Response Models

    private class JsonResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("samePerson")]
        public bool SamePerson { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }

    #endregion
}
