using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoMapperAI.Services.AI;
using PhotoMapperAI.Services.Image;

namespace PhotoMapperAI.UI.ViewModels;

public partial class GenerateStepViewModel : ViewModelBase
{
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
        if (string.IsNullOrEmpty(InputCsvPath) || string.IsNullOrEmpty(OutputDirectory))
        {
            ProcessingStatus = "Please select CSV file and output directory";
            return;
        }

        IsProcessing = true;
        ProcessingStatus = "Generating portraits...";
        Progress = 0;

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
            var result = await logic.ExecuteAsync(
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
                4
            );

            ProcessingStatus = $"✓ Generated {PortraitsGenerated} portraits ({PortraitsFailed} failed)";
            IsComplete = true;
            Progress = 100;
        }
        catch (Exception ex)
        {
            ProcessingStatus = $"✗ Error: {ex.Message}";
            IsComplete = false;
        }
        finally
        {
            IsProcessing = false;
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
