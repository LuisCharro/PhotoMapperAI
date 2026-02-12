using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PhotoMapperAI.UI.ViewModels;

namespace PhotoMapperAI.UI.Views;

public partial class MapStepView : UserControl
{
    public MapStepView()
    {
        InitializeComponent();
    }

    private async void BrowseCsvFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MapStepViewModel vm)
        {
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage != null)
            {
                var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select CSV File",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("CSV Files") { Patterns = new[] { "*.csv" } },
                        new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                    }
                });

                if (files.Count > 0)
                {
                    vm.InputCsvPath = files[0].Path.LocalPath;
                }
            }
        }
    }

    private async void BrowsePhotosDirectory_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MapStepViewModel vm)
        {
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage != null)
            {
                var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Photos Directory",
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    vm.PhotosDirectory = folders[0].Path.LocalPath;
                }
            }
        }
    }

    private async void BrowsePhotoManifest_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MapStepViewModel vm)
        {
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage != null)
            {
                var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Photo Manifest File",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } },
                        new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                    }
                });

                if (files.Count > 0)
                {
                    vm.PhotoManifestPath = files[0].Path.LocalPath;
                }
            }
        }
    }
}
