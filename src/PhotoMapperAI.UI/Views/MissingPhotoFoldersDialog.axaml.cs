using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PhotoMapperAI.UI.Views;

public partial class MissingPhotoFoldersDialog : Window
{
    public MissingPhotoFoldersDialog()
    {
        InitializeComponent();
    }

    public MissingPhotoFoldersDialog(object dataContext) : this()
    {
        DataContext = dataContext;
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
