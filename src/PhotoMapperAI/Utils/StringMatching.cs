using System.Globalization;
using System.Text;

namespace PhotoMapperAI.Utils;

/// <summary>
/// Utility class for string similarity calculations.
/// </summary>
public static class StringMatching
{
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
        
        normalized = System.Text.RegularExpressions.Regex.Replace(result.ToString(), @"\s+", " ");

        return normalized;
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
}
