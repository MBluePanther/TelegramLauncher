using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using TelegramLauncher.Models;

namespace TelegramLauncher.Converters
{
    public class StatusToBrushConverter : IValueConverter
    {
        private static SolidColorBrush B(byte r, byte g, byte b) =>
            new SolidColorBrush(Color.FromRgb(r, g, b));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var def = B(23, 33, 43); // тёмно-синий фон по умолчанию
            if (value is not ClientStatus st) return def;

            return st switch
            {
                ClientStatus.Active => B(46, 204, 113), // зелёный
                ClientStatus.Frozen => B(243, 156, 18), // оранжевый
                ClientStatus.Crash => B(231, 76, 60),  // красный
                _ => def
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }
}
