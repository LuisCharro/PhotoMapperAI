using PhotoMapperAI.Models;
using PhotoMapperAI.Services;
using PhotoMapperAI.Services.AI;
using PhotoMapperAI.Utils;

namespace PhotoMapperAI.Commands;

/// <summary>
/// Result of map command execution
/// </summary>
public class MapResult
{
    public int PlayersProcessed { get; set; }
    public int PlayersMatched { get; set; }
    public int DirectIdMatches { get; set; }
    public int StringMatches { get; set; }
    public int AiMatches { get; set; }
    public string OutputPath { get; set; } = string.Empty;
}

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
    public async Task<MapResult> ExecuteAsync(
        string inputCsvPath,
        string photosDir,
        string? filenamePattern,
        string? photoManifest,
        string nameModel,
        double confidenceThreshold,
        bool useAi,
        bool aiSecondPass,
        IProgress<(int processed, int total, string current)>? uiProgress = null,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Map Command");
        Console.WriteLine("============");
        Console.WriteLine($"CSV File: {inputCsvPath}");
        Console.WriteLine($"Photos Dir: {photosDir}");
        Console.WriteLine($"Name Model: {nameModel}");
        Console.WriteLine($"Confidence Threshold: {confidenceThreshold}");
        Console.WriteLine($"Use AI: {useAi}");
        Console.WriteLine($"AI Second Pass: {aiSecondPass}");
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
            var photos = photoFiles.ToList();

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

            // Step 4: Match players to photos
            Console.WriteLine("Matching players to photos...");
            var results = new List<MappingResult>();
            var totalPlayers = players.Count;
            var processedCount = 0;
            var directIdMatches = 0;
            var stringMatches = 0;
            var aiMatches = 0;

            var photoCandidates = BuildPhotoCandidates(photos, photosDir, filenamePattern, manifest);
            var remainingCandidates = new List<PhotoCandidate>(photoCandidates);
            var remainingByExternalId = remainingCandidates
                .Where(c => !string.IsNullOrWhiteSpace(c.Metadata.ExternalId))
                .ToDictionary(c => c.Metadata.ExternalId!, c => c, StringComparer.OrdinalIgnoreCase);

            var progress = new ProgressIndicator("Progress", totalPlayers, useBar: true);
            var unmatchedPlayers = new List<PlayerRecord>();

            foreach (var player in players)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress.Update(player.FullName);

                processedCount++;
                uiProgress?.Report((processedCount, totalPlayers, player.FullName));

                if (!string.IsNullOrWhiteSpace(player.ExternalId))
                {
                    if (remainingByExternalId.TryGetValue(player.ExternalId, out var directCandidate))
                    {
                        ApplyMatch(player, directCandidate, confidenceThreshold, 1.0, out var result);
                        result.Method = MatchMethod.DirectIdMatch;
                        result.ModelUsed = "DirectIdMatch";
                        results.Add(result);
                        RemoveCandidate(directCandidate, remainingCandidates, remainingByExternalId);
                        directIdMatches++;
                        continue;
                    }

                    results.Add(BuildNoMatchResult(player, confidenceThreshold, "ExternalId not found in photos"));
                    continue;
                }

                var simpleMatch = FindBestSimpleMatch(player, remainingCandidates, confidenceThreshold);
                if (simpleMatch != null)
                {
                    ApplyMatch(player, simpleMatch.Value.Candidate, confidenceThreshold, simpleMatch.Value.Confidence, out var result);
                    result.Method = MatchMethod.AiNameMatching;
                    result.ModelUsed = "StringMatching";
                    results.Add(result);
                    RemoveCandidate(simpleMatch.Value.Candidate, remainingCandidates, remainingByExternalId);
                    stringMatches++;
                    continue;
                }

                if (useAi && !aiSecondPass)
                {
                    var aiMatch = await FindBestAiMatchAsync(player, remainingCandidates, confidenceThreshold, cancellationToken);
                    if (aiMatch != null)
                    {
                        ApplyMatch(player, aiMatch.Value.Candidate, confidenceThreshold, aiMatch.Value.Match.Confidence, out var result);
                        result.Method = MatchMethod.AiNameMatching;
                        result.ModelUsed = nameModel;
                        result.Metadata["reason"] = aiMatch.Value.Match.Metadata.GetValueOrDefault("reason", string.Empty);
                        results.Add(result);
                        RemoveCandidate(aiMatch.Value.Candidate, remainingCandidates, remainingByExternalId);
                        aiMatches++;
                        continue;
                    }
                }

                unmatchedPlayers.Add(player);
            }

            progress.Complete();

            if (useAi && aiSecondPass && unmatchedPlayers.Count > 0 && remainingCandidates.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Running AI second pass for unmatched players...");
                var aiProgress = new ProgressIndicator("AI Pass", unmatchedPlayers.Count, useBar: true);
                var aiProcessed = 0;

                foreach (var player in unmatchedPlayers)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    aiProgress.Update(player.FullName);
                    aiProcessed++;
                    uiProgress?.Report((aiProcessed, unmatchedPlayers.Count, $"AI: {player.FullName}"));

                    var aiMatch = await FindBestAiMatchAsync(player, remainingCandidates, confidenceThreshold, cancellationToken);
                    if (aiMatch != null)
                    {
                        ApplyMatch(player, aiMatch.Value.Candidate, confidenceThreshold, aiMatch.Value.Match.Confidence, out var result);
                        result.Method = MatchMethod.AiNameMatching;
                        result.ModelUsed = nameModel;
                        result.Metadata["reason"] = aiMatch.Value.Match.Metadata.GetValueOrDefault("reason", string.Empty);
                        results.Add(result);
                        RemoveCandidate(aiMatch.Value.Candidate, remainingCandidates, remainingByExternalId);
                        aiMatches++;
                        continue;
                    }

                    results.Add(BuildNoMatchResult(player, confidenceThreshold, "No AI match"));
                }

                aiProgress.Complete();
            }
            else
            {
                foreach (var player in unmatchedPlayers)
                {
                    results.Add(BuildNoMatchResult(player, confidenceThreshold, "No match"));
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Matched {results.Count(r => r.IsValidMatch)} / {results.Count} players");
            Console.WriteLine($"✓ ID matches: {directIdMatches}");
            Console.WriteLine($"✓ String matches: {stringMatches}");
            Console.WriteLine($"✓ AI matches: {aiMatches}");
            Console.ResetColor();

            // Step 5: Write updated CSV
            var outputFileName = BuildMappedFileName(inputCsvPath);
            var outputPath = Path.Combine(Directory.GetCurrentDirectory(), outputFileName);
            Console.WriteLine();
            Console.WriteLine($"Writing results to: {outputPath}");
            await Services.Database.DatabaseExtractor.WriteCsvAsync(players, outputPath);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Complete! Results saved to {outputPath}");
            Console.ResetColor();

            return new MapResult
            {
                PlayersProcessed = results.Count,
                PlayersMatched = results.Count(r => r.IsValidMatch),
                DirectIdMatches = directIdMatches,
                StringMatches = stringMatches,
                AiMatches = aiMatches,
                OutputPath = outputPath
            };
        }
        catch (OperationCanceledException)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠ Mapping cancelled by user");
            Console.ResetColor();
            throw;
        }
        catch (FileNotFoundException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ File not found: {ex.FileName}");
            Console.ResetColor();
            throw;
        }
        catch (DirectoryNotFoundException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Directory not found: {ex.Message}");
            Console.ResetColor();
            throw;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Console.ResetColor();
            throw;
        }
    }

    #region Private Methods

    private sealed record PhotoCandidate(string PhotoPath, string FileName, PhotoMetadata Metadata, string DisplayName);

    private List<PhotoCandidate> BuildPhotoCandidates(
        List<string> photos,
        string photosDir,
        string? filenamePattern,
        Dictionary<string, PhotoMetadata>? manifest)
    {
        var candidates = new List<PhotoCandidate>(photos.Count);

        foreach (var photo in photos)
        {
            var fileName = Path.GetFileName(photo);
            var metadata = ExtractPhotoMetadata(photo, photosDir, filenamePattern, manifest);
            var displayName = GetPhotoDisplayName(metadata, fileName);

            candidates.Add(new PhotoCandidate(photo, fileName, metadata, displayName));
        }

        return candidates;
    }

    private static string GetPhotoDisplayName(PhotoMetadata metadata, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(metadata.FullName))
        {
            return metadata.FullName;
        }

        var raw = Path.GetFileNameWithoutExtension(fileName)
            .Replace('_', ' ')
            .Replace('-', ' ');

        return raw.Trim();
    }

    private static void RemoveCandidate(
        PhotoCandidate candidate,
        List<PhotoCandidate> remainingCandidates,
        Dictionary<string, PhotoCandidate> remainingByExternalId)
    {
        remainingCandidates.Remove(candidate);
        if (!string.IsNullOrWhiteSpace(candidate.Metadata.ExternalId))
        {
            remainingByExternalId.Remove(candidate.Metadata.ExternalId);
        }
    }

    private static void ApplyMatch(
        PlayerRecord player,
        PhotoCandidate candidate,
        double confidenceThreshold,
        double confidence,
        out MappingResult result)
    {
        var startTime = DateTime.UtcNow;
        player.ExternalId = candidate.Metadata.ExternalId;
        player.ValidMapping = true;
        player.Confidence = confidence;

        result = new MappingResult
        {
            PlayerId = player.PlayerId,
            ExternalId = candidate.Metadata.ExternalId,
            PhotoFileName = candidate.FileName,
            Confidence = confidence,
            ConfidenceThreshold = confidenceThreshold,
            ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
            Metadata = new Dictionary<string, string>
            {
                { "source", candidate.Metadata.Source.ToString() }
            }
        };
    }

    private static MappingResult BuildNoMatchResult(PlayerRecord player, double confidenceThreshold, string reason)
    {
        return new MappingResult
        {
            PlayerId = player.PlayerId,
            ExternalId = player.ExternalId,
            PhotoFileName = string.Empty,
            Confidence = 0.0,
            ConfidenceThreshold = confidenceThreshold,
            ModelUsed = "NoMatch",
            Method = MatchMethod.NoMatch,
            Metadata = new Dictionary<string, string> { { "reason", reason } }
        };
    }

    /// <summary>
    /// Finds the best matching player using simple string similarity.
    /// </summary>
    private static (PhotoCandidate Candidate, double Confidence)? FindBestSimpleMatch(
        PlayerRecord player,
        List<PhotoCandidate> candidates,
        double confidenceThreshold)
    {
        if (candidates.Count == 0)
            return null;

        PhotoCandidate? bestCandidate = null;
        double bestSimilarity = 0;

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.Metadata.ExternalId) || string.IsNullOrWhiteSpace(candidate.DisplayName))
            {
                continue;
            }

            var similarity = StringMatching.CompareNames(candidate.DisplayName, player.FullName);

            if (similarity > bestSimilarity)
            {
                bestSimilarity = similarity;
                bestCandidate = candidate;
            }
        }

        if (bestCandidate != null && bestSimilarity >= confidenceThreshold)
        {
            return (bestCandidate, bestSimilarity);
        }

        return null;
    }

    private async Task<(PhotoCandidate Candidate, MatchResult Match)?> FindBestAiMatchAsync(
        PlayerRecord player,
        List<PhotoCandidate> candidates,
        double confidenceThreshold,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
            return null;

        PhotoCandidate? bestCandidate = null;
        MatchResult? bestMatch = null;

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.Metadata.ExternalId) || string.IsNullOrWhiteSpace(candidate.DisplayName))
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var matchResult = await _nameMatchingService.CompareNamesAsync(player.FullName, candidate.DisplayName);
            if (bestMatch == null || matchResult.Confidence > bestMatch.Confidence)
            {
                bestMatch = matchResult;
                bestCandidate = candidate;
            }
        }

        if (bestMatch != null && bestCandidate != null && bestMatch.Confidence >= confidenceThreshold)
        {
            return (bestCandidate, bestMatch);
        }

        return null;
    }

    private static string BuildMappedFileName(string inputCsvPath)
    {
        var baseName = Path.GetFileNameWithoutExtension(inputCsvPath);
        return $"mapped_{baseName}.csv";
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
