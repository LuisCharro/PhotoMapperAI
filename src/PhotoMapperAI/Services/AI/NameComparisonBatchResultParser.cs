using PhotoMapperAI.Models;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhotoMapperAI.Services.AI;

internal static class NameComparisonBatchResultParser
{
    public static Dictionary<int, MatchResult> Parse(
        string response,
        double confidenceThreshold,
        Dictionary<string, string>? baseMetadata,
        JsonSerializerOptions? serializerOptions,
        out string? error,
        out string? rawJson)
    {
        error = null;
        rawJson = null;
        var options = serializerOptions ?? new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        try
        {
            var start = response.IndexOf('{');
            var end = response.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                error = "Could not locate JSON object in response.";
                return new Dictionary<int, MatchResult>();
            }

            rawJson = response.Substring(start, end - start + 1);
            var data = JsonSerializer.Deserialize<BatchResponse>(rawJson, options);
            if (data?.Results == null || data.Results.Count == 0)
            {
                error = "No batch results found in response.";
                return new Dictionary<int, MatchResult>();
            }

            var output = new Dictionary<int, MatchResult>();
            foreach (var item in data.Results)
            {
                var metadata = MergeMetadata(baseMetadata);
                metadata["reason"] = item.Reason ?? string.Empty;
                metadata["raw_json"] = rawJson;
                metadata["model_isMatch"] = item.IsMatch.ToString();

                output[item.Index] = new MatchResult
                {
                    Confidence = item.Confidence,
                    IsMatch = item.Confidence >= confidenceThreshold,
                    Metadata = metadata
                };
            }

            return output;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return new Dictionary<int, MatchResult>();
        }
    }

    public static MatchResult BuildError(
        string error,
        double confidenceThreshold,
        Dictionary<string, string>? baseMetadata = null,
        string? rawResponse = null)
    {
        var metadata = MergeMetadata(baseMetadata);
        metadata["threshold"] = confidenceThreshold.ToString("0.###", CultureInfo.InvariantCulture);
        metadata["error"] = error;
        if (!string.IsNullOrWhiteSpace(rawResponse))
            metadata["raw_response"] = rawResponse;

        return new MatchResult
        {
            Confidence = 0,
            IsMatch = false,
            Metadata = metadata
        };
    }

    private static Dictionary<string, string> MergeMetadata(Dictionary<string, string>? baseMetadata)
    {
        if (baseMetadata == null)
            return new Dictionary<string, string>();

        return new Dictionary<string, string>(baseMetadata, StringComparer.OrdinalIgnoreCase);
    }

    private class BatchResponse
    {
        [JsonPropertyName("results")]
        public List<BatchResultItem>? Results { get; set; }
    }

    private class BatchResultItem
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("isMatch")]
        public bool IsMatch { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }
}
