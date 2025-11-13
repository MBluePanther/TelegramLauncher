
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TelegramLauncher.Models;

namespace TelegramLauncher
{
    public partial class MainWindow
    {
        private void MenuSorter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var w = new TelegramLauncher.Views.SorterWindow { Owner = this };
                w.Show();
            }
            catch (Exception ex)
            {
                _ = this.ShowMessageAsync("Ошибка", ex.Message, MessageDialogStyle.Affirmative);
            }
            finally
            {
                try { MainMenuFlyout.IsOpen = false; } catch { }
            }
        }

        private async void OpenSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = (Clients as IEnumerable<ClientConfig>)?.Where(c => c.IsSelected).ToList() ?? new List<ClientConfig>();
            if (selected.Count == 0)
            {
                await this.ShowMessageAsync("Нет выбранных", "Поставь галочки у нужных клиентов.", MessageDialogStyle.Affirmative);
                return;
            }

            int started = 0, skipped = 0;
            foreach (var cfg in selected)
            {
                if (cfg.Status == ClientStatus.Crash) { skipped++; continue; }
                if (string.IsNullOrWhiteSpace(cfg.ExePath) || !File.Exists(cfg.ExePath)) { skipped++; continue; }

                try
                {
                    var proc = Process.Start(new ProcessStartInfo
                    {
                        FileName = cfg.ExePath,
                        Arguments = cfg.Arguments ?? "",
                        WorkingDirectory = Path.GetDirectoryName(cfg.ExePath)!,
                        UseShellExecute = true
                    });
                    if (proc != null)
                    {
                        TelegramLauncher.Services.StatusOnExitService.Track(proc, cfg, this.Dispatcher);
                        cfg.LastOpenedUtc = DateTime.UtcNow;
                        started++;
                    }
                    else skipped++;
                }
                catch { skipped++; }
            }

            await this.ShowMessageAsync("Запуск завершён",
                $"Выбрано: {selected.Count}\nЗапущено: {started}\nПропущено: {skipped}",
                MessageDialogStyle.Affirmative);
        }
    }
}
