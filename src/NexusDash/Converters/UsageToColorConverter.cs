using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace NexusDash.Converters
{
    public class UsageToColorConverter : IValueConverter
    {
        public static readonly UsageToColorConverter Instance = new UsageToColorConverter();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double usage)
            {
                if (usage < 50)
                    return new SolidColorBrush(Color.Parse("#4ec9b0"));
                else if (usage < 70)
                    return new SolidColorBrush(Color.Parse("#007acc"));
                else if (usage < 85)
                    return new SolidColorBrush(Color.Parse("#d7ba7d"));
                else if (usage < 95)
                    return new SolidColorBrush(Color.Parse("#ff9500"));
                else
                    return new SolidColorBrush(Color.Parse("#ff4757"));
            }
            return new SolidColorBrush(Color.Parse("#4ec9b0"));
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}