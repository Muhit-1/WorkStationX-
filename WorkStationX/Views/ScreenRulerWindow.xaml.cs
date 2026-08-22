using System.Windows;
using System.Windows.Input;
using WorkStationX.Infrastructure;

namespace WorkStationX.Views;

public partial class ScreenRulerWindow : Window
{
    public ScreenRulerWindow()
    {
        InitializeComponent();

        SizeChanged += (_, _) => Refresh();
        LocationChanged += (_, _) => Refresh();
        Loaded += (_, _) => Refresh();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };
    }

    /// <summary>
    /// Reports the measurement in PHYSICAL pixels.
    ///
    /// This is the entire point of the tool: a designer measuring a 200px element on a
    /// 150% display needs 200, not the 133 WPF units it occupies. The scale factor is
    /// read from the monitor this window is currently on, so dragging the ruler to a
    /// second screen with different scaling keeps the number correct.
    /// </summary>
    private void Refresh()
    {
        if (!IsLoaded)
        {
            return;
        }

        var (sx, sy) = DpiHelper.ScaleFor(this);

        var width = (int)Math.Round(ActualWidth * sx);
        var height = (int)Math.Round(ActualHeight * sy);

        SizeText.Text = $"{width} × {height}";
        ScaleText.Text = Math.Abs(sx - 1.0) < 0.001
            ? "100% display"
            : $"{sx * 100:0}% display · {ActualWidth:0} × {ActualHeight:0} WPF units";

        HLine.X1 = 0;
        HLine.X2 = ActualWidth;
        HLine.Y1 = HLine.Y2 = ActualHeight / 2;

        VLine.Y1 = 0;
        VLine.Y2 = ActualHeight;
        VLine.X1 = VLine.X2 = ActualWidth / 2;
    }

    private void OnDragHandle(object sender, MouseButtonEventArgs e) => DragMove();
}
