using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using WorkStationX.Infrastructure;
using WorkStationX.Models;

namespace WorkStationX.ViewModels;

/// <summary>One app or website inside the workspace editor.</summary>
public partial class WorkspaceItemRowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _target = string.Empty;

    [ObservableProperty]
    private ChromeProfile? _chromeProfile;

    [ObservableProperty]
    private int _delaySeconds;

    public WorkspaceItemRowViewModel(WorkspaceItemType type)
    {
        Type = type;
    }

    public WorkspaceItemType Type { get; }

    public bool IsApp => Type == WorkspaceItemType.App;

    public bool IsWebsite => Type == WorkspaceItemType.Website;

    public int? ExistingItemId { get; init; }

    public ImageSource? Icon => IsApp ? IconLoader.ForFile(Target) : null;

    /// <summary>True when an app's .exe has gone missing, so the row can warn.</summary>
    public bool IsBroken => IsApp && !string.IsNullOrWhiteSpace(Target) &&
                            !System.IO.File.Exists(Target);

    partial void OnTargetChanged(string value)
    {
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(IsBroken));
    }

    public static WorkspaceItemRowViewModel FromModel(WorkspaceItem item, int delaySeconds) =>
        new(item.Type)
        {
            ExistingItemId = item.Id,
            DisplayName = item.DisplayName,
            Target = item.Target,
            ChromeProfile = item.ChromeProfile,
            DelaySeconds = delaySeconds
        };
}
