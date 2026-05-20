using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace NexusDash.Converters
{
    public class BytesToTextConverter : IValueConverter
    {
        public static readonly BytesToTextConverter Instance = new BytesToTextConverter();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ulong bytes)
            {
                string[] sizes = { "B", "KB", "MB", "GB", "TB" };
                double len = bytes;
                int order = 0;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len /= 1024;
                }
                return $"{len:F2} {sizes[order]}";
            }
            return "0 B";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}