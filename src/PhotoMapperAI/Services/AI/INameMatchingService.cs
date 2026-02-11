namespace PhotoMapperAI.Services.AI;

/// <summary>
/// Interface for name matching services (supports multiple AI models).
/// </summary>
public interface INameMatchingService
{
    /// <summary>
    /// Gets the model name for display/logging.
    /// </summary>
    string ModelName { get; }

    /// <summary>
    /// Compares two names and returns a match result with confidence score.
    /// </summary>
    /// <param name="name1">First name to compare (e.g., from CSV)</param>
    /// <param name="name2">Second name to compare (e.g., from photo filename)</param>
    /// <returns>Match result with confidence score (0.0 to 1.0)</returns>
    Task<Models.MatchResult> CompareNamesAsync(string name1, string name2);

    /// <summary>
    /// Batch compare multiple names at once.
    /// </summary>
    /// <param name="baseName">Base name to compare</param>
    /// <param name="candidateNames">List of candidate names to match against</param>
    /// <returns>List of match results sorted by confidence</returns>
    Task<List<Models.MatchResult>> CompareNamesBatchAsync(string baseName, List<string> candidateNames);
}
