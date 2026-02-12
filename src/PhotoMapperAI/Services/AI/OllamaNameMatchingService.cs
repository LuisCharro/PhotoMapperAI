using PhotoMapperAI.Models;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PhotoMapperAI.Services.AI;

/// <summary>
/// AI-powered name matching service using Ollama LLMs.
/// Conservative: favors correctness over coverage.
/// </summary>
public class OllamaNameMatchingService : INameMatchingService
{
    private readonly OllamaClient _client;
    private readonly string _modelName;
    private readonly double _confidenceThreshold;
    private readonly JsonSerializerOptions _jsonOptions;

    private static readonly Regex NonAlphaNum = new(@"[^a-z0-9]+", RegexOptions.Compiled);

    // Particles/suffixes that often appear in football names; removed from "core tokens".
    private static readonly HashSet<string> IgnoredTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "de","del","della","da","do","dos","das",
        "van","von","der","den","la","le","di","du","st","saint",
        "jr","junior","sr","ii","iii","iv"
    };

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

    public async Task<MatchResult> CompareNamesAsync(string name1, string name2)
    {
        var prompt = BuildNameComparisonPrompt(name1, name2);

        try
        {
            // Conservative + deterministic.
            var response = await _client.ChatAsync(_modelName, prompt, temperature: 0.0);
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

    public async Task<List<MatchResult>> CompareNamesBatchAsync(string baseName, List<string> candidateNames)
    {
        var results = new List<MatchResult>();
        foreach (var candidate in candidateNames)
        {
            var result = await CompareNamesAsync(baseName, candidate);
            results.Add(result);
        }

        // Optional extra guard against "wrong remaining candidate":
        // If top is a match but runner-up is close, treat as ambiguous (return no match upstream).
        // Keep as metadata so your caller can decide.
        var ordered = results.OrderByDescending(r => r.Confidence).ToList();
        if (ordered.Count >= 2)
        {
            var top = ordered[0];
            var second = ordered[1];
            if (top.Confidence >= _confidenceThreshold && (top.Confidence - second.Confidence) < 0.07)
            {
                top.Metadata ??= new Dictionary<string, string>();
                top.Metadata["ambiguous_top_two"] = "true";
                top.Metadata["top_minus_second"] = (top.Confidence - second.Confidence).ToString(CultureInfo.InvariantCulture);
            }
        }

        return ordered;
    }

    private static string BuildNameComparisonPrompt(string name1, string name2)
    {
        var core1 = ToCoreTokens(name1);
        var core2 = ToCoreTokens(name2);

        var surname1 = GetSurnameTokens(core1);
        var surname2 = GetSurnameTokens(core2);

        var given1 = core1.Count > 0 ? core1[0] : "";
        var given2 = core2.Count > 0 ? core2[0] : "";

        var input = new
        {
            name1_raw = name1,
            name2_raw = name2,
            name1_core_tokens = core1,
            name2_core_tokens = core2,
            name1_given = given1,
            name2_given = given2,
            name1_surname_tokens = surname1,
            name2_surname_tokens = surname2
        };

        var inputJson = JsonSerializer.Serialize(input);

        // Key idea: hard rules + calibrated confidence + explicit "no guessing".
        return
$@"SYSTEM:
You are a conservative name-matching engine for football player records.
Your ONLY goal is to avoid false positives. Favor correctness over coverage.
Do NOT guess. Do NOT pick a ""best candidate"". If evidence is not strong, return isMatch=false.

IMPORTANT:
- Use ONLY the provided tokens. Do NOT use world knowledge about real players.
- Assume accents/diacritics and punctuation are already handled in the tokens.
- Output MUST be valid JSON ONLY (no markdown, no extra text).

INPUT (JSON):
{inputJson}

TASK:
Decide if the two names refer to the same person.

DEFINITIONS:
- core tokens: the provided token lists.
- surname tokens: the provided last 1-2 core tokens (already computed).
- given token: the provided first core token (already computed).
- matched tokens: exact string equality only (no semantic guessing).

HARD RULES (STRICT):
1) If there is NO overlap between surname token sets, then:
   - isMatch MUST be false
   - confidence MUST be <= 0.10
2) If there IS surname overlap but given tokens do NOT match exactly, then:
   - isMatch MUST be false
   - confidence MUST be <= 0.60
   (Exception: if one given token is a single-letter initial that matches the other's first letter,
    confidence may be up to 0.75 but isMatch MUST still be false.)
3) If surnames overlap AND given tokens match exactly, then a match is POSSIBLE, but still conservative:
   - If core token MULTISET is identical ignoring order => confidence 0.99 and isMatch true
   - Else if one core token set is a subset of the other (extra middle tokens only) => confidence 0.93..0.97 and isMatch true
   - Else (partial overlap beyond that) => confidence <= 0.89 and isMatch false
4) If any clear contradiction exists (e.g., two different surname tokens with no way to reconcile via subset),
   set confidence <= 0.20 and isMatch false.

OUTPUT SCHEMA (JSON only):
{{
  ""confidence"": 0.0,
  ""isMatch"": false,
  ""reason"": ""short explanation"",
  ""matchedCoreTokens"": [],
  ""matchedSurnameTokens"": []
}}

Remember: If you are not SURE based on strong token evidence, return low confidence and isMatch=false.";
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
                    // Do NOT trust model isMatch; enforce your threshold.
                    var isMatch = data.Confidence >= _confidenceThreshold;

                    return new MatchResult
                    {
                        Confidence = data.Confidence,
                        IsMatch = isMatch,
                        Metadata = new Dictionary<string, string>
                        {
                            { "reason", data.Reason ?? "" },
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
                    IsMatch = confidence >= _confidenceThreshold
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

        return new MatchResult
        {
            Confidence = 0,
            IsMatch = false,
            Metadata = new Dictionary<string, string> { { "raw_response", response } }
        };
    }

    private static List<string> ToCoreTokens(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        // Fold diacritics and lowercase.
        var folded = FoldToAsciiLower(raw);

        // Normalize separators.
        folded = folded
            .Replace('-', ' ')
            .Replace('’', ' ')
            .Replace('\'', ' ')
            .Replace('.', ' ')
            .Replace('_', ' ');

        // Keep only a-z0-9 as tokens.
        folded = NonAlphaNum.Replace(folded, " ").Trim();

        var parts = folded.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var tokens = new List<string>(parts.Length);
        foreach (var p in parts)
        {
            if (IgnoredTokens.Contains(p))
                continue;

            if (IsNumericToken(p))
                continue;

            tokens.Add(p);
        }

        return tokens;
    }

    private static string FoldToAsciiLower(string input)
    {
        var formD = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);

        foreach (var c in formD)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    private static List<string> GetSurnameTokens(List<string> coreTokens)
    {
        var filtered = coreTokens.Where(token => !IsNumericToken(token)).ToList();
        if (filtered.Count == 0)
            return new List<string>();

        if (filtered.Count == 1)
            return new List<string> { filtered[0] };

        // Conservative heuristic: last 2 tokens.
        return filtered.Skip(filtered.Count - 2).ToList();
    }

    private static bool IsNumericToken(string token)
    {
        return token.All(char.IsDigit);
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
