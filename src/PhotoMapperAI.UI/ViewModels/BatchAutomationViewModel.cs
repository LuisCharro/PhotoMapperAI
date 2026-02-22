using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoMapperAI.Services.Database;
using PhotoMapperAI.UI.Execution;
using PhotoMapperAI.UI.Models;
using PhotoMapperAI.Models;

namespace PhotoMapperAI.UI.ViewModels;

public partial class BatchAutomationViewModel : ViewModelBase
{
    private readonly DatabaseExtractor _databaseExtractor;
    private readonly ExternalMapCliRunner _mapRunner;
    private readonly ExternalGenerateCliRunner _generateRunner;
    private CancellationTokenSource? _cancellationTokenSource;
    private BatchSessionState _sessionState = new();

    public BatchAutomationViewModel()
    {
        _databaseExtractor = new DatabaseExtractor();
        _mapRunner = new ExternalMapCliRunner();
        _generateRunner = new ExternalGenerateCliRunner();
    }

    #region Configuration Properties

    // Database Connection
    [ObservableProperty]
    private string _connectionString = string.Empty;

    [ObservableProperty]
    private string _teamsSqlPath = string.Empty;

    [ObservableProperty]
    private string _playersSqlPath = string.Empty;

    // Paths
    [ObservableProperty]
    private string _baseCsvDirectory = string.Empty;

    [ObservableProperty]
    private string _basePhotoDirectory = string.Empty;

    [ObservableProperty]
    private string _baseOutputDirectory = string.Empty;

    // Photo directory structure
    [ObservableProperty]
    private bool _useTeamPhotoSubdirectories = true;

    // Name Matching Settings
    [ObservableProperty]
    private string _nameMatchingModel = "qwen2.5:7b";

    [ObservableProperty]
    private double _nameMatchingThreshold = 0.8;

    [ObservableProperty]
    private bool _useAiMapping;

    [ObservableProperty]
    private bool _aiOnly;

    [ObservableProperty]
    private bool _aiSecondPass;

    // Face Detection Settings
    [ObservableProperty]
    private string _faceDetectionModel = "opencv-dnn";

    [ObservableProperty]
    private bool _downloadOpenCvModels;

    // Available face detection models for dropdown
    public string[] FaceDetectionModels { get; } = new[] { "opencv-dnn", "haar-cascade", "ollama" };

    // Size Settings
    [ObservableProperty]
    private string _sizeProfilePath = string.Empty;

    [ObservableProperty]
    private bool _generateAllSizes = true;

    [ObservableProperty]
    private int _defaultWidth = 200;

    [ObservableProperty]
    private int _defaultHeight = 300;

    // Crop Offset Settings
    [ObservableProperty]
    private double _cropOffsetX;

    [ObservableProperty]
    private double _cropOffsetY;

    // Output Settings
    [ObservableProperty]
    private string _imageFormat = "jpg";

    [ObservableProperty]
    private string _outputProfile = "none";

    #endregion

    #region Team List

    [ObservableProperty]
    private ObservableCollection<BatchTeamItem> _teams = new();

    [ObservableProperty]
    private BatchTeamItem? _selectedTeam;

    [ObservableProperty]
    private bool _isLoadingTeams;

    #endregion

    #region Execution State

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _processingStatus = string.Empty;

    [ObservableProperty]
    private int _teamsCompleted;

    [ObservableProperty]
    private int _teamsFailed;

    [ObservableProperty]
    private int _teamsSkipped;

    public ObservableCollection<string> LogLines { get; } = new();

    public bool CanStart => !IsProcessing && Teams.Count > 0;

    #endregion

    #region Commands

    [RelayCommand]
    private async Task LoadTeamsFromDatabase()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString) || string.IsNullOrWhiteSpace(TeamsSqlPath))
        {
            AppendLog("Error: Connection string and Teams SQL path are required.");
            return;
        }

        if (!File.Exists(TeamsSqlPath))
        {
            AppendLog($"Error: Teams SQL file not found: {TeamsSqlPath}");
            return;
        }

        IsLoadingTeams = true;
        Teams.Clear();

        try
        {
            var sql = await File.ReadAllTextAsync(TeamsSqlPath);
            var tempCsvPath = Path.Combine(Path.GetTempPath(), "teams_temp.csv");
            
            await _databaseExtractor.ExtractTeamsToCsvAsync(ConnectionString, sql, tempCsvPath);
            var teams = await _databaseExtractor.ReadTeamsCsvAsync(tempCsvPath);

            foreach (var team in teams)
            {
                var teamPhotoDir = UseTeamPhotoSubdirectories 
                    ? Path.Combine(BasePhotoDirectory, team.TeamName) 
                    : BasePhotoDirectory;
                
                var hasPhotoDir = !string.IsNullOrWhiteSpace(BasePhotoDirectory) && Directory.Exists(teamPhotoDir);
                
                Teams.Add(new BatchTeamItem
                {
                    TeamId = team.TeamId,
                    TeamName = team.TeamName,
                    Status = BatchTeamStatus.Pending,
                    HasPhotoDirectory = hasPhotoDir,
                    StatusMessage = hasPhotoDir ? "Ready" : "⚠ No photo directory"
                });
            }

            var teamsWithoutPhotos = Teams.Count(t => !t.HasPhotoDirectory);
            AppendLog($"Loaded {Teams.Count} teams from database.");
            if (teamsWithoutPhotos > 0)
            {
                AppendLog($"⚠ WARNING: {teamsWithoutPhotos} teams have no photo directory!");
            }
            
            // Clean up temp file
            if (File.Exists(tempCsvPath))
                File.Delete(tempCsvPath);
        }
        catch (Exception ex)
        {
            AppendLog($"Error loading teams: {ex.Message}");
        }
        finally
        {
            IsLoadingTeams = false;
            OnPropertyChanged(nameof(CanStart));
        }
    }

    public async Task LoadTeamsFromCsvFileAsync(string csvPath)
    {
        if (string.IsNullOrWhiteSpace(csvPath) || !File.Exists(csvPath))
        {
            AppendLog($"Error: Teams CSV file not found: {csvPath}");
            return;
        }

        IsLoadingTeams = true;
        Teams.Clear();

        try
        {
            var teams = await _databaseExtractor.ReadTeamsCsvAsync(csvPath);

            foreach (var team in teams)
            {
                var teamPhotoDir = UseTeamPhotoSubdirectories 
                    ? Path.Combine(BasePhotoDirectory, team.TeamName) 
                    : BasePhotoDirectory;
                
                var hasPhotoDir = !string.IsNullOrWhiteSpace(BasePhotoDirectory) && Directory.Exists(teamPhotoDir);
                
                Teams.Add(new BatchTeamItem
                {
                    TeamId = team.TeamId,
                    TeamName = team.TeamName,
                    Status = BatchTeamStatus.Pending,
                    HasPhotoDirectory = hasPhotoDir,
                    StatusMessage = hasPhotoDir ? "Ready" : "⚠ No photo directory"
                });
            }

            var teamsWithoutPhotos = Teams.Count(t => !t.HasPhotoDirectory);
            AppendLog($"Loaded {Teams.Count} teams from CSV.");
            if (teamsWithoutPhotos > 0)
            {
                AppendLog($"⚠ WARNING: {teamsWithoutPhotos} teams have no photo directory!");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Error loading teams from CSV: {ex.Message}");
        }
        finally
        {
            IsLoadingTeams = false;
            OnPropertyChanged(nameof(CanStart));
        }
    }
    
    [RelayCommand]
    private void ValidatePhotoDirectories()
    {
        if (string.IsNullOrWhiteSpace(BasePhotoDirectory))
        {
            AppendLog("Error: Photo directory not configured.");
            return;
        }
        
        var teamsWithoutPhotos = 0;
        foreach (var team in Teams)
        {
            var teamPhotoDir = UseTeamPhotoSubdirectories 
                ? Path.Combine(BasePhotoDirectory, team.TeamName) 
                : BasePhotoDirectory;
            
            team.HasPhotoDirectory = Directory.Exists(teamPhotoDir);
            if (!team.HasPhotoDirectory)
            {
                teamsWithoutPhotos++;
                team.StatusMessage = $"⚠ No photo directory: {teamPhotoDir}";
            }
            else
            {
                team.StatusMessage = "Ready";
            }
        }
        
        AppendLog($"Photo directory validation complete. {teamsWithoutPhotos} teams missing photos.");
    }

    [RelayCommand]
    private void ClearTeams()
    {
        Teams.Clear();
        TeamsCompleted = 0;
        TeamsFailed = 0;
        TeamsSkipped = 0;
        OnPropertyChanged(nameof(CanStart));
    }

    public async Task SaveTeamsToCsvAsync(string csvPath)
    {
        if (string.IsNullOrWhiteSpace(csvPath))
        {
            AppendLog("Error: CSV path is required.");
            return;
        }

        if (Teams.Count == 0)
        {
            AppendLog("Error: No teams to save. Load teams first.");
            return;
        }

        try
        {
            var teamRecords = Teams.Select(t => new TeamRecord
            {
                TeamId = t.TeamId,
                TeamName = t.TeamName
            }).ToList();

            await DatabaseExtractor.WriteTeamsCsvAsync(teamRecords, csvPath);
            AppendLog($"Saved {Teams.Count} teams to {csvPath}");
        }
        catch (Exception ex)
        {
            AppendLog($"Error saving teams to CSV: {ex.Message}");
        }
    }

    [RelayCommand]
    private void SelectAllTeams()
    {
        foreach (var team in Teams)
        {
            team.IsEnabled = true;
        }
    }

    [RelayCommand]
    private void DeselectAllTeams()
    {
        foreach (var team in Teams)
        {
            team.IsEnabled = false;
        }
    }

    [RelayCommand]
    private async Task StartBatch()
    {
        if (IsProcessing || Teams.Count == 0)
            return;

        // Initialize session state
        _sessionState = new BatchSessionState
        {
            ConnectionString = ConnectionString,
            TeamsSqlPath = TeamsSqlPath,
            PlayersSqlPath = PlayersSqlPath,
            BaseCsvDirectory = BaseCsvDirectory,
            BasePhotoDirectory = BasePhotoDirectory,
            BaseOutputDirectory = BaseOutputDirectory,
            UseTeamPhotoSubdirectories = UseTeamPhotoSubdirectories,
            NameMatchingModel = NameMatchingModel,
            NameMatchingThreshold = NameMatchingThreshold,
            UseAiMapping = UseAiMapping,
            AiOnly = AiOnly,
            AiSecondPass = AiSecondPass,
            FaceDetectionModel = FaceDetectionModel,
            DownloadOpenCvModels = DownloadOpenCvModels,
            SizeProfilePath = SizeProfilePath,
            GenerateAllSizes = GenerateAllSizes,
            DefaultWidth = DefaultWidth,
            DefaultHeight = DefaultHeight,
            ImageFormat = ImageFormat,
            OutputProfile = OutputProfile,
            TotalTeams = Teams.Count,
            Status = "Running"
        };

        _cancellationTokenSource = new CancellationTokenSource();
        IsProcessing = true;
        Progress = 0;
        TeamsCompleted = 0;
        TeamsFailed = 0;
        TeamsSkipped = 0;

        var totalTeams = Teams.Count;

        AppendLog($"Starting batch processing for {totalTeams} teams...");

        // Run the entire batch processing on a background thread to prevent UI freeze
        await Task.Run(async () =>
        {
            var processedTeams = 0;
            
            try
            {
                foreach (var team in Teams)
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        AppendLog("Batch processing cancelled.");
                        break;
                    }

                    // Skip disabled teams
                    if (!team.IsEnabled)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            team.Status = BatchTeamStatus.Skipped;
                            team.StatusMessage = "Disabled by user";
                        });
                        TeamsSkipped++;
                        processedTeams++;
                        UpdateProgress(processedTeams, totalTeams);
                        continue;
                    }

                    try
                    {
                        await ProcessTeamAsync(team, _cancellationTokenSource.Token);
                        TeamsCompleted++;
                    }
                    catch (OperationCanceledException)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            team.Status = BatchTeamStatus.Skipped;
                            team.StatusMessage = "Cancelled";
                        });
                        TeamsSkipped++;
                    }
                    catch (Exception ex)
                    {
                        var errorMessage = ex.Message;
                        var innerMessage = ex.InnerException?.Message;
                        var fullError = string.IsNullOrEmpty(innerMessage) 
                            ? errorMessage 
                            : $"{errorMessage} -> {innerMessage}";
                        
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            team.Status = BatchTeamStatus.Failed;
                            team.StatusMessage = fullError;
                        });
                        TeamsFailed++;
                        AppendLog($"[ERROR] Team {team.TeamName}: {fullError}");
                        AppendLog($"[ERROR] Stack trace: {ex.StackTrace}");
                        
                        // Add failed result to session
                        _sessionState.TeamResults.Add(new BatchTeamResult
                        {
                            TeamId = team.TeamId,
                            TeamName = team.TeamName,
                            Status = "Failed",
                            StatusMessage = fullError,
                            ErrorMessage = fullError,
                            CompletedAt = DateTime.UtcNow
                        });
                    }

                    processedTeams++;
                    UpdateProgress(processedTeams, totalTeams);
                }
            }
            finally
            {
                // Finalize and save session state (always save, even on cancellation)
                _sessionState.CompletedAt = DateTime.UtcNow;
                _sessionState.TeamsCompleted = TeamsCompleted;
                _sessionState.TeamsFailed = TeamsFailed;
                _sessionState.TeamsSkipped = TeamsSkipped;
                _sessionState.Status = _cancellationTokenSource?.Token.IsCancellationRequested == true 
                    ? "Cancelled" 
                    : "Completed";

                // Save session state to JSON
                try
                {
                    if (!string.IsNullOrWhiteSpace(BaseOutputDirectory))
                    {
                        Directory.CreateDirectory(BaseOutputDirectory);
                        var sessionPath = BatchSessionState.GetSessionPath(BaseOutputDirectory);
                        _sessionState.SaveAsync(sessionPath).Wait();
                        AppendLog($"Session state saved to: {sessionPath}");
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"Warning: Could not save session state: {ex.Message}");
                }

                AppendLog($"Batch processing complete. Completed: {TeamsCompleted}, Failed: {TeamsFailed}, Skipped: {TeamsSkipped}");
                
                // Update UI on the UI thread
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    IsProcessing = false;
                    OnPropertyChanged(nameof(CanStart));
                });
            }
        }, _cancellationTokenSource.Token);
    }
    
    private void UpdateProgress(int processedTeams, int totalTeams)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Progress = (double)processedTeams / totalTeams * 100;
            ProcessingStatus = $"Processed {processedTeams}/{totalTeams} teams (Completed: {TeamsCompleted}, Failed: {TeamsFailed}, Skipped: {TeamsSkipped})";
        });
    }

    [RelayCommand]
    private void CancelBatch()
    {
        _cancellationTokenSource?.Cancel();
        ProcessingStatus = "Cancelling batch processing...";
    }

    #endregion

    #region Private Methods

    private async Task ProcessTeamAsync(BatchTeamItem team, CancellationToken cancellationToken)
    {
        var teamCsvDir = Path.Combine(BaseCsvDirectory, team.TeamName);
        // Use team subdirectory if enabled, otherwise use base photo directory directly
        var teamPhotoDir = UseTeamPhotoSubdirectories 
            ? Path.Combine(BasePhotoDirectory, team.TeamName) 
            : BasePhotoDirectory;
        var teamOutputDir = Path.Combine(BaseOutputDirectory, team.TeamName);

        Directory.CreateDirectory(teamCsvDir);
        Directory.CreateDirectory(teamOutputDir);

        var csvPath = Path.Combine(teamCsvDir, $"{team.TeamName}.csv");
        var teamResult = new BatchTeamResult
        {
            TeamId = team.TeamId,
            TeamName = team.TeamName,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // Step 1: Extract players
            UpdateTeamStatus(team, BatchTeamStatus.Extracting, "Extracting players...");
            AppendLog($"[EXTRACT] {team.TeamName}: Extracting players...");

            if (File.Exists(PlayersSqlPath))
            {
                var playersSql = await File.ReadAllTextAsync(PlayersSqlPath, cancellationToken);
                playersSql = playersSql.Replace("{TeamId}", team.TeamId.ToString());
                
                var parameters = new Dictionary<string, object>
                {
                    { "TeamId", team.TeamId }
                };
                
                var playerCount = await _databaseExtractor.ExtractPlayersToCsvAsync(
                    ConnectionString, playersSql, parameters, csvPath);
                
                UpdateTeamProperty(team, t => t.PlayersExtracted = playerCount);
                UpdateTeamProperty(team, t => t.CsvPath = csvPath);
                teamResult.PlayersExtracted = playerCount;
                teamResult.CsvPath = csvPath;
                AppendLog($"[EXTRACT] {team.TeamName}: Extracted {playerCount} players.");
            }
            else
            {
                throw new FileNotFoundException($"Players SQL file not found: {PlayersSqlPath}");
            }

            // Check if photo directory exists (after extraction)
            if (!Directory.Exists(teamPhotoDir))
            {
                AppendLog($"[SKIP] {team.TeamName}: Photo directory not found: {teamPhotoDir}");
                UpdateTeamStatus(team, BatchTeamStatus.Skipped, $"Photo directory not found: {teamPhotoDir}");
                TeamsSkipped++;
                teamResult.Status = "Skipped";
                teamResult.StatusMessage = $"Photo directory not found: {teamPhotoDir}";
                teamResult.CompletedAt = DateTime.UtcNow;
                _sessionState.TeamResults.Add(teamResult);
                return;
            }

            // Step 2: Map players
            UpdateTeamStatus(team, BatchTeamStatus.Mapping, "Mapping players...");
            AppendLog($"[MAP] {team.TeamName}: Mapping players...");

            try
            {
                var mapResult = await _mapRunner.ExecuteAsync(
                    Directory.GetCurrentDirectory(),
                    teamCsvDir,
                    csvPath,
                    teamPhotoDir,
                    filenamePattern: null,
                    photoManifest: null,
                    nameModel: NameMatchingModel,
                    confidenceThreshold: NameMatchingThreshold,
                    useAi: UseAiMapping,
                    aiSecondPass: AiSecondPass,
                    aiOnly: AiOnly,
                    openAiApiKey: null,
                    anthropicApiKey: null,
                    cancellationToken,
                    new Progress<string>(msg => AppendLog($"[MAP] {team.TeamName}: {msg}")));

                if (mapResult.ExitCode != 0)
                {
                    throw new Exception($"Map failed with exit code {mapResult.ExitCode}");
                }

                UpdateTeamProperty(team, t => t.PlayersMapped = mapResult.PlayersMatched);
                teamResult.PlayersMapped = mapResult.PlayersMatched;
                AppendLog($"[MAP] {team.TeamName}: Mapped {mapResult.PlayersMatched} players.");

                // Use the mapped CSV for generation (contains ExternalId)
                var mappedCsvPath = mapResult.OutputCsvPath;
                if (string.IsNullOrWhiteSpace(mappedCsvPath) || !File.Exists(mappedCsvPath))
                {
                    mappedCsvPath = csvPath; // Fallback to original if mapped not found
                }
                teamResult.MappedCsvPath = mappedCsvPath;
            }
            catch (Exception ex)
            {
                AppendLog($"[ERROR] {team.TeamName}: Map step failed - {ex.Message}");
                throw;
            }

            // Step 3: Generate photos
            UpdateTeamStatus(team, BatchTeamStatus.Generating, "Generating photos...");
            AppendLog($"[GENERATE] {team.TeamName}: Generating photos...");

            try
            {
                var cropOffset = new CropOffsetPreset
                {
                    Name = "batch",
                    HorizontalPercent = CropOffsetX,
                    VerticalPercent = CropOffsetY
                };

                var generateResult = await _generateRunner.ExecuteAsync(
                    Directory.GetCurrentDirectory(),
                    teamResult.MappedCsvPath ?? csvPath,  // Use mapped CSV with ExternalId
                    teamPhotoDir,
                    teamOutputDir,
                    ImageFormat,
                    FaceDetectionModel,
                    false,
                    DefaultWidth,
                    DefaultHeight,
                    !string.IsNullOrWhiteSpace(SizeProfilePath) ? SizeProfilePath : null,
                    GenerateAllSizes,
                    false,
                    DownloadOpenCvModels,
                    null,
                    null,
                    cropOffset,
                    cancellationToken,
                    new Progress<string>(msg => AppendLog($"[GENERATE] {team.TeamName}: {msg}")));

                if (generateResult.ExitCode != 0)
                {
                    throw new Exception($"Generate failed with exit code {generateResult.ExitCode}");
                }

                UpdateTeamProperty(team, t => t.PhotosGenerated = generateResult.PortraitsGenerated);
                UpdateTeamProperty(team, t => t.PhotoPath = teamOutputDir);
                UpdateTeamStatus(team, BatchTeamStatus.Completed, $"Completed: {generateResult.PortraitsGenerated} photos generated");
                AppendLog($"[GENERATE] {team.TeamName}: Generated {generateResult.PortraitsGenerated} photos.");

                // Update team result and add to session
                teamResult.PhotosGenerated = generateResult.PortraitsGenerated;
                teamResult.PhotoPath = teamOutputDir;
                teamResult.Status = "Completed";
                teamResult.StatusMessage = $"Completed: {generateResult.PortraitsGenerated} photos generated";
                teamResult.CompletedAt = DateTime.UtcNow;
                _sessionState.TeamResults.Add(teamResult);
            }
            catch (Exception ex)
            {
                AppendLog($"[ERROR] {team.TeamName}: Generate step failed - {ex.Message}");
                throw;
            }
        }
        catch (Exception ex)
        {
            AppendLog($"[ERROR] {team.TeamName}: ProcessTeamAsync failed - {ex.Message}");
            AppendLog($"[ERROR] Stack trace: {ex.StackTrace}");
            throw;
        }
    }
    
    private void UpdateTeamStatus(BatchTeamItem team, BatchTeamStatus status, string message)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            team.Status = status;
            team.StatusMessage = message;
        });
    }
    
    private void UpdateTeamProperty(BatchTeamItem team, Action<BatchTeamItem> updateAction)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => updateAction(team));
    }

    private void AppendLog(string message)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            LogLines.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        });
    }

    #endregion
}
