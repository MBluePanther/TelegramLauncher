using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel; // Win32Exception (UAC cancel code 1223)
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TelegramLauncher.Models;
using TelegramLauncher.Services;
using TelegramLauncher.Views;
using TelegramLauncher.Layouting;
using WinForms = System.Windows.Forms;

namespace TelegramLauncher
{
    public partial class MainWindow : MetroWindow
    {
        public ObservableCollection<ClientConfig> Clients { get; } = new();

        private enum Section { Launcher, Markup, Patcher, Glass, Tools }
        private Section _section = Section.Launcher;

        private readonly MetroDialogSettings _dlg = new()
        {
            AffirmativeButtonText = "ОК",
            NegativeButtonText = "Отмена",
            ColorScheme = MetroDialogColorScheme.Accented
        };

        // Сканирование
        private CancellationTokenSource? _scanCts;
        private volatile bool _isScanning;

        public MainWindow()
        {
            InitializeComponent();
            ClientsList.ItemsSource = Clients;

            foreach (var c in ClientStore.Load())
                Clients.Add(c);

            if (Clients.Count == 0)
                Clients.Add(new ClientConfig { Name = "Telegram Desktop (пример)" });

            SetSection(Section.Launcher);
        }

        /* ===== Навигация ===== */
        private void ToggleMenu_Click(object sender, RoutedEventArgs e) => MainMenuFlyout.IsOpen = !MainMenuFlyout.IsOpen;

        private void MenuLauncher_Click(object sender, RoutedEventArgs e) => SetSection(Section.Launcher);
        private void MenuMarkup_Click(object sender, RoutedEventArgs e) => SetSection(Section.Markup);
        private void MenuPatcher_Click(object sender, RoutedEventArgs e) => SetSection(Section.Patcher);
        private void MenuGlass_Click(object sender, RoutedEventArgs e) => SetSection(Section.Glass);
        private void MenuTools_Click(object sender, RoutedEventArgs e) => SetSection(Section.Tools);

        private void MenuArranger_Click(object sender, RoutedEventArgs e)
        {
            MainMenuFlyout.IsOpen = false;
            var clients = this.Clients?.ToList() ?? new List<ClientConfig>();
            var w = new TelegramLauncher.Views.LauncherArrangerWindow(clients) { Owner = this };
            w.Show();
        }

        private void SetSection(Section s)
        {
            _section = s;
            MainMenuFlyout.IsOpen = false;

            LauncherPanel.Visibility = s == Section.Launcher ? Visibility.Visible : Visibility.Collapsed;
            MarkupPanel.Visibility = s == Section.Markup ? Visibility.Visible : Visibility.Collapsed;
            PatcherPanel.Visibility = s == Section.Patcher ? Visibility.Visible : Visibility.Collapsed;
            GlassPanel.Visibility = s == Section.Glass ? Visibility.Visible : Visibility.Collapsed;
            ToolsPanel.Visibility = s == Section.Tools ? Visibility.Visible : Visibility.Collapsed;

            LauncherTopButtons.Visibility = s == Section.Launcher ? Visibility.Visible : Visibility.Collapsed;

            if (s == Section.Markup && MarkupHost.Content == null)
                MarkupHost.Content = new MarkupView();

            HeaderText.Text = s switch
            {
                Section.Launcher => "Мои Telegram-клиенты",
                Section.Markup => "Разметка",
                Section.Patcher => "Патчер",
                Section.Glass => "Стекловата",
                Section.Tools => "Инструменты",
                _ => "Telegram Launcher"
            };
        }

        /* ===== Drag & Drop ===== */
        private void Root_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (_section != Section.Launcher) { e.Effects = DragDropEffects.None; e.Handled = true; return; }
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private async void Root_Drop(object sender, DragEventArgs e)
        {
            if (_section != Section.Launcher) return;
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (var path in paths)
            {
                if (Directory.Exists(path))
                    await ScanFolderAndAddAsync(path);
                else if (File.Exists(path) && Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                    AddExe(path);
            }
        }

        private void MainScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer sv)
            {
                sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta);
                e.Handled = true;
            }
        }

        /* ===== Добавление клиентов ===== */
        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var result = await this.ShowMessageAsync(
                "Добавление клиента",
                "Что делаем?",
                MessageDialogStyle.AffirmativeAndNegativeAndSingleAuxiliary,
                new MetroDialogSettings
                {
                    AffirmativeButtonText = "Выбрать .exe",
                    NegativeButtonText = "Сканировать папку",
                    FirstAuxiliaryButtonText = "Отмена",
                    ColorScheme = MetroDialogColorScheme.Accented
                });

            if (result == MessageDialogResult.Affirmative) AddSingleExe();
            else if (result == MessageDialogResult.Negative) await SelectFolderAndScanAsync();
        }

        private void AddSingleExe()
        {
            var ofd = new OpenFileDialog
            {
                Title = "Выберите исполняемый файл клиента",
                Filter = "Приложения (*.exe)|*.exe",
                CheckFileExists = true
            };
            if (ofd.ShowDialog() == true) AddExe(ofd.FileName);
        }

        private void AddExe(string file)
        {
            if (!File.Exists(file)) return;

            if (!Clients.Any(c => string.Equals(c.ExePath, file, StringComparison.OrdinalIgnoreCase)))
            {
                Clients.Add(new ClientConfig
                {
                    Name = GetFolderName(file),
                    ExePath = file,
                    Status = ClientStatus.Active
                });
                ClientStore.Save(Clients);
            }
            else
            {
                _ = this.ShowMessageAsync("Уведомление", "Этот .exe уже в списке.", MessageDialogStyle.Affirmative, _dlg);
            }
        }

        private async Task SelectFolderAndScanAsync()
        {
            using var dlg = new WinForms.FolderBrowserDialog
            {
                Description = "Выберите папку, где искать *.exe клиентов Telegram",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false
            };

            if (dlg.ShowDialog() != WinForms.DialogResult.OK) return;
            await ScanFolderAndAddAsync(dlg.SelectedPath);
        }

        // Новый быстрый и безопасный сканер
        private async Task ScanFolderAndAddAsync(string root)
        {
            if (_isScanning)
            {
                await this.ShowMessageAsync("Поиск уже выполняется", "Дождитесь завершения или нажмите «Отмена».",
                    MessageDialogStyle.Affirmative, _dlg);
                return;
            }
            if (!Directory.Exists(root)) return;

            _isScanning = true;
            _scanCts = new CancellationTokenSource();
            var ct = _scanCts.Token;

            ShowScanUi(true, "Идёт поиск...");

            try
            {
                var patterns = new[] { "telegram", "kibitkogram", "unigram" };
                var existing = new HashSet<string>(Clients.Select(c => c.ExePath), StringComparer.OrdinalIgnoreCase);

                var found = new List<string>();
                var sw = Stopwatch.StartNew();
                int visited = 0, matched = 0;

                await Task.Run(() =>
                {
                    var stack = new Stack<string>();
                    stack.Push(root);

                    while (stack.Count > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        string dir = stack.Pop();

                        try
                        {
                            foreach (var sub in Directory.EnumerateDirectories(dir))
                                stack.Push(sub);
                        }
                        catch { }

                        IEnumerable<string> files;
                        try { files = Directory.EnumerateFiles(dir, "*.exe", SearchOption.TopDirectoryOnly); }
                        catch { continue; }

                        foreach (var f in files)
                        {
                            ct.ThrowIfCancellationRequested();
                            visited++;

                            var nameLower = Path.GetFileName(f).ToLowerInvariant();
                            if (!patterns.Any(p => nameLower.Contains(p))) continue;

                            matched++;
                            if (!existing.Contains(f)) found.Add(f);

                            if (sw.ElapsedMilliseconds > 150)
                            {
                                sw.Restart();
                                Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    ScanStatus.Text = $"Проверено: {visited:N0} | Найдено: {matched:N0} | К добавлению: {found.Count:N0}";
                                }));
                            }
                        }
                    }
                }, ct);

                int added = 0, skipped = 0;
                foreach (var file in found)
                {
                    if (ct.IsCancellationRequested) break;
                    if (existing.Contains(file)) { skipped++; continue; }
                    existing.Add(file);

                    Clients.Add(new ClientConfig
                    {
                        Name = GetFolderName(file),
                        ExePath = file,
                        Status = ClientStatus.Active
                    });
                    added++;
                }

                ClientStore.Save(Clients);
                ScanStatus.Text = $"Готово. Добавлено: {added} | Пропущено: {skipped} | Всего: {Clients.Count}";
                await Task.Delay(500);
            }
            catch (OperationCanceledException)
            {
                ScanStatus.Text = "Поиск отменён.";
                await Task.Delay(400);
            }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Ошибка сканирования", ex.Message, MessageDialogStyle.Affirmative, _dlg);
            }
            finally
            {
                ShowScanUi(false);
                _scanCts?.Dispose();
                _scanCts = null;
                _isScanning = false;
            }
        }

        private void CancelScan_Click(object sender, RoutedEventArgs e) => _scanCts?.Cancel();

        private void ShowScanUi(bool on, string? text = null)
        {
            ScanPanel.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            if (on)
            {
                ScanProgress.IsIndeterminate = true;
                ScanStatus.Text = text ?? "";
                CancelScanButton.IsEnabled = true;
            }
            else
            {
                ScanStatus.Text = "";
                CancelScanButton.IsEnabled = false;
            }
        }

        private static string GetFolderName(string filePath)
        {
            try
            {
                var dir = Path.GetDirectoryName(filePath);
                return string.IsNullOrWhiteSpace(dir) ? Path.GetFileNameWithoutExtension(filePath) : new DirectoryInfo(dir).Name;
            }
            catch { return Path.GetFileNameWithoutExtension(filePath); }
        }

        /* ===== Групповые операции ===== */
        private async void RemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            var toRemove = Clients.Where(c => c.IsSelected).ToList();
            if (toRemove.Count == 0)
            {
                await this.ShowMessageAsync("Нет выбранных", "Поставьте галочки у нужных клиентов.",
                    MessageDialogStyle.Affirmative, _dlg);
                return;
            }

            var res = await this.ShowMessageAsync(
                "Удалить выбранные",
                $"Будут удалены {toRemove.Count} клиент(ов). Продолжить?",
                MessageDialogStyle.AffirmativeAndNegative,
                new MetroDialogSettings
                {
                    AffirmativeButtonText = "Удалить",
                    NegativeButtonText = "Отмена",
                    ColorScheme = MetroDialogColorScheme.Accented
                });

            if (res != MessageDialogResult.Affirmative) return;

            foreach (var c in toRemove) Clients.Remove(c);
            ClientStore.Save(Clients);
            foreach (var c in Clients) c.IsSelected = false;

            await this.ShowMessageAsync("Готово", "Выбранные клиенты удалены.",
                MessageDialogStyle.Affirmative, _dlg);
        }

        /* ===== Карточки клиентов ===== */
        private void StatusButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu is ContextMenu cm)
            {
                cm.PlacementTarget = btn;
                cm.IsOpen = true;
            }
        }

        private async void StatusMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.DataContext is ClientConfig cfg)
            {
                var tag = mi.Tag?.ToString() ?? string.Empty;
                if (Enum.TryParse<ClientStatus>(tag, out var newStatus))
                {
                    cfg.Status = newStatus;
                    ClientStore.Save(Clients);
                    ClientsList.Items.Refresh();

                    string human = newStatus switch
                    {
                        ClientStatus.Active => "Актив",
                        ClientStatus.Frozen => "Заморожен",
                        ClientStatus.Crash => "Слёт",
                        _ => newStatus.ToString()
                    };

                    await this.ShowMessageAsync("Статус обновлён", $"«{cfg.Name}»: {human}", MessageDialogStyle.Affirmative, _dlg);
                }
            }
        }

        private async void PickExe_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not ClientConfig cfg) return;

            var ofd = new OpenFileDialog
            {
                Title = "Выберите исполняемый файл клиента",
                Filter = "Приложения (*.exe)|*.exe",
                CheckFileExists = true
            };
            if (ofd.ShowDialog() == true)
            {
                cfg.ExePath = ofd.FileName;
                cfg.Name = GetFolderName(ofd.FileName);
                ClientStore.Save(Clients);
                ClientsList.Items.Refresh();

                await this.ShowMessageAsync("Готово", "Путь к .exe обновлён.", MessageDialogStyle.Affirmative, _dlg);
            }
        }

        private async void RemoveClient_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not ClientConfig cfg) return;

            var res = await this.ShowMessageAsync(
                "Подтверждение",
                $"Удалить «{cfg.Name}»?",
                MessageDialogStyle.AffirmativeAndNegative,
                new MetroDialogSettings
                {
                    AffirmativeButtonText = "Удалить",
                    NegativeButtonText = "Отмена",
                    ColorScheme = MetroDialogColorScheme.Accented
                });

            if (res == MessageDialogResult.Affirmative)
            {
                Clients.Remove(cfg);
                ClientStore.Save(Clients);
                await this.ShowMessageAsync("Удалено", "Клиент удалён из списка.", MessageDialogStyle.Affirmative, _dlg);
            }
        }

        private async void RunClient_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not ClientConfig cfg) return;

            if (cfg.Status == ClientStatus.Crash)
            {
                await this.ShowMessageAsync("Статус: Слёт", "Запуск запрещён: статус «Слёт».", MessageDialogStyle.Affirmative, _dlg);
                return;
            }

            if (string.IsNullOrWhiteSpace(cfg.ExePath) || !File.Exists(cfg.ExePath))
            {
                await this.ShowMessageAsync("Нет файла", "Сначала укажите корректный путь к .exe", MessageDialogStyle.Affirmative, _dlg);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = cfg.ExePath,
                    Arguments = cfg.Arguments ?? "",
                    WorkingDirectory = Path.GetDirectoryName(cfg.ExePath)!,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                await this.ShowMessageAsync("Ошибка запуска", ex.Message, MessageDialogStyle.Affirmative, _dlg);
            }
        }

        private async void RunAllButton_Click(object sender, RoutedEventArgs e)
        {
            int started = 0, skipped = 0;

            foreach (var cfg in Clients.ToList())
            {
                if (cfg.Status == ClientStatus.Crash) { skipped++; continue; }
                if (string.IsNullOrWhiteSpace(cfg.ExePath) || !File.Exists(cfg.ExePath)) { skipped++; continue; }

                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = cfg.ExePath,
                        Arguments = cfg.Arguments ?? "",
                        WorkingDirectory = Path.GetDirectoryName(cfg.ExePath)!,
                        UseShellExecute = true
                    });
                    started++;
                }
                catch { skipped++; }
            }

            await this.ShowMessageAsync("Запуск завершён", $"Запущено: {started}\nПропущено: {skipped}", MessageDialogStyle.Affirmative, _dlg);

            try { await LayoutService.ApplyCurrentLayoutToRunningClientsAsync(); } catch { }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ClientStore.Save(Clients);
            await this.ShowMessageAsync("Сохранено", "Список клиентов обновлён.", MessageDialogStyle.Affirmative, _dlg);
        }

        /* ===== Инструмент: Очистка hiberfil.sys ===== */
        private async void CleanHiberfil_Click(object sender, RoutedEventArgs e)
        {
            var confirm = await this.ShowMessageAsync(
                "Очистка hiberfil.sys",
                "Отключить гибернацию и удалить файл hiberfil.sys? Это освободит место на системном диске, но отключит гибернацию и быстрый запуск.",
                MessageDialogStyle.AffirmativeAndNegative,
                new MetroDialogSettings { AffirmativeButtonText = "Очистить", NegativeButtonText = "Отмена", ColorScheme = MetroDialogColorScheme.Accented });

            if (confirm != MessageDialogResult.Affirmative) return;

            var sysDrive = GetSystemDriveRoot();
            long before = GetFreeSpace(sysDrive);

            var ok = await RunPowerCfgAsync("-h off");
            if (!ok)
            {
                await this.ShowMessageAsync("Не удалось выполнить", "Требуются права администратора или операция была отменена.",
                    MessageDialogStyle.Affirmative, _dlg);
                return;
            }

            TryDeleteHiberfil();

            long after = GetFreeSpace(sysDrive);
            var freedBytes = after >= 0 && before >= 0 ? Math.Max(0, after - before) : 0;
            string freed = freedBytes > 0 ? FormatSize(freedBytes) : "не удалось определить";

            HiberfilStatusText.Text = $"Готово. Освобождено: {freed}. Гибернация отключена.";
            await this.ShowMessageAsync("Готово", $"Очистка завершена. Освобождено: {freed}.", MessageDialogStyle.Affirmative, _dlg);
        }

        private async void RestoreHibernate_Click(object sender, RoutedEventArgs e)
        {
            var confirm = await this.ShowMessageAsync(
                "Включить гибернацию",
                "Включить гибернацию обратно? Это создаст файл hiberfil.sys и вернёт быстрый запуск.",
                MessageDialogStyle.AffirmativeAndNegative,
                new MetroDialogSettings { AffirmativeButtonText = "Включить", NegativeButtonText = "Отмена", ColorScheme = MetroDialogColorScheme.Accented });

            if (confirm != MessageDialogResult.Affirmative) return;

            var ok = await RunPowerCfgAsync("-h on");
            if (ok)
            {
                HiberfilStatusText.Text = "Гибернация включена.";
                await this.ShowMessageAsync("Готово", "Гибернация снова включена.", MessageDialogStyle.Affirmative, _dlg);
            }
            else
            {
                await this.ShowMessageAsync("Не удалось выполнить", "Требуются права администратора или операция была отменена.",
                    MessageDialogStyle.Affirmative, _dlg);
            }
        }

        private static string GetSystemDriveRoot()
        {
            var sysRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            return Path.GetPathRoot(sysRoot) ?? "C:\\";
        }

        private static long GetFreeSpace(string driveRoot)
        {
            try { return new DriveInfo(driveRoot).AvailableFreeSpace; }
            catch { return -1; }
        }

        private static string FormatSize(long bytes)
        {
            const double GB = 1024d * 1024d * 1024d;
            const double MB = 1024d * 1024d;
            if (bytes >= GB) return $"{bytes / GB:0.##} ГБ";
            if (bytes >= MB) return $"{bytes / MB:0.##} МБ";
            return $"{bytes / 1024d:0} КБ";
        }

        private static void TryDeleteHiberfil()
        {
            try
            {
                var sysRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                var hiberPath = Path.Combine(Path.GetPathRoot(sysRoot) ?? "C:\\", "hiberfil.sys");
                if (File.Exists(hiberPath))
                {
                    File.SetAttributes(hiberPath, FileAttributes.Normal);
                    File.Delete(hiberPath);
                }
            }
            catch { }
        }

        private static async Task<bool> RunPowerCfgAsync(string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = args,
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using var p = Process.Start(psi);
                if (p == null) return false;
                await Task.Run(() => p.WaitForExit());
                return p.ExitCode == 0;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { return false; }
            catch { return false; }
        }

        /* ===== Инструмент: смена «железа» ===== */
        private async void ApplyRandomHardwareProfile_Click(object sender, RoutedEventArgs e)
        {
            string profilesPath = GetProfilesFilePath();
            if (!File.Exists(profilesPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(profilesPath)!);
                await this.ShowMessageAsync(
                    "Файл профилей не найден",
                    $"Положите файл device_profiles.txt по адресу:\n{profilesPath}\n\n" +
                    "Каждый профиль — блок строк Key=Value, разделённый пустой строкой.",
                    MessageDialogStyle.Affirmative, _dlg);
                return;
            }

            var profiles = ReadProfiles(profilesPath);
            if (profiles.Count == 0)
            {
                await this.ShowMessageAsync("Нет профилей", "Файл есть, но профилей не найдено (проверьте формат).",
                    MessageDialogStyle.Affirmative, _dlg);
                return;
            }

            var rnd = new Random();
            int index = rnd.Next(0, profiles.Count);
            var chosen = profiles[index];

            string preview = string.Join(", ", chosen.Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                                                     .Where(kv => !kv.Key.StartsWith("__"))
                                                     .Take(4)
                                                     .Select(kv => $"{kv.Key}={kv.Value}"));

            var res = await this.ShowMessageAsync(
                "Смена «железа»",
                $"Применить профиль #{index + 1}?\n{preview}...",
                MessageDialogStyle.AffirmativeAndNegative,
                new MetroDialogSettings { AffirmativeButtonText = "Применить", NegativeButtonText = "Отмена", ColorScheme = MetroDialogColorScheme.Accented });

            if (res != MessageDialogResult.Affirmative) return;

            bool ok = await ApplyProfileToRegistryAsync(chosen);
            if (!ok)
            {
                await this.ShowMessageAsync("Не удалось применить",
                    "Нужны права администратора или операция была отменена.", MessageDialogStyle.Affirmative, _dlg);
                return;
            }

            string firstLine = chosen.TryGetValue("__ProfileName", out var pn) && !string.IsNullOrEmpty(pn) ? pn : "(без имени)";
            string logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\tIndex={index}\tFirstLine=\"{firstLine}\"\r\n";

            string logPath = Path.Combine(GetExeDir(), "applied_profiles.log");
            try { File.AppendAllText(logPath, logLine, Encoding.UTF8); }
            catch (UnauthorizedAccessException)
            {
                string fallback = Path.Combine(GetAppDataDir(), "applied_profiles.log");
                Directory.CreateDirectory(Path.GetDirectoryName(fallback)!);
                File.AppendAllText(fallback, logLine, Encoding.UTF8);
            }

            HardwareChangeStatusText.Text = $"Профиль #{index + 1} применён: {firstLine}";
            await this.ShowMessageAsync("Готово", $"Профиль #{index + 1} применён.", MessageDialogStyle.Affirmative, _dlg);
        }

        private async void OpenProfilesFolder_Click(object sender, RoutedEventArgs e)
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string profiles = Path.Combine(exeDir, "device_profiles.txt");

            if (File.Exists(profiles))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{profiles}\"",
                    UseShellExecute = true
                });
                return;
            }

            Process.Start(new ProcessStartInfo { FileName = exeDir, UseShellExecute = true });

            await this.ShowMessageAsync(
                "Файл не найден",
                $"Создайте файл device_profiles.txt в папке приложения:\n{exeDir}",
                MessageDialogStyle.Affirmative, _dlg);
        }

        private static string GetAppDataDir() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TelegramLauncher");
        private static string GetExeDir() => AppDomain.CurrentDomain.BaseDirectory;

        private static string GetProfilesFilePath()
        {
            string exeDir = GetExeDir();
            string local = Path.Combine(exeDir, "device_profiles.txt");
            if (File.Exists(local)) return local;

            string appData = Path.Combine(GetAppDataDir(), "device_profiles.txt");
            if (File.Exists(appData)) return appData;

            return local;
        }

        private static List<Dictionary<string, string>> ReadProfiles(string file)
        {
            var list = new List<Dictionary<string, string>>();
            var current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var raw in File.ReadLines(file, Encoding.UTF8))
            {
                var line = raw.Trim();
                if (string.IsNullOrEmpty(line))
                {
                    if (current.Count > 0) { list.Add(current); current = new(StringComparer.OrdinalIgnoreCase); }
                    continue;
                }
                if (line.StartsWith("#")) { current["__ProfileName"] = line; continue; }
                var eq = line.IndexOf('=');
                if (eq > 0) { current[line[..eq].Trim()] = line[(eq + 1)..].Trim(); }
            }
            if (current.Count > 0) list.Add(current);
            return list;
        }

        private static async Task<bool> ApplyProfileToRegistryAsync(Dictionary<string, string> profile)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Windows Registry Editor Version 5.00");
                sb.AppendLine();
                sb.AppendLine(@"[HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\BIOS]");

                void Add(string key)
                {
                    if (profile.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
                    {
                        string esc = v.Replace("\\", "\\\\").Replace("\"", "\\\"");
                        sb.AppendLine($"\"{key}\"=\"{esc}\"");
                    }
                }

                Add("BaseBoardManufacturer"); Add("BaseBoardProduct"); Add("BaseBoardVersion");
                Add("BIOSVendor"); Add("BIOSVersion");
                Add("SystemFamily"); Add("SystemManufacturer"); Add("SystemProductName"); Add("SystemSKU"); Add("SystemVersion");

                string dir = GetAppDataDir();
                Directory.CreateDirectory(dir);
                string regPath = Path.Combine(dir, "apply_profile.reg");
                File.WriteAllText(regPath, sb.ToString(), new UnicodeEncoding(false, true)); // UTF-16 LE

                var psi = new ProcessStartInfo
                {
                    FileName = "reg.exe",
                    Arguments = $"import \"{regPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var p = Process.Start(psi);
                if (p == null) return false;
                await Task.Run(() => p.WaitForExit());
                return p.ExitCode == 0;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { return false; }
            catch { return false; }
        }
        // ======================= КИЛЛЕР ПРОЦЕССОВ TELEGRAM =========================
        // Вся логика вынесена во вложенный статический класс, чтобы не конфликтовать с твоими именами.
        private static class TelegramProcessKiller
        {
            // Явные исполняемые + подстроки для совпадений
            private static readonly string[] KillExeNames =
            {
        "telegram.exe", "tdesktop.exe",
        "kotatogram.exe", "unigram.exe",
        "nekogram.exe", "kibibitogram.exe"
    };

            private static readonly string[] KillNameContains =
            {
        "telegram", "tdesktop"
    };

            // Главная функция: пройтись по процессам и прибить все подходящие
            public static (int killed, int errors, int total) KillAll()
            {
                int killed = 0, errors = 0, total = 0;

                foreach (var p in Process.GetProcesses())
                {
                    try
                    {
                        string pn = "";
                        string path = "";

                        try { pn = p.ProcessName ?? ""; } catch { }
                        try { path = p.MainModule?.FileName ?? ""; } catch { }

                        if (!Matches(pn, path))
                            continue;

                        total++;
                        try
                        {
                            p.Kill(entireProcessTree: true);
                            p.WaitForExit(200);
                            killed++;
                        }
                        catch
                        {
                            errors++;
                        }
                        finally
                        {
                            p.Dispose();
                        }
                    }
                    catch
                    {
                        // игнорируем «злые» процессы, к которым нет доступа
                    }
                }

                // Доп. «подметание» через taskkill (если доступно в системе)
                foreach (var exe in KillExeNames)
                {
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "taskkill",
                            Arguments = $"/F /T /IM \"{exe}\"",
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };
                        using var tk = Process.Start(psi);
                        tk?.WaitForExit(200);
                    }
                    catch { /* ignore */ }
                }

                return (killed, errors, total);
            }

            private static bool Matches(string processName, string fullPath)
            {
                if (!string.IsNullOrEmpty(processName))
                {
                    if (KillExeNames.Any(n =>
                            string.Equals(Path.GetFileNameWithoutExtension(n), processName,
                                          StringComparison.OrdinalIgnoreCase)))
                        return true;

                    if (KillNameContains.Any(s =>
                            processName.Contains(s, StringComparison.OrdinalIgnoreCase)))
                        return true;
                }

                if (!string.IsNullOrEmpty(fullPath))
                {
                    if (KillExeNames.Any(n =>
                            fullPath.EndsWith(n, StringComparison.OrdinalIgnoreCase)))
                        return true;

                    if (KillNameContains.Any(s =>
                            fullPath.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0))
                        return true;
                }

                return false;
            }
        }

        // Обработчик кнопки (по требованию — без подтверждения, «жёсткий» килл)
        private async void KillTelegram_Click(object sender, RoutedEventArgs e)
        {
            var confirm = await this.ShowMessageAsync(
                "Киллер Telegram",
                "Завершить ВСЕ процессы Telegram и форков? Это принудительное действие.",
                MessageDialogStyle.AffirmativeAndNegative);

            if (confirm != MessageDialogResult.Affirmative)
                return;


            var (killed, errors, total) = await Task.Run(TelegramProcessKiller.KillAll);

            await this.ShowMessageAsync(
                "Готово",
                $"Найдено: {total}\nЗавершено: {killed}\nОшибок: {errors}");
        }
        // ==================== КОНЕЦ: КИЛЛЕР ПРОЦЕССОВ TELEGRAM =====================


    }
}
