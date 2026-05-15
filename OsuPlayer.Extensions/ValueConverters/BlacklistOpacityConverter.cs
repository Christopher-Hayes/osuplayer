using System.Globalization;
using Avalonia.Data.Converters;

namespace OsuPlayer.Extensions.ValueConverters;

public class BlacklistOpacityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool isBlacklisted)
            return 1.0;

        return isBlacklisted ? 0.5 : 1.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
