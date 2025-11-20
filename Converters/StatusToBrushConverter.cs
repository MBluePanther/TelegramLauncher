using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TelegramLauncher.Converters
{
    public sealed class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value?.ToString() ?? string.Empty;
            status = status.Trim();

            // Default neutral
            Color color = (Color)ColorConverter.ConvertFromString("#455A64");

            switch (status)
            {
                case "Active":
                case "1":
                    color = (Color)ColorConverter.ConvertFromString("#2E7D32"); // green
                    break;

                case "Crash":
                case "2":
                    color = (Color)ColorConverter.ConvertFromString("#FBC02D"); // yellow
                    break;

                case "Spam":
                    color = (Color)ColorConverter.ConvertFromString("#FBC02D"); // yellow
                    break;

                case "Frozen":
                case "3":
                    color = (Color)ColorConverter.ConvertFromString("#C62828"); // red
                    break;
            }

            var brush = new SolidColorBrush(color);
            if (brush.CanFreeze) brush.Freeze();
            return brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}