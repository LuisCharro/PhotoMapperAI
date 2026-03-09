using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoMapperAI.Models;
using PhotoMapperAI.Services.AI;
using PhotoMapperAI.Services.Diagnostics;
using PhotoMapperAI.UI.Configuration;
using PhotoMapperAI.UI.Execution;

namespace PhotoMapperAI.UI.ViewModels;

public partial class MapStepViewModel : ViewModelBase
{
    private const double MinConfidenceThreshold = 0.65;
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
        "openai:gpt-5-mini",
        "openai:gpt-5.2",
        "openai:gpt-5.2-pro",
        "anthropic:claude-3-5-sonnet",
        "zai:glm-4.5",
        "zai:glm-4-flash",
        "zai:glm-4",
        // MiniMax models (Coding Plan - use Anthropic-compatible API)
        "minimax:MiniMax-M2.5-highspeed",  // ~100 tps - fastest option
        "minimax:MiniMax-M2.5",             // ~60 tps
        "minimax:MiniMax-M2.1-highspeed",
        "minimax:MiniMax-M2.1",
        "minimax:MiniMax-M2"
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
    private FilenamePatternPreset? _selectedFilenamePatternPreset;

    [ObservableProperty]
    private string _filenamePatternStatus = string.Empty;

    public ObservableCollection<FilenamePatternPreset> FilenamePatternPresets { get; } = new();

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
    private bool _aiTrace;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private bool _showPaidApiKeyInput;

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

    // Tier selection for new UI (radio-button style, mutually exclusive)
    [ObservableProperty]
    private bool _isFreeTierSelected;

    [ObservableProperty]
    private bool _isLocalSelected;

    [ObservableProperty]
    private bool _isPaidSelected;

    [ObservableProperty]
    private string? _selectedFreeTierModel;

    [ObservableProperty]
    private string? _selectedLocalModel;

    [ObservableProperty]
    private string? _selectedPaidModel;

    public ObservableCollection<string> LogLines { get; } = new();

    public ObservableCollection<string> LocalNameModels { get; } = new();

    public ObservableCollection<string> FreeTierNameModels { get; } = new();

    public ObservableCollection<string> PaidNameModels { get; } = new();

    public MapStepViewModel()
    {
        SeedNameModelList();
        LoadFilenamePatternPresets();
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
            if (IsPaidModel(NameModel))
            {
                var preferredKeys = new (string EnvVar, string Provider)[]
                {
                    ("MINIMAX_API_KEY", "MiniMax"),
                    ("ZAI_API_KEY", "Z.AI"),
                    ("OPENAI_API_KEY", "OpenAI"),
                    ("ANTHROPIC_API_KEY", "Anthropic")
                };

                var preferred = preferredKeys.FirstOrDefault(key =>
                    !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key.EnvVar)));
                    
                var envVarName = string.IsNullOrWhiteSpace(preferred.EnvVar)
                    ? preferredKeys[0].EnvVar
                    : preferred.EnvVar;

                var providerName = string.IsNullOrWhiteSpace(preferred.Provider)
                    ? preferredKeys[0].Provider
                    : preferred.Provider;

                var keyPresent = !string.IsNullOrWhiteSpace(ApiKey) ||
                                 !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(envVarName));

                ModelDiagnosticStatus = keyPresent
                    ? $"✓ {providerName} API key available. {providerName} model can be used."
                    : $"✗ {providerName} API key is missing (GUI field or {envVarName}).";
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

            // Log AI configuration
            AppendLog("AI Configuration:");
            AppendLog($"  - Use AI Mapping: {UseAiMapping}");
            AppendLog($"  - AI Only Mode: {AiOnly}");
            AppendLog($"  - AI Second Pass: {AiSecondPass}");
            AppendLog($"  - Name Model: {effectiveNameModel}");
            AppendLog($"  - Confidence Threshold: {ConfidenceThreshold:F2}");
            
            // Determine which API key to use based on model
            string? usedApiKey = null;
            if (IsPaidModel(effectiveNameModel))
            {
                usedApiKey = string.IsNullOrWhiteSpace(ApiKey) ? null : ApiKey;
                AppendLog($"  - API Key: {(usedApiKey != null ? "✓ Provided" : "✗ Missing")}");
            }
            AppendLog("");

            var preflight = await PreflightChecker.CheckMapAsync(
                UseAiMapping,
                effectiveNameModel,
                openAiApiKey: IsOpenAiModel(effectiveNameModel) ? usedApiKey : null,
                anthropicApiKey: IsAnthropicModel(effectiveNameModel) ? usedApiKey : null,
                zaiApiKey: IsZaiModel(effectiveNameModel) ? usedApiKey : null,
                minimaxApiKey: IsMiniMaxModel(effectiveNameModel) ? usedApiKey : null);
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
                UseAiMapping && AiTrace,
                IsOpenAiModel(effectiveNameModel) ? usedApiKey : null,
                IsAnthropicModel(effectiveNameModel) ? usedApiKey : null,
                IsZaiModel(effectiveNameModel) ? usedApiKey : null,
                IsMiniMaxModel(effectiveNameModel) ? usedApiKey : null,
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

        SelectedFreeTierModel = FreeTierNameModels.FirstOrDefault();
        SelectedLocalModel = LocalNameModels.FirstOrDefault();
        SelectedPaidModel = PaidNameModels.FirstOrDefault();

        if (IsFreeTierModel(previousSelection))
            SelectedFreeTierModel = previousSelection;
        else if (IsLocalModel(previousSelection))
            SelectedLocalModel = previousSelection;
        else if (IsPaidModel(previousSelection))
            SelectedPaidModel = previousSelection;

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

    private static bool IsLocalModel(string modelName)
        => !string.IsNullOrWhiteSpace(modelName) &&
           !modelName.Contains(':') &&
           !IsCloudModel(modelName) &&
           !modelName.EndsWith(":free", StringComparison.OrdinalIgnoreCase);

    private static bool IsPaidModel(string modelName)
        => IsOpenAiModel(modelName) || IsAnthropicModel(modelName) || IsZaiModel(modelName) || IsMiniMaxModel(modelName);

    private static bool IsOpenAiModel(string modelName)
        => !string.IsNullOrWhiteSpace(modelName) &&
           modelName.StartsWith("openai:", StringComparison.OrdinalIgnoreCase);

    private static bool IsAnthropicModel(string modelName)
        => !string.IsNullOrWhiteSpace(modelName) &&
           (modelName.StartsWith("anthropic:", StringComparison.OrdinalIgnoreCase) ||
            modelName.StartsWith("claude:", StringComparison.OrdinalIgnoreCase));

    private static bool IsZaiModel(string modelName)
        => !string.IsNullOrWhiteSpace(modelName) &&
           modelName.StartsWith("zai:", StringComparison.OrdinalIgnoreCase);

    private static bool IsMiniMaxModel(string modelName)
        => !string.IsNullOrWhiteSpace(modelName) &&
           modelName.StartsWith("minimax:", StringComparison.OrdinalIgnoreCase);

    partial void OnNameModelChanged(string value)
    {
        UpdateProviderKeyInputVisibility();
        SelectedModelTierIndex = GetTierIndexForModel(value);

        if (IsFreeTierModel(value))
            SelectedFreeTierModel = value;
        else if (IsLocalModel(value))
            SelectedLocalModel = value;
        else if (IsPaidModel(value))
            SelectedPaidModel = value;
    }

    // Handle tier selection - mutually exclusive (radio-button style)
    partial void OnIsFreeTierSelectedChanged(bool value)
    {
        if (value)
        {
            _isLocalSelected = false;
            _isPaidSelected = false;
            OnPropertyChanged(nameof(IsLocalSelected));
            OnPropertyChanged(nameof(IsPaidSelected));
            // Set a default free tier model if none selected
            if (string.IsNullOrWhiteSpace(NameModel) || !IsFreeTierModel(NameModel))
            {
                NameModel = SelectedFreeTierModel
                    ?? FreeTierNameModels.FirstOrDefault()
                    ?? string.Empty;
            }
        }
    }

    partial void OnIsLocalSelectedChanged(bool value)
    {
        if (value)
        {
            _isFreeTierSelected = false;
            _isPaidSelected = false;
            OnPropertyChanged(nameof(IsFreeTierSelected));
            OnPropertyChanged(nameof(IsPaidSelected));
            // Set a default local model if none selected
            if (string.IsNullOrWhiteSpace(NameModel) || !IsLocalModel(NameModel))
            {
                NameModel = SelectedLocalModel
                    ?? LocalNameModels.FirstOrDefault()
                    ?? string.Empty;
            }
        }
    }

    partial void OnIsPaidSelectedChanged(bool value)
    {
        if (value)
        {
            _isFreeTierSelected = false;
            _isLocalSelected = false;
            OnPropertyChanged(nameof(IsFreeTierSelected));
            OnPropertyChanged(nameof(IsLocalSelected));
            // Set a default paid model if none selected
            if (string.IsNullOrWhiteSpace(NameModel) || !IsPaidModel(NameModel))
            {
                NameModel = SelectedPaidModel
                    ?? PaidNameModels.FirstOrDefault()
                    ?? string.Empty;
            }
        }
    }

    partial void OnSelectedFreeTierModelChanged(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (!IsFreeTierSelected)
            IsFreeTierSelected = true;

        if (!string.Equals(NameModel, value, StringComparison.OrdinalIgnoreCase))
            NameModel = value;
    }

    partial void OnSelectedLocalModelChanged(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (!IsLocalSelected)
            IsLocalSelected = true;

        if (!string.Equals(NameModel, value, StringComparison.OrdinalIgnoreCase))
            NameModel = value;
    }

    partial void OnSelectedPaidModelChanged(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (!IsPaidSelected)
            IsPaidSelected = true;

        if (!string.Equals(NameModel, value, StringComparison.OrdinalIgnoreCase))
            NameModel = value;
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

    [RelayCommand]
    private void ClearLog()
    {
        LogLines.Clear();
        ProcessingStatus = "Log cleared";
    }

    [RelayCommand]
    private async Task SaveLog()
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var filename = $"map_log_{timestamp}.txt";
        var defaultPath = Path.Combine(OutputDirectory ?? Directory.GetCurrentDirectory(), filename);
        await SaveLogToFileAsync(defaultPath);
    }

    public async Task SaveLogToFileAsync(string savePath)
    {
        try
        {
            var lines = LogLines.ToList();
            await File.WriteAllLinesAsync(savePath, lines);
            ProcessingStatus = $"\u2713 Log saved to {savePath}";
        }
        catch (Exception ex)
        {
            ProcessingStatus = $"✗ Error saving log: {ex.Message}";
        }
    }

    private void UpdateProviderKeyInputVisibility()
    {
        ShowPaidApiKeyInput = IsPaidModel(NameModel);
    }

    private static int GetTierIndexForModel(string modelName)
    {
        if (IsPaidModel(modelName))
            return 2;
        if (IsFreeTierModel(modelName))
            return 0;
        return 1;
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

}
