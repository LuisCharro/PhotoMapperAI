using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoMapperAI.Services.AI;
using PhotoMapperAI.Services.Database;
using PhotoMapperAI.Services.Diagnostics;
using PhotoMapperAI.UI.Configuration;
using PhotoMapperAI.UI.Execution;
using PhotoMapperAI.UI.Models;
using PhotoMapperAI.Models;

namespace PhotoMapperAI.UI.ViewModels;

public partial class BatchAutomationViewModel : ViewModelBase
{
    private const double MinConfidenceThreshold = 0.8;
    private readonly DatabaseExtractor _databaseExtractor;
    private readonly ExternalMapCliRunner _mapRunner;
    private readonly ExternalGenerateCliRunner _generateRunner;
    private CancellationTokenSource? _cancellationTokenSource;
    private BatchSessionState _sessionState = new();

    private static readonly string[] DefaultLocalNameModels =
    {
        "qwen2.5:7b",
        "qwen2.5-coder:7b-instruct-q4_K_M",
        "qwen3:8b",
        "llava:7b"
    };
    private static readonly string[] KnownCloudNameModels =
    {
        "gemini-3-flash-preview:cloud",
        "qwen3-coder-next:cloud",
        "kimi-k2.5:cloud",
        "glm-4.7:cloud",
        "minimax-m2:cloud",
        "qwen3-coder:480b-cloud"
    };
    private static readonly string[] DefaultPaidNameModels =
    {
        "openai:gpt-4.1",
        "openai:gpt-4o",
        "openai:o3-mini",
        "anthropic:claude-3-5-sonnet"
    };

    private static readonly string[] ConfiguredPaidNameModels =
        UiModelConfigLoader.Load().MapPaidModels.ToArray();

    public BatchAutomationViewModel()
    {
        _databaseExtractor = new DatabaseExtractor();
        _mapRunner = new ExternalMapCliRunner();
        _generateRunner = new ExternalGenerateCliRunner();

        var defaultProfile = ResolveDefaultSizeProfilePath();
        if (!string.IsNullOrWhiteSpace(defaultProfile))
        {
            SizeProfilePath = defaultProfile;
        }
        
        SeedNameModelList();
        LoadCropOffsetPresets();
        LoadFilenamePatternPresets();
        _ = RefreshNameModelsAsync(showStatus: false);
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
    private double _nameMatchingThreshold = MinConfidenceThreshold;

    [ObservableProperty]
    private bool _useAiMapping;

    [ObservableProperty]
    private bool _aiOnly;

    [ObservableProperty]
    private bool _aiSecondPass = true;

    [ObservableProperty]
    private int _selectedModelTierIndex;

    [ObservableProperty]
    private bool _isCheckingModel;

    [ObservableProperty]
    private string _modelDiagnosticStatus = string.Empty;

    public ObservableCollection<string> LocalNameModels { get; } = new();
    public ObservableCollection<string> FreeTierNameModels { get; } = new();
    public ObservableCollection<string> PaidNameModels { get; } = new();

    // Face Detection Settings
    [ObservableProperty]
    private string _faceDetectionModel = "opencv-dnn";

    [ObservableProperty]
    private bool _downloadOpenCvModels;

    [ObservableProperty]
    private int _selectedFaceModelTierIndex;

    public ObservableCollection<string> RecommendedFaceDetectionModels { get; } = new() { "opencv-dnn" };
    public ObservableCollection<string> LocalVisionFaceDetectionModels { get; } = new() { "llava:7b", "qwen3-vl" };
    public ObservableCollection<string> AdvancedFaceDetectionModels { get; } = new() { "yolov8-face", "haar-cascade", "center" };

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

    [ObservableProperty]
    private CropOffsetPreset? _selectedCropOffsetPreset;

    public ObservableCollection<CropOffsetPreset> CropOffsetPresets { get; } = new();

    // Output Settings
    [ObservableProperty]
    private string _imageFormat = "jpg";

    [ObservableProperty]
    private string _outputProfile = "none";

    // Photo Filename Parsing
    [ObservableProperty]
    private string _filenamePattern = string.Empty;

    [ObservableProperty]
    private FilenamePatternPreset? _selectedFilenamePatternPreset;

    [ObservableProperty]
    private string _filenamePatternStatus = string.Empty;

    public ObservableCollection<FilenamePatternPreset> FilenamePatternPresets { get; } = new();

    [ObservableProperty]
    private bool _usePhotoManifest;

    [ObservableProperty]
    private string _photoManifestPath = string.Empty;

    #endregion

    #region Team List

    [ObservableProperty]
    private ObservableCollection<BatchTeamItem> _teams = new();

    [ObservableProperty]
    private BatchTeamItem? _selectedTeam;

    [ObservableProperty]
    private bool _isLoadingTeams;

    [ObservableProperty]
    private ObservableCollection<MissingTeamFolderItem> _missingPhotoTeams = new();

    [ObservableProperty]
    private MissingTeamFolderItem? _selectedMissingPhotoTeam;

    [ObservableProperty]
    private ObservableCollection<string> _availablePhotoFolders = new();

    [ObservableProperty]
    private string? _selectedPhotoFolder;

    [ObservableProperty]
    private string _missingPhotoStatus = string.Empty;

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

    [ObservableProperty]
    private ObservableCollection<BatchIssueSummaryItem> _errorSummaryItems = new();

    [ObservableProperty]
    private bool _hasErrorSummary;

    [ObservableProperty]
    private string _errorSummaryTitle = "Issues: none";

    public ObservableCollection<string> LogLines { get; } = new();

    public bool CanStart => !IsProcessing && Teams.Count > 0;

    public bool CanRenameMissingPhotoFolder =>
        !IsProcessing &&
        SelectedMissingPhotoTeam != null &&
        !string.IsNullOrWhiteSpace(SelectedPhotoFolder);

    #endregion

    #region Commands

    [RelayCommand]
    private async Task RefreshNameModels()
    {
        await RefreshNameModelsAsync(showStatus: true);
    }

    [RelayCommand]
    private async Task CheckNameModel()
    {
        if (!UseAiMapping)
        {
            ModelDiagnosticStatus = "Enable AI mapping to check models.";
            return;
        }

        if (IsProcessing)
            return;

        IsCheckingModel = true;
        ModelDiagnosticStatus = $"Checking model '{NameMatchingModel}'...";

        try
        {
            if (IsOpenAiModel(NameMatchingModel))
            {
                var keyPresent = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
                ModelDiagnosticStatus = keyPresent
                    ? "✓ OpenAI API key available (environment variable)."
                    : "✗ OpenAI API key is missing (OPENAI_API_KEY).";
                return;
            }

            if (IsAnthropicModel(NameMatchingModel))
            {
                var keyPresent = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));
                ModelDiagnosticStatus = keyPresent
                    ? "✓ Anthropic API key available (environment variable)."
                    : "✗ Anthropic API key is missing (ANTHROPIC_API_KEY).";
                return;
            }

            var client = new OllamaClient();
            var available = await client.IsAvailableAsync();
            if (!available)
            {
                ModelDiagnosticStatus = "✗ Ollama server is not reachable (http://localhost:11434)";
                return;
            }

            if (IsFreeTierModel(NameMatchingModel))
            {
                ModelDiagnosticStatus = $"ℹ Free-tier cloud model selected: {NameMatchingModel}.";
                return;
            }

            var models = await client.GetAvailableModelsAsync();
            var exists = models.Any(m => string.Equals(m, NameMatchingModel, StringComparison.OrdinalIgnoreCase));
            ModelDiagnosticStatus = exists
                ? $"✓ Model available: {NameMatchingModel}"
                : $"✗ Model not found in Ollama: {NameMatchingModel}";
        }
        catch (Exception ex)
        {
            ModelDiagnosticStatus = $"✗ Model check failed: {ex.Message}";
        }
        finally
        {
            IsCheckingModel = false;
        }
    }

    [RelayCommand]
    private async Task CheckFaceModel()
    {
        if (IsProcessing)
            return;

        IsCheckingModel = true;
        ModelDiagnosticStatus = $"Checking face detection model '{FaceDetectionModel}'...";

        try
        {
            var preflight = await PreflightChecker.CheckGenerateAsync(FaceDetectionModel, DownloadOpenCvModels);
            if (!preflight.IsOk)
            {
                ModelDiagnosticStatus = preflight.BuildMessage();
                return;
            }

            var warningMessage = preflight.BuildWarningMessage();
            ModelDiagnosticStatus = string.IsNullOrWhiteSpace(warningMessage)
                ? $"✓ Face detection model ready: {FaceDetectionModel}"
                : $"✓ Face detection model ready: {FaceDetectionModel}\n{warningMessage}";
        }
        catch (Exception ex)
        {
            ModelDiagnosticStatus = $"✗ Model check failed: {ex.Message}";
        }
        finally
        {
            IsCheckingModel = false;
        }
    }

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
        RefreshMissingPhotoFolders();
    }

    [RelayCommand]
    private void ClearTeams()
    {
        Teams.Clear();
        TeamsCompleted = 0;
        TeamsFailed = 0;
        TeamsSkipped = 0;
        ResetErrorSummary();
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
            NameMatchingThreshold = NameMatchingThreshold < MinConfidenceThreshold ? MinConfidenceThreshold : NameMatchingThreshold,
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

        if (NameMatchingThreshold < MinConfidenceThreshold)
        {
            NameMatchingThreshold = MinConfidenceThreshold;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        IsProcessing = true;
        Progress = 0;
        TeamsCompleted = 0;
        TeamsFailed = 0;
        TeamsSkipped = 0;
        ResetErrorSummary();

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
            UpdateErrorSummary();
        });
    }

    [RelayCommand]
    private void CancelBatch()
    {
        _cancellationTokenSource?.Cancel();
        ProcessingStatus = "Cancelling batch processing...";
    }

    [RelayCommand]
    private void RefreshMissingPhotoFolders()
    {
        MissingPhotoTeams.Clear();
        AvailablePhotoFolders.Clear();
        SelectedMissingPhotoTeam = null;
        SelectedPhotoFolder = null;

        if (string.IsNullOrWhiteSpace(BasePhotoDirectory))
        {
            MissingPhotoStatus = "Photo directory not configured.";
            return;
        }

        if (!Directory.Exists(BasePhotoDirectory))
        {
            MissingPhotoStatus = $"Photo directory not found: {BasePhotoDirectory}";
            return;
        }

        if (!UseTeamPhotoSubdirectories)
        {
            MissingPhotoStatus = "Team subdirectories are disabled; no per-team folders required.";
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(BasePhotoDirectory))
        {
            var name = Path.GetFileName(directory);
            if (!string.IsNullOrWhiteSpace(name))
            {
                AvailablePhotoFolders.Add(name);
            }
        }

        var missingCount = 0;
        foreach (var team in Teams)
        {
            var teamPhotoDir = Path.Combine(BasePhotoDirectory, team.TeamName);
            var hasPhotoDir = Directory.Exists(teamPhotoDir);
            team.HasPhotoDirectory = hasPhotoDir;
            team.StatusMessage = hasPhotoDir ? "Ready" : "No photo directory";

            if (!hasPhotoDir)
            {
                MissingPhotoTeams.Add(new MissingTeamFolderItem
                {
                    TeamId = team.TeamId,
                    TeamName = team.TeamName
                });
                missingCount++;
            }
        }

        MissingPhotoStatus = missingCount == 0
            ? "All teams have photo folders."
            : $"Missing folders: {missingCount}.";
    }

    [RelayCommand]
    private void RenameMissingPhotoFolder()
    {
        if (string.IsNullOrWhiteSpace(BasePhotoDirectory))
        {
            MissingPhotoStatus = "Photo directory not configured.";
            return;
        }

        if (SelectedMissingPhotoTeam == null || string.IsNullOrWhiteSpace(SelectedPhotoFolder))
        {
            MissingPhotoStatus = "Select a missing team and an existing folder.";
            return;
        }

        var sourceDir = Path.Combine(BasePhotoDirectory, SelectedPhotoFolder);
        var targetDir = Path.Combine(BasePhotoDirectory, SelectedMissingPhotoTeam.TeamName);

        if (!Directory.Exists(sourceDir))
        {
            MissingPhotoStatus = $"Source folder not found: {sourceDir}";
            return;
        }

        if (Directory.Exists(targetDir))
        {
            MissingPhotoStatus = $"Target folder already exists: {targetDir}";
            return;
        }

        try
        {
            Directory.Move(sourceDir, targetDir);
            AppendLog($"Renamed photo folder '{SelectedPhotoFolder}' to '{SelectedMissingPhotoTeam.TeamName}'.");
            MissingPhotoStatus = $"Renamed '{SelectedPhotoFolder}' to '{SelectedMissingPhotoTeam.TeamName}'.";
            RefreshMissingPhotoFolders();
        }
        catch (Exception ex)
        {
            MissingPhotoStatus = $"Rename failed: {ex.Message}";
        }
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
                    filenamePattern: string.IsNullOrWhiteSpace(FilenamePattern) ? null : FilenamePattern,
                    photoManifest: UsePhotoManifest ? PhotoManifestPath : null,
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
                    Name = SelectedCropOffsetPreset?.Name ?? "batch",
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
                var unmappedCount = GetUnmappedCount(teamResult.PlayersExtracted, teamResult.PlayersMapped);
                var completedMessage = unmappedCount > 0
                    ? $"Completed: {generateResult.PortraitsGenerated} photos generated (Unmapped: {unmappedCount})"
                    : $"Completed: {generateResult.PortraitsGenerated} photos generated";
                UpdateTeamStatus(team, BatchTeamStatus.Completed, completedMessage);
                AppendLog($"[GENERATE] {team.TeamName}: Generated {generateResult.PortraitsGenerated} photos.");

                // Update team result and add to session
                teamResult.PhotosGenerated = generateResult.PortraitsGenerated;
                teamResult.PhotoPath = teamOutputDir;
                teamResult.Status = "Completed";
                teamResult.StatusMessage = completedMessage;
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
            UpdateErrorSummary();
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

    private void ResetErrorSummary()
    {
        ErrorSummaryItems.Clear();
        HasErrorSummary = false;
        ErrorSummaryTitle = "Issues: none";
    }

    private static int GetUnmappedCount(int playersExtracted, int playersMapped)
        => Math.Max(0, playersExtracted - playersMapped);

    private void UpdateErrorSummary()
    {
        var items = Teams
            .Select(team =>
            {
                var unmappedCount = GetUnmappedCount(team.PlayersExtracted, team.PlayersMapped);
                var hasUnmapped = team.Status == BatchTeamStatus.Completed && unmappedCount > 0;
                var isIssue = team.Status is BatchTeamStatus.Failed or BatchTeamStatus.Skipped || hasUnmapped;
                if (!isIssue)
                {
                    return null;
                }

                var message = team.Status == BatchTeamStatus.Completed
                    ? $"Unmapped players: {unmappedCount}"
                    : team.StatusMessage;

                var isCritical = team.Status == BatchTeamStatus.Failed || hasUnmapped;

                return new BatchIssueSummaryItem
                {
                    TeamName = team.TeamName,
                    Status = team.Status.ToString(),
                    Message = message,
                    IsCritical = isCritical
                };
            })
            .Where(item => item != null)
            .Select(item => item!)
            .ToList();

        ErrorSummaryItems.Clear();
        foreach (var item in items)
        {
            ErrorSummaryItems.Add(item);
        }

        var failedCount = items.Count(item => item.Status == BatchTeamStatus.Failed.ToString());
        var skippedCount = items.Count(item => item.Status == BatchTeamStatus.Skipped.ToString());
        var unmappedCount = items.Count(item => item.Status == BatchTeamStatus.Completed.ToString());

        HasErrorSummary = items.Count > 0;
        ErrorSummaryTitle = HasErrorSummary
            ? $"Issues: {failedCount} failed, {skippedCount} skipped, {unmappedCount} unmapped"
            : "Issues: none";
    }

    #endregion

    #region Model Management

    private async Task RefreshNameModelsAsync(bool showStatus)
    {
        if (showStatus)
        {
            ModelDiagnosticStatus = "Refreshing models from Ollama...";
        }

        try
        {
            var client = new OllamaClient();
            var available = await client.IsAvailableAsync();

            if (!available)
            {
                if (showStatus)
                {
                    ModelDiagnosticStatus = "Ollama server is not reachable. Showing fallback model list.";
                }

                SeedNameModelList();
                return;
            }

            var localModels = await client.GetAvailableModelsAsync();
            RebuildNameModelLists(localModels);

            if (showStatus)
            {
                var total = LocalNameModels.Count + FreeTierNameModels.Count + PaidNameModels.Count;
                ModelDiagnosticStatus = $"✓ Loaded {total} models (Local {LocalNameModels.Count}, Free {FreeTierNameModels.Count}, Paid {PaidNameModels.Count})";
            }
        }
        catch (Exception ex)
        {
            SeedNameModelList();
            if (showStatus)
            {
                ModelDiagnosticStatus = $"✗ Failed to refresh model list: {ex.Message}";
            }
        }
    }

    private void SeedNameModelList()
    {
        RebuildNameModelLists(Array.Empty<string>());
    }

    private static string? ResolveDefaultSizeProfilePath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "size_profiles.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "size_profiles.json"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "size_profiles.json")),
            Path.Combine(Directory.GetCurrentDirectory(), "samples", "size_profiles.default.json"),
            Path.Combine(AppContext.BaseDirectory, "samples", "size_profiles.default.json"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "size_profiles.default.json")),
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private void RebuildNameModelLists(IEnumerable<string> discoveredModels)
    {
        var previousSelection = NameMatchingModel;
        var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var model in DefaultLocalNameModels)
            merged.Add(model);

        foreach (var model in discoveredModels.Where(m => !string.IsNullOrWhiteSpace(m)))
            merged.Add(model.Trim());

        foreach (var model in KnownCloudNameModels)
            merged.Add(model);

        var paidModels = ConfiguredPaidNameModels.Length > 0
            ? ConfiguredPaidNameModels
            : DefaultPaidNameModels;

        foreach (var model in paidModels)
            merged.Add(model);

        var local = new List<string>();
        var freeTier = new List<string>();
        var paid = new List<string>();

        foreach (var model in merged)
        {
            if (IsPaidModel(model))
            {
                paid.Add(model);
            }
            else if (IsFreeTierModel(model))
            {
                freeTier.Add(model);
            }
            else
            {
                local.Add(model);
            }
        }

        local.Sort(StringComparer.OrdinalIgnoreCase);
        freeTier.Sort(StringComparer.OrdinalIgnoreCase);

        var paidOrdered = new List<string>();
        foreach (var model in paidModels)
        {
            var found = paid.FirstOrDefault(x => string.Equals(x, model, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(found))
                paidOrdered.Add(found);
        }

        foreach (var model in paid)
        {
            if (!paidOrdered.Any(x => string.Equals(x, model, StringComparison.OrdinalIgnoreCase)))
                paidOrdered.Add(model);
        }

        paid = paidOrdered;

        LocalNameModels.Clear();
        foreach (var model in local)
            LocalNameModels.Add(model);

        FreeTierNameModels.Clear();
        foreach (var model in freeTier)
            FreeTierNameModels.Add(model);

        PaidNameModels.Clear();
        foreach (var model in paid)
            PaidNameModels.Add(model);

        if (!string.IsNullOrWhiteSpace(previousSelection) &&
            (LocalNameModels.Any(m => string.Equals(m, previousSelection, StringComparison.OrdinalIgnoreCase)) ||
             FreeTierNameModels.Any(m => string.Equals(m, previousSelection, StringComparison.OrdinalIgnoreCase)) ||
             PaidNameModels.Any(m => string.Equals(m, previousSelection, StringComparison.OrdinalIgnoreCase))))
        {
            NameMatchingModel = previousSelection;
            return;
        }

        if (LocalNameModels.Contains("qwen2.5:7b"))
        {
            NameMatchingModel = "qwen2.5:7b";
            return;
        }

        NameMatchingModel = LocalNameModels.FirstOrDefault()
                    ?? FreeTierNameModels.FirstOrDefault()
                    ?? PaidNameModels.FirstOrDefault()
                    ?? "qwen2.5:7b";
    }

    private static bool IsCloudModel(string modelName)
        => !string.IsNullOrWhiteSpace(modelName) &&
           (modelName.EndsWith(":cloud", StringComparison.OrdinalIgnoreCase) ||
            modelName.EndsWith("-cloud", StringComparison.OrdinalIgnoreCase));

    private static bool IsFreeTierModel(string modelName)
        => !string.IsNullOrWhiteSpace(modelName) &&
           (IsCloudModel(modelName) ||
            modelName.EndsWith(":free", StringComparison.OrdinalIgnoreCase) ||
            modelName.EndsWith("/free", StringComparison.OrdinalIgnoreCase) ||
            modelName.Contains(":free", StringComparison.OrdinalIgnoreCase));

    private static bool IsPaidModel(string modelName)
        => IsOpenAiModel(modelName) || IsAnthropicModel(modelName);

    private static bool IsOpenAiModel(string modelName)
        => !string.IsNullOrWhiteSpace(modelName) &&
           modelName.StartsWith("openai:", StringComparison.OrdinalIgnoreCase);

    private static bool IsAnthropicModel(string modelName)
        => !string.IsNullOrWhiteSpace(modelName) &&
           (modelName.StartsWith("anthropic:", StringComparison.OrdinalIgnoreCase) ||
            modelName.StartsWith("claude:", StringComparison.OrdinalIgnoreCase));

    partial void OnNameMatchingModelChanged(string value)
    {
        SelectedModelTierIndex = GetTierIndexForModel(value);
    }

    partial void OnFaceDetectionModelChanged(string value)
    {
        SelectedFaceModelTierIndex = GetFaceTierIndexForModel(value);
    }

    partial void OnSelectedCropOffsetPresetChanged(CropOffsetPreset? value)
    {
        if (value == null) return;
        CropOffsetX = value.HorizontalPercent;
        CropOffsetY = value.VerticalPercent;
    }

    partial void OnUseAiMappingChanged(bool value)
    {
        if (!value)
        {
            AiOnly = false;
        }
    }

    partial void OnAiOnlyChanged(bool value)
    {
        if (value)
        {
            UseAiMapping = true;
        }
    }

    partial void OnSelectedMissingPhotoTeamChanged(MissingTeamFolderItem? value)
    {
        OnPropertyChanged(nameof(CanRenameMissingPhotoFolder));
    }

    partial void OnSelectedPhotoFolderChanged(string? value)
    {
        OnPropertyChanged(nameof(CanRenameMissingPhotoFolder));
    }

    partial void OnIsProcessingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRenameMissingPhotoFolder));
    }

    private static int GetTierIndexForModel(string modelName)
    {
        if (IsPaidModel(modelName))
            return 2;
        if (IsFreeTierModel(modelName))
            return 0;
        return 1;
    }

    private static int GetFaceTierIndexForModel(string modelName)
    {
        if (string.Equals(modelName, "opencv-dnn", StringComparison.OrdinalIgnoreCase))
            return 0;
        if (modelName.Contains("llava", StringComparison.OrdinalIgnoreCase) ||
            modelName.Contains("qwen3-vl", StringComparison.OrdinalIgnoreCase))
            return 1;
        return 2;
    }

    private void LoadCropOffsetPresets()
    {
        var settings = CropOffsetSettingsLoader.Load();

        CropOffsetPresets.Clear();
        foreach (var preset in settings.Presets)
        {
            CropOffsetPresets.Add(new CropOffsetPreset
            {
                Name = preset.Name,
                HorizontalPercent = preset.HorizontalPercent,
                VerticalPercent = preset.VerticalPercent
            });
        }

        if (CropOffsetPresets.Count == 0)
        {
            CropOffsetPresets.Add(new CropOffsetPreset { Name = "default" });
        }

        var active = settings.GetActivePreset();
        SelectedCropOffsetPreset = CropOffsetPresets.FirstOrDefault(p =>
            string.Equals(p.Name, active.Name, StringComparison.OrdinalIgnoreCase))
            ?? CropOffsetPresets[0];
    }

    private void LoadFilenamePatternPresets()
    {
        var settings = FilenamePatternSettingsLoader.Load();

        FilenamePatternPresets.Clear();
        foreach (var preset in settings.Presets)
        {
            FilenamePatternPresets.Add(new FilenamePatternPreset
            {
                Name = preset.Name,
                Pattern = preset.Pattern,
                Description = preset.Description
            });
        }

        if (FilenamePatternPresets.Count == 0)
        {
            FilenamePatternPresets.Add(new FilenamePatternPreset { Name = "default", Pattern = string.Empty });
        }

        var active = settings.GetActivePreset();
        SelectedFilenamePatternPreset = FilenamePatternPresets.FirstOrDefault(p =>
            string.Equals(p.Name, active.Name, StringComparison.OrdinalIgnoreCase))
            ?? FilenamePatternPresets[0];
    }

    [RelayCommand]
    private void SaveFilenamePatternPreset()
    {
        var presetName = SelectedFilenamePatternPreset?.Name;
        if (string.IsNullOrWhiteSpace(presetName))
        {
            presetName = "default";
        }

        var preset = FilenamePatternPresets.FirstOrDefault(p =>
            string.Equals(p.Name, presetName, StringComparison.OrdinalIgnoreCase));

        if (preset == null)
        {
            preset = new FilenamePatternPreset { Name = presetName };
            FilenamePatternPresets.Add(preset);
        }

        preset.Pattern = FilenamePattern;

        var settings = new FilenamePatternSettings
        {
            ActivePresetName = presetName,
            Presets = FilenamePatternPresets
                .Select(p => new FilenamePatternPreset
                {
                    Name = p.Name,
                    Pattern = p.Pattern,
                    Description = p.Description
                })
                .ToList()
        };

        FilenamePatternSettingsLoader.SaveToLocal(settings);
        FilenamePatternStatus = $"Saved preset '{presetName}' to appsettings.local.json";
        SelectedFilenamePatternPreset = preset;
    }

    [RelayCommand]
    private void NewFilenamePatternPreset()
    {
        var baseName = "new_pattern";
        var counter = 1;
        var newName = $"{baseName}_{counter}";

        while (FilenamePatternPresets.Any(p => string.Equals(p.Name, newName, StringComparison.OrdinalIgnoreCase)))
        {
            counter++;
            newName = $"{baseName}_{counter}";
        }

        var newPreset = new FilenamePatternPreset
        {
            Name = newName,
            Pattern = FilenamePattern
        };

        FilenamePatternPresets.Add(newPreset);
        SelectedFilenamePatternPreset = newPreset;
        FilenamePatternStatus = $"Created new preset '{newName}'. Click 'Save' to persist.";
    }

    partial void OnSelectedFilenamePatternPresetChanged(FilenamePatternPreset? value)
    {
        if (value != null)
        {
            FilenamePattern = value.Pattern;
        }
    }

    #endregion
}
