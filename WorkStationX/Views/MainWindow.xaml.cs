using System.Windows;
using WorkStationX.ViewModels;

namespace WorkStationX.Views;

public partial class MainWindow : Window
{
    public MainWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
