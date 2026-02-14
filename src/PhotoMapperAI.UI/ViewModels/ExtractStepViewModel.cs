using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoMapperAI.UI.Execution;

namespace PhotoMapperAI.UI.ViewModels;

public partial class ExtractStepViewModel : ViewModelBase
{
    private readonly ExternalExtractCliRunner _extractRunner = new();
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

    public ObservableCollection<string> LogLines { get; } = new();

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

        LogLines.Clear();
        IsProcessing = true;
        IsComplete = false;
        ProcessingStatus = "Extracting player data...";

        try
        {
            if (string.IsNullOrWhiteSpace(OutputDirectory))
            {
                OutputDirectory = Directory.GetCurrentDirectory();
            }

            Directory.CreateDirectory(OutputDirectory);
            var outputCsvPath = Path.Combine(OutputDirectory, OutputFileName);
            OutputCsvPath = outputCsvPath;

            var log = new Progress<string>(AppendLog);

            var result = await _extractRunner.ExecuteAsync(
                Directory.GetCurrentDirectory(),
                SqlFilePath,
                ConnectionStringPath,
                TeamId,
                outputCsvPath,
                CancellationToken.None,
                log);

            if (result.ExitCode != 0)
            {
                ProcessingStatus = $"✗ Extract failed with exit code {result.ExitCode}";
                IsComplete = false;
                return;
            }

            PlayersExtracted = result.PlayersExtracted;
            ProcessingStatus = $"✓ Successfully extracted {PlayersExtracted} players to {result.OutputCsvPath}";
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

    private void AppendLog(string message)
    {
        if (LogLines.Count >= 200)
        {
            LogLines.RemoveAt(0);
        }

        LogLines.Add(message);
    }
}
