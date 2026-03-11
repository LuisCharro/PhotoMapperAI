using System.Globalization;
using System.Linq;
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
        ["chico"] = "francisco",
        // Cristiano Ronaldo (mononymous player) - maps "cristiano" token to "ronaldo" for matching
        // WARNING: May cause false positives for players named "Cristiano [Surname]"
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

        // Icelandic letter normalizations - removed: þ is already converted to th by NormalizeEuropeanCharacters before tokenization
        // Common surname prefixes/particles - removed: "dos" and "das" already in IgnoredTokens
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
$@"You are an expert in international football player name matching across different data sources (databases, photo filenames, official rosters). Your task is to determine if two name representations refer to the same person.

CONTEXT:
These names come from European football competitions (UEFA Euro tournaments). Players are from diverse backgrounds: Ukrainian, Portuguese, Brazilian, Icelandic, Turkish, Albanian, etc. You will encounter:
- Cyrillic transliterations (Ukrainian/Russian names spelled different ways in Latin script)
- Mononymous Brazilian/Portuguese players (e.g., ""Pepe"", ""Jorginho"" vs full legal names)
- Name order variations (""Given Family"" vs ""Family Given"")
- Nicknames and diminutives (""Lotte"" for ""Carlotte"", ""Zander"" for ""Alexander"")
- Multiple surnames (Spanish/Portuguese tradition: maternal + paternal)
- Icelandic patronymics and special characters (þ/th, ð/d)
- Abbreviated middle names (""Marit B. Lund"" vs ""Marit Bratberg Lund"")

INPUT:
{inputJson}

APPROACH:
1. **Phonetic similarity**: Do the names sound similar when pronounced? Consider transliteration equivalents (e.g., Ukrainian ""Yarmolenko"" = ""Jarmolenko"")
2. **Token matching**: Are key name components (surnames, given names) present in both? Token order may vary.
3. **Cultural awareness**: 
   - Brazilian players often use single names or nicknames professionally
   - Spanish/Portuguese players may have 3-4 names (given + maternal + paternal)
   - Eastern European names have multiple valid transliterations
   - Scandinavian/Icelandic characters have Latin equivalents
4. **Partial matches**: One name being a subset of another is common (photo filename vs full database name)
5. **Deal-breakers**: Completely different surnames with no phonetic relationship = different people

CONFIDENCE CALIBRATION:
- **0.95-1.00**: Identical or near-identical (all tokens match, trivial spelling difference)
- **0.85-0.94**: Very strong match (key tokens match, clear transliteration/nickname pattern)
- **0.75-0.84**: Strong match (most tokens match, some ambiguity in middle names or order)
- **0.65-0.74**: Moderate match (partial overlap, could be same person but requires verification)
- **0.50-0.64**: Weak match (some similarity but significant differences)
- **0.00-0.49**: Not a match (clear evidence these are different people)

EXAMPLES:
✓ MATCH (0.92): ""Zinchenko Oleksandr"" ↔ ""Sintschenko Alexander""
    → Ukrainian transliteration + given name variant

✓ MATCH (0.96): ""Pepe"" ↔ ""Kepler Laveran Lima Ferreira""
    → Known Brazilian mononymous player

✓ MATCH (0.89): ""Wubben-Moy Lotte"" ↔ ""Wubben-Moy Carlotte""
    → Nickname/full name for same person

✓ MATCH (0.94): ""Þorsteinn Halldorsson"" ↔ ""Thorsteinn Halldorsson""
    → Icelandic character þ = th

✗ NO MATCH (0.15): ""Silva João"" ↔ ""Silva Pedro""
    → Same surname but clearly different given names

✗ NO MATCH (0.25): ""Martinez"" ↔ ""Rodriguez Martinez""
    → Insufficient overlap, different family names

EXAMPLES:
✓ MATCH (0.92): ""Zinchenko Oleksandr"" ↔ ""Sintschenko Alexander"" 
   → Ukrainian transliteration + given name variant

✓ MATCH (0.96): ""Pepe"" ↔ ""Kepler Laveran Lima Ferreira"" 
   → Known Brazilian mononymous player

✓ MATCH (0.89): ""Wubben-Moy Lotte"" ↔ ""Wubben-Moy Carlotte"" 
   → Nickname/full name for same person

✓ MATCH (0.94): ""Þorsteinn Halldorsson"" ↔ ""Thorsteinn Halldorsson"" 
   → Icelandic character þ = th

✗ NO MATCH (0.15): ""Silva João"" ↔ ""Silva Pedro"" 
   → Same surname but clearly different given names

✗ NO MATCH (0.25): ""Martinez"" ↔ ""Rodriguez Martinez"" 
   → Insufficient overlap, different family names

TASK:
Analyze the two names provided. Use your knowledge of international naming conventions, phonetics, and cultural patterns to determine if they refer to the same person. Explain your reasoning briefly.

OUTPUT FORMAT (valid JSON only, no markdown):
{{
  ""confidence"": 0.0,
  ""isMatch"": false,
  ""reason"": ""brief explanation of why match/no-match with key evidence"",
  ""matchedCoreTokens"": [""list"", ""of"", ""matching"", ""tokens""],
  ""matchedSurnameTokens"": [""list"", ""of"", ""matching"", ""surname"", ""tokens""]
}}";
    }

    public static string BuildBatch(IReadOnlyList<BatchComparison> comparisons)
    {
        var items = comparisons.Select(c => new
        {
            index = c.Index,
            name1_raw = c.Name1,
            name2_raw = c.Name2,
            name1_core_tokens = ToCoreTokens(c.Name1),
            name2_core_tokens = ToCoreTokens(c.Name2)
        }).ToList();

        var inputJson = JsonSerializer.Serialize(new { comparisons = items });

        return
$@"You are an expert in international football player name matching across different data sources (databases, photo filenames, official rosters). Your task is to determine if two name representations refer to the same person.

CONTEXT:
These names come from European football competitions (UEFA Euro tournaments). Players are from diverse backgrounds: Ukrainian, Portuguese, Brazilian, Icelandic, Turkish, Albanian, etc. You will encounter:
- Cyrillic transliterations (Ukrainian/Russian names spelled different ways in Latin script)
- Mononymous Brazilian/Portuguese players (e.g., ""Pepe"", ""Jorginho"" vs full legal names)
- Name order variations (""Given Family"" vs ""Family Given"")
- Nicknames and diminutives (""Lotte"" for ""Carlotte"", ""Zander"" for ""Alexander"")
- Multiple surnames (Spanish/Portuguese tradition: maternal + paternal)
- Icelandic patronymics and special characters (þ/th, ð/d)
- Abbreviated middle names (""Marit B. Lund"" vs ""Marit Bratberg Lund"")

INPUT:
{inputJson}

APPROACH:
1. **Phonetic similarity**: Do the names sound similar when pronounced? Consider transliteration equivalents (e.g., Ukrainian ""Yarmolenko"" = ""Jarmolenko"")
2. **Token matching**: Are key name components (surnames, given names) present in both? Token order may vary.
3. **Cultural awareness**:
     - Brazilian players often use single names or nicknames professionally
     - Spanish/Portuguese players may have 3-4 names (given + maternal + paternal)
     - Eastern European names have multiple valid transliterations
     - Scandinavian/Icelandic characters have Latin equivalents
4. **Partial matches**: One name being a subset of another is common (photo filename vs full database name)
5. **Deal-breakers**: Completely different surnames with no phonetic relationship = different people

CONFIDENCE CALIBRATION:
- **0.95-1.00**: Identical or near-identical (all tokens match, trivial spelling difference)
- **0.85-0.94**: Very strong match (key tokens match, clear transliteration/nickname pattern)
- **0.75-0.84**: Strong match (most tokens match, some ambiguity in middle names or order)
- **0.65-0.74**: Moderate match (partial overlap, could be same person but requires verification)
- **0.50-0.64**: Weak match (some similarity but significant differences)
- **0.00-0.49**: Not a match (clear evidence these are different people)

TASK:
Analyze each comparison. Use your knowledge of international naming conventions, phonetics, and cultural patterns to determine if the names refer to the same person. Explain your reasoning briefly.

OUTPUT FORMAT (valid JSON only, no markdown):
{{
    ""results"": [
        {{ ""index"": 0, ""confidence"": 0.0, ""isMatch"": false, ""reason"": ""brief explanation"" }}
    ]
}}

Return exactly one result per comparison with the same index provided in the input. Do not omit any index or reorder results. If uncertain, provide your best estimate; do not default to 0.0 unless the names are clearly different.";
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

    /// <summary>
    /// Central world-competition normalization pipeline for AI token preparation.
    /// Keep this aligned with StringMatching so deterministic and AI-assisted matching use the same script buckets.
    /// </summary>
    public static string NormalizeWorldCompetitionCharacters(string input)
    {
        var result = input;

        result = NormalizeEuropeanCharacters(result);
        result = NormalizeAsianCharacters(result);
        result = NormalizeAfricanCharacters(result);
        result = NormalizeMiddleEastCharacters(result);

        return result;
    }

    /// <summary>
    /// Placeholder for future Asian-script normalization and transliteration.
    /// Intended for CJK, Hangul, kana/romaji variants, and similar competition datasets.
    /// </summary>
    public static string NormalizeAsianCharacters(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var normalized = input.Normalize(NormalizationForm.FormKC);
        var result = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            result.Append(c switch
            {
                '\u3000' => ' ',
                '\u30FB' => ' ',
                '\uFF65' => ' ',
                '\u00B7' => ' ',
                '\u0387' => ' ',
                '\u200B' => '\0',
                '\u200C' => '\0',
                '\u200D' => '\0',
                '\uFEFF' => '\0',
                _ => c
            });
        }

        return result.ToString().Replace("\0", string.Empty);
    }

    /// <summary>
    /// Placeholder for future African-language normalization.
    /// Intended for additional Latin variants, digraphs, and region-specific transliteration rules.
    /// </summary>
    public static string NormalizeAfricanCharacters(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var result = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            result.Append(c switch
            {
                'ɛ' => "e",
                'Ɛ' => "E",
                'ɔ' => "o",
                'Ɔ' => "O",
                'ə' => "e",
                'Ə' => "E",
                'ɓ' => "b",
                'Ɓ' => "B",
                'ɗ' => "d",
                'Ɗ' => "D",
                'ŋ' => "ng",
                'Ŋ' => "Ng",
                _ => c.ToString()
            });
        }

        return result.ToString();
    }

    /// <summary>
    /// Placeholder for future Middle Eastern script normalization and transliteration.
    /// Intended for Arabic, Hebrew, Persian, and competition-specific Latin renderings.
    /// </summary>
    public static string NormalizeMiddleEastCharacters(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var result = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            result.Append(c switch
            {
                '\u0640' => "",
                '\u200B' => "",
                '\u200C' => "",
                '\u200D' => "",
                '\u200E' => "",
                '\u200F' => "",
                '\u061C' => "",
                'أ' => "ا",
                'إ' => "ا",
                'آ' => "ا",
                'ٱ' => "ا",
                'ى' => "ي",
                'ی' => "ي",
                'ئ' => "ي",
                'ک' => "ك",
                'ة' => "ه",
                '٠' => "0",
                '١' => "1",
                '٢' => "2",
                '٣' => "3",
                '٤' => "4",
                '٥' => "5",
                '٦' => "6",
                '٧' => "7",
                '٨' => "8",
                '٩' => "9",
                '۰' => "0",
                '۱' => "1",
                '۲' => "2",
                '۳' => "3",
                '۴' => "4",
                '۵' => "5",
                '۶' => "6",
                '۷' => "7",
                '۸' => "8",
                '۹' => "9",
                _ => c.ToString()
            });
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

            // South Slavic latin letters
            'đ' => "dj",
            'Đ' => "Dj",

            // Turkish
            'ı' => "i",
            'İ' => "I",

            // Polish
            'ł' => "l",
            'Ł' => "L",

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

        // First normalize script/region variants before ASCII folding.
        var worldNormalized = NormalizeWorldCompetitionCharacters(raw);

        // Then fold to ASCII and lowercase
        var folded = FoldToAsciiLower(worldNormalized);
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

            var normalized = NormalizeToken(p);

            tokens.Add(normalized);
        }

        return tokens;
    }

    public sealed record BatchComparison(int Index, string Name1, string Name2);

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

    private static string NormalizeToken(string token)
    {
        var current = token;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (seen.Add(current) && SpellingNormalizations.TryGetValue(current, out var next))
        {
            current = next;
        }

        return current;
    }
}
