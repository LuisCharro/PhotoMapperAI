using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoMapperAI.Models;
using PhotoMapperAI.Services.AI;
using PhotoMapperAI.Services.Database;
using PhotoMapperAI.Services.Diagnostics;
using PhotoMapperAI.Services.Image;
using PhotoMapperAI.UI.Configuration;
using PhotoMapperAI.UI.Execution;
using PhotoMapperAI.Utils;
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
    public string? ExternalId { get; init; }
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
        ExternalIdNotInCsv, // ExternalId extracted but not in CSV
        NoExternalId        // Could not extract ExternalId from filename
    }

    public MappingStatusType StatusType { get; init; } = MappingStatusType.NoExternalId;

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
    private readonly ExternalGenerateCliRunner _cliRunner;
    private CancellationTokenSource? _cancellationTokenSource;

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
    private string _faceDetectionModel = "opencv-dnn";

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
    private string _previewStatus = string.Empty;

    [ObservableProperty]
    private string _previewPlayerLabel = string.Empty;

    [ObservableProperty]
    private bool _isPreviewing;

    [ObservableProperty]
    private ObservableCollection<PhotoPreviewItem> _photoPreviewItems = new();

    [ObservableProperty]
    private bool _isLoadingPhotoGrid;

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

    public ObservableCollection<string> LogLines { get; } = new();

    public List<string> ImageFormats { get; } = new() { "jpg", "png" };

    public List<string> OutputProfiles { get; } = new() { "none", "test", "prod" };

    public ObservableCollection<string> RecommendedFaceDetectionModels { get; } = new()
    {
        "opencv-dnn"
    };

    public ObservableCollection<string> LocalVisionFaceDetectionModels { get; } = new()
    {
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

        var settings = new CropOffsetSettings
        {
            ActivePresetName = presetName,
            Presets = CropOffsetPresets
                .Select(p => new CropOffsetPreset
                {
                    Name = p.Name,
                    HorizontalPercent = p.HorizontalPercent,
                    VerticalPercent = p.VerticalPercent
                })
                .ToList()
        };

        CropOffsetSettingsLoader.SaveToLocal(settings);
        CropOffsetStatus = $"Saved preset '{presetName}' to appsettings.local.json";
        SelectedCropOffsetPreset = preset;
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

            if (string.IsNullOrWhiteSpace(player.ExternalId))
            {
                PreviewStatus = "Selected player does not have an ExternalId.";
                return;
            }

            var (previewWidth, previewHeight, placeholderPath) = ResolvePreviewVariant();
            var photoPath = ResolvePreviewPhotoPath(player.ExternalId);

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

                PreviewStatus = $"No photo found for ExternalId {player.ExternalId}.";
                return;
            }

            var faceDetectionService = FaceDetectionServiceFactory.Create(FaceDetectionModel);
            await faceDetectionService.InitializeAsync();

            FaceLandmarks landmarks;
            if (PortraitOnly)
            {
                landmarks = new FaceLandmarks { FaceDetected = false };
            }
            else
            {
                PreviewStatus = "Detecting face for preview...";
                landmarks = await faceDetectionService.DetectFaceLandmarksAsync(photoPath);
            }

            var imageProcessor = new ImageProcessor();
            using var image = await imageProcessor.LoadImageAsync(photoPath);
            using var cropped = await imageProcessor.CropPortraitAsync(
                image,
                landmarks ?? new FaceLandmarks { FaceCenter = new PhotoMapperAI.Models.Point(image.Width / 2, image.Height / 2) },
                previewWidth,
                previewHeight,
                BuildCurrentCropOffsetPreset());

            using var stream = new MemoryStream();
            cropped.Save(stream, new PngEncoder());
            stream.Position = 0;
            PreviewImage = new Bitmap(stream);

            PreviewPlayerLabel = BuildPreviewPlayerLabel(player);
            PreviewStatus = $"Preview generated ({previewWidth}x{previewHeight}).";
        }
        catch (Exception ex)
        {
            PreviewStatus = $"Preview failed: {ex.Message}";
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
            return;
        }

        IsLoadingPhotoGrid = true;
        PreviewStatus = "Loading photos and CSV data...";

        try
        {
            // Load CSV data if available
            Dictionary<string, PlayerRecord> csvDataByExternalId = new(StringComparer.OrdinalIgnoreCase);
            List<PlayerRecord> csvPlayersWithoutPhoto = new();
            int totalCsvPlayers = 0;

            if (!string.IsNullOrWhiteSpace(InputCsvPath) && File.Exists(InputCsvPath))
            {
                var extractor = new DatabaseExtractor();
                var players = await extractor.ReadCsvAsync(InputCsvPath);
                totalCsvPlayers = players.Count;

                foreach (var player in players)
                {
                    // Only add players with ExternalId to the lookup dictionary
                    if (!string.IsNullOrEmpty(player.ExternalId))
                    {
                        csvDataByExternalId[player.ExternalId] = player;
                    }
                    else
                    {
                        // Track players without ExternalId
                        csvPlayersWithoutPhoto.Add(player);
                    }
                }
            }

            // Get all supported image files
            var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png", ".jpg", ".jpeg", ".bmp"
            };

            var imageFiles = Directory.EnumerateFiles(PhotosDirectory, "*.*", SearchOption.AllDirectories)
                .Where(path => supportedExtensions.Contains(Path.GetExtension(path)))
                .OrderBy(Path.GetFileName)
                .ToList();

            if (imageFiles.Count == 0)
            {
                PreviewStatus = "No supported images found in photos directory.";
                return;
            }

            // Parse each photo and match with CSV data
            var items = new List<PhotoPreviewItem>();

            // Track which ExternalIds from CSV were found in photos
            var foundExternalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var imagePath in imageFiles)
            {
                var fileName = Path.GetFileName(imagePath);
                
                // Try to extract ExternalId from filename
                var metadata = FilenameParser.ParseAutoDetect(fileName);
                
                string? externalId = metadata?.ExternalId;
                PlayerRecord? matchedPlayer = null;

                // Try to match with CSV data
                if (!string.IsNullOrEmpty(externalId) && csvDataByExternalId.TryGetValue(externalId, out var player))
                {
                    matchedPlayer = player;
                    foundExternalIds.Add(externalId);
                }

                var item = new PhotoPreviewItem
                {
                    FilePath = imagePath,
                    FileName = fileName,
                    ExternalId = externalId,
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
                else if (!string.IsNullOrEmpty(externalId))
                {
                    // ExternalId extracted but not found in CSV
                    item.StatusText = $"⚠ External ID: {externalId} (not in CSV)";
                }
                else
                {
                    // No ExternalId could be extracted from filename
                    item.StatusText = "? No ID in filename";
                }

                items.Add(item);
            }

            // Calculate statistics
            var totalPhotos = items.Count;
            var mappedCount = items.Count(i => i.HasValidMapping);
            var externalIdNotInCsvCount = items.Count(i => !i.HasValidMapping && !string.IsNullOrEmpty(i.ExternalId));
            var noIdInFilenameCount = items.Count(i => string.IsNullOrEmpty(i.ExternalId));

            // Find CSV players that don't have photos
            var unmatchedCsvPlayers = csvPlayersWithoutPhoto.Count;
            if (totalCsvPlayers > 0)
            {
                // Also count players in CSV who have ExternalId but no matching photo
                foreach (var kvp in csvDataByExternalId)
                {
                    if (!foundExternalIds.Contains(kvp.Key))
                    {
                        unmatchedCsvPlayers++;
                    }
                }
            }

            // Load thumbnails (limited to first 50 for performance)
            var itemsToLoad = items.Take(50).ToList();
            foreach (var item in itemsToLoad)
            {
                try
                {
                    await using var stream = File.OpenRead(item.FilePath);
                    item.Thumbnail = new Bitmap(stream);
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

            if (externalIdNotInCsvCount > 0)
                statusParts.Add($"{externalIdNotInCsvCount} not in CSV");

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

            var baseOutputDirectory = OutputDirectory;
            if (!string.Equals(OutputProfile, "none", StringComparison.OrdinalIgnoreCase))
            {
                baseOutputDirectory = ResolveOutputProfile(OutputProfile, OutputDirectory);
                AppendLog($"Using output profile '{OutputProfile}' => {baseOutputDirectory}");
            }

            var useSizeProfile = !string.IsNullOrWhiteSpace(SizeProfilePath);
            var sizeProfilePath = useSizeProfile ? SizeProfilePath : null;
            var allSizes = useSizeProfile && AllSizes;
            var ignoreProfilePlaceholders = useSizeProfile && !UsePlaceholderImages;
            var placeholderPath = useSizeProfile ? null : (UsePlaceholderImages ? PlaceholderImagePath : null);

            if (useSizeProfile)
            {
                var profile = LoadSizeProfile(SizeProfilePath);
                var variantMode = allSizes ? "all variants" : "single variant";
                AppendLog($"Using size profile '{profile.Name}' ({variantMode})");
            }

            Directory.CreateDirectory(baseOutputDirectory);
            if (useSizeProfile)
            {
                AppendLog($"Running size profile => {baseOutputDirectory}");
            }
            else
            {
                AppendLog($"Running variant 'single' => {PortraitWidth}x{PortraitHeight} -> {baseOutputDirectory}");
            }

            AppendLog(_cliRunner.BuildCommandPreview(
                Directory.GetCurrentDirectory(),
                InputCsvPath,
                PhotosDirectory,
                baseOutputDirectory,
                ImageFormat,
                selectedFaceDetectionModel,
                PortraitOnly,
                PortraitWidth,
                PortraitHeight,
                sizeProfilePath,
                allSizes,
                ignoreProfilePlaceholders,
                DownloadOpenCvModels,
                OnlyPlayerId,
                placeholderPath));

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
                        PortraitWidth,
                        PortraitHeight,
                        sizeProfilePath,
                        allSizes,
                        ignoreProfilePlaceholders,
                        DownloadOpenCvModels,
                        OnlyPlayerId,
                        placeholderPath);
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
                PortraitWidth,
                PortraitHeight,
                sizeProfilePath,
                allSizes,
                ignoreProfilePlaceholders,
                DownloadOpenCvModels,
                OnlyPlayerId,
                placeholderPath,
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
                string.Equals(p.ExternalId, OnlyPlayerId, StringComparison.OrdinalIgnoreCase));
            return match;
        }

        return players.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.ExternalId));
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

    private string? ResolvePreviewPhotoPath(string externalId)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return null;
        }

        var photoFiles = FindPlayerPhotoFiles(externalId);
        return photoFiles.FirstOrDefault();
    }

    private List<string> FindPlayerPhotoFiles(string externalId)
    {
        var photoFiles = Directory.GetFiles(PhotosDirectory, $"{externalId}.*")
            .Where(IsSupportedImageFormat)
            .ToList();

        if (photoFiles.Count == 0)
        {
            var pattern = $"*_{externalId}.*";
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

    private static string BuildPreviewPlayerLabel(PlayerRecord player)
    {
        var externalId = string.IsNullOrWhiteSpace(player.ExternalId) ? "n/a" : player.ExternalId;
        return $"Preview player: {player.FullName} (ID: {externalId})";
    }

    private void AppendLog(string message)
    {
        if (LogLines.Count >= 200)
        {
            LogLines.RemoveAt(0);
        }

        LogLines.Add(message);
    }

    private static int GetTierIndexForModel(string modelName)
    {
        if (string.Equals(modelName, "opencv-dnn", StringComparison.OrdinalIgnoreCase))
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
