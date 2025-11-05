using System.Collections.Generic;

namespace TelegramLauncher.Models
{
    public class LayoutConfig
    {
        // Режимы: Auto, Grid, Chaotic, Custom
        public string Mode { get; set; } = "Auto";

        // Индекс физического монитора (0..)
        public int MonitorIndex { get; set; } = 0;

        // Размер дизайнерской сетки (визуальная 10x5 по умолчанию)
        public int DesignerCols { get; set; } = 10;
        public int DesignerRows { get; set; } = 5;

        // Выбранные ячейки дизайнерской сетки (индексы 0..rows*cols-1) — задают позиции клиентов
        public List<int> SelectedCells { get; set; } = new();

        // Пользовательская сетка (гориз/верт)
        public int CustomCols { get; set; } = 2;
        public int CustomRows { get; set; } = 1;
    }
}
