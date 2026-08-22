using CommunityToolkit.Mvvm.ComponentModel;
using WorkStationX.Models;
using WorkStationX.Services;

namespace WorkStationX.ViewModels;

public partial class ReminderRowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isActive;

    public ReminderRowViewModel(Reminder reminder)
    {
        Reminder = reminder;
        _isActive = reminder.IsActive;
    }

    public Reminder Reminder { get; }

    public int Id => Reminder.Id;

    public string Message => Reminder.Message;

    public string ScheduleText => ReminderScheduler.Describe(Reminder);

    public string NextFireText =>
        Reminder.NextFireUtc is { } next && Reminder.IsActive
            ? $"next {next.ToLocalTime():ddd HH:mm}"
            : "paused";
}
