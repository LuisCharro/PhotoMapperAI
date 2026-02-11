using PhotoMapperAI.Models;
using PhotoMapperAI.Services;
using PhotoMapperAI.Services.AI;
using PhotoMapperAI.Utils;

namespace PhotoMapperAI.Commands;

/// <summary>
/// Map command - match photos to players using AI
/// </summary>
public class MapCommandLogic
{
    private readonly INameMatchingService _nameMatchingService;
    private readonly IImageProcessor _imageProcessor;

    /// <summary>
    /// Creates a new map command logic handler.
    /// </summary>
    public MapCommandLogic(
        INameMatchingService nameMatchingService,
        IImageProcessor imageProcessor)
    {
        _nameMatchingService = nameMatchingService;
        _imageProcessor = imageProcessor;
    }

    /// <summary>
    /// Executes the map command.
    /// </summary>
    public async Task<int> ExecuteAsync(
        string inputCsvPath,
        string photosDir,
        string? filenamePattern,
        string? photoManifest,
        string nameModel,
        double confidenceThreshold)
    {
        Console.WriteLine("Map Command");
        Console.WriteLine("============");
        Console.WriteLine($"CSV File: {inputCsvPath}");
        Console.WriteLine($"Photos Dir: {photosDir}");
        Console.WriteLine($"Name Model: {nameModel}");
        Console.WriteLine($"Confidence Threshold: {confidenceThreshold}");
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

            // Step 2: Load photos
            Console.WriteLine("Loading photos...");
            var photoFiles = Directory.GetFiles(photosDir, "*.*", SearchOption.AllDirectories);
            var photos = photoFiles.Select(f => Path.GetFileName(f)).ToList();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Loaded {photos.Count} photos");
            Console.ResetColor();
            Console.WriteLine();

            // Step 3: Load photo manifest if provided
            Dictionary<string, PhotoMetadata>? manifest = null;
            if (!string.IsNullOrEmpty(photoManifest))
            {
                Console.WriteLine($"Loading photo manifest: {photoManifest}");
                manifest = FilenameParser.LoadManifest(photoManifest);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Loaded {manifest.Count} entries from manifest");
                Console.ResetColor();
                Console.WriteLine();
            }

            // Step 4: Match photos to players
            Console.WriteLine("Matching photos to players...");
            var results = new List<MappingResult>();

            foreach (var photo in photos)
            {
                var result = await MatchPhotoToPlayerAsync(
                    photo,
                    players,
                    photosDir,
                    filenamePattern,
                    manifest,
                    nameModel,
                    confidenceThreshold
                );

                results.Add(result);

                // Update player if matched
                if (result.IsValidMatch)
                {
                    var player = players.FirstOrDefault(p => p.PlayerId == result.PlayerId);
                    if (player != null)
                    {
                        player.ExternalId = result.ExternalId;
                        player.ValidMapping = true;
                        player.Confidence = result.Confidence;
                    }
                }

                // Show progress
                Console.WriteLine(
                    $"  {Path.GetFileName(photo)} -> " +
                    (result.IsValidMatch ? $"Player {result.PlayerId} ({result.Confidence:P1})" : "No match")
                );
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Matched {results.Count(r => r.IsValidMatch)} / {results.Count} photos");
            Console.ResetColor();

            // Step 5: Write updated CSV
            var outputPath = Path.Combine(Directory.GetCurrentDirectory(), $"mapped_{Path.GetFileName(inputCsvPath)}");
            Console.WriteLine();
            Console.WriteLine($"Writing results to: {outputPath}");
            await Services.Database.DatabaseExtractor.WriteCsvAsync(players, outputPath);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Complete! Results saved to {outputPath}");
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
        catch (DirectoryNotFoundException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Directory not found: {ex.Message}");
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
    /// Matches a single photo to a player.
    /// </summary>
    private async Task<MappingResult> MatchPhotoToPlayerAsync(
        string photoPath,
        List<PlayerRecord> players,
        string photosDir,
        string? filenamePattern,
        Dictionary<string, PhotoMetadata>? manifest,
        string nameModel,
        double confidenceThreshold)
    {
        var startTime = DateTime.UtcNow;
        var photoName = Path.GetFileName(photoPath);

        // Step 1: Try direct ID match
        var metadata = ExtractPhotoMetadata(photoPath, photosDir, filenamePattern, manifest);

        if (metadata.ExternalId != null)
        {
            // Try to find player by external ID
            var player = players.FirstOrDefault(p => p.ExternalId == metadata.ExternalId);
            if (player != null)
            {
                return new MappingResult
                {
                    PhotoFileName = photoName,
                    ExternalId = metadata.ExternalId,
                    PlayerId = player.PlayerId,
                    Method = MatchMethod.DirectIdMatch,
                    Confidence = 1.0,
                    ConfidenceThreshold = confidenceThreshold,
                    ModelUsed = "DirectIdMatch",
                    ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                    Metadata = new Dictionary<string, string>
                    {
                        { "source", metadata.Source.ToString() }
                    }
                };
            }
        }

        // Step 2: Try AI name matching
        if (string.IsNullOrEmpty(metadata.FullName))
        {
            Console.WriteLine($"    No full name available, skipping AI matching");
            return new MappingResult
            {
                PhotoFileName = photoName,
                Method = MatchMethod.NoMatch,
                Confidence = 0.0,
                ConfidenceThreshold = confidenceThreshold,
                ModelUsed = nameModel,
                ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds
            };
        }

        // Find best matching player using AI
        var matchResults = new List<Models.MatchResult>();
        foreach (var player in players)
        {
            var matchResult = await _nameMatchingService.CompareNamesAsync(
                player.FullName,
                metadata.FullName ?? string.Empty
            );

            matchResult.PlayerId = player.PlayerId;
            matchResults.Add(matchResult);
        }

        // Get best match (highest confidence)
        var bestMatch = matchResults
            .OrderByDescending(r => r.Confidence)
            .FirstOrDefault();

        if (bestMatch != null)
        {
            Console.WriteLine($"    Best match: Player {bestMatch.PlayerId} (Confidence: {bestMatch.Confidence:P1})");
        }

        if (bestMatch == null || bestMatch.Confidence < confidenceThreshold)
        {
            return new MappingResult
            {
                PhotoFileName = photoName,
                Method = MatchMethod.NoMatch,
                Confidence = 0.0,
                ConfidenceThreshold = confidenceThreshold,
                ModelUsed = nameModel,
                ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds
            };
        }

        return new MappingResult
        {
            PhotoFileName = photoName,
            PlayerId = bestMatch.PlayerId,
            ExternalId = metadata.ExternalId,
            Method = MatchMethod.AiNameMatching,
            Confidence = bestMatch.Confidence,
            ConfidenceThreshold = confidenceThreshold,
            ModelUsed = nameModel,
            ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
            Metadata = new Dictionary<string, string>
            {
                { "reason", bestMatch.Metadata.GetValueOrDefault("reason", string.Empty) }
            }
        };
    }

    /// <summary>
    /// Extracts photo metadata using filename pattern or manifest.
    /// </summary>
    private PhotoMetadata ExtractPhotoMetadata(
        string photoPath,
        string photosDir,
        string? filenamePattern,
        Dictionary<string, PhotoMetadata>? manifest)
    {
        var photoName = Path.GetFileName(photoPath);

        // Priority 1: Manifest (if provided)
        if (manifest != null && manifest.TryGetValue(photoName, out var manifestMetadata))
        {
            return manifestMetadata;
        }

        // Priority 2: User-specified template
        if (!string.IsNullOrEmpty(filenamePattern))
        {
            var parsed = FilenameParser.ParseWithTemplate(photoName, filenamePattern);
            if (parsed != null)
            {
                return parsed;
            }
        }

        // Priority 3: Auto-detect pattern
        var autoParsed = FilenameParser.ParseAutoDetect(photoName);
        if (autoParsed != null)
        {
            return autoParsed;
        }

        // Priority 4: Use photo name only
        return new PhotoMetadata
        {
            FileName = photoName,
            FilePath = Path.Combine(photosDir, photoName),
            Source = MetadataSource.Unknown
        };
    }

    #endregion
}
