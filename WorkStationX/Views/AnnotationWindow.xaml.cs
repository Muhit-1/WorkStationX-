using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Serilog;
using WorkStationX.Services;

namespace WorkStationX.Views;

public partial class AnnotationWindow : Window
{
    private static readonly string[] Palette =
    {
        "#F0785C", "#63DCC0", "#74C8EA", "#E8A33D", "#FFFFFF", "#101010"
    };

    private readonly BitmapSource _image;

    // Annotations are data, not paint. Undo is then "remove the last one and redraw",
    // which is exactly why the shapes are never burned into the bitmap until export.
    private readonly List<Annotation> _annotations = new();

    private AnnotationTool _tool = AnnotationTool.Pen;
    private string _color = Palette[0];
    private Annotation? _inProgress;

    public AnnotationWindow(BitmapSource image)
    {
        _image = image;
        InitializeComponent();

        Shot.Source = image;
        Shot.Width = image.PixelWidth;
        Shot.Height = image.PixelHeight;
        Overlay.Width = image.PixelWidth;
        Overlay.Height = image.PixelHeight;

        BuildSwatches();

        Overlay.MouseLeftButtonDown += OnDown;
        Overlay.MouseMove += OnMove;
        Overlay.MouseLeftButtonUp += OnUp;

        PreviewKeyDown += OnKey;
    }

    private void BuildSwatches()
    {
        foreach (var hex in Palette)
        {
            var swatch = new Border
            {
                Width = 22,
                Height = 22,
                Margin = new Thickness(0, 0, 5, 0),
                Background = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(hex)),
                BorderThickness = new Thickness(hex == _color ? 2 : 1),
                CornerRadius = new CornerRadius(2),
                Cursor = Cursors.Hand,
                Tag = hex,
                ToolTip = hex
            };

            swatch.SetResourceReference(
                Border.BorderBrushProperty, hex == _color ? "Brush.Prime" : "Brush.EdgeHi");

            swatch.MouseLeftButtonDown += (s, _) =>
            {
                _color = (string)((Border)s).Tag;
                RefreshSwatches();
            };

            Swatches.Items.Add(swatch);
        }
    }

    private void RefreshSwatches()
    {
        foreach (var item in Swatches.Items)
        {
            if (item is not Border b)
            {
                continue;
            }

            var selected = (string)b.Tag == _color;
            b.BorderThickness = new Thickness(selected ? 2 : 1);
            b.SetResourceReference(
                Border.BorderBrushProperty, selected ? "Brush.Prime" : "Brush.EdgeHi");
        }
    }

    private void OnToolChecked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag } &&
            Enum.TryParse<AnnotationTool>(tag, out var tool))
        {
            _tool = tool;
        }
    }

    // ---------- drawing ----------

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        var p = e.GetPosition(Overlay);

        if (_tool == AnnotationTool.Text)
        {
            AddText(p);
            return;
        }

        _inProgress = new Annotation
        {
            Tool = _tool,
            ColorHex = _color,
            Points = { p }
        };

        Overlay.CaptureMouse();
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (_inProgress is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var p = e.GetPosition(Overlay);

        if (_inProgress.Tool == AnnotationTool.Pen)
        {
            _inProgress.Points.Add(p);
        }
        else if (_inProgress.Points.Count > 1)
        {
            _inProgress.Points[1] = p;
        }
        else
        {
            _inProgress.Points.Add(p);
        }

        Redraw(preview: _inProgress);
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        Overlay.ReleaseMouseCapture();

        if (_inProgress is null)
        {
            return;
        }

        // Discard an accidental click that produced nothing.
        var meaningful = _inProgress.Tool == AnnotationTool.Pen
            ? _inProgress.Points.Count > 1
            : (_inProgress.Bounds.Width > 3 || _inProgress.Bounds.Height > 3);

        if (meaningful)
        {
            _annotations.Add(_inProgress);
        }

        _inProgress = null;
        Redraw();
    }

    private void AddText(Point at)
    {
        var dialog = new TextEntryWindow { Owner = this };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.EnteredText))
        {
            return;
        }

        _annotations.Add(new Annotation
        {
            Tool = AnnotationTool.Text,
            ColorHex = _color,
            Text = dialog.EnteredText,
            Points = { at }
        });

        Redraw();
    }

    // ---------- rendering ----------

    private void Redraw(Annotation? preview = null)
    {
        Overlay.Children.Clear();

        foreach (var a in _annotations)
        {
            Draw(a);
        }

        if (preview is not null)
        {
            Draw(preview);
        }
    }

    private void Draw(Annotation a)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(a.ColorHex));

        switch (a.Tool)
        {
            case AnnotationTool.Pen when a.Points.Count > 1:
            {
                var poly = new Polyline
                {
                    Stroke = brush,
                    StrokeThickness = a.Thickness,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Points = new PointCollection(a.Points)
                };
                Overlay.Children.Add(poly);
                break;
            }

            case AnnotationTool.Rectangle:
            {
                var r = a.Bounds;
                var rect = new Rectangle
                {
                    Stroke = brush,
                    StrokeThickness = a.Thickness,
                    Width = r.Width,
                    Height = r.Height
                };
                Canvas.SetLeft(rect, r.X);
                Canvas.SetTop(rect, r.Y);
                Overlay.Children.Add(rect);
                break;
            }

            case AnnotationTool.Arrow:
            {
                Overlay.Children.Add(Line(a.Start, a.End, brush, a.Thickness));

                var (left, right) = a.ArrowHead();
                Overlay.Children.Add(Line(a.End, left, brush, a.Thickness));
                Overlay.Children.Add(Line(a.End, right, brush, a.Thickness));
                break;
            }

            case AnnotationTool.Text when !string.IsNullOrWhiteSpace(a.Text):
            {
                var text = new TextBlock
                {
                    Text = a.Text,
                    Foreground = brush,
                    FontSize = a.FontSize,
                    FontWeight = FontWeights.SemiBold
                };
                Canvas.SetLeft(text, a.Start.X);
                Canvas.SetTop(text, a.Start.Y);
                Overlay.Children.Add(text);
                break;
            }
        }
    }

    private static Line Line(Point a, Point b, Brush brush, double thickness) => new()
    {
        X1 = a.X,
        Y1 = a.Y,
        X2 = b.X,
        Y2 = b.Y,
        Stroke = brush,
        StrokeThickness = thickness,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round
    };

    // ---------- commands ----------

    private void OnUndo(object sender, RoutedEventArgs e)
    {
        if (_annotations.Count > 0)
        {
            _annotations.RemoveAt(_annotations.Count - 1);
            Redraw();
        }
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        _annotations.Clear();
        Redraw();
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (e.KeyboardDevice.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Z:
                OnUndo(sender, e);
                e.Handled = true;
                break;
            case Key.S:
                OnSave(sender, e);
                e.Handled = true;
                break;
            case Key.C:
                OnCopy(sender, e);
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// Flattens the screenshot and the annotations into one bitmap.
    /// The shapes are only ever rasterised here, at export time.
    /// </summary>
    private RenderTargetBitmap Flatten()
    {
        var width = _image.PixelWidth;
        var height = _image.PixelHeight;

        // 96 DPI: the stage is laid out in image pixels at 1:1, so rendering at the
        // screen's DPI would scale the output and produce a bigger, blurrier file.
        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(_image, new Rect(0, 0, width, height));
        }

        target.Render(visual);

        Overlay.UpdateLayout();
        target.Render(Overlay);

        return target;
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetImage(Flatten());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not copy annotated image");
            MessageBox.Show(this, "Could not copy the image.", "WorkStationX");
        }
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save screenshot",
            Filter = "PNG image (*.png)|*.png",
            FileName = $"workstationx-{DateTime.Now:yyyyMMdd-HHmmss}.png",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(Flatten()));

            using var stream = File.Create(dialog.FileName);
            encoder.Save(stream);

            Log.Information("Saved screenshot to {Path}", dialog.FileName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not save screenshot");
            MessageBox.Show(this, $"Could not save.\n\n{ex.Message}", "WorkStationX");
        }
    }
}
