using System;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PhotoMapperAI.UI.ViewModels;
using System.Linq;

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

    private async void BrowseSizeProfileFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is GenerateStepViewModel vm)
        {
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage != null)
            {
                var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Size Profile JSON",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } },
                        new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                    }
                });

                if (files.Count > 0)
                {
                    vm.SizeProfilePath = files[0].Path.LocalPath;
                }
            }
        }
    }

    private void FaceModelSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not GenerateStepViewModel vm)
            return;

        if (sender is ComboBox combo && combo.SelectedItem is string model && !string.IsNullOrWhiteSpace(model))
        {
            vm.FaceDetectionModel = model;
        }
    }

    private async void CopyRunLog_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not GenerateStepViewModel vm)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard == null)
            return;

        var text = string.Join(System.Environment.NewLine, vm.LogLines);
        await topLevel.Clipboard.SetTextAsync(text);

        vm.ProcessingStatus = "Run log copied to clipboard.";
    }
}
