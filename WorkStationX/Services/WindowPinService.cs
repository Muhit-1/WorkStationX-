using System.Diagnostics;
using System.Text;
using Serilog;
using WorkStationX.Infrastructure;

namespace WorkStationX.Services;

/// <summary>A top-level window the user could pin.</summary>
public sealed record PinnableWindow(IntPtr Handle, string Title, string ProcessName)
{
    public string Display => $"{Title}";

    public string Detail => ProcessName;
}

public interface IWindowPinService
{
    IReadOnlyList<PinnableWindow> ListWindows();
    IReadOnlyCollection<IntPtr> Pinned { get; }
    bool IsPinned(IntPtr handle);
    bool Pin(IntPtr handle);
    bool Unpin(IntPtr handle);
    void UnpinAll();
}

/// <summary>
/// Forces chosen windows to stay above everything else, via SetWindowPos.
/// </summary>
public class WindowPinService : IWindowPinService
{
    private readonly HashSet<IntPtr> _pinned = new();

    public IReadOnlyCollection<IntPtr> Pinned => _pinned;

    public bool IsPinned(IntPtr handle) => _pinned.Contains(handle);

    /// <summary>
    /// Top-level windows worth showing in a picker.
    ///
    /// Raw EnumWindows returns hundreds of entries, nearly all of them invisible
    /// helper windows. Three filters cut it to what a person would recognise:
    /// visible, has a title, and is not DWM-cloaked (the hidden UWP shells that
    /// otherwise appear as ghost entries with real-looking names).
    /// </summary>
    public IReadOnlyList<PinnableWindow> ListWindows()
    {
        var found = new List<PinnableWindow>();
        var shell = NativeMethodsExtra.GetShellWindow();
        var ownPid = Environment.ProcessId;

        NativeMethodsExtra.EnumWindows((hWnd, _) =>
        {
            if (hWnd == shell || !NativeMethodsExtra.IsWindowVisible(hWnd))
            {
                return true;
            }

            var length = NativeMethodsExtra.GetWindowTextLength(hWnd);
            if (length == 0)
            {
                return true;
            }

            if (IsCloaked(hWnd))
            {
                return true;
            }

            NativeMethodsExtra.GetWindowThreadProcessId(hWnd, out var pid);
            if (pid == ownPid)
            {
                // Pinning our own window from our own picker is just confusing.
                return true;
            }

            var builder = new StringBuilder(length + 1);
            NativeMethodsExtra.GetWindowText(hWnd, builder, builder.Capacity);

            found.Add(new PinnableWindow(hWnd, builder.ToString(), ProcessName(pid)));
            return true;
        }, IntPtr.Zero);

        return found
            .OrderBy(w => w.ProcessName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(w => w.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static bool IsCloaked(IntPtr hWnd)
    {
        try
        {
            var hr = NativeMethodsExtra.DwmGetWindowAttribute(
                hWnd, NativeMethodsExtra.DWMWA_CLOAKED, out var cloaked, sizeof(int));
            return hr == 0 && cloaked != 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string ProcessName(uint pid)
    {
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch (Exception)
        {
            // Elevated or exiting processes refuse to identify themselves.
            return "unknown";
        }
    }

    public bool Pin(IntPtr handle)
    {
        if (!NativeMethodsExtra.IsWindow(handle))
        {
            return false;
        }

        var ok = NativeMethodsExtra.SetWindowPos(
            handle, NativeMethodsExtra.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethodsExtra.SWP_NOMOVE | NativeMethodsExtra.SWP_NOSIZE |
            NativeMethodsExtra.SWP_NOACTIVATE);

        if (ok)
        {
            _pinned.Add(handle);
            Log.Information("Pinned window {Handle}", handle);
        }

        return ok;
    }

    public bool Unpin(IntPtr handle)
    {
        _pinned.Remove(handle);

        if (!NativeMethodsExtra.IsWindow(handle))
        {
            // Window already closed; dropping it from the set is the whole job.
            return true;
        }

        return NativeMethodsExtra.SetWindowPos(
            handle, NativeMethodsExtra.HWND_NOTOPMOST, 0, 0, 0, 0,
            NativeMethodsExtra.SWP_NOMOVE | NativeMethodsExtra.SWP_NOSIZE |
            NativeMethodsExtra.SWP_NOACTIVATE);
    }

    /// <summary>
    /// Releases every pin. Called on shutdown: a window left topmost after the app
    /// closes cannot be un-stuck by the user without restarting that app.
    /// </summary>
    public void UnpinAll()
    {
        foreach (var handle in _pinned.ToList())
        {
            Unpin(handle);
        }
    }
}
