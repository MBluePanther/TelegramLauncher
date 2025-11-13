
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using TelegramLauncher.Models;

namespace TelegramLauncher.Converters
{
    public sealed class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            SolidColorBrush brush = Brushes.Gray;
            if (value is ClientStatus st)
            {
                switch (st)
                {
                    case ClientStatus.Active:  brush = new SolidColorBrush(Color.FromRgb(76, 175, 80)); break;  // green
                    case ClientStatus.Frozen:  brush = new SolidColorBrush(Color.FromRgb(255, 193, 7)); break;  // amber
                    case ClientStatus.Crash:   brush = new SolidColorBrush(Color.FromRgb(244, 67, 54)); break; // red
                }
            }
            if (brush.CanFreeze) brush.Freeze();
            return brush;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
