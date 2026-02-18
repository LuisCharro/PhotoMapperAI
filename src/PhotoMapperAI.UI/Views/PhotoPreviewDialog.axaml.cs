using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PhotoMapperAI.UI.Views;

public partial class PhotoPreviewDialog : Window
{
    public PhotoPreviewDialog()
    {
        InitializeComponent();
    }

    public PhotoPreviewDialog(object dataContext) : this()
    {
        DataContext = dataContext;
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
