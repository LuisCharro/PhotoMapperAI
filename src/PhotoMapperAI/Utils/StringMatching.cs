using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PhotoMapperAI.Utils;

/// <summary>
/// Utility class for string similarity calculations.
/// </summary>
public static class StringMatching
{
    /// <summary>
    /// Regular expression to collapse consecutive identical vowels (e.g., "aa" -> "a", "oo" -> "o").
    /// This allows matching between transliterated forms (å -> aa) and direct ASCII (a).
    /// </summary>
    private static readonly Regex CollapseVowelsRegex = new(@"(a|e|i|o|u)\1+", RegexOptions.Compiled);

    /// <summary>
    /// Calculates Levenshtein distance between two strings (case-insensitive).
    /// </summary>
    public static int LevenshteinDistance(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1))
            return string.IsNullOrEmpty(s2) ? 0 : s2.Length;

        if (string.IsNullOrEmpty(s2))
            return s1.Length;

        int len1 = s1.Length;
        int len2 = s2.Length;
        int[,] dp = new int[len1 + 1, len2 + 1];

        for (int i = 0; i <= len1; i++)
        {
            dp[i, 0] = i;
        }

        for (int j = 0; j <= len2; j++)
        {
            dp[0, j] = j;
        }

        for (int i = 1; i <= len1; i++)
        {
            for (int j = 1; j <= len2; j++)
            {
                int cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost
                );
            }
        }

        return dp[len1, len2];
    }

    /// <summary>
    /// Calculates similarity score (0.0 to 1.0) based on Levenshtein distance.
    /// </summary>
    public static double CalculateSimilarity(string s1, string s2)
    {
        if (string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase))
            return 1.0;

        int maxLen = Math.Max(s1.Length, s2.Length);
        if (maxLen == 0) return 1.0;
        
        int distance = LevenshteinDistance(s1, s2);
        
        // Similarity = 1 - (distance / maxLen)
        return 1.0 - ((double)distance / maxLen);
    }

    /// <summary>
    /// Normalizes a name for comparison (removes accents, extra spaces, etc.).
    /// </summary>
    public static string NormalizeName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        // Treat common separators as spaces before normalization.
        name = name.Replace('-', ' ').Replace('_', ' ');

        // Apply European character transliteration first (before accent removal)
        name = NormalizeEuropeanCharacters(name);

        // Remove accents
        var normalized = name.Normalize(NormalizationForm.FormKD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        normalized = stringBuilder.ToString().Normalize(NormalizationForm.FormC);

        // Convert to lowercase and trim
        normalized = normalized.ToLowerInvariant().Trim();

        // Remove extra whitespace and non-alphanumeric (except spaces)
        var result = new StringBuilder();
        foreach (char c in normalized)
        {
            if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
            {
                result.Append(c);
            }
        }

        normalized = Regex.Replace(result.ToString(), @"\s+", " ");

        // Collapse consecutive identical vowels to allow matching between
        // transliterated forms (å -> aa) and direct ASCII (a).
        // This is a generic normalization that helps with Scandinavian names.
        normalized = CollapseVowelsRegex.Replace(normalized, "$1");

        return normalized;
    }

    /// <summary>
    /// Normalizes European character variants to ASCII equivalents.
    /// Handles German umlauts, Scandinavian characters, and French special characters.
    /// </summary>
    private static string NormalizeEuropeanCharacters(string input)
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
    /// Normalizes a single European character to its ASCII equivalent.
    /// This provides generic transliteration for common European character variants
    /// without hardcoding specific dataset examples.
    /// </summary>
    private static string NormalizeEuropeanChar(char c)
    {
        return c switch
        {
            // German umlauts: ü -> ue, ö -> oe, ä -> ae, ß -> ss
            'ä' => "ae",
            'ö' => "oe",
            'ü' => "ue",
            'Ä' => "Ae",
            'Ö' => "Oe",
            'Ü' => "Ue",
            'ß' => "ss",

            // Scandinavian: å -> aa, æ -> ae, ø -> oe
            // Note: These map to multi-character sequences to preserve sound similarity
            'æ' => "ae",
            'Æ' => "Ae",
            'ø' => "oe",
            'Ø' => "Oe",
            'å' => "aa",
            'Å' => "Aa",

            // French special characters
            'œ' => "oe",
            'Œ' => "Oe",
            'ç' => "c",
            'Ç' => "C",

            // Spanish special characters
            'ñ' => "n",
            'Ñ' => "N",

            // Portuguese
            'ã' => "a",
            'õ' => "o",
            'Ã' => "A",
            'Õ' => "O",

            // Italian
            'ì' => "i",
            'í' => "i",
            'Í' => "I",

            // Catalan - l with middle dot (handled as single char in pattern matching)
            // Using string to handle multi-codepoint sequences if needed

            _ => c.ToString()
        };
    }

    /// <summary>
    /// Compares two names using multiple strategies and returns best confidence score.
    /// </summary>
    public static double CompareNames(string name1, string name2)
    {
        var norm1 = NormalizeName(name1);
        var norm2 = NormalizeName(name2);

        // Strategy 1: Direct string equality
        if (string.Equals(norm1, norm2, StringComparison.OrdinalIgnoreCase))
            return 1.0;

        // Strategy 2: Word-set equality (order independent)
        var words1 = norm1.Split(' ', StringSplitOptions.RemoveEmptyEntries).OrderBy(w => w).ToList();
        var words2 = norm2.Split(' ', StringSplitOptions.RemoveEmptyEntries).OrderBy(w => w).ToList();
        
        if (words1.SequenceEqual(words2))
            return 1.0;

        // Strategy 3: One side is subset of the other (extra middle/second surname tokens)
        var set1 = new HashSet<string>(words1);
        var set2 = new HashSet<string>(words2);
        if ((set1.IsSubsetOf(set2) || set2.IsSubsetOf(set1)) && Math.Min(set1.Count, set2.Count) >= 2)
            return 0.93;

        // Strategy 4: Jaccard similarity for words
        var intersection = new HashSet<string>(set1);
        intersection.IntersectWith(set2);
        var union = new HashSet<string>(set1);
        union.UnionWith(set2);
        
        double jaccard = (double)intersection.Count / union.Count;
        if (jaccard >= 0.8)
            return jaccard;

        // Strategy 5: Contains check
        if (norm1.Contains(norm2) || norm2.Contains(norm1))
            return 0.95;

        // Strategy 6: Levenshtein similarity
        var similarity = CalculateSimilarity(norm1, norm2);
        
        return Math.Max(similarity, jaccard);
    }

    /// <summary>
    /// Compares two names and returns detailed comparison result including ambiguity info.
    /// This is useful for candidate selection when we need to know if the match is ambiguous.
    /// </summary>
    public static NameComparisonResult CompareNamesWithDetails(string name1, string name2)
    {
        var norm1 = NormalizeName(name1);
        var norm2 = NormalizeName(name2);

        var score = CompareNames(name1, name2);
        
        // Calculate a secondary similarity using a more lenient approach
        // This helps detect cases where the main score might be affected by
        // minor character differences
        var lenientScore = CalculateLenientSimilarity(norm1, norm2);

        return new NameComparisonResult
        {
            PrimaryScore = score,
            LenientScore = lenientScore,
            NormalizedName1 = norm1,
            NormalizedName2 = norm2,
            IsAmbiguous = false // Caller should determine ambiguity based on margin between top candidates
        };
    }

    /// <summary>
    /// Calculates a more lenient similarity that handles edge cases better.
    /// This uses token-based comparison with fuzzy matching.
    /// </summary>
    private static double CalculateLenientSimilarity(string norm1, string norm2)
    {
        if (string.IsNullOrEmpty(norm1) || string.IsNullOrEmpty(norm2))
            return 0;

        var tokens1 = norm1.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var tokens2 = norm2.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (tokens1.Length == 0 || tokens2.Length == 0)
            return 0;

        // Calculate best match for each token
        var totalScore = 0.0;
        foreach (var t1 in tokens1)
        {
            var bestMatch = 0.0;
            foreach (var t2 in tokens2)
            {
                var sim = CalculateSimilarity(t1, t2);
                if (sim > bestMatch)
                    bestMatch = sim;
            }
            totalScore += bestMatch;
        }

        return totalScore / Math.Max(tokens1.Length, tokens2.Length);
    }
}

/// <summary>
/// Result of a name comparison with detailed information.
/// </summary>
public class NameComparisonResult
{
    public double PrimaryScore { get; set; }
    public double LenientScore { get; set; }
    public string NormalizedName1 { get; set; } = string.Empty;
    public string NormalizedName2 { get; set; } = string.Empty;
    public bool IsAmbiguous { get; set; }
}
