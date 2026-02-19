using System;
using System.Collections.Generic;
using System.Linq;

namespace PhotoMapperAI.Models;

public sealed class CropOffsetPreset
{
    public string Name { get; set; } = "default";
    public double HorizontalPercent { get; set; }
    public double VerticalPercent { get; set; }
}

public sealed class CropOffsetSettings
{
    public string ActivePresetName { get; set; } = "default";
    public List<CropOffsetPreset> Presets { get; set; } = new();

    public CropOffsetPreset GetActivePreset()
    {
        if (Presets.Count == 0)
        {
            return new CropOffsetPreset { Name = "default" };
        }

        var match = Presets.FirstOrDefault(p =>
            string.Equals(p.Name, ActivePresetName, StringComparison.OrdinalIgnoreCase));

        return match ?? Presets[0];
    }

    public static CropOffsetSettings CreateDefault()
    {
        return new CropOffsetSettings
        {
            ActivePresetName = "default",
            Presets = new List<CropOffsetPreset>
            {
                new CropOffsetPreset { Name = "default", HorizontalPercent = 0, VerticalPercent = 0 }
            }
        };
    }
}
