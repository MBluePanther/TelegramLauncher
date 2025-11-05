// Services/LayoutServiceCompat.cs
using System;
using System.Threading.Tasks;
using TelegramLauncher.Layouting;
using TelegramLauncher.Models;
using WF = System.Windows.Forms;

namespace TelegramLauncher.Services
{
    /// <summary>
    /// Компактный совместимый слой между UI и фактическим LayoutService.
    /// Нигде не использует Layouting.*, только TelegramLauncher.Services.LayoutService и LayoutConfig.
    /// </summary>
    public static class LayoutServiceCompat
    {
        /// <summary> Текущая UI-конфигурация разметки. </summary>
        public static LayoutConfig Current => LayoutService.Current ?? new LayoutConfig();

        /// <summary> Список экранов (проксируется из сервиса). </summary>
        public static WF.Screen[] GetScreens() => LayoutService.GetScreens();

        /// <summary>
        /// Сохранение сетки: монитор + колонки/ряды.
        /// Обновляет LayoutConfig и вызывает стандартный Save(cfg).
        /// </summary>
        public static void Save(int monitorIndex, int cols, int rows)
        {
            var cfg = LayoutService.Current ?? new LayoutConfig();

            cfg.MonitorIndex = Math.Max(0, monitorIndex);
            cfg.CustomCols = Math.Max(1, cols);
            cfg.CustomRows = Math.Max(1, rows);

            // если дизайнерские размеры ещё не заданы — выставим минимум
            if (cfg.DesignerCols <= 0) cfg.DesignerCols = cfg.CustomCols;
            if (cfg.DesignerRows <= 0) cfg.DesignerRows = cfg.CustomRows;

            LayoutService.Save(cfg);
        }

        /// <summary>
        /// Обновление сетки без смены монитора (для совместимости со старым кодом).
        /// </summary>
        public static void UpdateGridSettings(int cols, int rows)
        {
            var cfg = LayoutService.Current ?? new LayoutConfig();
            cfg.CustomCols = Math.Max(1, cols);
            cfg.CustomRows = Math.Max(1, rows);
            LayoutService.Save(cfg);
        }

        /// <summary>
        /// Применить текущую раскладку ко всем запущенным клиентам (как и раньше).
        /// </summary>
        public static Task ApplyCurrentLayoutToRunningClientsAsync()
            => LayoutService.ApplyCurrentLayoutToRunningClientsAsync();
    }
}
