using PhotoMapperAI.Models;
using PhotoMapperAI.Services;
using PhotoMapperAI.Services.AI;
using PhotoMapperAI.Services.Image;
using PhotoMapperAI.Utils;
using CsvHelper.Configuration;
using CsvHelper;

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
        CancellationToken cancellationToken = default)
    {
        var totalPlayers = 0;
        var successCount = 0;
        var failedCount = 0;
        var processedCount = 0;

        Console.WriteLine("Generate Photos Command");
        Console.WriteLine("======================");
        Console.WriteLine($"CSV File: {inputCsvPath}");
        Console.WriteLine($"Photos Dir: {photosDir}");
        Console.WriteLine($"Output Dir: {processedPhotosOutputPath}");
        Console.WriteLine($"Format: {format}");
        Console.WriteLine($"Face Detection: {faceDetectionModel}");
        Console.WriteLine($"Crop Method: {crop}");
        Console.WriteLine($"Portrait Only: {portraitOnly}");
        Console.WriteLine($"Portrait Size: {portraitWidth}x{portraitHeight}");
        Console.WriteLine($"Parallel: {parallel} (Degree: {parallelDegree})");
        Console.WriteLine();

        try
        {
            // Step 1: Load players from CSV
            Console.WriteLine("Loading player data...");
            var extractor = new Services.Database.DatabaseExtractor(_imageProcessor);
            var players = await extractor.ReadCsvAsync(inputCsvPath);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Loaded {players.Count} players from CSV");
            Console.ResetColor();
            Console.WriteLine();

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
                return new GeneratePhotosResult
                {
                    ExitCode = 0,
                    TotalPlayers = 0
                };
            }

            Console.WriteLine($"Processing {totalPlayers} players...");
            Console.WriteLine();

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
                        Console.WriteLine();
                        Console.ForegroundColor = result.IsWarning ? ConsoleColor.Yellow : ConsoleColor.Red;
                        Console.WriteLine($"  {(result.IsWarning ? "⚠" : "✗")} {result.ErrorMessage}");
                        Console.ResetColor();
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
                        Console.WriteLine();
                        Console.ForegroundColor = result.IsWarning ? ConsoleColor.Yellow : ConsoleColor.Red;
                        Console.WriteLine($"  {(result.IsWarning ? "⚠" : "✗")} {result.ErrorMessage}");
                        Console.ResetColor();
                    }
                }
            }

            progressIndicator.Complete();

            Console.WriteLine();

            // Save cache if modified
            if (_cache != null)
            {
                _cache.SaveCache();
                var (totalEntries, validEntries) = _cache.GetStatistics();
                if (totalEntries > 0)
                {
                    Console.WriteLine($"📦 Cache: {validEntries}/{totalEntries} entries");
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Generated {successCount} portraits ({failedCount} failed)");
            Console.ResetColor();

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
