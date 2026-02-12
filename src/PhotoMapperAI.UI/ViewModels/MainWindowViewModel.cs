using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoMapperAI.UI.Models;

namespace PhotoMapperAI.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ExtractStepViewModel _extractStep;
    private readonly MapStepViewModel _mapStep;
    private readonly GenerateStepViewModel _generateStep;

    [ObservableProperty]
    private ViewModelBase _currentView;

    [ObservableProperty]
    private int _currentStep = 1;

    [ObservableProperty]
    private string _statusMessage = "Ready to start";

    [ObservableProperty]
    private string _currentStepDescription = "Step 1: Extract player data from database to CSV";

    public MainWindowViewModel()
    {
        _extractStep = new ExtractStepViewModel();
        _mapStep = new MapStepViewModel();
        _generateStep = new GenerateStepViewModel();
        _extractStep.PropertyChanged += OnStepPropertyChanged;
        _mapStep.PropertyChanged += OnStepPropertyChanged;
        _generateStep.PropertyChanged += OnStepPropertyChanged;

        _currentView = _extractStep;
    }

    // Step colors for progress indicator
    public IBrush Step1Background => CurrentStep >= 1 
        ? new SolidColorBrush(Color.Parse("#4CAF50")) 
        : new SolidColorBrush(Color.Parse("#E0E0E0"));
    public IBrush Step1Foreground => CurrentStep >= 1 
        ? new SolidColorBrush(Colors.White) 
        : new SolidColorBrush(Color.Parse("#666666"));

    public IBrush Step2Background => CurrentStep >= 2 
        ? new SolidColorBrush(Color.Parse("#4CAF50")) 
        : new SolidColorBrush(Color.Parse("#E0E0E0"));
    public IBrush Step2Foreground => CurrentStep >= 2 
        ? new SolidColorBrush(Colors.White) 
        : new SolidColorBrush(Color.Parse("#666666"));

    public IBrush Step3Background => CurrentStep >= 3 
        ? new SolidColorBrush(Color.Parse("#4CAF50")) 
        : new SolidColorBrush(Color.Parse("#E0E0E0"));
    public IBrush Step3Foreground => CurrentStep >= 3 
        ? new SolidColorBrush(Colors.White) 
        : new SolidColorBrush(Color.Parse("#666666"));

    public bool CanGoBack => CurrentStep > 1;
    public bool CanGoNext => CurrentStep < 3 && CanProceedToNextStep();
    public bool CanFinish => CurrentStep == 3;

    private bool CanProceedToNextStep()
    {
        return CurrentStep switch
        {
            1 => _extractStep.IsComplete,
            2 => _mapStep.IsComplete,
            _ => false
        };
    }

    [RelayCommand]
    private void Back()
    {
        if (CurrentStep > 1)
        {
            CurrentStep--;
            UpdateCurrentView();
        }
    }

    [RelayCommand]
    private void Next()
    {
        if (CurrentStep < 3 && CanProceedToNextStep())
        {
            CurrentStep++;
            UpdateCurrentView();
        }
    }

    [RelayCommand]
    private async Task Finish()
    {
        StatusMessage = "Processing complete! You can close the application.";
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SaveSession()
    {
        try
        {
            var session = BuildSessionState();
            var sessionPath = SessionState.GetDefaultSessionPath();
            await session.SaveAsync(sessionPath);
            StatusMessage = $"Session saved: {sessionPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save session: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoadSession()
    {
        try
        {
            var sessionPath = SessionState.GetDefaultSessionPath();
            if (!File.Exists(sessionPath))
            {
                StatusMessage = $"No saved session found at {sessionPath}";
                return;
            }

            var session = await SessionState.LoadAsync(sessionPath);
            ApplySessionState(session);
            StatusMessage = $"Session loaded: {sessionPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load session: {ex.Message}";
        }
    }

    private SessionState BuildSessionState()
    {
        return new SessionState
        {
            CurrentStep = CurrentStep,

            SqlFilePath = _extractStep.SqlFilePath,
            ConnectionStringPath = _extractStep.ConnectionStringPath,
            TeamId = _extractStep.TeamId,
            OutputFileName = _extractStep.OutputFileName,
            DatabaseType = _extractStep.DatabaseType,
            ExtractComplete = _extractStep.IsComplete,
            PlayersExtracted = _extractStep.PlayersExtracted,

            MapInputCsvPath = _mapStep.InputCsvPath,
            PhotosDirectory = _mapStep.PhotosDirectory,
            FilenamePattern = _mapStep.FilenamePattern,
            UsePhotoManifest = _mapStep.UsePhotoManifest,
            PhotoManifestPath = _mapStep.PhotoManifestPath,
            NameModel = _mapStep.NameModel,
            ConfidenceThreshold = _mapStep.ConfidenceThreshold,
            MapComplete = _mapStep.IsComplete,
            PlayersMatched = _mapStep.PlayersMatched,
            PlayersProcessed = _mapStep.PlayersProcessed,

            GenerateInputCsvPath = _generateStep.InputCsvPath,
            GeneratePhotosDirectory = _generateStep.PhotosDirectory,
            OutputDirectory = _generateStep.OutputDirectory,
            ImageFormat = _generateStep.ImageFormat,
            FaceDetectionModel = _generateStep.FaceDetectionModel,
            PortraitWidth = _generateStep.PortraitWidth,
            PortraitHeight = _generateStep.PortraitHeight,
            PortraitOnly = _generateStep.PortraitOnly,
            GenerateComplete = _generateStep.IsComplete,
            PortraitsGenerated = _generateStep.PortraitsGenerated,
            PortraitsFailed = _generateStep.PortraitsFailed
        };
    }

    private void ApplySessionState(SessionState session)
    {
        _extractStep.SqlFilePath = session.SqlFilePath ?? string.Empty;
        _extractStep.ConnectionStringPath = session.ConnectionStringPath ?? string.Empty;
        _extractStep.TeamId = session.TeamId;
        _extractStep.OutputFileName = session.OutputFileName ?? "players.csv";
        _extractStep.DatabaseType = session.DatabaseType ?? "SqlServer";
        _extractStep.IsComplete = session.ExtractComplete;
        _extractStep.PlayersExtracted = session.PlayersExtracted;

        _mapStep.InputCsvPath = session.MapInputCsvPath ?? string.Empty;
        _mapStep.PhotosDirectory = session.PhotosDirectory ?? string.Empty;
        _mapStep.FilenamePattern = session.FilenamePattern ?? string.Empty;
        _mapStep.UsePhotoManifest = session.UsePhotoManifest;
        _mapStep.PhotoManifestPath = session.PhotoManifestPath ?? string.Empty;
        _mapStep.NameModel = session.NameModel ?? "qwen2.5:7b";
        _mapStep.ConfidenceThreshold = session.ConfidenceThreshold;
        _mapStep.IsComplete = session.MapComplete;
        _mapStep.PlayersMatched = session.PlayersMatched;
        _mapStep.PlayersProcessed = session.PlayersProcessed;

        _generateStep.InputCsvPath = session.GenerateInputCsvPath ?? string.Empty;
        _generateStep.PhotosDirectory = session.GeneratePhotosDirectory ?? string.Empty;
        _generateStep.OutputDirectory = session.OutputDirectory ?? string.Empty;
        _generateStep.ImageFormat = session.ImageFormat ?? "jpg";
        _generateStep.FaceDetectionModel = session.FaceDetectionModel ?? "llava:7b,qwen3-vl";
        _generateStep.PortraitWidth = session.PortraitWidth;
        _generateStep.PortraitHeight = session.PortraitHeight;
        _generateStep.PortraitOnly = session.PortraitOnly;
        _generateStep.IsComplete = session.GenerateComplete;
        _generateStep.PortraitsGenerated = session.PortraitsGenerated;
        _generateStep.PortraitsFailed = session.PortraitsFailed;

        CurrentStep = session.CurrentStep is >= 1 and <= 3 ? session.CurrentStep : 1;
        UpdateCurrentView();
    }

    private void OnStepPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ExtractStepViewModel.IsComplete) or nameof(MapStepViewModel.IsComplete))
        {
            OnPropertyChanged(nameof(CanGoNext));
        }
    }

    private void UpdateCurrentView()
    {
        CurrentView = CurrentStep switch
        {
            1 => _extractStep,
            2 => _mapStep,
            3 => _generateStep,
            _ => _extractStep
        };

        CurrentStepDescription = CurrentStep switch
        {
            1 => "Step 1: Extract player data from database to CSV",
            2 => "Step 2: Map photos to players using AI",
            3 => "Step 3: Generate portrait photos",
            _ => ""
        };

        // Notify UI of property changes
        OnPropertyChanged(nameof(Step1Background));
        OnPropertyChanged(nameof(Step1Foreground));
        OnPropertyChanged(nameof(Step2Background));
        OnPropertyChanged(nameof(Step2Foreground));
        OnPropertyChanged(nameof(Step3Background));
        OnPropertyChanged(nameof(Step3Foreground));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanFinish));
    }
}
