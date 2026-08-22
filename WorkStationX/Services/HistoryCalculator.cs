using WorkStationX.Models;

namespace WorkStationX.Services;

/// <summary>One square in the contribution grid.</summary>
public sealed record DayCell(
    DateTime Day,
    int TasksCompleted,
    int WorkedSeconds,
    int BankDeltaSeconds,
    int Level)
{
    public bool IsFuture { get; init; }
}

/// <summary>
/// Rolls sessions and ledger entries into the daily grid.
/// Pure, so the bucketing rules can be tested without a database.
/// </summary>
public static class HistoryCalculator
{
    /// <summary>Number of shades in the grid, matching a contribution graph.</summary>
    public const int MaxLevel = 4;

    /// <summary>
    /// A year of days ending today, grouped into calendar weeks.
    ///
    /// Sessions are stored in UTC and converted to LOCAL first: a task finished at
    /// 01:00 in Dhaka is the previous day in UTC, and bucketing on the raw value
    /// would file it under yesterday.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<DayCell>> BuildGrid(
        IEnumerable<TaskItem> completedTasks,
        IEnumerable<TaskSession> sessions,
        IEnumerable<TimeBankEntry> entries,
        DateTime todayLocal,
        int weeks = 53)
    {
        var today = todayLocal.Date;

        // Start on the Sunday of the week containing (today - weeks). Every column is
        // then a full Sun..Sat week, which is what makes the grid line up.
        var start = today.AddDays(-((weeks - 1) * 7)) ;
        start = start.AddDays(-(int)start.DayOfWeek);

        var completedByDay = completedTasks
            .Where(t => t.CompletedAtUtc is not null)
            .GroupBy(t => t.CompletedAtUtc!.Value.ToLocalTime().Date)
            .ToDictionary(g => g.Key, g => g.Count());

        var workedByDay = sessions
            .Where(s => s.EndedUtc is not null)
            .GroupBy(s => s.StartedUtc.ToLocalTime().Date)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.DurationSeconds));

        var bankByDay = entries
            .GroupBy(e => e.CreatedUtc.ToLocalTime().Date)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.DeltaSeconds));

        var busiest = completedByDay.Count == 0 ? 0 : completedByDay.Values.Max();

        var grid = new List<IReadOnlyList<DayCell>>();

        for (var weekStart = start; weekStart <= today; weekStart = weekStart.AddDays(7))
        {
            var column = new List<DayCell>();

            for (var i = 0; i < 7; i++)
            {
                var day = weekStart.AddDays(i);

                completedByDay.TryGetValue(day, out var done);
                workedByDay.TryGetValue(day, out var worked);
                bankByDay.TryGetValue(day, out var delta);

                column.Add(new DayCell(day, done, worked, delta, Level(done, busiest))
                {
                    IsFuture = day > today
                });
            }

            grid.Add(column);
        }

        return grid;
    }

    /// <summary>
    /// Shade for a day, 0 (nothing) to 4 (busiest).
    ///
    /// Scaled against the user's own best day rather than a fixed count: someone
    /// finishing two tasks a day should still see a full range, not a grid that
    /// never gets past the palest shade.
    /// </summary>
    public static int Level(int completed, int busiest)
    {
        if (completed <= 0)
        {
            return 0;
        }

        if (busiest <= 0)
        {
            return 1;
        }

        var ratio = (double)completed / busiest;
        return Math.Clamp((int)Math.Ceiling(ratio * MaxLevel), 1, MaxLevel);
    }

    /// <summary>Tooltip text, in the style of a contribution graph.</summary>
    public static string Describe(DayCell cell)
    {
        var date = cell.Day.ToString("d MMM yyyy");

        var tasks = cell.TasksCompleted switch
        {
            0 => "No tasks completed",
            1 => "1 task completed",
            _ => $"{cell.TasksCompleted} tasks completed"
        };

        if (cell.WorkedSeconds <= 0)
        {
            return $"{tasks} on {date}";
        }

        var hours = cell.WorkedSeconds / 3600;
        var minutes = cell.WorkedSeconds % 3600 / 60;
        var worked = hours > 0 ? $"{hours}h {minutes:D2}m" : $"{minutes}m";

        return $"{tasks} on {date}  ·  {worked} tracked";
    }

    /// <summary>Month labels with the column each one starts at, for the grid header.</summary>
    public static IReadOnlyList<(int Column, string Label)> MonthLabels(
        IReadOnlyList<IReadOnlyList<DayCell>> grid)
    {
        var labels = new List<(int, string)>();
        var lastMonth = -1;

        for (var col = 0; col < grid.Count; col++)
        {
            var first = grid[col][0].Day;
            if (first.Month == lastMonth)
            {
                continue;
            }

            lastMonth = first.Month;
            labels.Add((col, first.ToString("MMM")));
        }

        return labels;
    }
}
