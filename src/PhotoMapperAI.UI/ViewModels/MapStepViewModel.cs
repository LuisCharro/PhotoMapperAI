using System;
using System.Collections.ObjectModel;
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
    private static readonly string[] KnownHostedNameModels =
    {
        "openai:gpt-4o-mini",
        "openai:gpt-4.1-mini",
        "anthropic:claude-3-5-sonnet",
        "anthropic:claude-3-5-haiku"
    };

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
    private string _openAiApiKey = string.Empty;

    [ObservableProperty]
    private string _anthropicApiKey = string.Empty;

    [ObservableProperty]
    private bool _showOpenAiApiKeyInput;

    [ObservableProperty]
    private bool _showAnthropicApiKeyInput;

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

    public ObservableCollection<string> NameModels { get; } = new();

    public MapStepViewModel()
    {
        SeedNameModelList();
        UpdateProviderKeyInputVisibility();
        _ = RefreshNameModelsAsync(showStatus: false);
    }

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
            if (IsOpenAiModel(NameModel))
            {
                var keyPresent = !string.IsNullOrWhiteSpace(OpenAiApiKey) ||
                                 !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
                ModelDiagnosticStatus = keyPresent
                    ? "✓ OpenAI API key available. OpenAI model can be used."
                    : "✗ OpenAI API key is missing (GUI field or OPENAI_API_KEY).";
                return;
            }

            if (IsAnthropicModel(NameModel))
            {
                var keyPresent = !string.IsNullOrWhiteSpace(AnthropicApiKey) ||
                                 !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));
                ModelDiagnosticStatus = keyPresent
                    ? "✓ Anthropic API key available. Anthropic model can be used."
                    : "✗ Anthropic API key is missing (GUI field or ANTHROPIC_API_KEY).";
                return;
            }

            var client = new OllamaClient();
            var available = await client.IsAvailableAsync();
            if (!available)
            {
                ModelDiagnosticStatus = "✗ Ollama server is not reachable (http://localhost:11434)";
                return;
            }

            if (IsCloudModel(NameModel))
            {
                ModelDiagnosticStatus = $"ℹ Cloud model selected: {NameModel}. Availability depends on cloud access/quota.";
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
    private async Task RefreshNameModels()
    {
        await RefreshNameModelsAsync(showStatus: true);
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

            var preflight = await PreflightChecker.CheckMapAsync(
                UseAiMapping,
                NameModel,
                openAiApiKey: string.IsNullOrWhiteSpace(OpenAiApiKey) ? null : OpenAiApiKey,
                anthropicApiKey: string.IsNullOrWhiteSpace(AnthropicApiKey) ? null : AnthropicApiKey);
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
            var nameMatchingService = NameMatchingServiceFactory.Create(
                NameModel,
                confidenceThreshold: ConfidenceThreshold,
                openAiApiKey: string.IsNullOrWhiteSpace(OpenAiApiKey) ? null : OpenAiApiKey,
                anthropicApiKey: string.IsNullOrWhiteSpace(AnthropicApiKey) ? null : AnthropicApiKey);
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
            RebuildNameModelList(localModels);

            if (showStatus)
            {
                ModelDiagnosticStatus = $"✓ Loaded {NameModels.Count} selectable models";
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
        RebuildNameModelList(Array.Empty<string>());
    }

    private void RebuildNameModelList(IEnumerable<string> discoveredModels)
    {
        var previousSelection = NameModel;
        var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var model in DefaultLocalNameModels)
            merged.Add(model);

        foreach (var model in discoveredModels.Where(m => !string.IsNullOrWhiteSpace(m)))
            merged.Add(model.Trim());

        foreach (var model in KnownCloudNameModels)
            merged.Add(model);
        foreach (var model in KnownHostedNameModels)
            merged.Add(model);

        var ordered = merged
            .OrderBy(model => IsCloudModel(model) ? 1 : 0)
            .ThenBy(model => model, StringComparer.OrdinalIgnoreCase)
            .ToList();

        NameModels.Clear();
        foreach (var model in ordered)
            NameModels.Add(model);

        if (!string.IsNullOrWhiteSpace(previousSelection) &&
            NameModels.Any(m => string.Equals(m, previousSelection, StringComparison.OrdinalIgnoreCase)))
        {
            NameModel = NameModels.First(m => string.Equals(m, previousSelection, StringComparison.OrdinalIgnoreCase));
            return;
        }

        if (NameModels.Contains("qwen2.5:7b"))
        {
            NameModel = "qwen2.5:7b";
            return;
        }

        NameModel = NameModels.FirstOrDefault() ?? "qwen2.5:7b";
    }

    private static bool IsCloudModel(string modelName)
        => !string.IsNullOrWhiteSpace(modelName) &&
           (modelName.EndsWith(":cloud", StringComparison.OrdinalIgnoreCase) ||
            modelName.EndsWith("-cloud", StringComparison.OrdinalIgnoreCase));

    private static bool IsOpenAiModel(string modelName)
        => !string.IsNullOrWhiteSpace(modelName) &&
           modelName.StartsWith("openai:", StringComparison.OrdinalIgnoreCase);

    private static bool IsAnthropicModel(string modelName)
        => !string.IsNullOrWhiteSpace(modelName) &&
           (modelName.StartsWith("anthropic:", StringComparison.OrdinalIgnoreCase) ||
            modelName.StartsWith("claude:", StringComparison.OrdinalIgnoreCase));

    partial void OnNameModelChanged(string value)
    {
        UpdateProviderKeyInputVisibility();
    }

    private void UpdateProviderKeyInputVisibility()
    {
        ShowOpenAiApiKeyInput = IsOpenAiModel(NameModel);
        ShowAnthropicApiKeyInput = IsAnthropicModel(NameModel);
    }
}
