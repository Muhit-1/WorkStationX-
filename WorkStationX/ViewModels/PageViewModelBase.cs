using CommunityToolkit.Mvvm.ComponentModel;

namespace WorkStationX.ViewModels;

/// <summary>
/// Base for anything hosted in the shell's content area. Pages are resolved from
/// DI and selected by DataTemplate, so no view-model ever constructs a Window.
/// </summary>
public abstract partial class PageViewModelBase : ObservableObject
{
    /// <summary>Label shown in the sidebar.</summary>
    public abstract string Title { get; }

    /// <summary>Segoe Fluent Icons glyph for the sidebar button.</summary>
    public abstract string Glyph { get; }

    /// <summary>Called each time the page becomes visible. Override to load data.</summary>
    public virtual Task OnNavigatedToAsync() => Task.CompletedTask;
}
