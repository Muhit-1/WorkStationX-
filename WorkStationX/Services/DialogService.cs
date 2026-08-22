using System.Windows;
using Microsoft.Win32;

namespace WorkStationX.Services;

public interface IDialogService
{
    string? PickExecutable();
    bool Confirm(string message, string title = "WorkStationX");
    void Inform(string message, string title = "WorkStationX");
    bool? ShowDialog<TWindow>(object viewModel) where TWindow : Window, new();
}

/// <summary>
/// The only place allowed to touch Window and the common dialogs. View-models depend
/// on this interface, which is what keeps them testable and free of UI types.
/// </summary>
public class DialogService : IDialogService
{
    public string? PickExecutable()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose an application",
            Filter = "Programs (*.exe;*.lnk)|*.exe;*.lnk|All files (*.*)|*.*",
            CheckFileExists = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public bool Confirm(string message, string title = "WorkStationX") =>
        MessageBox.Show(Owner(), message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
            == MessageBoxResult.Yes;

    public void Inform(string message, string title = "WorkStationX") =>
        MessageBox.Show(Owner(), message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public bool? ShowDialog<TWindow>(object viewModel) where TWindow : Window, new()
    {
        var window = new TWindow
        {
            DataContext = viewModel,
            Owner = Owner()
        };

        return window.ShowDialog();
    }

    private static Window? Owner() =>
        Application.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.IsActive)
        ?? Application.Current?.MainWindow;
}
