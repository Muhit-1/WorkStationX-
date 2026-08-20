namespace WorkStationX.Models;

/// <summary>
/// One contiguous stretch of time the timer ran for a task. This is what makes
/// "time tracking" real — without it there is no history to report on.
/// </summary>
public class TaskSession
{
    public int Id { get; set; }

    public int TaskItemId { get; set; }

    public TaskItem? TaskItem { get; set; }

    public DateTime StartedUtc { get; set; }

    /// <summary>Null while the session is still running.</summary>
    public DateTime? EndedUtc { get; set; }

    /// <summary>True if this session began after the original estimate ran out.</summary>
    public bool WasExtension { get; set; }

    public int DurationSeconds =>
        EndedUtc is null ? 0 : (int)(EndedUtc.Value - StartedUtc).TotalSeconds;
}
