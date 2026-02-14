using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace PhotoMapperAI.UI.Models;

/// <summary>
/// Represents the state of a PhotoMapperAI session.
/// Can be saved and loaded to continue work later.
/// </summary>
public class SessionState
{
    public int CurrentStep { get; set; } = 1;

    // Step 1: Extract
    public string? SqlFilePath { get; set; }
    public string? ConnectionStringPath { get; set; }
    public int TeamId { get; set; }
    public string? OutputFileName { get; set; } = "players.csv";
    public string? ExtractOutputDirectory { get; set; }
    public string? ExtractOutputCsvPath { get; set; }
    public string? DatabaseType { get; set; } = "SqlServer";
    public bool ExtractComplete { get; set; }
    public int PlayersExtracted { get; set; }

    // Step 2: Map
    public string? MapInputCsvPath { get; set; }
    public string? PhotosDirectory { get; set; }
    public string? FilenamePattern { get; set; }
    public bool UsePhotoManifest { get; set; }
    public string? PhotoManifestPath { get; set; }
    public string? NameModel { get; set; } = "qwen2.5:7b";
    public double ConfidenceThreshold { get; set; } = 0.9;
    public bool UseAiMapping { get; set; }
    public bool AiSecondPass { get; set; } = true;
    public bool AiOnly { get; set; }
    public bool MapComplete { get; set; }
    public int PlayersMatched { get; set; }
    public int PlayersProcessed { get; set; }

    // Step 3: Generate
    public string? GenerateInputCsvPath { get; set; }
    public string? GeneratePhotosDirectory { get; set; }
    public string? OutputDirectory { get; set; }
    public string? ImageFormat { get; set; } = "jpg";
    public string? FaceDetectionModel { get; set; } = "llava:7b";
    public int PortraitWidth { get; set; } = 200;
    public int PortraitHeight { get; set; } = 300;
    public string? SizeProfilePath { get; set; }
    public bool AllSizes { get; set; }
    public string? OutputProfile { get; set; } = "none";
    public bool PortraitOnly { get; set; }
    public bool DownloadOpenCvModels { get; set; }
    public bool GenerateComplete { get; set; }
    public int PortraitsGenerated { get; set; }
    public int PortraitsFailed { get; set; }

    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Saves the session state to a JSON file.
    /// </summary>
    public async Task SaveAsync(string filePath)
    {
        LastModified = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Loads a session state from a JSON file.
    /// </summary>
    public static async Task<SessionState> LoadAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<SessionState>(json) 
               ?? new SessionState();
    }

    /// <summary>
    /// Gets the default session file path.
    /// </summary>
    public static string GetDefaultSessionPath()
    {
        var appDataPath = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "PhotoMapperAI");
        Directory.CreateDirectory(appFolder);
        return Path.Combine(appFolder, "session.json");
    }
}
