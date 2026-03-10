using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PhotoMapperAI.Commands;
using PhotoMapperAI.Models;
using PhotoMapperAI.Services.AI;
using PhotoMapperAI.Services.Diagnostics;
using PhotoMapperAI.Services.Image;

namespace PhotoMapperAI.UI.Execution;

public sealed class ExternalGenerateCliRunner
{
    public async Task<GeneratePhotosResult> ExecuteAsync(
        string workingDirectory,
        string inputCsvPath,
        string photosDir,
        string outputDir,
        string format,
        string faceDetectionModel,
        bool portraitOnly,
        int width,
        int height,
        string? sizeProfilePath,
        bool allSizes,
        bool ignoreProfilePlaceholders,
        bool downloadOpenCvModels,
        string? onlyPlayer,
        string? placeholderImagePath,
        int? cropFrameWidth,
        int? cropFrameHeight,
        CropOffsetPreset? cropOffsetPreset,
        CancellationToken cancellationToken,
        IProgress<string>? log,
        IProgress<(int processed, int total, string current)>? progress = null)
    {
        var preflight = await PreflightChecker.CheckGenerateAsync(faceDetectionModel, downloadOpenCvModels);
        if (!preflight.IsOk)
        {
            log?.Report(preflight.BuildMessage());
            return new GeneratePhotosResult { ExitCode = 1 };
        }

        var warningMessage = preflight.BuildWarningMessage();
        if (!string.IsNullOrWhiteSpace(warningMessage))
        {
            log?.Report(warningMessage);
        }

        var faceDetectionService = FaceDetectionServiceFactory.Create(faceDetectionModel);
        log?.Report($"Resolved face detection service: {faceDetectionService.ModelName}");
        await faceDetectionService.InitializeAsync();

        var cachePath = Path.Combine(workingDirectory, ".face-detection-cache.json");
        var cache = new FaceDetectionCache(cachePath);
        var imageProcessor = new ImageProcessor();
        var logic = new GeneratePhotosCommandLogic(
            faceDetectionService,
            imageProcessor,
            cache,
            cropOffsetPreset,
            faceDetectionTrace: false);

        if (string.IsNullOrWhiteSpace(sizeProfilePath))
        {
            logic.SetPlaceholderImagePath(placeholderImagePath);
            Directory.CreateDirectory(outputDir);
            return await logic.ExecuteWithResultAsync(
                inputCsvPath,
                photosDir,
                outputDir,
                format,
                faceDetectionModel,
                crop: "generic",
                portraitOnly,
                width,
                height,
                parallel: false,
                parallelDegree: 4,
                onlyPlayerId: onlyPlayer,
                placeholderImagePath: placeholderImagePath,
                progress: progress,
                cancellationToken: cancellationToken,
                log: log,
                cropFrameWidth: cropFrameWidth,
                cropFrameHeight: cropFrameHeight);
        }

        var loadedProfile = SizeProfileLoader.LoadFromFile(sizeProfilePath);
        log?.Report($"Using size profile '{loadedProfile.Name}' from {sizeProfilePath}");

        if (!allSizes)
        {
            var firstVariant = loadedProfile.Variants.FirstOrDefault(v =>
                                   string.Equals(v.Key, "x200x300", StringComparison.OrdinalIgnoreCase)
                                   || (v.Width == 200 && v.Height == 300))
                               ?? loadedProfile.Variants.First();

            var resolvedPlaceholder = placeholderImagePath
                ?? (ignoreProfilePlaceholders ? null : firstVariant.PlaceholderPath);

            logic.SetPlaceholderImagePath(resolvedPlaceholder);
            Directory.CreateDirectory(outputDir);
            return await logic.ExecuteWithResultAsync(
                inputCsvPath,
                photosDir,
                outputDir,
                format,
                faceDetectionModel,
                crop: "generic",
                portraitOnly,
                firstVariant.Width,
                firstVariant.Height,
                parallel: false,
                parallelDegree: 4,
                onlyPlayerId: onlyPlayer,
                placeholderImagePath: resolvedPlaceholder,
                progress: progress,
                cancellationToken: cancellationToken,
                log: log,
                cropFrameWidth: cropFrameWidth,
                cropFrameHeight: cropFrameHeight);
        }

        var variantPlans = loadedProfile.Variants.Select(variant =>
        {
            var subfolder = string.IsNullOrWhiteSpace(variant.OutputSubfolder) ? variant.Key : variant.OutputSubfolder;
            var variantOutput = Path.Combine(outputDir, subfolder);
            var resolvedPlaceholder = placeholderImagePath
                ?? (ignoreProfilePlaceholders ? null : variant.PlaceholderPath);
            return new GeneratePhotosVariantPlan(
                variant.Key,
                variant.Width,
                variant.Height,
                variantOutput,
                resolvedPlaceholder);
        }).ToList();

        return await logic.ExecuteMultiVariantAsync(
            inputCsvPath,
            photosDir,
            variantPlans,
            format,
            faceDetectionModel,
            crop: "generic",
            portraitOnly,
            parallel: false,
            parallelDegree: 4,
            onlyPlayerId: onlyPlayer,
            cropFrameWidth: cropFrameWidth,
            cropFrameHeight: cropFrameHeight,
            progress: progress,
            cancellationToken: cancellationToken,
            log: log);
    }

    public string BuildCommandPreview(
        string workingDirectory,
        string inputCsvPath,
        string photosDir,
        string outputDir,
        string format,
        string faceDetectionModel,
        bool portraitOnly,
        int width,
        int height,
        string? sizeProfilePath,
        bool allSizes,
        bool ignoreProfilePlaceholders,
        bool downloadOpenCvModels,
        string? onlyPlayer,
        string? placeholderImagePath,
        int? cropFrameWidth,
        int? cropFrameHeight)
    {
        var parts = BuildGenerateArgs(
            inputCsvPath,
            photosDir,
            outputDir,
            format,
            faceDetectionModel,
            portraitOnly,
            width,
            height,
            sizeProfilePath,
            allSizes,
            ignoreProfilePlaceholders,
            downloadOpenCvModels,
            onlyPlayer,
            placeholderImagePath,
            cropFrameWidth,
            cropFrameHeight,
            cropOffsetPreset: null);

        return $"Working directory: {workingDirectory}\nExecution mode: in-process-shared-logic\nEquivalent CLI: dotnet "
            + string.Join(" ", parts.Select(p => p.Contains(' ') ? $"\"{p}\"" : p));
    }

    public string WriteDebugArtifact(
        string outputDir,
        string workingDirectory,
        string inputCsvPath,
        string photosDirectory,
        string imageFormat,
        string faceDetectionModel,
        bool portraitOnly,
        int width,
        int height,
        string? sizeProfilePath,
        bool allSizes,
        bool ignoreProfilePlaceholders,
        bool downloadOpenCvModels,
        string? onlyPlayer,
        string? placeholderImagePath,
        int? cropFrameWidth,
        int? cropFrameHeight)
    {
        var payload = new
        {
            utc = DateTime.UtcNow,
            workingDirectory,
            inputCsvPath,
            photosDirectory,
            outputDirectory = outputDir,
            imageFormat,
            faceDetectionModel,
            portraitOnly,
            width,
            height,
            sizeProfilePath,
            allSizes,
            ignoreProfilePlaceholders,
            downloadOpenCvModels,
            onlyPlayer,
            placeholderImagePath,
            cropFrameWidth,
            cropFrameHeight,
            executionMode = "in-process-shared-logic",
            args = BuildGenerateArgs(
                inputCsvPath,
                photosDirectory,
                outputDir,
                imageFormat,
                faceDetectionModel,
                portraitOnly,
                width,
                height,
                sizeProfilePath,
                allSizes,
                ignoreProfilePlaceholders,
                downloadOpenCvModels,
                onlyPlayer,
                placeholderImagePath,
                cropFrameWidth,
                cropFrameHeight,
                cropOffsetPreset: null)
        };

        Directory.CreateDirectory(outputDir);
        var path = Path.Combine(outputDir, "_gui_run_debug.json");
        File.WriteAllText(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    private static List<string> BuildGenerateArgs(
        string inputCsvPath,
        string photosDir,
        string outputDir,
        string format,
        string faceDetectionModel,
        bool portraitOnly,
        int width,
        int height,
        string? sizeProfilePath,
        bool allSizes,
        bool ignoreProfilePlaceholders,
        bool downloadOpenCvModels,
        string? onlyPlayer,
        string? placeholderImagePath,
        int? cropFrameWidth,
        int? cropFrameHeight,
        CropOffsetPreset? cropOffsetPreset)
    {
        var args = new List<string>
        {
            "run", "--project", "src/PhotoMapperAI", "--", "generatephotos",
            "--inputCsvPath", inputCsvPath,
            "--photosDir", photosDir,
            "--processedPhotosOutputPath", outputDir,
            "--format", format,
            "--faceDetection", faceDetectionModel
        };

        if (!string.IsNullOrWhiteSpace(sizeProfilePath))
        {
            args.Add("--sizeProfile");
            args.Add(sizeProfilePath);

            if (allSizes)
            {
                args.Add("--allSizes");
            }

            if (ignoreProfilePlaceholders)
            {
                args.Add("--noProfilePlaceholders");
            }
        }
        else
        {
            args.Add("--faceWidth");
            args.Add(width.ToString());
            args.Add("--faceHeight");
            args.Add(height.ToString());

            if (cropFrameWidth.HasValue)
            {
                args.Add("--cropFrameWidth");
                args.Add(cropFrameWidth.Value.ToString());
            }

            if (cropFrameHeight.HasValue)
            {
                args.Add("--cropFrameHeight");
                args.Add(cropFrameHeight.Value.ToString());
            }
        }

        if (portraitOnly)
            args.Add("--portraitOnly");

        if (downloadOpenCvModels)
            args.Add("--downloadOpenCvModels");

        if (!string.IsNullOrWhiteSpace(onlyPlayer))
        {
            args.Add("--onlyPlayer");
            args.Add(onlyPlayer);
        }

        if (!string.IsNullOrWhiteSpace(placeholderImagePath))
        {
            args.Add("--placeholderImage");
            args.Add(placeholderImagePath);
        }

        if (cropOffsetPreset != null)
        {
            args.Add("--cropOffsetX");
            args.Add(cropOffsetPreset.HorizontalPercent.ToString(System.Globalization.CultureInfo.InvariantCulture));
            args.Add("--cropOffsetY");
            args.Add(cropOffsetPreset.VerticalPercent.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return args;
    }
}
