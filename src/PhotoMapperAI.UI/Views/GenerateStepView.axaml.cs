using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PhotoMapperAI.UI.ViewModels;

namespace PhotoMapperAI.UI.Views;

public partial class GenerateStepView : UserControl
{
    public GenerateStepView()
    {
        InitializeComponent();
    }

    private async void BrowseCsvFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is GenerateStepViewModel vm)
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
        if (DataContext is GenerateStepViewModel vm)
        {
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage != null)
            {
                var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Source Photos Directory",
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    vm.PhotosDirectory = folders[0].Path.LocalPath;
                }
            }
        }
    }

    private async void BrowseOutputDirectory_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is GenerateStepViewModel vm)
        {
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage != null)
            {
                var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Output Directory",
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    vm.OutputDirectory = folders[0].Path.LocalPath;
                }
            }
        }
    }
}
