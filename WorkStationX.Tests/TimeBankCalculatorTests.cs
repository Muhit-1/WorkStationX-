using WorkStationX.Models;
using WorkStationX.Services;

namespace WorkStationX.Tests;

public class TimeBankCalculatorTests
{
    [Fact]
    public void Balance_IsZero_WhenLedgerIsEmpty()
    {
        Assert.Equal(0, TimeBankCalculator.Balance(Array.Empty<TimeBankEntry>()));
    }

    [Fact]
    public void Balance_NetsCreditsAgainstDebits()
    {
        var entries = new[]
        {
            new TimeBankEntry { DeltaSeconds = 600 },   // finished 10 min early
            new TimeBankEntry { DeltaSeconds = -900 },  // ran 15 min over
            new TimeBankEntry { DeltaSeconds = 300 }
        };

        // The whole point of a signed ledger: overruns actually cost you.
        Assert.Equal(0, TimeBankCalculator.Balance(entries));
    }

    [Fact]
    public void FinishingEarly_ProducesACredit()
    {
        var task = new TaskItem { Id = 1, EstimatedMinutes = 60, ActualSecondsSpent = 45 * 60 };

        var entry = TimeBankCalculator.EntryForCompletedTask(task);

        Assert.Equal(15 * 60, entry.DeltaSeconds);
        Assert.Equal(TimeBankReason.FinishedEarly, entry.Reason);
    }

    [Fact]
    public void Overrunning_ProducesADebit()
    {
        var task = new TaskItem { Id = 1, EstimatedMinutes = 30, ActualSecondsSpent = 50 * 60 };

        var entry = TimeBankCalculator.EntryForCompletedTask(task);

        Assert.Equal(-20 * 60, entry.DeltaSeconds);
        Assert.Equal(TimeBankReason.Overran, entry.Reason);
    }

    [Fact]
    public void BalanceBetween_ExcludesEntriesOutsideTheWindow()
    {
        var monday = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc);
        var entries = new[]
        {
            new TimeBankEntry { DeltaSeconds = 100, CreatedUtc = monday.AddDays(-1) },
            new TimeBankEntry { DeltaSeconds = 200, CreatedUtc = monday.AddHours(3) },
            new TimeBankEntry { DeltaSeconds = 400, CreatedUtc = monday.AddDays(9) }
        };

        var result = TimeBankCalculator.BalanceBetween(entries, monday, monday.AddDays(7));

        Assert.Equal(200, result);
    }

    [Fact]
    public void EstimationAccuracy_IsNull_WithoutData()
    {
        Assert.Null(TimeBankCalculator.EstimationAccuracy(Array.Empty<TaskItem>()));
    }

    [Fact]
    public void EstimationAccuracy_BelowOne_WhenUserIsOptimistic()
    {
        var tasks = new[]
        {
            new TaskItem { EstimatedMinutes = 30, ActualSecondsSpent = 60 * 60 },
            new TaskItem { EstimatedMinutes = 30, ActualSecondsSpent = 60 * 60 }
        };

        var accuracy = TimeBankCalculator.EstimationAccuracy(tasks);

        Assert.NotNull(accuracy);
        Assert.Equal(0.5, accuracy!.Value, precision: 3);
    }
}
