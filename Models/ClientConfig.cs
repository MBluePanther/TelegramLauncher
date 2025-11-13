
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TelegramLauncher.Models
{
    public enum ClientStatus
    {
        Active,
        Frozen,
        Crash
    }

    public sealed class ClientConfig : INotifyPropertyChanged
    {
        private string? _name;
        private string? _exePath;
        private string? _arguments;
        private ClientStatus _status;
        private bool _isSelected;
        private DateTime? _lastOpenedUtc;

        public string? Name { get => _name; set => SetField(ref _name, value); }
        public string? ExePath { get => _exePath; set => SetField(ref _exePath, value); }
        public string? Arguments { get => _arguments; set => SetField(ref _arguments, value); }
        public ClientStatus Status { get => _status; set => SetField(ref _status, value); }
        public bool IsSelected { get => _isSelected; set => SetField(ref _isSelected, value); }
        public DateTime? LastOpenedUtc { get => _lastOpenedUtc; set => SetField(ref _lastOpenedUtc, value); }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
        public override string ToString() => Name ?? base.ToString()!;
    }
}
