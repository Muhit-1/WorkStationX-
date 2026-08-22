using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WorkStationX.Services;

namespace WorkStationX.Views;

public partial class RegionSelectOverlay : Window
{
    private readonly ScreenShot _shot;
    private Point _origin;
    private bool _dragging;

    public RegionSelectOverlay(ScreenShot shot)
    {
        _shot = shot;
        InitializeComponent();

        Left = shot.Left;
        Top = shot.Top;
        Width = shot.Width;
        Height = shot.Height;

        Frozen.Source = shot.Image;
        Frozen.Width = shot.Width;
        Frozen.Height = shot.Height;
        Dim.Width = shot.Width;
        Dim.Height = shot.Height;

        Canvas.SetLeft(Hint, Math.Max(0, (shot.Width - 320) / 2.0));
        Canvas.SetTop(Hint, 60);

        MouseLeftButtonDown += OnDown;
        MouseMove += OnMove;
        MouseLeftButtonUp += OnUp;
        KeyDown += OnKey;
        Loaded += (_, _) => Focus();
    }

    /// <summary>Selection in image coordinates, or null if cancelled.</summary>
    public Int32Rect? Selection { get; private set; }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        _origin = e.GetPosition(Root);
        _dragging = true;
        Hint.Visibility = Visibility.Collapsed;
        Marquee.Visibility = Visibility.Visible;
        SizeChip.Visibility = Visibility.Visible;
        CaptureMouse();
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var p = e.GetPosition(Root);
        var rect = Normalise(_origin, p);

        Canvas.SetLeft(Marquee, rect.X);
        Canvas.SetTop(Marquee, rect.Y);
        Marquee.Width = rect.Width;
        Marquee.Height = rect.Height;

        SizeText.Text = $"{(int)rect.Width} × {(int)rect.Height}";
        Canvas.SetLeft(SizeChip, Math.Min(rect.X, ActualWidth - 110));
        Canvas.SetTop(SizeChip, Math.Max(0, rect.Y - 34));

        // Clear the dimming inside the selection so the user sees the real thing.
        Dim.Clip = new CombinedGeometry(
            GeometryCombineMode.Exclude,
            new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)),
            new RectangleGeometry(rect));
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        ReleaseMouseCapture();

        var rect = Normalise(_origin, e.GetPosition(Root));

        // A click with no drag is a cancel, not a zero-pixel capture.
        if (rect.Width < 4 || rect.Height < 4)
        {
            Selection = null;
            DialogResult = false;
            Close();
            return;
        }

        Selection = new Int32Rect(
            (int)Math.Round(rect.X),
            (int)Math.Round(rect.Y),
            (int)Math.Round(rect.Width),
            (int)Math.Round(rect.Height));

        DialogResult = true;
        Close();
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Selection = null;
            DialogResult = false;
            Close();
        }
    }

    /// <summary>Dragging up-left must work exactly like dragging down-right.</summary>
    private static Rect Normalise(Point a, Point b) => new(
        Math.Min(a.X, b.X),
        Math.Min(a.Y, b.Y),
        Math.Abs(b.X - a.X),
        Math.Abs(b.Y - a.Y));
}
