using PortableDeviceApiLib;
using System.Collections.ObjectModel;
using System.IO;
using System.Management; //??
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace CrosSave
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<GameItem> GameItems { get; set; } = new();
        private const string DataFile = "E:\\Test\\game_data.json"; //TODO: read from configs or etc
        //private const string DataFile = "C:\\test\\New folder\\game_data.json";

        public MainWindow()
        {
            InitializeComponent();
            LoadGameData();
            DataContext = this;
        }

        private void LoadGameData()
        {
            if (File.Exists(DataFile)) //TODO:Fix data load logic
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
            GetSwitch();
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

        private void GetSwitch()
        {
            //mtp:/Switch/saves/
            string query = "SELECT * FROM Win32_PnPEntity WHERE PNPClass = 'Portable Devices'";

            ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
            foreach (ManagementObject device in searcher.Get())
            {
                Console.WriteLine($"Device: {device["Name"]}, ID: {device["DeviceID"]}");
            }

            var deviceManager = new PortableDeviceManager();
            uint deviceCount = 1;
            deviceManager.GetDevices(null, ref deviceCount);
            if (deviceCount == 0)
            {
                Console.WriteLine("No MTP devices found.");
                return;
            }

            string[] deviceIDs = new string[deviceCount];
            deviceManager.GetDevices(ref deviceIDs[0], ref deviceCount);

            foreach (var deviceID in deviceIDs) //https://stackoverflow.com/questions/6162046/enumerating-windows-portable-devices-in-c-sharp
            {
                Console.WriteLine($"Connected MTP Device: {deviceID}");
                // Here, you can browse and copy files from Switch's save directory
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
