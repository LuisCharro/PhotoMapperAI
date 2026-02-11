using McMaster.Extensions.CommandLineUtils;
using PhotoMapperAI.Services.AI;
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
  photomapperai extract -inputSqlPath get_teams.sql -teamId 10 -outputName teams.csv
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

    public async Task<int> OnExecuteAsync()
    {
        Console.WriteLine("Extract Command");
        Console.WriteLine("================");
        Console.WriteLine($"SQL File: {InputSqlPath}");
        Console.WriteLine($"Connection String: {ConnectionStringPath}");
        Console.WriteLine($"Team ID: {TeamId}");
        Console.WriteLine($"Output: {OutputName}");
        Console.WriteLine();

        try
        {
            // Read SQL query
            var sqlQuery = await File.ReadAllTextAsync(InputSqlPath);

            // Read connection string
            var connectionString = await File.ReadAllTextAsync(ConnectionStringPath);

            // Build parameters
            var parameters = new Dictionary<string, object>
            {
                { "TeamId", TeamId }
            };

            // Create database extractor
            var extractor = new Services.Database.DatabaseExtractor();

            // Determine output path
            var outputCsvPath = Path.Combine(Directory.GetCurrentDirectory(), OutputName);

            // Extract data
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
    [Option(ShortName = "i", LongName = "inputCsvPath", Description = "Path to input CSV file")]
    public string InputCsvPath { get; set; } = string.Empty;

    [Option(ShortName = "p", LongName = "photosDir", Description = "Directory containing photo files")]
    public string PhotosDir { get; set; } = string.Empty;

    [Option(ShortName = "f", LongName = "filenamePattern", Description = "Filename pattern template (e.g., '{id}_{family}_{sur}.png')")]
    public string? FilenamePattern { get; set; }

    [Option(ShortName = "m", LongName = "photoManifest", Description = "Path to photo manifest JSON file")]
    public string? PhotoManifest { get; set; }

    [Option(ShortName = "n", LongName = "nameModel", Description = "Ollama model for name matching (default: qwen2.5:7b)")]
    public string NameModel { get; set; } = "qwen2.5:7b";

    [Option(ShortName = "t", LongName = "confidenceThreshold", Description = "Minimum confidence for valid match (default: 0.9)")]
    public double ConfidenceThreshold { get; set; } = 0.9;

    public async Task<int> OnExecuteAsync()
    {
        // Create Ollama name matching service
        var nameMatchingService = new Services.AI.OllamaNameMatchingService(
            modelName: NameModel,
            confidenceThreshold: ConfidenceThreshold
        );

        // Create image processor
        var imageProcessor = new Services.Image.ImageProcessor();

        // Create map command logic handler
        var logic = new MapCommandLogic(nameMatchingService, imageProcessor);

        // Execute map command
        return await logic.ExecuteAsync(
            InputCsvPath,
            PhotosDir,
            FilenamePattern,
            PhotoManifest,
            NameModel,
            ConfidenceThreshold
        );
    }
}

/// <summary>
/// GeneratePhotos command - generate portraits with face detection
/// </summary>
[Command("generatephotos", Description = "Generate portraits with face detection", ExtendedHelpText = @"
Generates portrait photos from full-body images using face and eye detection.
Supports OpenCV DNN, Haar Cascades, YOLOv8-Face, and Ollama Vision models.

Fallback mode: Provide comma-separated models to try each in order.
Example: llava:7b,qwen3-vl will try llava:7b first, fall back to qwen3-vl if it fails.

Examples:
  photomapperai generatephotos -inputCsvPath players.csv -processedPhotosOutputPath ./portraits -format jpg
  photomapperai generatephotos -inputCsvPath players.csv -processedPhotosOutputPath ./portraits -format jpg -faceDetection opencv-dnn
  photomapperai generatephotos -inputCsvPath players.csv -processedPhotosOutputPath ./portraits -format jpg -faceDetection qwen3-vl
  photomapperai generatephotos -inputCsvPath players.csv -processedPhotosOutputPath ./portraits -format jpg -faceDetection llava:7b,qwen3-vl
  photomapperai generatephotos -inputCsvPath players.csv -processedPhotosOutputPath ./portraits -format jpg -portraitOnly
")]
public class GeneratePhotosCommand
{
    [Option(ShortName = "i", LongName = "inputCsvPath", Description = "Path to input CSV file")]
    public string InputCsvPath { get; set; } = string.Empty;

    [Option(ShortName = "p", LongName = "photosDir", Description = "Directory containing source photo files")]
    public string PhotosDir { get; set; } = string.Empty;

    [Option(ShortName = "o", LongName = "processedPhotosOutputPath", Description = "Output directory for portrait photos")]
    public string ProcessedPhotosOutputPath { get; set; } = string.Empty;

    [Option(ShortName = "f", LongName = "format", Description = "Image format: jpg, png (default: jpg)")]
    public string Format { get; set; } = "jpg";

    [Option(ShortName = "d", LongName = "faceDetection", Description = "Face detection model: opencv-dnn, yolov8-face, llava:7b, qwen3-vl, or comma-separated fallback list (default: llava:7b,qwen3-vl)")]
    public string FaceDetection { get; set; } = "llava:7b,qwen3-vl";

    [Option(ShortName = "c", LongName = "crop", Description = "Crop method: generic, ai (default: generic)")]
    public string Crop { get; set; } = "generic";

    [Option(ShortName = "po", LongName = "portraitOnly", Description = "Skip face detection, use existing results")]
    public bool PortraitOnly { get; set; } = false;

    [Option(ShortName = "fw", LongName = "faceWidth", Description = "Portrait width in pixels (default: 800)")]
    public int FaceWidth { get; set; } = 800;

    [Option(ShortName = "fh", LongName = "faceHeight", Description = "Portrait height in pixels (default: 1000)")]
    public int FaceHeight { get; set; } = 1000;

    public async Task<int> OnExecuteAsync()
    {
        // Create face detection service
        var faceDetectionService = CreateFaceDetectionService(FaceDetection);

        // Initialize OpenCV service if needed
        if (faceDetectionService is OpenCVDNNFaceDetectionService cvService)
        {
            await cvService.InitializeAsync();
        }

        // Create image processor
        var imageProcessor = new Services.Image.ImageProcessor();

        // Create generate photos command logic handler
        var logic = new GeneratePhotosCommandLogic(faceDetectionService, imageProcessor);

        // Execute generate photos command
        return await logic.ExecuteAsync(
            InputCsvPath,
            PhotosDir,
            ProcessedPhotosOutputPath,
            Format,
            FaceDetection,
            Crop,
            PortraitOnly,
            FaceWidth,
            FaceHeight
        );
    }

    /// <summary>
    /// Creates the appropriate face detection service based on model name.
    /// If model contains commas, creates a fallback service that tries each model in order.
    /// </summary>
    private IFaceDetectionService CreateFaceDetectionService(string model)
    {
        // Check if fallback mode (comma-separated models)
        if (model.Contains(','))
        {
            return new FallbackFaceDetectionService(model);
        }

        // Single model mode
        return model.ToLower() switch
        {
            "opencv-dnn" => new OpenCVDNNFaceDetectionService(),
            "yolov8-face" => new OpenCVDNNFaceDetectionService(),
            var ollamaModel when ollamaModel.Contains("llava") || ollamaModel.Contains("qwen3-vl") => new OllamaFaceDetectionService(modelName: model),
            _ => throw new ArgumentException($"Unknown face detection model: {model}")
        };
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
  photomapperai benchmark -faceModels opencv-dnn,yolov8-face,llava:7b,qwen3-vl -testDataPath ./tests/Data
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
