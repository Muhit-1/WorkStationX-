using System.Windows;

namespace WorkStationX.Views;

public partial class WindowPickerWindow : Window
{
    public WindowPickerWindow() => InitializeComponent();

    private void OnDone(object sender, RoutedEventArgs e) => DialogResult = true;
}
