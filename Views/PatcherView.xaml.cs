using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using WF = System.Windows.Forms;

namespace TelegramLauncher.Views
{
    public partial class PatcherView : UserControl
    {
        private class FoundExe
        {
            public string Name { get; set; } = "";
            public string FullPath { get; set; } = "";
            public bool IsSelected { get; set; }
            public string SelectionMark => IsSelected ? "✔" : "";
        }

        private readonly ObservableCollection<FoundExe> _easyItems = new();
        private readonly ObservableCollection<FoundExe> _proItems = new();

        private CancellationTokenSource? _scanCts;
        private CancellationTokenSource? _patchCts;

        private MetroWindow Owner => (MetroWindow)Window.GetWindow(this);

        public PatcherView()
        {
            InitializeComponent();

            ExeList.ItemsSource = _easyItems;
            ProExeList.ItemsSource = _proItems;

            // двойной клик = отметить
            ExeList.MouseDoubleClick += (_, __) => ToggleSelection(ExeList);
            ProExeList.MouseDoubleClick += (_, __) => ToggleSelection(ProExeList);

            // подпишем смену вкладок, чтобы актуализировать счётчик
            ModeTabs.SelectionChanged += (_, __) => UpdateFoundCount();

            // показать путь по умолчанию к источнику Kibitkogram.exe
            var defSrc = DefaultSourceExe();
            DefaultSourceExeText.Text = File.Exists(defSrc) ? defSrc : $"{defSrc} (не найден)";
            ProSourceExeBox.Text = defSrc;
        }

        // ====== Helpers ======

        private void ToggleSelection(ListView lv)
        {
            if (lv.SelectedItem is FoundExe it)
            {
                it.IsSelected = !it.IsSelected;
                // хакнуть перерисовку
                var idx = lv.SelectedIndex;
                if (lv.ItemsSource is ObservableCollection<FoundExe> coll)
                {
                    coll[idx] = it;
                    UpdateFoundCount();
                }
            }
        }

        private string DefaultSourceExe() =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Kibitkogram.exe");

        private static string? PickFolder(string? startPath = null)
        {
            using var dlg = new WF.FolderBrowserDialog();
            if (!string.IsNullOrWhiteSpace(startPath) && Directory.Exists(startPath))
                dlg.SelectedPath = startPath;
            dlg.ShowNewFolderButton = false;
            var res = dlg.ShowDialog();
            return res == WF.DialogResult.OK ? dlg.SelectedPath : null;
        }

        private static string? PickFile(string filter = "Executable|*.exe")
        {
            using var dlg = new WF.OpenFileDialog();
            dlg.Filter = filter;
            dlg.CheckFileExists = true;
            return dlg.ShowDialog() == WF.DialogResult.OK ? dlg.FileName : null;
        }

        private static IEnumerable<string> SafeEnumerateFiles(string root, IReadOnlyCollection<string> names, CancellationToken ct)
        {
            // ручной стек (нет рекурсии) — быстрее и безопаснее
            var stack = new Stack<string>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                ct.ThrowIfCancellationRequested();

                var cur = stack.Pop();

                string[] files = Array.Empty<string>();
                string[] dirs = Array.Empty<string>();

                try { files = Directory.GetFiles(cur, "*.exe"); } catch { }
                try { dirs = Directory.GetDirectories(cur); } catch { }

                foreach (var f in files)
                {
                    var fn = Path.GetFileName(f);
                    if (names.Count == 0 || names.Contains(fn, StringComparer.OrdinalIgnoreCase))
                        yield return f;
                }

                foreach (var d in dirs)
                    stack.Push(d);
            }
        }

        private void UpdateFoundCount()
        {
            FoundCountText.Text = (ModeTabs.SelectedIndex == 0 ? _easyItems.Count : _proItems.Count).ToString();
        }

        // ====== Easy mode ======

        private void PickEasyRoot_Click(object sender, RoutedEventArgs e)
        {
            var p = PickFolder(EasyRootBox.Text);
            if (p != null) EasyRootBox.Text = p;
        }

        private async void ScanEasy_Click(object sender, RoutedEventArgs e)
        {
            var root = EasyRootBox.Text;
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                await Owner.ShowMessageAsync("Сканирование", "Укажи корректную папку для сканирования.");
                return;
            }

            var source = DefaultSourceExe();
            if (!File.Exists(source))
            {
                await Owner.ShowMessageAsync("Источник не найден",
                    $"В папке приложения отсутствует Kibitkogram.exe:\n{source}");
                return;
            }

            ScanEasyBtn.IsEnabled = false;
            CancelScanBtn.IsEnabled = true;
            ScanProgress.Visibility = Visibility.Visible;
            ScanProgress.IsIndeterminate = true;

            _easyItems.Clear();
            UpdateFoundCount();

            // имена, которые считаем целями
            var wanted = new[] { "Telegram.exe", "Tdesktop.exe", "Kibitkogram.exe" };

            _scanCts = new CancellationTokenSource();
            var token = _scanCts.Token;

            try
            {
                var list = await Task.Run(() =>
                {
                    var found = new List<FoundExe>();
                    foreach (var f in SafeEnumerateFiles(root, wanted, token))
                    {
                        token.ThrowIfCancellationRequested();
                        found.Add(new FoundExe { Name = Path.GetFileName(f), FullPath = f });
                    }
                    return found;
                }, token);

                foreach (var it in list) _easyItems.Add(it);
                UpdateFoundCount();
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            finally
            {
                _scanCts?.Dispose();
                _scanCts = null;

                ScanProgress.Visibility = Visibility.Collapsed;
                ScanProgress.IsIndeterminate = false;
                ScanEasyBtn.IsEnabled = true;
                CancelScanBtn.IsEnabled = false;
            }
        }

        private void CancelScan_Click(object sender, RoutedEventArgs e)
        {
            _scanCts?.Cancel();
        }

        // ====== Pro mode ======

        private void PickProSource_Click(object sender, RoutedEventArgs e)
        {
            var f = PickFile();
            if (f != null) ProSourceExeBox.Text = f;
        }

        // ====== Replace actions (both modes) ======

        private async void ReplaceSelected_Click(object sender, RoutedEventArgs e)
        {
            var list = ModeTabs.SelectedIndex == 0 ? _easyItems : _proItems;
            var selected = list.Where(i => i.IsSelected).ToList();
            if (selected.Count == 0)
            {
                await Owner.ShowMessageAsync("Замена", "Отметь файлы двойным кликом или выдели в списке.");
                return;
            }
            await ReplaceImplAsync(selected);
        }

        private async void ReplaceAll_Click(object sender, RoutedEventArgs e)
        {
            var list = ModeTabs.SelectedIndex == 0 ? _easyItems : _proItems;
            if (list.Count == 0)
            {
                await Owner.ShowMessageAsync("Замена", "Сначала сделай сканирование.");
                return;
            }
            await ReplaceImplAsync(list.ToList());
        }

        private async Task ReplaceImplAsync(List<FoundExe> targets)
        {
            // Параметры источника и целевого имени
            string srcExe;
            string targetName;
            bool doBackup;
            IReadOnlyCollection<string> filter;

            if (ModeTabs.SelectedIndex == 0)
            {
                srcExe = DefaultSourceExe();
                targetName = "Kibitkogram.exe";
                doBackup = true; // лёгкий режим всегда делает .bak
                filter = new[] { "Telegram.exe", "Tdesktop.exe", "Kibitkogram.exe" };
            }
            else
            {
                srcExe = ProSourceExeBox.Text;
                targetName = string.IsNullOrWhiteSpace(ProTargetNameBox.Text) ? "Kibitkogram.exe" : ProTargetNameBox.Text.Trim();
                doBackup = ProBackupCheck.IsChecked == true;
                filter = (ProNameFilterBox.Text ?? "")
                                .Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(s => s.Trim())
                                .ToArray();
            }

            if (!File.Exists(srcExe))
            {
                await Owner.ShowMessageAsync("Источник не найден",
                    $"Файл-источник не существует:\n{srcExe}");
                return;
            }

            // Подтверждение
            var res = await Owner.ShowMessageAsync("Подтверждение замены",
                $"Будет заменено файлов: {targets.Count}\n" +
                $"Источник: {srcExe}\n" +
                $"Новое имя: {targetName}\n" +
                (ModeTabs.SelectedIndex == 0 ? "Режим: Лёгкий" : "Режим: ПРО"),
                MessageDialogStyle.AffirmativeAndNegative);

            if (res != MessageDialogResult.Affirmative) return;

            ReplaceSelectedBtn.IsEnabled = false;
            ReplaceAllBtn.IsEnabled = false;
            PatchProgress.Visibility = Visibility.Visible;
            PatchProgress.IsIndeterminate = false;
            PatchProgress.Value = 0;

            _patchCts = new CancellationTokenSource();
            var token = _patchCts.Token;

            int processed = 0, ok = 0, skipped = 0, errors = 0;

            try
            {
                await Task.Run(() =>
                {
                    foreach (var it in targets)
                    {
                        token.ThrowIfCancellationRequested();

                        try
                        {
                            // дополнительно фильтруем по именам, если задано
                            if (filter.Count > 0 &&
                                !filter.Contains(it.Name, StringComparer.OrdinalIgnoreCase))
                            {
                                skipped++;
                                continue;
                            }

                            var dir = Path.GetDirectoryName(it.FullPath)!;
                            var dest = Path.Combine(dir, targetName);

                            // если целевой уже существует и совпадает — можно пропустить
                            if (File.Exists(dest))
                            {
                                // чуть умнее: если одинаковый размер — скорее всего тот же файл
                                if (new FileInfo(dest).Length == new FileInfo(srcExe).Length)
                                {
                                    skipped++;
                                    processed++;
                                    Application.Current.Dispatcher.Invoke(() =>
                                        PatchProgress.Value = (double)processed / targets.Count * 100d);
                                    continue;
                                }
                            }

                            // резервная копия исходника (по месту)
                            if (doBackup && File.Exists(it.FullPath))
                            {
                                try
                                {
                                    var bak = it.FullPath + ".bak";
                                    if (File.Exists(bak)) File.Delete(bak);
                                    File.Move(it.FullPath, bak);
                                }
                                catch { /* ignore */ }
                            }
                            else
                            {
                                try
                                {
                                    if (File.Exists(it.FullPath))
                                    {
                                        File.SetAttributes(it.FullPath, FileAttributes.Normal);
                                        File.Delete(it.FullPath);
                                    }
                                }
                                catch { /* ignore */ }
                            }

                            // копируем источник в целевой
                            Directory.CreateDirectory(dir);
                            File.Copy(srcExe, dest, overwrite: true);

                            ok++;
                        }
                        catch
                        {
                            errors++;
                        }
                        finally
                        {
                            processed++;
                            Application.Current.Dispatcher.Invoke(() =>
                                PatchProgress.Value = (double)processed / targets.Count * 100d);
                        }
                    }
                }, token);

                await Owner.ShowMessageAsync("Готово",
                    $"Успешно: {ok}\nПропущено: {skipped}\nОшибок: {errors}");
            }
            catch (OperationCanceledException)
            {
                await Owner.ShowMessageAsync("Прервано", "Операция замены была отменена.");
            }
            finally
            {
                _patchCts?.Dispose();
                _patchCts = null;

                PatchProgress.Visibility = Visibility.Collapsed;
                ReplaceSelectedBtn.IsEnabled = true;
                ReplaceAllBtn.IsEnabled = true;

                // Обновим список (некоторые exe могли исчезнуть)
                if (ModeTabs.SelectedIndex == 0)
                    ScanEasy_Click(this, new RoutedEventArgs());
                else
                    // ресканим по текущему фильтру и корню из easy (для про можно расширить при желании)
                    await RescanProAfterPatchAsync();
            }
        }

        private async Task RescanProAfterPatchAsync()
        {
            // для простоты — если PRO-режим используется,
            // считаем, что в качестве корня берём то же, что и в лёгком (или любой открытый каталог)
            var root = string.IsNullOrWhiteSpace(EasyRootBox.Text) ? AppDomain.CurrentDomain.BaseDirectory : EasyRootBox.Text;
            var filter = (ProNameFilterBox.Text ?? "")
                            .Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim())
                            .ToArray();

            _proItems.Clear();
            UpdateFoundCount();

            await Task.Run(() =>
            {
                foreach (var f in SafeEnumerateFiles(root, filter, CancellationToken.None))
                {
                    var it = new FoundExe { Name = Path.GetFileName(f), FullPath = f };
                    Application.Current.Dispatcher.Invoke(() => _proItems.Add(it));
                }
            });

            UpdateFoundCount();
        }
    }
}
