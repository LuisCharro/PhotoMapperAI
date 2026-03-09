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
/// Result of a generate photos operation.
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

public sealed record GeneratePhotosVariantPlan(
    string Key,
    int Width,
    int Height,
    string OutputDir,
    string? PlaceholderPath
);

/// <summary>
/// Business logic for generating portrait photos with face detection.
/// </summary>
public class GeneratePhotosCommandLogic
{
    private readonly IFaceDetectionService _faceDetectionService;
    private readonly IImageProcessor _imageProcessor;
    private readonly FaceDetectionCache? _cache;
    private readonly CropOffsetPreset? _cropOffsetPreset;
    private readonly bool _faceDetectionTrace;
    private string? _placeholderImagePath;

    /// <summary>
    /// Creates a new generate photos command logic handler.
    /// </summary>
    public GeneratePhotosCommandLogic(
        IFaceDetectionService faceDetectionService,
        IImageProcessor imageProcessor,
        FaceDetectionCache? cache = null,
        CropOffsetPreset? cropOffsetPreset = null,
        bool faceDetectionTrace = false)
    {
        _faceDetectionService = faceDetectionService;
        _imageProcessor = imageProcessor;
        _cache = cache;
        _cropOffsetPreset = cropOffsetPreset;
        _faceDetectionTrace = faceDetectionTrace;
    }

    /// <summary>
    /// Sets the placeholder image path for players without source photos.
    /// </summary>
    public void SetPlaceholderImagePath(string? placeholderPath)
    {
        _placeholderImagePath = placeholderPath;
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
        int parallelDegree,
        string? onlyPlayerId = null,
        string? placeholderImagePath = null)
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
            parallelDegree,
            onlyPlayerId,
            placeholderImagePath: placeholderImagePath
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
        string? onlyPlayerId = null,
        string? placeholderImagePath = null,
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
        LogLine($"Face Detection Trace: {_faceDetectionTrace}");
        LogLine($"Crop Method: {crop}");
        LogLine($"Portrait Only: {portraitOnly}");
        LogLine($"Portrait Size: {portraitWidth}x{portraitHeight}");
        LogLine($"Parallel: {parallel} (Degree: {parallelDegree})");
        if (!string.IsNullOrWhiteSpace(onlyPlayerId))
        {
            LogLine($"Filter: Only processing player ID: {onlyPlayerId}");
        }
        if (!string.IsNullOrWhiteSpace(placeholderImagePath))
        {
            LogLine($"Placeholder: {placeholderImagePath}");
            _placeholderImagePath = placeholderImagePath;
        }
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
            var playersToProcess = players.Where(p => !string.IsNullOrEmpty(p.External_Player_ID)).ToList();
            
            // Filter by specific player ID if provided
            if (!string.IsNullOrWhiteSpace(onlyPlayerId))
            {
                playersToProcess = playersToProcess
                    .Where(p => string.Equals(p.PlayerId.ToString(), onlyPlayerId, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(p.External_Player_ID, onlyPlayerId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                
                if (playersToProcess.Count == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"⚠ No player found with ID: {onlyPlayerId}");
                    Console.ResetColor();
                    log?.Report($"⚠ No player found with ID: {onlyPlayerId}");
                    return new GeneratePhotosResult
                    {
                        ExitCode = 0,
                        TotalPlayers = 0
                    };
                }
                
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"🔍 Filtering to {playersToProcess.Count} player(s) with ID: {onlyPlayerId}");
                Console.ResetColor();
                log?.Report($"🔍 Filtering to {playersToProcess.Count} player(s) with ID: {onlyPlayerId}");
            }
            
            totalPlayers = playersToProcess.Count;

            if (totalPlayers == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠ No players with External_Player_ID found to process");
                Console.ResetColor();
                log?.Report("⚠ No players with External_Player_ID found to process");
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
                    progressIndicator.Update($"{player.FullName} (ID: {player.External_Player_ID})");

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
                    progressIndicator.Update($"{player.FullName} (ID: {player.External_Player_ID})");

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

    /// <summary>
    /// Result structure for processing a single player.
    /// </summary>
    private record ProcessPlayerResult(
        bool IsSuccess,
        bool IsWarning,
        string ErrorMessage
    );


    private static List<string> FindPlayerPhotoFiles(string photosDir, string? External_Player_ID)
    {
        if (string.IsNullOrWhiteSpace(External_Player_ID))
        {
            return new List<string>();
        }

        var safeExternal_Player_ID = External_Player_ID.Trim();
        var photoFiles = Directory.GetFiles(photosDir, $"{safeExternal_Player_ID}.*")
            .Where(f => IsSupportedImageFormat(f))
            .ToList();

        if (photoFiles.Count == 0)
        {
            // Try searching with underscore pattern (ID at end of filename)
            // Filename pattern: FirstName_LastName_PlayerID.jpg
            var pattern = $"*_{safeExternal_Player_ID}.*";
            photoFiles = Directory.GetFiles(photosDir, pattern, SearchOption.AllDirectories)
                .Where(f => IsSupportedImageFormat(f))
                .ToList();
        }

        return photoFiles;
    }

    public async Task<GeneratePhotosResult> ExecuteMultiVariantAsync(
        string inputCsvPath,
        string photosDir,
        IReadOnlyList<GeneratePhotosVariantPlan> variants,
        string format,
        string faceDetectionModel,
        string crop,
        bool portraitOnly,
        bool parallel,
        int parallelDegree,
        string? onlyPlayerId = null,
        IProgress<(int processed, int total, string current)>? progress = null,
        CancellationToken cancellationToken = default,
        IProgress<string>? log = null)
    {
        if (variants == null || variants.Count == 0)
        {
            throw new ArgumentException("At least one variant is required.", nameof(variants));
        }

        var baseVariant = variants
            .OrderByDescending(v => v.Width * v.Height)
            .First();

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
        LogLine($"Format: {format}");
        LogLine($"Face Detection: {faceDetectionModel}");
        LogLine($"Crop Method: {crop}");
        LogLine($"Portrait Only: {portraitOnly}");
        LogLine($"Parallel: {parallel} (Degree: {parallelDegree})");
        LogLine($"Variants: {string.Join(", ", variants.Select(v => $"{v.Key}:{v.Width}x{v.Height}"))}");
        LogLine(string.Empty);

        try
        {
            LogLine("Loading player data...");
            var extractor = new Services.Database.DatabaseExtractor(_imageProcessor);
            var players = await extractor.ReadCsvAsync(inputCsvPath);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Loaded {players.Count} players from CSV");
            Console.ResetColor();
            log?.Report($"✓ Loaded {players.Count} players from CSV");
            LogLine(string.Empty);

            foreach (var variant in variants)
            {
                Directory.CreateDirectory(variant.OutputDir);
            }

            var playersToProcess = players.Where(p => !string.IsNullOrEmpty(p.External_Player_ID)).ToList();

            if (!string.IsNullOrWhiteSpace(onlyPlayerId))
            {
                playersToProcess = playersToProcess
                    .Where(p => string.Equals(p.PlayerId.ToString(), onlyPlayerId, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(p.External_Player_ID, onlyPlayerId, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (playersToProcess.Count == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"⚠ No player found with ID: {onlyPlayerId}");
                    Console.ResetColor();
                    log?.Report($"⚠ No player found with ID: {onlyPlayerId}");
                    return new GeneratePhotosResult
                    {
                        ExitCode = 0,
                        TotalPlayers = 0
                    };
                }

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"🔍 Filtering to {playersToProcess.Count} player(s) with ID: {onlyPlayerId}");
                Console.ResetColor();
                log?.Report($"🔍 Filtering to {playersToProcess.Count} player(s) with ID: {onlyPlayerId}");
            }

            totalPlayers = playersToProcess.Count;

            if (totalPlayers == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠ No players with External_Player_ID found to process");
                Console.ResetColor();
                log?.Report("⚠ No players with External_Player_ID found to process");
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
                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = parallelDegree,
                    CancellationToken = cancellationToken
                };

                await Parallel.ForEachAsync(playersToProcess, options, async (player, cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progressIndicator.Update($"{player.FullName} (ID: {player.External_Player_ID})");

                    var result = await ProcessPlayerMultiVariantAsync(
                        player,
                        photosDir,
                        variants,
                        baseVariant,
                        format,
                        portraitOnly,
                        faceDetectionModel,
                        cancellationToken);

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
                foreach (var player in playersToProcess)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progressIndicator.Update($"{player.FullName} (ID: {player.External_Player_ID})");

                    var result = await ProcessPlayerMultiVariantAsync(
                        player,
                        photosDir,
                        variants,
                        baseVariant,
                        format,
                        portraitOnly,
                        faceDetectionModel,
                        cancellationToken);

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

        string? External_Player_ID = player.External_Player_ID;
        if (string.IsNullOrEmpty(External_Player_ID))
        {
            return new ProcessPlayerResult(false, true, "Player has no External_Player_ID");
        }

        var photoFiles = FindPlayerPhotoFiles(photosDir, External_Player_ID ?? string.Empty);

        if (photoFiles.Count == 0)
        {
            // Check if we have a placeholder image to use
            // Placeholder should already be sized to match the target dimensions
            if (!string.IsNullOrEmpty(_placeholderImagePath) && File.Exists(_placeholderImagePath))
            {
                try
                {
                    Console.WriteLine($"  Using placeholder for {player.FullName}...");
                    
                    // Load placeholder (should already be correctly sized) and save
                    using var placeholder = await _imageProcessor.LoadImageAsync(_placeholderImagePath);
                    
                    // Save portrait - just convert format if needed
                    var outputPath = Path.Combine(processedPhotosOutputPath, $"{player.PlayerId}.{format}");
                    await _imageProcessor.SaveImageAsync(placeholder, outputPath, format);
                    
                    return new ProcessPlayerResult(true, false, string.Empty);
                }
                catch (Exception ex)
                {
                    return new ProcessPlayerResult(false, false, $"Error using placeholder: {ex.Message}");
                }
            }
            
            return new ProcessPlayerResult(false, true, $"No photo found for player {player.External_Player_ID}");
        }

        var photoPath = photoFiles[0];

        try
        {
            FaceLandmarks landmarks;
            var usedCache = false;

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
                        usedCache = true;
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

            if (_faceDetectionTrace)
            {
                LogFaceDetectionTrace(player, photoPath, faceDetectionModel, landmarks, usedCache);
            }

            // Step 5: Generate portrait
            var (imageWidth, imageHeight) = await _imageProcessor.GetImageDimensionsAsync(photoPath);

            // Load/crop/save with deterministic disposal to avoid memory pressure in large runs.
            using var image = await _imageProcessor.LoadImageAsync(photoPath);
            using var cropped = await _imageProcessor.CropPortraitAsync(
                image,
                landmarks ?? new FaceLandmarks { FaceCenter = new PhotoMapperAI.Models.Point(imageWidth / 2, imageHeight / 2) },
                portraitWidth,
                portraitHeight,
                _cropOffsetPreset
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

    private async Task<ProcessPlayerResult> ProcessPlayerMultiVariantAsync(
        PlayerRecord player,
        string photosDir,
        IReadOnlyList<GeneratePhotosVariantPlan> variants,
        GeneratePhotosVariantPlan baseVariant,
        string format,
        bool portraitOnly,
        string faceDetectionModel,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string? External_Player_ID = player.External_Player_ID;
        if (string.IsNullOrEmpty(External_Player_ID))
        {
            return new ProcessPlayerResult(false, true, "Player has no External_Player_ID");
        }

        var photoFiles = FindPlayerPhotoFiles(photosDir, External_Player_ID ?? string.Empty);

        if (photoFiles.Count == 0)
        {
            var missingVariants = new List<string>();
            foreach (var variant in variants)
            {
                if (string.IsNullOrWhiteSpace(variant.PlaceholderPath) || !File.Exists(variant.PlaceholderPath))
                {
                    missingVariants.Add(variant.Key);
                    continue;
                }

                try
                {
                    using var placeholder = await _imageProcessor.LoadImageAsync(variant.PlaceholderPath);
                    var outputPath = Path.Combine(variant.OutputDir, $"{player.PlayerId}.{format}");
                    await _imageProcessor.SaveImageAsync(placeholder, outputPath, format);
                }
                catch (Exception ex)
                {
                    return new ProcessPlayerResult(false, false, $"Error using placeholder: {ex.Message}");
                }
            }

            if (missingVariants.Count > 0)
            {
                return new ProcessPlayerResult(false, true,
                    $"No photo found for player {player.External_Player_ID} (missing placeholders for: {string.Join(", ", missingVariants)})");
            }

            return new ProcessPlayerResult(true, false, string.Empty);
        }

        var photoPath = photoFiles[0];

        try
        {
            FaceLandmarks landmarks;
            var usedCache = false;

            if (!portraitOnly)
            {
                if (_cache != null)
                {
                    var cached = _cache.GetCachedLandmarks(photoPath, faceDetectionModel);
                    if (cached != null)
                    {
                        landmarks = cached;
                        Console.WriteLine("  ✓ Using cached face detection");
                        usedCache = true;
                    }
                    else
                    {
                        Console.WriteLine($"  Detecting faces for {player.FullName} (uncached)...");
                        landmarks = await _faceDetectionService.DetectFaceLandmarksAsync(photoPath);
                        _cache.CacheLandmarks(photoPath, landmarks, faceDetectionModel);
                    }
                }
                else
                {
                    Console.WriteLine($"  Detecting faces for {player.FullName}...");
                    landmarks = await _faceDetectionService.DetectFaceLandmarksAsync(photoPath);
                }
            }
            else
            {
                landmarks = new FaceLandmarks { FaceDetected = false };
            }

            if (_faceDetectionTrace)
            {
                LogFaceDetectionTrace(player, photoPath, faceDetectionModel, landmarks, usedCache);
            }

            var (imageWidth, imageHeight) = await _imageProcessor.GetImageDimensionsAsync(photoPath);

            using var image = await _imageProcessor.LoadImageAsync(photoPath);
            using var basePortrait = await _imageProcessor.CropPortraitAsync(
                image,
                landmarks ?? new FaceLandmarks { FaceCenter = new PhotoMapperAI.Models.Point(imageWidth / 2, imageHeight / 2) },
                baseVariant.Width,
                baseVariant.Height,
                _cropOffsetPreset);

            var baseOutputPath = Path.Combine(baseVariant.OutputDir, $"{player.PlayerId}.{format}");
            await _imageProcessor.SaveImageAsync(basePortrait, baseOutputPath, format);

            foreach (var variant in variants)
            {
                if (variant.Width == baseVariant.Width && variant.Height == baseVariant.Height)
                {
                    continue;
                }

                using var resized = await _imageProcessor.ResizeAsync(basePortrait, variant.Width, variant.Height);
                var outputPath = Path.Combine(variant.OutputDir, $"{player.PlayerId}.{format}");
                await _imageProcessor.SaveImageAsync(resized, outputPath, format);
            }

            return new ProcessPlayerResult(true, false, string.Empty);
        }
        catch (Exception ex)
        {
            return new ProcessPlayerResult(false, false, $"Error: {ex.Message}");
        }
    }

    private static void LogFaceDetectionTrace(
        PlayerRecord player,
        string photoPath,
        string faceDetectionModel,
        FaceLandmarks landmarks,
        bool usedCache)
    {
        var fileName = Path.GetFileName(photoPath);
        var eyeMid = landmarks.EyeMidpoint;
        var faceRect = landmarks.FaceRect;
        var faceRectText = faceRect == null
            ? "none"
            : $"{faceRect.X},{faceRect.Y},{faceRect.Width},{faceRect.Height}";
        var leftEyeText = landmarks.LeftEye == null
            ? "none"
            : $"{landmarks.LeftEye.X},{landmarks.LeftEye.Y}";
        var rightEyeText = landmarks.RightEye == null
            ? "none"
            : $"{landmarks.RightEye.X},{landmarks.RightEye.Y}";
        var eyeMidText = eyeMid == null
            ? "none"
            : $"{eyeMid.X},{eyeMid.Y}";

        Console.WriteLine(
            $"[FaceTrace] Model={faceDetectionModel} UsedModel={landmarks.ModelUsed} Cached={usedCache} " +
            $"Player={player.PlayerId}/{player.External_Player_ID} File={fileName} " +
            $"FaceDetected={landmarks.FaceDetected} BothEyes={landmarks.BothEyesDetected} " +
            $"FaceRect={faceRectText} LeftEye={leftEyeText} RightEye={rightEyeText} EyeMid={eyeMidText}");

        if (!landmarks.FaceDetected)
        {
            Console.WriteLine("[FaceTrace] Warning: no face detected.");
            return;
        }

        if (!landmarks.BothEyesDetected)
        {
            Console.WriteLine("[FaceTrace] Warning: both eyes not detected.");
        }
    }

    #endregion
}

/// <summary>
/// CLI command for generating portrait photos with face detection.
/// </summary>
[Command("generatephotos", Description = "Generate portraits with face detection", ExtendedHelpText = @"
Generates portrait photos from full-body images using face and eye detection.
Supports OpenCV DNN, Haar Cascades, YOLOv8-Face, and Ollama Vision models.

Fallback mode: Provide comma-separated models to try each in order.
Example: llava:7b will try llava:7b first, fall back to qwen3-vl if it fails.

Examples:
  photomapperai generatephotos -inputCsvPath players.csv -processedPhotosOutputPath ./portraits -format jpg
  photomapperai generatephotos -inputCsvPath players.csv -processedPhotosOutputPath ./portraits -format jpg -faceDetection opencv-dnn
  photomapperai generatephotos -inputCsvPath players.csv -processedPhotosOutputPath ./portraits -format jpg -faceDetection qwen3-vl
  photomapperai generatephotos -inputCsvPath players.csv -processedPhotosOutputPath ./portraits -format jpg -faceDetection llava:7b
  photomapperai generatephotos -inputCsvPath players.csv -processedPhotosOutputPath ./portraits -format jpg -portraitOnly
  photomapperai generatephotos -inputCsvPath players.csv -processedPhotosOutputPath ./portraits -format jpg --onlyPlayer 12345
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

    [Option(ShortName = "d", LongName = "faceDetection", Description = "Face detection model: opencv-yunet, opencv-dnn, yolov8-face, llava:7b, qwen3-vl, or comma-separated fallback list (default: llava:7b)")]
    public string FaceDetection { get; set; } = "llava:7b";

    [Option(ShortName = "c", LongName = "crop", Description = "Crop method: generic, ai (default: generic)")]
    public string Crop { get; set; } = "generic";

    [Option(ShortName = "po", LongName = "portraitOnly", Description = "Skip face detection, use existing results")]
    public bool PortraitOnly { get; set; } = false;

    [Option(ShortName = "fw", LongName = "faceWidth", Description = "Portrait width in pixels (default: 200)")]
    public int FaceWidth { get; set; } = 200;

    [Option(ShortName = "fh", LongName = "faceHeight", Description = "Portrait height in pixels (default: 300)")]
    public int FaceHeight { get; set; } = 300;

    [Option(ShortName = "sp", LongName = "sizeProfile", Description = "Path to a size profile JSON.")]
    public string? SizeProfile { get; set; }

    [Option(ShortName = "as", LongName = "allSizes", Description = "When used with --sizeProfile, generate all variants into subfolders.")]
    public bool AllSizes { get; set; } = false;

    [Option(ShortName = "op", LongName = "outputProfile", Description = "Optional output profile alias: test|prod")]
    public string? OutputProfile { get; set; }

    [Option(ShortName = "par", LongName = "parallel", Description = "Enable parallel processing (default: false)")]
    public bool Parallel { get; set; } = false;

    [Option(ShortName = "pd", LongName = "parallelDegree", Description = "Max parallel tasks (default: 4)")]
    public int ParallelDegree { get; set; } = 4;

    [Option(ShortName = "cache", LongName = "cachePath", Description = "Path to face detection cache file (default: .face-detection-cache.json)")]
    public string? CachePath { get; set; }

    [Option(ShortName = "nc", LongName = "noCache", Description = "Disable face detection caching")]
    public bool NoCache { get; set; } = false;

    [Option(ShortName = "dl", LongName = "downloadOpenCvModels", Description = "Download missing OpenCV DNN model files if needed")]
    public bool DownloadOpenCvModels { get; set; } = false;

    [Option(ShortName = "fdtr", LongName = "faceDetectionTrace", Description = "Log detailed face detection output per image")]
    public bool FaceDetectionTrace { get; set; } = false;

    [Option(ShortName = "opl", LongName = "onlyPlayer", Description = "Process only the specified player ID (internal PlayerId or External_Player_ID)")]
    public string? OnlyPlayer { get; set; }

    [Option(ShortName = "ph", LongName = "placeholderImage", Description = "Path to a placeholder image to use when no source photo is available")]
    public string? PlaceholderImage { get; set; }

    [Option(ShortName = "npp", LongName = "noProfilePlaceholders", Description = "Ignore placeholder paths defined in size profile variants")]
    public bool NoProfilePlaceholders { get; set; } = false;

    [Option(ShortName = "ox", LongName = "cropOffsetX", Description = "Horizontal crop offset percentage override.")]
    public double? CropOffsetX { get; set; }

    [Option(ShortName = "oy", LongName = "cropOffsetY", Description = "Vertical crop offset percentage override.")]
    public double? CropOffsetY { get; set; }

    public async Task<int> OnExecuteAsync()
    {
        PhotoMapperAI.Models.SizeProfile? loadedProfile = null;

        if (!string.IsNullOrWhiteSpace(SizeProfile))
        {
            try
            {
                loadedProfile = SizeProfileLoader.LoadFromFile(SizeProfile);
                Console.WriteLine($"Using size profile '{loadedProfile.Name}' from {SizeProfile}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Invalid --sizeProfile: {ex.Message}");
                Console.ResetColor();
                return 1;
            }
        }

        var preflight = await PreflightChecker.CheckGenerateAsync(FaceDetection, DownloadOpenCvModels);
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

        // Initialize face detection service (loads models, checks availability)
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
        var cropOffsetPreset = CropOffsetX.HasValue || CropOffsetY.HasValue
            ? new CropOffsetPreset
            {
                Name = "cli",
                HorizontalPercent = CropOffsetX ?? 0,
                VerticalPercent = CropOffsetY ?? 0
            }
            : CropOffsetSettingsLoader.LoadActivePreset();
        var logic = new GeneratePhotosCommandLogic(faceDetectionService, imageProcessor, cache, cropOffsetPreset, FaceDetectionTrace);

        var baseOutputPath = ProcessedPhotosOutputPath;
        if (!string.IsNullOrWhiteSpace(OutputProfile))
        {
            try
            {
                baseOutputPath = OutputProfileResolver.Resolve(OutputProfile, ProcessedPhotosOutputPath);
                Console.WriteLine($"Using output profile '{OutputProfile}' => {baseOutputPath}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Invalid --outputProfile: {ex.Message}");
                Console.ResetColor();
                return 1;
            }
        }

        // For single-size mode (no size profile), use CLI placeholder option
        // For size profile mode, each variant may have its own placeholder
        async Task<int> RunOneVariant(int width, int height, string outputDir, string variantLabel, string? variantPlaceholderPath = null)
        {
            // CLI placeholder option takes precedence over variant placeholder
            var placeholderForVariant = PlaceholderImage ?? variantPlaceholderPath;
            
            // Set placeholder path in logic for this variant
            logic.SetPlaceholderImagePath(placeholderForVariant);
            
            Console.WriteLine($"Generating variant '{variantLabel}' => {width}x{height} -> {outputDir}");
            if (!string.IsNullOrEmpty(placeholderForVariant))
            {
                Console.WriteLine($"  Using placeholder: {placeholderForVariant}");
            }
            Directory.CreateDirectory(outputDir);

            return await logic.ExecuteAsync(
                InputCsvPath,
                PhotosDir,
                outputDir,
                Format,
                FaceDetection,
                Crop,
                PortraitOnly,
                width,
                height,
                Parallel,
                ParallelDegree,
                OnlyPlayer,
                placeholderForVariant
            );
        }

        if (loadedProfile == null)
        {
            return await RunOneVariant(FaceWidth, FaceHeight, baseOutputPath, "single");
        }

        if (!AllSizes)
        {
            var firstVariant = loadedProfile.Variants.FirstOrDefault(v =>
                                  string.Equals(v.Key, "x200x300", StringComparison.OrdinalIgnoreCase)
                                  || (v.Width == 200 && v.Height == 300))
                              ?? loadedProfile.Variants.First();

            var placeholderPath = NoProfilePlaceholders ? null : firstVariant.PlaceholderPath;
            return await RunOneVariant(firstVariant.Width, firstVariant.Height, baseOutputPath, firstVariant.Key, placeholderPath);
        }

        var variantPlans = loadedProfile.Variants.Select(variant =>
        {
            var subfolder = string.IsNullOrWhiteSpace(variant.OutputSubfolder) ? variant.Key : variant.OutputSubfolder;
            var variantOutput = Path.Combine(baseOutputPath, subfolder);
            var placeholderPath = NoProfilePlaceholders ? null : variant.PlaceholderPath;
            var resolvedPlaceholder = PlaceholderImage ?? placeholderPath;
            return new GeneratePhotosVariantPlan(variant.Key, variant.Width, variant.Height, variantOutput, resolvedPlaceholder);
        }).ToList();

        var multiResult = await logic.ExecuteMultiVariantAsync(
            InputCsvPath,
            PhotosDir,
            variantPlans,
            Format,
            FaceDetection,
            Crop,
            PortraitOnly,
            Parallel,
            ParallelDegree,
            OnlyPlayer);

        return multiResult.ExitCode;
    }
}
