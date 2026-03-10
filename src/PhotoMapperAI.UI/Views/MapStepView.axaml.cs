using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Platform.Storage;
using PhotoMapperAI.UI.ViewModels;
using System.IO;

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

    private async void BrowseOutputDirectory_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MapStepViewModel vm)
        {
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage != null)
            {
                var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Output Folder",
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    vm.OutputDirectory = folders[0].Path.LocalPath;
                }
            }
        }
    }

    private async void ExecuteMap_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MapStepViewModel vm)
            return;

        if (string.IsNullOrWhiteSpace(vm.OutputDirectory))
        {
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage != null)
            {
                var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Output Folder for Mapped CSV",
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    vm.OutputDirectory = folders[0].Path.LocalPath;
                }
            }

            if (string.IsNullOrWhiteSpace(vm.OutputDirectory))
                return;
        }

        if (vm.ExecuteMapCommand is IAsyncRelayCommand asyncRelayCommand)
        {
            await asyncRelayCommand.ExecuteAsync(null);
        }
        else
        {
            vm.ExecuteMapCommand.Execute(null);
        }

        if (!vm.IsComplete || string.IsNullOrWhiteSpace(vm.OutputCsvPath))
            return;

        await ShowInfoDialogAsync("Mapping Complete", $"Mapped CSV saved at:\n{vm.OutputCsvPath}");
    }

    private void NameModelSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MapStepViewModel vm)
            return;

        if (sender is ComboBox combo && combo.SelectedItem is string model && !string.IsNullOrWhiteSpace(model))
        {
            vm.NameModel = model;
        }
    }

    private async void CopyRunLog_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MapStepViewModel vm)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard == null)
            return;

        var text = string.Join(Environment.NewLine, vm.LogLines);
        await topLevel.Clipboard.SetTextAsync(text);
        vm.ProcessingStatus = "Run log copied to clipboard.";
    }

    private async void SaveLog_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MapStepViewModel vm) return;

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Map Log",
            SuggestedFileName = $"map_log_{timestamp}.txt",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        if (file != null)
        {
            await vm.SaveLogToFileAsync(file.Path.LocalPath);
        }
    }

    private async Task ShowInfoDialogAsync(string title, string message)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null)
            return;

        var closeButton = new Button
        {
            Content = "OK",
            Width = 100,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };

        var dialog = new Window
        {
            Title = title,
            Width = 760,
            Height = 220,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Padding = new Avalonia.Thickness(16),
                Child = new StackPanel
                {
                    Spacing = 14,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = message,
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            FontSize = 14
                        },
                        closeButton
                    }
                }
            }
        };

        closeButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(owner);
    }

    private async void OpenManualMapping_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MapStepViewModel vm)
            return;

        if (string.IsNullOrWhiteSpace(vm.OutputCsvPath) || !File.Exists(vm.OutputCsvPath))
        {
            await ShowInfoDialogAsync("Manual Mapping", "Run mapping first so there is a mapped CSV to edit.");
            return;
        }

        if (string.IsNullOrWhiteSpace(vm.PhotosDirectory) || !Directory.Exists(vm.PhotosDirectory))
        {
            await ShowInfoDialogAsync("Manual Mapping", "Configure a valid photos directory first.");
            return;
        }

        var dialogVm = new ManualPhotoMappingDialogViewModel(
            "Manual Player Mapping",
            vm.OutputCsvPath,
            vm.PhotosDirectory,
            vm.FilenamePattern,
            vm.UsePhotoManifest ? vm.PhotoManifestPath : null);
        await dialogVm.InitializeAsync();

        var dialog = new ManualPhotoMappingDialog(dialogVm);
        var owner = TopLevel.GetTopLevel(this) as Window;
        var saved = owner != null
            ? await dialog.ShowDialog<bool>(owner)
            : false;

        if (saved)
        {
            await vm.RefreshMappingSummaryAsync();
        }
    }
}
