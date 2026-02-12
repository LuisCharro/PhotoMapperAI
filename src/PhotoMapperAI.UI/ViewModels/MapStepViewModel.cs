using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoMapperAI.Services.AI;
using PhotoMapperAI.Services.Diagnostics;
using PhotoMapperAI.Services.Image;

namespace PhotoMapperAI.UI.ViewModels;

public partial class MapStepViewModel : ViewModelBase
{
    private const double MinConfidenceThreshold = 0.8;
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private string _inputCsvPath = string.Empty;

    [ObservableProperty]
    private string _photosDirectory = string.Empty;

    [ObservableProperty]
    private string _filenamePattern = string.Empty;

    [ObservableProperty]
    private string _photoManifestPath = string.Empty;

    [ObservableProperty]
    private string _nameModel = "qwen2.5:7b";

    [ObservableProperty]
    private double _confidenceThreshold = MinConfidenceThreshold;

    [ObservableProperty]
    private bool _useAiMapping;

    [ObservableProperty]
    private bool _aiSecondPass = true;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _processingStatus = string.Empty;

    [ObservableProperty]
    private int _playersProcessed;

    [ObservableProperty]
    private int _playersMatched;

    [ObservableProperty]
    private bool _isComplete;

    [ObservableProperty]
    private bool _usePhotoManifest;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isCheckingModel;

    [ObservableProperty]
    private string _modelDiagnosticStatus = string.Empty;

    public List<string> NameModels { get; } = new()
    {
        "qwen2.5:7b",
        "qwen3:8b",
        "llava:7b"
    };

    [RelayCommand]
    private async Task BrowseCsvFile()
    {
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task BrowsePhotosDirectory()
    {
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task BrowsePhotoManifest()
    {
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task CheckNameModel()
    {
        if (!UseAiMapping)
        {
            ModelDiagnosticStatus = "Enable AI mapping to check Ollama models.";
            return;
        }

        if (IsProcessing)
            return;

        IsCheckingModel = true;
        ModelDiagnosticStatus = $"Checking model '{NameModel}'...";

        try
        {
            var client = new OllamaClient();
            var available = await client.IsAvailableAsync();
            if (!available)
            {
                ModelDiagnosticStatus = "✗ Ollama server is not reachable (http://localhost:11434)";
                return;
            }

            var models = await client.GetAvailableModelsAsync();
            var exists = models.Any(m => string.Equals(m, NameModel, StringComparison.OrdinalIgnoreCase));
            ModelDiagnosticStatus = exists
                ? $"✓ Model available: {NameModel}"
                : $"✗ Model not found in Ollama: {NameModel}";
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
    private async Task ExecuteMap()
    {
        if (string.IsNullOrEmpty(InputCsvPath) || string.IsNullOrEmpty(PhotosDirectory))
        {
            ProcessingStatus = "Please select CSV file and photos directory";
            return;
        }

        IsProcessing = true;
        IsComplete = false;
        Progress = 0;
        PlayersProcessed = 0;
        PlayersMatched = 0;
        ProcessingStatus = "Mapping photos to players...";
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            if (ConfidenceThreshold < MinConfidenceThreshold)
            {
                ConfidenceThreshold = MinConfidenceThreshold;
            }

            var preflight = await PreflightChecker.CheckMapAsync(UseAiMapping, NameModel);
            if (!preflight.IsOk)
            {
                ProcessingStatus = preflight.BuildMessage();
                IsProcessing = false;
                return;
            }

            var warningMessage = preflight.BuildWarningMessage();
            if (!string.IsNullOrWhiteSpace(warningMessage))
            {
                ModelDiagnosticStatus = warningMessage;
            }

            // Create services
            var nameMatchingService = new OllamaNameMatchingService(
                modelName: NameModel,
                confidenceThreshold: ConfidenceThreshold
            );
            var imageProcessor = new ImageProcessor();

            // Create map command logic
            var logic = new Commands.MapCommandLogic(nameMatchingService, imageProcessor);

            // Execute mapping
            var progress = new Progress<(int processed, int total, string current)>(p =>
            {
                var percent = p.total > 0
                    ? (double)p.processed / p.total * 100.0
                    : 0.0;
                Progress = Math.Clamp(percent, 0, 100);
                ProcessingStatus = $"Processing {p.processed}/{p.total}: {p.current}";
            });

            var result = await logic.ExecuteAsync(
                InputCsvPath,
                PhotosDirectory,
                string.IsNullOrEmpty(FilenamePattern) ? null : FilenamePattern,
                UsePhotoManifest ? PhotoManifestPath : null,
                NameModel,
                ConfidenceThreshold,
                UseAiMapping,
                UseAiMapping && AiSecondPass,
                progress,
                _cancellationTokenSource.Token
            );

            PlayersProcessed = result.PlayersProcessed;
            PlayersMatched = result.PlayersMatched;
            ProcessingStatus = $"✓ Mapped {PlayersMatched}/{PlayersProcessed} players successfully";
            Progress = 100;
            IsComplete = true;
        }
        catch (OperationCanceledException)
        {
            ProcessingStatus = "⚠ Mapping cancelled";
            IsComplete = false;
        }
        catch (Exception ex)
        {
            ProcessingStatus = $"✗ Error: {ex.Message}";
            IsComplete = false;
        }
        finally
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void CancelMap()
    {
        if (IsProcessing)
        {
            _cancellationTokenSource?.Cancel();
            ProcessingStatus = "Cancelling mapping...";
        }
    }
}
