using System.Text.Json;
using PhotoMapperAI.Models;

namespace PhotoMapperAI.Services.Diagnostics;

/// <summary>
/// Loads and validates portrait size profiles from JSON.
/// </summary>
public static class SizeProfileLoader
{
    public static SizeProfile LoadFromFile(string profilePath)
    {
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            throw new ArgumentException("Size profile path cannot be empty.", nameof(profilePath));
        }

        if (!File.Exists(profilePath))
        {
            throw new FileNotFoundException($"Size profile file not found: {profilePath}");
        }

        var json = File.ReadAllText(profilePath);
        var profile = JsonSerializer.Deserialize<SizeProfile>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        if (profile == null)
        {
            throw new InvalidOperationException("Unable to parse size profile JSON.");
        }

        Validate(profile, profilePath);
        return profile;
    }

    public static void Validate(SizeProfile profile, string? sourceHint = null)
    {
        var origin = string.IsNullOrWhiteSpace(sourceHint) ? "size profile" : sourceHint;

        if (profile.Variants == null || profile.Variants.Count == 0)
        {
            throw new InvalidOperationException($"{origin}: profile must contain at least one size variant.");
        }

        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var variant in profile.Variants)
        {
            if (string.IsNullOrWhiteSpace(variant.Key))
            {
                throw new InvalidOperationException($"{origin}: each variant must have a non-empty key.");
            }

            if (!seenKeys.Add(variant.Key))
            {
                throw new InvalidOperationException($"{origin}: duplicate variant key '{variant.Key}'.");
            }

            if (variant.Width <= 0 || variant.Height <= 0)
            {
                throw new InvalidOperationException($"{origin}: variant '{variant.Key}' must have positive width and height.");
            }
        }
    }
}
