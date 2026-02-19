using Avalonia.Controls;
using Avalonia.Interactivity;
using PhotoMapperAI.UI.ViewModels;

namespace PhotoMapperAI.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void Step1Button_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.GoToStepCommand.Execute(1);
        }
    }

    private void Step2Button_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.GoToStepCommand.Execute(2);
        }
    }

    private void Step3Button_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.GoToStepCommand.Execute(3);
        }
    }
}
