using CommunityToolkit.Mvvm.ComponentModel;

namespace PhotoMapperAI.UI.Models;

/// <summary>
/// Represents a team item in the batch automation list.
/// </summary>
public partial class BatchTeamItem : ObservableObject
{
    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private int _teamId;

    [ObservableProperty]
    private string _teamName = string.Empty;

    [ObservableProperty]
    private BatchTeamStatus _status = BatchTeamStatus.Pending;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private int _playersExtracted;

    [ObservableProperty]
    private int _playersMapped;

    [ObservableProperty]
    private int _photosGenerated;

    [ObservableProperty]
    private string? _csvPath;

    [ObservableProperty]
    private string? _photoPath;
    
    [ObservableProperty]
    private bool _hasPhotoDirectory;
    
    /// <summary>
    /// Gets the status as a display string for the DataGrid.
    /// </summary>
    public string StatusText => Status.ToString();
    
    /// <summary>
    /// Gets a warning icon if photo directory is missing.
    /// </summary>
    public string PhotoDirectoryStatus => HasPhotoDirectory ? "✓" : "⚠";
    
    /// <summary>
    /// Override OnPropertyChanged to notify computed property changes.
    /// </summary>
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(Status))
        {
            OnPropertyChanged(nameof(StatusText));
        }
        if (e.PropertyName == nameof(HasPhotoDirectory))
        {
            OnPropertyChanged(nameof(PhotoDirectoryStatus));
        }
    }
}

/// <summary>
/// Status of a batch team processing.
/// </summary>
public enum BatchTeamStatus
{
    Pending,
    Extracting,
    Mapping,
    Generating,
    Completed,
    Failed,
    Skipped
}
