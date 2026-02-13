using System.Globalization;
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
    private readonly Dictionary<string, NameSignature> _nameSignatureCache = new(StringComparer.Ordinal);

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
        CancellationToken cancellationToken = default,
        bool aiTrace = false,
        bool aiOnly = false)
    {
        Console.WriteLine("Map Command");
        Console.WriteLine("============");
        Console.WriteLine($"CSV File: {inputCsvPath}");
        Console.WriteLine($"Photos Dir: {photosDir}");
        Console.WriteLine($"Name Model: {nameModel}");
        Console.WriteLine($"Confidence Threshold: {confidenceThreshold}");
        Console.WriteLine($"Use AI: {useAi}");
        Console.WriteLine($"AI Second Pass: {aiSecondPass}");
        Console.WriteLine($"AI Trace: {aiTrace}");
        Console.WriteLine($"AI Only: {aiOnly}");
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
            var aiFirstPassMatches = 0;
            var aiSecondPassMatches = 0;
            var aiFirstPassPlayersEvaluated = 0;
            var aiSecondPassPlayersEvaluated = 0;
            var aiFirstPassComparisons = 0;
            var aiSecondPassComparisons = 0;
            var aiFirstPassUsageCalls = 0;
            var aiSecondPassUsageCalls = 0;
            var aiFirstPassPromptTokens = 0;
            var aiSecondPassPromptTokens = 0;
            var aiFirstPassCompletionTokens = 0;
            var aiSecondPassCompletionTokens = 0;
            var aiFirstPassTotalTokens = 0;
            var aiSecondPassTotalTokens = 0;

            var photoCandidates = BuildPhotoCandidates(photos, photosDir, filenamePattern, manifest);
            var remainingCandidates = new List<PhotoCandidate>(photoCandidates);
            var remainingByExternalId = remainingCandidates
                .Where(c => !string.IsNullOrWhiteSpace(c.Metadata.ExternalId))
                .GroupBy(c => c.Metadata.ExternalId!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var progress = new ProgressIndicator("Progress", totalPlayers, useBar: true);
            var unmatchedPlayers = new List<PlayerRecord>();

            // Phase 1: direct ID only
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
                    }
                    else
                    {
                        results.Add(BuildNoMatchResult(player, confidenceThreshold, "ExternalId not found in photos"));
                    }

                    continue;
                }

                unmatchedPlayers.Add(player);
            }

            progress.Complete();

            // Phase 2: deterministic global assignment (optional)
            if (!aiOnly)
            {
                stringMatches = ApplyDeterministicGlobalMatches(
                    unmatchedPlayers,
                    remainingCandidates,
                    remainingByExternalId,
                    confidenceThreshold,
                    results);
            }

            // Phase 3: AI fallback passes
            if (useAi && unmatchedPlayers.Count > 0 && remainingCandidates.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Running AI pass 1 for unresolved players...");
                var aiFirstPass = await ApplyAiGlobalMatchesAsync(
                    unmatchedPlayers,
                    remainingCandidates,
                    remainingByExternalId,
                    confidenceThreshold,
                    nameModel,
                    results,
                    maxCandidates: 6,
                    minPreselectScore: 0.45,
                    maxGapFromTop: 0.25,
                    ambiguityMargin: 0.06,
                    progressLabel: "AI Pass 1",
                    uiProgress: uiProgress,
                    aiTrace: aiTrace,
                    passNumber: 1,
                    cancellationToken: cancellationToken);
                aiFirstPassMatches = aiFirstPass.Applied;
                aiFirstPassPlayersEvaluated = aiFirstPass.PlayersEvaluated;
                aiFirstPassComparisons = aiFirstPass.ModelComparisons;
                aiFirstPassUsageCalls = aiFirstPass.UsageCalls;
                aiFirstPassPromptTokens = aiFirstPass.PromptTokens;
                aiFirstPassCompletionTokens = aiFirstPass.CompletionTokens;
                aiFirstPassTotalTokens = aiFirstPass.TotalTokens;

                if (aiSecondPass && unmatchedPlayers.Count > 0 && remainingCandidates.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("Running AI pass 2 for remaining unresolved players...");
                    var aiSecondPassResult = await ApplyAiGlobalMatchesAsync(
                        unmatchedPlayers,
                        remainingCandidates,
                        remainingByExternalId,
                        confidenceThreshold,
                        nameModel,
                        results,
                        maxCandidates: 10,
                        minPreselectScore: 0.30,
                        maxGapFromTop: 0.35,
                        ambiguityMargin: 0.03,
                        progressLabel: "AI Pass 2",
                        uiProgress: uiProgress,
                        aiTrace: aiTrace,
                        passNumber: 2,
                        cancellationToken: cancellationToken);
                    aiSecondPassMatches = aiSecondPassResult.Applied;
                    aiSecondPassPlayersEvaluated = aiSecondPassResult.PlayersEvaluated;
                    aiSecondPassComparisons = aiSecondPassResult.ModelComparisons;
                    aiSecondPassUsageCalls = aiSecondPassResult.UsageCalls;
                    aiSecondPassPromptTokens = aiSecondPassResult.PromptTokens;
                    aiSecondPassCompletionTokens = aiSecondPassResult.CompletionTokens;
                    aiSecondPassTotalTokens = aiSecondPassResult.TotalTokens;
                }
            }

            foreach (var player in unmatchedPlayers)
            {
                results.Add(BuildNoMatchResult(player, confidenceThreshold, useAi ? "No AI match" : "No match"));
            }

            var firstRoundMapped = directIdMatches + stringMatches;
            var aiMatches = aiFirstPassMatches + aiSecondPassMatches;
            var totalMatched = results.Count(r => r.IsValidMatch);
            var unmappedCount = results.Count - totalMatched;
            var aiTotalPlayersEvaluated = aiFirstPassPlayersEvaluated + aiSecondPassPlayersEvaluated;
            var aiTotalComparisons = aiFirstPassComparisons + aiSecondPassComparisons;
            var aiTotalUsageCalls = aiFirstPassUsageCalls + aiSecondPassUsageCalls;
            var aiTotalPromptTokens = aiFirstPassPromptTokens + aiSecondPassPromptTokens;
            var aiTotalCompletionTokens = aiFirstPassCompletionTokens + aiSecondPassCompletionTokens;
            var aiTotalTokens = aiFirstPassTotalTokens + aiSecondPassTotalTokens;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Matched {totalMatched} / {results.Count} players");
            Console.WriteLine($"✓ First round mapped (ID + String): {firstRoundMapped} (ID: {directIdMatches}, String: {stringMatches})");
            Console.WriteLine($"✓ AI round mapped: {aiMatches} (Pass 1: {aiFirstPassMatches}, Pass 2: {aiSecondPassMatches})");
            Console.WriteLine($"✓ AI evaluated: {aiTotalPlayersEvaluated} players (Pass 1: {aiFirstPassPlayersEvaluated}, Pass 2: {aiSecondPassPlayersEvaluated}), {aiTotalComparisons} model comparisons (Pass 1: {aiFirstPassComparisons}, Pass 2: {aiSecondPassComparisons})");
            if (aiTotalUsageCalls > 0 || aiTotalTokens > 0)
            {
                Console.WriteLine(
                    $"✓ AI usage: {aiTotalUsageCalls} billable calls, {aiTotalPromptTokens} prompt/input tokens, " +
                    $"{aiTotalCompletionTokens} completion/output tokens, {aiTotalTokens} total tokens " +
                    $"(Pass 1 calls/tokens: {aiFirstPassUsageCalls}/{aiFirstPassTotalTokens}, Pass 2 calls/tokens: {aiSecondPassUsageCalls}/{aiSecondPassTotalTokens})");
            }
            Console.WriteLine($"✓ Left unmapped: {unmappedCount}");
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
    private sealed record NameSignature(string Normalized, string[] Tokens, HashSet<string> TokenSet);
    private sealed record RankedCandidate(PhotoCandidate Candidate, double Score);
    private sealed record MatchProposal(PlayerRecord Player, PhotoCandidate Candidate, double Confidence, string? Reason = null);
    private sealed record AiPassResult(
        int Applied,
        int PlayersEvaluated,
        int ModelComparisons,
        int UsageCalls,
        int PromptTokens,
        int CompletionTokens,
        int TotalTokens);
    private sealed record AiProposalAttempt(
        MatchProposal? Proposal,
        int Comparisons,
        string Outcome,
        string? Reason = null,
        int UsageCalls = 0,
        int PromptTokens = 0,
        int CompletionTokens = 0,
        int TotalTokens = 0,
        string? BestExternalId = null,
        string? BestName = null,
        double? BestConfidence = null,
        double? SecondBestConfidence = null,
        double? Margin = null,
        string? SelectedExternalId = null,
        string? SelectedName = null,
        double? SelectedConfidence = null);

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

    private int ApplyDeterministicGlobalMatches(
        List<PlayerRecord> unmatchedPlayers,
        List<PhotoCandidate> remainingCandidates,
        Dictionary<string, PhotoCandidate> remainingByExternalId,
        double confidenceThreshold,
        List<MappingResult> results)
    {
        const double ambiguityMargin = 0.07;
        var applied = 0;
        var madeProgress = true;

        while (madeProgress && unmatchedPlayers.Count > 0 && remainingCandidates.Count > 0)
        {
            madeProgress = false;
            var proposals = new List<MatchProposal>(unmatchedPlayers.Count);

            foreach (var player in unmatchedPlayers)
            {
                if (TryBuildDeterministicProposal(player, remainingCandidates, confidenceThreshold, ambiguityMargin, out var proposal))
                {
                    proposals.Add(proposal!);
                }
            }

            foreach (var proposal in proposals.OrderByDescending(p => p.Confidence))
            {
                if (!unmatchedPlayers.Contains(proposal.Player) || !remainingCandidates.Contains(proposal.Candidate))
                    continue;

                ApplyMatch(proposal.Player, proposal.Candidate, confidenceThreshold, proposal.Confidence, out var result);
                result.Method = MatchMethod.AiNameMatching;
                result.ModelUsed = "StringMatching";
                result.Metadata["reason"] = proposal.Reason ?? "deterministic_global";
                results.Add(result);

                RemoveCandidate(proposal.Candidate, remainingCandidates, remainingByExternalId);
                unmatchedPlayers.Remove(proposal.Player);
                applied++;
                madeProgress = true;
            }
        }

        return applied;
    }

    private async Task<AiPassResult> ApplyAiGlobalMatchesAsync(
        List<PlayerRecord> unmatchedPlayers,
        List<PhotoCandidate> remainingCandidates,
        Dictionary<string, PhotoCandidate> remainingByExternalId,
        double confidenceThreshold,
        string nameModel,
        List<MappingResult> results,
        int maxCandidates,
        double minPreselectScore,
        double maxGapFromTop,
        double ambiguityMargin,
        string progressLabel,
        IProgress<(int processed, int total, string current)>? uiProgress,
        bool aiTrace,
        int passNumber,
        CancellationToken cancellationToken)
    {
        if (unmatchedPlayers.Count == 0 || remainingCandidates.Count == 0)
            return new AiPassResult(0, 0, 0, 0, 0, 0, 0);

        var snapshot = unmatchedPlayers.ToList();
        var proposals = new List<MatchProposal>(snapshot.Count);
        var aiProgress = new ProgressIndicator(progressLabel, snapshot.Count, useBar: true);
        var processed = 0;
        var modelComparisons = 0;
        var usageCalls = 0;
        var promptTokens = 0;
        var completionTokens = 0;
        var totalTokens = 0;

        foreach (var player in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            aiProgress.Update(player.FullName);
            processed++;
            uiProgress?.Report((processed, snapshot.Count, $"AI: {player.FullName}"));

            var attempt = await TryBuildAiProposalAsync(
                player,
                remainingCandidates,
                confidenceThreshold,
                maxCandidates,
                minPreselectScore,
                maxGapFromTop,
                ambiguityMargin,
                cancellationToken);
            modelComparisons += attempt.Comparisons;
            usageCalls += attempt.UsageCalls;
            promptTokens += attempt.PromptTokens;
            completionTokens += attempt.CompletionTokens;
            totalTokens += attempt.TotalTokens;

            if (aiTrace)
            {
                LogAiTrace(passNumber, player, attempt);
            }

            if (attempt.Proposal != null)
            {
                proposals.Add(attempt.Proposal);
            }
        }

        aiProgress.Complete();

        var applied = 0;
        foreach (var proposal in proposals.OrderByDescending(p => p.Confidence))
        {
            if (!unmatchedPlayers.Contains(proposal.Player) || !remainingCandidates.Contains(proposal.Candidate))
                continue;

            ApplyMatch(proposal.Player, proposal.Candidate, confidenceThreshold, proposal.Confidence, out var result);
            result.Method = MatchMethod.AiNameMatching;
            result.ModelUsed = nameModel;
            if (!string.IsNullOrWhiteSpace(proposal.Reason))
            {
                result.Metadata["reason"] = proposal.Reason;
            }

            results.Add(result);
            RemoveCandidate(proposal.Candidate, remainingCandidates, remainingByExternalId);
            unmatchedPlayers.Remove(proposal.Player);
            applied++;
        }

        return new AiPassResult(applied, snapshot.Count, modelComparisons, usageCalls, promptTokens, completionTokens, totalTokens);
    }

    private bool TryBuildDeterministicProposal(
        PlayerRecord player,
        List<PhotoCandidate> candidates,
        double confidenceThreshold,
        double ambiguityMargin,
        out MatchProposal? proposal)
    {
        proposal = default;
        var ranked = RankCandidatesForPlayer(
            player,
            candidates,
            maxCandidates: 3,
            minPreselectScore: 0,
            maxGapFromTop: 1.0);

        if (ranked.Count == 0)
            return false;

        var best = ranked[0];
        var second = ranked.Count > 1 ? ranked[1].Score : 0;
        var margin = best.Score - second;

        if (best.Score < confidenceThreshold)
            return false;

        if (margin < ambiguityMargin && best.Score < 0.98)
            return false;

        proposal = new MatchProposal(
            player,
            best.Candidate,
            best.Score,
            $"deterministic_score={best.Score:0.###};margin={margin:0.###}");
        return true;
    }

    private async Task<AiProposalAttempt> TryBuildAiProposalAsync(
        PlayerRecord player,
        List<PhotoCandidate> candidates,
        double confidenceThreshold,
        int maxCandidates,
        double minPreselectScore,
        double maxGapFromTop,
        double ambiguityMargin,
        CancellationToken cancellationToken)
    {
        var ranked = RankCandidatesForPlayer(
            player,
            candidates,
            maxCandidates,
            minPreselectScore,
            maxGapFromTop);

        if (ranked.Count == 0)
            return new AiProposalAttempt(
                Proposal: null,
                Comparisons: 0,
                Outcome: "rejected",
                Reason: "no_ranked_candidates");

        RankedCandidate? bestCandidate = null;
        MatchResult? bestMatch = null;
        var secondBestConfidence = 0.0;
        var usageCalls = 0;
        var promptTokens = 0;
        var completionTokens = 0;
        var totalTokens = 0;

        foreach (var rankedCandidate in ranked)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var match = await _nameMatchingService.CompareNamesAsync(player.FullName, rankedCandidate.Candidate.DisplayName);
            AddUsage(match, ref usageCalls, ref promptTokens, ref completionTokens, ref totalTokens);

            if (bestMatch == null || match.Confidence > bestMatch.Confidence)
            {
                secondBestConfidence = bestMatch?.Confidence ?? secondBestConfidence;
                bestMatch = match;
                bestCandidate = rankedCandidate;
            }
            else if (match.Confidence > secondBestConfidence)
            {
                secondBestConfidence = match.Confidence;
            }
        }

        if (bestMatch == null || bestCandidate == null)
        {
            return new AiProposalAttempt(
                Proposal: null,
                Comparisons: ranked.Count,
                Outcome: "rejected",
                Reason: "no_model_result",
                UsageCalls: usageCalls,
                PromptTokens: promptTokens,
                CompletionTokens: completionTokens,
                TotalTokens: totalTokens);
        }

        if (bestMatch.Confidence < confidenceThreshold)
        {
            return new AiProposalAttempt(
                Proposal: null,
                Comparisons: ranked.Count,
                Outcome: "rejected",
                Reason: "below_threshold",
                UsageCalls: usageCalls,
                PromptTokens: promptTokens,
                CompletionTokens: completionTokens,
                TotalTokens: totalTokens,
                BestExternalId: bestCandidate.Candidate.Metadata.ExternalId,
                BestName: bestCandidate.Candidate.DisplayName,
                BestConfidence: bestMatch.Confidence,
                SecondBestConfidence: secondBestConfidence,
                Margin: bestMatch.Confidence - secondBestConfidence);
        }

        var margin = bestMatch.Confidence - secondBestConfidence;
        if (margin < ambiguityMargin && bestMatch.Confidence < 0.95)
        {
            return new AiProposalAttempt(
                Proposal: null,
                Comparisons: ranked.Count,
                Outcome: "rejected",
                Reason: "ambiguous",
                UsageCalls: usageCalls,
                PromptTokens: promptTokens,
                CompletionTokens: completionTokens,
                TotalTokens: totalTokens,
                BestExternalId: bestCandidate.Candidate.Metadata.ExternalId,
                BestName: bestCandidate.Candidate.DisplayName,
                BestConfidence: bestMatch.Confidence,
                SecondBestConfidence: secondBestConfidence,
                Margin: margin);
        }

        var reason = bestMatch.Metadata.GetValueOrDefault("reason", string.Empty);
        if (string.IsNullOrWhiteSpace(reason))
        {
            reason = $"ai_confidence={bestMatch.Confidence:0.###};margin={margin:0.###};pre_rank_score={bestCandidate.Score:0.###}";
        }

        return new AiProposalAttempt(
            Proposal: new MatchProposal(player, bestCandidate.Candidate, bestMatch.Confidence, reason),
            Comparisons: ranked.Count,
            Outcome: "accepted",
            Reason: "accepted",
            UsageCalls: usageCalls,
            PromptTokens: promptTokens,
            CompletionTokens: completionTokens,
            TotalTokens: totalTokens,
            BestExternalId: bestCandidate.Candidate.Metadata.ExternalId,
            BestName: bestCandidate.Candidate.DisplayName,
            BestConfidence: bestMatch.Confidence,
            SecondBestConfidence: secondBestConfidence,
            Margin: margin,
            SelectedExternalId: bestCandidate.Candidate.Metadata.ExternalId,
            SelectedName: bestCandidate.Candidate.DisplayName,
            SelectedConfidence: bestMatch.Confidence);
    }

    private static void LogAiTrace(int passNumber, PlayerRecord player, AiProposalAttempt attempt)
    {
        Console.WriteLine(
            "AI_TRACE" +
            $"|pass={passNumber}" +
            $"|player_id={FormatTraceValue(player.PlayerId.ToString(CultureInfo.InvariantCulture))}" +
            $"|player_name={FormatTraceValue(player.FullName)}" +
            $"|outcome={FormatTraceValue(attempt.Outcome)}" +
            $"|reason={FormatTraceValue(attempt.Reason)}" +
            $"|compared={attempt.Comparisons}" +
            $"|best_external_id={FormatTraceValue(attempt.BestExternalId)}" +
            $"|best_name={FormatTraceValue(attempt.BestName)}" +
            $"|best_conf={FormatTraceDouble(attempt.BestConfidence)}" +
            $"|second_conf={FormatTraceDouble(attempt.SecondBestConfidence)}" +
            $"|margin={FormatTraceDouble(attempt.Margin)}" +
            $"|selected_external_id={FormatTraceValue(attempt.SelectedExternalId)}" +
            $"|selected_name={FormatTraceValue(attempt.SelectedName)}" +
            $"|selected_conf={FormatTraceDouble(attempt.SelectedConfidence)}");
    }

    private static string FormatTraceValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Replace('|', '/').Replace('\n', ' ').Replace('\r', ' ').Trim();
    }

    private static string FormatTraceDouble(double? value)
    {
        if (!value.HasValue)
            return string.Empty;

        return value.Value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static void AddUsage(
        MatchResult match,
        ref int usageCalls,
        ref int promptTokens,
        ref int completionTokens,
        ref int totalTokens)
    {
        if (match.Metadata == null || match.Metadata.Count == 0)
            return;

        var hasPrompt = TryGetInt(match.Metadata, "usage_prompt_tokens", out var prompt);
        var hasCompletion = TryGetInt(match.Metadata, "usage_completion_tokens", out var completion);
        var hasTotal = TryGetInt(match.Metadata, "usage_total_tokens", out var total);
        if (!hasPrompt && !hasCompletion && !hasTotal)
            return;

        usageCalls++;
        promptTokens += hasPrompt ? prompt : 0;
        completionTokens += hasCompletion ? completion : 0;

        if (hasTotal)
        {
            totalTokens += total;
        }
        else
        {
            totalTokens += (hasPrompt ? prompt : 0) + (hasCompletion ? completion : 0);
        }
    }

    private static bool TryGetInt(IDictionary<string, string> metadata, string key, out int value)
    {
        value = 0;
        return metadata.TryGetValue(key, out var raw) && int.TryParse(raw, out value);
    }

    private List<RankedCandidate> RankCandidatesForPlayer(
        PlayerRecord player,
        List<PhotoCandidate> candidates,
        int maxCandidates,
        double minPreselectScore,
        double maxGapFromTop)
    {
        var playerSignature = GetNameSignature(player.FullName);
        var ranked = new List<RankedCandidate>(candidates.Count);

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.Metadata.ExternalId) || string.IsNullOrWhiteSpace(candidate.DisplayName))
                continue;

            var candidateSignature = GetNameSignature(candidate.DisplayName);
            var score = ScoreDeterministic(playerSignature, candidateSignature);
            ranked.Add(new RankedCandidate(candidate, score));
        }

        if (ranked.Count == 0)
            return ranked;

        ranked = ranked
            .OrderByDescending(r => r.Score)
            .ToList();

        var topScore = ranked[0].Score;
        var filtered = ranked
            .Where(r => r.Score >= minPreselectScore || (topScore - r.Score) <= maxGapFromTop)
            .Take(Math.Max(1, maxCandidates))
            .ToList();

        if (filtered.Count == 0)
        {
            filtered = ranked.Take(Math.Max(1, maxCandidates)).ToList();
        }

        return filtered;
    }

    private NameSignature GetNameSignature(string name)
    {
        var key = name ?? string.Empty;
        if (_nameSignatureCache.TryGetValue(key, out var cached))
            return cached;

        var normalized = StringMatching.NormalizeName(key);
        var tokens = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => !token.All(char.IsDigit))
            .ToArray();

        var signature = new NameSignature(
            normalized,
            tokens,
            new HashSet<string>(tokens, StringComparer.Ordinal));

        _nameSignatureCache[key] = signature;
        return signature;
    }

    private static double ScoreDeterministic(NameSignature left, NameSignature right)
    {
        if (left.Tokens.Length == 0 || right.Tokens.Length == 0)
            return 0;

        if (string.Equals(left.Normalized, right.Normalized, StringComparison.Ordinal))
            return 1.0;

        var intersectionCount = left.TokenSet.Intersect(right.TokenSet).Count();
        var unionCount = left.TokenSet.Count + right.TokenSet.Count - intersectionCount;
        var minTokenCount = Math.Min(left.TokenSet.Count, right.TokenSet.Count);

        var overlapRatio = minTokenCount == 0 ? 0 : (double)intersectionCount / minTokenCount;
        var jaccard = unionCount == 0 ? 0 : (double)intersectionCount / unionCount;
        var orderedSimilarity = StringMatching.CalculateSimilarity(left.Normalized, right.Normalized);

        var smaller = left.Tokens.Length <= right.Tokens.Length ? left.Tokens : right.Tokens;
        var larger = left.Tokens.Length <= right.Tokens.Length ? right.Tokens : left.Tokens;
        var softTokenSimilarity = AverageBestTokenSimilarity(smaller, larger);

        var score = (0.45 * overlapRatio) +
                    (0.20 * jaccard) +
                    (0.20 * softTokenSimilarity) +
                    (0.15 * orderedSimilarity);

        // Strong subset/equality evidence should be promoted.
        if (intersectionCount == minTokenCount && minTokenCount >= 2)
        {
            score = Math.Max(score, 0.93);
        }

        var nearVariantBoosted = false;

        // Handle diminutive/variant given names without hardcoded dictionaries:
        // if exactly one token differs on the shorter side, allow a controlled boost
        // when the differing tokens are clearly close by string shape.
        if (minTokenCount >= 2 && intersectionCount == minTokenCount - 1)
        {
            var shorter = left.TokenSet.Count <= right.TokenSet.Count ? left.TokenSet : right.TokenSet;
            var longer = left.TokenSet.Count <= right.TokenSet.Count ? right.TokenSet : left.TokenSet;

            var missingFromShorter = shorter.Where(token => !longer.Contains(token)).ToList();
            var extraInLonger = longer.Where(token => !shorter.Contains(token)).ToList();

            if (missingFromShorter.Count == 1 && extraInLonger.Count > 0)
            {
                var missing = missingFromShorter[0];
                var bestOther = extraInLonger
                    .OrderByDescending(token => StringMatching.CalculateSimilarity(missing, token))
                    .First();

                var similarity = StringMatching.CalculateSimilarity(missing, bestOther);
                var sameInitial = missing.Length > 0 && bestOther.Length > 0 && missing[0] == bestOther[0];
                var sharedPrefix = GetCommonPrefixLength(missing, bestOther);

                if (sameInitial && (similarity >= 0.55 || sharedPrefix >= 3))
                {
                    score = Math.Max(score, 0.84);
                    nearVariantBoosted = true;
                }
            }
        }

        // Keep weak-overlap cases conservative to reduce false positives.
        if (intersectionCount == 0)
        {
            score = Math.Min(score, 0.35);
        }
        else if (intersectionCount == 1 && minTokenCount >= 2 && !nearVariantBoosted)
        {
            score = Math.Min(score, 0.79);
        }

        return Math.Clamp(score, 0.0, 1.0);
    }

    private static double AverageBestTokenSimilarity(string[] smaller, string[] larger)
    {
        if (smaller.Length == 0 || larger.Length == 0)
            return 0;

        var total = 0.0;
        foreach (var token in smaller)
        {
            var best = 0.0;
            foreach (var other in larger)
            {
                var similarity = StringMatching.CalculateSimilarity(token, other);
                if (similarity > best)
                {
                    best = similarity;
                }
            }

            total += best;
        }

        return total / smaller.Length;
    }

    private static int GetCommonPrefixLength(string left, string right)
    {
        var max = Math.Min(left.Length, right.Length);
        var count = 0;
        while (count < max && left[count] == right[count])
        {
            count++;
        }

        return count;
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
