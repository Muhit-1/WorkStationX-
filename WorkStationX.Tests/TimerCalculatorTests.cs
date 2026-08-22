using WorkStationX.Services;

namespace WorkStationX.Tests;

public class TimerCalculatorTests
{
    private static readonly DateTime Anchor = new(2026, 8, 21, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PausedTimerReportsTheStoredValueUnchanged()
    {
        // runningSince == null means paused: the clock must not move.
        Assert.Equal(1800, TimerCalculator.RemainingSeconds(1800, null, Anchor.AddHours(9)));
    }

    [Fact]
    public void RunningTimerCountsDownFromTheAnchor()
    {
        var remaining = TimerCalculator.RemainingSeconds(1800, Anchor, Anchor.AddMinutes(5));

        Assert.Equal(1500, remaining);
    }

    [Fact]
    public void RunningPastTheEstimateGoesNegativeRatherThanStoppingAtZero()
    {
        // An overrun has to stay measurable - that number becomes the Time Bank debit.
        var remaining = TimerCalculator.RemainingSeconds(600, Anchor, Anchor.AddMinutes(20));

        Assert.Equal(-600, remaining);
    }

    [Fact]
    public void ClockGoingBackwardsNeverInventsTime()
    {
        // NTP correction or a manual clock change must not hand the user free minutes.
        var elapsed = TimerCalculator.ElapsedSeconds(Anchor, Anchor.AddMinutes(-30));

        Assert.Equal(0, elapsed);
    }

    [Theory]
    [InlineData(0, "00:00:00")]
    [InlineData(59, "00:00:59")]
    [InlineData(1534, "00:25:34")]
    [InlineData(3661, "01:01:01")]
    [InlineData(-312, "-00:05:12")]
    public void FormatsAsAClockIncludingOverruns(int seconds, string expected)
    {
        Assert.Equal(expected, TimerCalculator.Format(seconds));
    }

    [Theory]
    [InlineData(1020, "+17m")]
    [InlineData(-3120, "-52m")]
    [InlineData(15120, "+4h 12m")]
    [InlineData(0, "+0m")]
    public void FormatsLedgerDeltasShortAndSigned(int seconds, string expected)
    {
        Assert.Equal(expected, TimerCalculator.FormatDelta(seconds));
    }

    [Theory]
    [InlineData(3600, 3600, 0.0)]   // nothing done
    [InlineData(3600, 1800, 0.5)]   // halfway
    [InlineData(3600, 0, 1.0)]      // exactly on estimate
    [InlineData(3600, -1800, 1.0)]  // overrun clamps, never overflows the track
    public void ProgressStaysWithinTheTrack(int estimated, int remaining, double expected)
    {
        Assert.Equal(expected, TimerCalculator.Progress(estimated, remaining), precision: 3);
    }

    [Fact]
    public void ProgressIsZeroWhenThereIsNoEstimate()
    {
        Assert.Equal(0, TimerCalculator.Progress(0, 0));
    }

    [Fact]
    public void AccruedPauseIsWallClockMinusTimeActuallyWorked()
    {
        // Started an hour ago, worked 20 minutes of it: 40 minutes paused.
        var paused = TimerCalculator.AccruedPauseSeconds(Anchor, 20 * 60, Anchor.AddHours(1));

        Assert.Equal(40 * 60, paused);
    }

    [Fact]
    public void AccruedPauseNeverGoesNegative()
    {
        var paused = TimerCalculator.AccruedPauseSeconds(Anchor, 9999, Anchor.AddMinutes(1));

        Assert.Equal(0, paused);
    }
}
