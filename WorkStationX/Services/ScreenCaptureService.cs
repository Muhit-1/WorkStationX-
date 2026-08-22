using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Serilog;
using WorkStationX.Infrastructure;

namespace WorkStationX.Services;

/// <summary>A captured screen image plus where it came from, in physical pixels.</summary>
public sealed record ScreenShot(BitmapSource Image, int Left, int Top, int Width, int Height);

public interface IScreenCaptureService
{
    ScreenShot? CaptureVirtualScreen();
    ScreenShot? CaptureRegion(int left, int top, int width, int height);
    ScreenShot? CaptureWindow(IntPtr handle);
}

/// <summary>
/// Screen capture via GDI BitBlt.
///
/// Not the Windows Graphics Capture API: WGC is designed for video, needs WinRT
/// interop from WPF, and draws a yellow capture border that only Windows 11 can turn
/// off. For a still image BitBlt is a few lines and works everywhere.
/// </summary>
public class ScreenCaptureService : IScreenCaptureService
{
    public ScreenShot? CaptureVirtualScreen()
    {
        // VirtualScreen spans every monitor and is already in physical pixels.
        var left = (int)SystemParameters.VirtualScreenLeft;
        var top = (int)SystemParameters.VirtualScreenTop;
        var width = (int)SystemParameters.VirtualScreenWidth;
        var height = (int)SystemParameters.VirtualScreenHeight;

        return CaptureRegion(left, top, width, height);
    }

    /// <summary>
    /// Captures just one window.
    ///
    /// The window is brought to the front first and given a moment to paint:
    /// BitBlt reads what is actually on the glass, so a window sitting behind
    /// another would otherwise be captured with the other one covering it.
    /// </summary>
    public ScreenShot? CaptureWindow(IntPtr handle)
    {
        if (!NativeMethodsExtra.IsWindow(handle))
        {
            return null;
        }

        NativeMethodsExtra.ShowWindow(handle, NativeMethodsExtra.SW_RESTORE);
        NativeMethodsExtra.SetForegroundWindow(handle);
        Thread.Sleep(260);

        if (!NativeMethodsExtra.GetWindowRect(handle, out var rect))
        {
            return null;
        }

        return CaptureRegion(rect.Left, rect.Top, rect.Width, rect.Height);
    }

    public ScreenShot? CaptureRegion(int left, int top, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var screenDc = NativeMethodsExtra.GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            return null;
        }

        var memDc = IntPtr.Zero;
        var bitmap = IntPtr.Zero;
        var previous = IntPtr.Zero;

        try
        {
            memDc = NativeGdi.CreateCompatibleDC(screenDc);
            bitmap = NativeGdi.CreateCompatibleBitmap(screenDc, width, height);

            if (memDc == IntPtr.Zero || bitmap == IntPtr.Zero)
            {
                return null;
            }

            previous = NativeGdi.SelectObject(memDc, bitmap);

            var ok = NativeGdi.BitBlt(
                memDc, 0, 0, width, height, screenDc, left, top,
                NativeGdi.SRCCOPY | NativeGdi.CAPTUREBLT);

            if (!ok)
            {
                return null;
            }

            var source = Imaging.CreateBitmapSourceFromHBitmap(
                bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

            // Freezing lets the image cross threads and stops WPF re-rendering it.
            source.Freeze();

            return new ScreenShot(source, left, top, width, height);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Screen capture failed");
            return null;
        }
        finally
        {
            // GDI handles are a fixed OS resource; leaking them degrades the whole
            // desktop, not just this app.
            if (previous != IntPtr.Zero && memDc != IntPtr.Zero)
            {
                NativeGdi.SelectObject(memDc, previous);
            }

            if (bitmap != IntPtr.Zero)
            {
                NativeGdi.DeleteObject(bitmap);
            }

            if (memDc != IntPtr.Zero)
            {
                NativeGdi.DeleteDC(memDc);
            }

            NativeMethodsExtra.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }
}
