using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using WorkStationX.Infrastructure;
using WorkStationX.Models;

namespace WorkStationX.ViewModels;

/// <summary>One switch in the bank.</summary>
public partial class WorkspaceRowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private bool _isBusy;

    public WorkspaceRowViewModel(Workspace workspace, int index)
    {
        Workspace = workspace;
        Shortcut = index < 9 ? $"Alt {index + 1}" : string.Empty;
    }

    public Workspace Workspace { get; }

    public int Id => Workspace.Id;

    public string Name => Workspace.Name;

    public string Shortcut { get; }

    /// <summary>"VS 2022 · Figma · +3" - the same summary line the design shows.</summary>
    public string Summary
    {
        get
        {
            var names = Workspace.ItemLinks
                .OrderBy(l => l.LaunchOrder)
                .Select(l => l.WorkspaceItem?.DisplayName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();

            if (names.Count == 0)
            {
                return "Empty — add apps or websites";
            }

            var shown = names.Take(3).ToList();
            var rest = names.Count - shown.Count;
            var line = string.Join(" · ", shown);
            return rest > 0 ? $"{line} · +{rest}" : line;
        }
    }

    public int ItemCount => Workspace.ItemLinks.Count;

    /// <summary>Icon of the first app in the set, as a stand-in for the workspace.</summary>
    public ImageSource? Icon
    {
        get
        {
            var firstApp = Workspace.ItemLinks
                .OrderBy(l => l.LaunchOrder)
                .Select(l => l.WorkspaceItem)
                .FirstOrDefault(i => i is { Type: WorkspaceItemType.App });

            return firstApp is null ? null : IconLoader.ForFile(firstApp.Target);
        }
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(ItemCount));
        OnPropertyChanged(nameof(Icon));
    }
}
