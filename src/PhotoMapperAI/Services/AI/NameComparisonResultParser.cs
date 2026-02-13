using PhotoMapperAI.Models;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PhotoMapperAI.Services.AI;

internal static class NameComparisonResultParser
{
    private static readonly Regex ConfidenceRegex = new(@"""confidence"":\s*([0-9\.]+)", RegexOptions.Compiled);

    public static MatchResult Parse(
        string response,
        double confidenceThreshold,
        Dictionary<string, string>? baseMetadata = null,
        JsonSerializerOptions? serializerOptions = null)
    {
        var options = serializerOptions ?? new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        try
        {
            var start = response.IndexOf('{');
            var end = response.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                var json = response.Substring(start, end - start + 1);
                var data = JsonSerializer.Deserialize<NameComparisonResponse>(json, options);
                if (data != null)
                {
                    var metadata = MergeMetadata(baseMetadata);
                    metadata["reason"] = data.Reason ?? string.Empty;
                    metadata["raw_json"] = json;
                    metadata["model_isMatch"] = data.IsMatch.ToString();

                    return new MatchResult
                    {
                        Confidence = data.Confidence,
                        IsMatch = data.Confidence >= confidenceThreshold,
                        Metadata = metadata
                    };
                }
            }

            var confidenceMatch = ConfidenceRegex.Match(response);
            if (confidenceMatch.Success &&
                double.TryParse(confidenceMatch.Groups[1].Value, CultureInfo.InvariantCulture, out var confidence))
            {
                var metadata = MergeMetadata(baseMetadata);
                metadata["raw_response"] = response;

                return new MatchResult
                {
                    Confidence = confidence,
                    IsMatch = confidence >= confidenceThreshold,
                    Metadata = metadata
                };
            }
        }
        catch (Exception ex)
        {
            return BuildError(ex.Message, confidenceThreshold, baseMetadata, response);
        }

        return BuildError("Could not parse model response.", confidenceThreshold, baseMetadata, response);
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

