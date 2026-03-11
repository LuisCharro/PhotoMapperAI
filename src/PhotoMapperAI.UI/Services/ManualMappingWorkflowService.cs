using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using PhotoMapperAI.UI.Models;
using PhotoMapperAI.UI.ViewModels;
using PhotoMapperAI.UI.Views;

namespace PhotoMapperAI.UI.Services;

public sealed class ManualMappingWorkflowService
{
    public async Task<ManualMappingWorkflowResult> OpenAsync(Window owner, ManualMappingWorkflowRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.MappedCsvPath) || !File.Exists(request.MappedCsvPath))
        {
            return new ManualMappingWorkflowResult
            {
                ErrorMessage = "No mapped CSV found. Run the map step first."
            };
        }

        if (string.IsNullOrWhiteSpace(request.PhotosDirectory) || !Directory.Exists(request.PhotosDirectory))
        {
            return new ManualMappingWorkflowResult
            {
                ErrorMessage = "Photo directory not found."
            };
        }

        var dialogVm = new ManualPhotoMappingDialogViewModel(
            request.Title,
            request.MappedCsvPath,
            request.PhotosDirectory,
            request.FilenamePattern,
            request.PhotoManifestPath);
        await dialogVm.InitializeAsync();

        var dialog = new ManualPhotoMappingDialog(dialogVm);
        var saved = await dialog.ShowDialog<bool>(owner);

        return new ManualMappingWorkflowResult
        {
            Saved = saved,
            MappedCsvPath = request.MappedCsvPath
        };
    }
}
