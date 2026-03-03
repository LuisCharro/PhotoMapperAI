using System.Collections.Generic;

namespace PhotoMapperAI.UI.Models;

/// <summary>
/// Represents a failed or skipped team entry in the batch error summary.
/// </summary>
public class BatchIssueSummaryItem
{
    public string TeamName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsCritical { get; set; }

    /// <summary>
    /// Detailed per-player information (e.g. unmapped player names).
    /// </summary>
    public string Details { get; set; } = string.Empty;

    /// <summary>
    /// Whether this item has non-empty details to display.
    /// </summary>
    public bool HasDetails => !string.IsNullOrWhiteSpace(Details);
}
