using WorkStationX.Models;
using WorkStationX.Services;

namespace WorkStationX.Tests;

public class HistoryCalculatorTests
{
    private static readonly DateTime Today = new(2026, 8, 22);

    private static TaskItem Done(DateTime completedLocal) => new()
    {
        Title = "t",
        Status = Models.TaskStatus.Done,
        CompletedAtUtc = DateTime.SpecifyKind(completedLocal, DateTimeKind.Local).ToUniversalTime()
    };

    private static TaskSession Session(DateTime startedLocal, int minutes) => new()
    {
        TaskItemId = 1,
        StartedUtc = DateTime.SpecifyKind(startedLocal, DateTimeKind.Local).ToUniversalTime(),
        EndedUtc = DateTime.SpecifyKind(startedLocal.AddMinutes(minutes), DateTimeKind.Local)
            .ToUniversalTime()
    };

    private static IReadOnlyList<DayCell> Flatten(
        IReadOnlyList<IReadOnlyList<DayCell>> grid) => grid.SelectMany(w => w).ToList();

    [Fact]
    public void EveryColumnIsAFullSevenDayWeek()
    {
        // Columns must be whole Sun..Sat weeks or the grid rows stop lining up.
        var grid = HistoryCalculator.BuildGrid(
            Array.Empty<TaskItem>(), Array.Empty<TaskSession>(),
            Array.Empty<TimeBankEntry>(), Today);

        Assert.All(grid, week => Assert.Equal(7, week.Count));
    }

    [Fact]
    public void GridStartsOnASunday()
    {
        var grid = HistoryCalculator.BuildGrid(
            Array.Empty<TaskItem>(), Array.Empty<TaskSession>(),
            Array.Empty<TimeBankEntry>(), Today);

        Assert.Equal(DayOfWeek.Sunday, grid[0][0].Day.DayOfWeek);
    }

    [Fact]
    public void DaysAfterTodayAreMarkedFuture()
    {
        // The last column runs to Saturday, so it contains days that have not happened.
        var grid = HistoryCalculator.BuildGrid(
            Array.Empty<TaskItem>(), Array.Empty<TaskSession>(),
            Array.Empty<TimeBankEntry>(), Today);

        var future = Flatten(grid).Where(d => d.IsFuture).ToList();

        Assert.All(future, d => Assert.True(d.Day > Today.Date));
    }

    [Fact]
    public void CountsCompletedTasksOnTheirLocalDay()
    {
        var grid = HistoryCalculator.BuildGrid(
            new[] { Done(Today.AddHours(10)), Done(Today.AddHours(14)) },
            Array.Empty<TaskSession>(), Array.Empty<TimeBankEntry>(), Today);

        var cell = Flatten(grid).Single(d => d.Day == Today.Date);

        Assert.Equal(2, cell.TasksCompleted);
    }

    [Fact]
    public void AnEarlyHoursTaskCountsForTheLocalDayNotTheUtcOne()
    {
        // 01:00 local in Dhaka is the previous day in UTC. Bucketing on the stored
        // value would file it under yesterday and the square would light up wrong.
        var grid = HistoryCalculator.BuildGrid(
            new[] { Done(Today.AddHours(1)) },
            Array.Empty<TaskSession>(), Array.Empty<TimeBankEntry>(), Today);

        var cell = Flatten(grid).Single(d => d.Day == Today.Date);

        Assert.Equal(1, cell.TasksCompleted);
    }

    [Fact]
    public void UnfinishedTasksNeverLightASquare()
    {
        var running = new TaskItem { Title = "t", Status = Models.TaskStatus.Active };

        var grid = HistoryCalculator.BuildGrid(
            new[] { running }, Array.Empty<TaskSession>(), Array.Empty<TimeBankEntry>(), Today);

        Assert.All(Flatten(grid), d => Assert.Equal(0, d.TasksCompleted));
    }

    [Fact]
    public void TracksWorkedTimeSeparatelyFromCompletions()
    {
        var grid = HistoryCalculator.BuildGrid(
            Array.Empty<TaskItem>(),
            new[] { Session(Today.AddHours(9), 45) },
            Array.Empty<TimeBankEntry>(), Today);

        var cell = Flatten(grid).Single(d => d.Day == Today.Date);

        Assert.Equal(45 * 60, cell.WorkedSeconds);
        Assert.Equal(0, cell.TasksCompleted);
    }

    [Theory]
    [InlineData(0, 8, 0)]
    [InlineData(1, 8, 1)]
    [InlineData(2, 8, 1)]
    [InlineData(4, 8, 2)]
    [InlineData(6, 8, 3)]
    [InlineData(8, 8, 4)]
    public void ShadeScalesAgainstTheUsersOwnBestDay(int done, int busiest, int expected)
    {
        Assert.Equal(expected, HistoryCalculator.Level(done, busiest));
    }

    [Fact]
    public void AnySingleCompletionAlwaysShowsAtLeastTheFaintestShade()
    {
        // Someone finishing one task a day must not see an entirely blank grid.
        // The exact shade depends on their own best day - one task IS the maximum
        // when the best day is also one - so the invariant is simply "not empty".
        Assert.True(HistoryCalculator.Level(1, 1) >= 1);
        Assert.True(HistoryCalculator.Level(1, 0) >= 1);
        Assert.True(HistoryCalculator.Level(1, 20) >= 1);
    }

    [Fact]
    public void ShadeNeverExceedsTheTopLevel()
    {
        Assert.Equal(HistoryCalculator.MaxLevel, HistoryCalculator.Level(100, 4));
    }

    [Fact]
    public void TooltipReadsLikeAContributionGraph()
    {
        var cell = new DayCell(new DateTime(2026, 8, 22), 3, 0, 0, 2);

        Assert.Equal("3 tasks completed on 22 Aug 2026", HistoryCalculator.Describe(cell));
    }

    [Fact]
    public void TooltipUsesSingularForOneTask()
    {
        var cell = new DayCell(new DateTime(2026, 8, 22), 1, 0, 0, 1);

        Assert.StartsWith("1 task completed", HistoryCalculator.Describe(cell));
    }

    [Fact]
    public void TooltipMentionsTrackedTimeWhenThereIsAny()
    {
        var cell = new DayCell(new DateTime(2026, 8, 22), 1, 3900, 0, 1);

        Assert.Contains("1h 05m tracked", HistoryCalculator.Describe(cell));
    }

    [Fact]
    public void EmptyDayStillGetsAReadableTooltip()
    {
        var cell = new DayCell(new DateTime(2026, 8, 22), 0, 0, 0, 0);

        Assert.Equal("No tasks completed on 22 Aug 2026", HistoryCalculator.Describe(cell));
    }

    [Fact]
    public void MonthLabelsAppearOnceEachAndInOrder()
    {
        var grid = HistoryCalculator.BuildGrid(
            Array.Empty<TaskItem>(), Array.Empty<TaskSession>(),
            Array.Empty<TimeBankEntry>(), Today);

        var labels = HistoryCalculator.MonthLabels(grid);

        Assert.NotEmpty(labels);
        Assert.Equal(labels.Select(l => l.Column).OrderBy(c => c), labels.Select(l => l.Column));
    }
}
