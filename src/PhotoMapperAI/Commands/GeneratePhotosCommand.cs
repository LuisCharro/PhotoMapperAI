using PhotoMapperAI.Models;
using PhotoMapperAI.Services;
using PhotoMapperAI.Services.AI;
using PhotoMapperAI.Services.Diagnostics;
using PhotoMapperAI.Services.Image;
using PhotoMapperAI.Utils;
using CsvHelper.Configuration;
using CsvHelper;
using McMaster.Extensions.CommandLineUtils;

namespace PhotoMapperAI.Commands;

/// <summary>
/// GeneratePhotos command - generate portraits with face detection
/// </summary>
public class GeneratePhotosResult
{
    public int ExitCode { get; set; }
    public int TotalPlayers { get; set; }
    public int ProcessedPlayers { get; set; }
    public int PortraitsGenerated { get; set; }
    public int PortraitsFailed { get; set; }
    public bool IsCancelled { get; set; }
}

public class GeneratePhotosCommandLogic
{
    private readonly IFaceDetectionService _faceDetectionService;
    private readonly IImageProcessor _imageProcessor;
    private readonly FaceDetectionCache? _cache;

    /// <summary>
    /// Creates a new generate photos command logic handler.
    /// </summary>
    public GeneratePhotosCommandLogic(
        IFaceDetectionService faceDetectionService,
        IImageProcessor imageProcessor,
        FaceDetectionCache? cache = null)
    {
        _faceDetectionService = faceDetectionService;
        _imageProcessor = imageProcessor;
        _cache = cache;
    }

    /// <summary>
    /// Executes the generate photos command.
    /// </summary>
    public async Task<int> ExecuteAsync(
        string inputCsvPath,
        string photosDir,
        string processedPhotosOutputPath,
        string format,
        string faceDetectionModel,
        string crop,
        bool portraitOnly,
        int portraitWidth,
        int portraitHeight,
        bool parallel,
        int parallelDegree)
    {
        var result = await ExecuteWithResultAsync(
            inputCsvPath,
            photosDir,
            processedPhotosOutputPath,
            format,
            faceDetectionModel,
            crop,
            portraitOnly,
            portraitWidth,
            portraitHeight,
            parallel,
            parallelDegree
        );

        return result.ExitCode;
    }

    /// <summary>
    /// Executes generation and returns detailed result metrics.
    /// </summary>
    public async Task<GeneratePhotosResult> ExecuteWithResultAsync(
        string inputCsvPath,
        string photosDir,
        string processedPhotosOutputPath,
        string format,
        string faceDetectionModel,
        string crop,
        bool portraitOnly,
        int portraitWidth,
        int portraitHeight,
        bool parallel,
        int parallelDegree,
        IProgress<(int processed, int total, string current)>? progress = null,
        CancellationToken cancellationToken = default,
        IProgress<string>? log = null)
    {
        var totalPlayers = 0;
        var successCount = 0;
        var failedCount = 0;
        var processedCount = 0;

        void LogLine(string message)
        {
            Console.WriteLine(message);
            log?.Report(message);
        }

        LogLine("Generate Photos Command");
        LogLine("======================");
        LogLine($"CSV File: {inputCsvPath}");
        LogLine($"Photos Dir: {photosDir}");
        LogLine($"Output Dir: {processedPhotosOutputPath}");
        LogLine($"Format: {format}");
        LogLine($"Face Detection: {faceDetectionModel}");
        LogLine($"Crop Method: {crop}");
        LogLine($"Portrait Only: {portraitOnly}");
        LogLine($"Portrait Size: {portraitWidth}x{portraitHeight}");
        LogLine($"Parallel: {parallel} (Degree: {parallelDegree})");
        LogLine(string.Empty);

        try
        {
            // Step 1: Load players from CSV
            LogLine("Loading player data...");
            var extractor = new Services.Database.DatabaseExtractor(_imageProcessor);
            var players = await extractor.ReadCsvAsync(inputCsvPath);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Loaded {players.Count} players from CSV");
            Console.ResetColor();
            log?.Report($"✓ Loaded {players.Count} players from CSV");
            LogLine(string.Empty);

            // Step 2: Create output directory
            Directory.CreateDirectory(processedPhotosOutputPath);

            // Step 3: Process each player
            var playersToProcess = players.Where(p => !string.IsNullOrEmpty(p.ExternalId)).ToList();
            totalPlayers = playersToProcess.Count;

            if (totalPlayers == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠ No players with ExternalId found to process");
                Console.ResetColor();
                log?.Report("⚠ No players with ExternalId found to process");
                return new GeneratePhotosResult
                {
                    ExitCode = 0,
                    TotalPlayers = 0
                };
            }

            LogLine($"Processing {totalPlayers} players...");
            LogLine(string.Empty);

            var progressIndicator = new ProgressIndicator("Progress", totalPlayers, useBar: true);

            if (parallel)
            {
                // Parallel processing mode
                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = parallelDegree,
                    CancellationToken = cancellationToken
                };

                await Parallel.ForEachAsync(playersToProcess, options, async (player, cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progressIndicator.Update($"{player.FullName} (ID: {player.ExternalId})");

                    var result = await ProcessPlayerAsync(
                        player,
                        photosDir,
                        processedPhotosOutputPath,
                        format,
                        portraitWidth,
                        portraitHeight,
                        portraitOnly,
                        faceDetectionModel,
                        cancellationToken
                    );

                    var currentProcessed = Interlocked.Increment(ref processedCount);
                    progress?.Report((currentProcessed, totalPlayers, player.FullName));

                    if (result.IsSuccess)
                    {
                        Interlocked.Increment(ref successCount);
                    }
                    else
                    {
                        Interlocked.Increment(ref failedCount);
                        LogLine(string.Empty);
                        Console.ForegroundColor = result.IsWarning ? ConsoleColor.Yellow : ConsoleColor.Red;
                        Console.WriteLine($"  {(result.IsWarning ? "⚠" : "✗")} {result.ErrorMessage}");
                        Console.ResetColor();
                        log?.Report($"  {(result.IsWarning ? "⚠" : "✗")} {result.ErrorMessage}");
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                });
            }
            else
            {
                // Sequential processing mode
                foreach (var player in playersToProcess)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progressIndicator.Update($"{player.FullName} (ID: {player.ExternalId})");

                    var result = await ProcessPlayerAsync(
                        player,
                        photosDir,
                        processedPhotosOutputPath,
                        format,
                        portraitWidth,
                        portraitHeight,
                        portraitOnly,
                        faceDetectionModel,
                        cancellationToken
                    );

                    processedCount++;
                    progress?.Report((processedCount, totalPlayers, player.FullName));

                    if (result.IsSuccess)
                    {
                        successCount++;
                    }
                    else
                    {
                        failedCount++;
                        LogLine(string.Empty);
                        Console.ForegroundColor = result.IsWarning ? ConsoleColor.Yellow : ConsoleColor.Red;
                        Console.WriteLine($"  {(result.IsWarning ? "⚠" : "✗")} {result.ErrorMessage}");
                        Console.ResetColor();
                        log?.Report($"  {(result.IsWarning ? "⚠" : "✗")} {result.ErrorMessage}");
                    }
                }
            }

            progressIndicator.Complete();

            LogLine(string.Empty);

            // Save cache if modified
            if (_cache != null)
            {
                _cache.SaveCache();
                var (totalEntries, validEntries) = _cache.GetStatistics();
                if (totalEntries > 0)
                {
                    LogLine($"📦 Cache: {validEntries}/{totalEntries} entries");
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Generated {successCount} portraits ({failedCount} failed)");
            Console.ResetColor();
            log?.Report($"✓ Generated {successCount} portraits ({failedCount} failed)");

            return new GeneratePhotosResult
            {
                ExitCode = 0,
                TotalPlayers = totalPlayers,
                ProcessedPlayers = successCount + failedCount,
                PortraitsGenerated = successCount,
                PortraitsFailed = failedCount
            };
        }
        catch (OperationCanceledException)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠ Generation cancelled by user");
            Console.ResetColor();
            log?.Report("⚠ Generation cancelled by user");
            return new GeneratePhotosResult
            {
                ExitCode = 130,
                TotalPlayers = totalPlayers,
                ProcessedPlayers = successCount + failedCount,
                PortraitsGenerated = successCount,
                PortraitsFailed = failedCount,
                IsCancelled = true
            };
        }
        catch (FileNotFoundException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ File not found: {ex.FileName}");
            Console.ResetColor();
            log?.Report($"✗ File not found: {ex.FileName}");
            return new GeneratePhotosResult
            {
                ExitCode = 1,
                TotalPlayers = totalPlayers,
                ProcessedPlayers = successCount + failedCount,
                PortraitsGenerated = successCount,
                PortraitsFailed = failedCount
            };
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Console.ResetColor();
            log?.Report($"✗ Error: {ex.Message}");
            return new GeneratePhotosResult
            {
                ExitCode = 1,
                TotalPlayers = totalPlayers,
                ProcessedPlayers = successCount + failedCount,
                PortraitsGenerated = successCount,
                PortraitsFailed = failedCount
            };
        }
    }

    #region Private Methods

    /// <summary>
    /// Checks if a file format is supported.
    /// </summary>
    private static bool IsSupportedImageFormat(string path)
    {
        var extension = Path.GetExtension(path).ToLower();
        return extension is ".png" or ".jpg" or ".jpeg" or ".bmp";
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Result structure for processing a single player.
    /// </summary>
    private record ProcessPlayerResult(
        bool IsSuccess,
        bool IsWarning,
        string ErrorMessage
    );

    /// <summary>
    /// Processes a single player's photo.
    /// </summary>
    private async Task<ProcessPlayerResult> ProcessPlayerAsync(
        PlayerRecord player,
        string photosDir,
        string processedPhotosOutputPath,
        string format,
        int portraitWidth,
        int portraitHeight,
        bool portraitOnly,
        string faceDetectionModel,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Construct input photo path - search in photosDir
        var photoFiles = Directory.GetFiles(photosDir, $"{player.ExternalId}.*")
            .Where(f => IsSupportedImageFormat(f))
            .ToList();

        if (photoFiles.Count == 0)
        {
            // Try searching with underscore pattern (ID at end of filename)
            // Filename pattern: FirstName_LastName_PlayerID.jpg
            var pattern = $"*_{player.ExternalId}.*";
            photoFiles = Directory.GetFiles(photosDir, pattern, SearchOption.AllDirectories)
                .Where(f => IsSupportedImageFormat(f))
                .ToList();
        }

        if (photoFiles.Count == 0)
        {
            return new ProcessPlayerResult(false, true, $"No photo found for player {player.ExternalId}");
        }

        var photoPath = photoFiles[0];

        try
        {
            FaceLandmarks landmarks;

            // Step 4a: Detect faces (unless portrait-only mode)
            if (!portraitOnly)
            {
                // Check cache first
                if (_cache != null)
                {
                    var cached = _cache.GetCachedLandmarks(photoPath, faceDetectionModel);
                    if (cached != null)
                    {
                        landmarks = cached;
                        Console.WriteLine("  ✓ Using cached face detection");
                    }
                    else
                    {
                        // Cache miss - detect faces
                        Console.WriteLine($"  Detecting faces for {player.FullName} (uncached)...");
                        landmarks = await _faceDetectionService.DetectFaceLandmarksAsync(photoPath);
                        // Cache the result
                        _cache.CacheLandmarks(photoPath, landmarks, faceDetectionModel);
                    }
                }
                else
                {
                    // No cache - detect faces directly
                    Console.WriteLine($"  Detecting faces for {player.FullName}...");
                    landmarks = await _faceDetectionService.DetectFaceLandmarksAsync(photoPath);
                }
            }
            else
            {
                landmarks = new FaceLandmarks { FaceDetected = false };
            }

            // Step 5: Generate portrait
            var (imageWidth, imageHeight) = await _imageProcessor.GetImageDimensionsAsync(photoPath);
            // Load and crop image
            var image = await _imageProcessor.LoadImageAsync(photoPath);
            var cropped = await _imageProcessor.CropPortraitAsync(
                image,
                landmarks ?? new FaceLandmarks { FaceCenter = new PhotoMapperAI.Models.Point(imageWidth / 2, imageHeight / 2) },
                portraitWidth,
                portraitHeight
            );

            // Save portrait
            var outputPath = Path.Combine(processedPhotosOutputPath, $"{player.PlayerId}.{format}");
            await _imageProcessor.SaveImageAsync(cropped, outputPath, format);

            return new ProcessPlayerResult(true, false, string.Empty);
        }
        catch (Exception ex)
        {
            return new ProcessPlayerResult(false, false, $"Error: {ex.Message}");
        }
    }

    #endregion
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

    [Option(ShortName = "f", LongName = "format", Description = "Output image format (jpg or png)")]
    public string Format { get; set; } = "jpg";

    [Option(ShortName = "d", LongName = "faceDetection", Description = "Face detection model (opencv-dnn, haar-cascade, yolov8-face, llava:7b, qwen3-vl, etc.)")]
    public string FaceDetection { get; set; } = "llava:7b,qwen3-vl";

    [Option(ShortName = "c", LongName = "crop", Description = "Crop method (generic, ai)")]
    public string Crop { get; set; } = "generic";

    [Option(ShortName = "po", LongName = "portraitOnly", Description = "Skip face detection, use existing results")]
    public bool PortraitOnly { get; set; } = false;

    [Option(ShortName = "w", LongName = "faceWidth", Description = "Output portrait width (px)")]
    public int FaceWidth { get; set; } = 200;

    [Option(ShortName = "h", LongName = "faceHeight", Description = "Output portrait height (px)")]
    public int FaceHeight { get; set; } = 300;

    [Option(ShortName = "par", LongName = "parallel", Description = "Enable parallel processing")]
    public bool Parallel { get; set; } = false;

    [Option(ShortName = "pd", LongName = "parallelDegree", Description = "Degree of parallelism (default: 4)")]
    public int ParallelDegree { get; set; } = 4;

    [Option(ShortName = "nc", LongName = "noCache", Description = "Disable face detection cache")]
    public bool NoCache { get; set; } = false;

    [Option(ShortName = "cp", LongName = "cachePath", Description = "Path to face detection cache file")]
    public string? CachePath { get; set; }

    public async Task<int> OnExecuteAsync()
    {
        if (string.IsNullOrWhiteSpace(InputCsvPath) ||
            string.IsNullOrWhiteSpace(PhotosDir) ||
            string.IsNullOrWhiteSpace(ProcessedPhotosOutputPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: input CSV path, photos directory, and output directory are required.");
            Console.ResetColor();
            return 1;
        }

        // Check preflight conditions
        var preflight = await PreflightChecker.CheckGenerateAsync(FaceDetection);
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

        // Create face detection service
        var faceDetectionService = FaceDetectionServiceFactory.Create(FaceDetection);
        await faceDetectionService.InitializeAsync();

        // Create cache if enabled
        FaceDetectionCache? cache = null;
        if (!NoCache)
        {
            var cachePath = CachePath ?? Path.Combine(Directory.GetCurrentDirectory(), ".face-detection-cache.json");
            cache = new FaceDetectionCache(cachePath);
        }

        // Create image processor
        var imageProcessor = new Services.Image.ImageProcessor();

        // Create generate photos command logic handler
        var logic = new GeneratePhotosCommandLogic(faceDetectionService, imageProcessor, cache);

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
            FaceHeight,
            Parallel,
            ParallelDegree
        );
    }
}