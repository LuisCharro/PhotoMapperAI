using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PhotoMapperAI.Services.Database;

namespace PhotoMapperAI.UI.Execution;

public sealed class ExternalExtractCliRunner
{
    private readonly DatabaseExtractor _databaseExtractor = new();

    public sealed class ExtractCliResult
    {
        public int ExitCode { get; set; }
        public int PlayersExtracted { get; set; }
        public int TeamsExtracted { get; set; }
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
        _ = workingDirectory;
        _ = cancellationToken;

        try
        {
            log?.Report("Extract Command");
            log?.Report("================");
            log?.Report($"SQL File: {inputSqlPath}");
            log?.Report($"Connection String: {connectionStringPath}");
            log?.Report($"Team ID: {teamId}");
            log?.Report($"Output: {outputCsvPath}");
            log?.Report("");

            // Read SQL query
            var sqlQuery = await File.ReadAllTextAsync(inputSqlPath);

            // Read connection string
            var connectionString = await File.ReadAllTextAsync(connectionStringPath);

            // Build parameters for players extraction
            var parameters = new Dictionary<string, object>
            {
                { "TeamId", teamId }
            };

            // Extract players data
            log?.Report("Extracting player data...");
            var playerCount = await _databaseExtractor.ExtractPlayersToCsvAsync(
                connectionString,
                sqlQuery,
                parameters,
                outputCsvPath
            );

            log?.Report("");
            log?.Report($"✓ Extracted {playerCount} players to {Path.GetFileName(outputCsvPath)}");

            return new ExtractCliResult
            {
                ExitCode = 0,
                PlayersExtracted = playerCount,
                TeamsExtracted = 0,
                OutputCsvPath = outputCsvPath
            };
        }
        catch (Exception ex)
        {
            log?.Report($"✗ Error: {ex.Message}");
            return new ExtractCliResult
            {
                ExitCode = 1,
                PlayersExtracted = 0,
                TeamsExtracted = 0,
                OutputCsvPath = outputCsvPath
            };
        }
    }

    public async Task<ExtractCliResult> ExecuteTeamsAsync(
        string workingDirectory,
        string inputSqlPath,
        string connectionStringPath,
        string outputCsvPath,
        CancellationToken cancellationToken,
        IProgress<string>? log)
    {
        _ = workingDirectory;
        _ = cancellationToken;

        try
        {
            log?.Report("Extract Command");
            log?.Report("================");
            log?.Report($"SQL File: {inputSqlPath}");
            log?.Report($"Connection String: {connectionStringPath}");
            log?.Report($"Output: {outputCsvPath}");
            log?.Report($"Extract Teams: True");
            log?.Report("");

            // Read SQL query
            var sqlQuery = await File.ReadAllTextAsync(inputSqlPath);

            // Read connection string
            var connectionString = await File.ReadAllTextAsync(connectionStringPath);

            // Extract teams data
            log?.Report("Extracting team data...");
            var teamCount = await _databaseExtractor.ExtractTeamsToCsvAsync(
                connectionString,
                sqlQuery,
                outputCsvPath
            );

            log?.Report("");
            log?.Report($"✓ Extracted {teamCount} teams to {Path.GetFileName(outputCsvPath)}");

            return new ExtractCliResult
            {
                ExitCode = 0,
                PlayersExtracted = 0,
                TeamsExtracted = teamCount,
                OutputCsvPath = outputCsvPath
            };
        }
        catch (Exception ex)
        {
            log?.Report($"✗ Error: {ex.Message}");
            return new ExtractCliResult
            {
                ExitCode = 1,
                PlayersExtracted = 0,
                TeamsExtracted = 0,
                OutputCsvPath = outputCsvPath
            };
        }
    }
}
