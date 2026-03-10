using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoMapperAI.Models;
using PhotoMapperAI.Services.AI;
using PhotoMapperAI.Services.Database;
using PhotoMapperAI.Services.Diagnostics;
using PhotoMapperAI.Services.Image;
using PhotoMapperAI.UI.Configuration;
using PhotoMapperAI.UI.Execution;
using PhotoMapperAI.UI.Models;
using PhotoMapperAI.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;

namespace PhotoMapperAI.UI.ViewModels;

/// <summary>
/// Represents a photo preview item with its associated CSV mapping information.
/// </summary>
public class PhotoPreviewItem : ObservableObject
{
    private Bitmap? _thumbnail;
    private string _statusText = string.Empty;
    private bool _hasValidMapping;

    public string FilePath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string? External_Player_ID { get; init; }
    public int? PlayerId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string FamilyName { get; init; } = string.Empty;
    public string SurName { get; init; } = string.Empty;
    public double Confidence { get; init; }

    /// <summary>
    /// Mapping status type for display purposes.
    /// </summary>
    public enum MappingStatusType
    {
        ValidMapping,       // Photo found in CSV with ValidMapping=true
        External_Player_IDNotInCsv, // External_Player_ID extracted but not in CSV
        NoExternal_Player_ID        // Could not extract External_Player_ID from filename
    }

    public MappingStatusType StatusType { get; init; } = MappingStatusType.NoExternal_Player_ID;

    public Bitmap? Thumbnail
    {
        get => _thumbnail;
        set => this.SetProperty(ref _thumbnail, value);
    }

    public bool HasValidMapping
    {
        get => _hasValidMapping;
        set => this.SetProperty(ref _hasValidMapping, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => this.SetProperty(ref _statusText, value);
    }
}

public partial class GenerateStepViewModel : ViewModelBase
{
    private const double PreviewMaxDisplayWidth = 240;
    private const double PreviewMaxDisplayHeight = 180;
    private readonly ExternalGenerateCliRunner _cliRunner;
    private CancellationTokenSource? _cancellationTokenSource;
    private CancellationTokenSource? _autoPreviewCts;

    public GenerateStepViewModel()
    {
        _cliRunner = new ExternalGenerateCliRunner();

        // Prefer a convenient default profile path when available.
        // User can clear it to use manual single-size mode.
        var defaultProfile = ResolveDefaultSizeProfilePath();
        if (!string.IsNullOrWhiteSpace(defaultProfile))
        {
            SizeProfilePath = defaultProfile;
        }

        ConfigureFaceDetectionModelsForPlatform();

        SelectedFaceModelTierIndex = GetTierIndexForModel(FaceDetectionModel);

        var configuredPaidModels = UiModelConfigLoader.Load().GeneratePaidModels;
        if (configuredPaidModels.Count == 0)
        {
            PaidFaceDetectionModels.Add("NotYetImplemented:ComingSoon");
        }
        else
        {
            foreach (var model in configuredPaidModels)
                PaidFaceDetectionModels.Add(model);
        }

        LoadCropOffsetPresets();
    }

    [ObservableProperty]
    private string _inputCsvPath = string.Empty;

    [ObservableProperty]
    private string _photosDirectory = string.Empty;

    [ObservableProperty]
    private string _outputDirectory = string.Empty;

    [ObservableProperty]
    private string _imageFormat = "jpg";

    [ObservableProperty]
    private string _faceDetectionModel = "opencv-yunet";

    [ObservableProperty]
    private string _sizeProfilePath = string.Empty;

    public bool ManualSizeControlsEnabled => string.IsNullOrWhiteSpace(SizeProfilePath);
    public bool CanUseAllSizes => !string.IsNullOrWhiteSpace(SizeProfilePath);

    [ObservableProperty]
    private bool _allSizes;

    [ObservableProperty]
    private string _outputProfile = "none";

    [ObservableProperty]
    private int _portraitWidth = 200;

    [ObservableProperty]
    private int _portraitHeight = 300;

    [ObservableProperty]
    private bool _portraitOnly;

    [ObservableProperty]
    private bool _downloadOpenCvModels;

    [ObservableProperty]
    private bool _writeDebugArtifacts = true;

    [ObservableProperty]
    private string? _onlyPlayerId;

    [ObservableProperty]
    private bool _usePlaceholderImages;

    [ObservableProperty]
    private string? _placeholderImagePath;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _processingStatus = string.Empty;

    [ObservableProperty]
    private int _portraitsGenerated;

    [ObservableProperty]
    private int _portraitsFailed;

    [ObservableProperty]
    private bool _isComplete;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isCheckingModel;

    [ObservableProperty]
    private string _modelDiagnosticStatus = string.Empty;

    [ObservableProperty]
    private Bitmap? _previewImage;

    [ObservableProperty]
    private double _previewDisplayWidth;

    [ObservableProperty]
    private double _previewDisplayHeight;

    [ObservableProperty]
    private string _previewStatus = string.Empty;

    [ObservableProperty]
    private string _previewPlayerLabel = string.Empty;

    [ObservableProperty]
    private bool _isPreviewing;

    [ObservableProperty]
    private bool _autoPreviewEnabled;

    [ObservableProperty]
    private ObservableCollection<PhotoPreviewItem> _photoPreviewItems = new();

    [ObservableProperty]
    private bool _isLoadingPhotoGrid;

    [ObservableProperty]
    private ObservableCollection<GenerateIssueSummaryItem> _generateIssueItems = new();

    [ObservableProperty]
    private bool _hasGenerateIssues;

    [ObservableProperty]
    private string _generateIssueTitle = "Mapping issues: none";

    [ObservableProperty]
    private PhotoPreviewItem? _selectedPhotoItem;

    [ObservableProperty]
    private bool _isPhotoDialogOpen;

    [ObservableProperty]
    private int _selectedFaceModelTierIndex;

    [ObservableProperty]
    private double _cropOffsetXPercent;

    [ObservableProperty]
    private double _cropOffsetYPercent;

    [ObservableProperty]
    private CropOffsetPreset? _selectedCropOffsetPreset;

    [ObservableProperty]
    private string _cropOffsetStatus = string.Empty;

    [ObservableProperty]
    private PreviewDimensionPreset? _selectedPreviewDimensionPreset;

    [ObservableProperty]
    private string _previewDimensionStatus = string.Empty;

    // Custom preview dimensions (separate from profile)
    [ObservableProperty]
    private int _previewCustomWidth = 200;

    [ObservableProperty]
    private int _previewCustomHeight = 300;

    [ObservableProperty]
    private bool _useCustomPreviewDimensions;

    public ObservableCollection<string> LogLines { get; } = new();

    public List<string> ImageFormats { get; } = new() { "jpg", "png" };

    public List<string> OutputProfiles { get; } = new() { "none", "test", "prod" };

    public ObservableCollection<string> RecommendedFaceDetectionModels { get; } = new();

    public ObservableCollection<string> LocalVisionFaceDetectionModels { get; } = new()
    {
        "apple-vision",
        "llava:7b",
        "qwen3-vl"
    };

    public ObservableCollection<string> AdvancedFaceDetectionModels { get; } = new()
    {
        "yolov8-face",
        "haar-cascade",
        "center"
    };

    public ObservableCollection<CropOffsetPreset> CropOffsetPresets { get; } = new();

    public ObservableCollection<PreviewDimensionPreset> PreviewDimensionPresets { get; } = new();

    public ObservableCollection<string> PaidFaceDetectionModels { get; } = new();

    [RelayCommand]
    private async Task BrowseCsvFile()
    {
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void ClearInputCsv()
    {
        InputCsvPath = string.Empty;
        IsComplete = false;
        ResetGenerateIssues();
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
        ResetGenerateIssues();
    }

    [RelayCommand]
    private async Task BrowseOutputDirectory()
    {
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void ClearOutputDirectory()
    {
        OutputDirectory = string.Empty;
        IsComplete = false;
    }

    [RelayCommand]
    private void ClearSizeProfilePath()
    {
        SizeProfilePath = string.Empty;
        AllSizes = false;
        IsComplete = false;
    }

    partial void OnSizeProfilePathChanged(string value)
    {
        OnPropertyChanged(nameof(ManualSizeControlsEnabled));
        OnPropertyChanged(nameof(CanUseAllSizes));

        if (string.IsNullOrWhiteSpace(value))
        {
            AllSizes = false;
        }
    }

    partial void OnFaceDetectionModelChanged(string value)
    {
        SelectedFaceModelTierIndex = GetTierIndexForModel(value);
    }

    partial void OnSelectedCropOffsetPresetChanged(CropOffsetPreset? value)
    {
        if (value == null)
        {
            return;
        }

        CropOffsetXPercent = value.HorizontalPercent;
        CropOffsetYPercent = value.VerticalPercent;
    }

    partial void OnSelectedPreviewDimensionPresetChanged(PreviewDimensionPreset? value)
    {
        if (value == null)
        {
            return;
        }

        PreviewCustomWidth = value.Width;
        PreviewCustomHeight = value.Height;
        UseCustomPreviewDimensions = true;
    }

    partial void OnPreviewCustomWidthChanged(int value)
    {
        TriggerAutoPreviewIfNeeded();
    }

    partial void OnPreviewCustomHeightChanged(int value)
    {
        TriggerAutoPreviewIfNeeded();
    }

    partial void OnUseCustomPreviewDimensionsChanged(bool value)
    {
        TriggerAutoPreviewIfNeeded();
    }

    partial void OnPreviewImageChanged(Bitmap? value)
    {
        UpdatePreviewDisplaySize(value);
    }

    private void TriggerAutoPreviewIfNeeded()
    {
        if (!AutoPreviewEnabled || IsProcessing)
        {
            return;
        }

        // Debounce: cancel any pending auto-preview
        _autoPreviewCts?.Cancel();
        _autoPreviewCts = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            await Task.Delay(300, _autoPreviewCts.Token);
            if (!_autoPreviewCts.Token.IsCancellationRequested)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (GeneratePreviewCommand.CanExecute(null))
                    {
                        GeneratePreviewCommand.Execute(null);
                    }
                });
            }
        });
    }

    [RelayCommand]
    private void SaveCropOffsetPreset()
    {
        var presetName = SelectedCropOffsetPreset?.Name;
        if (string.IsNullOrWhiteSpace(presetName))
        {
            presetName = "default";
        }

        var preset = CropOffsetPresets.FirstOrDefault(p =>
            string.Equals(p.Name, presetName, StringComparison.OrdinalIgnoreCase));

        if (preset == null)
        {
            preset = new CropOffsetPreset { Name = presetName };
            CropOffsetPresets.Add(preset);
        }

        preset.HorizontalPercent = CropOffsetXPercent;
        preset.VerticalPercent = CropOffsetYPercent;

        var settings = BuildCropOffsetSettingsSnapshot(presetName);

        CropOffsetSettingsLoader.SaveToLocal(settings);
        CropOffsetStatus = $"Saved offset preset '{presetName}'.";
        SelectedCropOffsetPreset = preset;
    }

    [RelayCommand]
    private void NewCropOffsetPreset()
    {
        // Generate a unique name
        var baseName = "new_preset";
        var counter = 1;
        var newName = $"{baseName}_{counter}";
        
        while (CropOffsetPresets.Any(p => string.Equals(p.Name, newName, StringComparison.OrdinalIgnoreCase)))
        {
            counter++;
            newName = $"{baseName}_{counter}";
        }

        var newPreset = new CropOffsetPreset
        {
            Name = newName,
            HorizontalPercent = CropOffsetXPercent,
            VerticalPercent = CropOffsetYPercent
        };

        CropOffsetPresets.Add(newPreset);
        SelectedCropOffsetPreset = newPreset;
        CropOffsetStatus = $"Created new preset '{newName}'. Click 'Update Preset' to persist.";
    }

    [RelayCommand]
    private void SavePreviewDimensionPreset()
    {
        var presetName = SelectedPreviewDimensionPreset?.Name;
        if (string.IsNullOrWhiteSpace(presetName))
        {
            presetName = "default";
        }

        var preset = PreviewDimensionPresets.FirstOrDefault(p =>
            string.Equals(p.Name, presetName, StringComparison.OrdinalIgnoreCase));

        if (preset == null)
        {
            preset = new PreviewDimensionPreset { Name = presetName };
            PreviewDimensionPresets.Add(preset);
        }

        preset.Width = PreviewCustomWidth;
        preset.Height = PreviewCustomHeight;

        var settings = BuildCropOffsetSettingsSnapshot(SelectedCropOffsetPreset?.Name ?? "default");
        settings.ActivePreviewDimensionPresetName = presetName;
        settings.PreviewDimensionPresets = PreviewDimensionPresets
            .Select(p => new PreviewDimensionPreset
            {
                Name = p.Name,
                Width = p.Width,
                Height = p.Height
            })
            .ToList();

        CropOffsetSettingsLoader.SaveToLocal(settings);
        PreviewDimensionStatus = $"Saved dimension preset '{presetName}'.";
        SelectedPreviewDimensionPreset = preset;
    }

    [RelayCommand]
    private void NewPreviewDimensionPreset()
    {
        var baseName = "new_dimensions";
        var counter = 1;
        var newName = $"{baseName}_{counter}";

        while (PreviewDimensionPresets.Any(p =>
                   string.Equals(p.Name, newName, StringComparison.OrdinalIgnoreCase)))
        {
            counter++;
            newName = $"{baseName}_{counter}";
        }

        var newPreset = new PreviewDimensionPreset
        {
            Name = newName,
            Width = PreviewCustomWidth,
            Height = PreviewCustomHeight
        };

        PreviewDimensionPresets.Add(newPreset);
        SelectedPreviewDimensionPreset = newPreset;
        UseCustomPreviewDimensions = true;
        PreviewDimensionStatus = $"Created new preset '{newName}'. Click 'Update Preset' to persist.";
    }

    [RelayCommand]
    private void DecrementCustomWidth()
    {
        if (PreviewCustomWidth > 50)
        {
            PreviewCustomWidth -= 10;
        }
    }

    [RelayCommand]
    private void IncrementCustomWidth()
    {
        PreviewCustomWidth += 10;
    }

    [RelayCommand]
    private void DecrementCustomHeight()
    {
        if (PreviewCustomHeight > 50)
        {
            PreviewCustomHeight -= 10;
        }
    }

    [RelayCommand]
    private void IncrementCustomHeight()
    {
        PreviewCustomHeight += 10;
    }

    [RelayCommand]
    private async Task LoadPreviewImage()
    {
        await Task.CompletedTask;

        if (string.IsNullOrWhiteSpace(PhotosDirectory) || !Directory.Exists(PhotosDirectory))
        {
            PreviewStatus = "Select a valid photos directory to load preview.";
            PreviewImage = null;
            return;
        }

        var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".bmp"
        };

        var firstImagePath = Directory.EnumerateFiles(PhotosDirectory, "*.*", SearchOption.AllDirectories)
            .FirstOrDefault(path => supportedExtensions.Contains(Path.GetExtension(path)));

        if (string.IsNullOrWhiteSpace(firstImagePath))
        {
            PreviewStatus = "No supported images found in photos directory.";
            PreviewImage = null;
            return;
        }

        try
        {
            await using var stream = File.OpenRead(firstImagePath);
            PreviewImage = new Bitmap(stream);
            PreviewStatus = $"Preview loaded: {Path.GetFileName(firstImagePath)}";
        }
        catch (Exception ex)
        {
            PreviewStatus = $"Failed to load preview: {ex.Message}";
            PreviewImage = null;
        }
    }

    [ObservableProperty]
    private bool _strictModeEnabled;

    [RelayCommand]
    private async Task GeneratePreview()
    {
        if (IsProcessing || IsPreviewing)
        {
            return;
        }

        PreviewImage = null;
        PreviewStatus = string.Empty;
        PreviewPlayerLabel = string.Empty;
        IsPreviewing = true;

        var diagnostics = new List<string>();
        void LogDiagnostic(string message)
        {
            diagnostics.Add(message);
            AppendLog($"[PREVIEW] {message}");
        }

        try
        {
            if (string.IsNullOrWhiteSpace(InputCsvPath) || !File.Exists(InputCsvPath))
            {
                PreviewStatus = "Select a valid CSV file to generate preview.";
                return;
            }

            if (string.IsNullOrWhiteSpace(PhotosDirectory) || !Directory.Exists(PhotosDirectory))
            {
                PreviewStatus = "Select a valid photos directory to generate preview.";
                return;
            }

            var player = await ResolvePreviewPlayerAsync();
            if (player == null)
            {
                PreviewStatus = "No eligible player found in CSV for preview.";
                return;
            }

            if (string.IsNullOrWhiteSpace(player.External_Player_ID))
            {
                PreviewStatus = "Selected player does not have an External_Player_ID.";
                return;
            }

            var wantsAllSizes = AllSizes && !string.IsNullOrWhiteSpace(SizeProfilePath);
            var wantsCustomDimensions = UseCustomPreviewDimensions && !wantsAllSizes;
            var useSizeProfile = !string.IsNullOrWhiteSpace(SizeProfilePath) && !wantsCustomDimensions;
            var sizeProfilePath = useSizeProfile ? SizeProfilePath : null;
            var ignoreProfilePlaceholders = useSizeProfile && !UsePlaceholderImages;

            int previewWidth;
            int previewHeight;
            int? previewCropFrameWidth = null;
            int? previewCropFrameHeight = null;
            string? placeholderPath;

            if (useSizeProfile)
            {
                var (variantWidth, variantHeight, variantPlaceholder) = ResolvePreviewVariant();
                previewWidth = variantWidth;
                previewHeight = variantHeight;
                placeholderPath = ignoreProfilePlaceholders ? null : variantPlaceholder;
                LogDiagnostic($"Using size profile preview dimensions: {previewWidth}x{previewHeight}");
            }
            else
            {
                previewWidth = PortraitWidth;
                previewHeight = PortraitHeight;
                if (wantsCustomDimensions)
                {
                    previewCropFrameWidth = PreviewCustomWidth;
                    previewCropFrameHeight = PreviewCustomHeight;
                }
                placeholderPath = UsePlaceholderImages ? PlaceholderImagePath : null;
                LogDiagnostic(previewCropFrameWidth.HasValue || previewCropFrameHeight.HasValue
                    ? $"Using manual output {previewWidth}x{previewHeight} with crop frame {previewCropFrameWidth ?? previewWidth}x{previewCropFrameHeight ?? previewHeight}"
                    : $"Using manual preview dimensions: {previewWidth}x{previewHeight}");
            }

            var photoPath = ResolvePreviewPhotoPath(player.External_Player_ID);

            if (string.IsNullOrWhiteSpace(photoPath))
            {
                if (!string.IsNullOrWhiteSpace(placeholderPath) && File.Exists(placeholderPath))
                {
                    await using var placeholderStream = File.OpenRead(placeholderPath);
                    PreviewImage = new Bitmap(placeholderStream);
                    PreviewStatus = "Previewed placeholder image.";
                    PreviewPlayerLabel = BuildPreviewPlayerLabel(player);
                    return;
                }

                PreviewStatus = $"No photo found for External_Player_ID {player.External_Player_ID}.";
                return;
            }

            LogDiagnostic($"Preview generation path: external-cli");
            LogDiagnostic($"Face detection model requested: {FaceDetectionModel}");
            PreviewStatus = "Generating preview...";

            var previewOutputDir = Path.Combine(
                Path.GetTempPath(),
                "PhotoMapperAI",
                "preview",
                "generate",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(previewOutputDir);

            var previewResult = await _cliRunner.ExecuteAsync(
                Directory.GetCurrentDirectory(),
                InputCsvPath,
                PhotosDirectory,
                previewOutputDir,
                ImageFormat,
                FaceDetectionModel,
                PortraitOnly,
                previewWidth,
                previewHeight,
                sizeProfilePath,
                allSizes: false,
                ignoreProfilePlaceholders,
                DownloadOpenCvModels,
                player.PlayerId.ToString(),
                placeholderPath,
                previewCropFrameWidth,
                previewCropFrameHeight,
                BuildCurrentCropOffsetPreset(),
                _cancellationTokenSource?.Token ?? CancellationToken.None,
                new Progress<string>(msg => LogDiagnostic(msg)));

            if (previewResult.ExitCode != 0)
            {
                PreviewStatus = $"Preview failed: external generation exited with {previewResult.ExitCode}.";
                return;
            }

            var previewFileName = $"{player.PlayerId}.{NormalizePreviewExtension(ImageFormat)}";
            var previewFilePath = Path.Combine(previewOutputDir, previewFileName);
            if (!File.Exists(previewFilePath))
            {
                PreviewStatus = $"Preview failed: generated file not found ({previewFileName}).";
                return;
            }

            await using var previewStream = File.OpenRead(previewFilePath);
            PreviewImage = new Bitmap(previewStream);

            PreviewPlayerLabel = BuildPreviewPlayerLabel(player);
            PreviewStatus = previewCropFrameWidth.HasValue || previewCropFrameHeight.HasValue
                ? $"Preview generated (output {previewWidth}x{previewHeight}, crop frame {previewCropFrameWidth ?? previewWidth}x{previewCropFrameHeight ?? previewHeight})."
                : $"Preview generated ({previewWidth}x{previewHeight}).";
            LogDiagnostic($"Preview encoded message: {PreviewStatus}");

            if (StrictModeEnabled && diagnostics.Count > 0)
            {
                var diagnosticSummary = string.Join("\n", diagnostics);
                PreviewStatus = $"[STRICT] {PreviewStatus}\n\nDiagnostics:\n{diagnosticSummary}";
            }
        }
        catch (Exception ex)
        {
            LogDiagnostic($"Error: {ex.Message}");
            if (StrictModeEnabled && diagnostics.Count > 0)
            {
                var diagnosticSummary = string.Join("\n", diagnostics);
                PreviewStatus = $"[STRICT] Preview failed: {ex.Message}\n\nDiagnostics:\n{diagnosticSummary}";
            }
            else
            {
                PreviewStatus = $"Preview failed: {ex.Message}";
            }
        }
        finally
        {
            IsPreviewing = false;
        }
    }

    [RelayCommand]
    private async Task LoadPhotoGrid()
    {
        PhotoPreviewItems.Clear();

        if (string.IsNullOrWhiteSpace(PhotosDirectory) || !Directory.Exists(PhotosDirectory))
        {
            PreviewStatus = "Select a valid photos directory to load photo grid.";
            ResetGenerateIssues();
            return;
        }

        IsLoadingPhotoGrid = true;
        PreviewStatus = "Loading photos and CSV data...";

        try
        {
            // Load CSV data if available
            Dictionary<string, PlayerRecord> csvDataByExternal_Player_ID = new(StringComparer.OrdinalIgnoreCase);
            List<PlayerRecord> csvPlayersWithoutPhoto = new();
            int totalCsvPlayers = 0;

            if (!string.IsNullOrWhiteSpace(InputCsvPath) && File.Exists(InputCsvPath))
            {
                var extractor = new DatabaseExtractor();
                var players = await extractor.ReadCsvAsync(InputCsvPath);
                totalCsvPlayers = players.Count;

                foreach (var player in players)
                {
                    // Only add players with External_Player_ID to the lookup dictionary
                    if (!string.IsNullOrEmpty(player.External_Player_ID))
                    {
                        csvDataByExternal_Player_ID[player.External_Player_ID] = player;
                    }
                    else
                    {
                        // Track players without External_Player_ID
                        csvPlayersWithoutPhoto.Add(player);
                    }
                }
            }

            // Get all supported image files
            var imageFiles = BuildImageFileList(PhotosDirectory)
                .OrderBy(Path.GetFileName)
                .ToList();

            if (imageFiles.Count == 0)
            {
                PreviewStatus = "No supported images found in photos directory.";
                ResetGenerateIssues();
                return;
            }

            // Parse each photo and match with CSV data
            var items = new List<PhotoPreviewItem>();

            // Track which External_Player_IDs from CSV were found in photos
            var foundExternal_Player_IDs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var imagePath in imageFiles)
            {
                var fileName = Path.GetFileName(imagePath);
                
                // Try to extract External_Player_ID from filename
                var metadata = FilenameParser.ParseAutoDetect(fileName);
                
                string? External_Player_ID = metadata?.External_Player_ID;
                PlayerRecord? matchedPlayer = null;

                // Try to match with CSV data
                if (!string.IsNullOrEmpty(External_Player_ID) && csvDataByExternal_Player_ID.TryGetValue(External_Player_ID, out var player))
                {
                    matchedPlayer = player;
                    foundExternal_Player_IDs.Add(External_Player_ID);
                }

                var item = new PhotoPreviewItem
                {
                    FilePath = imagePath,
                    FileName = fileName,
                    External_Player_ID = External_Player_ID,
                    PlayerId = matchedPlayer?.PlayerId,
                    FullName = matchedPlayer?.FullName ?? metadata?.FullName ?? string.Empty,
                    FamilyName = matchedPlayer?.FamilyName ?? metadata?.FamilyName ?? string.Empty,
                    SurName = matchedPlayer?.SurName ?? metadata?.SurName ?? string.Empty,
                    Confidence = matchedPlayer?.Confidence ?? 0.0,
                    HasValidMapping = matchedPlayer?.ValidMapping ?? false
                };

                // Build status text based on mapping state
                if (matchedPlayer != null)
                {
                    // Photo found in CSV with valid mapping
                    item.StatusText = $"✓ {matchedPlayer.FullName} (ID: {matchedPlayer.PlayerId})";
                }
                else if (!string.IsNullOrEmpty(External_Player_ID))
                {
                    // External_Player_ID extracted but not found in CSV
                    item.StatusText = $"⚠ External ID: {External_Player_ID} (not in CSV)";
                }
                else
                {
                    // No External_Player_ID could be extracted from filename
                    item.StatusText = "? No ID in filename";
                }

                items.Add(item);
            }

            // Calculate statistics
            var totalPhotos = items.Count;
            var mappedCount = items.Count(i => i.HasValidMapping);
            var External_Player_IDNotInCsvCount = items.Count(i => !i.HasValidMapping && !string.IsNullOrEmpty(i.External_Player_ID));
            var noIdInFilenameCount = items.Count(i => string.IsNullOrEmpty(i.External_Player_ID));

            // Collect file names for issue details
            var External_Player_IDNotInCsvFiles = items
                .Where(i => !i.HasValidMapping && !string.IsNullOrEmpty(i.External_Player_ID))
                .Select(i => i.FileName)
                .ToList();
            var noIdInFilenameFiles = items
                .Where(i => string.IsNullOrEmpty(i.External_Player_ID))
                .Select(i => i.FileName)
                .ToList();

            // Find CSV players that don't have photos
            var unmatchedCsvPlayers = csvPlayersWithoutPhoto.Count;
            var unmatchedCsvPlayerNames = csvPlayersWithoutPhoto
                .Select(p => string.IsNullOrWhiteSpace(p.FullName) ? $"ID:{p.PlayerId}" : p.FullName)
                .ToList();
            if (totalCsvPlayers > 0)
            {
                // Also count players in CSV who have External_Player_ID but no matching photo
                foreach (var kvp in csvDataByExternal_Player_ID)
                {
                    if (!foundExternal_Player_IDs.Contains(kvp.Key))
                    {
                        unmatchedCsvPlayers++;
                        var p = kvp.Value;
                        unmatchedCsvPlayerNames.Add(
                            string.IsNullOrWhiteSpace(p.FullName) ? $"ID:{p.External_Player_ID}" : p.FullName);
                    }
                }
            }

            // Load thumbnails (limited to first 50 for performance)
            var itemsToLoad = items.Take(50).ToList();
            foreach (var item in itemsToLoad)
            {
                try
                {
                    // Load image with ImageSharp and resize to thumbnail size
                    using var image = await Image.LoadAsync(item.FilePath);

                    // Calculate thumbnail size (max 160x80, preserve aspect ratio)
                    int thumbnailWidth = 160;
                    int thumbnailHeight = 80;
                    double aspectRatio = (double)image.Width / image.Height;

                    if (aspectRatio > (thumbnailWidth / (double)thumbnailHeight))
                    {
                        // Image is wider than target - scale by width
                        thumbnailHeight = (int)(thumbnailWidth / aspectRatio);
                    }
                    else
                    {
                        // Image is taller than target - scale by height
                        thumbnailWidth = (int)(thumbnailHeight * aspectRatio);
                    }

                    // Resize image using high-quality resampling
                    image.Mutate(x => x
                        .Resize(new ResizeOptions
                        {
                            Size = new Size(thumbnailWidth, thumbnailHeight),
                            Sampler = KnownResamplers.Lanczos3
                        }));

                    // Convert to Avalonia Bitmap
                    using var outputStream = new MemoryStream();
                    await image.SaveAsJpegAsync(outputStream);
                    outputStream.Position = 0;
                    item.Thumbnail = new Bitmap(outputStream);
                }
                catch
                {
                    // Skip thumbnails that fail to load
                }
            }

            foreach (var item in items)
            {
                PhotoPreviewItems.Add(item);
            }

            // Build detailed status message
            var statusParts = new List<string>();
            statusParts.Add($"{totalPhotos} photo(s)");

            if (mappedCount > 0)
                statusParts.Add($"{mappedCount} mapped");

            if (External_Player_IDNotInCsvCount > 0)
                statusParts.Add($"{External_Player_IDNotInCsvCount} not in CSV");

            if (noIdInFilenameCount > 0)
                statusParts.Add($"{noIdInFilenameCount} no ID");

            if (unmatchedCsvPlayers > 0)
                statusParts.Add($"{unmatchedCsvPlayers} CSV player(s) without photo");

            var statusMessage = string.Join(" | ", statusParts);

            if (totalPhotos > 50)
            {
                PreviewStatus = $"Showing 50 of {statusMessage}";
            }
            else
            {
                PreviewStatus = statusMessage;
            }

            UpdateGenerateIssues(new GenerateIssueCounts(
                External_Player_IDNotInCsvCount,
                noIdInFilenameCount,
                unmatchedCsvPlayers,
                unmatchedCsvPlayerNames,
                External_Player_IDNotInCsvFiles,
                noIdInFilenameFiles));
        }
        catch (Exception ex)
        {
            PreviewStatus = $"Failed to load photo grid: {ex.Message}";
        }
        finally
        {
            IsLoadingPhotoGrid = false;
        }
    }

    [RelayCommand]
    private void ClosePhotoDialog()
    {
        IsPhotoDialogOpen = false;
        SelectedPhotoItem = null;
    }

    [RelayCommand]
    private void SelectPhoto(PhotoPreviewItem? item)
    {
        if (item != null)
        {
            SelectedPhotoItem = item;
            IsPhotoDialogOpen = true;
        }
    }

    [RelayCommand]
    private async Task CheckFaceModel()
    {
        if (IsProcessing)
            return;

        IsCheckingModel = true;
        ModelDiagnosticStatus = $"Checking model '{FaceDetectionModel}'...";

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
    private async Task ExecuteGenerate()
    {
        if (string.IsNullOrWhiteSpace(InputCsvPath) ||
            string.IsNullOrWhiteSpace(PhotosDirectory) ||
            string.IsNullOrWhiteSpace(OutputDirectory))
        {
            ProcessingStatus = "Please select CSV file, photos directory, and output directory";
            return;
        }

        LogLines.Clear();
        IsProcessing = true;
        IsComplete = false;
        PortraitsGenerated = 0;
        PortraitsFailed = 0;
        ProcessingStatus = "Generating portraits...";
        Progress = 0;
        ResetGenerateIssues();
        _cancellationTokenSource = new CancellationTokenSource();

        var selectedFaceDetectionModel = FaceDetectionModel;

        try
        {
            var preflight = await PreflightChecker.CheckGenerateAsync(selectedFaceDetectionModel, DownloadOpenCvModels);
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

            var resolvedService = FaceDetectionServiceFactory.Create(selectedFaceDetectionModel);
            AppendLog($"Resolved face detection service: {resolvedService.ModelName}");

            var issueCounts = await ComputeGenerateIssueCountsAsync();
            UpdateGenerateIssues(issueCounts);

            var baseOutputDirectory = OutputDirectory;
            if (!string.Equals(OutputProfile, "none", StringComparison.OrdinalIgnoreCase))
            {
                baseOutputDirectory = ResolveOutputProfile(OutputProfile, OutputDirectory);
                AppendLog($"Using output profile '{OutputProfile}' => {baseOutputDirectory}");
            }

            // Determine mode:
            // - If AllSizes is checked -> use the full size profile.
            // - If custom dimensions are enabled (and AllSizes is off) -> custom dimensions override the profile.
            // - Otherwise use the selected size profile in single-variant mode.
            var wantsAllSizes = AllSizes && !string.IsNullOrWhiteSpace(SizeProfilePath);
            var wantsCustomDimensions = UseCustomPreviewDimensions && !wantsAllSizes;
            var useSizeProfile = !string.IsNullOrWhiteSpace(SizeProfilePath) && !wantsCustomDimensions;
            var sizeProfilePath = useSizeProfile ? SizeProfilePath : null;
            var allSizes = wantsAllSizes;
            var ignoreProfilePlaceholders = useSizeProfile && !UsePlaceholderImages;
            var placeholderPath = useSizeProfile ? null : (UsePlaceholderImages ? PlaceholderImagePath : null);

            // Determine actual dimensions to use for generation
            int effectiveWidth, effectiveHeight;
            int? cropFrameWidth = null;
            int? cropFrameHeight = null;
            if (wantsCustomDimensions)
            {
                effectiveWidth = PortraitWidth;
                effectiveHeight = PortraitHeight;
                cropFrameWidth = PreviewCustomWidth;
                cropFrameHeight = PreviewCustomHeight;
                AppendLog($"Using custom crop frame: {cropFrameWidth}x{cropFrameHeight} with output size {effectiveWidth}x{effectiveHeight}");
            }
            else
            {
                effectiveWidth = PortraitWidth;
                effectiveHeight = PortraitHeight;
            }

            if (useSizeProfile)
            {
                var profile = LoadSizeProfile(SizeProfilePath);
                var variantMode = allSizes ? "all variants" : "single variant";
                AppendLog($"Using size profile '{profile.Name}' ({variantMode})");
                if (!allSizes)
                {
                    baseOutputDirectory = ResolveSingleProfileOutputDirectory(baseOutputDirectory, profile);
                    AppendLog($"Single-profile output folder => {baseOutputDirectory}");
                }
            }
            else
            {
                baseOutputDirectory = ResolveDefaultSingleVariantOutputDirectory(baseOutputDirectory);
                AppendLog($"Single-size output folder => {baseOutputDirectory}");
            }

            Directory.CreateDirectory(baseOutputDirectory);
            if (useSizeProfile)
            {
                AppendLog($"Running size profile => {baseOutputDirectory}");
            }
            else
            {
                AppendLog($"Running variant 'single' => {effectiveWidth}x{effectiveHeight} -> {baseOutputDirectory}");
            }

            AppendLog(_cliRunner.BuildCommandPreview(
                Directory.GetCurrentDirectory(),
                InputCsvPath,
                PhotosDirectory,
                baseOutputDirectory,
                ImageFormat,
                selectedFaceDetectionModel,
                PortraitOnly,
                effectiveWidth,
                effectiveHeight,
                sizeProfilePath,
                allSizes,
                ignoreProfilePlaceholders,
                DownloadOpenCvModels,
                OnlyPlayerId,
                placeholderPath,
                cropFrameWidth,
                cropFrameHeight));

            var log = new Progress<string>(AppendLog);
            var progress = new Progress<(int processed, int total, string current)>(state =>
            {
                if (state.total <= 0)
                {
                    Progress = 0;
                    return;
                }

                Progress = Math.Clamp((double)state.processed / state.total * 100.0, 0, 100);
                var currentLabel = string.IsNullOrWhiteSpace(state.current) ? string.Empty : $" {state.current}";
                ProcessingStatus = $"Processed {state.processed}/{state.total} players{currentLabel}";
            });

            if (WriteDebugArtifacts)
            {
                try
                {
                    var debugPath = _cliRunner.WriteDebugArtifact(
                        baseOutputDirectory,
                        Directory.GetCurrentDirectory(),
                        InputCsvPath,
                        PhotosDirectory,
                        ImageFormat,
                        selectedFaceDetectionModel,
                        PortraitOnly,
                        effectiveWidth,
                        effectiveHeight,
                        sizeProfilePath,
                        allSizes,
                        ignoreProfilePlaceholders,
                        DownloadOpenCvModels,
                        OnlyPlayerId,
                        placeholderPath,
                        cropFrameWidth,
                        cropFrameHeight);
                    AppendLog($"Debug artifact: {debugPath}");
                }
                catch (Exception ex)
                {
                    AppendLog($"Debug artifact write failed: {ex.Message}");
                }
            }

            var result = await _cliRunner.ExecuteAsync(
                Directory.GetCurrentDirectory(),
                InputCsvPath,
                PhotosDirectory,
                baseOutputDirectory,
                ImageFormat,
                selectedFaceDetectionModel,
                PortraitOnly,
                effectiveWidth,
                effectiveHeight,
                sizeProfilePath,
                allSizes,
                ignoreProfilePlaceholders,
                DownloadOpenCvModels,
                OnlyPlayerId,
                placeholderPath,
                cropFrameWidth,
                cropFrameHeight,
                BuildCurrentCropOffsetPreset(),
                _cancellationTokenSource.Token,
                log,
                progress);

            PortraitsGenerated = result.PortraitsGenerated;
            PortraitsFailed = result.PortraitsFailed;

            if (result.IsCancelled)
            {
                ProcessingStatus = $"⚠ Generation cancelled ({result.ProcessedPlayers}/{result.TotalPlayers} processed)";
                IsComplete = false;
                return;
            }

            if (result.ExitCode != 0)
            {
                ProcessingStatus = $"✗ Generation failed with exit code {result.ExitCode}";
                IsComplete = false;
                return;
            }

            ProcessingStatus = $"✓ Generated {PortraitsGenerated} portraits ({PortraitsFailed} failed)";
            IsComplete = true;
            Progress = 100;
        }
        catch (OperationCanceledException)
        {
            ProcessingStatus = "⚠ Generation cancelled";
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
    private void CancelGenerate()
    {
        if (IsProcessing)
        {
            _cancellationTokenSource?.Cancel();
            ProcessingStatus = "Cancelling generation...";
        }
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

        // Load PreviewCustomDimensions if present
        if (settings.PreviewCustomDimensions != null)
        {
            PreviewCustomWidth = settings.PreviewCustomDimensions.Width;
            PreviewCustomHeight = settings.PreviewCustomDimensions.Height;
            UseCustomPreviewDimensions = settings.PreviewCustomDimensions.UseCustom;
        }

        PreviewDimensionPresets.Clear();
        foreach (var preset in settings.PreviewDimensionPresets)
        {
            PreviewDimensionPresets.Add(new PreviewDimensionPreset
            {
                Name = preset.Name,
                Width = preset.Width,
                Height = preset.Height
            });
        }

        if (PreviewDimensionPresets.Count == 0)
        {
            PreviewDimensionPresets.Add(new PreviewDimensionPreset
            {
                Name = "default",
                Width = PreviewCustomWidth,
                Height = PreviewCustomHeight
            });
        }

        var activeDimensions = settings.GetActivePreviewDimensionPreset();
        SelectedPreviewDimensionPreset = PreviewDimensionPresets.FirstOrDefault(p =>
            string.Equals(p.Name, activeDimensions.Name, StringComparison.OrdinalIgnoreCase))
            ?? PreviewDimensionPresets[0];
    }

    private CropOffsetSettings BuildCropOffsetSettingsSnapshot(string activePresetName)
    {
        return new CropOffsetSettings
        {
            ActivePresetName = activePresetName,
            Presets = CropOffsetPresets
                .Select(p => new CropOffsetPreset
                {
                    Name = p.Name,
                    HorizontalPercent = p.HorizontalPercent,
                    VerticalPercent = p.VerticalPercent
                })
                .ToList(),
            PreviewCustomDimensions = new PreviewCustomDimensions
            {
                Width = PreviewCustomWidth,
                Height = PreviewCustomHeight,
                UseCustom = UseCustomPreviewDimensions
            },
            ActivePreviewDimensionPresetName = SelectedPreviewDimensionPreset?.Name ?? "default",
            PreviewDimensionPresets = PreviewDimensionPresets
                .Select(p => new PreviewDimensionPreset
                {
                    Name = p.Name,
                    Width = p.Width,
                    Height = p.Height
                })
                .ToList()
        };
    }

    private CropOffsetPreset BuildCurrentCropOffsetPreset()
    {
        var presetName = SelectedCropOffsetPreset?.Name;
        if (string.IsNullOrWhiteSpace(presetName))
        {
            presetName = "custom";
        }

        return new CropOffsetPreset
        {
            Name = presetName,
            HorizontalPercent = CropOffsetXPercent,
            VerticalPercent = CropOffsetYPercent
        };
    }

    private async Task<PlayerRecord?> ResolvePreviewPlayerAsync()
    {
        var extractor = new DatabaseExtractor();
        var players = await extractor.ReadCsvAsync(InputCsvPath);

        if (!string.IsNullOrWhiteSpace(OnlyPlayerId))
        {
            var match = players.FirstOrDefault(p =>
                string.Equals(p.PlayerId.ToString(), OnlyPlayerId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.External_Player_ID, OnlyPlayerId, StringComparison.OrdinalIgnoreCase));
            return match;
        }

        return players.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.External_Player_ID));
    }

    private (int width, int height, string? placeholderPath) ResolvePreviewVariant()
    {
        if (!string.IsNullOrWhiteSpace(SizeProfilePath))
        {
            var profile = LoadSizeProfile(SizeProfilePath);
            var variant = profile.Variants.FirstOrDefault(v =>
                              string.Equals(v.Key, "x200x300", StringComparison.OrdinalIgnoreCase)
                              || (v.Width == 200 && v.Height == 300))
                          ?? profile.Variants.First();
            var placeholder = UsePlaceholderImages ? variant.PlaceholderPath : null;
            return (variant.Width, variant.Height, placeholder);
        }

        var manualPlaceholder = UsePlaceholderImages ? PlaceholderImagePath : null;
        return (PortraitWidth, PortraitHeight, manualPlaceholder);
    }

    private static string ResolveSingleProfileOutputDirectory(string baseOutputDirectory, UiSizeProfile profile)
    {
        var folderName = SanitizeOutputFolderName(profile.Name);
        return Path.Combine(baseOutputDirectory, folderName);
    }

    private void UpdatePreviewDisplaySize(Bitmap? bitmap)
    {
        if (bitmap == null || bitmap.PixelSize.Width <= 0 || bitmap.PixelSize.Height <= 0)
        {
            PreviewDisplayWidth = 0;
            PreviewDisplayHeight = 0;
            return;
        }

        var width = (double)bitmap.PixelSize.Width;
        var height = (double)bitmap.PixelSize.Height;
        var scale = Math.Min(1d, Math.Min(PreviewMaxDisplayWidth / width, PreviewMaxDisplayHeight / height));

        PreviewDisplayWidth = Math.Max(1, width * scale);
        PreviewDisplayHeight = Math.Max(1, height * scale);
    }

    private static string ResolveDefaultSingleVariantOutputDirectory(string baseOutputDirectory)
    {
        var normalizedBasePath = baseOutputDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(Path.GetFileName(normalizedBasePath), "default", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedBasePath;
        }

        return Path.Combine(baseOutputDirectory, "default");
    }

    private static string SanitizeOutputFolderName(string? folderName)
    {
        var trimmed = string.IsNullOrWhiteSpace(folderName) ? "default" : folderName.Trim();
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitizedChars = trimmed.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray();
        var sanitized = new string(sanitizedChars).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "default" : sanitized;
    }

    private static string NormalizePreviewExtension(string imageFormat)
    {
        var normalized = (imageFormat ?? "jpg").Trim().ToLowerInvariant();
        return normalized switch
        {
            "jpeg" => "jpg",
            "png" => "png",
            _ => "jpg"
        };
    }

    private string? ResolvePreviewPhotoPath(string External_Player_ID)
    {
        if (string.IsNullOrWhiteSpace(External_Player_ID))
        {
            return null;
        }

        var photoFiles = FindPlayerPhotoFiles(External_Player_ID);
        return photoFiles.FirstOrDefault();
    }

    private List<string> FindPlayerPhotoFiles(string External_Player_ID)
    {
        var photoFiles = Directory.GetFiles(PhotosDirectory, $"{External_Player_ID}.*")
            .Where(IsSupportedImageFormat)
            .ToList();

        if (photoFiles.Count == 0)
        {
            var pattern = $"*_{External_Player_ID}.*";
            photoFiles = Directory.GetFiles(PhotosDirectory, pattern, SearchOption.AllDirectories)
                .Where(IsSupportedImageFormat)
                .ToList();
        }

        return photoFiles;
    }

    private static bool IsSupportedImageFormat(string path)
    {
        var extension = Path.GetExtension(path);
        return extension is ".png" or ".jpg" or ".jpeg" or ".bmp";
    }

    private static List<string> BuildImageFileList(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return new List<string>();
        }

        return Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
            .Where(IsSupportedImageFormat)
            .ToList();
    }

    private sealed record GenerateIssueCounts(
        int External_Player_IDNotInCsv,
        int NoIdInFilename,
        int CsvPlayersWithoutPhoto,
        List<string> CsvPlayersWithoutPhotoNames,
        List<string> External_Player_IDNotInCsvFiles,
        List<string> NoIdInFilenameFiles);

    private void ResetGenerateIssues()
    {
        GenerateIssueItems.Clear();
        HasGenerateIssues = false;
        GenerateIssueTitle = "Mapping issues: none";
    }

    private void UpdateGenerateIssues(GenerateIssueCounts counts)
    {
        GenerateIssueItems.Clear();

        AddGenerateIssue("CSV players without photo", counts.CsvPlayersWithoutPhoto,
            counts.CsvPlayersWithoutPhotoNames);
        AddGenerateIssue("Photos with External_Player_ID not in CSV", counts.External_Player_IDNotInCsv,
            counts.External_Player_IDNotInCsvFiles);
        AddGenerateIssue("Photos missing External_Player_ID", counts.NoIdInFilename,
            counts.NoIdInFilenameFiles);

        HasGenerateIssues = GenerateIssueItems.Count > 0;
        var totalIssues = GenerateIssueItems.Sum(item => item.Count);
        GenerateIssueTitle = HasGenerateIssues
            ? $"Mapping issues: {totalIssues}"
            : "Mapping issues: none";
    }

    private void AddGenerateIssue(string label, int count, List<string>? detailNames = null)
    {
        if (count <= 0)
        {
            return;
        }

        var details = detailNames != null && detailNames.Count > 0
            ? string.Join(", ", detailNames)
            : string.Empty;

        GenerateIssueItems.Add(new GenerateIssueSummaryItem
        {
            Label = label,
            Count = count,
            Message = $"{count}",
            IsCritical = true,
            Details = details
        });
    }

    private async Task<GenerateIssueCounts> ComputeGenerateIssueCountsAsync()
    {
        if (string.IsNullOrWhiteSpace(InputCsvPath) || !File.Exists(InputCsvPath))
        {
            return new GenerateIssueCounts(0, 0, 0, new(), new(), new());
        }

        if (string.IsNullOrWhiteSpace(PhotosDirectory) || !Directory.Exists(PhotosDirectory))
        {
            return new GenerateIssueCounts(0, 0, 0, new(), new(), new());
        }

        var extractor = new DatabaseExtractor();
        var players = await extractor.ReadCsvAsync(InputCsvPath);

        var csvByExternal_Player_ID = players
            .Where(player => !string.IsNullOrWhiteSpace(player.External_Player_ID))
            .ToDictionary(player => player.External_Player_ID!, StringComparer.OrdinalIgnoreCase);

        var foundExternal_Player_IDs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var External_Player_IDNotInCsvCount = 0;
        var noIdInFilenameCount = 0;
        var External_Player_IDNotInCsvFiles = new List<string>();
        var noIdInFilenameFiles = new List<string>();

        var imageFiles = BuildImageFileList(PhotosDirectory);
        foreach (var imagePath in imageFiles)
        {
            var fileName = Path.GetFileName(imagePath);
            var metadata = FilenameParser.ParseAutoDetect(fileName);
            var External_Player_ID = metadata?.External_Player_ID;

            if (string.IsNullOrWhiteSpace(External_Player_ID))
            {
                noIdInFilenameCount++;
                noIdInFilenameFiles.Add(fileName);
                continue;
            }

            if (csvByExternal_Player_ID.ContainsKey(External_Player_ID))
            {
                foundExternal_Player_IDs.Add(External_Player_ID);
            }
            else
            {
                External_Player_IDNotInCsvCount++;
                External_Player_IDNotInCsvFiles.Add(fileName);
            }
        }

        var csvPlayersWithoutPhoto = Math.Max(0, csvByExternal_Player_ID.Count - foundExternal_Player_IDs.Count);

        // Collect names of CSV players who have no matching photo
        var csvPlayersWithoutPhotoNames = players
            .Where(p => !string.IsNullOrWhiteSpace(p.External_Player_ID) &&
                        !foundExternal_Player_IDs.Contains(p.External_Player_ID!))
            .Select(p => string.IsNullOrWhiteSpace(p.FullName) ? $"ID:{p.External_Player_ID}" : p.FullName)
            .ToList();

        return new GenerateIssueCounts(
            External_Player_IDNotInCsvCount,
            noIdInFilenameCount,
            csvPlayersWithoutPhoto,
            csvPlayersWithoutPhotoNames,
            External_Player_IDNotInCsvFiles,
            noIdInFilenameFiles);
    }

    private static string BuildPreviewPlayerLabel(PlayerRecord player)
    {
        var External_Player_ID = string.IsNullOrWhiteSpace(player.External_Player_ID) ? "n/a" : player.External_Player_ID;
        return $"Preview player: {player.FullName} (ID: {External_Player_ID})";
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
        var filename = $"generate_log_{timestamp}.txt";
        var defaultPath = Path.Combine(OutputDirectory ?? Directory.GetCurrentDirectory(), filename);
        await SaveLogToFileAsync(defaultPath);
    }

    public async Task SaveLogToFileAsync(string savePath)
    {
        try
        {
            var lines = LogLines.ToList();
            await File.WriteAllLinesAsync(savePath, lines);
            ProcessingStatus = $"✓ Log saved to {savePath}";
        }
        catch (Exception ex)
        {
            ProcessingStatus = $"✗ Error saving log: {ex.Message}";
        }
    }

    private void ScheduleAutoPreview()
    {
        if (!AutoPreviewEnabled || IsProcessing)
        {
            return;
        }

        _autoPreviewCts?.Cancel();
        _autoPreviewCts?.Dispose();
        _autoPreviewCts = new CancellationTokenSource();
        var token = _autoPreviewCts.Token;

        _ = DebouncedAutoPreviewAsync(token);
    }

    private async Task DebouncedAutoPreviewAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(250, token);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (GeneratePreviewCommand.CanExecute(null))
                {
                    GeneratePreviewCommand.Execute(null);
                }
            });
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void RequestAutoPreviewFromUi()
    {
        ScheduleAutoPreview();
    }

    private static int GetTierIndexForModel(string modelName)
    {
        if (string.Equals(modelName, "opencv-dnn", StringComparison.OrdinalIgnoreCase))
            return 0;

        if (string.Equals(modelName, "apple-vision", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(modelName, "vision", StringComparison.OrdinalIgnoreCase))
            return 0;

        if (string.Equals(modelName, "llava:7b", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(modelName, "qwen3-vl", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(modelName, "qwen3-vl:latest", StringComparison.OrdinalIgnoreCase))
            return 1;

        return 2;
    }

    private static string ResolveOutputProfile(string profile, string baseOutputPath)
    {
        var normalized = (profile ?? "none").Trim().ToLowerInvariant();
        return normalized switch
        {
            "none" => baseOutputPath,
            "test" => Environment.GetEnvironmentVariable("PHOTOMAPPER_OUTPUT_TEST") ?? Path.Combine(baseOutputPath, "test"),
            "prod" => Environment.GetEnvironmentVariable("PHOTOMAPPER_OUTPUT_PROD") ?? Path.Combine(baseOutputPath, "prod"),
            _ => throw new InvalidOperationException($"Unsupported output profile '{profile}'. Use none/test/prod.")
        };
    }

    private static string? ResolveDefaultSizeProfilePath()
    {
        var candidates = new[]
        {
            // Primary: size_profiles.json next to appsettings.json
            Path.Combine(AppContext.BaseDirectory, "size_profiles.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "size_profiles.json"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "size_profiles.json")),
            // Fallback: samples/size_profiles.default.json for backwards compatibility
            Path.Combine(Directory.GetCurrentDirectory(), "samples", "size_profiles.default.json"),
            Path.Combine(AppContext.BaseDirectory, "samples", "size_profiles.default.json"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "size_profiles.default.json")),
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static UiSizeProfile LoadSizeProfile(string profilePath)
    {
        if (string.IsNullOrWhiteSpace(profilePath) || !File.Exists(profilePath))
        {
            throw new FileNotFoundException($"Size profile file not found: {profilePath}");
        }

        var json = File.ReadAllText(profilePath);
        var profile = JsonSerializer.Deserialize<UiSizeProfile>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        if (profile == null || profile.Variants == null || profile.Variants.Count == 0)
        {
            throw new InvalidOperationException("Size profile must contain at least one variant.");
        }

        foreach (var variant in profile.Variants)
        {
            if (string.IsNullOrWhiteSpace(variant.Key) || variant.Width <= 0 || variant.Height <= 0)
            {
                throw new InvalidOperationException("Each size profile variant requires key, width > 0, and height > 0.");
            }
        }

        return profile;
    }

    private void ConfigureFaceDetectionModelsForPlatform()
    {
        var isWindows = OperatingSystem.IsWindows();
        var isMacOS = OperatingSystem.IsMacOS();
        var isLinux = OperatingSystem.IsLinux();

        if (isWindows)
        {
            // Windows: OpenCV works fine
            RecommendedFaceDetectionModels.Add("opencv-yunet");
            RecommendedFaceDetectionModels.Add("opencv-dnn");
            FaceDetectionModel = "opencv-yunet";
        }
        else if (isMacOS)
        {
            // macOS: prefer Apple Vision, keep Ollama and center as fallbacks
            RecommendedFaceDetectionModels.Add("apple-vision");
            RecommendedFaceDetectionModels.Add("qwen3-vl");
            RecommendedFaceDetectionModels.Add("llava:7b");
            RecommendedFaceDetectionModels.Add("center");
            LocalVisionFaceDetectionModels.Clear();
            LocalVisionFaceDetectionModels.Add("apple-vision");
            LocalVisionFaceDetectionModels.Add("llava:7b");
            LocalVisionFaceDetectionModels.Add("qwen3-vl");
            AdvancedFaceDetectionModels.Clear();
            AdvancedFaceDetectionModels.Add("apple-vision");
            AdvancedFaceDetectionModels.Add("qwen3-vl");
            AdvancedFaceDetectionModels.Add("llava:7b");
            AdvancedFaceDetectionModels.Add("center");
            FaceDetectionModel = "apple-vision";
        }
        else if (isLinux)
        {
            // Linux: Try OpenCV, fallback to center
            RecommendedFaceDetectionModels.Add("opencv-yunet");
            RecommendedFaceDetectionModels.Add("opencv-dnn");
            RecommendedFaceDetectionModels.Add("center");
            FaceDetectionModel = "opencv-yunet";
        }
        else
        {
            // Unknown platform: Use center as safe default
            RecommendedFaceDetectionModels.Add("center");
            FaceDetectionModel = "center";
        }
    }

    private sealed class UiSizeProfile
    {
        public string Name { get; set; } = "default";
        public List<UiSizeVariant> Variants { get; set; } = new();
    }

    private sealed class UiSizeVariant
    {
        public string Key { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public string? OutputSubfolder { get; set; }
        public string? PlaceholderPath { get; set; }
    }
}
