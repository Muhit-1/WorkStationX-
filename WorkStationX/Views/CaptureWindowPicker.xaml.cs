using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WorkStationX.Services;

namespace WorkStationX.Views;

public partial class CaptureWindowPicker : Window
{
    public CaptureWindowPicker(IReadOnlyList<PinnableWindow> windows)
    {
        InitializeComponent();
        DataContext = windows;
    }

    /// <summary>The window to capture, or null when the whole desktop was chosen.</summary>
    public PinnableWindow? Chosen { get; private set; }

    /// <summary>True when the user asked for the entire desktop instead.</summary>
    public bool WholeDesktop { get; private set; }

    private void OnCaptureClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PinnableWindow window })
        {
            Accept(window);
        }
    }

    private void OnRowActivated(object sender, MouseButtonEventArgs e)
    {
        if (List.SelectedItem is PinnableWindow window)
        {
            Accept(window);
        }
    }

    private void Accept(PinnableWindow window)
    {
        Chosen = window;
        DialogResult = true;
    }

    private void OnWholeDesktop(object sender, RoutedEventArgs e)
    {
        WholeDesktop = true;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
