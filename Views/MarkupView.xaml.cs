using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TelegramLauncher.Models;
using LayoutBridge = TelegramLauncher.Services.LayoutServiceCompat;

namespace TelegramLauncher.Views
{
    public partial class MarkupView : UserControl
    {
        // --- поля (один раз) ---
        private readonly SolidColorBrush _cellOff = new SolidColorBrush(Color.FromRgb(34, 45, 58));
        private readonly SolidColorBrush _cellOn = new SolidColorBrush(Color.FromRgb(47, 137, 201));
        private LayoutConfig Cfg;

        public MarkupView()
        {
            // Инициализируем Cfg ДО InitializeComponent (XAML может триггернуть события)
            Cfg = LayoutBridge.Current ?? new LayoutConfig();

            InitializeComponent();

            int dCols = Cfg.DesignerCols > 0 ? Cfg.DesignerCols : 10;
            int dRows = Cfg.DesignerRows > 0 ? Cfg.DesignerRows : 5;

            BuildDesignerGrid(dCols, dRows);
            LoadMonitors();
            LoadConfigToUi();

            Loaded += (_, __) => UpdateCustomVisibility();
        }

        // ---------- загрузка мониторов ----------
        private void LoadMonitors()
        {
            if (MonitorBox == null) return;

            MonitorBox.Items.Clear();
            var screens = LayoutBridge.GetScreens();

            for (int i = 0; i < screens.Length; i++)
            {
                var s = screens[i];
                MonitorBox.Items.Add(new ComboBoxItem
                {
                    Content = $"{i + 1}: {s.DeviceName} ({s.Bounds.Width}x{s.Bounds.Height})" + (s.Primary ? " [Основной]" : ""),
                    Tag = i
                });
            }

            int safe = (screens.Length > 0)
                ? Math.Max(0, Math.Min(Cfg.MonitorIndex, screens.Length - 1))
                : 0;

            MonitorBox.SelectedIndex = safe;
        }

        // ---------- загрузка конфига в UI ----------
        private void LoadConfigToUi()
        {
            if (ModeBox == null || CustomColsBox == null || CustomRowsBox == null) return;

            string mode = Cfg?.Mode ?? "Auto";
            foreach (ComboBoxItem it in ModeBox.Items)
            {
                if (string.Equals((string)it.Tag, mode, StringComparison.OrdinalIgnoreCase))
                {
                    ModeBox.SelectedItem = it;
                    break;
                }
            }

            CustomColsBox.Text = Math.Max(1,
                Cfg?.CustomCols > 0 ? Cfg.CustomCols :
                Cfg?.DesignerCols > 0 ? Cfg.DesignerCols : 10).ToString();

            CustomRowsBox.Text = Math.Max(1,
                Cfg?.CustomRows > 0 ? Cfg.CustomRows :
                Cfg?.DesignerRows > 0 ? Cfg.DesignerRows : 5).ToString();

            RestoreSelection();
            UpdateCustomVisibility();
        }

        // ---------- показать/скрыть блок кастомной сетки ----------
        private void UpdateCustomVisibility()
        {
            if (CustomGridPanel == null || ModeBox == null) return;

            var tag = ((ComboBoxItem)ModeBox.SelectedItem)?.Tag?.ToString() ?? "Auto";
            CustomGridPanel.Visibility =
                string.Equals(tag, "Custom", StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // ---------- построение сетки превью ----------
        private void BuildDesignerGrid(int cols, int rows)
        {
            if (DesignerGrid == null) return;

            DesignerGrid.Columns = Math.Max(1, cols);
            DesignerGrid.Rows = Math.Max(1, rows);
            DesignerGrid.Children.Clear();

            for (int i = 0; i < DesignerGrid.Columns * DesignerGrid.Rows; i++)
            {
                var cell = new Border
                {
                    Margin = new Thickness(2),
                    Background = _cellOff,
                    CornerRadius = new CornerRadius(4),
                    Tag = i,
                    ToolTip = $"Ячейка {i + 1}"
                };

                cell.MouseLeftButtonUp += (s, e) =>
                {
                    int idx = (int)((Border)s).Tag;
                    ToggleCell(idx, (Border)s);
                };

                DesignerGrid.Children.Add(cell);
            }
        }

        // ---------- выделение ячейки ----------
        private void ToggleCell(int idx, Border cell)
        {
            if (Cfg == null) return;

            if (Cfg.SelectedCells.Contains(idx))
            {
                Cfg.SelectedCells.Remove(idx);
                cell.Background = _cellOff;
            }
            else
            {
                Cfg.SelectedCells.Add(idx);
                cell.Background = _cellOn;
            }
        }

        private void RestoreSelection()
        {
            if (DesignerGrid == null || Cfg == null) return;

            foreach (var b in DesignerGrid.Children.OfType<Border>())
            {
                int idx = (int)b.Tag;
                b.Background = Cfg.SelectedCells.Contains(idx) ? _cellOn : _cellOff;
            }
        }

        /* ====================== События ====================== */

        private void ModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Cfg == null) return;
            Cfg.Mode = ((ComboBoxItem)ModeBox.SelectedItem)?.Tag?.ToString() ?? "Auto";
            UpdateCustomVisibility();
        }

        private void ClearSelection_Click(object sender, RoutedEventArgs e)
        {
            if (Cfg == null) return;
            Cfg.SelectedCells.Clear();
            RestoreSelection();
        }

        private void ResizePreview_Click(object sender, RoutedEventArgs e)
        {
            if (DesignerGrid == null) return;

            if (!int.TryParse(CustomColsBox.Text, out var cc)) cc = DesignerGrid.Columns;
            if (!int.TryParse(CustomRowsBox.Text, out var rr)) rr = DesignerGrid.Rows;

            cc = Math.Max(1, cc);
            rr = Math.Max(1, rr);

            if (Cfg != null)
            {
                Cfg.DesignerCols = cc;
                Cfg.DesignerRows = rr;
            }

            BuildDesignerGrid(cc, rr);
            RestoreSelection();
        }

        private void SaveDefault_Click(object sender, RoutedEventArgs e)
        {
            if (Cfg == null) return;

            Cfg.MonitorIndex = ((ComboBoxItem)MonitorBox.SelectedItem)?.Tag is int i ? i : 0;

            if (DesignerGrid != null)
            {
                Cfg.DesignerCols = DesignerGrid.Columns;
                Cfg.DesignerRows = DesignerGrid.Rows;
            }

            string mode = Cfg.Mode ?? "Auto";
            int cols, rows;

            if (string.Equals(mode, "Custom", StringComparison.OrdinalIgnoreCase))
            {
                cols = int.TryParse(CustomColsBox.Text, out var cc) ? Math.Max(1, cc) : Math.Max(1, Cfg.CustomCols);
                rows = int.TryParse(CustomRowsBox.Text, out var rr) ? Math.Max(1, rr) : Math.Max(1, Cfg.CustomRows);

                Cfg.CustomCols = cols;
                Cfg.CustomRows = rows;
            }
            else
            {
                cols = Math.Max(1, Cfg.DesignerCols > 0 ? Cfg.DesignerCols : 10);
                rows = Math.Max(1, Cfg.DesignerRows > 0 ? Cfg.DesignerRows : 5);
            }

            LayoutBridge.Save(Cfg.MonitorIndex, cols, rows);

            MessageBox.Show("Разметка сохранена.", "Сохранено",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void ApplyToRunning_Click(object sender, RoutedEventArgs e)
        {
            SaveDefault_Click(sender, e);
            try
            {
                await LayoutBridge.ApplyCurrentLayoutToRunningClientsAsync();
                MessageBox.Show("Разметка применена к запущенным клиентам.", "Готово",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка применения разметки:\n{ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
