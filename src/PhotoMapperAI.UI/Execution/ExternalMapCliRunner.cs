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
        public int PlayersMappedDirectId { get; set; }
        public int PlayersMappedDeterministic { get; set; }
        public int PlayersMappedFirstRound { get; set; }
        public int PlayersMappedAiPass1 { get; set; }
        public int PlayersMappedAiPass2 { get; set; }
        public int PlayersMappedAiTotal { get; set; }
        public int ManualEditsPreserved { get; set; }
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
        string? zaiApiKey,
        string? minimaxApiKey,
        CancellationToken cancellationToken,
        IProgress<string>? log,
        IProgress<(int processed, int total, string current)>? uiProgress = null)
    {
        _ = projectRootDirectory;

        var nameMatchingService = NameMatchingServiceFactory.Create(
            nameModel,
            confidenceThreshold: confidenceThreshold,
            openAiApiKey: openAiApiKey,
            anthropicApiKey: anthropicApiKey,
            zaiApiKey: zaiApiKey,
            minimaxApiKey: minimaxApiKey);
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
            PlayersMappedDirectId = result.DirectIdMatches,
            PlayersMappedDeterministic = result.StringMatches,
            PlayersMappedFirstRound = result.FirstRoundMatches,
            PlayersMappedAiPass1 = result.AiFirstPassMatches,
            PlayersMappedAiPass2 = result.AiSecondPassMatches,
            PlayersMappedAiTotal = result.AiMatches,
            ManualEditsPreserved = result.ManualEditsPreserved,
            OutputCsvPath = result.OutputPath
        };
    }
}
