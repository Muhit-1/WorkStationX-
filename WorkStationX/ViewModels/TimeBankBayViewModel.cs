using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Serilog;
using WorkStationX.Data;
using WorkStationX.Models;
using WorkStationX.Services;

namespace WorkStationX.ViewModels;

/// <summary>One line in the ledger.</summary>
public class LedgerRowViewModel
{
    public LedgerRowViewModel(TimeBankEntry entry, string title)
    {
        Title = title;
        Delta = entry.DeltaSeconds;
        When = entry.CreatedUtc.ToLocalTime();
        IsCredit = entry.DeltaSeconds >= 0;
    }

    public string Title { get; }

    public int Delta { get; }

    public DateTime When { get; }

    public bool IsCredit { get; }

    public string DeltaText => TimerCalculator.FormatDelta(Delta);

    public string WhenText => When.ToString("ddd HH:mm");
}

/// <summary>Bay 3: the centre-zero meter and the ledger under it.</summary>
public partial class TimeBankBayViewModel : ObservableObject
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    [ObservableProperty]
    private int _weekBalanceSeconds;

    [ObservableProperty]
    private int _allTimeBalanceSeconds;

    [ObservableProperty]
    private string _estimationErrorText = "—";

    [ObservableProperty]
    private string _adjustMinutes = "15";

    public TimeBankBayViewModel(IDbContextFactory<AppDbContext> dbFactory) => _dbFactory = dbFactory;

    /// <summary>
    /// Posts time to the bank with no task attached.
    ///
    /// The ledger is meant to reflect reality, and some of reality happens away from
    /// the timer: work done before the app was open, or banked time deliberately spent
    /// on a longer break. Without this the balance slowly stops being true, and a
    /// number you do not trust is a number you stop reading.
    /// </summary>
    [RelayCommand]
    private async Task AdjustAsync(string? sign)
    {
        if (!int.TryParse(AdjustMinutes, out var minutes) || minutes <= 0)
        {
            return;
        }

        var credit = sign != "-";
        var delta = credit ? minutes * 60 : -minutes * 60;

        await using (var db = await _dbFactory.CreateDbContextAsync())
        {
            db.TimeBankEntries.Add(new TimeBankEntry
            {
                TaskItemId = null,
                DeltaSeconds = delta,
                Reason = credit ? TimeBankReason.Adjustment : TimeBankReason.Withdrawal,
                CreatedUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        Log.Information("Manual bank {Kind}: {Minutes}m", credit ? "credit" : "withdrawal", minutes);
        await LoadAsync();
    }

    public ObservableCollection<LedgerRowViewModel> Entries { get; } = new();

    public string WeekBalanceText => TimerCalculator.FormatDelta(WeekBalanceSeconds);

    public string AllTimeBalanceText => TimerCalculator.FormatDelta(AllTimeBalanceSeconds);

    public bool IsWeekCredit => WeekBalanceSeconds >= 0;

    public string WeekLabel => $"wk {System.Globalization.ISOWeek.GetWeekOfYear(DateTime.Now)}";

    public bool IsEmpty => Entries.Count == 0;

    /// <summary>
    /// Needle angle in degrees, -90 (two hours in debt) to +90 (two hours ahead).
    /// Clamped, so a wild week pins the needle instead of spinning it off the dial.
    /// </summary>
    public double NeedleAngle
    {
        get
        {
            const double fullScaleSeconds = 2 * 3600;
            var ratio = Math.Clamp(WeekBalanceSeconds / fullScaleSeconds, -1, 1);
            return ratio * 90;
        }
    }

    public async Task LoadAsync()
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var entries = await db.TimeBankEntries
                .Include(e => e.TaskItem)
                .OrderByDescending(e => e.CreatedUtc)
                .Take(50)
                .AsNoTracking()
                .ToListAsync();

            Entries.Clear();
            foreach (var e in entries)
            {
                Entries.Add(new LedgerRowViewModel(e, e.TaskItem?.Title ?? "Adjustment"));
            }

            var all = await db.TimeBankEntries.AsNoTracking().ToListAsync();
            AllTimeBalanceSeconds = TimeBankCalculator.Balance(all);

            // Week runs Monday to Monday, matching how people talk about a work week.
            var today = DateTime.Now.Date;
            var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            WeekBalanceSeconds = TimeBankCalculator.BalanceBetween(
                all, monday.ToUniversalTime(), monday.AddDays(7).ToUniversalTime());

            var completed = await db.Tasks
                .Where(t => t.Status == Models.TaskStatus.Done)
                .AsNoTracking()
                .ToListAsync();

            var accuracy = TimeBankCalculator.EstimationAccuracy(completed);
            EstimationErrorText = accuracy is null
                ? "—"
                : $"{(accuracy.Value - 1) * 100:+0;-0;0}%";

            RefreshDerived();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not load the Time Bank");
        }
    }

    private void RefreshDerived()
    {
        OnPropertyChanged(nameof(WeekBalanceText));
        OnPropertyChanged(nameof(AllTimeBalanceText));
        OnPropertyChanged(nameof(IsWeekCredit));
        OnPropertyChanged(nameof(NeedleAngle));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(WeekLabel));
    }
}
