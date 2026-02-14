using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PhotoMapperAI.UI.Execution;

public sealed class ExternalExtractCliRunner
{
    public sealed class ExtractCliResult
    {
        public int ExitCode { get; set; }
        public int PlayersExtracted { get; set; }
        public string OutputCsvPath { get; set; } = string.Empty;
    }

    public async Task<ExtractCliResult> ExecuteAsync(
        string workingDirectory,
        string inputSqlPath,
        string connectionStringPath,
        int teamId,
        string outputCsvPath,
        CancellationToken cancellationToken,
        IProgress<string>? log)
    {
        var args = new List<string>
        {
            "run", "--project", "src/PhotoMapperAI", "--", "extract",
            "--inputSqlPath", inputSqlPath,
            "--connectionStringPath", connectionStringPath,
            "--teamId", teamId.ToString(),
            "--outputName", outputCsvPath
        };

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

        var extracted = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await process.StandardOutput.ReadLineAsync();
            if (line == null)
                break;

            log?.Report(line);

            var m = Regex.Match(line, @"Extracted\s+(\d+)\s+players", RegexOptions.IgnoreCase);
            if (m.Success)
                extracted = int.Parse(m.Groups[1].Value);
        }

        var err = await process.StandardError.ReadToEndAsync();
        if (!string.IsNullOrWhiteSpace(err))
        {
            foreach (var line in err.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                log?.Report(line.TrimEnd('\r'));
        }

        await process.WaitForExitAsync(cancellationToken);

        return new ExtractCliResult
        {
            ExitCode = process.ExitCode,
            PlayersExtracted = extracted,
            OutputCsvPath = outputCsvPath
        };
    }
}
