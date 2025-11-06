using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TelegramLauncher.Models;
using TelegramLauncher.ViewModels;

namespace TelegramLauncher.Views
{
    public partial class LauncherArrangerWindow : MahApps.Metro.Controls.MetroWindow
    {
        public LauncherArrangerWindow()
        {
            InitializeComponent();

            // Рантайм-DataContext задаём здесь — дизайнеру так проще жить.
            if (DataContext == null)
                DataContext = new ArrangerViewModel();
        }

        public LauncherArrangerWindow(IEnumerable<ClientConfig> clients) : this()
        {
            if (this.DataContext is ArrangerViewModel vm && clients != null)
            {
                foreach (var c in clients)
                    vm.Clients.Add(new ClientItemVM(c));

                vm.RaiseCounters();
            }
        }
    }
}
