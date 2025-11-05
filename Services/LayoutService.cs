using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TelegramLauncher.Models;
using TelegramLauncher.Services;
using WF = System.Windows.Forms;

namespace TelegramLauncher.Layouting
{
    /// <summary>
    /// Хранит/считает конфиг разметки и применяет позиции к окнам клиентов.
    /// </summary>
    public static class LayoutService
    {
        private static readonly string ConfigPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "TelegramLauncher", "layout.json");

        public static LayoutConfig Current { get; private set; } = Load();

        public static LayoutConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                    return JsonSerializer.Deserialize<LayoutConfig>(File.ReadAllText(ConfigPath)) ?? new LayoutConfig();
            }
            catch { /* ignore */ }
            return new LayoutConfig();
        }

        public static void Save(LayoutConfig cfg)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
                Current = cfg;
            }
            catch { /* ignore */ }
        }

        public static WF.Screen[] GetScreens() => WF.Screen.AllScreens;

        public static List<Rectangle> ComputeSlots(LayoutConfig cfg, int clientsCount, WF.Screen screen)
        {
            var area = screen.WorkingArea; // пиксели
            int W = area.Width, H = area.Height, X = area.X, Y = area.Y;

            // Если пользователь выделил клетки — используем их (по порядку)
            if (cfg.SelectedCells != null && cfg.SelectedCells.Count > 0)
            {
                var rects = new List<Rectangle>();
                int cols = Math.Max(1, cfg.DesignerCols);
                int rows = Math.Max(1, cfg.DesignerRows);
                int cellW = W / cols;
                int cellH = H / rows;

                foreach (var idx in cfg.SelectedCells.Take(clientsCount))
                {
                    int r = idx / cols;
                    int c = idx % cols;
                    rects.Add(new Rectangle(X + c * cellW, Y + r * cellH, cellW, cellH));
                }
                return rects;
            }

            // Иначе — режим
            string mode = (cfg.Mode ?? "Auto").ToLowerInvariant();
            return mode switch
            {
                "grid" => GridLayout(clientsCount, X, Y, W, H),
                "chaotic" => ChaoticLayout(clientsCount, X, Y, W, H, seed: Environment.TickCount),
                "custom" => CustomLayout(cfg.CustomCols, cfg.CustomRows, clientsCount, X, Y, W, H),
                _ => AutoLayout(clientsCount, X, Y, W, H),
            };
        }

        private static List<Rectangle> AutoLayout(int n, int X, int Y, int W, int H)
        {
            int cols = (int)Math.Ceiling(Math.Sqrt(n));
            int rows = (int)Math.Ceiling(n / (double)cols);
            return UniformGrid(n, cols, rows, X, Y, W, H);
        }

        private static List<Rectangle> GridLayout(int n, int X, int Y, int W, int H)
        {
            return AutoLayout(n, X, Y, W, H); // почти квадратная сетка
        }

        private static List<Rectangle> CustomLayout(int cols, int rows, int n, int X, int Y, int W, int H)
        {
            cols = Math.Max(1, cols);
            rows = Math.Max(1, rows);
            return UniformGrid(n, cols, rows, X, Y, W, H);
        }

        private static List<Rectangle> UniformGrid(int n, int cols, int rows, int X, int Y, int W, int H)
        {
            var list = new List<Rectangle>(n);
            int cellW = W / Math.Max(1, cols);
            int cellH = H / Math.Max(1, rows);

            for (int i = 0; i < n; i++)
            {
                int r = i / cols;
                int c = i % cols;
                if (r >= rows) break;
                list.Add(new Rectangle(X + c * cellW, Y + r * cellH, cellW, cellH));
            }
            return list;
        }

        private static List<Rectangle> ChaoticLayout(int n, int X, int Y, int W, int H, int seed)
        {
            int cols = 10, rows = 5;
            int cellW = W / cols, cellH = H / rows;
            var all = Enumerable.Range(0, cols * rows).ToList();
            var rnd = new Random(seed);
            all = all.OrderBy(_ => rnd.Next()).ToList();

            var rects = new List<Rectangle>();
            foreach (var idx in all.Take(n))
            {
                int r = idx / cols;
                int c = idx % cols;
                rects.Add(new Rectangle(X + c * cellW, Y + r * cellH, cellW, cellH));
            }
            return rects;
        }

        /* ===== Применение к окнам клиентов ===== */

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(
            IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int SW_SHOWNORMAL = 1;

        public static async Task ApplyCurrentLayoutToRunningClientsAsync(CancellationToken ct = default)
        {
            var cfg = Current;
            var screen = GetScreens().ElementAtOrDefault(Math.Max(0, cfg.MonitorIndex)) ?? WF.Screen.PrimaryScreen;

            var clients = ClientStore.Load().Where(c => !string.IsNullOrWhiteSpace(c.ExePath)).ToList();
            if (clients.Count == 0) return;

            var rects = ComputeSlots(cfg, clients.Count, screen);

            for (int i = 0; i < clients.Count && i < rects.Count; i++)
            {
                if (ct.IsCancellationRequested) break;

                var exe = clients[i].ExePath;
                string procName = Path.GetFileNameWithoutExtension(exe);

                var procs = Process.GetProcessesByName(procName);
                var p = procs.OrderByDescending(pr => SafeStartTime(pr)).FirstOrDefault();
                if (p == null) continue;

                try { p.WaitForInputIdle(3000); } catch { }

                IntPtr h = p.MainWindowHandle;
                if (h == IntPtr.Zero)
                {
                    await Task.Delay(500, ct);
                    p.Refresh();
                    h = p.MainWindowHandle;
                }
                if (h == IntPtr.Zero) continue;

                var r = rects[i];
                ShowWindow(h, SW_SHOWNORMAL);
                SetWindowPos(h, IntPtr.Zero, r.X, r.Y, Math.Max(200, r.Width), Math.Max(200, r.Height),
                    SWP_NOZORDER | SWP_SHOWWINDOW);
            }
        }

        private static DateTime SafeStartTime(Process p)
        {
            try { return p.StartTime; } catch { return DateTime.MinValue; }
        }
    }
}
