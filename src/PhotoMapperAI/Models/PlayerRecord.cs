namespace PhotoMapperAI.Models;

/// <summary>
/// Represents a player record extracted from database.
/// </summary>
public class PlayerRecord
{
    /// <summary>
    /// Internal player ID (from user's database)
    /// </summary>
    public int PlayerId { get; set; }

    /// <summary>
    /// Team ID for the player
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// Family name (surname/last name)
    /// </summary>
    public string FamilyName { get; set; } = string.Empty;

    /// <summary>
    /// Sur name (first name/middle name)
    /// </summary>
    public string SurName { get; set; } = string.Empty;

    /// <summary>
    /// External player ID (e.g., FIFA Player ID)
    /// Null if not mapped yet
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// Valid mapping flag (true if confident match found)
    /// </summary>
    public bool ValidMapping { get; set; }

    /// <summary>
    /// Confidence score of the mapping (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Full name for display purposes
    /// </summary>
    public string FullName => $"{FamilyName} {SurName}".Trim();
}
