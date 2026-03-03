namespace PhotoMapperAI.UI.Models;

/// <summary>
/// Represents a mapping issue summary row for single-team generation.
/// </summary>
public class GenerateIssueSummaryItem
{
    public string Label { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int Count { get; set; }
    public bool IsCritical { get; set; }

    /// <summary>
    /// Detailed per-player/file information for this issue category.
    /// </summary>
    public string Details { get; set; } = string.Empty;

    /// <summary>
    /// Whether this item has non-empty details to display.
    /// </summary>
    public bool HasDetails => !string.IsNullOrWhiteSpace(Details);
}
