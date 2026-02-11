namespace PhotoMapperAI.Models;

/// <summary>
/// Represents metadata extracted from a photo filename or manifest.
/// </summary>
public class PhotoMetadata
{
    /// <summary>
    /// Original photo filename
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Full file path
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// External ID extracted from filename or manifest
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// Full name extracted from filename or manifest
    /// </summary>
    public string? FullName { get; set; }

    /// <summary>
    /// Family name (surname)
    /// </summary>
    public string? FamilyName { get; set; }

    /// <summary>
    /// Sur name (first name)
    /// </summary>
    public string? SurName { get; set; }

    /// <summary>
    /// Source of the metadata (filename, manifest, auto-detect)
    /// </summary>
    public MetadataSource Source { get; set; }

    /// <summary>
    /// Pattern used for extraction (if auto-detected)
    /// </summary>
    public string? PatternUsed { get; set; }
}

/// <summary>
/// Where the metadata came from.
/// </summary>
public enum MetadataSource
{
    /// <summary>
    /// Extracted automatically from filename using pattern detection
    /// </summary>
    AutoDetect,

    /// <summary>
    /// Extracted using user-specified filename pattern
    /// </summary>
    UserPattern,

    /// <summary>
    /// Loaded from photo manifest JSON file
    /// </summary>
    Manifest,

    /// <summary>
    /// Could not extract metadata
    /// </summary>
    Unknown
}
