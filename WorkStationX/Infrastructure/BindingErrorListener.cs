using System.Diagnostics;
using System.Windows;
using Serilog;

namespace WorkStationX.Infrastructure;

/// <summary>
/// Routes WPF's binding failures into the log.
///
/// A broken binding does not throw and does not appear anywhere at runtime - the
/// control simply renders blank. Without this, a typo in a binding path ships and
/// only shows up as "why is that panel empty?". In DEBUG builds it also raises the
/// failure as a visible exception so it cannot be ignored.
/// </summary>
public sealed class BindingErrorListener : TraceListener
{
    private BindingErrorListener()
    {
    }

    public static void Attach()
    {
        PresentationTraceSources.Refresh();

        var source = PresentationTraceSources.DataBindingSource;
        source.Listeners.Add(new BindingErrorListener());
        source.Switch.Level = SourceLevels.Warning;
    }

    public override void Write(string? message)
    {
    }

    public override void WriteLine(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Log.Warning("XAML binding: {Message}", message);
    }
}
