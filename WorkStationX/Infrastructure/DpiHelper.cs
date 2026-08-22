using System.Windows;
using System.Windows.Media;

namespace WorkStationX.Infrastructure;

/// <summary>
/// Converts between WPF's device-independent units and real screen pixels.
///
/// This is the single most common source of "it works on my machine" bugs in screen
/// tools. WPF measures in 1/96in units; Win32 APIs like GetPixel and GetCursorPos
/// speak physical pixels. On a 150% display the two differ by half again, so a ruler
/// or colour picker that mixes them silently reads the wrong place — and on a laptop
/// plus external monitor the scale factor is different per screen, so a single
/// constant cannot fix it either.
/// </summary>
public static class DpiHelper
{
    /// <summary>Scale factor for the monitor a given visual is on. 1.0 at 100%.</summary>
    public static (double X, double Y) ScaleFor(Visual visual)
    {
        var source = PresentationSource.FromVisual(visual);
        var m = source?.CompositionTarget?.TransformToDevice;

        return m is null ? (1.0, 1.0) : (m.Value.M11, m.Value.M22);
    }

    /// <summary>WPF units to physical pixels, for the monitor this visual is on.</summary>
    public static (int X, int Y) ToPhysical(Visual visual, Point point)
    {
        var (sx, sy) = ScaleFor(visual);
        return ((int)Math.Round(point.X * sx), (int)Math.Round(point.Y * sy));
    }

    /// <summary>Physical pixels back to WPF units.</summary>
    public static Point ToLogical(Visual visual, int x, int y)
    {
        var (sx, sy) = ScaleFor(visual);
        return new Point(x / sx, y / sy);
    }

    /// <summary>
    /// The cursor position in physical pixels, straight from Win32.
    /// Preferred over Mouse.GetPosition for screen tools because it is already in the
    /// coordinate space GetPixel expects, with no per-monitor conversion needed.
    /// </summary>
    public static (int X, int Y) CursorPhysical()
    {
        return NativeMethodsExtra.GetCursorPos(out var p) ? (p.X, p.Y) : (0, 0);
    }
}
