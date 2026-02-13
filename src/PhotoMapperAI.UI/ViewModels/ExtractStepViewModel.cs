using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PhotoMapperAI.UI.ViewModels;

public partial class ExtractStepViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _sqlFilePath = string.Empty;

    [ObservableProperty]
    private string _connectionStringPath = string.Empty;

    [ObservableProperty]
    private int _teamId;

    [ObservableProperty]
    private string _outputFileName = "players.csv";

    [ObservableProperty]
    private string _outputDirectory = Directory.GetCurrentDirectory();

    [ObservableProperty]
    private string _outputCsvPath = string.Empty;

    [ObservableProperty]
    private string _databaseType = "SqlServer";

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _processingStatus = string.Empty;

    [ObservableProperty]
    private int _playersExtracted;

    [ObservableProperty]
    private bool _isComplete;

    public List<string> DatabaseTypes { get; } = new()
    {
        "SqlServer",
        "MySQL",
        "PostgreSQL",
        "SQLite"
    };

    [RelayCommand]
    private async Task BrowseSqlFile()
    {
        // This will be handled by the view with file picker
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task BrowseConnectionStringFile()
    {
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ExecuteExtract()
    {
        if (string.IsNullOrEmpty(SqlFilePath) || string.IsNullOrEmpty(ConnectionStringPath))
        {
            ProcessingStatus = "Please select SQL file and connection string file";
            return;
        }

        IsProcessing = true;
        IsComplete = false;
        ProcessingStatus = "Extracting player data...";

        try
        {
            // Read SQL query
            var sqlQuery = await File.ReadAllTextAsync(SqlFilePath);

            // Read connection string
            var connectionString = await File.ReadAllTextAsync(ConnectionStringPath);

            // Build parameters
            var parameters = new Dictionary<string, object>
            {
                { "TeamId", TeamId }
            };

            // Create database extractor
            var extractor = new Services.Database.DatabaseExtractor();

            // Determine output path
            if (string.IsNullOrWhiteSpace(OutputDirectory))
            {
                OutputDirectory = Directory.GetCurrentDirectory();
            }

            Directory.CreateDirectory(OutputDirectory);
            var outputCsvPath = Path.Combine(OutputDirectory, OutputFileName);
            OutputCsvPath = outputCsvPath;

            // Extract data
            PlayersExtracted = await extractor.ExtractPlayersToCsvAsync(
                connectionString,
                sqlQuery,
                parameters,
                outputCsvPath
            );

            ProcessingStatus = $"✓ Successfully extracted {PlayersExtracted} players to {outputCsvPath}";
            IsComplete = true;
        }
        catch (Exception ex)
        {
            ProcessingStatus = $"✗ Error: {ex.Message}";
            IsComplete = false;
        }
        finally
        {
            IsProcessing = false;
        }
    }
}
