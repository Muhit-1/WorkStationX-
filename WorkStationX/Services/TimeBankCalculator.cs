using WorkStationX.Models;

namespace WorkStationX.Services;

/// <summary>
/// Pure Time Bank arithmetic, deliberately free of EF and UI so it can be unit tested.
/// The bank is a signed ledger: finishing early credits, overrunning debits.
/// </summary>
public static class TimeBankCalculator
{
    /// <summary>Net balance in seconds. Negative means the user is over-committed.</summary>
    public static int Balance(IEnumerable<TimeBankEntry> entries) =>
        entries.Sum(e => e.DeltaSeconds);

    public static int BalanceBetween(
        IEnumerable<TimeBankEntry> entries, DateTime fromUtc, DateTime toUtc) =>
        entries.Where(e => e.CreatedUtc >= fromUtc && e.CreatedUtc < toUtc)
               .Sum(e => e.DeltaSeconds);

    /// <summary>
    /// Ledger entry produced when a task is marked done. Positive when the user beat
    /// their estimate, negative when they ran over.
    /// </summary>
    public static TimeBankEntry EntryForCompletedTask(TaskItem task)
    {
        var estimatedSeconds = task.EstimatedMinutes * 60;
        var delta = estimatedSeconds - task.ActualSecondsSpent;

        return new TimeBankEntry
        {
            TaskItemId = task.Id,
            DeltaSeconds = delta,
            Reason = delta >= 0 ? TimeBankReason.FinishedEarly : TimeBankReason.Overran,
            CreatedUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Ratio of estimated to actual across completed tasks. 1.0 is perfect,
    /// below 1.0 means the user is consistently optimistic. Null when there is no data.
    /// </summary>
    public static double? EstimationAccuracy(IEnumerable<TaskItem> completedTasks)
    {
        var tasks = completedTasks.Where(t => t.ActualSecondsSpent > 0).ToList();
        if (tasks.Count == 0)
        {
            return null;
        }

        var estimated = tasks.Sum(t => (long)t.EstimatedMinutes * 60);
        var actual = tasks.Sum(t => (long)t.ActualSecondsSpent);
        return actual == 0 ? null : (double)estimated / actual;
    }
}
