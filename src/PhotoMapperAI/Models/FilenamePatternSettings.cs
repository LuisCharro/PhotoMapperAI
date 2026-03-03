using System;
using System.Collections.Generic;
using System.Linq;

namespace PhotoMapperAI.Models;

public sealed class FilenamePatternPreset
{
    public string Name { get; set; } = "default";
    public string Pattern { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public sealed class FilenamePatternSettings
{
    public string ActivePresetName { get; set; } = "default";
    public List<FilenamePatternPreset> Presets { get; set; } = new();

    public FilenamePatternPreset GetActivePreset()
    {
        if (Presets.Count == 0)
        {
            return new FilenamePatternPreset { Name = "default", Pattern = string.Empty };
        }

        var match = Presets.FirstOrDefault(p =>
            string.Equals(p.Name, ActivePresetName, StringComparison.OrdinalIgnoreCase));

        return match ?? Presets[0];
    }

    public static FilenamePatternSettings CreateDefault()
    {
        return new FilenamePatternSettings
        {
            ActivePresetName = "default",
            Presets = new List<FilenamePatternPreset>
            {
                new FilenamePatternPreset 
                { 
                    Name = "Auto-detect", 
                    Pattern = string.Empty,
                    Description = "Automatically detect from common patterns"
                },
                new FilenamePatternPreset 
                { 
                    Name = "FirstName_LastName_ID", 
                    Pattern = "{first}_{last}_{id}.jpg",
                    Description = "Dani_Carvajal_250024448.jpg"
                },
                new FilenamePatternPreset 
                { 
                    Name = "ID_FirstName_LastName", 
                    Pattern = "{id}_{first}_{last}.png",
                    Description = "250024448_Dani_Carvajal.png"
                },
                new FilenamePatternPreset 
                { 
                    Name = "FirstName-LastName-ID", 
                    Pattern = "{first}-{last}-{id}.jpg",
                    Description = "Dani-Carvajal-250024448.jpg"
                },
                new FilenamePatternPreset 
                { 
                    Name = "ID only", 
                    Pattern = "{id}.jpg",
                    Description = "250024448.jpg"
                }
            }
        };
    }
}
