using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;


namespace TelegramLauncher.Notifications
{
    public sealed class ToastService
    {
        public static ToastService Instance { get; } = new ToastService();
        private ToastService() { }


        public ObservableCollection<Toast> Toasts { get; } = new();


        public void Show(string message, ToastKind kind = ToastKind.Info, TimeSpan? lifetime = null)
        {
            var toast = new Toast { Message = message, Kind = kind };
            Application.Current.Dispatcher.Invoke(() => Toasts.Add(toast));


            var timeout = lifetime ?? TimeSpan.FromSeconds(3);
            var timer = new DispatcherTimer { Interval = timeout };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                Application.Current.Dispatcher.Invoke(() => Toasts.Remove(toast));
            };
            timer.Start();
        }
    }
}