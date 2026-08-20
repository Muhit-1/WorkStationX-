namespace WorkStationX.Models;

/// <summary>A named context (Design, Research, Development) that launches a set of items.</summary>
public class Workspace
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? IconPath { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastLaunchedUtc { get; set; }

    public List<WorkspaceItemLink> ItemLinks { get; set; } = new();
}
