using System;
using System.Globalization;
using System.Windows.Data;

namespace TelegramLauncher
{
    /// <summary>
    /// Returns (availableWidth - padding) / columns, clamped to [160, 10000].
    /// </summary>
    public sealed class ItemWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                double available = 0;
                int cols = 1;
                if (values.Length > 0 && values[0] is double d) available = d;
                if (values.Length > 1 && values[1] is int c) cols = Math.Max(1, c);
                double padding = 40; // margins + scrollbar reserve
                var width = (available - padding) / cols;
                if (double.IsNaN(width) || double.IsInfinity(width)) width = 220;
                width = Math.Max(160, Math.Min(10000, width));
                return width;
            }
            catch
            {
                return 220d;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}