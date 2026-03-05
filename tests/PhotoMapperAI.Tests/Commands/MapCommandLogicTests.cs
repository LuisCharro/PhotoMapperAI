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
        Assert.Equal(0, result.DirectIdMatches);
        Assert.Equal(2, result.StringMatches);
        Assert.Equal(2, result.FirstRoundMatches);
        Assert.Equal(0, result.AiFirstPassMatches);
        Assert.Equal(0, result.AiSecondPassMatches);
        Assert.Equal(0, result.AiMatches);

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
        Assert.Equal(0, result.StringMatches);
        Assert.Equal(2, result.FirstRoundMatches);
        Assert.Equal(0, result.AiFirstPassMatches);
        Assert.Equal(0, result.AiSecondPassMatches);
        Assert.Equal(0, result.AiMatches);
    }

    [Fact]
    public async Task ExecuteAsync_BatchAiRejected_FallsBackToIndividualAiAndMapsPlayer()
    {
        using var temp = new TestWorkspace();

        var inputCsvPath = temp.WriteFile(
            "players_ai.csv",
            "PlayerId,TeamId,FamilyName,SurName,External_Player_ID\n" +
            "1,10,,Pepe,\n");

        var photosDir = temp.CreateDirectory("photos");
        temp.WriteFile(Path.Combine("photos", "901_photo.jpg"), "photo-1");
        var manifestPath = temp.WriteFile(
            "manifest.json",
            """
            {
              "photos": {
                "901_photo.jpg": {
                  "External_Player_ID": "901",
                  "fullName": "Kepler Laveran"
                }
              }
            }
            """);

        var map = new MapCommandLogic(new BatchRejectingNameMatchingService(), new ImageProcessor());

        var result = await map.ExecuteAsync(
            inputCsvPath,
            photosDir,
            filenamePattern: null,
            photoManifest: manifestPath,
            outputDirectory: temp.Root,
            nameModel: "test-model",
            confidenceThreshold: 0.8,
            useAi: true,
            aiSecondPass: false);

        Assert.Equal(1, result.PlayersProcessed);
        Assert.Equal(1, result.PlayersMatched);
        Assert.Equal(0, result.DirectIdMatches);
        Assert.Equal(0, result.StringMatches);
        Assert.Equal(0, result.FirstRoundMatches);
        Assert.Equal(1, result.AiFirstPassMatches);
        Assert.Equal(0, result.AiSecondPassMatches);
        Assert.Equal(1, result.AiMatches);

        var outputCsvPath = Path.Combine(temp.Root, "mapped_players_ai.csv");
        var extractor = new DatabaseExtractor();
        var outputPlayers = await extractor.ReadCsvAsync(outputCsvPath);
        var mappedPlayer = Assert.Single(outputPlayers);

        Assert.Equal("901", mappedPlayer.External_Player_ID);
        Assert.True(mappedPlayer.ValidMapping);
        Assert.True(mappedPlayer.Confidence >= 0.8);
    }

    [Fact]
    public async Task ExecuteAsync_PortugueseNicknameChico_DeterministicallyMapsFranciscoConceicao()
    {
        using var temp = new TestWorkspace();

        var inputCsvPath = temp.WriteFile(
            "players_portugal.csv",
            "PlayerId,TeamId,FamilyName,SurName,External_Player_ID\n" +
            "1,10,Conceicao,Francisco Chico,\n");

        var photosDir = temp.CreateDirectory("photos");
        temp.WriteFile(Path.Combine("photos", "901_photo.jpg"), "photo-1");
        temp.WriteFile(Path.Combine("photos", "902_photo.jpg"), "photo-2");
        var manifestPath = temp.WriteFile(
            "manifest.json",
            """
            {
              "photos": {
                "901_photo.jpg": {
                  "External_Player_ID": "901",
                  "fullName": "Francisco Fernandes Conceição"
                },
                "902_photo.jpg": {
                  "External_Player_ID": "902",
                  "fullName": "Rafael Alexandre Conceição Leão"
                }
              }
            }
            """);

        var map = new MapCommandLogic(new NoOpNameMatchingService(), new ImageProcessor());

        var result = await map.ExecuteAsync(
            inputCsvPath,
            photosDir,
            filenamePattern: null,
            photoManifest: manifestPath,
            outputDirectory: temp.Root,
            nameModel: "test-model",
            confidenceThreshold: 0.8,
            useAi: false,
            aiSecondPass: false);

        Assert.Equal(1, result.PlayersProcessed);
        Assert.Equal(1, result.PlayersMatched);
        Assert.Equal(1, result.StringMatches);
        Assert.Equal(0, result.DirectIdMatches);
        Assert.Equal(1, result.FirstRoundMatches);
        Assert.Equal(0, result.AiFirstPassMatches);
        Assert.Equal(0, result.AiSecondPassMatches);
        Assert.Equal(0, result.AiMatches);

        var outputCsvPath = Path.Combine(temp.Root, "mapped_players_portugal.csv");
        var extractor = new DatabaseExtractor();
        var outputPlayers = await extractor.ReadCsvAsync(outputCsvPath);
        var mappedPlayer = Assert.Single(outputPlayers);

        Assert.Equal("901", mappedPlayer.External_Player_ID);
        Assert.True(mappedPlayer.ValidMapping);
        Assert.True(mappedPlayer.Confidence >= 0.8);
    }

    [Fact]
    public async Task ExecuteAsync_AiQuotaExceeded_DoesNotFailAndMarksPlayerUnmapped()
    {
        using var temp = new TestWorkspace();

        var inputCsvPath = temp.WriteFile(
            "players_quota.csv",
            "PlayerId,TeamId,FamilyName,SurName,External_Player_ID\n" +
            "1,10,Coach,Sylvinho,\n");

        var photosDir = temp.CreateDirectory("photos");
        temp.WriteFile(Path.Combine("photos", "901_photo.jpg"), "photo-1");
        var manifestPath = temp.WriteFile(
            "manifest.json",
            """
            {
              "photos": {
                "901_photo.jpg": {
                  "External_Player_ID": "901",
                  "fullName": "Mendes De Campo Sylvio"
                }
              }
            }
            """);

        var map = new MapCommandLogic(new QuotaExceededNameMatchingService(), new ImageProcessor());

        var result = await map.ExecuteAsync(
            inputCsvPath,
            photosDir,
            filenamePattern: null,
            photoManifest: manifestPath,
            outputDirectory: temp.Root,
            nameModel: "test-model",
            confidenceThreshold: 0.8,
            useAi: true,
            aiSecondPass: true);

        Assert.Equal(1, result.PlayersProcessed);
        Assert.Equal(0, result.PlayersMatched);
        Assert.Equal(0, result.DirectIdMatches);
        Assert.Equal(0, result.StringMatches);
        Assert.Equal(0, result.FirstRoundMatches);
        Assert.Equal(0, result.AiFirstPassMatches);
        Assert.Equal(0, result.AiSecondPassMatches);
        Assert.Equal(0, result.AiMatches);

        var outputCsvPath = Path.Combine(temp.Root, "mapped_players_quota.csv");
        var extractor = new DatabaseExtractor();
        var outputPlayers = await extractor.ReadCsvAsync(outputCsvPath);
        var player = Assert.Single(outputPlayers);

        Assert.True(string.IsNullOrWhiteSpace(player.External_Player_ID));
        Assert.False(player.ValidMapping);
    }

    [Fact]
    public async Task ExecuteAsync_ScottishNicknameAndSurnameVariant_MapsWithoutSwap()
    {
        using var temp = new TestWorkspace();

        var inputCsvPath = temp.WriteFile(
            "players_scotland.csv",
            "PlayerId,TeamId,FamilyName,SurName,External_Player_ID\n" +
            "1,10,Clark,Zander,\n" +
            "2,10,Clarke,Steve,\n");

        var photosDir = temp.CreateDirectory("photos");
        temp.WriteFile(Path.Combine("photos", "250051123_photo.jpg"), "photo-1");
        temp.WriteFile(Path.Combine("photos", "8961_photo.jpg"), "photo-2");
        var manifestPath = temp.WriteFile(
            "manifest.json",
            """
            {
              "photos": {
                "250051123_photo.jpg": {
                  "External_Player_ID": "250051123",
                  "fullName": "Alexander Clark"
                },
                "8961_photo.jpg": {
                  "External_Player_ID": "8961",
                  "fullName": "Stephen Clarke"
                }
              }
            }
            """);

        var map = new MapCommandLogic(new NoOpNameMatchingService(), new ImageProcessor());

        var result = await map.ExecuteAsync(
            inputCsvPath,
            photosDir,
            filenamePattern: null,
            photoManifest: manifestPath,
            outputDirectory: temp.Root,
            nameModel: "test-model",
            confidenceThreshold: 0.8,
            useAi: false,
            aiSecondPass: false);

        Assert.Equal(2, result.PlayersProcessed);
        Assert.Equal(2, result.PlayersMatched);
        Assert.Equal(0, result.DirectIdMatches);
        Assert.Equal(2, result.StringMatches);
        Assert.Equal(2, result.FirstRoundMatches);
        Assert.Equal(0, result.AiFirstPassMatches);
        Assert.Equal(0, result.AiSecondPassMatches);
        Assert.Equal(0, result.AiMatches);

        var outputCsvPath = Path.Combine(temp.Root, "mapped_players_scotland.csv");
        var extractor = new DatabaseExtractor();
        var outputPlayers = await extractor.ReadCsvAsync(outputCsvPath);

        var zander = outputPlayers.Single(p => p.FullName == "Clark Zander");
        var steve = outputPlayers.Single(p => p.FullName == "Clarke Steve");

        Assert.Equal("250051123", zander.External_Player_ID);
        Assert.Equal("8961", steve.External_Player_ID);
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

    private sealed class BatchRejectingNameMatchingService : INameMatchingService
    {
        public string ModelName => "batch-rejecting";

        public Task<MatchResult> CompareNamesAsync(string name1, string name2)
        {
            var isPepeMatch =
                string.Equals(name1, "Pepe", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(name2, "Kepler Laveran", StringComparison.OrdinalIgnoreCase);

            return Task.FromResult(new MatchResult
            {
                IsMatch = isPepeMatch,
                Confidence = isPepeMatch ? 0.96 : 0.05,
                Metadata = new Dictionary<string, string>
                {
                    { "reason", isPepeMatch ? "individual_fallback_match" : "individual_no_match" }
                }
            });
        }

        public Task<List<MatchResult>> CompareNamesBatchAsync(string baseName, List<string> candidateNames)
            => Task.FromResult(candidateNames.Select(_ => new MatchResult { IsMatch = false, Confidence = 0.0 }).ToList());

        public Task<NameComparisonBatchResult> CompareNamePairsBatchAsync(IReadOnlyList<NameComparisonPair> comparisons)
        {
            var results = comparisons
                .Select(_ => new MatchResult
                {
                    IsMatch = false,
                    Confidence = 0.0,
                    Metadata = new Dictionary<string, string> { { "reason", "batch_rejected" } }
                })
                .ToList();

            return Task.FromResult(new NameComparisonBatchResult(results, 1, 10, 10, 20));
        }
    }

    private sealed class QuotaExceededNameMatchingService : INameMatchingService
    {
        public string ModelName => "quota-exceeded";

        public Task<MatchResult> CompareNamesAsync(string name1, string name2)
            => throw new OllamaQuotaExceededException("Weekly usage limit reached");

        public Task<List<MatchResult>> CompareNamesBatchAsync(string baseName, List<string> candidateNames)
            => throw new OllamaQuotaExceededException("Weekly usage limit reached");

        public Task<NameComparisonBatchResult> CompareNamePairsBatchAsync(IReadOnlyList<NameComparisonPair> comparisons)
            => throw new OllamaQuotaExceededException("Weekly usage limit reached");
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
