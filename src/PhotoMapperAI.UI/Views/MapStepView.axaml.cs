using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
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
}
