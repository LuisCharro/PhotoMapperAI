using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using PhotoMapperAI.Models;

namespace PhotoMapperAI.Services.Diagnostics;

public static class CropOffsetSettingsLoader
{
    public static CropOffsetSettings Load()
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
                // Ignore parse errors and continue to next candidate.
            }
        }

        return CropOffsetSettings.CreateDefault();
    }

    public static CropOffsetPreset LoadActivePreset()
    {
        var settings = Load();
        return settings.GetActivePreset();
    }

    public static void SaveToLocal(CropOffsetSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        var path = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.local.json");
        var root = File.Exists(path)
            ? (JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? new JsonObject())
            : new JsonObject();

        var imageNode = root["ImageProcessing"] as JsonObject ?? new JsonObject();
        root["ImageProcessing"] = imageNode;

        var offsetsNode = new JsonObject
        {
            ["ActivePresetName"] = settings.ActivePresetName
        };

        var presetsArray = new JsonArray();
        foreach (var preset in settings.Presets)
        {
            var presetNode = new JsonObject
            {
                ["Name"] = preset.Name,
                ["HorizontalPercent"] = preset.HorizontalPercent,
                ["VerticalPercent"] = preset.VerticalPercent
            };
            presetsArray.Add(presetNode);
        }

        offsetsNode["Presets"] = presetsArray;
        imageNode["CropOffsets"] = offsetsNode;

        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static CropOffsetSettings? TryRead(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("ImageProcessing", out var imageRoot))
            return null;

        if (!imageRoot.TryGetProperty("CropOffsets", out var offsetsRoot))
            return null;

        var settings = new CropOffsetSettings();

        if (offsetsRoot.TryGetProperty("ActivePresetName", out var activeElement) &&
            activeElement.ValueKind == JsonValueKind.String)
        {
            settings.ActivePresetName = activeElement.GetString() ?? settings.ActivePresetName;
        }

        if (offsetsRoot.TryGetProperty("Presets", out var presetsElement) &&
            presetsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var presetElement in presetsElement.EnumerateArray())
            {
                if (presetElement.ValueKind != JsonValueKind.Object)
                    continue;

                var preset = new CropOffsetPreset();

                if (presetElement.TryGetProperty("Name", out var nameElement) &&
                    nameElement.ValueKind == JsonValueKind.String)
                {
                    preset.Name = nameElement.GetString() ?? preset.Name;
                }

                if (presetElement.TryGetProperty("HorizontalPercent", out var horizElement) &&
                    horizElement.ValueKind == JsonValueKind.Number)
                {
                    preset.HorizontalPercent = horizElement.GetDouble();
                }

                if (presetElement.TryGetProperty("VerticalPercent", out var vertElement) &&
                    vertElement.ValueKind == JsonValueKind.Number)
                {
                    preset.VerticalPercent = vertElement.GetDouble();
                }

                if (!string.IsNullOrWhiteSpace(preset.Name))
                {
                    settings.Presets.Add(preset);
                }
            }
        }

        if (settings.Presets.Count == 0)
        {
            settings.Presets.Add(new CropOffsetPreset { Name = "default" });
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
