using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Serilog;
using WorkStationX.Data;
using WorkStationX.Services;

namespace WorkStationX.ViewModels;

/// <summary>One square in the grid.</summary>
public class DayCellViewModel
{
    public DayCellViewModel(DayCell cell)
    {
        Cell = cell;
        Tooltip = HistoryCalculator.Describe(cell);
    }

    public DayCell Cell { get; }

    public int Level => Cell.Level;

    public bool IsFuture => Cell.IsFuture;

    public string Tooltip { get; }
}

public class WeekColumnViewModel
{
    public WeekColumnViewModel(IReadOnlyList<DayCell> week)
    {
        Days = week.Select(d => new DayCellViewModel(d)).ToList();
    }

    public IReadOnlyList<DayCellViewModel> Days { get; }
}

public partial class HistoryViewModel : PageViewModelBase
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    [ObservableProperty]
    private string _headline = "No tasks completed yet";

    [ObservableProperty]
    private string _totalWorkedText = "0h 00m";

    [ObservableProperty]
    private string _streakText = "0 days";

    [ObservableProperty]
    private string _bestDayText = "—";

    public HistoryViewModel(IDbContextFactory<AppDbContext> dbFactory) => _dbFactory = dbFactory;

    public override string Title => "History";

    public override string Glyph => string.Empty;

    public ObservableCollection<WeekColumnViewModel> Weeks { get; } = new();

    public ObservableCollection<string> MonthLabels { get; } = new();

    public override Task OnNavigatedToAsync() => LoadAsync();

    public async Task LoadAsync()
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var completed = await db.Tasks
                .Where(t => t.Status == Models.TaskStatus.Done)
                .AsNoTracking()
                .ToListAsync();

            var sessions = await db.TaskSessions.AsNoTracking().ToListAsync();
            var entries = await db.TimeBankEntries.AsNoTracking().ToListAsync();

            var grid = HistoryCalculator.BuildGrid(completed, sessions, entries, DateTime.Now);

            Weeks.Clear();
            foreach (var week in grid)
            {
                Weeks.Add(new WeekColumnViewModel(week));
            }

            MonthLabels.Clear();
            foreach (var (_, label) in HistoryCalculator.MonthLabels(grid))
            {
                MonthLabels.Add(label);
            }

            var totalWorked = sessions.Where(s => s.EndedUtc is not null).Sum(s => s.DurationSeconds);
            TotalWorkedText = $"{totalWorked / 3600}h {totalWorked % 3600 / 60:D2}m";

            Headline = completed.Count switch
            {
                0 => "No tasks completed yet",
                1 => "1 task completed in the last year",
                _ => $"{completed.Count} tasks completed in the last year"
            };

            var byDay = grid.SelectMany(w => w).Where(d => !d.IsFuture).ToList();

            var best = byDay.OrderByDescending(d => d.TasksCompleted).FirstOrDefault();
            BestDayText = best is { TasksCompleted: > 0 }
                ? $"{best.TasksCompleted} on {best.Day:d MMM}"
                : "—";

            StreakText = $"{CurrentStreak(byDay)} days";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not load history");
        }
    }

    /// <summary>
    /// Consecutive days ending today with at least one task finished.
    /// Today not yet counting does not break the streak - it has not finished.
    /// </summary>
    private static int CurrentStreak(IReadOnlyList<DayCell> days)
    {
        var ordered = days.OrderByDescending(d => d.Day).ToList();
        var streak = 0;

        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].TasksCompleted > 0)
            {
                streak++;
            }
            else if (i > 0)
            {
                break;
            }
        }

        return streak;
    }
}
