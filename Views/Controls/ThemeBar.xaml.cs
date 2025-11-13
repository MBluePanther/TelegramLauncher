
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ControlzEx.Theming;

namespace TelegramLauncher.Views.Controls
{
    public partial class ThemeBar : UserControl
    {
        private static readonly string[] Schemes =
        {
            "Blue","Cobalt","Cyan","Emerald","Lime","Magenta","Orange","Purple","Red","Teal"
        };

        public ThemeBar()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            AccentBox.ItemsSource = Schemes;

            try
            {
                var theme = ThemeManager.Current.DetectTheme(Application.Current);
                if (theme != null)
                {
                    BaseToggle.IsOn = string.Equals(theme.BaseColorScheme, "Dark", StringComparison.OrdinalIgnoreCase);
                    var scheme = theme.ColorScheme ?? "Blue";
                    AccentBox.SelectedItem = Schemes.Contains(scheme) ? scheme : Schemes[0];
                }
                else
                {
                    BaseToggle.IsOn = true;
                    AccentBox.SelectedIndex = 0;
                }
            }
            catch
            {
                BaseToggle.IsOn = true;
                AccentBox.SelectedIndex = 0;
            }

            BaseToggle.Toggled += (_, __) => ApplyTheme();
            AccentBox.SelectionChanged += (_, __) => ApplyTheme();
        }

        private void ApplyTheme()
        {
            try
            {
                var baseColor = BaseToggle.IsOn ? "Dark" : "Light";
                var scheme = (string?)AccentBox.SelectedItem ?? "Blue";
                ThemeManager.Current.ChangeThemeBaseColor(Application.Current, baseColor);
                ThemeManager.Current.ChangeThemeColorScheme(Application.Current, scheme);
            }
            catch { /* ignore */ }
        }
    }
}
