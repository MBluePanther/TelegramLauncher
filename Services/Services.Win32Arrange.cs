using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TelegramLauncher.Services
{
    internal static class Win32Arrange
    {
        public static void ArrangeProcessesOnScreen(IReadOnlyList<Process> processes, Screen screen, IReadOnlyList<System.Drawing.Rectangle> targetRects)
        {
            for (int i = 0; i < processes.Count && i < targetRects.Count; i++)
            {
                var p = processes[i];
                var rect = targetRects[i];

                var hWnd = WaitForMainWindow(p, 5000);
                if (hWnd == IntPtr.Zero) continue;

                if (IsIconic(hWnd)) ShowWindow(hWnd, SW_RESTORE);
                SetWindowPos(hWnd, IntPtr.Zero, rect.X, rect.Y, rect.Width, rect.Height,
                             SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
        }

        private static IntPtr WaitForMainWindow(Process p, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var h = GetVisibleTopWindowForProcess(p.Id);
                if (h != IntPtr.Zero) return h;
                Thread.Sleep(120);
                try { p.Refresh(); } catch {}
            }
            return IntPtr.Zero;
        }

        private static IntPtr GetVisibleTopWindowForProcess(int pid)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows((h, l) =>
            {
                GetWindowThreadProcessId(h, out uint wpid);
                if (wpid == pid && IsWindowVisible(h))
                {
                    found = h;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        // P/Invoke
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public const int SW_RESTORE = 9;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;
    }
}