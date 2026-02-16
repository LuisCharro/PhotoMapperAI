using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PhotoMapperAI.Commands;

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
        bool downloadOpenCvModels,
        string? onlyPlayer,
        string? placeholderImagePath,
        CancellationToken cancellationToken,
        IProgress<string>? log)
    {
        var args = BuildGenerateArgs(inputCsvPath, photosDir, outputDir, format, faceDetectionModel, portraitOnly, width, height, downloadOpenCvModels, onlyPlayer, placeholderImagePath);

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi };
        process.Start();

        var generated = 0;
        var failed = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await process.StandardOutput.ReadLineAsync();
            if (line == null)
                break;

            log?.Report(line);

            var m = Regex.Match(line, @"Generated\s+(\d+)\s+portraits\s+\((\d+)\s+failed\)", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                generated = int.Parse(m.Groups[1].Value);
                failed = int.Parse(m.Groups[2].Value);
            }
        }

        var stdErr = await process.StandardError.ReadToEndAsync();
        if (!string.IsNullOrWhiteSpace(stdErr))
        {
            foreach (var line in stdErr.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                log?.Report(line.TrimEnd('\r'));
        }

        await process.WaitForExitAsync(cancellationToken);

        return new GeneratePhotosResult
        {
            ExitCode = process.ExitCode,
            PortraitsGenerated = generated,
            PortraitsFailed = failed,
            IsCancelled = cancellationToken.IsCancellationRequested
        };
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
        bool downloadOpenCvModels,
        string? onlyPlayer,
        string? placeholderImagePath)
    {
        var parts = BuildGenerateArgs(inputCsvPath, photosDir, outputDir, format, faceDetectionModel, portraitOnly, width, height, downloadOpenCvModels, onlyPlayer, placeholderImagePath);
        return $"Working directory: {workingDirectory}\nCommand: dotnet " + string.Join(" ", parts.Select(p => p.Contains(' ') ? $"\"{p}\"" : p));
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
            downloadOpenCvModels,
            onlyPlayer,
            placeholderImagePath,
            executionMode = "external-cli",
            args = BuildGenerateArgs(inputCsvPath, photosDirectory, outputDir, imageFormat, faceDetectionModel, portraitOnly, width, height, downloadOpenCvModels, onlyPlayer, placeholderImagePath)
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
            "--faceWidth", width.ToString(),
            "--faceHeight", height.ToString(),
            "--noCache"
        };

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
