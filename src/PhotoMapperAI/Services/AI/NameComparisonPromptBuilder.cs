using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PhotoMapperAI.Services.AI;

internal static class NameComparisonPromptBuilder
{
    private static readonly Regex NonAlphaNum = new(@"[^a-z0-9]+", RegexOptions.Compiled);

    private static readonly HashSet<string> IgnoredTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "de","del","della","da","do","dos","das",
        "van","von","der","den","la","le","di","du","st","saint",
        "jr","junior","sr","ii","iii","iv"
    };

    public static string Build(string name1, string name2)
    {
        var core1 = ToCoreTokens(name1);
        var core2 = ToCoreTokens(name2);

        var input = new
        {
            name1_raw = name1,
            name2_raw = name2,
            name1_core_tokens = core1,
            name2_core_tokens = core2
        };

        var inputJson = JsonSerializer.Serialize(input);

        return
$@"SYSTEM:
You are a conservative name-matching engine for football player records.
Your ONLY goal is to avoid false positives. Favor correctness over coverage.
Do NOT guess. Do NOT pick a ""best candidate"". If evidence is not strong, return isMatch=false.

IMPORTANT:
- Use ONLY the provided tokens. Do NOT use world knowledge about real players.
- Assume accents/diacritics and punctuation are already handled in the tokens.
- Token order is NOT reliable. One source may be ""family given"", another may be ""given family"".
- Extra middle/second-surname tokens may appear on one side only.
- Output MUST be valid JSON ONLY (no markdown, no extra text).

INPUT (JSON):
{inputJson}

TASK:
Decide if the two names refer to the same person.

DEFINITIONS:
- core tokens: the provided token lists.
- matched tokens: exact string equality only (no semantic guessing).

HARD RULES (STRICT):
1) If there is NO overlap between core token sets, then:
   - isMatch MUST be false
   - confidence MUST be <= 0.10
2) If core-token MULTISET is identical ignoring order, then:
   - isMatch MUST be true
   - confidence MUST be 0.99
3) If all tokens from the shorter side are contained in the longer side
   (subset relation) AND overlap count >= 2, then:
   - isMatch MUST be true
   - confidence MUST be in 0.92..0.97
4) If overlap is ""all but one token"" for the shorter side (e.g. 2-of-3 or 1-of-2)
   and the non-overlapping token pair has strong string similarity (minor variant/diminutive)
   with same first letter, then:
   - isMatch MAY be true
   - confidence MUST be in 0.82..0.90
5) If overlap count is exactly 1 and neither side is single-token-only (and rule 4 does not apply), then:
   - isMatch MUST be false
   - confidence MUST be <= 0.60
6) For other partial-overlap cases, be conservative:
   - if evidence is not clearly subset/equality, set isMatch=false and confidence <= 0.89
7) If any clear contradiction exists (large token disagreement with weak overlap),
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

    private static List<string> ToCoreTokens(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        var folded = FoldToAsciiLower(raw);
        folded = folded
            .Replace('-', ' ')
            .Replace('’', ' ')
            .Replace('\'', ' ')
            .Replace('.', ' ')
            .Replace('_', ' ');
        folded = NonAlphaNum.Replace(folded, " ").Trim();

        var parts = folded.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var tokens = new List<string>(parts.Length);
        foreach (var p in parts)
        {
            if (IgnoredTokens.Contains(p))
                continue;

            if (p.All(char.IsDigit))
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
}

