using WorkStationX.Services;

namespace WorkStationX.Tests;

/// <summary>
/// Opening a site in the right Chrome profile is the whole point of the workspace
/// launcher, and getting it wrong fails silently - the tabs open, just under the
/// wrong Google account. These lock the command line down.
/// </summary>
public class ChromeArgumentTests
{
    [Fact]
    public void ProfileFlagComesBeforeAnyUrl()
    {
        var args = LauncherService.BuildChromeArguments(
            "Profile 1", new[] { "https://github.com/", "https://figma.com/" });

        Assert.Equal("--profile-directory=Profile 1", args[0]);
        Assert.True(
            args.ToList().IndexOf("https://github.com/") > 0,
            "URLs must follow the profile flag, or Chrome routes them to whichever profile is already open.");
    }

    [Fact]
    public void AllUrlsGoIntoOneWindow()
    {
        var urls = new[] { "https://a.com/", "https://b.com/", "https://c.com/" };

        var args = LauncherService.BuildChromeArguments("Profile 4", urls);

        // One --new-window, three URLs: one window with three tabs.
        Assert.Single(args, a => a == "--new-window");
        Assert.All(urls, u => Assert.Contains(u, args));
        Assert.Equal(5, args.Count);
    }

    [Fact]
    public void OmitsTheProfileFlagWhenNoProfileIsMapped()
    {
        var args = LauncherService.BuildChromeArguments(null, new[] { "https://a.com/" });

        Assert.DoesNotContain(args, a => a.StartsWith("--profile-directory", StringComparison.Ordinal));
        Assert.Equal("--new-window", args[0]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TreatsBlankProfileAsNoProfile(string profile)
    {
        var args = LauncherService.BuildChromeArguments(profile, new[] { "https://a.com/" });

        Assert.DoesNotContain(args, a => a.StartsWith("--profile-directory", StringComparison.Ordinal));
    }

    [Fact]
    public void DropsEmptyUrls()
    {
        var args = LauncherService.BuildChromeArguments(
            "Default", new[] { "https://a.com/", "", "   " });

        Assert.Equal(new[] { "--profile-directory=Default", "--new-window", "https://a.com/" }, args);
    }

    [Fact]
    public void PassesProfileDirectoryWithSpacesAsASingleArgument()
    {
        // ArgumentList quotes each entry, so "Profile 1" must stay one element -
        // splitting it would make Chrome ignore the flag entirely.
        var args = LauncherService.BuildChromeArguments("Profile 1", Array.Empty<string>());

        Assert.Single(args, a => a.Contains("Profile 1", StringComparison.Ordinal));
        Assert.Equal("--profile-directory=Profile 1", args[0]);
    }
}
