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
using PhotoMapperAI.UI.Configuration;
using PhotoMapperAI.UI.Execution;

namespace PhotoMapperAI.UI.ViewModels;

public partial class MapStepViewModel : ViewModelBase
{
    private const double MinConfidenceThreshold = 0.8;
    private readonly ExternalMapCliRunner _mapRunner = new();
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
    private static readonly string[] DefaultPaidNameModels =
    {
        "openai:gpt-4.1",
        "openai:gpt-4o",
        "openai:o3-mini",
        "anthropic:claude-3-5-sonnet"
    };

    private static readonly string[] ConfiguredPaidNameModels =
        UiModelConfigLoader.Load().MapPaidModels.ToArray();

    [ObservableProperty]
    private string _inputCsvPath = string.Empty;

    [ObservableProperty]
    private string _photosDirectory = string.Empty;

    [ObservableProperty]
    private string _outputDirectory = string.Empty;

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
    private bool _aiOnly;

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
    private string _outputCsvPath = string.Empty;

    [ObservableProperty]
    private bool _usePhotoManifest;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isCheckingModel;

    [ObservableProperty]
    private string _modelDiagnosticStatus = string.Empty;

    [ObservableProperty]
    private int _selectedModelTierIndex;

    public ObservableCollection<string> LogLines { get; } = new();

    public ObservableCollection<string> LocalNameModels { get; } = new();

    public ObservableCollection<string> FreeTierNameModels { get; } = new();

    public ObservableCollection<string> PaidNameModels { get; } = new();

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
    private void ClearInputCsv()
    {
        InputCsvPath = string.Empty;
        OutputCsvPath = string.Empty;
        IsComplete = false;
    }

    [RelayCommand]
    private async Task BrowsePhotosDirectory()
    {
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void ClearPhotosDirectory()
    {
        PhotosDirectory = string.Empty;
        IsComplete = false;
    }

    [RelayCommand]
    private void ClearOutputDirectory()
    {
        OutputDirectory = string.Empty;
        IsComplete = false;
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

            if (IsFreeTierModel(NameModel))
            {
                ModelDiagnosticStatus = $"ℹ Free-tier cloud model selected: {NameModel}. Availability depends on cloud access/quota.";
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

        LogLines.Clear();
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

            var effectiveNameModel = NameModel;

            var preflight = await PreflightChecker.CheckMapAsync(
                UseAiMapping,
                effectiveNameModel,
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

            var log = new Progress<string>(line =>
            {
                AppendLog(line);
                var m = System.Text.RegularExpressions.Regex.Match(line, @"Matched\s+(\d+)\s*/\s*(\d+)\s*players", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    PlayersMatched = int.Parse(m.Groups[1].Value);
                    var total = int.Parse(m.Groups[2].Value);
                    if (total > 0)
                    {
                        PlayersProcessed = total;
                    }
                    ProcessingStatus = $"Matched {PlayersMatched}/{PlayersProcessed} players...";
                }
            });

            var uiProgress = new Progress<(int processed, int total, string current)>(state =>
            {
                if (state.total <= 0)
                {
                    Progress = 0;
                    return;
                }

                PlayersProcessed = state.processed;
                Progress = Math.Clamp((double)state.processed / state.total * 100.0, 0, 100);
                var currentLabel = string.IsNullOrWhiteSpace(state.current) ? string.Empty : $" {state.current}";
                ProcessingStatus = $"Mapping {state.processed}/{state.total} players{currentLabel}";
            });

            var effectiveOutputDirectory = string.IsNullOrWhiteSpace(OutputDirectory)
                ? Directory.GetCurrentDirectory()
                : OutputDirectory;
            Directory.CreateDirectory(effectiveOutputDirectory);

            var result = await _mapRunner.ExecuteAsync(
                Directory.GetCurrentDirectory(),
                effectiveOutputDirectory,
                InputCsvPath,
                PhotosDirectory,
                string.IsNullOrWhiteSpace(FilenamePattern) ? null : FilenamePattern,
                UsePhotoManifest ? PhotoManifestPath : null,
                effectiveNameModel,
                ConfidenceThreshold,
                UseAiMapping,
                UseAiMapping && AiSecondPass,
                UseAiMapping && AiOnly,
                string.IsNullOrWhiteSpace(OpenAiApiKey) ? null : OpenAiApiKey,
                string.IsNullOrWhiteSpace(AnthropicApiKey) ? null : AnthropicApiKey,
                _cancellationTokenSource.Token,
                log,
                uiProgress);

            if (result.ExitCode != 0)
            {
                ProcessingStatus = $"✗ Mapping failed with exit code {result.ExitCode}";
                IsComplete = false;
                return;
            }

            PlayersProcessed = result.PlayersProcessed;
            PlayersMatched = result.PlayersMatched;
            OutputCsvPath = result.OutputCsvPath ?? string.Empty;
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

    private void RebuildNameModelLists(IEnumerable<string> discoveredModels)
    {
        var previousSelection = NameModel;
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

        // Keep configured paid model order (power order) when provided.
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
            NameModel = previousSelection;
            return;
        }

        if (LocalNameModels.Contains("qwen2.5:7b"))
        {
            NameModel = "qwen2.5:7b";
            return;
        }

        NameModel = LocalNameModels.FirstOrDefault()
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

    partial void OnNameModelChanged(string value)
    {
        UpdateProviderKeyInputVisibility();
        SelectedModelTierIndex = GetTierIndexForModel(value);
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

    private void AppendLog(string message)
    {
        if (LogLines.Count >= 200)
        {
            LogLines.RemoveAt(0);
        }

        LogLines.Add(message);
    }

    private void UpdateProviderKeyInputVisibility()
    {
        ShowOpenAiApiKeyInput = IsOpenAiModel(NameModel);
        ShowAnthropicApiKeyInput = IsAnthropicModel(NameModel);
    }

    private static int GetTierIndexForModel(string modelName)
    {
        if (IsPaidModel(modelName))
            return 2;
        if (IsFreeTierModel(modelName))
            return 0;
        return 1;
    }

}
