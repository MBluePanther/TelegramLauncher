using MahApps.Metro.Controls;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using TelegramLauncher.Models;
using TelegramLauncher.ViewModels;

namespace TelegramLauncher.Views
{
    public partial class LauncherArrangerWindow : MetroWindow
    {
        private ArrangerViewModel? VM => DataContext as ArrangerViewModel;

        public LauncherArrangerWindow()
        {
            InitializeComponent();
            EnsureDataContext();
        }

        public LauncherArrangerWindow(object? arg) : this()
        {
            switch (arg)
            {
                case ArrangerViewModel vm:
                    DataContext = vm;
                    break;
                case IEnumerable<ClientConfig> clients:
                    DataContext = new ArrangerViewModel(clients);
                    break;
                case Window owner:
                    Owner = owner;
                    break;
            }
            EnsureDataContext();
        }

        private void EnsureDataContext()
        {
            if (DataContext is ArrangerViewModel) return;

            try
            {
                var path = Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                    "TelegramLauncher", "clients.json");

                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var list = JsonSerializer.Deserialize<List<ClientConfig>>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ClientConfig>();
                    DataContext = new ArrangerViewModel(list);
                    return;
                }
            }
            catch { /* ignore and build empty VM */ }

            DataContext = new ArrangerViewModel(null);
        }

        private void ClientsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VM is null) return;

            foreach (var it in e.AddedItems)
                if (it is ClientItemVM c) c.IsChecked = true;

            foreach (var it in e.RemovedItems)
                if (it is ClientItemVM c) c.IsChecked = false;

            VM.RaiseCounters();
        }
    }
}