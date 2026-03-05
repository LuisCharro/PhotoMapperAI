namespace PhotoMapperAI.Models;

/// <summary>
/// Represents a single name comparison request.
/// </summary>
public sealed class NameComparisonPair
{
    public NameComparisonPair(string name1, string name2)
    {
        Name1 = name1;
        Name2 = name2;
    }

    /// <summary>
    /// First name to compare (e.g., player name from CSV).
    /// </summary>
    public string Name1 { get; }

    /// <summary>
    /// Second name to compare (e.g., candidate photo name).
    /// </summary>
    public string Name2 { get; }
}
