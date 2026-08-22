using WorkStationX.ViewModels;

namespace WorkStationX.Tests;

/// <summary>
/// The editor lets people type "github.com" rather than a full URL, because that is
/// what anyone actually types. These pin down what counts as valid.
/// </summary>
public class UrlNormalisationTests
{
    [Theory]
    [InlineData("github.com", "https://github.com/")]
    [InlineData("  github.com  ", "https://github.com/")]
    [InlineData("www.figma.com", "https://www.figma.com/")]
    [InlineData("http://example.com", "http://example.com/")]
    [InlineData("https://mail.google.com/mail/u/0", "https://mail.google.com/mail/u/0")]
    public void AcceptsAndCompletesRealAddresses(string input, string expected)
    {
        Assert.Equal(expected, WorkspaceEditorViewModel.NormaliseUrl(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("localhost")]          // no dot, so almost certainly a typo
    [InlineData("not a url")]
    [InlineData("ftp://files.example.com")]  // only http(s) opens in Chrome
    public void RejectsWhatIsNotAWebAddress(string? input)
    {
        Assert.Null(WorkspaceEditorViewModel.NormaliseUrl(input));
    }

    [Theory]
    [InlineData("https://github.com/", "github.com")]
    [InlineData("https://www.figma.com/files", "figma.com")]
    [InlineData("https://mail.google.com/mail", "mail.google.com")]
    public void DerivesAReadableNameFromTheHost(string url, string expected)
    {
        Assert.Equal(expected, WorkspaceEditorViewModel.HostOf(url));
    }
}
