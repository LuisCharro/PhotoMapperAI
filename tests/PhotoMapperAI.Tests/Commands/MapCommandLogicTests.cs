using PhotoMapperAI.Commands;
using PhotoMapperAI.Models;
using PhotoMapperAI.Services.AI;
using PhotoMapperAI.Services.Database;
using PhotoMapperAI.Services.Image;

namespace PhotoMapperAI.Tests.Commands;

public class MapCommandLogicTests
{
    [Fact]
    public async Task ExecuteAsync_UnmappedCsvAndPhotos_GeneratesMappedCsvWithPhotoIds()
    {
        using var temp = new TestWorkspace();

        var inputCsvPath = temp.WriteFile(
            "players_unmapped.csv",
            "PlayerId,TeamId,FamilyName,SurName,External_Player_ID\n" +
            "1,10,Smith,John,\n" +
            "2,10,Doe,Jane,\n");

        var photosDir = temp.CreateDirectory("photos");
        temp.WriteFile(Path.Combine("photos", "501_Smith_John.jpg"), "photo-1");
        temp.WriteFile(Path.Combine("photos", "502_Doe_Jane.jpg"), "photo-2");

        var map = new MapCommandLogic(new NoOpNameMatchingService(), new ImageProcessor());

        var result = await map.ExecuteAsync(
            inputCsvPath,
            photosDir,
            filenamePattern: "{id}_{family}_{sur}.jpg",
            photoManifest: null,
            outputDirectory: temp.Root,
            nameModel: "test-model",
            confidenceThreshold: 0.8,
            useAi: false,
            aiSecondPass: false);

        Assert.Equal(2, result.PlayersProcessed);
        Assert.Equal(2, result.PlayersMatched);

        var outputCsvPath = Path.Combine(temp.Root, "mapped_players_unmapped.csv");
        Assert.True(File.Exists(outputCsvPath));

        var extractor = new DatabaseExtractor();
        var outputPlayers = await extractor.ReadCsvAsync(outputCsvPath);

        Assert.Collection(
            outputPlayers.OrderBy(p => p.PlayerId),
            p => Assert.Equal("501", p.External_Player_ID),
            p => Assert.Equal("502", p.External_Player_ID));
    }

    [Fact]
    public async Task ExecuteAsync_InputCsvWithLegacyExternalIdHeader_KeepsBackwardCompatibility()
    {
        using var temp = new TestWorkspace();

        var inputCsvPath = temp.WriteFile(
            "players_legacy.csv",
            "PlayerId,TeamId,FamilyName,SurName,ExternalId\n" +
            "1,10,Smith,John,501\n" +
            "2,10,Doe,Jane,502\n");

        var photosDir = temp.CreateDirectory("photos");
        temp.WriteFile(Path.Combine("photos", "501_Any_Name.jpg"), "photo-1");
        temp.WriteFile(Path.Combine("photos", "502_Any_Name.jpg"), "photo-2");

        var map = new MapCommandLogic(new NoOpNameMatchingService(), new ImageProcessor());

        var result = await map.ExecuteAsync(
            inputCsvPath,
            photosDir,
            filenamePattern: "{id}_{family}_{sur}.jpg",
            photoManifest: null,
            outputDirectory: temp.Root,
            nameModel: "test-model",
            confidenceThreshold: 0.8,
            useAi: false,
            aiSecondPass: false);

        Assert.Equal(2, result.PlayersProcessed);
        Assert.Equal(2, result.PlayersMatched);
        Assert.Equal(2, result.DirectIdMatches);
    }

    private sealed class NoOpNameMatchingService : INameMatchingService
    {
        public string ModelName => "noop";

        public Task<MatchResult> CompareNamesAsync(string name1, string name2)
            => Task.FromResult(new MatchResult { IsMatch = false, Confidence = 0.0 });

        public Task<List<MatchResult>> CompareNamesBatchAsync(string baseName, List<string> candidateNames)
            => Task.FromResult(new List<MatchResult>());

        public Task<NameComparisonBatchResult> CompareNamePairsBatchAsync(IReadOnlyList<NameComparisonPair> comparisons)
            => Task.FromResult(new NameComparisonBatchResult(new List<MatchResult>(), 0, 0, 0, 0));
    }

    private sealed class TestWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"photomapperai-map-tests-{Guid.NewGuid():N}");

        public TestWorkspace()
        {
            Directory.CreateDirectory(Root);
        }

        public string CreateDirectory(string relativePath)
        {
            var full = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(full);
            return full;
        }

        public string WriteFile(string relativePath, string content)
        {
            var full = Path.Combine(Root, relativePath);
            var dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(full, content);
            return full;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }
}
