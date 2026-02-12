using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PhotoMapperAI.UI.ViewModels;

namespace PhotoMapperAI.UI.Views;

public partial class ExtractStepView : UserControl
{
    public ExtractStepView()
    {
        InitializeComponent();
    }

    private async void BrowseSqlFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ExtractStepViewModel vm)
        {
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage != null)
            {
                var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select SQL Query File",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("SQL Files") { Patterns = new[] { "*.sql" } },
                        new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                    }
                });

                if (files.Count > 0)
                {
                    vm.SqlFilePath = files[0].Path.LocalPath;
                }
            }
        }
    }

    private async void BrowseConnectionStringFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ExtractStepViewModel vm)
        {
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage != null)
            {
                var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Connection String File",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } },
                        new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                    }
                });

                if (files.Count > 0)
                {
                    vm.ConnectionStringPath = files[0].Path.LocalPath;
                }
            }
        }
    }
}
