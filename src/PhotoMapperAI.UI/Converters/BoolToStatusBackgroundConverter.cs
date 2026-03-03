using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PhotoMapperAI.UI.Converters;

/// <summary>
/// Converts a boolean to a status background color.
/// True = Green (valid mapping), False = Orange (no mapping).
/// </summary>
public class BoolToStatusBackgroundConverter : IValueConverter
{
    public static readonly BoolToStatusBackgroundConverter Instance = new();

    private static readonly SolidColorBrush ValidMappingBrush = new(Color.Parse("#E8F5E9")); // Light green
    private static readonly SolidColorBrush NoMappingBrush = new(Color.Parse("#FFF3E0")); // Light orange

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool hasValidMapping)
        {
            return hasValidMapping ? ValidMappingBrush : NoMappingBrush;
        }
        return NoMappingBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
