using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PhotoMapperAI.UI.Converters;

/// <summary>
/// Converts a boolean to a text brush.
/// True = ErrorBrush, False = SecondaryTextBrush.
/// </summary>
public class BoolToTextBrushConverter : IValueConverter
{
    public static readonly BoolToTextBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var useError = value is bool flag && flag;
        var error = TryGetResource("ErrorBrush");
        var secondary = TryGetResource("SecondaryTextBrush");
        return useError ? error ?? Brushes.Red : secondary ?? Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static IBrush? TryGetResource(string key)
    {
        if (Avalonia.Application.Current?.Resources.TryGetResource(key, null, out var value) == true)
        {
            return value as IBrush;
        }

        return null;
    }
}