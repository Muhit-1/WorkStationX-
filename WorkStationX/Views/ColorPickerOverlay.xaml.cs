using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WorkStationX.Services;

namespace WorkStationX.Views;

public partial class ColorPickerOverlay : Window
{
    private const int ZoomFactor = 8;
    private const int ZoomSourcePixels = 12;

    private readonly IColorPickService _picker;
    private readonly ScreenShot _shot;
    private readonly SolidColorBrush _swatchBrush = new(Colors.Black);

    private byte[]? _pixels;
    private int _stride;
    private Color _current = Colors.Black;

    public ColorPickerOverlay(IColorPickService picker, ScreenShot shot)
    {
        _picker = picker;
        _shot = shot;

        InitializeComponent();

        Left = shot.Left;
        Top = shot.Top;
        Width = shot.Width;
        Height = shot.Height;

        Frozen.Source = shot.Image;
        Frozen.Width = shot.Width;
        Frozen.Height = shot.Height;
        Swatch.Background = _swatchBrush;

        CachePixels();

        MouseMove += OnMouseMove;
        MouseLeftButtonDown += OnPick;
        KeyDown += OnKeyDown;
        Loaded += (_, _) => Focus();
    }

    /// <summary>The colour the user settled on, or null if they cancelled.</summary>
    public Color? Picked { get; private set; }

    /// <summary>
    /// Copies the whole capture into a byte array once.
    ///
    /// Reading a pixel then becomes an array index instead of a GPU round trip, which
    /// is what makes tracking the cursor smooth rather than laggy.
    /// </summary>
    private void CachePixels()
    {
        try
        {
            var converted = new FormatConvertedBitmap(_shot.Image, PixelFormats.Bgra32, null, 0);
            converted.Freeze();

            _stride = converted.PixelWidth * 4;
            _pixels = new byte[_stride * converted.PixelHeight];
            converted.CopyPixels(_pixels, _stride, 0);
        }
        catch (Exception)
        {
            // Fall back to reading the live screen if the copy fails.
            _pixels = null;
        }
    }

    private Color SampleAt(int x, int y)
    {
        if (_pixels is null)
        {
            return _picker.ReadPixel(_shot.Left + x, _shot.Top + y);
        }

        if (x < 0 || y < 0 || x >= _shot.Width || y >= _shot.Height)
        {
            return Colors.Black;
        }

        var offset = y * _stride + x * 4;
        return Color.FromRgb(_pixels[offset + 2], _pixels[offset + 1], _pixels[offset]);
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var p = e.GetPosition(Root);

        // The window is placed at the capture origin at 1:1, so canvas coordinates
        // are already image pixels.
        var x = (int)Math.Round(p.X);
        var y = (int)Math.Round(p.Y);

        _current = SampleAt(x, y);
        _swatchBrush.Color = _current;
        HexText.Text = _picker.ToHex(_current);
        RgbText.Text = $"rgb({_current.R}, {_current.G}, {_current.B})";

        UpdateZoom(x, y);

        // Keep the readout beside the cursor and always fully on screen.
        Readout.UpdateLayout();
        var rx = Math.Clamp(p.X + 26, 0, Math.Max(0, ActualWidth - Readout.ActualWidth - 8));
        var ry = Math.Clamp(p.Y + 26, 0, Math.Max(0, ActualHeight - Readout.ActualHeight - 8));
        Canvas.SetLeft(Readout, rx);
        Canvas.SetTop(Readout, ry);
    }

    private void UpdateZoom(int x, int y)
    {
        try
        {
            var half = ZoomSourcePixels / 2;
            var sx = Math.Clamp(x - half, 0, Math.Max(0, _shot.Width - ZoomSourcePixels));
            var sy = Math.Clamp(y - half, 0, Math.Max(0, _shot.Height - ZoomSourcePixels));

            var crop = new CroppedBitmap(
                _shot.Image, new Int32Rect(sx, sy, ZoomSourcePixels, ZoomSourcePixels));

            Zoom.Source = crop;
            Zoom.Width = ZoomSourcePixels * ZoomFactor;
            Zoom.Height = ZoomSourcePixels * ZoomFactor;
        }
        catch (Exception)
        {
            // Cropping can fail at the very edge of the capture; the swatch still works.
        }
    }

    private void OnPick(object sender, MouseButtonEventArgs e)
    {
        Picked = _current;
        DialogResult = true;
        Close();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Picked = null;
            DialogResult = false;
            Close();
        }
    }
}
