
using System.Windows;
using System.Windows.Controls;
using TelegramLauncher.Models;

namespace TelegramLauncher.Views
{
    public partial class ClientStatusDialog : MahApps.Metro.Controls.MetroWindow
    {
        public ClientStatus? SelectedStatus { get; private set; }
        public string? Comment { get; private set; }

        public ClientStatusDialog(ClientConfig client)
        {
            InitializeComponent();
            Subtitle.Text = $"Клиент: {client?.Name ?? System.IO.Path.GetFileNameWithoutExtension(client?.ExePath ?? "")}";
            try { CommentBox.Text = client?.Comment ?? ""; } catch { /* если TextBox ещё не создан в InitializeComponent */ }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (StatusBox.SelectedItem is ComboBoxItem item && item.Tag is ClientStatus st)
            {
                SelectedStatus = st;
                Comment = string.IsNullOrWhiteSpace(CommentBox.Text) ? null : CommentBox.Text.Trim();
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
