namespace PhotoMapperAI.Models;

/// <summary>
/// Result of matching a photo to a player record.
/// </summary>
public class MappingResult
{
    /// <summary>
    /// Internal player ID (from database)
    /// Null if no match found
    /// </summary>
    public int? PlayerId { get; set; }

    /// <summary>
    /// External player ID from photo
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// Photo filename
    /// </summary>
    public string PhotoFileName { get; set; } = string.Empty;

    /// <summary>
    /// Is this a valid match (confidence >= threshold)
    /// </summary>
    public bool IsValidMatch => Confidence >= ConfidenceThreshold;

    /// <summary>
    /// Confidence score (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Confidence threshold used for validation
    /// </summary>
    public double ConfidenceThreshold { get; set; } = 0.9;

    /// <summary>
    /// Model used for matching
    /// </summary>
    public string ModelUsed { get; set; } = string.Empty;

    /// <summary>
    /// Match method (direct ID, AI name matching, etc.)
    /// </summary>
    public MatchMethod Method { get; set; }

    /// <summary>
    /// Time taken for matching operation (milliseconds)
    /// </summary>
    public long ProcessingTimeMs { get; set; }

    /// <summary>
    /// Additional metadata about the match
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// How the match was determined.
/// </summary>
public enum MatchMethod
{
    /// <summary>
    /// Direct ID match from filename
    /// </summary>
    DirectIdMatch,

    /// <summary>
    /// AI-powered fuzzy name matching
    /// </summary>
    AiNameMatching,

    /// <summary>
    /// Manual match (not implemented yet)
    /// </summary>
    Manual,

    /// <summary>
    /// No match found
    /// </summary>
    NoMatch
}
