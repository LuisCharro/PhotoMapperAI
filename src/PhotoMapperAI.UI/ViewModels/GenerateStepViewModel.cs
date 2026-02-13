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
using PhotoMapperAI.Services.AI;
using PhotoMapperAI.Services.Diagnostics;
using PhotoMapperAI.Services.Image;

namespace PhotoMapperAI.UI.ViewModels;

public partial class GenerateStepViewModel : ViewModelBase
{
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private string _inputCsvPath = string.Empty;

    [ObservableProperty]
    private string _photosDirectory = string.Empty;

    [ObservableProperty]
    private string _outputDirectory = string.Empty;

    [ObservableProperty]
    private string _imageFormat = "jpg";

    [ObservableProperty]
    private string _faceDetectionModel = "llava:7b";

    [ObservableProperty]
    private string _sizeProfilePath = string.Empty;

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

    public ObservableCollection<string> LogLines { get; } = new();

    public List<string> ImageFormats { get; } = new() { "jpg", "png" };

    public List<string> OutputProfiles { get; } = new() { "none", "test", "prod" };

    public List<string> FaceDetectionModels { get; } = new()
    {
        "llava:7b",
        "qwen3-vl",
        "opencv-dnn",
        "yolov8-face",
        "haar-cascade",
        "center"
    };

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
    private async Task CheckFaceModel()
    {
        if (IsProcessing)
            return;

        IsCheckingModel = true;
        ModelDiagnosticStatus = $"Checking model '{FaceDetectionModel}'...";

        try
        {
            var service = FaceDetectionServiceFactory.Create(FaceDetectionModel);
            var initialized = await service.InitializeAsync();

            ModelDiagnosticStatus = initialized
                ? $"✓ Face detection model ready: {FaceDetectionModel}"
                : $"✗ Face detection model unavailable: {FaceDetectionModel}";
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

        try
        {
            var preflight = await PreflightChecker.CheckGenerateAsync(FaceDetectionModel, DownloadOpenCvModels);
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

            // Create face detection service
            var faceDetectionService = FaceDetectionServiceFactory.Create(FaceDetectionModel);
            await faceDetectionService.InitializeAsync();

            // Create image processor
            var imageProcessor = new ImageProcessor();

            // Create generate photos command logic
            var logic = new Commands.GeneratePhotosCommandLogic(
                faceDetectionService, 
                imageProcessor, 
                cache: null
            );

            // Execute generation
            var baseOutputDirectory = OutputDirectory;
            if (!string.Equals(OutputProfile, "none", StringComparison.OrdinalIgnoreCase))
            {
                baseOutputDirectory = ResolveOutputProfile(OutputProfile, OutputDirectory);
                AppendLog($"Using output profile '{OutputProfile}' => {baseOutputDirectory}");
            }

            var variants = new List<(string key, int width, int height, string outputDir)>();

            if (!string.IsNullOrWhiteSpace(SizeProfilePath))
            {
                var profile = LoadSizeProfile(SizeProfilePath);
                AppendLog($"Using size profile '{profile.Name}' with {profile.Variants.Count} variants");

                if (AllSizes)
                {
                    variants.AddRange(profile.Variants.Select(v => (
                        v.Key,
                        v.Width,
                        v.Height,
                        Path.Combine(baseOutputDirectory, string.IsNullOrWhiteSpace(v.OutputSubfolder) ? v.Key : v.OutputSubfolder)
                    )));
                }
                else
                {
                    var v = profile.Variants.First();
                    variants.Add((v.Key, v.Width, v.Height, baseOutputDirectory));
                }
            }
            else
            {
                variants.Add(("single", PortraitWidth, PortraitHeight, baseOutputDirectory));
            }

            var totalGenerated = 0;
            var totalFailed = 0;
            var anyFailure = false;

            foreach (var variant in variants)
            {
                Directory.CreateDirectory(variant.outputDir);
                AppendLog($"Running variant '{variant.key}' => {variant.width}x{variant.height} -> {variant.outputDir}");

                var progress = new Progress<(int processed, int total, string current)>(p =>
                {
                    var percent = p.total > 0
                        ? (double)p.processed / p.total * 100.0
                        : 0.0;

                    Progress = Math.Clamp(percent, 0, 100);
                    ProcessingStatus = $"[{variant.key}] Processing {p.processed}/{p.total}: {p.current}";
                });

                var log = new Progress<string>(AppendLog);

                var result = await logic.ExecuteWithResultAsync(
                    InputCsvPath,
                    PhotosDirectory,
                    variant.outputDir,
                    ImageFormat,
                    FaceDetectionModel,
                    "generic",
                    PortraitOnly,
                    variant.width,
                    variant.height,
                    false,
                    4,
                    progress,
                    _cancellationTokenSource.Token,
                    log
                );

                totalGenerated += result.PortraitsGenerated;
                totalFailed += result.PortraitsFailed;

                if (result.IsCancelled)
                {
                    ProcessingStatus = $"⚠ Generation cancelled ({result.ProcessedPlayers}/{result.TotalPlayers} processed)";
                    IsComplete = false;
                    return;
                }

                if (result.ExitCode != 0)
                {
                    anyFailure = true;
                    AppendLog($"Variant '{variant.key}' failed with exit code {result.ExitCode}");
                }
            }

            PortraitsGenerated = totalGenerated;
            PortraitsFailed = totalFailed;

            if (anyFailure)
            {
                ProcessingStatus = $"✗ Generation completed with failures ({PortraitsGenerated} generated, {PortraitsFailed} failed)";
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

    private void AppendLog(string message)
    {
        if (LogLines.Count >= 200)
        {
            LogLines.RemoveAt(0);
        }

        LogLines.Add(message);
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
    }
}
