using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace CrosSave
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<GameItem> GameItems { get; set; } = new();
        private const string DataFile = "C:\\test\\New folder\\game_data.json";

        public MainWindow()
        {
            InitializeComponent();
            LoadGameData();
            DataContext = this;
        }

        private void LoadGameData()
        {
            if (File.Exists(DataFile))
            {
                var json = File.ReadAllText(DataFile);
                var data = JsonSerializer.Deserialize<ObservableCollection<GameItem>>(json);

                if (data != null)
                {
                    GameItems.Clear();
                    foreach (var item in data)
                    {
                        GameItems.Add(item);
                    }
                }
            }
        }

        private void SaveGameData()
        {
            var json = JsonSerializer.Serialize(GameItems, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(DataFile, json);
        }

        private void RefreshData_Click(object sender, RoutedEventArgs e)
        {
            LoadGameData();
        }

        private void OpenPopup_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is GameItem item)
            {
                var popup = new ConfigPopup(item);
                popup.ShowDialog();
                SaveGameData();
            }
        }
    }

    public class GameItem
    {
        public string Name { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
        public string ConfigPath { get; set; } = string.Empty;
        public bool IsConfigured => !string.IsNullOrEmpty(ConfigPath);
    }

    
}
