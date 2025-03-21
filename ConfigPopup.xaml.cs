using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace CrosSave
{
    public partial class ConfigPopup : Window
    {
        private GameItem GameItem { get; }

        public ConfigPopup(GameItem item)
        {
            InitializeComponent();
            GameItem = item;
            DataContext = GameItem;
        }

        private void SelectFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            if (dialog.ShowDialog() == true)
            {
                GameItem.ConfigPath = dialog.FileName;
            }
        }

        private void Push_Click(object sender, RoutedEventArgs e)
        {
            // Mock push logic
            MessageBox.Show("Pushed data for " + GameItem.Name);
        }

        private void Pull_Click(object sender, RoutedEventArgs e)
        {
            // Mock pull logic
            MessageBox.Show("Pulled data for " + GameItem.Name);
        }
    }
}
