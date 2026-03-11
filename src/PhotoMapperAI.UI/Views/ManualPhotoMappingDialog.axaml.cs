using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PhotoMapperAI.UI.ViewModels;

namespace PhotoMapperAI.UI.Views;

public partial class ManualPhotoMappingDialog : Window
{
    public ManualPhotoMappingDialog()
    {
        InitializeComponent();
    }

    public ManualPhotoMappingDialog(object dataContext) : this()
    {
        DataContext = dataContext;
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ManualPhotoMappingDialogViewModel vm)
        {
            Close(false);
            return;
        }

        var saved = await vm.SaveChangesAsync();
        if (saved)
        {
            Close(true);
        }
    }

    private void Undo_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ManualPhotoMappingDialogViewModel vm)
        {
            return;
        }

        if (sender is Control { DataContext: ManualMappingAssignmentItem assignment })
        {
            vm.RemoveAssignmentCommand.Execute(assignment);
        }
    }
}
