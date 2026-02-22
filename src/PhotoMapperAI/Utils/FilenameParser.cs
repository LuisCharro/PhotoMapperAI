using PhotoMapperAI.Models;
using System.Text.RegularExpressions;

namespace PhotoMapperAI.Utils;

/// <summary>
/// Parser for extracting metadata from photo filenames using patterns.
/// </summary>
public class FilenameParser
{
    // Common regex patterns for filename extraction
    // Note: Internal regex groups use "sur" for first name and "family" for last name
    // User-facing placeholders are {first}, {last}, {id}
    private static readonly Regex[] _patterns =
    {
        // Pattern 1: {id}_{last}_{first}.png (ID_LastName_FirstName)
        new Regex(@"^(?<id>\d+)_(?<family>[^_]+)_(?<sur>[^\.]+)\.(png|jpg|jpeg|bmp)$",
                  RegexOptions.IgnoreCase),

        // Pattern 2: {first}-{last}-{id}.jpg (FirstName-LastName-ID)
        new Regex(@"^(?<sur>[^-]+)-(?<family>[^-]+)-(?<id>\d+)\.(png|jpg|jpeg|bmp)$",
                  RegexOptions.IgnoreCase),

        // Pattern 3: {last}, {first} - {id}.png (LastName, FirstName - ID)
        new Regex(@"^(?<family>[^,]+),\s*(?<sur>[^-]+)\s*-\s*(?<id>\d+)\.(png|jpg|jpeg|bmp)$",
                  RegexOptions.IgnoreCase),

        // Pattern 4: {last}_{first}_position_{id}.png
        new Regex(@"^(?<family>[^_]+)_(?<sur>[^_]+)_position_(?<id>\d+)\.(png|jpg|jpeg|bmp)$",
                  RegexOptions.IgnoreCase),

        // Pattern 5: {id}-{first}-{last}.jpg (ID-FirstName-LastName)
        new Regex(@"^(?<id>\d+)-(?<sur>[^-]+)-(?<family>[^\.]+)\.(png|jpg|jpeg|bmp)$",
                  RegexOptions.IgnoreCase),

        // Pattern 6: {first}_{last}_{id}.jpg (FirstName_LastName_ID) - FIFA/Euro format
        new Regex(@"^(?<sur>[^_]+)_(?<family>[^_]+)_(?<id>\d+)\.(png|jpg|jpeg|bmp)$",
                  RegexOptions.IgnoreCase),

        // Pattern 7: {id}.jpg (simple ID naming)
        new Regex(@"^(?<id>\d+)\.(png|jpg|jpeg|bmp)$",
                  RegexOptions.IgnoreCase)
    };

    /// <summary>
    /// Tries to extract metadata from filename using auto-detection.
    /// </summary>
    /// <param name="filename">Photo filename</param>
    /// <returns>Photo metadata if pattern matches, null otherwise</returns>
    public static PhotoMetadata? ParseAutoDetect(string filename)
    {
        foreach (var pattern in _patterns)
        {
            var match = pattern.Match(filename);

            if (match.Success)
            {
                return BuildMetadata(match, filename, MetadataSource.AutoDetect, pattern.ToString());
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts metadata using user-specified template.
    /// </summary>
    /// <param name="filename">Photo filename</param>
    /// <param name="template">Template string (e.g., "{first}_{last}_{id}.png")</param>
    /// <returns>Photo metadata if template matches, null otherwise</returns>
    /// <remarks>
    /// Supported placeholders: {id}, {first}, {last}
    /// Legacy placeholders (backward compatible): {sur} maps to first name, {family} maps to last name
    /// </remarks>
    public static PhotoMetadata? ParseWithTemplate(string filename, string template)
    {
        try
        {
            // Convert template to regex pattern
            // Support both new ({first}, {last}) and legacy ({sur}, {family}) placeholders
            var regexTemplate = template
                .Replace("{id}", "(?<id>\\d+)")
                .Replace("{first}", "(?<sur>[^{}_]+)")
                .Replace("{last}", "(?<family>[^{}_]+)")
                .Replace("{family}", "(?<family>[^{}_]+)")
                .Replace("{sur}", "(?<sur>[^{}.]+)")
                .Replace(".", @"\.");

            var regex = new Regex($"^{regexTemplate}$", RegexOptions.IgnoreCase);
            var match = regex.Match(filename);

            if (match.Success)
            {
                return BuildMetadata(match, filename, MetadataSource.UserPattern, template);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Loads photo metadata from manifest JSON file.
    /// </summary>
    /// <param name="manifestPath">Path to manifest JSON file</param>
    /// <returns>Dictionary of filename to metadata</returns>
    public static Dictionary<string, PhotoMetadata> LoadManifest(string manifestPath)
    {
        var json = File.ReadAllText(manifestPath);
        var manifest = System.Text.Json.JsonSerializer.Deserialize<PhotoManifest>(json);

        var result = new Dictionary<string, PhotoMetadata>();

        if (manifest?.Photos != null)
        {
            foreach (var kvp in manifest.Photos)
            {
                result[kvp.Key] = new PhotoMetadata
                {
                    FileName = kvp.Key,
                    ExternalId = kvp.Value.ExternalId,
                    FullName = kvp.Value.FullName,
                    FamilyName = kvp.Value.FamilyName,
                    SurName = kvp.Value.SurName,
                    Source = MetadataSource.Manifest
                };
            }
        }

        return result;
    }

    #region Private Methods

    /// <summary>
    /// Builds PhotoMetadata from regex match.
    /// </summary>
    private static PhotoMetadata BuildMetadata(
        Match match,
        string filename,
        MetadataSource source,
        string? patternUsed = null)
    {
        var familyName = match.Groups["family"].Value;
        var surName = match.Groups["sur"].Value;

        return new PhotoMetadata
        {
            FileName = filename,
            ExternalId = match.Groups["id"].Value,
            FamilyName = familyName,
            SurName = surName,
            FullName = $"{familyName} {surName}".Trim(),
            Source = source,
            PatternUsed = patternUsed
        };
    }

    #endregion

    #region Manifest Models

    private class PhotoManifest
    {
        [System.Text.Json.Serialization.JsonPropertyName("photos")]
        public Dictionary<string, PhotoData>? Photos { get; set; }
    }

    private class PhotoData
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string? ExternalId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("fullName")]
        public string? FullName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("familyName")]
        public string? FamilyName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("surName")]
        public string? SurName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("teamId")]
        public string? TeamId { get; set; }
    }

    #endregion
}
