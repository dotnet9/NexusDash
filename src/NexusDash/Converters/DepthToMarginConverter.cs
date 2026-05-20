using Avalonia;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace NexusDash.Converters
{
    public sealed class DepthToMarginConverter : IValueConverter
    {
        public static readonly DepthToMarginConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var depth = value is int intDepth ? Math.Max(intDepth, 0) : 0;
            return new Thickness(depth * 16, 0, 0, 0);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
