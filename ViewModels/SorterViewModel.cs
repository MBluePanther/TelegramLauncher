using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using TelegramLauncher.Services;
using TelegramLauncher.Models;

namespace TelegramLauncher.ViewModels
{
    public class SorterViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<SorterClientVM> Clients { get; } = new();
        public ICollectionView ClientsView { get; }

        private string _searchText;
        public string SearchText { get => _searchText; set { _searchText = value; OnPropertyChanged(); ClientsView.Refresh(); } }

        private string _sortMode = "Name";
        public string SortMode { get => _sortMode; set { _sortMode = value; OnPropertyChanged(); ApplySorting(); } }
        public bool SortDescending { get => _sortDescending; set { _sortDescending = value; OnPropertyChanged(); ApplySorting(); } }
        private bool _sortDescending;

        private string _targetFolder;
        public string TargetFolder { get => _targetFolder; set { _targetFolder = value; OnPropertyChanged(); OnCanRunChanged(); } }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); OnCanRunChanged(); OnCanUndoChanged(); } }

        private double _progress;
        public double Progress { get => _progress; set { _progress = value; OnPropertyChanged(); } }

        // Counters
        public int SelectedCount => Clients.Count(c => c.IsChecked);
        public int TotalCount => Clients.Count;
        public int TdataSelectedCount => Clients.Count(c => c.IsChecked && c.TDataExists);
        private string _selectionHint;
        public string SelectionHint { get => _selectionHint; set { _selectionHint = value; OnPropertyChanged(); } }

        // Status line
        private string _statusMessage;
        public string StatusMessage { get => _statusMessage; set { _statusMessage = value; OnPropertyChanged(); } }
        private string _statusGlyph;
        public string StatusGlyph { get => _statusGlyph; set { _statusGlyph = value; OnPropertyChanged(); } }
        private Brush _statusBrush = Brushes.Transparent;
        public Brush StatusBrush { get => _statusBrush; set { _statusBrush = value; OnPropertyChanged(); } }

        public bool SelectAll
        {
            get => Clients.Count > 0 && Clients.All(c => c.IsChecked);
            set
            {
                foreach (var c in Clients) c.IsChecked = value;
                OnPropertyChanged();
                UpdateSelectionHint();
                OnCanRunChanged();
            }
        }

        // Кнопки
        public bool CanRun => !IsBusy && Clients.Any(c => c.IsChecked);
        public bool CanUndo => _lastUndo != null && !IsBusy && (_lastUndo.CreatedFiles.Count > 0 || _lastUndo.CreatedDirs.Count > 0);

        public ICommand BrowseFolderCommand => _browseFolderCommand;
        public ICommand RunSortCommand => _runSortCommand;
        public ICommand UndoCommand => _undoCommand;
        private readonly RelayCommand _browseFolderCommand;
        private readonly RelayCommand _runSortCommand;
        private readonly RelayCommand _undoCommand;

        private readonly SorterService _service = new();
        private UndoJournal _lastUndo;
        private CancellationTokenSource _cts;

        public SorterViewModel()
        {
            ClientsView = CollectionViewSource.GetDefaultView(Clients);
            ClientsView.Filter = Filter;
            ClientsView.SortDescriptions.Add(new SortDescription(nameof(SorterClientVM.Name), ListSortDirection.Ascending));

            _browseFolderCommand = new RelayCommand(_ => BrowseFolder());
            _runSortCommand = new RelayCommand(async _ => await RunAsync(), _ => CanRun);
            _undoCommand = new RelayCommand(_ => Undo(), _ => CanUndo);

            Clients.CollectionChanged += Clients_CollectionChanged;

            LoadClientsFromStore();
            ApplySorting();
            UpdateSelectionHint();
            SetIdle();
            OnCanRunChanged();
            OnCanUndoChanged();
        }

        private void Clients_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (var it in e.OldItems.OfType<SorterClientVM>())
                    it.PropertyChanged -= Client_PropertyChanged;

            if (e.NewItems != null)
                foreach (var it in e.NewItems.OfType<SorterClientVM>())
                    it.PropertyChanged += Client_PropertyChanged;

            UpdateSelectionHint();
            OnCanRunChanged();
        }

        private void Client_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SorterClientVM.IsChecked))
            {
                UpdateSelectionHint();
                OnCanRunChanged();
            }
        }

        private void UpdateSelectionHint()
        {
            SelectionHint = $"Выбрано: {SelectedCount}/{TotalCount}  (tdata: {TdataSelectedCount})";
        }

        private void OnCanRunChanged()
        {
            OnPropertyChanged(nameof(CanRun));
            _runSortCommand.RaiseCanExecuteChanged();
        }

        private void OnCanUndoChanged()
        {
            OnPropertyChanged(nameof(CanUndo));
            _undoCommand.RaiseCanExecuteChanged();
        }

        private void ApplySorting()
        {
            ClientsView.SortDescriptions.Clear();
            var dir = SortDescending ? ListSortDirection.Descending : ListSortDirection.Ascending;
            switch (SortMode)
            {
                case "Date":
                    ClientsView.SortDescriptions.Add(new SortDescription(nameof(SorterClientVM.LastLaunchedAt), dir));
                    break;
                case "Status":
                    ClientsView.SortDescriptions.Add(new SortDescription(nameof(SorterClientVM.StatusOrder), dir));
                    ClientsView.SortDescriptions.Add(new SortDescription(nameof(SorterClientVM.Name), ListSortDirection.Ascending));
                    break;
                default:
                    ClientsView.SortDescriptions.Add(new SortDescription(nameof(SorterClientVM.Name), dir));
                    break;
            }
        }

        private bool Filter(object obj)
        {
            if (obj is not SorterClientVM c) return false;
            if (string.IsNullOrWhiteSpace(SearchText)) return true;
            return c.Name?.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void BrowseFolder()
        {
            using var dlg = new FolderBrowserDialog();
            dlg.ShowNewFolderButton = true;
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                TargetFolder = dlg.SelectedPath;
            }
        }

        private bool EnsureTargetFolder()
        {
            if (!string.IsNullOrWhiteSpace(TargetFolder)) return true;
            BrowseFolder();
            return !string.IsNullOrWhiteSpace(TargetFolder);
        }

        private async Task RunAsync()
        {
            if (!Clients.Any(c => c.IsChecked)) return;
            if (!EnsureTargetFolder()) return;

            IsBusy = true;
            Progress = 0;
            SetRunning();

            _cts = new CancellationTokenSource();

            try
            {
                var selected = Clients.Where(c => c.IsChecked).ToList();
                var progress = new Progress<(int done, int total, string current)>(p =>
                {
                    Progress = p.total == 0 ? 0 : (double)p.done / p.total * 100.0;
                    StatusMessage = $"Копирование: {p.done}/{p.total} — {p.current}";
                });

                _lastUndo = await _service.CopyTdataAsync(selected, TargetFolder, progress, _cts.Token);

                var total = selected.Count(c => c.TDataExists);
                var skipped = selected.Count - total;
                SetSuccess($"Готово: скопировано {total}/{selected.Count}. Пропущено: {skipped}. → {TargetFolder}");
            }
            finally
            {
                IsBusy = false;
                OnCanUndoChanged();
                OnCanRunChanged();
            }
        }

        private void Undo()
        {
            if (_lastUndo == null) return;
            _service.Undo(_lastUndo);
            var files = _lastUndo.CreatedFiles.Count;
            var dirs = _lastUndo.CreatedDirs.Count;
            SetWarn($"Откат выполнен: удалено файлов {files}, папок {dirs}.");
            _lastUndo = null;
            OnCanUndoChanged();
        }

        private void LoadClientsFromStore()
        {
            try
            {
                var list = ClientStore.Load();
                Clients.Clear();
                foreach (var cfg in list)
                    Clients.Add(new SorterClientVM(cfg));
            }
            catch { }
        }

        // Status helpers
        private void SetIdle()
        {
            StatusGlyph = "";
            StatusMessage = "";
            StatusBrush = Brushes.Transparent;
        }
        private void SetRunning()
        {
            StatusGlyph = "⏳";
            StatusMessage = "Готовимся к копированию…";
            StatusBrush = new SolidColorBrush(Color.FromRgb(24, 39, 54));
        }
        private void SetSuccess(string msg)
        {
            StatusGlyph = "✔";
            StatusMessage = msg;
            StatusBrush = new SolidColorBrush(Color.FromRgb(18, 54, 32));
        }
        private void SetWarn(string msg)
        {
            StatusGlyph = "↩";
            StatusMessage = msg;
            StatusBrush = new SolidColorBrush(Color.FromRgb(54, 39, 18));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
