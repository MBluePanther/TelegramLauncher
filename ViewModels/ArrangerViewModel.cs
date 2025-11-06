using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using Media = System.Windows.Media;
using SD = System.Drawing;

using TelegramLauncher.Models;
using TelegramLauncher.Services;

namespace TelegramLauncher.ViewModels
{
    public enum ArrangeMode { Auto, Grid, Chaotic, CustomHorizontal, CustomVertical }

    public sealed class ClientItemVM : INotifyPropertyChanged
    {
        public ClientConfig Client { get; }
        private bool _isChecked;

        public ClientItemVM(ClientConfig client) => Client = client;

        public bool IsChecked
        {
            get => _isChecked;
            set { if (_isChecked != value) { _isChecked = value; OnPropertyChanged(); } }
        }

        public string DisplayName
        {
            get
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(Client.ExePath)) return Client.Name ?? "—";
                    var dir = Path.GetDirectoryName(Client.ExePath);
                    return string.IsNullOrEmpty(dir) ? (Client.Name ?? "—") : new DirectoryInfo(dir).Name;
                }
                catch { return Client.Name ?? "—"; }
            }
        }

        private static Media.Color MakeColor(byte r, byte g, byte b) => Media.Color.FromRgb(r, g, b);

        public Media.Color StatusColor => Client.Status switch
        {
            ClientStatus.Active => MakeColor(0x43, 0xA0, 0x47),
            ClientStatus.Frozen => MakeColor(0xFF, 0xC1, 0x07),
            ClientStatus.Crash => MakeColor(0xE5, 0x39, 0x35),
            _ => MakeColor(0x90, 0xA4, 0xAE)
        };

        public Media.Brush StatusFillBrush => new Media.SolidColorBrush(StatusColor);

        public Media.Brush StatusStrokeBrush
        {
            get
            {
                var c = StatusColor;
                byte dr = (byte)Math.Max(0, c.R - 30);
                byte dg = (byte)Math.Max(0, c.G - 30);
                byte db = (byte)Math.Max(0, c.B - 30);
                return new Media.SolidColorBrush(Media.Color.FromRgb(dr, dg, db));
            }
        }

        public Media.Brush TextBrush
        {
            get
            {
                double y = 0.2126 * StatusColor.R + 0.7152 * StatusColor.G + 0.0722 * StatusColor.B;
                return new Media.SolidColorBrush(y < 140 ? Media.Colors.White : Media.Colors.Black);
            }
        }

        public string StatusText => Client.Status switch
        {
            ClientStatus.Active => "Актив",
            ClientStatus.Frozen => "Заморожен",
            ClientStatus.Crash => "Слёт",
            _ => "Неизвестно"
        };

        public DateTime? FileDateUtc
        {
            get
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(Client.ExePath) && File.Exists(Client.ExePath))
                        return File.GetLastWriteTimeUtc(Client.ExePath);
                }
                catch { }
                return null;
            }
        }

        public string LastWriteDisplay
        {
            get
            {
                var d = FileDateUtc;
                if (d == null) return "";
                try { return "обновлён " + d.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm"); } catch { return ""; }
            }
        }

        public string SortName => DisplayName;
        public DateTime SortDate => FileDateUtc ?? DateTime.MinValue;
        public int SortStatus => Client.Status switch
        {
            ClientStatus.Active => 1,
            ClientStatus.Frozen => 0,
            ClientStatus.Crash => 2,
            _ => 9
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        internal void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    internal sealed class DelegateCommand : ICommand
    {
        private readonly Action<object?> _exec;
        private readonly Func<object?, bool>? _can;
        public DelegateCommand(Action<object?> exec, Func<object?, bool>? can = null) { _exec = exec; _can = can; }
        public bool CanExecute(object? parameter) => _can?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _exec(parameter);
        public event EventHandler? CanExecuteChanged { add { CommandManager.RequerySuggested += value; } remove { CommandManager.RequerySuggested -= value; } }
    }

    public sealed class ArrangerViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<ClientItemVM> Clients { get; } = new();
        public ListCollectionView ClientsView { get; }

        private ArrangeMode _mode = ArrangeMode.Auto;
        public ArrangeMode Mode { get => _mode; set { if (_mode != value) { _mode = value; OnPropertyChanged(); OnPropertyChanged(nameof(ModeIndex)); } } }
        public int ModeIndex { get => (int)Mode; set { if ((int)Mode != value) Mode = (ArrangeMode)value; } }

        private int _constructorCols = 2;
        public int ConstructorCols { get => _constructorCols; set { if (_constructorCols != value) { _constructorCols = Math.Max(1, Math.Min(12, value)); OnPropertyChanged(); RaiseCounters(); } } }
        private int _constructorRows = 2;
        public int ConstructorRows { get => _constructorRows; set { if (_constructorRows != value) { _constructorRows = Math.Max(1, Math.Min(12, value)); OnPropertyChanged(); RaiseCounters(); } } }
        public int ConstructorCapacity => Math.Max(1, ConstructorCols) * Math.Max(1, ConstructorRows);
        public int ConstructorFitCount => Math.Min(SelectedCount, ConstructorCapacity);
        public ObservableCollection<int> PreviewBoxes { get; } = new();

        public ObservableCollection<string> Monitors { get; } = new();
        private int _monitorIndex;
        public int MonitorIndex { get => _monitorIndex; set { if (_monitorIndex != value) { _monitorIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedScreen)); } } }
        public Screen? SelectedScreen => (MonitorIndex >= 0 && MonitorIndex < Screen.AllScreens.Length) ? Screen.AllScreens[MonitorIndex] : Screen.PrimaryScreen;

        public int SelectedCount => Clients.Count(c => c.IsChecked);
        public string CounterText => $"Выбрано: {SelectedCount} / Всего: {Clients.Count}";

        public ObservableCollection<string> SortOptions { get; } = new()
        {
            "Название A→Я","Название Я→А",
            "Дата новая→старая","Дата старая→новая",
            "Статус заморожен→актив","Статус актив→заморожен"
        };
        private string? _selectedSort;
        public string? SelectedSort { get => _selectedSort; set { if (_selectedSort != value) { _selectedSort = value; OnPropertyChanged(); ApplySelectedSort(); } } }

        public ICommand ClearConstructorCmd { get; }
        public ICommand AutoFitConstructorCmd { get; }
        public ICommand LaunchSelectedCmd { get; }
        public ICommand LaunchFromConstructorCmd { get; }

        public ArrangerViewModel() : this(null) { }
        public ArrangerViewModel(System.Collections.Generic.IEnumerable<ClientConfig>? sourceClients)
        {
            if (sourceClients != null)
                foreach (var c in sourceClients) AddAndHook(c);

            ClientsView = (ListCollectionView)CollectionViewSource.GetDefaultView(Clients);
            ClientsView.Filter = o => o is ClientItemVM vm && vm.Client.Status != ClientStatus.Crash;

            Monitors.Clear();
            foreach (var t in Screen.AllScreens.Select((s, i) => new { s, i }))
                Monitors.Add($"{t.i}: {t.s.Bounds.Width}×{t.s.Bounds.Height} {(t.s.Primary ? "(основной)" : "")}");

            ClearConstructorCmd = new DelegateCommand(_ => { foreach (var c in Clients) c.IsChecked = false; RaiseCounters(); });
            AutoFitConstructorCmd = new DelegateCommand(_ => AutoFitGrid());
            LaunchSelectedCmd = new DelegateCommand(_ => _ = LaunchAndArrangeSelectedAsync(useConstructor: false));
            LaunchFromConstructorCmd = new DelegateCommand(_ => _ = LaunchAndArrangeSelectedAsync(useConstructor: true));

            Clients.CollectionChanged += Clients_CollectionChanged;
            RaiseCounters();
        }

        private void Clients_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (ClientItemVM vm in e.NewItems) vm.PropertyChanged += Vm_PropertyChanged;
            if (e.OldItems != null)
                foreach (ClientItemVM vm in e.OldItems) vm.PropertyChanged -= Vm_PropertyChanged;
            RaiseCounters();
        }
        private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ClientItemVM.IsChecked)) RaiseCounters();
        }

        private void AddAndHook(ClientConfig c) { var vm = new ClientItemVM(c); vm.PropertyChanged += Vm_PropertyChanged; Clients.Add(vm); }

        private void ApplySelectedSort()
        {
            if (string.IsNullOrEmpty(SelectedSort)) return;
            var s = SelectedSort;
            ClientsView.SortDescriptions.Clear();

            if (s.StartsWith("Название"))
                ClientsView.SortDescriptions.Add(new SortDescription(nameof(ClientItemVM.SortName),
                    s.Contains("Я") ? ListSortDirection.Descending : ListSortDirection.Ascending));
            else if (s.StartsWith("Дата"))
                ClientsView.SortDescriptions.Add(new SortDescription(nameof(ClientItemVM.SortDate),
                    s.Contains("старая") ? ListSortDirection.Ascending : ListSortDirection.Descending));
            else if (s.StartsWith("Статус"))
            {
                bool frozenFirst = s.Contains("заморожен→актив");
                ClientsView.SortDescriptions.Add(new SortDescription(nameof(ClientItemVM.SortStatus),
                    frozenFirst ? ListSortDirection.Ascending : ListSortDirection.Descending));
            }

            ClientsView.Refresh();
        }

        private void AutoFitGrid()
        {
            int n = Math.Max(1, SelectedCount);
            int cols = (int)Math.Ceiling(Math.Sqrt(n));
            int rows = (int)Math.Ceiling(n / (double)cols);
            ConstructorCols = cols;
            ConstructorRows = rows;
        }

        private System.Collections.Generic.List<SD.Rectangle> BuildRectsForMode(int n, SD.Rectangle work, bool useConstructor)
        {
            var rects = new System.Collections.Generic.List<SD.Rectangle>(n);
            if (n <= 0) return rects;

            int cols, rows;

            if (useConstructor)
            {
                cols = Math.Max(1, ConstructorCols);
                rows = Math.Max(1, ConstructorRows);
            }
            else
            {
                switch (Mode)
                {
                    case ArrangeMode.CustomHorizontal:
                        cols = n; rows = 1; break;
                    case ArrangeMode.CustomVertical:
                        cols = 1; rows = n; break;
                    case ArrangeMode.Grid:
                    case ArrangeMode.Auto:
                        cols = (int)Math.Ceiling(Math.Sqrt(n));
                        rows = (int)Math.Ceiling(n / (double)cols);
                        break;
                    case ArrangeMode.Chaotic:
                        cols = Math.Min(n, 3);
                        rows = (int)Math.Ceiling(n / (double)cols);
                        break;
                    default:
                        cols = (int)Math.Ceiling(Math.Sqrt(n));
                        rows = (int)Math.Ceiling(n / (double)cols);
                        break;
                }
            }

            int cellW = Math.Max(200, work.Width / Math.Max(1, cols));
            int cellH = Math.Max(160, work.Height / Math.Max(1, rows));

            for (int i = 0; i < n; i++)
            {
                int r = i / cols;
                int c = i % cols;
                int x = work.Left + c * cellW;
                int y = work.Top + r * cellH;
                var rc = new SD.Rectangle(x, y, Math.Min(cellW, work.Right - x), Math.Min(cellH, work.Bottom - y));
                rects.Add(rc);
            }
            return rects;
        }

        private async Task LaunchAndArrangeSelectedAsync(bool useConstructor)
        {
            var selected = Clients.Where(c => c.IsChecked).Select(c => c.Client).ToList();
            if (selected.Count == 0) return;

            var procs = new System.Collections.Generic.List<Process>();
            foreach (var cfg in selected)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(cfg.ExePath) && File.Exists(cfg.ExePath))
                    {
                        var psi = new ProcessStartInfo(cfg.ExePath)
                        {
                            Arguments = cfg.Arguments ?? string.Empty,
                            UseShellExecute = false,
                            WorkingDirectory = Path.GetDirectoryName(cfg.ExePath) ?? string.Empty,
                            WindowStyle = ProcessWindowStyle.Normal
                        };
                        var p = Process.Start(psi);
                        if (p != null) procs.Add(p);
                    }
                }
                catch { /* TODO: тост об ошибке */ }
            }

            await Task.Delay(600);

            var screen = SelectedScreen ?? Screen.PrimaryScreen;
            var wa = screen.WorkingArea;
            var rects = BuildRectsForMode(procs.Count, new SD.Rectangle(wa.Left, wa.Top, wa.Width, wa.Height), useConstructor);
            Win32Arrange.ArrangeProcessesOnScreen(procs, screen, rects);
        }

        public void RaiseCounters()
        {
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(CounterText));
            OnPropertyChanged(nameof(ConstructorCapacity));
            OnPropertyChanged(nameof(ConstructorFitCount));

            PreviewBoxes.Clear();
            int need = Math.Min(SelectedCount, ConstructorCapacity);
            for (int i = 0; i < need; i++) PreviewBoxes.Add(i + 1);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}