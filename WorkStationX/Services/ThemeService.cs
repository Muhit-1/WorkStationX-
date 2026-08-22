using System.Windows;

namespace WorkStationX.Services;

/// <summary>A selectable colourway. Source is the palette dictionary to merge.</summary>
public sealed record ThemeOption(
    string Id, string Name, string Blurb, string Source,
    string PanelHex, string PrimeHex, string LampHex);

public interface IThemeService
{
    IReadOnlyList<ThemeOption> Available { get; }
    ThemeOption Current { get; }
    void Apply(string themeId);
}

/// <summary>
/// Swaps the palette dictionary at index 0 of Application.Resources.
///
/// This works because every style uses DynamicResource for colour: replacing the
/// dictionary re-resolves them live, so the whole window recolours with no restart
/// and no reload of the view tree.
/// </summary>
public class ThemeService : IThemeService
{
    public const string DefaultThemeId = "dark-green";

    private readonly Application _app;

    public ThemeService(Application app)
    {
        _app = app;
        Current = Available[0];
    }

    public IReadOnlyList<ThemeOption> Available { get; } = new[]
    {
        new ThemeOption(
            "dark-green", "Ice",
            "Dark green",
            "Themes/Palettes/DarkGreen.xaml",
            "#142720", "#74C8EA", "#63DCC0"),
        new ThemeOption(
            "graphite", "Amber",
            "Graphite",
            "Themes/Palettes/Graphite.xaml",
            "#212226", "#E8A33D", "#9BD46A"),
        new ThemeOption(
            "midnight", "Violet",
            "Midnight blue",
            "Themes/Palettes/Midnight.xaml",
            "#171B2C", "#9B8BF0", "#5BD3E6"),
        new ThemeOption(
            "oxblood", "Brass",
            "Oxblood red",
            "Themes/Palettes/Oxblood.xaml",
            "#2A1618", "#D9AC5E", "#C9B36B")
    };

    public ThemeOption Current { get; private set; }

    public void Apply(string themeId)
    {
        var theme = Available.FirstOrDefault(t => t.Id == themeId)
                    ?? Available.First(t => t.Id == DefaultThemeId);

        var merged = _app.Resources.MergedDictionaries;
        var palette = new ResourceDictionary
        {
            Source = new Uri(theme.Source, UriKind.Relative)
        };

        // Index 0 is the palette slot by convention; App.xaml declares it first.
        if (merged.Count == 0)
        {
            merged.Add(palette);
        }
        else
        {
            merged[0] = palette;
        }

        Current = theme;
    }
}
