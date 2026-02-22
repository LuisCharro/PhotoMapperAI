using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using PhotoMapperAI.Services.Database;
using PhotoMapperAI.UI.Models;

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
}