using System.IO;
using System.Xml.Linq;

namespace WorkStationX.Tests;

/// <summary>
/// Every palette must define exactly the same keys.
///
/// This matters because DynamicResource fails SILENTLY: a key missing from one
/// colourway does not throw, it just leaves that element unpainted. Without this
/// test the bug only shows up as "the Amber theme has a black hole in it", and
/// only if someone happens to switch to it.
/// </summary>
public class PaletteContractTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static string PaletteDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "WorkStationX.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "WorkStationX", "Themes", "Palettes");
    }

    private static IReadOnlyDictionary<string, HashSet<string>> LoadPaletteKeys()
    {
        var files = Directory.GetFiles(PaletteDirectory(), "*.xaml");
        return files.ToDictionary(
            f => Path.GetFileNameWithoutExtension(f)!,
            f => XDocument.Load(f)
                          .Descendants()
                          .Select(e => e.Attribute(X + "Key")?.Value)
                          .Where(k => k is not null)
                          .Select(k => k!)
                          .ToHashSet());
    }

    [Fact]
    public void ThereAreAtLeastTwoPalettes()
    {
        Assert.True(LoadPaletteKeys().Count >= 2);
    }

    [Fact]
    public void EveryPaletteDefinesTheSameKeys()
    {
        var palettes = LoadPaletteKeys();
        var reference = palettes["DarkGreen"];

        foreach (var (name, keys) in palettes.Where(p => p.Key != "DarkGreen"))
        {
            var missing = reference.Except(keys).OrderBy(k => k).ToList();
            var extra = keys.Except(reference).OrderBy(k => k).ToList();

            Assert.True(
                missing.Count == 0,
                $"{name}.xaml is missing: {string.Join(", ", missing)}");
            Assert.True(
                extra.Count == 0,
                $"{name}.xaml defines keys DarkGreen does not: {string.Join(", ", extra)}");
        }
    }

    [Fact]
    public void EveryPaletteDeclaresItsOwnIdAndName()
    {
        foreach (var (name, keys) in LoadPaletteKeys())
        {
            Assert.True(keys.Contains("Palette.Id"), $"{name}.xaml has no Palette.Id");
            Assert.True(keys.Contains("Palette.Name"), $"{name}.xaml has no Palette.Name");
        }
    }

    [Fact]
    public void DarkGreenDefinesTheKeysTheControlStylesActuallyUse()
    {
        // A sample of keys Controls.xaml and the views bind to. If one of these
        // disappears the whole panel loses a surface.
        string[] required =
        {
            "Brush.Shell", "Brush.Panel", "Brush.Well", "Brush.Titlebar",
            "Brush.Phos", "Brush.Engrave", "Brush.EngraveDim", "Brush.TextBright",
            "Brush.EdgeHi", "Brush.EdgeLo", "Brush.HoverWash",
            "Brush.Prime", "Brush.OnPrime", "Brush.LampOn", "Brush.LampRed",
            "Brush.BayFace", "Brush.Switch", "Brush.Plate", "Brush.Button",
            "Brush.ButtonPrime", "Brush.Tool", "Brush.Rail", "Brush.Screw",
            "Color.LampOn"
        };

        var keys = LoadPaletteKeys()["DarkGreen"];
        var missing = required.Where(k => !keys.Contains(k)).ToList();

        Assert.True(missing.Count == 0, $"DarkGreen.xaml is missing: {string.Join(", ", missing)}");
    }
}
