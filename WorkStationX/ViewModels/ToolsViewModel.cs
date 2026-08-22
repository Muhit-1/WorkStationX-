using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WorkStationX.Services;
using WorkStationX.Views;

namespace WorkStationX.ViewModels;

/// <summary>A window offered in the pin picker.</summary>
public partial class PinRowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isPinned;

    public PinRowViewModel(PinnableWindow window, bool isPinned)
    {
        Window = window;
        _isPinned = isPinned;
    }

    public PinnableWindow Window { get; }

    public string Title => Window.Title;

    public string ProcessName => Window.ProcessName;
}

/// <summary>The screen tools on the bottom rail.</summary>
public partial class ToolsViewModel : ObservableObject
{
    private const int MaxRecentColors = 10;

    private readonly IWindowPinService _pins;
    private readonly IColorPickService _colors;
    private readonly IScreenCaptureService _capture;
    private readonly ISettingsService _settings;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private string? _lastHex;

    private ScreenRulerWindow? _ruler;

    public ToolsViewModel(
        IWindowPinService pins,
        IColorPickService colors,
        IScreenCaptureService capture,
        ISettingsService settings,
        IDialogService dialogs)
    {
        _pins = pins;
        _colors = colors;
        _capture = capture;
        _settings = settings;
        _dialogs = dialogs;

        foreach (var hex in settings.Current.RecentColors.Take(MaxRecentColors))
        {
            RecentColors.Add(hex);
        }

        LastHex = RecentColors.FirstOrDefault();
    }

    public ObservableCollection<string> RecentColors { get; } = new();

    /// <summary>Routes a global shortcut to the matching tool.</summary>
    public void Invoke(HotkeyAction action)
    {
        switch (action)
        {
            case HotkeyAction.CaptureRegion:
                CaptureRegionCommand.Execute(null);
                break;
            case HotkeyAction.CaptureWindow:
                CaptureWindowCommand.Execute(null);
                break;
            case HotkeyAction.PickColour:
                PickColorCommand.Execute(null);
                break;
            case HotkeyAction.ToggleRuler:
                ShowRulerCommand.Execute(null);
                break;
        }
    }

    public ObservableCollection<PinRowViewModel> Windows { get; } = new();

    public int PinnedCount => _pins.Pinned.Count;

    public string PinStatus => PinnedCount == 0 ? "PIN ON TOP" : $"PINNED {PinnedCount}";

    // ---------- window pinner ----------

    [RelayCommand]
    private void ShowPinPicker()
    {
        RefreshWindows();
        _dialogs.ShowDialog<WindowPickerWindow>(this);
    }

    public void RefreshWindows()
    {
        Windows.Clear();
        foreach (var w in _pins.ListWindows())
        {
            Windows.Add(new PinRowViewModel(w, _pins.IsPinned(w.Handle)));
        }
    }

    [RelayCommand]
    private void TogglePin(PinRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var ok = row.IsPinned
            ? _pins.Unpin(row.Window.Handle)
            : _pins.Pin(row.Window.Handle);

        if (ok)
        {
            row.IsPinned = !row.IsPinned;
        }
        else
        {
            // Usually means the window closed between listing and clicking.
            _dialogs.Inform(
                "That window could not be changed. It may have been closed, or it belongs to an elevated app.",
                "Pin on top");
            RefreshWindows();
        }

        OnPropertyChanged(nameof(PinnedCount));
        OnPropertyChanged(nameof(PinStatus));
    }

    [RelayCommand]
    private void UnpinAll()
    {
        _pins.UnpinAll();
        RefreshWindows();
        OnPropertyChanged(nameof(PinnedCount));
        OnPropertyChanged(nameof(PinStatus));
    }

    // ---------- colour picker ----------

    [RelayCommand]
    private void PickColor()
    {
        try
        {
            var owner = Application.Current?.MainWindow;
            var wasVisible = owner is { IsVisible: true };

            // Hide the app while picking: otherwise the most likely thing under the
            // cursor is our own window, which is never what the user wants to sample.
            if (wasVisible)
            {
                owner!.Hide();
            }

            // Capture AFTER hiding, so our own window is not in the frozen image.
            var shot = _capture.CaptureVirtualScreen();
            if (shot is null)
            {
                if (wasVisible)
                {
                    owner!.Show();
                }

                _dialogs.Inform("Could not read the screen.", "Colour picker");
                return;
            }

            var overlay = new ColorPickerOverlay(_colors, shot);
            var result = overlay.ShowDialog();

            if (wasVisible)
            {
                owner!.Show();
            }

            if (result != true || overlay.Picked is not { } picked)
            {
                return;
            }

            var hex = _colors.ToHex(picked);
            Clipboard.SetText(hex);
            LastHex = hex;

            RecentColors.Remove(hex);
            RecentColors.Insert(0, hex);
            while (RecentColors.Count > MaxRecentColors)
            {
                RecentColors.RemoveAt(RecentColors.Count - 1);
            }

            _settings.Current.RecentColors = RecentColors.ToList();
            _settings.Save();
            OnPropertyChanged(nameof(CanClearColors));

            Log.Information("Picked colour {Hex}", hex);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Colour pick failed");
            _dialogs.Inform($"Could not pick a colour.\n\n{ex.Message}");
        }
    }

    /// <summary>True once the strip is long enough to be worth clearing.</summary>
    public bool CanClearColors => RecentColors.Count > 5;

    /// <summary>Empties the swatch strip; it is a scratch pad, not a saved palette.</summary>
    [RelayCommand]
    private void ClearColors()
    {
        RecentColors.Clear();
        LastHex = null;
        _settings.Current.RecentColors = new List<string>();
        _settings.Save();
        OnPropertyChanged(nameof(CanClearColors));
    }

    [RelayCommand]
    private void CopyColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return;
        }

        Clipboard.SetText(hex);
        LastHex = hex;
    }

    // ---------- screenshot ----------

    /// <summary>Drag a region, then annotate it.</summary>
    [RelayCommand]
    private void CaptureRegion() => Capture(CaptureMode.Region);

    /// <summary>
    /// Choose a window to capture, or the whole desktop.
    /// Capturing whatever happened to be in front was rarely what was wanted.
    /// </summary>
    [RelayCommand]
    private void CaptureWindow()
    {
        var picker = new CaptureWindowPicker(_pins.ListWindows());

        if (picker.ShowDialog() != true)
        {
            return;
        }

        if (picker.WholeDesktop)
        {
            Capture(CaptureMode.FullScreen);
            return;
        }

        if (picker.Chosen is { } chosen)
        {
            Capture(CaptureMode.Window, chosen.Handle);
        }
    }

    private enum CaptureMode
    {
        Region,
        FullScreen,
        Window
    }

    private void Capture(CaptureMode mode, IntPtr windowHandle = default)
    {
        var owner = Application.Current?.MainWindow;
        var wasVisible = owner is { IsVisible: true };

        try
        {
            if (wasVisible)
            {
                owner!.Hide();
            }

            // Let the desktop finish repainting where our window was, otherwise the
            // capture can contain a ghost of it.
            System.Threading.Thread.Sleep(200);

            var shot = mode == CaptureMode.Window
                ? _capture.CaptureWindow(windowHandle)
                : _capture.CaptureVirtualScreen();

            if (shot is null)
            {
                _dialogs.Inform("Could not read the screen.", "Screenshot");
                return;
            }

            BitmapSource image = shot.Image;

            if (mode == CaptureMode.Region)
            {
                var selector = new RegionSelectOverlay(shot);
                if (selector.ShowDialog() != true || selector.Selection is not { } sel)
                {
                    return;
                }

                image = new CroppedBitmap(shot.Image, sel);
                image.Freeze();
            }

            // Restore the panel BEFORE opening the editor. Doing it afterwards put the
            // main window in front of the thing the user had just asked to look at.
            if (wasVisible)
            {
                owner!.Show();
            }

            var editor = new AnnotationWindow(image);
            editor.Show();
            editor.Activate();
            editor.Focus();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Screenshot failed");
            _dialogs.Inform($"Could not take a screenshot.\n\n{ex.Message}");
        }
        finally
        {
            // Only if the editor path did not already restore it.
            if (wasVisible && owner is { IsVisible: false })
            {
                owner.Show();
            }
        }
    }

    // ---------- ruler ----------

    /// <summary>Toggles: pressing RULER again closes the one already on screen.</summary>
    [RelayCommand]
    private void ShowRuler()
    {
        if (_ruler is not null)
        {
            _ruler.Close();
            _ruler = null;
            return;
        }

        _ruler = new ScreenRulerWindow();
        _ruler.Closed += (_, _) => _ruler = null;
        _ruler.Show();
    }

    public static Color ParseHex(string hex) => (Color)ColorConverter.ConvertFromString(hex);
}
