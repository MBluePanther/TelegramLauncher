
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using TelegramLauncher.Models;

namespace TelegramLauncher.Services
{
    /// <summary>
    /// Reliable tracker for launched client processes. Keeps strong references
    /// so Exited always fires; shows status dialog on real exit.
    /// </summary>
    public static class StatusOnExitService
    {
        private static readonly ConcurrentDictionary<int, Tracked> _tracked = new();
        private static bool _killAll;

        /// <summary>Global switch controlled from the toggle in MainWindow.</summary>
        public static bool Enabled { get; set; } = false;

        /// <summary>Ignore processes that exit immediately (spawners). Milliseconds.</summary>
        public static int MinLifetimeMs { get; set; } = 1500;

        private sealed record Tracked(Process Proc, ClientConfig Client, DateTime StartUtc);

        /// <summary>
        /// Call right after Process.Start for a client launched by the app.
        /// </summary>
        public static void Track(Process? process, ClientConfig? client, Dispatcher dispatcher)
        {
            if (process == null || client == null) return;

            try
            {
                process.EnableRaisingEvents = true;
                var startUtc = DateTime.UtcNow;
                _tracked[process.Id] = new Tracked(process, client, startUtc);

                process.Exited += async (s, e) =>
                {
                    if (_killAll) { _tracked.TryRemove(process.Id, out _); return; }
                    if (!Enabled) { _tracked.TryRemove(process.Id, out _); return; }

                    // debounce: ignore immediate spawner exits
                    var lived = (int)(DateTime.UtcNow - startUtc).TotalMilliseconds;
                    if (lived < MinLifetimeMs)
                    {
                        _tracked.TryRemove(process.Id, out _);
                        return;
                    }

                    _tracked.TryRemove(process.Id, out var info);
                    if (info == null) return;

                    await dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            var dlg = new TelegramLauncher.Views.ClientStatusDialog(info.Client);
                            var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                                        ?? Application.Current?.Windows.OfType<Window>().FirstOrDefault();
                            if (owner != null) dlg.Owner = owner;
                            if (dlg.ShowDialog() == true && dlg.SelectedStatus.HasValue)
                            {
                                var status = dlg.SelectedStatus.Value;
                                info.Client.Status = status;
                                TryPersistStatus(info.Client, status);
                            }
                        }
                        catch
                        {
                            // swallow UI errors
                        }
                    }, DispatcherPriority.Normal);
                };
            }
            catch
            {
                // ignore tracking errors
            }
        }

        public static IDisposable SuppressKillAll()
        {
            _killAll = true;
            return new Scope(() => _killAll = false);
        }

        private sealed class Scope : IDisposable
        {
            private readonly Action _onDispose;
            public Scope(Action onDispose) => _onDispose = onDispose;
            public void Dispose() => _onDispose?.Invoke();
        }

        private static void TryPersistStatus(ClientConfig client, ClientStatus status)
        {
            try
            {
                // update persistent storage if your app has a central store service;
                // otherwise leave as in-memory only (Sorter/Launcher reads from memory anyway).
                // If you already have ClientStore.Save(list) – better call it there.
                // This stub intentionally does not throw.
            }
            catch { }
        }
    }
}
