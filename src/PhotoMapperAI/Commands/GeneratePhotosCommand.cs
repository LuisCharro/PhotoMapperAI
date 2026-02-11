using PhotoMapperAI.Models;
using PhotoMapperAI.Services;
using PhotoMapperAI.Services.AI;
using PhotoMapperAI.Services.Image;
using CsvHelper.Configuration;
using CsvHelper;

namespace PhotoMapperAI.Commands;

/// <summary>
/// GeneratePhotos command - generate portraits with face detection
/// </summary>
public class GeneratePhotosCommandLogic
{
    private readonly IFaceDetectionService _faceDetectionService;
    private readonly IImageProcessor _imageProcessor;

    /// <summary>
    /// Creates a new generate photos command logic handler.
    /// </summary>
    public GeneratePhotosCommandLogic(
        IFaceDetectionService faceDetectionService,
        IImageProcessor imageProcessor)
    {
        _faceDetectionService = faceDetectionService;
        _imageProcessor = imageProcessor;
    }

    /// <summary>
    /// Executes the generate photos command.
    /// </summary>
    public async Task<int> ExecuteAsync(
        string inputCsvPath,
        string processedPhotosOutputPath,
        string format,
        string faceDetectionModel,
        string crop,
        bool portraitOnly,
        int portraitWidth,
        int portraitHeight)
    {
        Console.WriteLine("Generate Photos Command");
        Console.WriteLine("======================");
        Console.WriteLine($"CSV File: {inputCsvPath}");
        Console.WriteLine($"Output Dir: {processedPhotosOutputPath}");
        Console.WriteLine($"Format: {format}");
        Console.WriteLine($"Face Detection: {faceDetectionModel}");
        Console.WriteLine($"Crop Method: {crop}");
        Console.WriteLine($"Portrait Only: {portraitOnly}");
        Console.WriteLine($"Portrait Size: {portraitWidth}x{portraitHeight}");
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
            var successCount = 0;
            var failedCount = 0;

            foreach (var player in players.Where(p => !string.IsNullOrEmpty(p.ExternalId)))
            {
                Console.WriteLine();
                Console.WriteLine($"Processing: {player.FullName} (ID: {player.ExternalId})");

                // Construct input photo path
                var photoFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), $"{player.ExternalId}.*")
                    .Where(f => IsSupportedImageFormat(f))
                    .ToList();

                if (photoFiles.Count == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  ⚠ No photo found for player {player.ExternalId}");
                    Console.ResetColor();
                    failedCount++;
                    continue;
                }

                var photoPath = photoFiles[0];
                Console.WriteLine($"  Found: {Path.GetFileName(photoPath)}");

                try
                {
                    FaceLandmarks? landmarks = null;

                    // Step 4a: Detect faces (unless portrait-only mode)
                    if (!portraitOnly)
                    {
                        Console.WriteLine($"  Detecting faces using: {faceDetectionModel}");

                        // Select face detection service
                        var service = CreateFaceDetectionService(faceDetectionModel);

                        // Initialize service if needed
                        if (service is OpenCVDNNFaceDetectionService cvService)
                        {
                            await cvService.InitializeAsync();
                        }

                        landmarks = await service.DetectFaceLandmarksAsync(photoPath);

                        if (landmarks.FaceDetected)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"  ✓ Face detected (confidence: {landmarks.FaceConfidence:P1})");
                            Console.ResetColor();

                            if (landmarks.BothEyesDetected)
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"  ✓ Both eyes detected");
                                Console.ResetColor();
                            }
                            else if (landmarks.LeftEye != null || landmarks.RightEye != null)
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"  ⚠ Only one eye detected");
                                Console.ResetColor();
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"  ⚠ No eyes detected, using face center");
                                Console.ResetColor();
                            }
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"  ⚠ No face detected, using center crop");
                            Console.ResetColor();
                        }

                        Console.WriteLine($"  Processing time: {landmarks.ProcessingTimeMs}ms");
                    }
                    else
                    {
                        Console.WriteLine("  Skipping face detection (portrait-only mode)");
                    }

                    // Step 4b: Determine crop strategy
                    PortraitCropStrategy strategy;
                    if (portraitOnly)
                    {
                        strategy = PortraitCropStrategy.UseExistingLandmarks;
                        Console.WriteLine("  Using existing landmarks from CSV");
                    }
                    else if (crop == "ai")
                    {
                        strategy = PortraitCropStrategy.AiBased;
                        Console.WriteLine("  Using AI-based cropping");
                    }
                    else
                    {
                        strategy = PortraitCropStrategy.Generic;
                        Console.WriteLine("  Using generic cropping");
                    }

                    // Step 5: Generate portrait
                    Console.WriteLine($"  Generating portrait ({strategy})...");

                    var (imageWidth, imageHeight) = await _imageProcessor.GetImageDimensionsAsync(photoPath);

                    // Determine actual crop dimensions
                    int actualCropWidth, actualCropHeight;

                    if (strategy == PortraitCropStrategy.AiBased && landmarks == null)
                    {
                        // Fallback to generic if AI failed
                        strategy = PortraitCropStrategy.Generic;
                    }

                    if (strategy == PortraitCropStrategy.Generic || landmarks == null)
                    {
                        // Generic center crop
                        var cropRect = CalculateGenericCrop(imageWidth, imageHeight, portraitWidth, portraitHeight);
                        Console.WriteLine($"  Crop: {cropRect.X},{cropRect.Y} {cropRect.Width}x{cropRect.Height}");
                        actualCropWidth = portraitWidth;
                        actualCropHeight = portraitHeight;
                    }
                    else
                    {
                        // Use face landmarks
                        Console.WriteLine($"  Face center: {landmarks.FaceCenter}");
                        actualCropWidth = portraitWidth;
                        actualCropHeight = portraitHeight;
                    }

                    // Load and crop image
                    var image = await _imageProcessor.LoadImageAsync(photoPath);
                    var cropped = await _imageProcessor.CropPortraitAsync(
                        image,
                        landmarks ?? new FaceLandmarks { FaceCenter = new PhotoMapperAI.Models.Point(imageWidth / 2, imageHeight / 2) },
                        actualCropWidth,
                        actualCropHeight
                    );

                    // Save portrait
                    var outputPath = Path.Combine(processedPhotosOutputPath, $"{player.PlayerId}.{format}");
                    await _imageProcessor.SaveImageAsync(cropped, outputPath, format);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"  ✓ Saved: {Path.GetFileName(outputPath)}");
                    Console.ResetColor();

                    successCount++;
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  ✗ Error: {ex.Message}");
                    Console.ResetColor();
                    failedCount++;
                }
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Generated {successCount} portraits ({failedCount} failed)");
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
            Console.WriteLine(ex.StackTrace);
            Console.ResetColor();
            return 1;
        }
    }

    #region Private Methods

    /// <summary>
    /// Creates the appropriate face detection service based on model name.
    /// </summary>
    private IFaceDetectionService CreateFaceDetectionService(string model)
    {
        return model.ToLower() switch
        {
            "opencv-dnn" => new OpenCVDNNFaceDetectionService(),
            "yolov8-face" => new OpenCVDNNFaceDetectionService(),
            var llavaModel when model.Contains("llava") || model.Contains("qwen3-vl") => new OllamaFaceDetectionService(modelName: model),
            _ => throw new ArgumentException($"Unknown face detection model: {model}")
        };
    }

    /// <summary>
    /// Checks if a file format is supported.
    /// </summary>
    private static bool IsSupportedImageFormat(string path)
    {
        var extension = Path.GetExtension(path).ToLower();
        return extension is ".png" or ".jpg" or ".jpeg" or ".bmp";
    }

    /// <summary>
    /// Calculates a generic center crop rectangle.
    /// </summary>
    private static PhotoMapperAI.Models.Rectangle CalculateGenericCrop(
        int imageWidth,
        int imageHeight,
        int targetWidth,
        int targetHeight)
    {
        var cropWidth = (int)(targetWidth * 2.0);
        var cropHeight = (int)(targetHeight * 2.0);

        return new PhotoMapperAI.Models.Rectangle(
            (imageWidth - cropWidth) / 2,
            (imageHeight - cropHeight) / 2,
            cropWidth,
            cropHeight
        );
    }

    #endregion

    #region Enums

    /// <summary>
    /// Portrait cropping strategy.
    /// </summary>
    private enum PortraitCropStrategy
    {
        /// <summary>
        /// Use existing face landmarks from CSV/previous run.
        /// </summary>
        UseExistingLandmarks,

        /// <summary>
        /// Use AI-based detection (face detection service).
        /// </summary>
        AiBased,

        /// <summary>
        /// Use generic center crop (no face detection).
        /// </summary>
        Generic
    }

    #endregion
}
