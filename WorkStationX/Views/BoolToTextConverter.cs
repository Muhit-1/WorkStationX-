using System.Globalization;
using System.Windows.Data;

namespace WorkStationX.Views;

/// <summary>
/// Picks one of two strings from a bool. Keeps label-swapping out of code-behind.
/// </summary>
public class BoolToTextConverter : IValueConverter
{
    public string TrueText { get; set; } = string.Empty;

    public string FalseText { get; set; } = string.Empty;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? TrueText : FalseText;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
