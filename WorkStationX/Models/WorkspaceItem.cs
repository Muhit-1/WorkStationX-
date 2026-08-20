namespace WorkStationX.Models;

public enum WorkspaceItemType
{
    App = 0,
    Website = 1
}

/// <summary>
/// A launchable app or URL. Exists independently of any workspace so the same item
/// can genuinely belong to several workspaces (via WorkspaceItemLink).
/// </summary>
public class WorkspaceItem
{
    public int Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public WorkspaceItemType Type { get; set; }

    /// <summary>Full path to the .exe for App, or the URL for Website.</summary>
    public string Target { get; set; } = string.Empty;

    public string? LaunchArguments { get; set; }

    public string? WorkingDirectory { get; set; }

    /// <summary>Only meaningful for Website items.</summary>
    public int? ChromeProfileId { get; set; }

    public ChromeProfile? ChromeProfile { get; set; }

    public List<WorkspaceItemLink> WorkspaceLinks { get; set; } = new();
}

/// <summary>
/// Join between Workspace and WorkspaceItem. Carries the per-workspace launch
/// ordering, because the same app may want a different position in each context.
/// </summary>
public class WorkspaceItemLink
{
    public int Id { get; set; }

    public int WorkspaceId { get; set; }

    public Workspace? Workspace { get; set; }

    public int WorkspaceItemId { get; set; }

    public WorkspaceItem? WorkspaceItem { get; set; }

    public int LaunchOrder { get; set; }

    /// <summary>Stagger before launching this item. Firing eight processes at once
    /// thrashes a low-RAM machine.</summary>
    public int DelaySeconds { get; set; }
}
