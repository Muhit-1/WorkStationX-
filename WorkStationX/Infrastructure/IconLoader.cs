using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace WorkStationX.Infrastructure;

/// <summary>
/// Pulls the shell icon out of an .exe so a workspace row looks like the app it launches.
///
/// Uses SHGetFileInfo rather than System.Drawing.Common: it avoids a ~1 MB dependency
/// for one feature, and returns the icon the shell actually shows (which is the right
/// one for shortcuts and registered file types).
/// </summary>
public static class IconLoader
{
    private static readonly ConcurrentDictionary<string, BitmapSource?> Cache = new();

    /// <summary>Null when the path is missing or has no icon; callers show a placeholder.</summary>
    public static BitmapSource? ForFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return Cache.GetOrAdd(path, Extract);
    }

    private static BitmapSource? Extract(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var info = new NativeMethods.SHFILEINFO();
        var result = NativeMethods.SHGetFileInfo(
            path, 0, ref info, (uint)System.Runtime.InteropServices.Marshal.SizeOf(info),
            NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON);

        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(
                info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

            // Freezing lets the icon cross threads and stops WPF re-rendering it.
            source.Freeze();
            return source;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            NativeMethods.DestroyIcon(info.hIcon);
        }
    }
}
