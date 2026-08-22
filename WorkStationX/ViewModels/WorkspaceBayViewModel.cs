using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Serilog;
using WorkStationX.Data;
using WorkStationX.Models;
using WorkStationX.Services;
using WorkStationX.Views;

namespace WorkStationX.ViewModels;

/// <summary>Bay 1: the workspace switch bank.</summary>
public partial class WorkspaceBayViewModel : ObservableObject
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILauncherService _launcher;
    private readonly IChromeProfileService _chrome;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private WorkspaceRowViewModel? _selected;

    [ObservableProperty]
    private bool _isLoading;

    public WorkspaceBayViewModel(
        IDbContextFactory<AppDbContext> dbFactory,
        ILauncherService launcher,
        IChromeProfileService chrome,
        IDialogService dialogs)
    {
        _dbFactory = dbFactory;
        _launcher = launcher;
        _chrome = chrome;
        _dialogs = dialogs;
    }

    public ObservableCollection<WorkspaceRowViewModel> Workspaces { get; } = new();

    public bool IsEmpty => Workspaces.Count == 0 && !IsLoading;

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            await _chrome.SyncToDatabaseAsync();

            await using var db = await _dbFactory.CreateDbContextAsync();

            var workspaces = await db.Workspaces
                .Include(w => w.ItemLinks).ThenInclude(l => l.WorkspaceItem)
                    .ThenInclude(i => i!.ChromeProfile)
                .OrderBy(w => w.SortOrder).ThenBy(w => w.Name)
                .AsNoTracking()
                .ToListAsync();

            Workspaces.Clear();
            for (var i = 0; i < workspaces.Count; i++)
            {
                Workspaces.Add(new WorkspaceRowViewModel(workspaces[i], i)
                {
                    IsOpen = _launcher.IsOpen(workspaces[i].Id)
                });
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not load workspaces");
            _dialogs.Inform("Could not load your workspaces. See the log for details.");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    /// <summary>
    /// Flipping a switch on launches the set; flipping it off closes what we opened.
    /// </summary>
    [RelayCommand]
    private async Task ToggleAsync(WorkspaceRowViewModel? row)
    {
        if (row is null || row.IsBusy)
        {
            return;
        }

        row.IsBusy = true;
        try
        {
            if (row.IsOpen)
            {
                await _launcher.CloseAsync(row.Id);
                row.IsOpen = false;
                return;
            }

            await using var db = await _dbFactory.CreateDbContextAsync();
            var workspace = await LoadFullAsync(db, row.Id);
            if (workspace is null)
            {
                return;
            }

            if (workspace.ItemLinks.Count == 0)
            {
                _dialogs.Inform(
                    $"\"{row.Name}\" has nothing in it yet.\n\nRight-click it and choose Edit to add apps or websites.",
                    "Empty workspace");
                return;
            }

            var result = await _launcher.LaunchAsync(workspace);
            row.IsOpen = result.Launched > 0 || result.SkippedAlreadyRunning > 0;

            if (result.HasProblems)
            {
                var lines = result.Problems.Select(p => $"- {p.ItemName}: {p.Reason}");
                _dialogs.Inform(
                    $"Opened {result.Launched} item(s), but some did not start:\n\n" +
                    string.Join("\n", lines),
                    "Workspace launched with problems");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not toggle workspace {Id}", row.Id);
            _dialogs.Inform($"Could not open that workspace.\n\n{ex.Message}");
        }
        finally
        {
            row.IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var profiles = await db.ChromeProfiles.AsNoTracking().ToListAsync();
        var editor = new WorkspaceEditorViewModel(_dialogs, profiles);

        if (_dialogs.ShowDialog<WorkspaceEditorWindow>(editor) != true || !editor.Accepted)
        {
            return;
        }

        var workspace = new Workspace
        {
            Name = editor.Name.Trim(),
            SortOrder = Workspaces.Count
        };

        db.Workspaces.Add(workspace);
        await db.SaveChangesAsync();
        await PersistItemsAsync(db, workspace, editor);

        await LoadAsync();
    }

    [RelayCommand]
    private async Task EditAsync(WorkspaceRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();

        var workspace = await LoadFullAsync(db, row.Id, tracking: true);
        if (workspace is null)
        {
            return;
        }

        var profiles = await db.ChromeProfiles.AsNoTracking().ToListAsync();
        var editor = new WorkspaceEditorViewModel(_dialogs, profiles, workspace);

        if (_dialogs.ShowDialog<WorkspaceEditorWindow>(editor) != true || !editor.Accepted)
        {
            return;
        }

        workspace.Name = editor.Name.Trim();
        await PersistItemsAsync(db, workspace, editor);

        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(WorkspaceRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        if (!_dialogs.Confirm(
                $"Delete the workspace \"{row.Name}\"?\n\nThe apps and websites themselves are not affected.",
                "Delete workspace"))
        {
            return;
        }

        await using (var db = await _dbFactory.CreateDbContextAsync())
        {
            var workspace = await db.Workspaces
                .Include(w => w.ItemLinks)
                .FirstOrDefaultAsync(w => w.Id == row.Id);

            if (workspace is not null)
            {
                db.Workspaces.Remove(workspace);
                await db.SaveChangesAsync();
            }
        }

        await LoadAsync();
    }

    private static Task<Workspace?> LoadFullAsync(AppDbContext db, int id, bool tracking = false)
    {
        var query = db.Workspaces
            .Include(w => w.ItemLinks).ThenInclude(l => l.WorkspaceItem)
                .ThenInclude(i => i!.ChromeProfile)
            .Where(w => w.Id == id);

        return tracking
            ? query.FirstOrDefaultAsync()
            : query.AsNoTracking().FirstOrDefaultAsync();
    }

    /// <summary>
    /// Replaces the workspace's links with what the editor holds. Items themselves are
    /// reused when they already exist, so one app can belong to several workspaces.
    /// </summary>
    private static async Task PersistItemsAsync(
        AppDbContext db, Workspace workspace, WorkspaceEditorViewModel editor)
    {
        var existingLinks = await db.WorkspaceItemLinks
            .Where(l => l.WorkspaceId == workspace.Id)
            .ToListAsync();

        db.WorkspaceItemLinks.RemoveRange(existingLinks);
        await db.SaveChangesAsync();

        for (var i = 0; i < editor.Items.Count; i++)
        {
            var row = editor.Items[i];
            if (string.IsNullOrWhiteSpace(row.Target))
            {
                continue;
            }

            var target = row.Target.Trim();
            var type = row.Type;

            var item = await db.WorkspaceItems
                .FirstOrDefaultAsync(x => x.Target == target && x.Type == type);

            if (item is null)
            {
                item = new WorkspaceItem
                {
                    DisplayName = string.IsNullOrWhiteSpace(row.DisplayName)
                        ? target
                        : row.DisplayName.Trim(),
                    Type = type,
                    Target = target,
                    ChromeProfileId = row.ChromeProfile?.Id
                };
                db.WorkspaceItems.Add(item);
                await db.SaveChangesAsync();
            }
            else if (row.IsWebsite && item.ChromeProfileId != row.ChromeProfile?.Id)
            {
                item.ChromeProfileId = row.ChromeProfile?.Id;
            }

            db.WorkspaceItemLinks.Add(new WorkspaceItemLink
            {
                WorkspaceId = workspace.Id,
                WorkspaceItemId = item.Id,
                LaunchOrder = i,
                DelaySeconds = row.DelaySeconds
            });
        }

        await db.SaveChangesAsync();
    }
}
