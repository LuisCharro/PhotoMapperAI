using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace PhotoMapperAI.UI.Models;

/// <summary>
/// Represents the state of a batch processing session.
/// </summary>
public class BatchSessionState
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    
    // Configuration
    public string? ConnectionString { get; set; }
    public string? TeamsSqlPath { get; set; }
    public string? PlayersSqlPath { get; set; }
    public string? BaseCsvDirectory { get; set; }
    public string? BasePhotoDirectory { get; set; }
    public string? BaseOutputDirectory { get; set; }
    public bool UseTeamPhotoSubdirectories { get; set; } = true;
    
    // Name Matching Settings
    public string? NameMatchingModel { get; set; } = "qwen2.5:7b";
    public double NameMatchingThreshold { get; set; } = 0.8;
    public bool UseAiMapping { get; set; }
    public bool AiOnly { get; set; }
    public bool AiSecondPass { get; set; }
    
    // Face Detection Settings
    public string? FaceDetectionModel { get; set; } = "opencv-dnn";
    public bool DownloadOpenCvModels { get; set; }
    
    // Size Settings
    public string? SizeProfilePath { get; set; }
    public bool GenerateAllSizes { get; set; } = true;
    public int DefaultWidth { get; set; } = 200;
    public int DefaultHeight { get; set; } = 300;
    
    // Output Settings
    public string? ImageFormat { get; set; } = "jpg";
    public string? OutputProfile { get; set; } = "none";
    
    // Results
    public List<BatchTeamResult> TeamResults { get; set; } = new();
    public int TotalTeams { get; set; }
    public int TeamsCompleted { get; set; }
    public int TeamsFailed { get; set; }
    public int TeamsSkipped { get; set; }
    public string? Status { get; set; }
    
    /// <summary>
    /// Saves the batch session state to a JSON file.
    /// </summary>
    public async Task SaveAsync(string filePath)
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        await File.WriteAllTextAsync(filePath, json);
    }
    
    /// <summary>
    /// Loads a batch session state from a JSON file.
    /// </summary>
    public static async Task<BatchSessionState> LoadAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<BatchSessionState>(json) 
               ?? new BatchSessionState();
    }
    
    /// <summary>
    /// Gets a default batch session file path in the output directory.
    /// </summary>
    public static string GetSessionPath(string baseOutputDirectory)
    {
        var fileName = $"batch_session_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
        return Path.Combine(baseOutputDirectory, fileName);
    }
}

/// <summary>
/// Result of processing a single team in batch mode.
/// </summary>
public class BatchTeamResult
{
    public int TeamId { get; set; }
    public string? TeamName { get; set; }
    public string Status { get; set; } = "Pending";
    public string? StatusMessage { get; set; }
    public int PlayersExtracted { get; set; }
    public int PlayersMapped { get; set; }
    public int PhotosGenerated { get; set; }
    public string? CsvPath { get; set; }
    public string? MappedCsvPath { get; set; }
    public string? PhotoPath { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
