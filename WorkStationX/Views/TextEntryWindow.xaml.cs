using System.Windows;

namespace WorkStationX.Views;

public partial class TextEntryWindow : Window
{
    public TextEntryWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => Input.Focus();
    }

    public string? EnteredText { get; private set; }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        EnteredText = Input.Text;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
