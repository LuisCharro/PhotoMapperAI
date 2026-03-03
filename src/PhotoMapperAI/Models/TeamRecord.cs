namespace PhotoMapperAI.Models;

/// <summary>
/// Represents a team record for CSV export/import.
/// </summary>
public class TeamRecord
{
    /// <summary>
    /// Unique team identifier.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// Team name.
    /// </summary>
    public string TeamName { get; set; } = string.Empty;

    /// <summary>
    /// Returns a display string for dropdowns (e.g., "1 - FC Barcelona").
    /// </summary>
    public string DisplayName => $"{TeamId} - {TeamName}";

    /// <summary>
    /// Generates a safe filename component from the team name.
    /// </summary>
    public string SafeTeamName => string.Join("_", TeamName.Split(Path.GetInvalidFileNameChars()));
}
