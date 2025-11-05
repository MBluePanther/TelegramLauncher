using System;
using System.Globalization;
using System.Windows.Data;
using TelegramLauncher.Models;

namespace TelegramLauncher.Converters
{
    public class StatusToEmojiConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not ClientStatus st) return "";
            return st switch
            {
                ClientStatus.Active => "😊",
                ClientStatus.Frozen => "😠",
                ClientStatus.Crash => "💀",
                _ => ""
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }
}
