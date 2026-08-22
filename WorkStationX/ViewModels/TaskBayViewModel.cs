using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Serilog;
using WorkStationX.Data;
using WorkStationX.Models;
using WorkStationX.Services;

namespace WorkStationX.ViewModels;

/// <summary>Bay 2: the task list and the countdown that runs one of them.</summary>
public partial class TaskBayViewModel : ObservableObject
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IDialogService _dialogs;
    private readonly DispatcherTimer _ticker;

    /// <summary>UTC start of the stretch currently running. Null while paused.</summary>
    private DateTime? _runningSinceUtc;

    private int _activeSessionId;

    [ObservableProperty]
    private TaskRowViewModel? _active;

    [ObservableProperty]
    private string _newTitle = string.Empty;

    [ObservableProperty]
    private int _newEstimateMinutes = 30;

    [ObservableProperty]
    private string? _error;

    public TaskBayViewModel(IDbContextFactory<AppDbContext> dbFactory, IDialogService dialogs)
    {
        _dbFactory = dbFactory;
        _dialogs = dialogs;

        // 250ms so the seconds digit turns over promptly; the value shown is always
        // computed from the anchor, never accumulated from ticks.
        _ticker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _ticker.Tick += (_, _) => RefreshReadout();
    }

    /// <summary>Raised when the ledger changes so bay 3 can reload.</summary>
    public event EventHandler? BankChanged;

    public ObservableCollection<TaskRowViewModel> Tasks { get; } = new();

    public bool IsRunning => _runningSinceUtc is not null;

    public bool HasActive => Active is not null;

    public string HeaderStatus => Active is null ? "idle" : IsRunning ? "running" : "paused";

    // ---------- readout ----------
    public string ActiveTitle => Active?.Title ?? "No active task";

    public int RemainingSeconds => Active is null
        ? 0
        : TimerCalculator.RemainingSeconds(
            Active.Task.RemainingSeconds, _runningSinceUtc, DateTime.UtcNow);

    public string RemainingText => TimerCalculator.Format(RemainingSeconds);

    public bool IsOverrun => Active is not null && RemainingSeconds < 0;

    public string EstimateText =>
        Active is null ? "00:00:00" : TimerCalculator.Format(Active.Task.EstimatedMinutes * 60);

    public string RemainingCaption
    {
        get
        {
            if (Active is null)
            {
                return "nothing running";
            }

            var s = RemainingSeconds;
            var abs = Math.Abs(s);
            var text = $"{abs / 60}m {abs % 60:D2}s";
            return s < 0 ? $"{text} over" : $"{text} remaining";
        }
    }

    public double Progress => Active is null
        ? 0
        : TimerCalculator.Progress(Active.Task.EstimatedMinutes * 60, RemainingSeconds);

    // ---------- anchor plate ----------
    public string AnchorText => _runningSinceUtc?.ToString("yyyy-MM-ddTHH:mm:ss'Z'") ?? "—";

    [ObservableProperty]
    private string _accruedPauseText = "00:00:00";

    [ObservableProperty]
    private int _stopCount;

    public string StopText => StopCount == 1 ? "1 stop" : $"{StopCount} stops";

    public async Task LoadAsync()
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            // Anything still marked Active belongs to a previous run: the app was
            // closed or crashed mid-task. Bank the time that genuinely elapsed and
            // leave it paused rather than silently counting hours of sleep as work.
            await ReconcileOrphanedSessionsAsync(db);

            var tasks = await db.Tasks
                // Finished tasks leave the list the moment they are posted. The row
                // stays in the database with its CompletedAtUtc, which is what the
                // History grid counts - the list is a to-do, not an archive.
                .Where(t => t.Status != Models.TaskStatus.Done)
                .OrderBy(t => t.SortOrder)
                .ThenByDescending(t => t.CreatedUtc)
                .AsNoTracking()
                .ToListAsync();

            Tasks.Clear();
            foreach (var t in tasks)
            {
                Tasks.Add(new TaskRowViewModel(t));
            }

            Active = null;
            _runningSinceUtc = null;
            _activeSessionId = 0;
            _ticker.Stop();
            RefreshAll();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not load tasks");
            _dialogs.Inform("Could not load your tasks. See the log for details.");
        }
    }

    private static async Task ReconcileOrphanedSessionsAsync(AppDbContext db)
    {
        var open = await db.TaskSessions
            .Include(s => s.TaskItem)
            .Where(s => s.EndedUtc == null)
            .ToListAsync();

        if (open.Count == 0)
        {
            return;
        }

        foreach (var session in open)
        {
            var elapsed = TimerCalculator.ElapsedSeconds(session.StartedUtc, DateTime.UtcNow);

            if (session.TaskItem is { } task)
            {
                // Cap at what was left: a laptop closed overnight must not turn a
                // 30 minute task into an eight hour overrun.
                var counted = Math.Min(elapsed, Math.Max(0, task.RemainingSeconds));
                task.RemainingSeconds -= counted;
                task.ActualSecondsSpent += counted;
                task.Status = Models.TaskStatus.Pending;
                session.EndedUtc = session.StartedUtc.AddSeconds(counted);
            }
            else
            {
                session.EndedUtc = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync();
        Log.Information("Reconciled {Count} interrupted timer session(s)", open.Count);
    }

    // ---------- commands ----------

    [RelayCommand]
    private async Task AddTaskAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTitle))
        {
            Error = "Give the task a name.";
            return;
        }

        if (NewEstimateMinutes <= 0)
        {
            Error = "Estimate must be at least one minute.";
            return;
        }

        Error = null;

        await using var db = await _dbFactory.CreateDbContextAsync();
        db.Tasks.Add(new TaskItem
        {
            Title = NewTitle.Trim(),
            EstimatedMinutes = NewEstimateMinutes,
            RemainingSeconds = NewEstimateMinutes * 60,
            Status = Models.TaskStatus.Pending,
            SortOrder = Tasks.Count
        });
        await db.SaveChangesAsync();

        NewTitle = string.Empty;
        await LoadAsync();
    }

    /// <summary>
    /// Starts timing immediately with no set-up.
    ///
    /// Wanting to track time is usually the reason you reach for the app at all, and
    /// forcing a name and an estimate first is the friction that stops people bothering.
    /// This creates a task from whatever is typed (or an untitled one) and starts it in
    /// one press; the name and estimate can be corrected afterwards.
    /// </summary>
    [RelayCommand]
    private async Task QuickStartAsync()
    {
        Error = null;

        var title = string.IsNullOrWhiteSpace(NewTitle)
            ? $"Untitled — {DateTime.Now:HH:mm}"
            : NewTitle.Trim();

        var minutes = NewEstimateMinutes > 0 ? NewEstimateMinutes : 30;

        int newId;
        await using (var db = await _dbFactory.CreateDbContextAsync())
        {
            var task = new TaskItem
            {
                Title = title,
                EstimatedMinutes = minutes,
                RemainingSeconds = minutes * 60,
                Status = Models.TaskStatus.Pending,
                SortOrder = Tasks.Count
            };
            db.Tasks.Add(task);
            await db.SaveChangesAsync();
            newId = task.Id;
        }

        NewTitle = string.Empty;
        await LoadAsync();

        var row = Tasks.FirstOrDefault(t => t.Id == newId);
        if (row is not null)
        {
            await StartAsync(row);
        }
    }

    /// <summary>Only one task runs at a time, so starting one stops whatever was running.</summary>
    [RelayCommand]
    private async Task StartAsync(TaskRowViewModel? row)
    {
        if (row is null || row.IsDone)
        {
            return;
        }

        if (Active is not null && Active.Id != row.Id)
        {
            await PauseAsync();
        }

        if (Active?.Id == row.Id && IsRunning)
        {
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();

        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == row.Id);
        if (task is null)
        {
            return;
        }

        var startedUtc = DateTime.UtcNow;

        // The session row IS the anchor: it is what survives a restart.
        var session = new TaskSession
        {
            TaskItemId = task.Id,
            StartedUtc = startedUtc,
            WasExtension = task.RemainingSeconds <= 0
        };
        db.TaskSessions.Add(session);

        task.Status = Models.TaskStatus.Active;
        await db.SaveChangesAsync();

        foreach (var r in Tasks)
        {
            r.IsActive = r.Id == row.Id;
        }

        row.Task.Status = Models.TaskStatus.Active;
        row.Task.RemainingSeconds = task.RemainingSeconds;

        Active = row;
        _runningSinceUtc = startedUtc;
        _activeSessionId = session.Id;
        _ticker.Start();

        await RefreshPausePlateAsync();
        RefreshAll();
    }

    [RelayCommand]
    private async Task PauseAsync()
    {
        if (Active is null || _runningSinceUtc is null)
        {
            return;
        }

        var endedUtc = DateTime.UtcNow;
        var elapsed = TimerCalculator.ElapsedSeconds(_runningSinceUtc.Value, endedUtc);

        await using var db = await _dbFactory.CreateDbContextAsync();

        var session = await db.TaskSessions.FirstOrDefaultAsync(s => s.Id == _activeSessionId);
        if (session is not null)
        {
            session.EndedUtc = endedUtc;
        }

        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == Active.Id);
        if (task is not null)
        {
            task.RemainingSeconds -= elapsed;
            task.ActualSecondsSpent += elapsed;
            task.Status = Models.TaskStatus.Pending;

            Active.Task.RemainingSeconds = task.RemainingSeconds;
            Active.Task.ActualSecondsSpent = task.ActualSecondsSpent;
            Active.Task.Status = Models.TaskStatus.Pending;
        }

        await db.SaveChangesAsync();

        _runningSinceUtc = null;
        _activeSessionId = 0;
        _ticker.Stop();

        await RefreshPausePlateAsync();
        RefreshAll();
    }

    [RelayCommand]
    private Task ResumeAsync() => Active is null ? Task.CompletedTask : StartAsync(Active);

    /// <summary>
    /// The transport button. Running means pause; paused means resume.
    ///
    /// Without this a paused task read as stuck: PAUSE greyed out and the only way
    /// back was the START button down in the list, which is not where anyone looks.
    /// </summary>
    [RelayCommand]
    private Task ResumeOrPauseAsync() => IsRunning ? PauseAsync() : ResumeAsync();

    /// <summary>Adds time when a task is running long. The overrun is still recorded.</summary>
    [RelayCommand]
    private async Task ExtendAsync(string? minutesText)
    {
        if (Active is null || !int.TryParse(minutesText, out var minutes) || minutes <= 0)
        {
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();

        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == Active.Id);
        if (task is null)
        {
            return;
        }

        task.RemainingSeconds += minutes * 60;
        await db.SaveChangesAsync();

        Active.Task.RemainingSeconds = task.RemainingSeconds;
        RefreshAll();
    }

    /// <summary>Completes whatever is running. Wired to POST TO BANK.</summary>
    [RelayCommand]
    private Task CompleteAsync() => CompleteTaskAsync(Active);

    /// <summary>
    /// Marks a task done and posts the ledger entry. Finishing under the estimate
    /// credits the bank; running over debits it.
    ///
    /// Takes a row rather than assuming the active task, so a task can be ticked off
    /// straight from the list without having to start it first.
    /// </summary>
    [RelayCommand]
    private async Task CompleteTaskAsync(TaskRowViewModel? row)
    {
        if (row is null || row.IsDone)
        {
            return;
        }

        // Pause whatever is running first, even if it is a different task: leaving a
        // session open would orphan it and inflate that task on the next launch.
        if (IsRunning)
        {
            await PauseAsync();
        }

        await using var db = await _dbFactory.CreateDbContextAsync();

        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == row.Id);
        if (task is null)
        {
            return;
        }

        task.Status = Models.TaskStatus.Done;
        task.CompletedAtUtc = DateTime.UtcNow;

        db.TimeBankEntries.Add(TimeBankCalculator.EntryForCompletedTask(task));
        await db.SaveChangesAsync();

        Log.Information(
            "Completed {Title}: estimated {Est}m, actual {Act}s",
            task.Title, task.EstimatedMinutes, task.ActualSecondsSpent);

        Active = null;
        _runningSinceUtc = null;
        _ticker.Stop();

        await LoadAsync();
        BankChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>True when the list holds anything already finished.</summary>
    public bool HasCompleted => Tasks.Any(t => t.IsDone);

    public int CompletedCount => Tasks.Count(t => t.IsDone);

    /// <summary>
    /// Removes finished tasks from the list only. Their sessions and ledger entries
    /// stay, so History and the Time Bank are unaffected - this tidies the view, it
    /// does not erase the record.
    /// </summary>
    [RelayCommand]
    private async Task ClearDoneAsync()
    {
        if (!HasCompleted)
        {
            return;
        }

        if (!_dialogs.Confirm(
                $"Remove {CompletedCount} finished task(s) from the list?\n\nYour history and Time Bank entries are kept.",
                "Clear finished"))
        {
            return;
        }

        await using (var db = await _dbFactory.CreateDbContextAsync())
        {
            var done = await db.Tasks
                .Where(t => t.Status == Models.TaskStatus.Done)
                .ToListAsync();

            db.Tasks.RemoveRange(done);
            await db.SaveChangesAsync();
        }

        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(TaskRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        if (!_dialogs.Confirm($"Delete the task \"{row.Title}\"?", "Delete task"))
        {
            return;
        }

        if (Active?.Id == row.Id)
        {
            if (IsRunning)
            {
                await PauseAsync();
            }

            Active = null;
        }

        await using (var db = await _dbFactory.CreateDbContextAsync())
        {
            var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == row.Id);
            if (task is not null)
            {
                db.Tasks.Remove(task);
                await db.SaveChangesAsync();
            }
        }

        await LoadAsync();
    }

    // ---------- refresh ----------

    private async Task RefreshPausePlateAsync()
    {
        if (Active is null)
        {
            AccruedPauseText = "00:00:00";
            StopCount = 0;
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();

        var sessions = await db.TaskSessions
            .Where(s => s.TaskItemId == Active.Id)
            .AsNoTracking()
            .ToListAsync();

        if (sessions.Count == 0)
        {
            AccruedPauseText = "00:00:00";
            StopCount = 0;
            return;
        }

        var firstStart = sessions.Min(s => s.StartedUtc);
        var worked = sessions.Sum(s => s.DurationSeconds);

        AccruedPauseText = TimerCalculator.Format(
            TimerCalculator.AccruedPauseSeconds(firstStart, worked, DateTime.UtcNow));
        StopCount = sessions.Count(s => s.EndedUtc is not null);
        OnPropertyChanged(nameof(StopText));
    }

    /// <summary>Called on every tick: only the values that actually change each second.</summary>
    private void RefreshReadout()
    {
        OnPropertyChanged(nameof(RemainingSeconds));
        OnPropertyChanged(nameof(RemainingText));
        OnPropertyChanged(nameof(RemainingCaption));
        OnPropertyChanged(nameof(IsOverrun));
        OnPropertyChanged(nameof(Progress));
    }

    private void RefreshAll()
    {
        RefreshReadout();
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(HasActive));
        OnPropertyChanged(nameof(HeaderStatus));
        OnPropertyChanged(nameof(ActiveTitle));
        OnPropertyChanged(nameof(EstimateText));
        OnPropertyChanged(nameof(AnchorText));
        OnPropertyChanged(nameof(StopText));
        OnPropertyChanged(nameof(HasCompleted));
        OnPropertyChanged(nameof(CompletedCount));

        foreach (var row in Tasks)
        {
            row.Refresh();
        }
    }
}
