namespace PhotoMapperAI.UI.Models;

public sealed class ManualMappingWorkflowRequest
{
    public string Title { get; init; } = string.Empty;
    public string MappedCsvPath { get; init; } = string.Empty;
    public string PhotosDirectory { get; init; } = string.Empty;
    public string? FilenamePattern { get; init; }
    public string? PhotoManifestPath { get; init; }
}

public sealed class ManualMappingWorkflowResult
{
    public bool Saved { get; init; }
    public string? ErrorMessage { get; init; }
    public string MappedCsvPath { get; init; } = string.Empty;
}
