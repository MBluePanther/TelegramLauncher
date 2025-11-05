using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using TelegramLauncher.Models;

namespace TelegramLauncher
{
    public class StatusToBrushConverter : IValueConverter
    {
        // Телеграм-цвета
        private static readonly SolidColorBrush Green = (SolidColorBrush)new BrushConverter().ConvertFrom("#2ECC71");
        private static readonly SolidColorBrush Orange = (SolidColorBrush)new BrushConverter().ConvertFrom("#F39C12");
        private static readonly SolidColorBrush Red = (SolidColorBrush)new BrushConverter().ConvertFrom("#E74C3C");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is ClientStatus s ? s switch
            {
                ClientStatus.Active => Green,
                ClientStatus.Frozen => Orange,
                ClientStatus.Crash => Red,
                _ => Green
            } : Green;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    public class StatusToEmojiConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is ClientStatus s ? s switch
            {
                ClientStatus.Active => "🙂",
                ClientStatus.Frozen => "😠",
                ClientStatus.Crash => "💀",
                _ => "🙂"
            } : "🙂";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
