using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Serilog;
using WorkStationX.Infrastructure;

namespace WorkStationX.Services;

/// <summary>Actions a global shortcut can trigger.</summary>
public enum HotkeyAction
{
    CaptureRegion = 0,
    CaptureWindow = 1,
    PickColour = 2,
    ToggleRuler = 3,
    ShowApp = 4
}

/// <summary>A key combination, stored as text so settings.json stays readable.</summary>
public sealed record HotkeyBinding(HotkeyAction Action, ModifierKeys Modifiers, Key Key)
{
    public bool IsEmpty => Key == Key.None;

    /// <summary>"Ctrl + Shift + 2".</summary>
    public string Display
    {
        get
        {
            if (IsEmpty)
            {
                return "Not set";
            }

            var parts = new List<string>();
            if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
            parts.Add(KeyName(Key));

            return string.Join(" + ", parts);
        }
    }

    private static string KeyName(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => ((int)key - (int)Key.D0).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => $"Num{(int)key - (int)Key.NumPad0}",
        _ => key.ToString()
    };

    public string Serialise() => IsEmpty ? string.Empty : $"{(int)Modifiers}|{(int)Key}";

    public static HotkeyBinding Parse(HotkeyAction action, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new HotkeyBinding(action, ModifierKeys.None, Key.None);
        }

        var parts = text.Split('|');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out var mods) &&
            int.TryParse(parts[1], out var key))
        {
            return new HotkeyBinding(action, (ModifierKeys)mods, (Key)key);
        }

        return new HotkeyBinding(action, ModifierKeys.None, Key.None);
    }
}

public interface IHotkeyService
{
    /// <summary>Bindings that Windows refused, usually because another app owns them.</summary>
    IReadOnlyList<HotkeyAction> Conflicts { get; }

    void Attach(Window window);
    void Rebind(IEnumerable<HotkeyBinding> bindings);
    event EventHandler<HotkeyAction>? Triggered;
}

/// <summary>
/// System-wide shortcuts via RegisterHotKey.
///
/// These fire even when WorkStationX has no focus, which is the whole point: needing
/// to click the app first to reach a screen tool defeats the tool. That also means
/// Windows can refuse a combination another program already owns, so failures are
/// collected and surfaced rather than swallowed.
/// </summary>
public class HotkeyService : IHotkeyService, IDisposable
{
    private readonly List<HotkeyAction> _conflicts = new();
    private readonly List<int> _registered = new();

    private HwndSource? _source;
    private IntPtr _handle = IntPtr.Zero;

    public IReadOnlyList<HotkeyAction> Conflicts => _conflicts;

    public event EventHandler<HotkeyAction>? Triggered;

    /// <summary>
    /// Hotkeys need a window handle to deliver WM_HOTKEY to. The main window is used
    /// because it lives for the whole session, even while hidden in the tray.
    /// </summary>
    public void Attach(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _handle = helper.Handle != IntPtr.Zero ? helper.Handle : helper.EnsureHandle();

        _source = HwndSource.FromHwnd(_handle);
        _source?.AddHook(WndProc);
    }

    public void Rebind(IEnumerable<HotkeyBinding> bindings)
    {
        UnregisterAll();
        _conflicts.Clear();

        if (_handle == IntPtr.Zero)
        {
            return;
        }

        foreach (var binding in bindings.Where(b => !b.IsEmpty))
        {
            var id = (int)binding.Action;
            var mods = ToNative(binding.Modifiers) | NativeHotkeys.MOD_NOREPEAT;
            var vk = (uint)KeyInterop.VirtualKeyFromKey(binding.Key);

            if (NativeHotkeys.RegisterHotKey(_handle, id, mods, vk))
            {
                _registered.Add(id);
            }
            else
            {
                _conflicts.Add(binding.Action);
                Log.Warning(
                    "Hotkey {Combo} for {Action} was refused; another app likely owns it",
                    binding.Display, binding.Action);
            }
        }
    }

    private static uint ToNative(ModifierKeys modifiers)
    {
        uint value = 0;
        if (modifiers.HasFlag(ModifierKeys.Control)) value |= NativeHotkeys.MOD_CONTROL;
        if (modifiers.HasFlag(ModifierKeys.Alt)) value |= NativeHotkeys.MOD_ALT;
        if (modifiers.HasFlag(ModifierKeys.Shift)) value |= NativeHotkeys.MOD_SHIFT;
        if (modifiers.HasFlag(ModifierKeys.Windows)) value |= NativeHotkeys.MOD_WIN;
        return value;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != NativeHotkeys.WM_HOTKEY)
        {
            return IntPtr.Zero;
        }

        var id = wParam.ToInt32();
        if (Enum.IsDefined(typeof(HotkeyAction), id))
        {
            Triggered?.Invoke(this, (HotkeyAction)id);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void UnregisterAll()
    {
        if (_handle == IntPtr.Zero)
        {
            return;
        }

        foreach (var id in _registered)
        {
            NativeHotkeys.UnregisterHotKey(_handle, id);
        }

        _registered.Clear();
    }

    public void Dispose()
    {
        UnregisterAll();
        _source?.RemoveHook(WndProc);
        _source = null;
        GC.SuppressFinalize(this);
    }
}

/// <summary>Reads and writes hotkey bindings, and supplies the defaults.</summary>
public static class HotkeyDefaults
{
    /// <summary>
    /// Ctrl+Shift+... deliberately: Windows reserves most Win+key combinations, and
    /// plain Ctrl+key would collide with whatever app is in front.
    /// </summary>
    public static IReadOnlyList<HotkeyBinding> All => new[]
    {
        new HotkeyBinding(HotkeyAction.CaptureRegion, ModifierKeys.Control | ModifierKeys.Shift, Key.D2),
        new HotkeyBinding(HotkeyAction.CaptureWindow, ModifierKeys.Control | ModifierKeys.Shift, Key.D3),
        new HotkeyBinding(HotkeyAction.PickColour, ModifierKeys.Control | ModifierKeys.Shift, Key.D4),
        new HotkeyBinding(HotkeyAction.ToggleRuler, ModifierKeys.Control | ModifierKeys.Shift, Key.D5),
        new HotkeyBinding(HotkeyAction.ShowApp, ModifierKeys.Control | ModifierKeys.Shift, Key.W)
    };

    public static string Describe(HotkeyAction action) => action switch
    {
        HotkeyAction.CaptureRegion => "Capture a region",
        HotkeyAction.CaptureWindow => "Capture a window",
        HotkeyAction.PickColour => "Pick a colour",
        HotkeyAction.ToggleRuler => "Show or hide the ruler",
        HotkeyAction.ShowApp => "Bring WorkStationX to the front",
        _ => action.ToString()
    };

    public static IReadOnlyList<HotkeyBinding> Load(UserSettings settings)
    {
        var result = new List<HotkeyBinding>();

        foreach (var fallback in All)
        {
            var key = fallback.Action.ToString();

            // A stored empty string means the user deliberately cleared it, which is
            // different from never having set one - so only fall back when absent.
            result.Add(settings.Hotkeys.TryGetValue(key, out var stored)
                ? HotkeyBinding.Parse(fallback.Action, stored)
                : fallback);
        }

        return result;
    }

    public static void Save(UserSettings settings, IEnumerable<HotkeyBinding> bindings)
    {
        foreach (var binding in bindings)
        {
            settings.Hotkeys[binding.Action.ToString()] = binding.Serialise();
        }
    }
}
