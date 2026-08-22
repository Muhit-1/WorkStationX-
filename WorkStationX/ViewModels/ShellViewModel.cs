using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkStationX.Services;

namespace WorkStationX.ViewModels;

/// <summary>
/// Owns the window chrome: brand mark, status bar, and whichever page is on the
/// panel face. The dashboard is home; History and Settings are the two detours.
/// </summary>
public partial class ShellViewModel : ObservableObject
{
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private PageViewModelBase? _currentPage;

    [ObservableProperty]
    private string _statusText = "READY";

    [ObservableProperty]
    private string _statusHint = string.Empty;

    public string Brand => "WORKSTATIONX";

    /// <summary>The screen tools on the bottom rail.</summary>
    public ToolsViewModel Tools { get; } = null!;

    public ShellViewModel(INavigationService navigation, ToolsViewModel tools)
    {
        _navigation = navigation;
        Tools = tools;
        _navigation.Navigated += (_, page) => CurrentPage = page;
    }

    public bool IsOnDashboard => CurrentPage is DashboardViewModel;

    public bool IsOnHistory => CurrentPage is HistoryViewModel;

    public bool IsOnSettings => CurrentPage is SettingsViewModel;

    partial void OnCurrentPageChanged(PageViewModelBase? value)
    {
        OnPropertyChanged(nameof(IsOnDashboard));
        OnPropertyChanged(nameof(IsOnHistory));
        OnPropertyChanged(nameof(IsOnSettings));
    }

    /// <summary>Each nav button toggles: pressing it again returns to the bench.</summary>
    [RelayCommand]
    private Task ShowHistoryAsync() =>
        IsOnHistory
            ? _navigation.NavigateToAsync<DashboardViewModel>()
            : _navigation.NavigateToAsync<HistoryViewModel>();

    [RelayCommand]
    private Task ShowSettingsAsync() =>
        IsOnSettings
            ? _navigation.NavigateToAsync<DashboardViewModel>()
            : _navigation.NavigateToAsync<SettingsViewModel>();

    public Task InitializeAsync() => _navigation.NavigateToAsync<DashboardViewModel>();
}
