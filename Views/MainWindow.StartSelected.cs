// Views/MainWindow.StartSelected.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.Controls.Dialogs;      // ShowMessageAsync
using TelegramLauncher.Models;
using TelegramLauncher.Services;

namespace TelegramLauncher
{
    /// <summary>
    /// Расширение MainWindow: "Запустить выбранные" + включение/выключение массовых кнопок.
    /// Не требует конкретного имени коллекции в ClientStore — находит её сам (Items/All/Clients/GetAll()).
    /// </summary>
    public partial class MainWindow
    {
        // --- Универсальный доступ к списку клиентов через ClientStore ---
        private static IEnumerable<ClientConfig> EnumerateAllClients()
        {
            // Ищем статическое свойство в ClientStore: Items / All / Clients
            var t = typeof(ClientStore);
            var prop =
                t.GetProperty("Items", BindingFlags.Public | BindingFlags.Static) ??
                t.GetProperty("All", BindingFlags.Public | BindingFlags.Static) ??
                t.GetProperty("Clients", BindingFlags.Public | BindingFlags.Static);

            if (prop != null)
            {
                var val = prop.GetValue(null);
                if (val is IEnumerable seq)
                    return seq.Cast<object>().OfType<ClientConfig>();
            }

            // Пытаемся найти статический метод GetAll()
            var method = t.GetMethod("GetAll", BindingFlags.Public | BindingFlags.Static, Type.DefaultBinder, Type.EmptyTypes, null);
            if (method != null)
            {
                var val = method.Invoke(null, null);
                if (val is IEnumerable seq)
                    return seq.Cast<object>().OfType<ClientConfig>();
            }

            // Фолбэк: пусто
            return Enumerable.Empty<ClientConfig>();
        }

        private static string GetClientLabel(ClientConfig c)
        {
            if (c == null) return "client";

            // Пытаемся вытащить DisplayName/Title/Name, если такие пропы есть
            var t = c.GetType();
            string byProp =
                t.GetProperty("DisplayName")?.GetValue(c) as string ??
                t.GetProperty("Title")?.GetValue(c) as string ??
                t.GetProperty("Name")?.GetValue(c) as string;

            if (!string.IsNullOrWhiteSpace(byProp))
                return byProp;

            // Фолбэк — имя exe без расширения
            try
            {
                if (!string.IsNullOrWhiteSpace(c.ExePath))
                    return Path.GetFileNameWithoutExtension(c.ExePath);
            }
            catch { /* игнор */ }

            return "client";
        }


        // --- Включение/выключение кнопок массовых действий ---
        private void UpdateBulkActionButtons()
        {
            var any = EnumerateAllClients().Any(x => x.IsSelected);

            if (FindName("DeleteSelectedButton") is Button delBtn)
                delBtn.IsEnabled = any;

            if (FindName("StartSelectedButton") is Button startBtn)
                startBtn.IsEnabled = any;
        }

        // Повесь это на Click у чекбокса выбора клиента (если ещё не повешено)
        private void ClientSelectCheckBox_Click(object sender, RoutedEventArgs e)
        {
            UpdateBulkActionButtons();
        }



        // --- Кнопка "Запустить выбранные" ---
        private async void StartSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = EnumerateAllClients().Where(c => c.IsSelected).ToList();
            if (selected.Count == 0)
            {
                await this.ShowMessageAsync("Запуск", "Не выбраны клиенты для запуска.");
                return;
            }

            int started = 0, skipped = 0, errors = 0;

            foreach (var client in selected)
            {
                // Блокируем запуск при статусе "Слёт"
                if (client.Status == ClientStatus.Crash)
                {
                    skipped++;
                    continue;
                }

                try
                {
                    await StartClientAsync(client);
                    started++;
                }
                catch (Exception ex)
                {
                    errors++;
                    Debug.WriteLine($"Ошибка запуска {GetClientLabel(client)}: {ex}");

                }
            }

            var msg =
                $"Успешно запущено: {started}\n" +
                $"Пропущено (статус «Слёт»): {skipped}\n" +
                $"Ошибок: {errors}";
            await this.ShowMessageAsync("Запуск выбранных", msg);

            UpdateBulkActionButtons();
        }

        // --- Запуск одного клиента ---
        private Task StartClientAsync(ClientConfig c)
        {
            if (c == null)
                throw new ArgumentNullException(nameof(c));

            if (string.IsNullOrWhiteSpace(c.ExePath) || !File.Exists(c.ExePath))
                throw new FileNotFoundException("Файл .exe не найден.", c?.ExePath ?? "<null>");

            var psi = new ProcessStartInfo
            {
                FileName = c.ExePath,
                WorkingDirectory = Path.GetDirectoryName(c.ExePath),
                UseShellExecute = true
            };

            Process.Start(psi);
            return Task.CompletedTask;
        }
    }
}
