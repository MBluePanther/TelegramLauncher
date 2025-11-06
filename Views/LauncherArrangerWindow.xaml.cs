using MahApps.Metro.Controls;
using System.Windows.Controls;
using TelegramLauncher.ViewModels;

namespace TelegramLauncher.Views
{
    public partial class LauncherArrangerWindow : MetroWindow
    {
        private ArrangerViewModel? VM => DataContext as ArrangerViewModel;

        public LauncherArrangerWindow()
        {
            InitializeComponent();
        }

        // Синхронизируем выделение ListBox с флагом IsChecked у VM
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