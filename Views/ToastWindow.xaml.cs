using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using TelegramLauncher.Notifications;

namespace TelegramLauncher.Views
{
    public partial class ToastWindow : Window
    {
        public string Message { get; set; } = string.Empty;
        public ToastKind Kind { get; set; } = ToastKind.Info;

        private readonly TimeSpan _anim = TimeSpan.FromMilliseconds(150);

        public ToastWindow()
        {
            InitializeComponent();
            Loaded += ToastWindow_Loaded;
            MouseLeftButtonUp += (_, __) => BeginClose(); // клик — закрыть
        }

        private void ToastWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Цвет рамки по типу
            switch (Kind)
            {
                case ToastKind.Success: Card.BorderBrush = (Brush)new BrushConverter().ConvertFrom("#4CAF50"); break;
                case ToastKind.Warning: Card.BorderBrush = (Brush)new BrushConverter().ConvertFrom("#FFC107"); break;
                case ToastKind.Error: Card.BorderBrush = (Brush)new BrushConverter().ConvertFrom("#F44336"); break;
            }

            // Анимация появления (легкий слайд вверх)
            var sb = new Storyboard();

            var fade = new DoubleAnimation(0, 1, _anim);
            Storyboard.SetTarget(fade, this);
            Storyboard.SetTargetProperty(fade, new PropertyPath(Window.OpacityProperty));
            sb.Children.Add(fade);

            // Трансформа на Border 'Card', а не на Window (Window не поддерживает RenderTransform)
            var move = new DoubleAnimation(20, 0, _anim);
            Storyboard.SetTarget(move, Card);
            Storyboard.SetTargetProperty(move, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
            sb.Children.Add(move);

            this.Opacity = 0;
            sb.Begin(this);
        }

        public void BeginClose()
        {
            var sb = new Storyboard();

            var fade = new DoubleAnimation(1, 0, _anim);
            Storyboard.SetTarget(fade, this);
            Storyboard.SetTargetProperty(fade, new PropertyPath(Window.OpacityProperty));
            sb.Children.Add(fade);

            var move = new DoubleAnimation(0, 20, _anim);
            Storyboard.SetTarget(move, Card);
            Storyboard.SetTargetProperty(move, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
            sb.Children.Add(move);

            sb.Completed += (_, __) => this.Close();
            sb.Begin(this);
        }
    }
}