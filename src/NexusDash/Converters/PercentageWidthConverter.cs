using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace NexusDash.Converters
{
    public class PercentageWidthConverter : IValueConverter
    {
        public static readonly PercentageWidthConverter Instance = new PercentageWidthConverter();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double percentage && percentage >= 0 && percentage <= 100)
            {
                return percentage;
            }
            return 0.0;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
