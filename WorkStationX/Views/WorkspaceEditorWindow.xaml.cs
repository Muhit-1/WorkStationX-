using System.Windows;
using WorkStationX.ViewModels;

namespace WorkStationX.Views;

public partial class WorkspaceEditorWindow : Window
{
    public WorkspaceEditorWindow() => InitializeComponent();

    // Dialog plumbing only; validation lives in the view-model.
    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (DataContext is WorkspaceEditorViewModel vm && vm.TryAccept())
        {
            DialogResult = true;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
