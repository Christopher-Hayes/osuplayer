using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace OsuPlayer.Extensions.ValueConverters;

/// <summary>
/// Converts a local file path <see cref="string"/> to a <see cref="Bitmap"/>.
/// Returns <c>null</c> when the path is null/empty or the file does not exist.
/// </summary>
public class FilePathToBitmapConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        try
        {
            return new Bitmap(path);
        }
        catch
        {
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
