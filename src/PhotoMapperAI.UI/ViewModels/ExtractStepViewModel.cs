using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoMapperAI.UI.Execution;
using PhotoMapperAI.Models;
using PhotoMapperAI.Services.Database;

namespace PhotoMapperAI.UI.ViewModels;

public partial class ExtractStepViewModel : ViewModelBase
{
    private readonly ExternalExtractCliRunner _extractRunner = new();
    private readonly DatabaseExtractor _databaseExtractor = new();

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

    // Teams-related properties
    [ObservableProperty]
    private string _teamsSqlFilePath = string.Empty;

    [ObservableProperty]
    private string _teamsCsvPath = string.Empty;

    [ObservableProperty]
    private bool _useTeams;

    [ObservableProperty]
    private bool _isExtractingTeams;

    [ObservableProperty]
    private int _teamsExtracted;

    [ObservableProperty]
    private TeamRecord? _selectedTeam;

    [ObservableProperty]
    private bool _teamsLoaded;

    public ObservableCollection<string> LogLines { get; } = new();
    public ObservableCollection<TeamRecord> AvailableTeams { get; } = new();

    public List<string> DatabaseTypes { get; } = new()
    {
        "SqlServer",
        "MySQL",
        "PostgreSQL",
        "SQLite"
    };

    // Computed property for output filename when team is selected
    partial void OnSelectedTeamChanged(TeamRecord? value)
    {
        UpdateOutputFileName();
    }

    partial void OnUseTeamsChanged(bool value)
    {
        if (!value)
        {
            OutputFileName = "players.csv";
        }
        else
        {
            UpdateOutputFileName();
        }
    }

    private void UpdateOutputFileName()
    {
        if (UseTeams && SelectedTeam != null)
        {
            var safeTeamName = string.Join("_", SelectedTeam.TeamName.Split(Path.GetInvalidFileNameChars()));
            OutputFileName = $"players_{SelectedTeam.TeamId}_{safeTeamName}.csv";
        }
        else if (!UseTeams)
        {
            OutputFileName = "players.csv";
        }
    }

    [RelayCommand]
    private async Task BrowseSqlFile()
    {
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task BrowseConnectionStringFile()
    {
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task BrowseTeamsSqlFile()
    {
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task BrowseTeamsCsvFile()
    {
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task LoadTeamsFromCsv()
    {
        if (string.IsNullOrEmpty(TeamsCsvPath) || !File.Exists(TeamsCsvPath))
        {
            ProcessingStatus = "Please select a valid teams CSV file";
            return;
        }

        try
        {
            var teams = await _databaseExtractor.ReadTeamsCsvAsync(TeamsCsvPath);
            AvailableTeams.Clear();
            foreach (var team in teams)
            {
                AvailableTeams.Add(team);
            }
            TeamsLoaded = true;
            UseTeams = true;
            ProcessingStatus = $"Loaded {teams.Count} teams from {Path.GetFileName(TeamsCsvPath)}";
        }
        catch (Exception ex)
        {
            ProcessingStatus = $"Error loading teams: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExtractTeams()
    {
        if (string.IsNullOrEmpty(TeamsSqlFilePath) || string.IsNullOrEmpty(ConnectionStringPath))
        {
            ProcessingStatus = "Please select teams SQL file and connection string file";
            return;
        }

        LogLines.Clear();
        IsExtractingTeams = true;
        ProcessingStatus = "Extracting team data...";

        try
        {
            if (string.IsNullOrWhiteSpace(OutputDirectory))
            {
                OutputDirectory = Directory.GetCurrentDirectory();
            }

            Directory.CreateDirectory(OutputDirectory);
            var teamsOutputPath = Path.Combine(OutputDirectory, "teams.csv");

            var log = new Progress<string>(AppendLog);

            var result = await _extractRunner.ExecuteTeamsAsync(
                Directory.GetCurrentDirectory(),
                TeamsSqlFilePath,
                ConnectionStringPath,
                teamsOutputPath,
                CancellationToken.None,
                log);

            if (result.ExitCode != 0)
            {
                ProcessingStatus = $"✗ Teams extract failed with exit code {result.ExitCode}";
                return;
            }

            TeamsCsvPath = teamsOutputPath;
            TeamsExtracted = result.TeamsExtracted;

            // Load the extracted teams
            var teams = await _databaseExtractor.ReadTeamsCsvAsync(teamsOutputPath);
            AvailableTeams.Clear();
            foreach (var team in teams)
            {
                AvailableTeams.Add(team);
            }
            TeamsLoaded = true;
            UseTeams = true;

            ProcessingStatus = $"✓ Successfully extracted {TeamsExtracted} teams to teams.csv";
        }
        catch (Exception ex)
        {
            ProcessingStatus = $"✗ Error: {ex.Message}";
        }
        finally
        {
            IsExtractingTeams = false;
        }
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

            // Determine TeamId - use selected team if available, otherwise use manual input
            var teamIdToUse = UseTeams && SelectedTeam != null ? SelectedTeam.TeamId : TeamId;

            var result = await _extractRunner.ExecuteAsync(
                Directory.GetCurrentDirectory(),
                SqlFilePath,
                ConnectionStringPath,
                teamIdToUse,
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

    [RelayCommand]
    private void ClearTeamsSelection()
    {
        SelectedTeam = null;
        UseTeams = false;
        OutputFileName = "players.csv";
    }

    public async Task SaveTeamsToCsvAsync(string csvPath)
    {
        if (string.IsNullOrWhiteSpace(csvPath))
        {
            ProcessingStatus = "Error: CSV path is required.";
            return;
        }

        if (AvailableTeams.Count == 0)
        {
            ProcessingStatus = "Error: No teams to save. Load teams first.";
            return;
        }

        try
        {
            await DatabaseExtractor.WriteTeamsCsvAsync(AvailableTeams.ToList(), csvPath);
            ProcessingStatus = $"✓ Saved {AvailableTeams.Count} teams to {Path.GetFileName(csvPath)}";
        }
        catch (Exception ex)
        {
            ProcessingStatus = $"✗ Error saving teams to CSV: {ex.Message}";
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
