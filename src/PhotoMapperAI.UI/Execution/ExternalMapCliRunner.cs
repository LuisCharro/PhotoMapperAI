using System;
using System.Threading;
using System.Threading.Tasks;
using PhotoMapperAI.Commands;
using PhotoMapperAI.Services.AI;
using PhotoMapperAI.Services.Image;

namespace PhotoMapperAI.UI.Execution;

public sealed class ExternalMapCliRunner
{
    public sealed class MapCliResult
    {
        public int ExitCode { get; set; }
        public int PlayersProcessed { get; set; }
        public int PlayersMatched { get; set; }
        public string OutputCsvPath { get; set; } = string.Empty;
    }

    public async Task<MapCliResult> ExecuteAsync(
        string projectRootDirectory,
        string executionDirectory,
        string inputCsvPath,
        string photosDir,
        string? filenamePattern,
        string? photoManifest,
        string nameModel,
        double confidenceThreshold,
        bool useAi,
        bool aiSecondPass,
        bool aiOnly,
        string? openAiApiKey,
        string? anthropicApiKey,
        CancellationToken cancellationToken,
        IProgress<string>? log,
        IProgress<(int processed, int total, string current)>? uiProgress = null)
    {
        _ = projectRootDirectory;

        var nameMatchingService = NameMatchingServiceFactory.Create(
            nameModel,
            confidenceThreshold: confidenceThreshold,
            openAiApiKey: openAiApiKey,
            anthropicApiKey: anthropicApiKey);
        var imageProcessor = new ImageProcessor();
        var logic = new MapCommandLogic(nameMatchingService, imageProcessor);

        var result = await logic.ExecuteAsync(
            inputCsvPath,
            photosDir,
            filenamePattern,
            photoManifest,
            outputDirectory: executionDirectory,
            nameModel,
            confidenceThreshold,
            useAi,
            aiSecondPass,
            uiProgress: uiProgress,
            cancellationToken: cancellationToken,
            aiTrace: false,
            aiOnly: aiOnly,
            log: log);

        return new MapCliResult
        {
            ExitCode = 0,
            PlayersProcessed = result.PlayersProcessed,
            PlayersMatched = result.PlayersMatched,
            OutputCsvPath = result.OutputPath
        };
    }
}
