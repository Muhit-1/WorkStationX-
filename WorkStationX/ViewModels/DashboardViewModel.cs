namespace WorkStationX.ViewModels;

/// <summary>
/// The single dashboard: workspaces, task timer and Time Bank side by side.
/// The design has no page navigation, so each bay gets its own child view-model
/// rather than being a separate page.
/// </summary>
public partial class DashboardViewModel : PageViewModelBase
{
    public DashboardViewModel(
        WorkspaceBayViewModel workspaces,
        TaskBayViewModel tasks,
        TimeBankBayViewModel bank)
    {
        Workspaces = workspaces;
        Tasks = tasks;
        Bank = bank;

        // Completing a task writes a ledger entry, so bay 3 reloads when bay 2 says so.
        Tasks.BankChanged += async (_, _) => await Bank.LoadAsync();
    }

    public override string Title => "Bench";

    public override string Glyph => "";

    /// <summary>Bay 1.</summary>
    public WorkspaceBayViewModel Workspaces { get; }

    /// <summary>Bay 2.</summary>
    public TaskBayViewModel Tasks { get; }

    /// <summary>Bay 3.</summary>
    public TimeBankBayViewModel Bank { get; }

    public override async Task OnNavigatedToAsync()
    {
        await Workspaces.LoadAsync();
        await Tasks.LoadAsync();
        await Bank.LoadAsync();
    }
}
