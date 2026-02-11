using McMaster.Extensions.CommandLineUtils;
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
    [Option(ShortName = "i", LongName = "inputSqlPath", Required = true, Description = "Path to SQL query file")]
    public string InputSqlPath { get; set; } = string.Empty;

    [Option(ShortName = "c", LongName = "connectionStringPath", Required = true, Description = "Path to database connection string file")]
    public string ConnectionStringPath { get; set; } = string.Empty;

    [Option(ShortName = "t", LongName = "teamId", Required = true, Description = "Team ID to filter")]
    public int TeamId { get; set; }

    [Option(ShortName = "o", LongName = "outputName", Required = true, Description = "Output CSV filename")]
    public string OutputName { get; set; } = string.Empty;

    public async Task<int> OnExecuteAsync()
    {
        Console.WriteLine("Extract Command");
        Console.WriteLine("================");
        Console.WriteLine($"SQL File: {InputSqlPath}");
        Console.WriteLine($"Team ID: {TeamId}");
        Console.WriteLine($"Output: {OutputName}");
        Console.WriteLine();
        Console.WriteLine("TODO: Implement Extract command logic");

        // TODO: Implement database extraction logic
        await Task.CompletedTask;

        return 0;
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
    [Option(ShortName = "i", LongName = "inputCsvPath", Required = true, Description = "Path to input CSV file")]
    public string InputCsvPath { get; set; } = string.Empty;

    [Option(ShortName = "p", LongName = "photosDir", Required = true, Description = "Directory containing photo files")]
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
        Console.WriteLine("Map Command");
        Console.WriteLine("============");
        Console.WriteLine($"CSV File: {InputCsvPath}");
        Console.WriteLine($"Photos Dir: {PhotosDir}");
        Console.WriteLine($"Name Model: {NameModel}");
        Console.WriteLine($"Confidence Threshold: {ConfidenceThreshold}");
        Console.WriteLine();

        if (!string.IsNullOrEmpty(FilenamePattern))
            Console.WriteLine($"Filename Pattern: {FilenamePattern}");

        if (!string.IsNullOrEmpty(PhotoManifest))
            Console.WriteLine($"Photo Manifest: {PhotoManifest}");

        Console.WriteLine();
        Console.WriteLine("TODO: Implement Map command logic");

        // TODO: Implement photo mapping logic
        await Task.CompletedTask;

        return 0;
    }
}

/// <summary>
/// GeneratePhotos command - generate portraits with face detection
/// </summary>
[Command("generatephotos", Description = "Generate portraits with face detection", ExtendedHelpText = @"
Generates portrait photos from full-body images using face and eye detection.
Supports OpenCV DNN, Haar Cascades, YOLOv8-Face, and Ollama Vision models.

Examples:
  photomapperai generatephotos -inputCsvPath players.csv -processedPhotosOutputPath ./portraits -format jpg
  photomapperai generatephotos -inputCsvPath players.csv -processedPhotosOutputPath ./portraits -format jpg -faceDetection opencv-dnn
  photomapperai generatephotos -inputCsvPath players.csv -processedPhotosOutputPath ./portraits -format jpg -faceDetection qwen3-vl
  photomapperai generatephotos -inputCsvPath players.csv -processedPhotosOutputPath ./portraits -format jpg -portraitOnly
")]
public class GeneratePhotosCommand
{
    [Option(ShortName = "i", LongName = "inputCsvPath", Required = true, Description = "Path to input CSV file")]
    public string InputCsvPath { get; set; } = string.Empty;

    [Option(ShortName = "o", LongName = "processedPhotosOutputPath", Required = true, Description = "Output directory for portrait photos")]
    public string ProcessedPhotosOutputPath { get; set; } = string.Empty;

    [Option(ShortName = "f", LongName = "format", Description = "Image format: jpg, png (default: jpg)")]
    public string Format { get; set; } = "jpg";

    [Option(ShortName = "d", LongName = "faceDetection", Description = "Face detection model: opencv-dnn, yolov8-face, llava:7b, qwen3-vl (default: opencv-dnn)")]
    public string FaceDetection { get; set; } = "opencv-dnn";

    [Option(ShortName = "c", LongName = "crop", Description = "Crop method: generic, ai (default: generic)")]
    public string Crop { get; set; } = "generic";

    [Option(ShortName = "p", LongName = "portraitOnly", Description = "Skip face detection, use existing results")]
    public bool PortraitOnly { get; set; } = false;

    [Option(ShortName = "w", LongName = "faceWidth", Description = "Portrait width in pixels (default: 800)")]
    public int FaceWidth { get; set; } = 800;

    [Option(ShortName = "h", LongName = "faceHeight", Description = "Portrait height in pixels (default: 1000)")]
    public int FaceHeight { get; set; } = 1000;

    public async Task<int> OnExecuteAsync()
    {
        Console.WriteLine("Generate Photos Command");
        Console.WriteLine("======================");
        Console.WriteLine($"CSV File: {InputCsvPath}");
        Console.WriteLine($"Output Dir: {ProcessedPhotosOutputPath}");
        Console.WriteLine($"Format: {Format}");
        Console.WriteLine($"Face Detection: {FaceDetection}");
        Console.WriteLine($"Crop Method: {Crop}");
        Console.WriteLine($"Portrait Only: {PortraitOnly}");
        Console.WriteLine($"Portrait Size: {FaceWidth}x{FaceHeight}");
        Console.WriteLine();
        Console.WriteLine("TODO: Implement GeneratePhotos command logic");

        // TODO: Implement portrait generation logic
        await Task.CompletedTask;

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
  photomapperai benchmark -faceModels opencv-dnn,yolov8-face,llava:7b,qwen3-vl -testDataPath ./tests/Data
  photomapperai benchmark -nameModels qwen2.5:7b,qwen3:8b -faceModels opencv-dnn,llava:7b -testDataPath ./tests/Data
")]
public class BenchmarkCommand
{
    [Option(ShortName = "n", LongName = "nameModels", Description = "Comma-separated list of name matching models")]
    public string? NameModels { get; set; }

    [Option(ShortName = "f", LongName = "faceModels", Description = "Comma-separated list of face detection models")]
    public string? FaceModels { get; set; }

    [Option(ShortName = "t", LongName = "testDataPath", Required = true, Description = "Path to test data directory")]
    public string TestDataPath { get; set; } = string.Empty;

    [Option(ShortName = "o", LongName = "outputPath", Description = "Path for benchmark results (default: benchmark-results/)")]
    public string OutputPath { get; set; } = "benchmark-results/";

    public async Task<int> OnExecuteAsync()
    {
        Console.WriteLine("Benchmark Command");
        Console.WriteLine("================");
        Console.WriteLine($"Test Data: {TestDataPath}");
        Console.WriteLine($"Output Path: {OutputPath}");

        if (!string.IsNullOrEmpty(NameModels))
            Console.WriteLine($"Name Models: {NameModels}");

        if (!string.IsNullOrEmpty(FaceModels))
            Console.WriteLine($"Face Models: {FaceModels}");

        Console.WriteLine();
        Console.WriteLine("TODO: Implement Benchmark command logic");

        // TODO: Implement benchmark logic
        await Task.CompletedTask;

        return 0;
    }
}
