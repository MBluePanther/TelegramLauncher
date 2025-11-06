using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using TelegramLauncher.Views;

namespace TelegramLauncher.Notifications
{
    public static class ToastManager
    {
        private static readonly List<ToastWindow> _open = new();
        private static bool _ownerHooked = false;

        private const double MarginX = 16;
        private const double MarginY = 16;
        private const double Spacing = 8;

        public static void Show(string message, ToastKind kind = ToastKind.Info, TimeSpan? lifetime = null)
        {
            var owner = Application.Current?.MainWindow;
            if (owner == null) return;

            HookOwner(owner);

            var tw = new ToastWindow
            {
                Owner = owner,
                Message = message,
                Kind = kind
            };

            tw.Loaded += (_, __) => Reposition(owner);
            tw.Closed += (_, __) =>
            {
                _open.Remove(tw);
                Reposition(owner);
            };

            _open.Add(tw);
            tw.Show();

            var t = new DispatcherTimer { Interval = lifetime ?? TimeSpan.FromSeconds(6) };
            t.Tick += (s, e) =>
            {
                (s as DispatcherTimer)?.Stop();
                if (tw.IsVisible) tw.BeginClose();
            };
            t.Start();

            Reposition(owner);
        }

        private static void HookOwner(Window owner)
        {
            if (_ownerHooked) return;
            _ownerHooked = true;
            owner.LocationChanged += (_, __) => Reposition(owner);
            owner.SizeChanged += (_, __) => Reposition(owner);
            owner.StateChanged += (_, __) => Reposition(owner);
            owner.ContentRendered += (_, __) => Reposition(owner);
            owner.DpiChanged += (_, __) => Reposition(owner);
        }

        private static void Reposition(Window owner)
        {
            if (_open.Count == 0) return;

            var root = owner.Content as FrameworkElement;
            if (root == null)
            {
                PositionByWindowBounds(owner);
                return;
            }

            root.UpdateLayout();
            foreach (var tw in _open) tw.UpdateLayout();

            // bottom-right of client area in device pixels
            Point brDev = root.PointToScreen(new Point(root.ActualWidth, root.ActualHeight));

            // convert to DIPs (WPF units) to match Window.Top/Left
            var source = PresentationSource.FromVisual(owner);
            if (source?.CompositionTarget is null)
            {
                PositionByWindowBounds(owner);
                return;
            }
            Matrix fromDevice = source.CompositionTarget.TransformFromDevice;
            Point br = fromDevice.Transform(brDev);

            double right = br.X - MarginX;
            double bottom = br.Y - MarginY;

            double currentBottom = bottom;
            foreach (var tw in _open)
            {
                // ensure measure
                if (double.IsNaN(tw.Width) || double.IsNaN(tw.Height))
                {
                    tw.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                }
                var w = tw.ActualWidth > 0 ? tw.ActualWidth : (tw.Width > 0 ? tw.Width : tw.DesiredSize.Width);
                var h = tw.ActualHeight > 0 ? tw.ActualHeight : (tw.Height > 0 ? tw.Height : tw.DesiredSize.Height);

                tw.Left = Math.Round(right - w);
                tw.Top = Math.Round(currentBottom - h);

                currentBottom = tw.Top - Spacing;
            }
        }

        private static void PositionByWindowBounds(Window owner)
        {
            foreach (var tw in _open) tw.UpdateLayout();

            double right = owner.Left + owner.Width - MarginX;
            double bottom = owner.Top + owner.Height - MarginY;

            double currentBottom = bottom;
            foreach (var tw in _open)
            {
                var w = tw.ActualWidth > 0 ? tw.ActualWidth : (tw.Width > 0 ? tw.Width : 300);
                var h = tw.ActualHeight > 0 ? tw.ActualHeight : (tw.Height > 0 ? tw.Height : 60);

                tw.Left = Math.Round(right - w);
                tw.Top = Math.Round(currentBottom - h);

                currentBottom = tw.Top - Spacing;
            }
        }
    }
}
