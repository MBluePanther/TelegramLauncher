using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using WF = System.Windows.Forms;

namespace TelegramLauncher.Views
{
    public partial class GlassWoolView : UserControl
    {
        // Тип искомого элемента
        private enum ItemType { Exe, Tdata }

        // Что показываем в списке
        private class FoundItem
        {
            public ItemType Type { get; set; }
            public string TypeText => Type == ItemType.Exe ? "exe" : "tdata";
            public string Name { get; set; } = "";
            public string FullPath { get; set; } = "";
            public bool IsSelected { get; set; } = false;
            public string SelectionMark => IsSelected ? "✔" : "";
        }

        private readonly ObservableCollection<FoundItem> _items = new();
        private ScanMode _lastScan = ScanMode.None;

        private enum ScanMode { None, ExeOnly, TdataOnly }

        // Имена exe: поддержим разные форки
        private static readonly string[] ExeNames = new[]
        {
            "Telegram.exe", "Tdesktop.exe",
            "Kibitkogram.exe", "Kibibitogram.exe"
        };

        public GlassWoolView()
        {
            InitializeComponent();
            ResultsList.ItemsSource = _items;

            // Клик по элементу — просто переключаем IsSelected
            ResultsList.MouseDoubleClick += (_, __) =>
            {
                if (ResultsList.SelectedItem is FoundItem it)
                {
                    it.IsSelected = !it.IsSelected;
                    // Обновить SelectionMark
                    var idx = ResultsList.SelectedIndex;
                    _items[idx] = it;
                    UpdateCount();
                }
            };

            UpdateCount();
        }

        // ========= UI helpers =========

        private MetroWindow Owner => (MetroWindow)Window.GetWindow(this);

        private void UpdateCount() => FoundCountText.Text = _items.Count.ToString();

        private static string? PickFolder(string? start = null)
        {
            using var dlg = new WF.FolderBrowserDialog();
            if (!string.IsNullOrWhiteSpace(start) && Directory.Exists(start))
                dlg.SelectedPath = start;
            dlg.ShowNewFolderButton = true;
            var res = dlg.ShowDialog();
            return res == WF.DialogResult.OK ? dlg.SelectedPath : null;
        }

        // ========= Скан =========

        private async Task<List<FoundItem>> ScanExeAsync(string root)
        {
            return await Task.Run(() =>
            {
                var list = new List<FoundItem>();
                foreach (var file in SafeEnumerateFiles(root, "*.exe"))
                {
                    var name = Path.GetFileName(file);
                    if (!ExeNames.Any(en => name.Equals(en, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    list.Add(new FoundItem
                    {
                        Type = ItemType.Exe,
                        Name = name,
                        FullPath = file
                    });
                }
                return list;
            });
        }

        private async Task<List<FoundItem>> ScanTdataAsync(string root)
        {
            return await Task.Run(() =>
            {
                var list = new List<FoundItem>();
                foreach (var dir in SafeEnumerateDirectories(root, "tdata"))
                {
                    list.Add(new FoundItem
                    {
                        Type = ItemType.Tdata,
                        Name = "tdata",
                        FullPath = dir
                    });
                }
                return list;
            });
        }

        // Безопасные обходы (не валимся на AccessDenied)
        private static IEnumerable<string> SafeEnumerateFiles(string root, string pattern)
        {
            var stack = new Stack<string>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                string[] files = Array.Empty<string>();
                string[] dirs = Array.Empty<string>();

                try { files = Directory.GetFiles(cur, pattern); } catch { }
                try { dirs = Directory.GetDirectories(cur); } catch { }

                foreach (var f in files) yield return f;
                foreach (var d in dirs) stack.Push(d);
            }
        }

        private static IEnumerable<string> SafeEnumerateDirectories(string root, string name)
        {
            var stack = new Stack<string>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                string[] dirs = Array.Empty<string>();

                try { dirs = Directory.GetDirectories(cur); }
                catch { }

                foreach (var d in dirs)
                {
                    if (string.Equals(Path.GetFileName(d), name, StringComparison.OrdinalIgnoreCase))
                        yield return d;
                    stack.Push(d);
                }
            }
        }

        // ========= Кнопки =========

        private void PickBaseFolder_Click(object sender, RoutedEventArgs e)
        {
            var p = PickFolder(BaseFolderBox.Text);
            if (p != null) BaseFolderBox.Text = p;
        }

        private async void ScanExe_Click(object sender, RoutedEventArgs e)
        {
            var root = BaseFolderBox.Text;
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                await Owner.ShowMessageAsync("Сканирование", "Укажи корректную «Папку с ТГ»."); return;
            }

            _lastScan = ScanMode.ExeOnly;
            _items.Clear();
            UpdateCount();

            var found = await ScanExeAsync(root);
            foreach (var it in found) _items.Add(it);
            UpdateCount();
        }

        private async void ScanTdata_Click(object sender, RoutedEventArgs e)
        {
            var root = BaseFolderBox.Text;
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                await Owner.ShowMessageAsync("Сканирование", "Укажи корректную «Папку с ТГ»."); return;
            }

            _lastScan = ScanMode.TdataOnly;
            _items.Clear();
            UpdateCount();

            var found = await ScanTdataAsync(root);
            foreach (var it in found) _items.Add(it);
            UpdateCount();
        }

        private async void Rescan_Click(object sender, RoutedEventArgs e)
        {
            switch (_lastScan)
            {
                case ScanMode.ExeOnly: ScanExe_Click(sender, e); break;
                case ScanMode.TdataOnly: ScanTdata_Click(sender, e); break;
                default:
                    await Owner.ShowMessageAsync("Обновить список", "Сначала сделай сканирование.");
                    break;
            }
        }

        private void PickDestFolder_Click(object sender, RoutedEventArgs e)
        {
            var p = PickFolder(DestFolderBox.Text);
            if (p != null) DestFolderBox.Text = p;
        }

        private async void RemoveExe_Click(object sender, RoutedEventArgs e)
        {
            if (_items.Count == 0)
            {
                await Owner.ShowMessageAsync("Удаление", "Сначала отсканируй exe."); return;
            }

            var exeItems = _items.Where(i => i.Type == ItemType.Exe).ToList();
            if (exeItems.Count == 0)
            {
                await Owner.ShowMessageAsync("Удаление", "В списке нет Telegram.exe / форков."); return;
            }

            var confirm = await Owner.ShowMessageAsync("Удаление",
                "Удалить ВСЕ найденные .exe? (Действие необратимо)",
                MessageDialogStyle.AffirmativeAndNegative);

            if (confirm != MessageDialogResult.Affirmative) return;

            int removed = 0, errors = 0;
            await Task.Run(() =>
            {
                foreach (var it in exeItems)
                {
                    try
                    {
                        if (File.Exists(it.FullPath))
                        {
                            File.SetAttributes(it.FullPath, FileAttributes.Normal);
                            File.Delete(it.FullPath);
                            removed++;
                        }
                    }
                    catch
                    {
                        errors++;
                    }
                }
            });

            // очистим удалённые из списка
            foreach (var it in exeItems)
                _items.Remove(it);
            UpdateCount();

            await Owner.ShowMessageAsync("Готово",
                $"Удалено .exe: {removed}\nОшибок: {errors}");
        }

        private async void MoveSelectedTdata_Click(object sender, RoutedEventArgs e)
        {
            var destRoot = DestFolderBox.Text;
            if (string.IsNullOrWhiteSpace(destRoot) || !Directory.Exists(destRoot))
            {
                await Owner.ShowMessageAsync("Перенос tdata", "Укажи корректную «Папку назначения tdata»."); return;
            }

            var selected = _items.Where(i => i.Type == ItemType.Tdata && i.IsSelected).ToList();
            if (selected.Count == 0)
            {
                await Owner.ShowMessageAsync("Перенос tdata", "Отметь (двойной клик) нужные tdata в списке."); return;
            }

            int moved = 0, copied = 0, errors = 0;

            await Task.Run(() =>
            {
                foreach (var it in selected)
                {
                    try
                    {
                        var srcTdata = it.FullPath;
                        if (!Directory.Exists(srcTdata)) { errors++; continue; }

                        // имя родительской папки над tdata: ...\79217711916\tdata
                        var parent = Directory.GetParent(srcTdata)?.Name ?? "tdata";
                        var target = Path.Combine(destRoot, parent, "tdata");

                        // если target уже есть — создадим уникальную папку
                        target = MakeUniqueFolder(target);

                        // пробуем переместить; если не вышло — копируем
                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                            Directory.Move(srcTdata, target);
                            moved++;
                        }
                        catch
                        {
                            try
                            {
                                Directory.CreateDirectory(target);
                                CopyDirectory(srcTdata, target);
                                copied++;
                            }
                            catch
                            {
                                errors++;
                            }
                        }
                    }
                    catch
                    {
                        errors++;
                    }
                }
            });

            await Owner.ShowMessageAsync("Перенос завершён",
                $"Перемещено: {moved}\nСкопировано: {copied}\nОшибок: {errors}");
        }

        // ====== utils: уникальный путь, копирование каталога ======

        private static string MakeUniqueFolder(string path)
        {
            if (!Directory.Exists(path)) return path;
            int i = 1;
            string candidate;
            do
            {
                candidate = $"{path}_{i++}";
            } while (Directory.Exists(candidate));
            return candidate;
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            foreach (var dir in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                var rel = dir.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(targetDir, rel));
            }

            foreach (var file in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
            {
                var rel = file.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar);
                File.Copy(file, Path.Combine(targetDir, rel), overwrite: true);
            }
        }
    }
}
