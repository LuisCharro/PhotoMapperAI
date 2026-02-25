namespace PhotoMapperAI.UI.Models;

/// <summary>
/// Represents a failed or skipped team entry in the batch error summary.
/// </summary>
public class BatchIssueSummaryItem
{
    public string TeamName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
