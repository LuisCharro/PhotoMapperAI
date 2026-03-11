using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoMapperAI.Models;
using PhotoMapperAI.Services.Database;
using PhotoMapperAI.Utils;

namespace PhotoMapperAI.UI.ViewModels;

public partial class ManualPhotoMappingDialogViewModel : ObservableObject
{
    private readonly string _mappedCsvPath;
    private readonly string _photosDirectory;
    private readonly string? _filenamePattern;
    private readonly string? _photoManifestPath;
    private readonly Dictionary<string, PhotoMetadata>? _manifest;
    private readonly List<PlayerRecord> _allPlayers = new();
    private bool _isApplyingSelection;

    public ManualPhotoMappingDialogViewModel(
        string title,
        string mappedCsvPath,
        string photosDirectory,
        string? filenamePattern,
        string? photoManifestPath)
    {
        Title = title;
        _mappedCsvPath = mappedCsvPath;
        _photosDirectory = photosDirectory;
        _filenamePattern = string.IsNullOrWhiteSpace(filenamePattern) ? null : filenamePattern;
        _photoManifestPath = string.IsNullOrWhiteSpace(photoManifestPath) ? null : photoManifestPath;

        if (!string.IsNullOrWhiteSpace(_photoManifestPath) && File.Exists(_photoManifestPath))
        {
            _manifest = FilenameParser.LoadManifest(_photoManifestPath);
        }

        UnmappedPlayers.CollectionChanged += OnCollectionChanged;
        AvailablePhotos.CollectionChanged += OnCollectionChanged;
        PendingAssignments.CollectionChanged += OnCollectionChanged;
    }

    public string Title { get; }

    [ObservableProperty]
    private string _statusMessage = "Load pending manual mappings.";

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private ManualMappingPlayerItem? _selectedPlayer;

    [ObservableProperty]
    private ManualMappingPhotoItem? _selectedPhoto;

    [ObservableProperty]
    private Bitmap? _selectedPhotoPreview;

    [ObservableProperty]
    private bool _saveSucceeded;

    public ObservableCollection<ManualMappingPlayerItem> UnmappedPlayers { get; } = new();
    public ObservableCollection<ManualMappingPhotoItem> AvailablePhotos { get; } = new();
    public ObservableCollection<ManualMappingAssignmentItem> PendingAssignments { get; } = new();

    public int UnmappedCount => UnmappedPlayers.Count;
    public int AvailablePhotoCount => AvailablePhotos.Count;
    public bool HasPendingAssignments => PendingAssignments.Count > 0;
    public bool CanQueueSelectedAssignment => !_isApplyingSelection && SelectedPlayer != null && SelectedPhoto != null;
    public string CsvPath => _mappedCsvPath;
    public string PhotosDirectory => _photosDirectory;

    public async Task InitializeAsync()
    {
        UnmappedPlayers.Clear();
        AvailablePhotos.Clear();
        PendingAssignments.Clear();
        _allPlayers.Clear();
        SaveSucceeded = false;

        var players = await DatabaseExtractor.ReadExistingMappedCsvRowsAsync(_mappedCsvPath);
        _allPlayers.AddRange(players);

        foreach (var player in players
                     .Where(p => !p.ValidMapping || string.IsNullOrWhiteSpace(p.External_Player_ID))
                     .OrderBy(p => p.FamilyName)
                     .ThenBy(p => p.SurName)
                     .ThenBy(p => p.PlayerId))
        {
            UnmappedPlayers.Add(new ManualMappingPlayerItem
            {
                PlayerId = player.PlayerId,
                TeamId = player.TeamId,
                FullName = player.FullName,
                FamilyName = player.FamilyName,
                SurName = player.SurName
            });
        }

        var usedExternalIds = players
            .Where(p => p.ValidMapping && !string.IsNullOrWhiteSpace(p.External_Player_ID))
            .Select(p => p.External_Player_ID!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var photoFiles = Directory.GetFiles(_photosDirectory, "*.*", SearchOption.AllDirectories)
            .Where(IsSupportedImageFormat)
            .OrderBy(Path.GetFileName)
            .ToList();

        var skippedWithoutId = 0;

        foreach (var photoPath in photoFiles)
        {
            var metadata = ExtractPhotoMetadata(photoPath);
            if (string.IsNullOrWhiteSpace(metadata?.External_Player_ID))
            {
                skippedWithoutId++;
                continue;
            }

            if (usedExternalIds.Contains(metadata.External_Player_ID))
            {
                continue;
            }

            AvailablePhotos.Add(new ManualMappingPhotoItem
            {
                FileName = Path.GetFileName(photoPath),
                FilePath = photoPath,
                External_Player_ID = metadata.External_Player_ID,
                FullName = metadata.FullName ?? string.Empty,
                FamilyName = metadata.FamilyName ?? string.Empty,
                SurName = metadata.SurName ?? string.Empty,
                Source = metadata.Source.ToString()
            });
        }

        StatusMessage = skippedWithoutId > 0
            ? $"Loaded {UnmappedCount} unmapped players and {AvailablePhotoCount} available photos. Skipped {skippedWithoutId} photos without External_Player_ID."
            : $"Loaded {UnmappedCount} unmapped players and {AvailablePhotoCount} available photos.";
    }

    partial void OnSelectedPlayerChanged(ManualMappingPlayerItem? value)
    {
        OnPropertyChanged(nameof(CanQueueSelectedAssignment));
        QueueSelectedAssignmentCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedPhotoChanged(ManualMappingPhotoItem? value)
    {
        LoadSelectedPhotoPreview(value);
        OnPropertyChanged(nameof(CanQueueSelectedAssignment));
        QueueSelectedAssignmentCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void RemoveAssignment(ManualMappingAssignmentItem? assignment)
    {
        if (assignment == null)
        {
            return;
        }

        PendingAssignments.Remove(assignment);
        InsertSorted(UnmappedPlayers, assignment.Player, static p => $"{p.FamilyName}|{p.SurName}|{p.PlayerId}");
        InsertSorted(AvailablePhotos, assignment.Photo, static p => $"{p.FileName}|{p.External_Player_ID}");
        StatusMessage = $"Removed queued mapping for {assignment.Player.FullName}.";
        OnPropertyChanged(nameof(CanQueueSelectedAssignment));
    }

    [RelayCommand(CanExecute = nameof(CanQueueSelectedAssignment))]
    private void QueueSelectedAssignment()
    {
        TryCreateAssignmentFromSelection();
    }

    public async Task<bool> SaveChangesAsync()
    {
        if (IsSaving)
        {
            return false;
        }

        if (PendingAssignments.Count == 0)
        {
            StatusMessage = "No queued mappings to save.";
            return false;
        }

        IsSaving = true;
        SaveSucceeded = false;

        try
        {
            foreach (var assignment in PendingAssignments)
            {
                var player = _allPlayers.FirstOrDefault(p => p.PlayerId == assignment.Player.PlayerId);
                if (player == null)
                {
                    continue;
                }

                player.External_Player_ID = assignment.Photo.External_Player_ID;
                player.ValidMapping = true;
                player.Confidence = 1.0;
            }

            await DatabaseExtractor.WriteCsvAsync(_allPlayers, _mappedCsvPath);
            StatusMessage = $"Saved {PendingAssignments.Count} manual mapping(s) to CSV.";
            SaveSucceeded = true;
            PendingAssignments.Clear();
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
            return false;
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void TryCreateAssignmentFromSelection()
    {
        if (_isApplyingSelection || SelectedPlayer == null || SelectedPhoto == null)
        {
            return;
        }

        var selectedPlayer = SelectedPlayer;
        var selectedPhoto = SelectedPhoto;
        if (selectedPlayer == null || selectedPhoto == null)
        {
            return;
        }

        try
        {
            _isApplyingSelection = true;

            var assignment = new ManualMappingAssignmentItem
            {
                Player = selectedPlayer,
                Photo = selectedPhoto
            };

            PendingAssignments.Add(assignment);
            UnmappedPlayers.Remove(selectedPlayer);
            AvailablePhotos.Remove(selectedPhoto);
            StatusMessage = $"Queued {selectedPlayer.FullName} -> {selectedPhoto.External_Player_ID}.";

            SelectedPlayer = null;
            SelectedPhoto = null;
            SelectedPhotoPreview = null;
        }
        finally
        {
            _isApplyingSelection = false;
            OnPropertyChanged(nameof(CanQueueSelectedAssignment));
            QueueSelectedAssignmentCommand.NotifyCanExecuteChanged();
        }
    }

    private void LoadSelectedPhotoPreview(ManualMappingPhotoItem? photo)
    {
        if (photo == null || !File.Exists(photo.FilePath))
        {
            SelectedPhotoPreview = null;
            return;
        }

        try
        {
            using var stream = File.OpenRead(photo.FilePath);
            SelectedPhotoPreview = new Bitmap(stream);
        }
        catch
        {
            SelectedPhotoPreview = null;
        }
    }

    private PhotoMetadata? ExtractPhotoMetadata(string photoPath)
    {
        var photoName = Path.GetFileName(photoPath);

        if (_manifest != null && _manifest.TryGetValue(photoName, out var manifestMetadata))
        {
            return manifestMetadata;
        }

        if (!string.IsNullOrWhiteSpace(_filenamePattern))
        {
            var parsed = FilenameParser.ParseWithTemplate(photoName, _filenamePattern);
            if (parsed != null)
            {
                return parsed;
            }
        }

        return FilenameParser.ParseAutoDetect(photoName);
    }

    private static bool IsSupportedImageFormat(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase);
    }

    private static void InsertSorted<T>(ObservableCollection<T> collection, T item, Func<T, string> keySelector)
    {
        var key = keySelector(item);
        var index = 0;
        while (index < collection.Count && string.CompareOrdinal(keySelector(collection[index]), key) < 0)
        {
            index++;
        }

        collection.Insert(index, item);
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(UnmappedCount));
        OnPropertyChanged(nameof(AvailablePhotoCount));
        OnPropertyChanged(nameof(HasPendingAssignments));
        OnPropertyChanged(nameof(CanQueueSelectedAssignment));
        QueueSelectedAssignmentCommand.NotifyCanExecuteChanged();
    }
}

public partial class ManualMappingPlayerItem : ObservableObject
{
    [ObservableProperty]
    private int _playerId;

    [ObservableProperty]
    private int _teamId;

    [ObservableProperty]
    private string _fullName = string.Empty;

    [ObservableProperty]
    private string _familyName = string.Empty;

    [ObservableProperty]
    private string _surName = string.Empty;

    public string Subtitle => $"ID: {PlayerId}";
}

public partial class ManualMappingPhotoItem : ObservableObject
{
    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _external_Player_ID = string.Empty;

    [ObservableProperty]
    private string _fullName = string.Empty;

    [ObservableProperty]
    private string _familyName = string.Empty;

    [ObservableProperty]
    private string _surName = string.Empty;

    [ObservableProperty]
    private string _source = string.Empty;

    public string Subtitle => string.IsNullOrWhiteSpace(FullName)
        ? $"External ID: {External_Player_ID}"
        : $"{FullName} | External ID: {External_Player_ID}";
}

public partial class ManualMappingAssignmentItem : ObservableObject
{
    [ObservableProperty]
    private ManualMappingPlayerItem _player = new();

    [ObservableProperty]
    private ManualMappingPhotoItem _photo = new();

    public string Summary => $"{Player.FullName} -> {Photo.External_Player_ID}";
}
