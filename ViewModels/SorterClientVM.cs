
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using TelegramLauncher.Models;

namespace TelegramLauncher.ViewModels
{
    public class SorterClientVM : INotifyPropertyChanged
    {
        private bool _isChecked;
        public bool IsChecked { get => _isChecked; set { _isChecked = value; OnPropertyChanged(); } }

        public ClientConfig Client { get; }
        public string Name => Client.Name;
        public ClientStatus Status => Client.Status;
        public string ExePath => Client.ExePath;

        // Для будущего — если начнёшь писать дату последнего запуска в JSON/модель
        public DateTime? LastLaunchedAt { get; }

        public string SourceFolder => SafeDirName(ExePath);
        public string Id => System.IO.Path.GetFileName(SourceFolder);
        public bool TDataExists => Directory.Exists(System.IO.Path.Combine(SourceFolder, "tdata"));

        public int StatusOrder => Status switch
        {
            ClientStatus.Active => 0,
            ClientStatus.Frozen => 1,
            ClientStatus.Crash => 2,
            _ => 9
        };

        public Color StatusColor => StatusOrder switch
        {
            0 => Colors.LimeGreen,
            1 => Colors.Goldenrod,
            2 => Colors.IndianRed,
            _ => Colors.Gray
        };

        public SorterClientVM(ClientConfig client, DateTime? lastLaunchedAt = null)
        {
            Client = client;
            LastLaunchedAt = lastLaunchedAt;
        }

        private static string SafeDirName(string exe)
        {
            try { return System.IO.Path.GetDirectoryName(exe); } catch { return string.Empty; }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string prop = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
