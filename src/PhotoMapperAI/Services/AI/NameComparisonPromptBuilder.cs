using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PhotoMapperAI.Services.AI;

public static class NameComparisonPromptBuilder
{
    private static readonly Regex NonAlphaNum = new(@"[^a-z0-9]+", RegexOptions.Compiled);

    private static readonly HashSet<string> IgnoredTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "de","del","della","da","do","dos","das",
        "van","von","der","den","la","le","di","du","st","saint",
        "jr","junior","sr","ii","iii","iv"
    };

    private static readonly Dictionary<string, string> SpellingNormalizations = new(StringComparer.OrdinalIgnoreCase)
    {
        // Spanish/Portuguese s/z variants mapped to canonical forms
        ["fernandes"] = "fernandez",
        ["rodrigues"] = "rodriguez",
        ["gonsales"] = "gonzalez",
        ["gonsalez"] = "gonzalez",
        ["sanches"] = "sanchez",
        ["garcias"] = "garcia",

        // Common surname variants
        ["lope"] = "lopez",
        ["martine"] = "martinez",

        // Ukrainian/Cyrillic transliteration variants - Given names
        ["heorhiy"] = "georgiy",
        ["heorhii"] = "georgiy",
        ["georgi"] = "georgiy",
        ["andrej"] = "andriy",
        ["andrei"] = "andriy",
        ["andrew"] = "andriy",
        ["sergej"] = "serhiy",
        ["sergei"] = "serhiy",
        ["sergii"] = "serhiy",
        ["serge"] = "serhiy",
        ["alexander"] = "oleksandr",
        ["aleksandr"] = "oleksandr",
        ["alexandar"] = "oleksandr",
        ["mykola"] = "nicholas",
        ["nikolai"] = "nicholas",
        ["nikolay"] = "nicholas",
        ["nikolaj"] = "nicholas",
        ["nicola"] = "nicholas",
        ["mykhaylo"] = "mykhailo",
        ["mikhail"] = "mykhailo",
        ["michael"] = "mykhailo",
        ["vitaliy"] = "vitalii",
        ["vitaly"] = "vitalii",
        ["vitalij"] = "vitalii",
        ["vladyslav"] = "vladislav",
        ["wladyslaw"] = "vladislav",

        // Ukrainian/Cyrillic transliteration variants - Surnames
        ["jarmolenko"] = "yarmolenko",
        ["iarmolenko"] = "yarmolenko",
        ["zinchenko"] = "zinchenko",
        ["sintschenko"] = "zinchenko",
        ["sinchenko"] = "zinchenko",
        ["zincenko"] = "zinchenko",
        ["sydorchuk"] = "sydorchuk",
        ["sidorchuk"] = "sydorchuk",
        ["sidortschuk"] = "sydorchuk",
        ["sidorchuck"] = "sydorchuk",
        ["bushchan"] = "bushchan",
        ["buschchan"] = "bushchan",
        ["buschan"] = "bushchan",
        ["rebrov"] = "rebrov",
        ["rebrow"] = "rebrov",
        ["rebroff"] = "rebrov",
        ["shaparenko"] = "shaparenko",
        ["schaparenko"] = "shaparenko",
        ["yaremchuk"] = "yaremchuk",
        ["jaremchuk"] = "yaremchuk",
        ["mudryk"] = "mudryk",
        ["mudruk"] = "mudryk",
        ["mudryk"] = "mudryk",
        ["tsygankov"] = "tsyhankov",
        ["tsyhankov"] = "tsyhankov",
        ["tsihankow"] = "tsyhankov",
        ["tsigankov"] = "tsyhankov",
        ["cigankov"] = "tsyhankov",

        // Common nickname/full name equivalents
        ["zander"] = "alexander",
        ["alex"] = "alexander",
        ["sasha"] = "alexander",
        ["joey"] = "johannes",
        ["joe"] = "joseph",
        ["jimmy"] = "james",
        ["jim"] = "james",
        ["chris"] = "christopher",
        ["cristiano"] = "ronaldo",
        ["cristian"] = "christian",
        ["lotte"] = "carlotte",
        ["charlie"] = "charlotte",
        ["maggie"] = "margaret",
        ["beth"] = "elizabeth",
        ["betsy"] = "elizabeth",
        ["lizzy"] = "elizabeth",

        // Brazilian/Portuguese mononymous players (map nickname to full name components)
        ["jorginho"] = "jorge",
        ["pepe"] = "kepler",
        ["danilo"] = "danilo",
        ["ronaldo"] = "ronaldo",
        ["joselu"] = "jose",
        ["sylvinho"] = "sylvio",
        ["sylvio"] = "sylvio",
        ["costa"] = "carole",

        // Icelandic letter normalizations
        ["thorsteinn"] = "þorsteinn",
        ["þorsteinn"] = "thorsteinn",
        ["þ"] = "th",

        // Common surname prefixes/particles (normalized for matching)
        ["dos"] = "",
        ["das"] = "",
        ["aveiro"] = "aveiro",
        ["ferreira"] = "ferreira"
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
You are a name-matching engine for football player records.
Your goal is to correctly identify when two names refer to the same person.
Be permissive for minor character variations (s/z, accent marks, diacritics) but strict on clear mismatches.

IMPORTANT:
- Use ONLY the provided tokens. Do NOT use world knowledge about real players.
- Assume accents/diacritics and punctuation are already handled in the tokens.
- Token order is NOT reliable. One source may be ""family given"", another may be ""given family"".
- Extra middle/second-surname tokens may appear on one side only.
- Transliteration variations are PRE-NORMALIZED (Ukrainian/Cyrillic j→y, w→v, sch→sh, etc.).
- After normalization, single-character differences (s/z, ñ/n, accent marks) should be treated as MATCHES if other tokens align.
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
   - confidence MUST be in 0.85..0.97

4) If overlap is ""all but one token"" for the shorter side (e.g. 2-of-3 or 1-of-2)
   and the non-overlapping token pair has strong string similarity (minor variant/diminutive)
   OR single-character difference (s/z, c/k, i/y, accent marks, ñ/n, ç/c)
   OR are known transliteration equivalents (already pre-normalized), then:
   - isMatch MUST be true
   - confidence MUST be in 0.82..0.93

5) If overlap count is exactly 1 and neither side is single-token-only AND rule 4 does NOT apply, then:
   - isMatch MUST be false
   - confidence MUST be <= 0.60

6) For other partial-overlap cases with overlap >= 2:
   - if evidence suggests likely match (multiple tokens align), set isMatch=true with confidence 0.75..0.88
   - otherwise be conservative: isMatch=false and confidence <= 0.89

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

Remember: Transliteration variations are pre-normalized. After normalization, minor remaining character variations (s/z, c/k, i/y, single-letter differences) should NOT prevent a match when tokens otherwise align.";
    }

    /// <summary>
    /// Normalizes European character variants to ASCII equivalents.
    /// Handles German umlauts, Scandinavian characters, and French special characters.
    /// </summary>
    public static string NormalizeEuropeanCharacters(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var result = new StringBuilder(input.Length);
        foreach (char c in input)
        {
            result.Append(NormalizeEuropeanChar(c));
        }
        return result.ToString();
    }

    private static string NormalizeEuropeanChar(char c)
    {
        return c switch
        {
            // German umlauts
            'ä' => "ae",
            'ö' => "oe",
            'ü' => "ue",
            'Ä' => "Ae",
            'Ö' => "Oe",
            'Ü' => "Ue",
            'ß' => "ss",

            // Scandinavian
            'æ' => "ae",
            'Æ' => "Ae",
            'ø' => "oe",
            'Ø' => "Oe",
            'å' => "aa",
            'Å' => "Aa",

            // Icelandic
            'þ' => "th",
            'Þ' => "Th",
            'ð' => "d",
            'Ð' => "D",

            // French
            'œ' => "oe",
            'Œ' => "Oe",
            'ç' => "c",
            'Ç' => "C",

            // Spanish (already handled by accent removal in FoldToAsciiLower)
            'ñ' => "n",
            'Ñ' => "N",

            _ => c.ToString()
        };
    }

    internal static List<string> ToCoreTokens(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        // First normalize European characters (German, Scandinavian, French variants)
        var europeanNormalized = NormalizeEuropeanCharacters(raw);

        // Then fold to ASCII and lowercase
        var folded = FoldToAsciiLower(europeanNormalized);
        folded = folded
            .Replace('-', ' ')
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

            // Normalize spelling variants (case-insensitive lookup)
            var normalized = SpellingNormalizations.TryGetValue(p, out var norm) ? norm : p;

            tokens.Add(normalized);
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

