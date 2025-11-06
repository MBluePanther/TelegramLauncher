using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using TelegramLauncher.Models;

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
                    return string.IsNullOrEmpty(dir) ? Client.Name ?? "—" : new DirectoryInfo(dir).Name;
                }
                catch { return Client.Name ?? "—"; }
            }
        }

        public Brush StatusBrush => Client.Status switch
        {
            ClientStatus.Active => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
            ClientStatus.Frozen => new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07)),
            ClientStatus.Crash  => new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
            _ => new SolidColorBrush(Color.FromRgb(0x90, 0xA4, 0xAE))
        };

        public DateTime? FileDateUtc
        {
            get
            {
                try
                {
                    if (File.Exists(Client.ExePath))
                        return File.GetCreationTimeUtc(Client.ExePath);
                }
                catch { }
                return null;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public sealed class ArrangerViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<ClientItemVM> Clients { get; } = new();
        public ListCollectionView ClientsView { get; }

        private ArrangeMode _mode = ArrangeMode.Auto;
        public ArrangeMode Mode { get => _mode; set { if (_mode != value) { _mode = value; OnPropertyChanged(); } } }

        private int _columns = 1;
        public int Columns { get => _columns; set { if (_columns != value) { _columns = value; OnPropertyChanged(); } } }

        public ObservableCollection<string> Monitors { get; } = new();
        private int _monitorIndex;
        public int MonitorIndex { get => _monitorIndex; set { if (_monitorIndex != value) { _monitorIndex = value; OnPropertyChanged(); } } }

        public ObservableCollection<string> VirtualDesktops { get; } = new() { "Текущий", "Рабочий стол 2", "Рабочий стол 3" };
        private int _vdIndex = 0;
        public int VirtualDesktopIndex { get => _vdIndex; set { if (_vdIndex != value) { _vdIndex = value; OnPropertyChanged(); } } }

        public int SelectedCount => Clients.Count(c => c.IsChecked);
        public string CounterText => $"Выбрано: {SelectedCount} / Всего: {Clients.Count}";

        public ICommand SortByNameCmd { get; }
        public ICommand SortByDateCmd { get; }
        public ICommand SortByStatusCmd { get; }
        public ICommand SetColumnsCmd { get; }
        public ICommand ClearConstructorCmd { get; }
        public ICommand SaveDefaultCmd { get; }
        public ICommand LaunchSelectedCmd { get; }
        public ICommand LaunchWithCriteriaCmd { get; }

        public ArrangerViewModel(System.Collections.Generic.IEnumerable<ClientConfig>? sourceClients = null)
        {
            if (sourceClients != null)
            {
                foreach (var c in sourceClients) Clients.Add(new ClientItemVM(c));
            }

            ClientsView = (ListCollectionView)CollectionViewSource.GetDefaultView(Clients);

            foreach (var s in Screen.AllScreens.Select((s, i) => new { s, i }))
                Monitors.Add($"{s.i}: {s.s.Bounds.Width}×{s.s.Bounds.Height}" );

            SortByNameCmd = new RelayCommand(_ => ApplySort(nameof(ClientItemVM.DisplayName)));
            SortByDateCmd = new RelayCommand(_ => ApplySort(nameof(ClientItemVM.FileDateUtc)));
            SortByStatusCmd = new RelayCommand(_ => ApplySort("Client.Status"));
            SetColumnsCmd = new RelayCommand(n => { if (n is string s && int.TryParse(s, out var v)) Columns = Math.Max(1, Math.Min(3, v)); });
            ClearConstructorCmd = new RelayCommand(_ => { foreach (var c in Clients) c.IsChecked = false; RaiseCounters(); });
            SaveDefaultCmd = new RelayCommand(_ => {/* TODO: persist default */});
            LaunchSelectedCmd = new RelayCommand(_ => LaunchSelected());
            LaunchWithCriteriaCmd = new RelayCommand(_ => LaunchWithCriteria());
        }

        private void ApplySort(string prop)
        {
            ClientsView.SortDescriptions.Clear();
            ClientsView.SortDescriptions.Add(new SortDescription(prop, ListSortDirection.Ascending));
            ClientsView.Refresh();
        }

        private void LaunchSelected()
        {
            var selected = Clients.Where(c => c.IsChecked).Select(c => c.Client).ToList();
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
                            WindowStyle = ProcessWindowStyle.Normal
                        };
                        Process.Start(psi);
                    }
                }
                catch { }
            }
        }

        private void LaunchWithCriteria()
        {
            LaunchSelected();
        }

        public void RaiseCounters()
        {
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(CounterText));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}