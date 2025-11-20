using System.Windows;

namespace TelegramLauncher
{
    public partial class MainWindow
    {
        private void OpenSorter_Click(object sender, RoutedEventArgs e)
        {
            var win = new TelegramLauncher.Views.SorterWindow();
            win.Owner = this;
            win.Show();
        }
    }
}
