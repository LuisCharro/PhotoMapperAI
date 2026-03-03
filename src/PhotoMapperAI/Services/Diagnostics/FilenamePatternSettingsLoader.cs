using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using PhotoMapperAI.Models;

namespace PhotoMapperAI.Services.Diagnostics;

public static class FilenamePatternSettingsLoader
{
    public static FilenamePatternSettings Load()
    {
        foreach (var path in ResolveConfigPaths())
        {
            if (!File.Exists(path))
                continue;

            try
            {
                var settings = TryRead(path);
                if (settings != null)
                    return settings;
            }
            catch
            {
            }
        }

        return FilenamePatternSettings.CreateDefault();
    }

    public static FilenamePatternPreset LoadActivePreset()
    {
        var settings = Load();
        return settings.GetActivePreset();
    }

    public static void SaveToLocal(FilenamePatternSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        var path = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.local.json");
        var root = File.Exists(path)
            ? (JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? new JsonObject())
            : new JsonObject();

        var imageNode = root["ImageProcessing"] as JsonObject ?? new JsonObject();
        root["ImageProcessing"] = imageNode;

        var patternsNode = new JsonObject
        {
            ["ActivePresetName"] = settings.ActivePresetName
        };

        var presetsArray = new JsonArray();
        foreach (var preset in settings.Presets)
        {
            var presetNode = new JsonObject
            {
                ["Name"] = preset.Name,
                ["Pattern"] = preset.Pattern ?? string.Empty
            };
            
            if (!string.IsNullOrWhiteSpace(preset.Description))
            {
                presetNode["Description"] = preset.Description;
            }
            
            presetsArray.Add(presetNode);
        }

        patternsNode["Presets"] = presetsArray;
        imageNode["FilenamePatterns"] = patternsNode;

        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static FilenamePatternSettings? TryRead(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("ImageProcessing", out var imageRoot))
            return null;

        if (!imageRoot.TryGetProperty("FilenamePatterns", out var patternsRoot))
            return null;

        var settings = new FilenamePatternSettings();

        if (patternsRoot.TryGetProperty("ActivePresetName", out var activeElement) &&
            activeElement.ValueKind == JsonValueKind.String)
        {
            settings.ActivePresetName = activeElement.GetString() ?? settings.ActivePresetName;
        }

        if (patternsRoot.TryGetProperty("Presets", out var presetsElement) &&
            presetsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var presetElement in presetsElement.EnumerateArray())
            {
                if (presetElement.ValueKind != JsonValueKind.Object)
                    continue;

                var preset = new FilenamePatternPreset();

                if (presetElement.TryGetProperty("Name", out var nameElement) &&
                    nameElement.ValueKind == JsonValueKind.String)
                {
                    preset.Name = nameElement.GetString() ?? preset.Name;
                }

                if (presetElement.TryGetProperty("Pattern", out var patternElement) &&
                    patternElement.ValueKind == JsonValueKind.String)
                {
                    preset.Pattern = patternElement.GetString() ?? string.Empty;
                }

                if (presetElement.TryGetProperty("Description", out var descElement) &&
                    descElement.ValueKind == JsonValueKind.String)
                {
                    preset.Description = descElement.GetString();
                }

                if (!string.IsNullOrWhiteSpace(preset.Name))
                {
                    settings.Presets.Add(preset);
                }
            }
        }

        if (settings.Presets.Count == 0)
        {
            settings.Presets.Add(new FilenamePatternPreset { Name = "default", Pattern = string.Empty });
        }

        return settings;
    }

    private static IEnumerable<string> ResolveConfigPaths()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "appsettings.local.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "appsettings.template.json"),
            Path.Combine(AppContext.BaseDirectory, "appsettings.local.json"),
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
            Path.Combine(AppContext.BaseDirectory, "appsettings.template.json")
        };

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
