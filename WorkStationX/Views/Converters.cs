using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WorkStationX.Views;

/// <summary>true → Visible, false → Collapsed.</summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Null or empty string → Collapsed. Used to hide inline error text.</summary>
public class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null || (value is string s && string.IsNullOrWhiteSpace(s))
            ? Visibility.Collapsed
            : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Count of 0 → Visible. Shows an empty state exactly when a list is empty.</summary>
public class ZeroToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int n && n == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Count greater than 0 → Visible.</summary>
public class NonZeroToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int n && n > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// A 0..1 fraction to a star GridLength, so a progress fill can be a share of its
/// track without needing the track's pixel width. Paired with
/// <see cref="InverseFractionToStarConverter"/> on the remaining column.
/// </summary>
public class FractionToStarConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        new GridLength(value is double d ? Math.Clamp(d, 0, 1) : 0, GridUnitType.Star);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>The leftover share: 1 minus the fraction.</summary>
public class InverseFractionToStarConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        new GridLength(1 - (value is double d ? Math.Clamp(d, 0, 1) : 0), GridUnitType.Star);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>false → Visible. For controls that only apply while something is NOT done.</summary>
public class FalseToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is false ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Contribution-grid shade, 0 (empty) to 4 (busiest).
///
/// The shades are mixed from the theme's lamp colour against the well, so the grid
/// recolours with the rest of the app instead of being hard-coded GitHub green.
/// </summary>
public class LevelToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var level = parameter is not null
            ? int.TryParse(parameter.ToString(), out var p) ? p : 0
            : value is int v ? v : 0;

        var app = Application.Current;

        var empty = app?.TryFindResource("Color.ScaleTrack") is Color e
            ? e
            : Color.FromRgb(20, 24, 22);

        var full = app?.TryFindResource("Color.LampOn") is Color f
            ? f
            : Color.FromRgb(99, 220, 192);

        if (level <= 0)
        {
            return new SolidColorBrush(empty);
        }

        // Even level 1 needs to be clearly "something", so the ramp starts at 0.3.
        var t = 0.3 + 0.7 * (Math.Clamp(level, 1, 4) - 1) / 3.0;

        return new SolidColorBrush(Color.FromRgb(
            (byte)(empty.R + (full.R - empty.R) * t),
            (byte)(empty.G + (full.G - empty.G) * t),
            (byte)(empty.B + (full.B - empty.B) * t)));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Dims a finished row without hiding it.</summary>
public class DoneToOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? 0.45 : 1.0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
