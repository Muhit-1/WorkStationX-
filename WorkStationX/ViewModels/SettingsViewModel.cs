using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Serilog;
using WorkStationX.Data;
using WorkStationX.Models;
using WorkStationX.Services;

namespace WorkStationX.ViewModels;

public partial class SettingsViewModel : PageViewModelBase
{
    private readonly IThemeService _themes;
    private readonly ISettingsService _settings;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IDialogService _dialogs;
    private readonly IHotkeyService _hotkeys;

    [ObservableProperty]
    private ThemeOption? _selectedTheme;

    // ---- new reminder form ----
    [ObservableProperty]
    private string _newMessage = string.Empty;

    [ObservableProperty]
    private ReminderScheduleType _newScheduleType = ReminderScheduleType.Hourly;

    [ObservableProperty]
    private int _newIntervalHours = 1;

    [ObservableProperty]
    private string _newTimeOfDay = "09:00";

    [ObservableProperty]
    private DayOfWeek _newDayOfWeek = DayOfWeek.Monday;

    [ObservableProperty]
    private string? _reminderError;

    public SettingsViewModel(
        IThemeService themes,
        ISettingsService settings,
        IDbContextFactory<AppDbContext> dbFactory,
        IDialogService dialogs,
        IHotkeyService hotkeys)
    {
        _hotkeys = hotkeys;
        _themes = themes;
        _settings = settings;
        _dbFactory = dbFactory;
        _dialogs = dialogs;

        Themes = new ObservableCollection<ThemeOption>(themes.Available);
        _selectedTheme = themes.Current;

        foreach (var binding in HotkeyDefaults.Load(settings.Current))
        {
            Hotkeys.Add(new HotkeyRowViewModel(binding));
        }
    }

    public override string Title => "Settings";

    public override string Glyph => string.Empty;

    public ObservableCollection<ThemeOption> Themes { get; }

    public ObservableCollection<ReminderRowViewModel> Reminders { get; } = new();

    public ObservableCollection<HotkeyRowViewModel> Hotkeys { get; } = new();

    /// <summary>The row waiting for a key press, if any.</summary>
    public HotkeyRowViewModel? Capturing =>
        Hotkeys.FirstOrDefault(h => h.IsCapturing);

    /// <summary>Puts one row into capture mode; only one at a time.</summary>
    [RelayCommand]
    private void BeginCapture(HotkeyRowViewModel? row)
    {
        foreach (var h in Hotkeys)
        {
            h.IsCapturing = ReferenceEquals(h, row);
        }

        OnPropertyChanged(nameof(Capturing));
    }

    /// <summary>Clears a shortcut. An empty binding is stored, not deleted, so the
    /// default does not come back on the next launch.</summary>
    [RelayCommand]
    private void ClearHotkey(HotkeyRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        row.IsCapturing = false;
        row.Binding = new HotkeyBinding(row.Action, System.Windows.Input.ModifierKeys.None,
            System.Windows.Input.Key.None);
        ApplyHotkeys();
    }

    /// <summary>Called by the view once it has a complete combination.</summary>
    public void CompleteCapture(
        HotkeyRowViewModel row,
        System.Windows.Input.ModifierKeys modifiers,
        System.Windows.Input.Key key)
    {
        row.IsCapturing = false;
        row.Binding = new HotkeyBinding(row.Action, modifiers, key);
        OnPropertyChanged(nameof(Capturing));
        ApplyHotkeys();
    }

    private void ApplyHotkeys()
    {
        var bindings = Hotkeys.Select(h => h.Binding).ToList();

        HotkeyDefaults.Save(_settings.Current, bindings);
        _settings.Save();
        _hotkeys.Rebind(bindings);

        // Windows refuses a combination another app already owns, and says nothing
        // to the user - so the row marks itself instead.
        foreach (var row in Hotkeys)
        {
            row.HasConflict = _hotkeys.Conflicts.Contains(row.Action);
        }
    }

    public IReadOnlyList<ReminderScheduleType> ScheduleTypes { get; } =
        Enum.GetValues<ReminderScheduleType>();

    public IReadOnlyList<DayOfWeek> DaysOfWeek { get; } = Enum.GetValues<DayOfWeek>();

    public bool IsHourly => NewScheduleType == ReminderScheduleType.Hourly;

    public bool IsWeekly => NewScheduleType == ReminderScheduleType.Weekly;

    public bool NeedsTimeOfDay => NewScheduleType != ReminderScheduleType.Hourly;

    partial void OnNewScheduleTypeChanged(ReminderScheduleType value)
    {
        OnPropertyChanged(nameof(IsHourly));
        OnPropertyChanged(nameof(IsWeekly));
        OnPropertyChanged(nameof(NeedsTimeOfDay));
    }

    /// <summary>
    /// Applying on selection is the live preview - the whole window recolours as the
    /// user moves through the list, so there is no separate preview to build.
    /// </summary>
    partial void OnSelectedThemeChanged(ThemeOption? value)
    {
        if (value is null || value.Id == _themes.Current.Id)
        {
            return;
        }

        _themes.Apply(value.Id);
        _settings.Current.ThemeId = value.Id;
        _settings.Save();
    }

    public override Task OnNavigatedToAsync() => LoadRemindersAsync();

    public async Task LoadRemindersAsync()
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var reminders = await db.Reminders
                .OrderByDescending(r => r.IsActive)
                .ThenBy(r => r.Id)
                .AsNoTracking()
                .ToListAsync();

            Reminders.Clear();
            foreach (var r in reminders)
            {
                Reminders.Add(new ReminderRowViewModel(r));
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not load reminders");
        }
    }

    [RelayCommand]
    private async Task AddReminderAsync()
    {
        if (string.IsNullOrWhiteSpace(NewMessage))
        {
            ReminderError = "Write what the reminder should say.";
            return;
        }

        TimeSpan timeOfDay = default;
        if (NeedsTimeOfDay && !TimeSpan.TryParse(NewTimeOfDay, out timeOfDay))
        {
            ReminderError = "Time must look like 09:00.";
            return;
        }

        if (IsHourly && NewIntervalHours <= 0)
        {
            ReminderError = "Interval must be at least one hour.";
            return;
        }

        ReminderError = null;

        var reminder = new Reminder
        {
            Message = NewMessage.Trim(),
            ScheduleType = NewScheduleType,
            IntervalHours = NewIntervalHours,
            TimeOfDay = timeOfDay,
            DayOfWeek = IsWeekly ? NewDayOfWeek : null,
            IsActive = true
        };

        // Schedule it now so the row can show "next Tue 09:00" straight away rather
        // than waiting for the background service to pick it up.
        reminder.NextFireUtc = ReminderScheduler.NextFireUtc(reminder, DateTime.UtcNow);

        await using var db = await _dbFactory.CreateDbContextAsync();
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        NewMessage = string.Empty;
        await LoadRemindersAsync();
    }

    [RelayCommand]
    private async Task ToggleReminderAsync(ReminderRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();

        var reminder = await db.Reminders.FirstOrDefaultAsync(r => r.Id == row.Id);
        if (reminder is null)
        {
            return;
        }

        reminder.IsActive = !reminder.IsActive;
        reminder.NextFireUtc = reminder.IsActive
            ? ReminderScheduler.NextFireUtc(reminder, DateTime.UtcNow)
            : null;

        await db.SaveChangesAsync();
        await LoadRemindersAsync();
    }

    [RelayCommand]
    private async Task DeleteReminderAsync(ReminderRowViewModel? row)
    {
        if (row is null || !_dialogs.Confirm($"Delete this reminder?\n\n{row.Message}", "Delete reminder"))
        {
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();

        var reminder = await db.Reminders.FirstOrDefaultAsync(r => r.Id == row.Id);
        if (reminder is not null)
        {
            db.Reminders.Remove(reminder);
            await db.SaveChangesAsync();
        }

        await LoadRemindersAsync();
    }
}
