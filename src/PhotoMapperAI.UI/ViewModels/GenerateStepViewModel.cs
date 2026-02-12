using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
            var faceDetectionService = CreateFaceDetectionService(FaceDetectionModel);
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

    private IFaceDetectionService CreateFaceDetectionService(string model)
    {
        if (model.Contains(','))
        {
            return new FallbackFaceDetectionService(model);
        }

        return model.ToLower() switch
        {
            "opencv-dnn" => new OpenCVDNNFaceDetectionService(),
            "yolov8-face" => new OpenCVDNNFaceDetectionService(),
            "haar-cascade" or "haar" => new HaarCascadeFaceDetectionService(),
            "center" => new CenterCropFallbackService(),
            var ollamaModel when ollamaModel.Contains("llava") || ollamaModel.Contains("qwen3-vl") 
                => new OllamaFaceDetectionService(modelName: model),
            _ => throw new ArgumentException($"Unknown face detection model: {model}")
        };
    }
}
