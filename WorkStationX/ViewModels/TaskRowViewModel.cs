using CommunityToolkit.Mvvm.ComponentModel;
using WorkStationX.Models;
using WorkStationX.Services;

namespace WorkStationX.ViewModels;

/// <summary>One row in the task list under the timer.</summary>
public partial class TaskRowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isActive;

    public TaskRowViewModel(TaskItem task)
    {
        Task = task;
    }

    public TaskItem Task { get; }

    public int Id => Task.Id;

    public string Title => Task.Title;

    public bool IsDone => Task.Status == Models.TaskStatus.Done;

    public string EstimateText => $"est {Task.EstimatedMinutes}m";

    public string RemainingText => TimerCalculator.Format(Task.RemainingSeconds);

    /// <summary>"act 0:58" once there is real time on the clock.</summary>
    public string ActualText =>
        Task.ActualSecondsSpent > 0
            ? $"act {Task.ActualSecondsSpent / 3600}:{Task.ActualSecondsSpent % 3600 / 60:D2}"
            : string.Empty;

    public bool IsOverrun => Task.RemainingSeconds < 0;

    public void Refresh()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(IsDone));
        OnPropertyChanged(nameof(EstimateText));
        OnPropertyChanged(nameof(RemainingText));
        OnPropertyChanged(nameof(ActualText));
        OnPropertyChanged(nameof(IsOverrun));
    }
}
