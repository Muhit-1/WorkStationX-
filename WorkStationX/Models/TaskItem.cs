namespace WorkStationX.Models;

public enum TaskStatus
{
    Pending = 0,
    Active = 1,
    Done = 2
}

/// <summary>
/// A unit of work with an estimated duration. Named TaskItem rather than Task to
/// avoid colliding with System.Threading.Tasks.Task in every async file.
/// </summary>
public class TaskItem
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Notes { get; set; }

    /// <summary>The user's original estimate. Never mutated by extensions —
    /// extensions are recorded as sessions so estimation accuracy stays measurable.</summary>
    public int EstimatedMinutes { get; set; }

    /// <summary>Live countdown remainder, persisted so a restart can restore it.</summary>
    public int RemainingSeconds { get; set; }

    /// <summary>Total wall-clock time actually spent, summed from completed sessions.</summary>
    public int ActualSecondsSpent { get; set; }

    public TaskStatus Status { get; set; } = TaskStatus.Pending;

    public int SortOrder { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAtUtc { get; set; }

    public List<TaskSession> Sessions { get; set; } = new();

    public List<TimeBankEntry> TimeBankEntries { get; set; } = new();
}
