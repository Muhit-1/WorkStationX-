using WorkStationX.Models;
using WorkStationX.Services;

namespace WorkStationX.Tests;

/// <summary>
/// A reminder that never fires, or fires twice, is invisible until it annoys someone.
/// You cannot test it by waiting an hour, so the clock is injected instead.
/// </summary>
public class ReminderSchedulerTests
{
    private static DateTime LocalUtc(int hour, int minute = 0, int day = 21) =>
        DateTime.SpecifyKind(new DateTime(2026, 8, day, hour, minute, 0), DateTimeKind.Local)
            .ToUniversalTime();

    [Fact]
    public void InactiveReminderIsNeverScheduled()
    {
        var reminder = new Reminder { IsActive = false, ScheduleType = ReminderScheduleType.Hourly };

        Assert.Null(ReminderScheduler.NextFireUtc(reminder, DateTime.UtcNow));
    }

    [Fact]
    public void HourlyCountsFromTheLastFireNotFromNow()
    {
        // This is what stops a restart re-firing immediately. Last fired 20 minutes
        // ago on a 1 hour schedule: 40 minutes still to wait, not a fresh hour.
        var now = LocalUtc(10, 20);
        var reminder = new Reminder
        {
            IsActive = true,
            ScheduleType = ReminderScheduleType.Hourly,
            IntervalHours = 1,
            LastFiredUtc = LocalUtc(10, 0)
        };

        var next = ReminderScheduler.NextFireUtc(reminder, now);

        Assert.Equal(LocalUtc(11, 0), next);
    }

    [Fact]
    public void HourlyCatchesUpAfterALongGapWithoutFiringForEverySlotMissed()
    {
        // App closed for 10 hours on a 3 hour schedule. The next fire must be in the
        // future, not eight backdated ones all at once.
        var now = LocalUtc(20, 0);
        var reminder = new Reminder
        {
            IsActive = true,
            ScheduleType = ReminderScheduleType.Hourly,
            IntervalHours = 3,
            LastFiredUtc = LocalUtc(10, 0)
        };

        var next = ReminderScheduler.NextFireUtc(reminder, now);

        Assert.True(next > now);
        Assert.Equal(LocalUtc(22, 0), next);
    }

    [Fact]
    public void HourlyWithNoHistoryStartsOneIntervalFromNow()
    {
        var now = LocalUtc(14, 0);
        var reminder = new Reminder
        {
            IsActive = true,
            ScheduleType = ReminderScheduleType.Hourly,
            IntervalHours = 2
        };

        Assert.Equal(LocalUtc(16, 0), ReminderScheduler.NextFireUtc(reminder, now));
    }

    [Fact]
    public void DailyBeforeTheTimeFiresToday()
    {
        var reminder = new Reminder
        {
            IsActive = true,
            ScheduleType = ReminderScheduleType.Daily,
            TimeOfDay = new TimeSpan(17, 0, 0)
        };

        Assert.Equal(LocalUtc(17, 0), ReminderScheduler.NextFireUtc(reminder, LocalUtc(9, 0)));
    }

    [Fact]
    public void DailyAfterTheTimeRollsToTomorrow()
    {
        var reminder = new Reminder
        {
            IsActive = true,
            ScheduleType = ReminderScheduleType.Daily,
            TimeOfDay = new TimeSpan(9, 0, 0)
        };

        var next = ReminderScheduler.NextFireUtc(reminder, LocalUtc(17, 0));

        Assert.Equal(LocalUtc(9, 0, day: 22), next);
    }

    [Fact]
    public void WeeklyFindsTheNextMatchingDay()
    {
        // 21 Aug 2026 is a Friday; the next Monday is the 24th.
        var reminder = new Reminder
        {
            IsActive = true,
            ScheduleType = ReminderScheduleType.Weekly,
            DayOfWeek = DayOfWeek.Monday,
            TimeOfDay = new TimeSpan(9, 0, 0)
        };

        var next = ReminderScheduler.NextFireUtc(reminder, LocalUtc(12, 0));

        Assert.Equal(DayOfWeek.Monday, next!.Value.ToLocalTime().DayOfWeek);
        Assert.Equal(24, next.Value.ToLocalTime().Day);
    }

    [Fact]
    public void WeeklyOnTodayButAlreadyPastGoesToNextWeek()
    {
        // Friday 17:00, asking for Fridays at 09:00: seven days away, not today.
        var reminder = new Reminder
        {
            IsActive = true,
            ScheduleType = ReminderScheduleType.Weekly,
            DayOfWeek = DayOfWeek.Friday,
            TimeOfDay = new TimeSpan(9, 0, 0)
        };

        var next = ReminderScheduler.NextFireUtc(reminder, LocalUtc(17, 0));

        Assert.Equal(28, next!.Value.ToLocalTime().Day);
    }

    [Fact]
    public void SnoozeOverridesTheSchedule()
    {
        var now = LocalUtc(10, 0);
        var reminder = new Reminder
        {
            IsActive = true,
            ScheduleType = ReminderScheduleType.Daily,
            TimeOfDay = new TimeSpan(17, 0, 0),
            SnoozeUntilUtc = now.AddMinutes(10)
        };

        Assert.Equal(now.AddMinutes(10), ReminderScheduler.NextFireUtc(reminder, now));
    }

    [Fact]
    public void ExpiredSnoozeIsIgnored()
    {
        var now = LocalUtc(10, 0);
        var reminder = new Reminder
        {
            IsActive = true,
            ScheduleType = ReminderScheduleType.Daily,
            TimeOfDay = new TimeSpan(17, 0, 0),
            SnoozeUntilUtc = now.AddMinutes(-5)
        };

        Assert.Equal(LocalUtc(17, 0), ReminderScheduler.NextFireUtc(reminder, now));
    }

    [Fact]
    public void IsDueOnlyOnceTheScheduledMomentHasArrived()
    {
        var now = LocalUtc(10, 0);
        var reminder = new Reminder { IsActive = true, NextFireUtc = now.AddMinutes(1) };

        Assert.False(ReminderScheduler.IsDue(reminder, now));
        Assert.True(ReminderScheduler.IsDue(reminder, now.AddMinutes(1)));
    }

    [Theory]
    [InlineData(ReminderScheduleType.Hourly, 1, "Every hour")]
    [InlineData(ReminderScheduleType.Hourly, 3, "Every 3 hours")]
    public void DescribesHourlySchedulesReadably(
        ReminderScheduleType type, int hours, string expected)
    {
        var reminder = new Reminder { ScheduleType = type, IntervalHours = hours };

        Assert.Equal(expected, ReminderScheduler.Describe(reminder));
    }

    [Fact]
    public void DescribesDailyAndWeeklyReadably()
    {
        var daily = new Reminder
        {
            ScheduleType = ReminderScheduleType.Daily,
            TimeOfDay = new TimeSpan(9, 30, 0)
        };
        var weekly = new Reminder
        {
            ScheduleType = ReminderScheduleType.Weekly,
            DayOfWeek = DayOfWeek.Tuesday,
            TimeOfDay = new TimeSpan(14, 0, 0)
        };

        Assert.Equal("Daily at 09:30", ReminderScheduler.Describe(daily));
        Assert.Equal("Tuesdays at 14:00", ReminderScheduler.Describe(weekly));
    }
}
