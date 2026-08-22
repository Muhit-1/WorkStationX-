using System.Windows.Controls;
using System.Windows.Input;
using WorkStationX.ViewModels;

namespace WorkStationX.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();

        // PreviewKeyDown, not KeyDown: capture has to see the key before any control
        // consumes it, and before Tab or Space move focus instead of being recorded.
        PreviewKeyDown += OnPreviewKeyDown;
        Focusable = true;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm || vm.Capturing is not { } row)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Escape abandons the capture rather than binding itself.
        if (key == Key.Escape)
        {
            row.IsCapturing = false;
            e.Handled = true;
            return;
        }

        // A modifier alone is not a shortcut; wait for the real key.
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System)
        {
            e.Handled = true;
            return;
        }

        var modifiers = Keyboard.Modifiers;

        // Without a modifier the shortcut would swallow that key everywhere in Windows,
        // so a bare letter is refused rather than quietly breaking typing.
        if (modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            return;
        }

        vm.CompleteCapture(row, modifiers, key);
        e.Handled = true;
    }
}
