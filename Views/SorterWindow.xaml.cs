using System.Windows;
using TelegramLauncher.ViewModels;

namespace TelegramLauncher.Views
{
    public partial class SorterWindow : MahApps.Metro.Controls.MetroWindow
    {
        public SorterWindow()
        {
            InitializeComponent();
            DataContext = new SorterViewModel();
        }
    }
}
