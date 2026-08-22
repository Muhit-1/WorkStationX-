namespace WorkStationX.Services;

/// <summary>
/// All countdown maths, kept pure so it can be tested without a clock or a database.
///
/// The durability rule: a Stopwatch drives the on-screen tick because it does not
/// drift, but nothing is ever *stored* from it. What gets written to disk is a UTC
/// anchor plus the remaining seconds, so closing the app, or a crash, cannot lose or
/// invent time.
/// </summary>
public static class TimerCalculator
{
    /// <summary>
    /// Live remaining for a task. <paramref name="runningSince"/> is null when paused,
    /// in which case the stored value is already correct.
    /// </summary>
    public static int RemainingSeconds(int storedRemaining, DateTime? runningSince, DateTime nowUtc)
    {
        if (runningSince is null)
        {
            return storedRemaining;
        }

        var elapsed = (int)Math.Floor((nowUtc - runningSince.Value).TotalSeconds);
        return storedRemaining - Math.Max(0, elapsed);
    }

    /// <summary>Seconds actually worked in a stretch. Never negative, even if the clock moves.</summary>
    public static int ElapsedSeconds(DateTime startedUtc, DateTime endedUtc) =>
        Math.Max(0, (int)Math.Floor((endedUtc - startedUtc).TotalSeconds));

    /// <summary>
    /// "01:25:34". Negative values render as "-00:05:12" so an overrun still reads
    /// as a duration rather than turning into nonsense.
    /// </summary>
    public static string Format(int totalSeconds)
    {
        var negative = totalSeconds < 0;
        var s = Math.Abs(totalSeconds);

        var hours = s / 3600;
        var minutes = s % 3600 / 60;
        var seconds = s % 60;

        return $"{(negative ? "-" : string.Empty)}{hours:D2}:{minutes:D2}:{seconds:D2}";
    }

    /// <summary>Short human form for ledger rows: "+17m", "-52m", "+4h 12m".</summary>
    public static string FormatDelta(int totalSeconds)
    {
        var sign = totalSeconds < 0 ? "-" : "+";
        var s = Math.Abs(totalSeconds);

        var hours = s / 3600;
        var minutes = s % 3600 / 60;

        return hours > 0 ? $"{sign}{hours}h {minutes:D2}m" : $"{sign}{minutes}m";
    }

    /// <summary>
    /// How far through the estimate, clamped to 0..1 so the bar never overflows its
    /// track once a task runs long.
    /// </summary>
    public static double Progress(int estimatedSeconds, int remainingSeconds)
    {
        if (estimatedSeconds <= 0)
        {
            return 0;
        }

        var done = estimatedSeconds - remainingSeconds;
        return Math.Clamp((double)done / estimatedSeconds, 0, 1);
    }

    /// <summary>
    /// Total time the task sat paused: wall-clock span since it first started, minus
    /// the time actually worked. This is what the ACCRUED PAUSE plate reports.
    /// </summary>
    public static int AccruedPauseSeconds(
        DateTime firstStartUtc, int workedSeconds, DateTime nowUtc) =>
        Math.Max(0, (int)Math.Floor((nowUtc - firstStartUtc).TotalSeconds) - workedSeconds);
}
