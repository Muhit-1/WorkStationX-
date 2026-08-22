using System.Windows.Media;
using WorkStationX.Services;

namespace WorkStationX.Tests;

public class ColorPickServiceTests
{
    private readonly ColorPickService _service = new();

    [Theory]
    [InlineData(0, 0, 0, "#000000")]
    [InlineData(255, 255, 255, "#FFFFFF")]
    [InlineData(116, 200, 234, "#74C8EA")]   // the Ice accent
    [InlineData(99, 220, 192, "#63DCC0")]    // the credit lamp
    [InlineData(5, 10, 15, "#050A0F")]       // single digits must stay zero-padded
    public void FormatsHexUppercaseAndPadded(byte r, byte g, byte b, string expected)
    {
        Assert.Equal(expected, _service.ToHex(Color.FromRgb(r, g, b)));
    }

    [Fact]
    public void HexRoundTripsThroughWpfParsing()
    {
        // The hex we copy to the clipboard has to be something other tools accept.
        var original = Color.FromRgb(116, 200, 234);

        var parsed = (Color)ColorConverter.ConvertFromString(_service.ToHex(original));

        Assert.Equal(original, parsed);
    }
}
