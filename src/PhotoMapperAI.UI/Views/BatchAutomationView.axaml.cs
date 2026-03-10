using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Controls.Primitives;
using Avalonia.Platform.Storage;
using PhotoMapperAI.Services.Database;
using PhotoMapperAI.UI.Models;
using PhotoMapperAI.UI.ViewModels;

namespace PhotoMapperAI.UI.Views;

public partial class BatchAutomationView : UserControl
{
    public BatchAutomationView()
    {
        InitializeComponent();
    }

    private async void BrowseTeamsSql_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null) return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Teams SQL File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("SQL Files") { Patterns = new[] { "*.sql" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        if (files.Count > 0)
        {
            var path = files[0].Path.LocalPath;
            if (DataContext is ViewModels.BatchAutomationViewModel vm)
            {
                vm.TeamsSqlPath = path;
            }
        }
    }

    private async void BrowsePlayersSql_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null) return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Players SQL File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("SQL Files") { Patterns = new[] { "*.sql" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        if (files.Count > 0)
        {
            var path = files[0].Path.LocalPath;
            if (DataContext is ViewModels.BatchAutomationViewModel vm)
            {
                vm.PlayersSqlPath = path;
            }
        }
    }

    private async void BrowseCsvDir_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null) return;

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select CSV Output Directory",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            var path = folders[0].Path.LocalPath;
            if (DataContext is ViewModels.BatchAutomationViewModel vm)
            {
                vm.BaseCsvDirectory = path;
            }
        }
    }

    private async void BrowsePhotoDir_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null) return;

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Photos Directory",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            var path = folders[0].Path.LocalPath;
            if (DataContext is ViewModels.BatchAutomationViewModel vm)
            {
                vm.BasePhotoDirectory = path;
            }
        }
    }

    private async void BrowseOutputDir_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null) return;

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Output Directory",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            var path = folders[0].Path.LocalPath;
            if (DataContext is ViewModels.BatchAutomationViewModel vm)
            {
                vm.BaseOutputDirectory = path;
            }
        }
    }

    private async void CopyLog_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.BatchAutomationViewModel vm)
        {
            var logText = string.Join(Environment.NewLine, vm.LogLines);
            
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(logText);
            }
        }
    }

    private async void SaveLog_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.BatchAutomationViewModel vm) return;

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Batch Log",
            SuggestedFileName = $"batch_log_{timestamp}.txt",
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

    private async void LoadTeamsCsv_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null) return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Teams CSV File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("CSV Files") { Patterns = new[] { "*.csv" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        if (files.Count > 0)
        {
            var path = files[0].Path.LocalPath;
            if (DataContext is ViewModels.BatchAutomationViewModel vm)
            {
                await vm.LoadTeamsFromCsvFileAsync(path);
            }
        }
    }

    private async void SaveTeamsCsv_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null) return;

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Teams CSV File",
            SuggestedFileName = "teams.csv",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("CSV Files") { Patterns = new[] { "*.csv" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        if (file != null)
        {
            var path = file.Path.LocalPath;
            if (DataContext is ViewModels.BatchAutomationViewModel vm)
            {
                await vm.SaveTeamsToCsvAsync(path);
            }
        }
    }

    private async void OpenMissingPhotoFolders_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.BatchAutomationViewModel vm)
        {
            vm.RefreshMissingPhotoFoldersCommand.Execute(null);
            var dialog = new MissingPhotoFoldersDialog(vm);
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is Window window)
            {
                await dialog.ShowDialog(window);
            }
            else
            {
                dialog.Show();
            }
        }
    }

    private async void BrowseSizeProfile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null) return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
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
            var path = files[0].Path.LocalPath;
            if (DataContext is ViewModels.BatchAutomationViewModel vm)
            {
                vm.SizeProfilePath = path;
            }
        }
    }

    private async void BrowsePhotoManifest_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null) return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Photo Manifest JSON",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        if (files.Count > 0)
        {
            var path = files[0].Path.LocalPath;
            if (DataContext is ViewModels.BatchAutomationViewModel vm)
            {
                vm.PhotoManifestPath = path;
            }
        }
    }

    private void CropOffsetSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (DataContext is ViewModels.BatchAutomationViewModel vm)
        {
            vm.RequestAutoPreviewFromUi();
        }
    }

    private async void OpenManualMapping_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.BatchAutomationViewModel vm)
        {
            return;
        }

        var team = vm.SelectedTeam;
        if (team == null)
        {
            await ShowInfoDialogAsync("Manual Mapping", "Select a team first.");
            return;
        }

        var mappedCsvPath = team.MappedCsvPath;
        if (string.IsNullOrWhiteSpace(mappedCsvPath))
        {
            var teamCsvDir = Path.Combine(vm.BaseCsvDirectory ?? string.Empty, team.TeamName);
            mappedCsvPath = Path.Combine(teamCsvDir, $"mapped_{team.TeamName}.csv");
        }

        if (string.IsNullOrWhiteSpace(mappedCsvPath) || !File.Exists(mappedCsvPath))
        {
            await ShowInfoDialogAsync("Manual Mapping", $"No mapped CSV found for {team.TeamName}. Run the map step first.");
            return;
        }

        var teamPhotoDir = vm.UseTeamPhotoSubdirectories
            ? Path.Combine(vm.BasePhotoDirectory ?? string.Empty, team.TeamName)
            : vm.BasePhotoDirectory;

        if (string.IsNullOrWhiteSpace(teamPhotoDir) || !Directory.Exists(teamPhotoDir))
        {
            await ShowInfoDialogAsync("Manual Mapping", $"Photo directory not found for {team.TeamName}.");
            return;
        }

        var dialogVm = new ManualPhotoMappingDialogViewModel(
            $"Manual Mapping: {team.TeamName}",
            mappedCsvPath,
            teamPhotoDir,
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
            await vm.RefreshTeamMappingSummaryAsync(team, mappedCsvPath);
        }
    }

    private async Task ShowInfoDialogAsync(string title, string message)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null)
        {
            return;
        }

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
