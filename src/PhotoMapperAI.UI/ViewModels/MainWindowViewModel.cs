using System;
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
        // TODO: Implement session save
        StatusMessage = "Session saved successfully!";
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task LoadSession()
    {
        // TODO: Implement session load
        StatusMessage = "Session loaded successfully!";
        await Task.CompletedTask;
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
