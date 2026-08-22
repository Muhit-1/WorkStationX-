using System.Windows.Input;
using WorkStationX.Services;

namespace WorkStationX.Tests;

public class HotkeyTests
{
    [Fact]
    public void ABindingSurvivesASaveAndReload()
    {
        var original = new HotkeyBinding(
            HotkeyAction.CaptureRegion, ModifierKeys.Control | ModifierKeys.Shift, Key.D2);

        var reloaded = HotkeyBinding.Parse(HotkeyAction.CaptureRegion, original.Serialise());

        Assert.Equal(original.Modifiers, reloaded.Modifiers);
        Assert.Equal(original.Key, reloaded.Key);
    }

    [Fact]
    public void AClearedShortcutStaysCleared()
    {
        // An empty stored value must NOT quietly restore the default on next launch,
        // or a shortcut the user deliberately removed keeps coming back.
        var settings = new UserSettings();
        var cleared = new HotkeyBinding(HotkeyAction.PickColour, ModifierKeys.None, Key.None);

        HotkeyDefaults.Save(settings, new[] { cleared });
        var loaded = HotkeyDefaults.Load(settings);

        Assert.True(loaded.Single(b => b.Action == HotkeyAction.PickColour).IsEmpty);
    }

    [Fact]
    public void MissingSettingsFallBackToTheDefaults()
    {
        var loaded = HotkeyDefaults.Load(new UserSettings());

        Assert.Equal(HotkeyDefaults.All.Count, loaded.Count);
        Assert.All(loaded, b => Assert.False(b.IsEmpty));
    }

    [Fact]
    public void EveryDefaultCarriesAModifier()
    {
        // A bare key would be swallowed system-wide and break normal typing.
        Assert.All(HotkeyDefaults.All, b => Assert.NotEqual(ModifierKeys.None, b.Modifiers));
    }

    [Fact]
    public void DefaultsDoNotCollideWithEachOther()
    {
        var combos = HotkeyDefaults.All.Select(b => (b.Modifiers, b.Key)).ToList();

        Assert.Equal(combos.Count, combos.Distinct().Count());
    }

    [Fact]
    public void EveryActionHasADefaultAndADescription()
    {
        foreach (var action in Enum.GetValues<HotkeyAction>())
        {
            Assert.Contains(HotkeyDefaults.All, b => b.Action == action);
            Assert.False(string.IsNullOrWhiteSpace(HotkeyDefaults.Describe(action)));
        }
    }

    [Theory]
    [InlineData(ModifierKeys.Control | ModifierKeys.Shift, Key.D2, "Ctrl + Shift + 2")]
    [InlineData(ModifierKeys.Control, Key.W, "Ctrl + W")]
    [InlineData(ModifierKeys.Alt | ModifierKeys.Shift, Key.F4, "Alt + Shift + F4")]
    public void DisplayReadsTheWayAMenuWouldWriteIt(
        ModifierKeys modifiers, Key key, string expected)
    {
        var binding = new HotkeyBinding(HotkeyAction.PickColour, modifiers, key);

        Assert.Equal(expected, binding.Display);
    }

    [Fact]
    public void AnEmptyBindingSaysSoRatherThanShowingNothing()
    {
        var binding = new HotkeyBinding(HotkeyAction.ToggleRuler, ModifierKeys.None, Key.None);

        Assert.Equal("Not set", binding.Display);
    }

    [Fact]
    public void GarbledSettingsDoNotCrashTheApp()
    {
        // A hand-edited settings.json must degrade to "no shortcut", not an exception.
        var binding = HotkeyBinding.Parse(HotkeyAction.PickColour, "not|a|number");

        Assert.True(binding.IsEmpty);
    }
}
