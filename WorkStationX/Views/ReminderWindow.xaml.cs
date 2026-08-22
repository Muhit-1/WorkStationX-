using System.Windows;

namespace WorkStationX.Views;

public partial class ReminderWindow : Window
{
    public ReminderWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    /// <summary>True when the user asked to be reminded again shortly.</summary>
    public bool Snoozed { get; private set; }

    // Bottom-right of the working area, above the taskbar, like every other
    // notification the user sees.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 16;
        Top = area.Bottom - ActualHeight - 16;
    }

    private void OnSnooze(object sender, RoutedEventArgs e)
    {
        Snoozed = true;
        Close();
    }

    private void OnDismiss(object sender, RoutedEventArgs e) => Close();
}
