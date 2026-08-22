using System.Windows.Media;
using WorkStationX.Infrastructure;

namespace WorkStationX.Services;

public interface IColorPickService
{
    Color ReadPixel(int physicalX, int physicalY);
    string ToHex(Color color);
}

/// <summary>Reads a single pixel off the screen.</summary>
public class ColorPickService : IColorPickService
{
    /// <summary>
    /// Coordinates are PHYSICAL pixels, not WPF units - see DpiHelper. GetPixel
    /// returns a COLORREF, which is 0x00BBGGRR: blue and red are swapped relative to
    /// the order every other API uses, and reading it as RGB is a classic silent bug.
    /// </summary>
    public Color ReadPixel(int physicalX, int physicalY)
    {
        var dc = NativeMethodsExtra.GetDC(IntPtr.Zero);
        if (dc == IntPtr.Zero)
        {
            return Colors.Black;
        }

        try
        {
            var colorRef = NativeMethodsExtra.GetPixel(dc, physicalX, physicalY);

            return Color.FromRgb(
                (byte)(colorRef & 0x000000FF),
                (byte)((colorRef & 0x0000FF00) >> 8),
                (byte)((colorRef & 0x00FF0000) >> 16));
        }
        finally
        {
            NativeMethodsExtra.ReleaseDC(IntPtr.Zero, dc);
        }
    }

    public string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}
