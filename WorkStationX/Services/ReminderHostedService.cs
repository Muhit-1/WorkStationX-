using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Serilog;
using WorkStationX.Data;
using WorkStationX.Models;

namespace WorkStationX.Services;

/// <summary>
/// Checks once a minute whether any reminder is due.
///
/// A minute is deliberate: the finest schedule the UI offers is a time of day, so
/// polling faster would burn wakeups for no extra precision. Each fire writes
/// LastFiredUtc and the next slot back to the database before showing anything, so
/// a crash mid-notification cannot cause a double fire on restart.
/// </summary>
public class ReminderHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly INotificationService _notifications;

    public ReminderHostedService(
        IDbContextFactory<AppDbContext> dbFactory, INotificationService notifications)
    {
        _dbFactory = dbFactory;
        _notifications = notifications;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Fill in any reminder that has never been scheduled before starting the loop.
        await SeedSchedulesAsync(stoppingToken);

        using var timer = new PeriodicTimer(Interval);

        try
        {
            do
            {
                await CheckAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    private async Task SeedSchedulesAsync(CancellationToken ct)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var unscheduled = await db.Reminders
                .Where(r => r.IsActive && r.NextFireUtc == null)
                .ToListAsync(ct);

            foreach (var reminder in unscheduled)
            {
                reminder.NextFireUtc = ReminderScheduler.NextFireUtc(reminder, DateTime.UtcNow);
            }

            if (unscheduled.Count > 0)
            {
                await db.SaveChangesAsync(ct);
            }

            var active = await db.Reminders.CountAsync(r => r.IsActive, ct);
            Log.Information(
                "Reminder service started; {Active} active reminder(s), checking every {Seconds}s",
                active, Interval.TotalSeconds);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not seed reminder schedules");
        }
    }

    private async Task CheckAsync(CancellationToken ct)
    {
        try
        {
            var nowUtc = DateTime.UtcNow;

            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var due = await db.Reminders
                .Where(r => r.IsActive && r.NextFireUtc != null && r.NextFireUtc <= nowUtc)
                .ToListAsync(ct);

            if (due.Count == 0)
            {
                return;
            }

            foreach (var reminder in due)
            {
                reminder.LastFiredUtc = nowUtc;
                reminder.SnoozeUntilUtc = null;
                reminder.NextFireUtc = ReminderScheduler.NextFireUtc(reminder, nowUtc);
            }

            // Persist BEFORE notifying: if the app dies while the window is up, the
            // reminder must not fire again for the same slot on restart.
            await db.SaveChangesAsync(ct);

            foreach (var reminder in due)
            {
                Log.Information("Reminder fired: {Message}", reminder.Message);
                _notifications.Show(ReminderScheduler.Describe(reminder), reminder.Message);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Reminder check failed");
        }
    }
}
