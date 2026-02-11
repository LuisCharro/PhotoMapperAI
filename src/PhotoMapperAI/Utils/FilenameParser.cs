using PhotoMapperAI.Models;
using System.Text.RegularExpressions;

namespace PhotoMapperAI.Utils;

/// <summary>
/// Parser for extracting metadata from photo filenames using patterns.
/// </summary>
public class FilenameParser
{
    // Common regex patterns for filename extraction
    private static readonly Regex[] _patterns =
    {
        // Pattern 1: {ExternalId}_{FamilyName}_{SurName}.png
        new Regex(@"^(?<id>\d+)_(?<family>[^_]+)_(?<sur>[^\.]+)\.(png|jpg|jpeg|bmp)$",
                  RegexOptions.IgnoreCase),

        // Pattern 2: {SurName}-{FamilyName}-{ExternalId}.jpg
        new Regex(@"^(?<sur>[^-]+)-(?<family>[^-]+)-(?<id>\d+)\.(png|jpg|jpeg|bmp)$",
                  RegexOptions.IgnoreCase),

        // Pattern 3: {FamilyName}, {SurName} - {ExternalId}.png
        new Regex(@"^(?<family>[^,]+),\s*(?<sur>[^-]+)\s*-\s*(?<id>\d+)\.(png|jpg|jpeg|bmp)$",
                  RegexOptions.IgnoreCase),

        // Pattern 4: {FamilyName}_{SurName}_position_{ExternalId}.png
        new Regex(@"^(?<family>[^_]+)_(?<sur>[^_]+)_position_(?<id>\d+)\.(png|jpg|jpeg|bmp)$",
                  RegexOptions.IgnoreCase),

        // Pattern 5: {ExternalId}-{SurName}-{FamilyName}.jpg
        new Regex(@"^(?<id>\d+)-(?<sur>[^-]+)-(?<family>[^\.]+)\.(png|jpg|jpeg|bmp)$",
                  RegexOptions.IgnoreCase),

        // Pattern 6: {FamilyName}_{SurName}_{ExternalId}.jpg (variation)
        new Regex(@"^(?<family>[^_]+)_(?<sur>[^_]+)_(?<id>\d+)\.(png|jpg|jpeg|bmp)$",
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
    /// <param name="template">Template string (e.g., "{id}_{family}_{sur}.png")</param>
    /// <returns>Photo metadata if template matches, null otherwise</returns>
    public static PhotoMetadata? ParseWithTemplate(string filename, string template)
    {
        try
        {
            // Convert template to regex pattern
            var regexTemplate = template
                .Replace("{id}", "(?<id>\\d+)")
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
