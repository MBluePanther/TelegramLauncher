
using System;
using System.Globalization;
using System.Windows.Data;
using TelegramLauncher.Models;

namespace TelegramLauncher.Converters
{
    public sealed class StatusToEmojiConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ClientStatus st)
            {
                return st switch
                {
                    ClientStatus.Active => "🟢",
                    ClientStatus.Frozen => "🟡",
                    ClientStatus.Crash  => "🔴",
                    _ => "⚪"
                };
            }
            return "⚪";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
