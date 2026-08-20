namespace WorkStationX.Models;

public enum TimeBankReason
{
    /// <summary>Task finished before the estimate ran out — credit.</summary>
    FinishedEarly = 0,

    /// <summary>Task was extended past the estimate — debit.</summary>
    Overran = 1,

    /// <summary>User deliberately spent banked time (e.g. a longer break) — debit.</summary>
    Withdrawal = 2,

    /// <summary>Manual correction.</summary>
    Adjustment = 3
}

/// <summary>
/// A signed ledger entry. Time Bank is a double-entry balance, not a running total:
/// finishing early credits, overrunning debits. A number that only ever increases
/// is a scoreboard; a signed ledger is a measure of estimation accuracy.
/// </summary>
public class TimeBankEntry
{
    public int Id { get; set; }

    public int? TaskItemId { get; set; }

    public TaskItem? TaskItem { get; set; }

    /// <summary>Positive credits the bank, negative debits it.</summary>
    public int DeltaSeconds { get; set; }

    public TimeBankReason Reason { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
