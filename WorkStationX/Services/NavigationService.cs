using WorkStationX.ViewModels;

namespace WorkStationX.Services;

public interface INavigationService
{
    PageViewModelBase? Current { get; }
    event EventHandler<PageViewModelBase>? Navigated;
    Task NavigateToAsync<TPage>() where TPage : PageViewModelBase;
    Task NavigateToAsync(PageViewModelBase page);
}

/// <summary>
/// Resolves pages from the DI container and raises Navigated. Having this means
/// view-models never call `new Window()`, which is what keeps the MVVM claim honest.
/// </summary>
public class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;

    public NavigationService(IServiceProvider services) => _services = services;

    public PageViewModelBase? Current { get; private set; }

    public event EventHandler<PageViewModelBase>? Navigated;

    public Task NavigateToAsync<TPage>() where TPage : PageViewModelBase
    {
        var page = (PageViewModelBase)_services.GetService(typeof(TPage))!;
        return NavigateToAsync(page);
    }

    public async Task NavigateToAsync(PageViewModelBase page)
    {
        if (ReferenceEquals(Current, page))
        {
            return;
        }

        Current = page;
        Navigated?.Invoke(this, page);
        await page.OnNavigatedToAsync();
    }
}
