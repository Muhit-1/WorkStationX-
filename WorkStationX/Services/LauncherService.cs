using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using Serilog;
using WorkStationX.Models;

namespace WorkStationX.Services;

/// <summary>One thing that went wrong during a launch, surfaced to the user.</summary>
public sealed record LaunchProblem(string ItemName, string Reason);

public sealed record LaunchResult(
    int Launched,
    int SkippedAlreadyRunning,
    IReadOnlyList<LaunchProblem> Problems)
{
    public bool HasProblems => Problems.Count > 0;
}

public interface ILauncherService
{
    bool IsOpen(int workspaceId);
    Task<LaunchResult> LaunchAsync(Workspace workspace, CancellationToken ct = default);
    Task CloseAsync(int workspaceId);
}

public class LauncherService : ILauncherService
{
    private readonly IChromeProfileService _chrome;

    // Processes we started, per workspace. Only ours - closing a workspace must never
    // touch an app the user opened themselves.
    private readonly ConcurrentDictionary<int, List<Process>> _open = new();

    public LauncherService(IChromeProfileService chrome) => _chrome = chrome;

    public bool IsOpen(int workspaceId) =>
        _open.TryGetValue(workspaceId, out var procs) && procs.Any(p => !SafeHasExited(p));

    public async Task<LaunchResult> LaunchAsync(Workspace workspace, CancellationToken ct = default)
    {
        var items = workspace.ItemLinks
            .OrderBy(l => l.LaunchOrder)
            .Select(l => (Link: l, Item: l.WorkspaceItem))
            .Where(x => x.Item is not null)
            .ToList();

        var problems = new List<LaunchProblem>();
        var started = new List<Process>();
        var skipped = 0;

        // Apps first, in order, honouring each item's stagger.
        foreach (var (link, item) in items.Where(x => x.Item!.Type == WorkspaceItemType.App))
        {
            ct.ThrowIfCancellationRequested();

            if (link.DelaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(link.DelaySeconds), ct);
            }

            if (!File.Exists(item!.Target))
            {
                // The app was uninstalled or moved. Say so; do not crash the launch.
                problems.Add(new LaunchProblem(item.DisplayName, "File no longer exists"));
                continue;
            }

            if (IsProcessRunning(item.Target))
            {
                skipped++;
                continue;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = item.Target,
                    UseShellExecute = true,
                    WorkingDirectory = string.IsNullOrWhiteSpace(item.WorkingDirectory)
                        ? Path.GetDirectoryName(item.Target) ?? string.Empty
                        : item.WorkingDirectory
                };

                if (!string.IsNullOrWhiteSpace(item.LaunchArguments))
                {
                    psi.Arguments = item.LaunchArguments;
                }

                if (Process.Start(psi) is { } proc)
                {
                    started.Add(proc);
                }
            }
            catch (Exception ex)
            {
                problems.Add(new LaunchProblem(item.DisplayName, ex.Message));
                Log.Warning(ex, "Could not launch {Target}", item.Target);
            }
        }

        // Websites grouped by profile: one Chrome call per profile with every URL as an
        // argument, so a profile gets ONE window with tabs rather than N windows.
        var sites = items
            .Where(x => x.Item!.Type == WorkspaceItemType.Website)
            .GroupBy(x => x.Item!.ChromeProfile?.ProfileDirectory);

        foreach (var group in sites)
        {
            ct.ThrowIfCancellationRequested();

            var urls = group.Select(g => g.Item!.Target).Where(u => !string.IsNullOrWhiteSpace(u)).ToList();
            if (urls.Count == 0)
            {
                continue;
            }

            if (_chrome.ChromeExecutablePath is not { } chromePath)
            {
                problems.Add(new LaunchProblem(
                    $"{urls.Count} website(s)", "Chrome is not installed"));
                continue;
            }

            try
            {
                var psi = new ProcessStartInfo { FileName = chromePath, UseShellExecute = false };

                foreach (var arg in BuildChromeArguments(group.Key, urls))
                {
                    psi.ArgumentList.Add(arg);
                }

                if (Process.Start(psi) is { } proc)
                {
                    started.Add(proc);
                }
            }
            catch (Exception ex)
            {
                problems.Add(new LaunchProblem($"{urls.Count} website(s)", ex.Message));
                Log.Warning(ex, "Could not launch Chrome for profile {Profile}", group.Key);
            }
        }

        _open.AddOrUpdate(workspace.Id, started, (_, existing) =>
        {
            existing.AddRange(started);
            return existing;
        });

        Log.Information(
            "Launched workspace {Name}: {Started} started, {Skipped} already running, {Problems} problem(s)",
            workspace.Name, started.Count, skipped, problems.Count);

        return new LaunchResult(started.Count, skipped, problems);
    }

    /// <summary>
    /// Asks each process we started to close, then gives up rather than killing it -
    /// a forced kill would lose the user's unsaved work.
    /// </summary>
    public async Task CloseAsync(int workspaceId)
    {
        if (!_open.TryRemove(workspaceId, out var procs))
        {
            return;
        }

        foreach (var proc in procs)
        {
            try
            {
                if (SafeHasExited(proc))
                {
                    continue;
                }

                if (proc.MainWindowHandle != IntPtr.Zero)
                {
                    proc.CloseMainWindow();
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Could not close process {Id}", SafeId(proc));
            }
        }

        // Give windows a moment to act on the close request before we stop tracking.
        await Task.Delay(TimeSpan.FromMilliseconds(400));

        foreach (var proc in procs)
        {
            proc.Dispose();
        }
    }

    /// <summary>
    /// Builds Chrome's command line for one profile.
    ///
    /// --profile-directory MUST come first: Chrome reads it while deciding which
    /// process to hand the URLs to, and a URL placed before it is opened by whichever
    /// profile happens to be running. Getting this order wrong is silent - the sites
    /// open, just signed in as the wrong person - which is exactly why it is tested.
    /// </summary>
    public static IReadOnlyList<string> BuildChromeArguments(
        string? profileDirectory, IEnumerable<string> urls)
    {
        var args = new List<string>();

        if (!string.IsNullOrWhiteSpace(profileDirectory))
        {
            args.Add($"--profile-directory={profileDirectory}");
        }

        // One window per profile, with the URLs as tabs, rather than one window each.
        args.Add("--new-window");

        args.AddRange(urls.Where(u => !string.IsNullOrWhiteSpace(u)));
        return args;
    }

    private static bool IsProcessRunning(string exePath)
    {
        var name = Path.GetFileNameWithoutExtension(exePath);
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        // Matching on process name only: reading MainModule.FileName for another
        // user's or an elevated process throws, and would make this unreliable.
        return Process.GetProcessesByName(name).Length > 0;
    }

    private static bool SafeHasExited(Process p)
    {
        try
        {
            return p.HasExited;
        }
        catch (Exception)
        {
            return true;
        }
    }

    private static string SafeId(Process p)
    {
        try
        {
            return p.Id.ToString();
        }
        catch (Exception)
        {
            return "?";
        }
    }
}
