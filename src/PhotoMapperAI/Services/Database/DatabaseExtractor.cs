using PhotoMapperAI.Models;
using CsvHelper.Configuration;
using CsvHelper;

namespace PhotoMapperAI.Services.Database;

/// <summary>
/// Service for extracting player data from databases to CSV format.
/// </summary>
public class DatabaseExtractor
{
    private readonly IImageProcessor _imageProcessor;

    public DatabaseExtractor(IImageProcessor? imageProcessor = null)
    {
        _imageProcessor = imageProcessor ?? new Services.Image.ImageProcessor();
    }

    /// <summary>
    /// Extracts player data from database using SQL query and exports to CSV.
    /// </summary>
    /// <param name="connectionString">Database connection string</param>
    /// <param name="sqlQuery">SQL query to execute</param>
    /// <param name="parameters">Query parameters (e.g., TeamId)</param>
    /// <param name="outputCsvPath">Path for output CSV file</param>
    /// <returns>Number of players extracted</returns>
    public async Task<int> ExtractPlayersToCsvAsync(
        string connectionString,
        string sqlQuery,
        Dictionary<string, object>? parameters,
        string outputCsvPath)
    {
        // For now, return synthetic data (TODO: Implement actual database access)
        // This would need:
        // - Microsoft.Data.SqlClient for SQL Server
        // - Npgsql for PostgreSQL
        // - Microsoft.Data.Sqlite for SQLite

        var players = GenerateSyntheticPlayers(parameters?.GetValueOrDefault("TeamId", 1).ToString());

        await WriteCsvAsync(players, outputCsvPath);

        return players.Count;
    }

    /// <summary>
    /// Reads player data from CSV file.
    /// </summary>
    /// <param name="csvPath">Path to CSV file</param>
    /// <returns>List of player records</returns>
    public async Task<List<PlayerRecord>> ReadCsvAsync(string csvPath)
    {
        return await Task.Run(() =>
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                Delimiter = ",",
                IgnoreBlankLines = true
            };

            using var reader = new CsvReader(csvPath, config);
            var records = new List<PlayerRecord>();

            foreach (var record in reader.GetRecords<PlayerRecordCsv>())
            {
                players.Add(new PlayerRecord
                {
                    PlayerId = record.PlayerId,
                    TeamId = record.TeamId,
                    FamilyName = record.FamilyName ?? string.Empty,
                    SurName = record.SurName ?? string.Empty,
                    ExternalId = record.ExternalId,
                    ValidMapping = !string.IsNullOrEmpty(record.ExternalId),
                    Confidence = !string.IsNullOrEmpty(record.ExternalId) ? 1.0 : 0.0
                });
            }

            return players;
        });
    }

    #region Private Methods

    /// <summary>
    /// Writes player records to CSV file.
    /// </summary>
    private static async Task WriteCsvAsync(List<PlayerRecord> players, string outputCsvPath)
    {
        await Task.Run(() =>
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true
            };

            using var writer = new CsvWriter(outputCsvPath, config);
            await writer.WriteRecordsAsync(players);
        });
    }

    /// <summary>
    /// Generates synthetic player data for testing.
    /// </summary>
    private static List<PlayerRecord> GenerateSyntheticPlayers(string teamIdStr)
    {
        var teamId = int.TryParse(teamIdStr, out var tid) ? tid : 1;
        var players = new List<PlayerRecord>();

        // Team 1: Small (3 players)
        if (teamId == 1)
        {
            players.AddRange(new[]
            {
                new PlayerRecord { PlayerId = 1, TeamId = 1, FamilyName = "Rodríguez", SurName = "Sánchez" },
                new PlayerRecord { PlayerId = 2, TeamId = 1, FamilyName = "Ramos", SurName = "Sergio" },
                new PlayerRecord { PlayerId = 3, TeamId = 1, FamilyName = "Iniesta", SurName = "Andrés" }
            });
        }
        // Team 2: Medium (10 players)
        else if (teamId == 2)
        {
            for (int i = 1; i <= 10; i++)
            {
                players.Add(new PlayerRecord
                {
                    PlayerId = i,
                    TeamId = 2,
                    FamilyName = $"Player{i}",
                    SurName = $"FamilyName{i}"
                });
            }
        }
        // Team 3: Large (25 players)
        else if (teamId == 3)
        {
            for (int i = 1; i <= 25; i++)
            {
                players.Add(new PlayerRecord
                {
                    PlayerId = i,
                    TeamId = 3,
                    FamilyName = $"Player{i}",
                    SurName = $"FamilyName{i}"
                });
            }
        }

        return players;
    }

    #endregion

    #region CSV Mapping Models

    /// <summary>
    /// CSV record format for reading.
    /// </summary>
    private class PlayerRecordCsv
    {
        public int PlayerId { get; set; }
        public int TeamId { get; set; }
        public string? FamilyName { get; set; }
        public string? SurName { get; set; }
        public string? ExternalId { get; set; }
    }

    #endregion
}
