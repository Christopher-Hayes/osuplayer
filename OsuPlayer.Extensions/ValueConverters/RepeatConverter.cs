using System.Globalization;
using Avalonia.Data.Converters;
using Material.Icons;
using OsuPlayer.Data.OsuPlayer.Enums;

namespace OsuPlayer.Extensions.ValueConverters;

public class RepeatConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not RepeatMode val) return MaterialIconKind.QuestionMark;

        return val switch
        {
            RepeatMode.NoRepeat => MaterialIconKind.RepeatOff,
            RepeatMode.RepeatAll => MaterialIconKind.Repeat,
            RepeatMode.RepeatOne => MaterialIconKind.RepeatOnce,
            _ => MaterialIconKind.RepeatOff
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}