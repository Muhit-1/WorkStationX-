using System.Windows.Media;

namespace WorkStationX.Tests;

/// <summary>
/// GetPixel returns a Win32 COLORREF, which is 0x00BBGGRR - blue and red are the
/// opposite way round to almost every other API. Reading it as RGB produces a colour
/// that is plausible but wrong, with nothing to indicate a bug: a designer sampling
/// a brand blue would quietly be handed an orange.
///
/// This mirrors the unpacking in ColorPickService so the byte order stays pinned.
/// </summary>
public class ColorRefTests
{
    private static Color FromColorRef(uint colorRef) =>
        Color.FromRgb(
            (byte)(colorRef & 0x000000FF),
            (byte)((colorRef & 0x0000FF00) >> 8),
            (byte)((colorRef & 0x00FF0000) >> 16));

    [Fact]
    public void PureRedComesBackAsRedNotBlue()
    {
        // COLORREF for pure red is 0x000000FF - the low byte.
        var color = FromColorRef(0x000000FF);

        Assert.Equal(Color.FromRgb(255, 0, 0), color);
    }

    [Fact]
    public void PureBlueComesBackAsBlueNotRed()
    {
        // COLORREF for pure blue is 0x00FF0000 - the high byte. Read naively as RGB
        // this would come out as red.
        var color = FromColorRef(0x00FF0000);

        Assert.Equal(Color.FromRgb(0, 0, 255), color);
    }

    [Fact]
    public void GreenIsUnaffectedByTheSwapWhichIsWhyTheBugHides()
    {
        Assert.Equal(Color.FromRgb(0, 255, 0), FromColorRef(0x0000FF00));
    }

    [Fact]
    public void MixedColourUnpacksEveryChannelCorrectly()
    {
        // #74C8EA -> R=0x74 G=0xC8 B=0xEA -> COLORREF 0x00EAC874
        var color = FromColorRef(0x00EAC874);

        Assert.Equal(Color.FromRgb(0x74, 0xC8, 0xEA), color);
    }

    [Fact]
    public void HighByteIsIgnoredRatherThanBleedingIntoBlue()
    {
        // COLORREF's top byte is reserved; masking must drop it.
        Assert.Equal(Color.FromRgb(0x74, 0xC8, 0xEA), FromColorRef(0xFFEAC874));
    }
}
