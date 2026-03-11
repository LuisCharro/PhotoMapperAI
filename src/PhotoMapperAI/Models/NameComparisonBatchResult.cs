namespace PhotoMapperAI.Models;

/// <summary>
/// Result of a batch name comparison request.
/// </summary>
public sealed class NameComparisonBatchResult
{
    public NameComparisonBatchResult(
        List<MatchResult> results,
        int usageCalls,
        int promptTokens,
        int completionTokens,
        int totalTokens)
    {
        Results = results;
        UsageCalls = usageCalls;
        PromptTokens = promptTokens;
        CompletionTokens = completionTokens;
        TotalTokens = totalTokens;
    }

    /// <summary>
    /// Match results in the same order as the input comparisons.
    /// </summary>
    public List<MatchResult> Results { get; }

    /// <summary>
    /// Number of billable model calls (usually 1 for a batch).
    /// </summary>
    public int UsageCalls { get; }

    /// <summary>
    /// Prompt/input tokens used by the batch call.
    /// </summary>
    public int PromptTokens { get; }

    /// <summary>
    /// Completion/output tokens used by the batch call.
    /// </summary>
    public int CompletionTokens { get; }

    /// <summary>
    /// Total tokens used by the batch call.
    /// </summary>
    public int TotalTokens { get; }
}
