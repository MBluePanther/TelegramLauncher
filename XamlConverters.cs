using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using TelegramLauncher.Models;

namespace TelegramLauncher
{
    public sealed class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var status = value is ClientStatus cs ? cs : ClientStatus.Active;
            Color c = status switch
            {
                ClientStatus.Active => Color.FromRgb(0x43, 0xA0, 0x47),
                ClientStatus.Frozen => Color.FromRgb(0xFF, 0xC1, 0x07),
                ClientStatus.Crash  => Color.FromRgb(0xE5, 0x39, 0x35),
                _ => Color.FromRgb(0x90, 0xA4, 0xAE),
            };
            return new SolidColorBrush(c);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) 
            => throw new NotSupportedException();
    }

    public sealed class StatusToEmojiConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var status = value is ClientStatus cs ? cs : ClientStatus.Active;
            return status switch
            {
                ClientStatus.Active => "✔",
                ClientStatus.Frozen => "⏸",
                ClientStatus.Crash  => "⚠",
                _ => "•"
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) 
            => throw new NotSupportedException();
    }
}
