using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoMapperAI.Services.AI;
using PhotoMapperAI.Services.Image;

namespace PhotoMapperAI.UI.ViewModels;

public partial class MapStepViewModel : ViewModelBase
{
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
    private double _confidenceThreshold = 0.9;

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
    private async Task ExecuteMap()
    {
        if (string.IsNullOrEmpty(InputCsvPath) || string.IsNullOrEmpty(PhotosDirectory))
        {
            ProcessingStatus = "Please select CSV file and photos directory";
            return;
        }

        IsProcessing = true;
        ProcessingStatus = "Mapping photos to players...";

        try
        {
            // Create services
            var nameMatchingService = new OllamaNameMatchingService(
                modelName: NameModel,
                confidenceThreshold: ConfidenceThreshold
            );
            var imageProcessor = new ImageProcessor();

            // Create map command logic
            var logic = new Commands.MapCommandLogic(nameMatchingService, imageProcessor);

            // Execute mapping
            var result = await logic.ExecuteAsync(
                InputCsvPath,
                PhotosDirectory,
                string.IsNullOrEmpty(FilenamePattern) ? null : FilenamePattern,
                UsePhotoManifest ? PhotoManifestPath : null,
                NameModel,
                ConfidenceThreshold
            );

            PlayersProcessed = result.PlayersProcessed;
            PlayersMatched = result.PlayersMatched;
            ProcessingStatus = $"✓ Mapped {PlayersMatched}/{PlayersProcessed} players successfully";
            IsComplete = true;
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
}

// Helper class for result
public class MapResult
{
    public int PlayersProcessed { get; set; }
    public int PlayersMatched { get; set; }
}
