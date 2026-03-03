using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using PhotoMapperAI.UI.Models;

namespace PhotoMapperAI.UI.Converters;

/// <summary>
/// Converts a BatchTeamStatus to a background color for the DataGrid.
/// </summary>
public class BatchStatusToColorConverter : IValueConverter
{
    public static readonly BatchStatusToColorConverter Instance = new();

    private static readonly SolidColorBrush PendingBrush = new(Color.Parse("#E3F2FD")); // Light blue
    private static readonly SolidColorBrush ExtractingBrush = new(Color.Parse("#E3F2FD")); // Light blue
    private static readonly SolidColorBrush MappingBrush = new(Color.Parse("#FFF3E0")); // Light orange
    private static readonly SolidColorBrush GeneratingBrush = new(Color.Parse("#F3E5F5")); // Light purple
    private static readonly SolidColorBrush CompletedBrush = new(Color.Parse("#E8F5E9")); // Light green
    private static readonly SolidColorBrush FailedBrush = new(Color.Parse("#FFEBEE")); // Light red
    private static readonly SolidColorBrush SkippedBrush = new(Color.Parse("#FAFAFA")); // Light gray

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is BatchTeamStatus status)
        {
            return status switch
            {
                BatchTeamStatus.Pending => PendingBrush,
                BatchTeamStatus.Extracting => ExtractingBrush,
                BatchTeamStatus.Mapping => MappingBrush,
                BatchTeamStatus.Generating => GeneratingBrush,
                BatchTeamStatus.Completed => CompletedBrush,
                BatchTeamStatus.Failed => FailedBrush,
                BatchTeamStatus.Skipped => SkippedBrush,
                _ => PendingBrush
            };
        }
        return PendingBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
