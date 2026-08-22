using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkStationX.Models;
using WorkStationX.Services;

namespace WorkStationX.ViewModels;

/// <summary>Add or edit one workspace and its items. Shown as a modal dialog.</summary>
public partial class WorkspaceEditorViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _newUrl = string.Empty;

    [ObservableProperty]
    private ChromeProfile? _newUrlProfile;

    [ObservableProperty]
    private WorkspaceItemRowViewModel? _selectedItem;

    [ObservableProperty]
    private string? _error;

    public WorkspaceEditorViewModel(
        IDialogService dialogs,
        IReadOnlyList<ChromeProfile> profiles,
        Workspace? existing = null)
    {
        _dialogs = dialogs;
        Profiles = new ObservableCollection<ChromeProfile>(profiles);
        _newUrlProfile = profiles.FirstOrDefault(p => p.IsDefault) ?? profiles.FirstOrDefault();

        IsNew = existing is null;
        EditingId = existing?.Id;

        if (existing is not null)
        {
            _name = existing.Name;
            foreach (var link in existing.ItemLinks.OrderBy(l => l.LaunchOrder))
            {
                if (link.WorkspaceItem is { } item)
                {
                    Items.Add(WorkspaceItemRowViewModel.FromModel(item, link.DelaySeconds));
                }
            }
        }
    }

    public bool IsNew { get; }

    public int? EditingId { get; }

    public string HeaderText => IsNew ? "NEW WORKSPACE" : "EDIT WORKSPACE";

    public ObservableCollection<ChromeProfile> Profiles { get; }

    public ObservableCollection<WorkspaceItemRowViewModel> Items { get; } = new();

    public bool HasProfiles => Profiles.Count > 0;

    /// <summary>Set by the view when the dialog is accepted, so the caller can persist.</summary>
    public bool Accepted { get; private set; }

    [RelayCommand]
    private void AddApp()
    {
        if (_dialogs.PickExecutable() is not { } path)
        {
            return;
        }

        Items.Add(new WorkspaceItemRowViewModel(WorkspaceItemType.App)
        {
            DisplayName = Path.GetFileNameWithoutExtension(path),
            Target = path
        });
    }

    [RelayCommand]
    private void AddUrl()
    {
        var url = NormaliseUrl(NewUrl);
        if (url is null)
        {
            Error = "Enter a valid web address.";
            return;
        }

        Items.Add(new WorkspaceItemRowViewModel(WorkspaceItemType.Website)
        {
            DisplayName = HostOf(url),
            Target = url,
            ChromeProfile = NewUrlProfile
        });

        NewUrl = string.Empty;
        Error = null;
    }

    [RelayCommand]
    private void Remove(WorkspaceItemRowViewModel? row)
    {
        if (row is not null)
        {
            Items.Remove(row);
        }
    }

    [RelayCommand]
    private void MoveUp(WorkspaceItemRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var i = Items.IndexOf(row);
        if (i > 0)
        {
            Items.Move(i, i - 1);
        }
    }

    [RelayCommand]
    private void MoveDown(WorkspaceItemRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var i = Items.IndexOf(row);
        if (i >= 0 && i < Items.Count - 1)
        {
            Items.Move(i, i + 1);
        }
    }

    /// <summary>Returns true when the dialog may close.</summary>
    public bool TryAccept()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            Error = "Give the workspace a name.";
            return false;
        }

        Error = null;
        Accepted = true;
        return true;
    }

    /// <summary>Accepts "github.com" as readily as a full URL.</summary>
    public static string? NormaliseUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim();
        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = "https://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var ok = uri.Scheme is "http" or "https";
        return ok && uri.Host.Contains('.') ? uri.ToString() : null;
    }

    public static string HostOf(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                ? uri.Host[4..]
                : uri.Host
            : url;
}
