using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PhotoMapperAI.UI.Execution;

public sealed class ExternalMapCliRunner
{
    public sealed class MapCliResult
    {
        public int ExitCode { get; set; }
        public int PlayersProcessed { get; set; }
        public int PlayersMatched { get; set; }
        public string OutputCsvPath { get; set; } = string.Empty;
    }

    public async Task<MapCliResult> ExecuteAsync(
        string projectRootDirectory,
        string executionDirectory,
        string inputCsvPath,
        string photosDir,
        string? filenamePattern,
        string? photoManifest,
        string nameModel,
        double confidenceThreshold,
        bool useAi,
        bool aiSecondPass,
        bool aiOnly,
        string? openAiApiKey,
        string? anthropicApiKey,
        CancellationToken cancellationToken,
        IProgress<string>? log)
    {
        var projectPath = Path.Combine(projectRootDirectory, "src", "PhotoMapperAI");

        var args = new List<string>
        {
            "run", "--project", projectPath, "--", "map",
            "--inputCsvPath", inputCsvPath,
            "--photosDir", photosDir,
            "--nameModel", nameModel,
            "--confidenceThreshold", confidenceThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        if (!string.IsNullOrWhiteSpace(filenamePattern))
        {
            args.Add("--filenamePattern");
            args.Add(filenamePattern);
        }

        if (!string.IsNullOrWhiteSpace(photoManifest))
        {
            args.Add("--photoManifest");
            args.Add(photoManifest);
        }

        if (useAi)
            args.Add("--useAI");
        if (aiSecondPass)
            args.Add("--aiSecondPass");
        if (aiOnly)
            args.Add("--aiOnly");

        if (!string.IsNullOrWhiteSpace(openAiApiKey))
        {
            args.Add("--openaiApiKey");
            args.Add(openAiApiKey);
        }

        if (!string.IsNullOrWhiteSpace(anthropicApiKey))
        {
            args.Add("--anthropicApiKey");
            args.Add(anthropicApiKey);
        }

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = executionDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi };
        process.Start();

        var processed = 0;
        var matched = 0;
        var outputPath = string.Empty;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await process.StandardOutput.ReadLineAsync();
            if (line == null)
                break;

            log?.Report(line);

            var mm = Regex.Match(line, @"Matched\s+(\d+)\s*/\s*(\d+)\s*players", RegexOptions.IgnoreCase);
            if (mm.Success)
            {
                matched = int.Parse(mm.Groups[1].Value);
                processed = int.Parse(mm.Groups[2].Value);
            }

            var mo = Regex.Match(line, @"Results saved to\s+(.+)$", RegexOptions.IgnoreCase);
            if (mo.Success)
                outputPath = mo.Groups[1].Value.Trim();
        }

        var err = await process.StandardError.ReadToEndAsync();
        if (!string.IsNullOrWhiteSpace(err))
        {
            foreach (var line in err.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                log?.Report(line.TrimEnd('\r'));
        }

        await process.WaitForExitAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(outputPath) && !string.IsNullOrWhiteSpace(inputCsvPath))
        {
            var baseName = Path.GetFileNameWithoutExtension(inputCsvPath) ?? "players";
            outputPath = Path.Combine(executionDirectory, $"mapped_{baseName}.csv");
        }

        return new MapCliResult
        {
            ExitCode = process.ExitCode,
            PlayersProcessed = processed,
            PlayersMatched = matched,
            OutputCsvPath = outputPath
        };
    }
}
