using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using OsuPlayer.Data.DataModels;
using OsuPlayer.Data.DataModels.Interfaces;

namespace OsuPlayer.Extensions.ValueConverters;

/// <summary>
/// Converts an <see cref="IMapEntryBase" /> to a <see cref="Bitmap" /> by loading the locally stored
/// background image from <see cref="RealmMapEntryBase.BackgroundFileLocation" />.
/// Returns null when no background is available.
/// </summary>
public class MapEntryCoverConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not RealmMapEntryBase entry)
            return null;

        var path = entry.BackgroundFileLocation;

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
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
