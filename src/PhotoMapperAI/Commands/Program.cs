using McMaster.Extensions.CommandLineUtils;
using PhotoMapperAI.Services.AI;
using PhotoMapperAI.Services.Diagnostics;
using PhotoMapperAI.Commands;
using PhotoMapperAI.Utils;
using System.Reflection;

namespace PhotoMapperAI;

/// <summary>
/// PhotoMapperAI - AI-powered photo mapping tool for sports organizations
/// </summary>
[Command(Name = "photomapperai", Description = "AI-powered photo mapping tool for sports organizations")]
[Subcommand(typeof(ExtractCommand))]
[Subcommand(typeof(MapCommand))]
[Subcommand(typeof(GeneratePhotosCommand))]
[Subcommand(typeof(BenchmarkCommand))]
[Subcommand(typeof(BenchmarkCompareCommand))]
[HelpOption("--help", ShortName = "h", Description = "Show help")]
[VersionOptionFromMember(MemberName = nameof(GetVersion))]
public class Program
{
    public static Task<int> Main(string[] args) => CommandLineApplication.ExecuteAsync<Program>(args);

    public static int OnExecute(CommandLineApplication app)
    {
        Console.WriteLine("PhotoMapperAI - AI-powered photo mapping tool");
        Console.WriteLine("Use --help for usage information");
        Console.WriteLine(" ");
        Console.WriteLine("Available commands:");
        Console.WriteLine("  extract       - Extract player data from database to CSV");
        Console.WriteLine("  map           - Map photos to players using AI");
        Console.WriteLine("  generatephotos - Generate portraits with face detection");
        Console.WriteLine("  benchmark     - Run model benchmarks");
        Console.WriteLine("  benchmark-compare - Compare benchmark JSON results");
        return 0;
    }

    public static string GetVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return $"PhotoMapperAI v{version}";
    }
}

/// <summary>
/// Extract command - export player data from database to CSV
/// </summary>
[Command("extract", Description = "Extract player data from database to CSV", ExtendedHelpText = @"
Extracts player data from a user-provided database and exports to CSV format.
Requires SQL query file and database connection string.

Examples:
  photomapperai extract -inputSqlPath get_players.sql -teamId 10 -outputName team.csv
  photomapperai extract -inputSqlPath get_teams.sql -outputName teams.csv --extractTeams
")]
public class ExtractCommand
{
    [Option(ShortName = "i", LongName = "inputSqlPath", Description = "Path to SQL query file")]
    public string InputSqlPath { get; set; } = string.Empty;

    [Option(ShortName = "c", LongName = "connectionStringPath", Description = "Path to database connection string file")]
    public string ConnectionStringPath { get; set; } = string.Empty;

    [Option(ShortName = "t", LongName = "teamId", Description = "Team ID to filter")]
    public int TeamId { get; set; }

    [Option(ShortName = "o", LongName = "outputName", Description = "Output CSV filename")]
    public string OutputName { get; set; } = string.Empty;

    [Option(ShortName = "et", LongName = "extractTeams", Description = "Extract teams instead of players (SQL should return TeamId, TeamName)")]
    public bool ExtractTeams { get; set; }

    public async Task<int> OnExecuteAsync()
    {
        Console.WriteLine("Extract Command");
        Console.WriteLine("================");
        Console.WriteLine($"SQL File: {InputSqlPath}");
        Console.WriteLine($"Connection String: {ConnectionStringPath}");
        Console.WriteLine($"Team ID: {TeamId}");
        Console.WriteLine($"Output: {OutputName}");
        Console.WriteLine($"Extract Teams: {ExtractTeams}");
        Console.WriteLine();

        try
        {
            // Read SQL query
            var sqlQuery = await File.ReadAllTextAsync(InputSqlPath);

            // Read connection string
            var connectionString = await File.ReadAllTextAsync(ConnectionStringPath);

            // Create database extractor
            var extractor = new Services.Database.DatabaseExtractor();

            // Determine output path
            var outputCsvPath = Path.Combine(Directory.GetCurrentDirectory(), OutputName);

            if (ExtractTeams)
            {
                // Extract teams data
                Console.WriteLine("Extracting team data...");
                int teamCount;
                using (var spinner = ProgressIndicator.CreateSpinner("  Reading from database"))
                {
                    teamCount = await extractor.ExtractTeamsToCsvAsync(
                        connectionString,
                        sqlQuery,
                        outputCsvPath
                    );
                }

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Extracted {teamCount} teams to {OutputName}");
                Console.ResetColor();
            }
            else
            {
                // Build parameters for players extraction
                var parameters = new Dictionary<string, object>
                {
                    { "TeamId", TeamId }
                };

                // Extract players data
                Console.WriteLine("Extracting player data...");
                int playerCount;
                using (var spinner = ProgressIndicator.CreateSpinner("  Reading from database"))
                {
                    playerCount = await extractor.ExtractPlayersToCsvAsync(
                        connectionString,
                        sqlQuery,
                        parameters,
                        outputCsvPath
                    );
                }

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Extracted {playerCount} players to {OutputName}");
                Console.ResetColor();
            }

            return 0;
        }
        catch (FileNotFoundException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ File not found: {ex.FileName}");
            Console.ResetColor();
            return 1;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }
}

/// <summary>
/// Map command - match photos to players using AI
/// </summary>
[Command("map", Description = "Map photos to players using AI", ExtendedHelpText = @"
Maps photo files to player records using AI-powered name matching.
Supports automatic filename pattern detection, user-specified templates, and photo manifests.

Examples:
  photomapperai map -inputCsvPath players.csv -photosDir ./photos
  photomapperai map -inputCsvPath players.csv -photosDir ./photos -filenamePattern '{id}_{family}_{sur}.png'
  photomapperai map -inputCsvPath players.csv -photosDir ./photos -photoManifest manifest.json
")]
public class MapCommand
{
    private const double MinConfidenceThreshold = 0.8;
    [Option(ShortName = "i", LongName = "inputCsvPath", Description = "Path to input CSV file")]
    public string InputCsvPath { get; set; } = string.Empty;

    [Option(ShortName = "p", LongName = "photosDir", Description = "Directory containing photo files")]
    public string PhotosDir { get; set; } = string.Empty;

    [Option(ShortName = "f", LongName = "filenamePattern", Description = "Filename pattern template (e.g., '{id}_{family}_{sur}.png')")]
    public string? FilenamePattern { get; set; }

    [Option(ShortName = "m", LongName = "photoManifest", Description = "Path to photo manifest JSON file")]
    public string? PhotoManifest { get; set; }

    [Option(ShortName = "n", LongName = "nameModel", Description = "Name matching model identifier (e.g., qwen2.5:7b, ollama:qwen2.5:7b, openai:gpt-4o-mini, anthropic:claude-3-5-sonnet)")]
    public string NameModel { get; set; } = "qwen2.5:7b";

    [Option(ShortName = "t", LongName = "confidenceThreshold", Description = "Minimum confidence for valid match (default: 0.8)")]
    public double ConfidenceThreshold { get; set; } = MinConfidenceThreshold;

    [Option(ShortName = "a", LongName = "useAI", Description = "Enable AI name matching (slower, optional)")]
    public bool UseAi { get; set; } = false;

    [Option(ShortName = "ap", LongName = "aiSecondPass", Description = "Run a second AI pass on remaining unmatched players")]
    public bool AiSecondPass { get; set; } = false;

    [Option(ShortName = "ao", LongName = "aiOnly", Description = "Skip deterministic name matching and use AI for all unresolved players")]
    public bool AiOnly { get; set; } = false;

    [Option(ShortName = "at", LongName = "aiTrace", Description = "Print structured per-player AI evaluation trace lines")]
    public bool AiTrace { get; set; } = false;

    [Option(ShortName = "oak", LongName = "openaiApiKey", Description = "OpenAI API key override (optional, in-memory only for this command run)")]
    public string? OpenAiApiKey { get; set; }

    [Option(ShortName = "aak", LongName = "anthropicApiKey", Description = "Anthropic API key override (optional, in-memory only for this command run)")]
    public string? AnthropicApiKey { get; set; }

    public async Task<int> OnExecuteAsync()
    {
        if (AiOnly && !UseAi)
        {
            Console.WriteLine("AI-only mode enables AI matching automatically.");
            UseAi = true;
        }

        if (ConfidenceThreshold < MinConfidenceThreshold)
        {
            Console.WriteLine($"Confidence threshold raised to minimum {MinConfidenceThreshold:0.0}.");
            ConfidenceThreshold = MinConfidenceThreshold;
        }

        var preflight = await PreflightChecker.CheckMapAsync(
            UseAi,
            NameModel,
            openAiApiKey: OpenAiApiKey,
            anthropicApiKey: AnthropicApiKey);
        if (!preflight.IsOk)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(preflight.BuildMessage());
            Console.ResetColor();
            return 1;
        }

        if (preflight.Warnings.Count > 0 || preflight.MissingOllamaModels.Count > 0 || preflight.MissingOpenCvFiles.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(preflight.BuildWarningMessage());
            Console.ResetColor();
        }

        // Create provider-aware name matching service.
        var nameMatchingService = NameMatchingServiceFactory.Create(
            NameModel,
            confidenceThreshold: ConfidenceThreshold,
            openAiApiKey: OpenAiApiKey,
            anthropicApiKey: AnthropicApiKey
        );

        // Create image processor
        var imageProcessor = new Services.Image.ImageProcessor();

        // Create map command logic handler
        var logic = new MapCommandLogic(nameMatchingService, imageProcessor);

        // Execute map command
        var result = await logic.ExecuteAsync(
            InputCsvPath,
            PhotosDir,
            FilenamePattern,
            PhotoManifest,
            outputDirectory: null,
            NameModel,
            ConfidenceThreshold,
            UseAi,
            UseAi && AiSecondPass,
            aiTrace: AiTrace,
            aiOnly: AiOnly,
            log: null
        );

        return 0;
    }
}

/// <summary>
/// Benchmark command - run model benchmarks
/// </summary>
[Command("benchmark", Description = "Run model benchmarks", ExtendedHelpText = @"
Benchmarks multiple AI models and compares performance metrics.
Collects accuracy, speed, and confidence scores for name matching and face detection.

Examples:
  photomapperai benchmark -nameModels qwen2.5:7b,qwen3:8b,llava:7b -testDataPath ./tests/Data
  photomapperai benchmark -faceModels opencv-dnn,yolov8-face,llava:7b -testDataPath ./tests/Data
  photomapperai benchmark -nameModels qwen2.5:7b,qwen3:8b -faceModels opencv-dnn,llava:7b -testDataPath ./tests/Data
")]
public class BenchmarkCommand
{
    [Option(ShortName = "n", LongName = "nameModels", Description = "Comma-separated list of name matching models")]
    public string? NameModels { get; set; }

    [Option(ShortName = "f", LongName = "faceModels", Description = "Comma-separated list of face detection models")]
    public string? FaceModels { get; set; }

    [Option(ShortName = "t", LongName = "testDataPath", Description = "Path to test data directory")]
    public string TestDataPath { get; set; } = string.Empty;

    [Option(ShortName = "o", LongName = "outputPath", Description = "Path for benchmark results (default: benchmark-results/)")]
    public string OutputPath { get; set; } = "benchmark-results/";

    public async Task<int> OnExecuteAsync()
    {
        // Create image processor
        var imageProcessor = new Services.Image.ImageProcessor();

        // Create benchmark command logic handler
        var logic = new BenchmarkCommandLogic(imageProcessor);

        // Execute benchmark command
        return await logic.ExecuteAsync(
            NameModels,
            FaceModels,
            TestDataPath,
            OutputPath
        );
    }
}
