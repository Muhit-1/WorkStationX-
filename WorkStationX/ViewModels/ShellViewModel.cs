using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkStationX.Services;

namespace WorkStationX.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private PageViewModelBase? _currentPage;

    public ObservableCollection<PageViewModelBase> Pages { get; }

    public ShellViewModel(
        INavigationService navigation,
        WorkspaceViewModel workspaces,
        TaskViewModel tasks,
        ToolsViewModel tools)
    {
        _navigation = navigation;
        Pages = new ObservableCollection<PageViewModelBase> { workspaces, tasks, tools };
        _navigation.Navigated += (_, page) => CurrentPage = page;
    }

    [RelayCommand]
    private Task NavigateAsync(PageViewModelBase? page) =>
        page is null ? Task.CompletedTask : _navigation.NavigateToAsync(page);

    public Task InitializeAsync() => _navigation.NavigateToAsync(Pages[0]);
}
