
using System.Windows;
using System.Windows.Controls;
using TelegramLauncher.Models;

namespace TelegramLauncher.Views
{
    public partial class ClientStatusDialog : MahApps.Metro.Controls.MetroWindow
    {
        public ClientStatus? SelectedStatus { get; private set; }

        public ClientStatusDialog(ClientConfig client)
        {
            InitializeComponent();
            Subtitle.Text = $"Клиент: {client?.Name ?? System.IO.Path.GetFileNameWithoutExtension(client?.ExePath ?? "")}";
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (StatusBox.SelectedItem is ComboBoxItem item && item.Tag is ClientStatus st)
            {
                SelectedStatus = st;
                DialogResult = true;
            }
            else
            {
                DialogResult = false;
            }
            Close();
        }
    }
}
