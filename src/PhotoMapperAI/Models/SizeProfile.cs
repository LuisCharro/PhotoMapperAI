namespace PhotoMapperAI.Models;

/// <summary>
/// Represents a portrait size profile configuration.
/// </summary>
public sealed class SizeProfile
{
    public string Name { get; set; } = "default";
    public List<SizeVariant> Variants { get; set; } = new();
}

/// <summary>
/// Represents one portrait output variant.
/// </summary>
public sealed class SizeVariant
{
    public string Key { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public string? OutputSubfolder { get; set; }
    
    /// <summary>
    /// Path to a placeholder image for this variant.
    /// The placeholder should already be sized to match Width x Height.
    /// If specified, this placeholder will be copied to output when no source photo is found.
    /// </summary>
    public string? PlaceholderPath { get; set; }
}
