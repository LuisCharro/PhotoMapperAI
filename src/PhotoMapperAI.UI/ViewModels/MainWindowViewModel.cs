using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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

    [ObservableProperty]
    private bool _isDarkTheme;

    public string ThemeToggleText => IsDarkTheme ? "☀️ Light" : "🌙 Dark";

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
    public bool CanGoNext => CurrentStep < 3;
    public bool CanFinish => CurrentStep == 3;

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
        if (CurrentStep < 3)
        {
            CurrentStep++;
            UpdateCurrentView();
        }
    }

    [RelayCommand]
    private void GoToStep(int step)
    {
        if (step is < 1 or > 3 || step == CurrentStep)
        {
            return;
        }

        CurrentStep = step;
        UpdateCurrentView();
    }

    [RelayCommand]
    private async Task Finish()
    {
        StatusMessage = "Processing complete! You can close the application.";
        await Task.CompletedTask;
    }


    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;

        if (Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = IsDarkTheme
                ? ThemeVariant.Dark
                : ThemeVariant.Light;
        }

        OnPropertyChanged(nameof(ThemeToggleText));
        StatusMessage = $"Theme changed to {(IsDarkTheme ? "Dark" : "Light")}";
    }


    private void OnStepPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender == _extractStep &&
            e.PropertyName == nameof(ExtractStepViewModel.IsComplete) &&
            _extractStep.IsComplete &&
            !string.IsNullOrWhiteSpace(_extractStep.OutputCsvPath))
        {
            _mapStep.InputCsvPath = _extractStep.OutputCsvPath;
            _mapStep.IsComplete = false;
            StatusMessage = $"Step 2 input CSV set to latest extract: {_extractStep.OutputCsvPath}";
        }

        if (e.PropertyName is nameof(ExtractStepViewModel.IsComplete) or nameof(MapStepViewModel.IsComplete))
        {
            OnPropertyChanged(nameof(CanGoNext));
        }

        if (sender == _mapStep &&
            e.PropertyName == nameof(MapStepViewModel.IsComplete) &&
            _mapStep.IsComplete &&
            !string.IsNullOrWhiteSpace(_mapStep.OutputCsvPath))
        {
            _generateStep.InputCsvPath = _mapStep.OutputCsvPath;
            if (!string.IsNullOrWhiteSpace(_mapStep.PhotosDirectory))
            {
                _generateStep.PhotosDirectory = _mapStep.PhotosDirectory;
            }

            StatusMessage = $"Step 3 inputs set: {_mapStep.OutputCsvPath}";
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
            2 => "Step 2: Map players to photos using filename IDs",
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
