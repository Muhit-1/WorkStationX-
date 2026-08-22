using System.Windows;

namespace WorkStationX.Services;

public enum AnnotationTool
{
    Pen = 0,
    Rectangle = 1,
    Arrow = 2,
    Text = 3
}

/// <summary>
/// One annotation, stored as data rather than pixels.
///
/// Keeping shapes as objects instead of painting straight onto the bitmap is what
/// makes undo possible at all: undo becomes "drop the last item", not "remember and
/// restore a region of the image".
/// </summary>
public class Annotation
{
    public AnnotationTool Tool { get; init; }

    public string ColorHex { get; init; } = "#F0785C";

    public double Thickness { get; init; } = 3;

    /// <summary>Pen uses every point; the others use the first and last.</summary>
    public List<Point> Points { get; init; } = new();

    public string? Text { get; set; }

    public double FontSize { get; init; } = 18;

    public Point Start => Points.Count > 0 ? Points[0] : default;

    public Point End => Points.Count > 0 ? Points[^1] : default;

    /// <summary>Normalised rectangle, so dragging up-left works the same as down-right.</summary>
    public Rect Bounds
    {
        get
        {
            var a = Start;
            var b = End;
            return new Rect(
                Math.Min(a.X, b.X),
                Math.Min(a.Y, b.Y),
                Math.Abs(b.X - a.X),
                Math.Abs(b.Y - a.Y));
        }
    }

    /// <summary>
    /// The two short strokes of an arrowhead, at a fixed angle to the shaft.
    /// Returned as geometry so the renderer stays free of trigonometry.
    /// </summary>
    public (Point Left, Point Right) ArrowHead(double length = 18, double spreadDegrees = 28)
    {
        var dx = End.X - Start.X;
        var dy = End.Y - Start.Y;
        var angle = Math.Atan2(dy, dx);
        var spread = spreadDegrees * Math.PI / 180;

        var left = new Point(
            End.X - length * Math.Cos(angle - spread),
            End.Y - length * Math.Sin(angle - spread));

        var right = new Point(
            End.X - length * Math.Cos(angle + spread),
            End.Y - length * Math.Sin(angle + spread));

        return (left, right);
    }
}
