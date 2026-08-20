namespace WorkStationX.Models;

public enum ReminderScheduleType
{
    Hourly = 0,
    Daily = 1,
    Weekly = 2
}

public class Reminder
{
    public int Id { get; set; }

    public string Message { get; set; } = string.Empty;

    public ReminderScheduleType ScheduleType { get; set; }

    /// <summary>Used when ScheduleType is Hourly.</summary>
    public int IntervalHours { get; set; } = 1;

    /// <summary>Used when ScheduleType is Weekly.</summary>
    public DayOfWeek? DayOfWeek { get; set; }

    /// <summary>Local time-of-day for Daily and Weekly schedules.</summary>
    public TimeSpan TimeOfDay { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Without these two, "every 3 hours" re-fires on every app restart or drifts.</summary>
    public DateTime? LastFiredUtc { get; set; }

    public DateTime? NextFireUtc { get; set; }

    public DateTime? SnoozeUntilUtc { get; set; }
}
