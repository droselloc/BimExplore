using System.Globalization;
using System.Windows.Data;

namespace BimExplorer.App.Converters;

public class FileSizeConverter : IValueConverter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not long bytes || bytes == 0)
            return "0 B";

        var order = (int)Math.Floor(Math.Log(bytes, 1024));
        order = Math.Min(order, Units.Length - 1);
        var size = bytes / Math.Pow(1024, order);
        return $"{size:0.##} {Units[order]}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
