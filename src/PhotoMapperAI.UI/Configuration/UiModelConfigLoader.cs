using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PhotoMapperAI.UI.Configuration;

public sealed class UiModelConfig
{
    public List<string> MapPaidModels { get; init; } = new();
    public List<string> GeneratePaidModels { get; init; } = new();
}

public static class UiModelConfigLoader
{
    public static UiModelConfig Load()
    {
        var path = ResolveConfigPath();
        if (path == null)
            return new UiModelConfig();

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("UiModelLists", out var root))
                return new UiModelConfig();

            return new UiModelConfig
            {
                MapPaidModels = ReadStringList(root, "MapPaidModels"),
                GeneratePaidModels = ReadStringList(root, "GeneratePaidModels")
            };
        }
        catch
        {
            return new UiModelConfig();
        }
    }

    private static List<string> ReadStringList(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return new List<string>();

        return arr.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? ResolveConfigPath()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "appsettings.local.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "appsettings.template.json"),
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
            Path.Combine(AppContext.BaseDirectory, "appsettings.local.json"),
            Path.Combine(AppContext.BaseDirectory, "appsettings.template.json")
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
