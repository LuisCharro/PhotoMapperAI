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

public sealed class PreviewCustomDimensions
{
    public int Width { get; set; } = 200;
    public int Height { get; set; } = 300;
    public bool UseCustom { get; set; }
}

public sealed class PreviewDimensionPreset
{
    public string Name { get; set; } = "default";
    public int Width { get; set; } = 200;
    public int Height { get; set; } = 300;
}

public sealed class CropOffsetSettings
{
    public string ActivePresetName { get; set; } = "default";
    public List<CropOffsetPreset> Presets { get; set; } = new();
    public PreviewCustomDimensions? PreviewCustomDimensions { get; set; }
    public string ActivePreviewDimensionPresetName { get; set; } = "default";
    public List<PreviewDimensionPreset> PreviewDimensionPresets { get; set; } = new();

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

    public PreviewDimensionPreset GetActivePreviewDimensionPreset()
    {
        if (PreviewDimensionPresets.Count == 0)
        {
            return new PreviewDimensionPreset { Name = "default" };
        }

        var match = PreviewDimensionPresets.FirstOrDefault(p =>
            string.Equals(p.Name, ActivePreviewDimensionPresetName, StringComparison.OrdinalIgnoreCase));

        return match ?? PreviewDimensionPresets[0];
    }

    public static CropOffsetSettings CreateDefault()
    {
        return new CropOffsetSettings
        {
            ActivePresetName = "default",
            Presets = new List<CropOffsetPreset>
            {
                new CropOffsetPreset { Name = "default", HorizontalPercent = 0, VerticalPercent = 0 }
            },
            PreviewCustomDimensions = new PreviewCustomDimensions(),
            ActivePreviewDimensionPresetName = "default",
            PreviewDimensionPresets = new List<PreviewDimensionPreset>
            {
                new PreviewDimensionPreset { Name = "default", Width = 200, Height = 300 }
            }
        };
    }
}
