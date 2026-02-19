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
        CropOffsetPreset? cropOffsetPreset,
        CancellationToken cancellationToken,
        IProgress<string>? log,
        IProgress<(int processed, int total, string current)>? progress = null)
    {
        _ = workingDirectory;
        _ = downloadOpenCvModels;

        var faceDetectionService = FaceDetectionServiceFactory.Create(faceDetectionModel);
        await faceDetectionService.InitializeAsync();

        var imageProcessor = new ImageProcessor();
        var logic = new GeneratePhotosCommandLogic(faceDetectionService, imageProcessor, cache: null, cropOffsetPreset);

        if (string.IsNullOrWhiteSpace(sizeProfilePath))
        {
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
                log: log);
        }

        var profile = SizeProfileLoader.LoadFromFile(sizeProfilePath);

        if (!allSizes)
        {
            var firstVariant = profile.Variants.FirstOrDefault(v =>
                                   string.Equals(v.Key, "x200x300", StringComparison.OrdinalIgnoreCase)
                                   || (v.Width == 200 && v.Height == 300))
                               ?? profile.Variants.First();
            var placeholder = ignoreProfilePlaceholders ? null : firstVariant.PlaceholderPath;

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
                placeholderImagePath: placeholder,
                progress: progress,
                cancellationToken: cancellationToken,
                log: log);
        }

        var variants = profile.Variants.Select(variant =>
        {
            var subfolder = string.IsNullOrWhiteSpace(variant.OutputSubfolder) ? variant.Key : variant.OutputSubfolder;
            var variantOutput = Path.Combine(outputDir, subfolder);
            var placeholder = ignoreProfilePlaceholders ? null : variant.PlaceholderPath;
            return new GeneratePhotosVariantPlan(variant.Key, variant.Width, variant.Height, variantOutput, placeholder);
        }).ToList();

        return await logic.ExecuteMultiVariantAsync(
            inputCsvPath,
            photosDir,
            variants,
            format,
            faceDetectionModel,
            crop: "generic",
            portraitOnly,
            parallel: false,
            parallelDegree: 4,
            onlyPlayerId: onlyPlayer,
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
        string? placeholderImagePath)
    {
        var parts = BuildGenerateArgs(inputCsvPath, photosDir, outputDir, format, faceDetectionModel, portraitOnly, width, height, sizeProfilePath, allSizes, ignoreProfilePlaceholders, downloadOpenCvModels, onlyPlayer, placeholderImagePath);
        return $"Working directory: {workingDirectory}\nExecution mode: in-process\nEquivalent CLI: dotnet " + string.Join(" ", parts.Select(p => p.Contains(' ') ? $"\"{p}\"" : p));
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
        string? placeholderImagePath)
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
            executionMode = "in-process",
            args = BuildGenerateArgs(inputCsvPath, photosDirectory, outputDir, imageFormat, faceDetectionModel, portraitOnly, width, height, sizeProfilePath, allSizes, ignoreProfilePlaceholders, downloadOpenCvModels, onlyPlayer, placeholderImagePath)
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
        string? placeholderImagePath)
    {
        var args = new List<string>
        {
            "run", "--project", "src/PhotoMapperAI", "--", "generatephotos",
            "--inputCsvPath", inputCsvPath,
            "--photosDir", photosDir,
            "--processedPhotosOutputPath", outputDir,
            "--format", format,
            "--faceDetection", faceDetectionModel,
            "--noCache"
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

        return args;
    }
}
