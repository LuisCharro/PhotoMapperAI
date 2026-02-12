using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoMapperAI.Services.AI;
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
    private string _faceDetectionModel = "llava:7b,qwen3-vl";

    [ObservableProperty]
    private int _portraitWidth = 200;

    [ObservableProperty]
    private int _portraitHeight = 300;

    [ObservableProperty]
    private bool _portraitOnly;

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

    public List<string> ImageFormats { get; } = new() { "jpg", "png" };

    public List<string> FaceDetectionModels { get; } = new()
    {
        "llava:7b,qwen3-vl",
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
    private async Task BrowsePhotosDirectory()
    {
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task BrowseOutputDirectory()
    {
        await Task.CompletedTask;
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

        IsProcessing = true;
        IsComplete = false;
        PortraitsGenerated = 0;
        PortraitsFailed = 0;
        ProcessingStatus = "Generating portraits...";
        Progress = 0;
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
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
            var progress = new Progress<(int processed, int total, string current)>(p =>
            {
                var percent = p.total > 0
                    ? (double)p.processed / p.total * 100.0
                    : 0.0;

                Progress = Math.Clamp(percent, 0, 100);
                ProcessingStatus = $"Processing {p.processed}/{p.total}: {p.current}";
            });

            var result = await logic.ExecuteWithResultAsync(
                InputCsvPath,
                PhotosDirectory,
                OutputDirectory,
                ImageFormat,
                FaceDetectionModel,
                "generic",
                PortraitOnly,
                PortraitWidth,
                PortraitHeight,
                false,
                4,
                progress,
                _cancellationTokenSource.Token
            );

            PortraitsGenerated = result.PortraitsGenerated;
            PortraitsFailed = result.PortraitsFailed;
            Progress = result.TotalPlayers > 0
                ? Math.Clamp((double)result.ProcessedPlayers / result.TotalPlayers * 100.0, 0, 100)
                : Progress;

            if (result.IsCancelled)
            {
                ProcessingStatus = $"⚠ Generation cancelled ({result.ProcessedPlayers}/{result.TotalPlayers} processed)";
                IsComplete = false;
                return;
            }

            if (result.ExitCode != 0)
            {
                ProcessingStatus = $"✗ Generation failed ({PortraitsGenerated} generated, {PortraitsFailed} failed)";
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

}
