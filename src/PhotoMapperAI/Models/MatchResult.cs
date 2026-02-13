namespace PhotoMapperAI.Models;

/// <summary>
/// Result of comparing two names using AI.
/// </summary>
public class MatchResult
{
    /// <summary>
    /// Player ID from the comparison (if applicable).
    /// </summary>
    public int PlayerId { get; set; }

    /// <summary>
    /// Confidence score (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Whether the names match (confidence >= threshold).
    /// </summary>
    public bool IsMatch { get; set; }

    /// <summary>
    /// Additional metadata about the comparison.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}
