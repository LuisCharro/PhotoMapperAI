using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PhotoMapperAI.Commands;
using PhotoMapperAI.Models;
using PhotoMapperAI.Services.Diagnostics;

namespace PhotoMapperAI.UI.Execution;

public sealed class ExternalGenerateCliRunner
{
    private static readonly Regex LoadedPlayersRegex = new(@"Loaded\s+(?<count>\d+)\s+players", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex GeneratedRegex = new(@"Generated\s+(?<ok>\d+)\s+portraits\s+\((?<failed>\d+)\s+failed\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
        _ = cropOffsetPreset;
        _ = progress;

        var args = BuildGenerateArgs(
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
            placeholderImagePath);

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        var result = new GeneratePhotosResult();

        process.OutputDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data))
                return;

            log?.Report(e.Data);
            UpdateResultFromOutput(e.Data, result);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data))
                return;

            log?.Report(e.Data);
            UpdateResultFromOutput(e.Data, result);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Best effort cancellation.
            }
        });

        await process.WaitForExitAsync(cancellationToken);

        result.ExitCode = process.ExitCode;
        result.IsCancelled = cancellationToken.IsCancellationRequested;
        result.ProcessedPlayers = result.PortraitsGenerated + result.PortraitsFailed;

        if (result.TotalPlayers == 0)
        {
            result.TotalPlayers = result.ProcessedPlayers;
        }

        return result;
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
        return $"Working directory: {workingDirectory}\nExecution mode: external-cli\nCommand: dotnet " + string.Join(" ", parts.Select(p => p.Contains(' ') ? $"\"{p}\"" : p));
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
            executionMode = "external-cli",
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

    private static void UpdateResultFromOutput(string line, GeneratePhotosResult result)
    {
        var loadedMatch = LoadedPlayersRegex.Match(line);
        if (loadedMatch.Success && int.TryParse(loadedMatch.Groups["count"].Value, out var totalPlayers))
        {
            result.TotalPlayers = totalPlayers;
        }

        var generatedMatch = GeneratedRegex.Match(line);
        if (generatedMatch.Success)
        {
            if (int.TryParse(generatedMatch.Groups["ok"].Value, out var portraitsGenerated))
            {
                result.PortraitsGenerated = portraitsGenerated;
            }

            if (int.TryParse(generatedMatch.Groups["failed"].Value, out var portraitsFailed))
            {
                result.PortraitsFailed = portraitsFailed;
            }
        }
    }
}
