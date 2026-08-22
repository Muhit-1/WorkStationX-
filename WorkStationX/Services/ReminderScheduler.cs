using WorkStationX.Models;

namespace WorkStationX.Services;

/// <summary>
/// Works out when a reminder should next fire.
///
/// Pure and clock-injected on purpose: "every Tuesday at 09:00" crossing a DST
/// boundary, or an app restarted three times inside one hourly window, are exactly
/// the cases that produce a reminder that never fires or one that fires twice. They
/// cannot be tested by waiting an hour, so the maths lives here.
///
/// All arithmetic is done in LOCAL time because that is what the user means by
/// "9am", then converted back to UTC for storage.
/// </summary>
public static class ReminderScheduler
{
    /// <summary>
    /// Next fire time in UTC, or null when the reminder is inactive.
    /// <paramref name="nowUtc"/> is passed in so tests can place "now" anywhere.
    /// </summary>
    public static DateTime? NextFireUtc(Reminder reminder, DateTime nowUtc)
    {
        if (!reminder.IsActive)
        {
            return null;
        }

        // A snooze overrides the schedule until it expires.
        if (reminder.SnoozeUntilUtc is { } snooze && snooze > nowUtc)
        {
            return snooze;
        }

        var nowLocal = nowUtc.ToLocalTime();

        return reminder.ScheduleType switch
        {
            ReminderScheduleType.Hourly => NextHourly(reminder, nowUtc),
            ReminderScheduleType.Daily => ToUtc(NextDaily(reminder, nowLocal)),
            ReminderScheduleType.Weekly => ToUtc(NextWeekly(reminder, nowLocal)),
            _ => null
        };
    }

    private static DateTime NextHourly(Reminder reminder, DateTime nowUtc)
    {
        var interval = TimeSpan.FromHours(Math.Max(1, reminder.IntervalHours));

        // Counting from the last fire is what stops a restart re-firing immediately:
        // the schedule is anchored to history, not to when the app happened to start.
        var anchor = reminder.LastFiredUtc ?? nowUtc;

        var next = anchor + interval;
        while (next <= nowUtc)
        {
            next += interval;
        }

        return next;
    }

    private static DateTime NextDaily(Reminder reminder, DateTime nowLocal)
    {
        var candidate = nowLocal.Date + reminder.TimeOfDay;
        return candidate > nowLocal ? candidate : candidate.AddDays(1);
    }

    private static DateTime NextWeekly(Reminder reminder, DateTime nowLocal)
    {
        var target = reminder.DayOfWeek ?? DayOfWeek.Monday;

        var daysAhead = ((int)target - (int)nowLocal.DayOfWeek + 7) % 7;
        var candidate = nowLocal.Date.AddDays(daysAhead) + reminder.TimeOfDay;

        // Same day but the time has already passed: go round to next week.
        return candidate > nowLocal ? candidate : candidate.AddDays(7);
    }

    /// <summary>
    /// Local wall-clock to UTC. DateTime.SpecifyKind(Local) then ToUniversalTime
    /// applies the offset in force on THAT date, so a time set before a DST change
    /// still fires at the right wall-clock time afterwards.
    /// </summary>
    private static DateTime ToUtc(DateTime local) =>
        DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();

    /// <summary>True when this reminder is due and has not already fired for this slot.</summary>
    public static bool IsDue(Reminder reminder, DateTime nowUtc) =>
        reminder.IsActive &&
        reminder.NextFireUtc is { } next &&
        next <= nowUtc;

    /// <summary>Human summary for the list: "Every 3 hours", "Daily at 09:00".</summary>
    public static string Describe(Reminder reminder) => reminder.ScheduleType switch
    {
        ReminderScheduleType.Hourly =>
            reminder.IntervalHours == 1 ? "Every hour" : $"Every {reminder.IntervalHours} hours",
        ReminderScheduleType.Daily =>
            $"Daily at {reminder.TimeOfDay:hh\\:mm}",
        ReminderScheduleType.Weekly =>
            $"{reminder.DayOfWeek ?? DayOfWeek.Monday}s at {reminder.TimeOfDay:hh\\:mm}",
        _ => "—"
    };
}
