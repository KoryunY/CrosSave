using PortableDeviceApiLib;
using System.Collections.ObjectModel;
using System.IO;
using System.Management; //??
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MediaDevices;
using System.Linq;

namespace CrosSave
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<GameItem> GameItems { get; set; } = new();
        public bool HasData => GameItems.Count > 0;
        //private const string DataFile = "E:\\Test\\game_data.json"; //TODO: read from configs or etc
        private const string DataFile = "C:\\test\\New folder\\game_data.json";
        //public string ImagePath = "https://tinfoil.media/ti/01003F601025E000/0/0/"
        public MainWindow()
        {
            InitializeComponent();
            //LoadGameData();
            LoadSwitchGameSaves();
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
            //LoadGameData();
            //GetSwitch();
            LoadSwitchGameSaves();
        }

        private const string SwitchDeviceName = "Switch";
        private const string SavesPath = @"7: Saves\Installed games";

        private void LoadSwitchGameSaves()
        {
            GameItems.Clear();

            // Find Switch MTP device
            var devices = MediaDevice.GetDevices();
            var switchDevice = devices.FirstOrDefault(d => d.FriendlyName.Contains(SwitchDeviceName, StringComparison.OrdinalIgnoreCase));
            if (switchDevice == null)
            {
                // No Switch found
                GameItems.Clear();
                NoDataToShow();
                return;
            }

            using (switchDevice)
            {
                switchDevice.Connect();
                if (!switchDevice.DirectoryExists(SavesPath))
                {
                    // Path not found
                    GameItems.Clear();
                    NoDataToShow();
                    return;
                }

                // Enumerate games
                var gameFolders = switchDevice.GetDirectories(SavesPath);
                foreach (var gameFolder in gameFolders)
                {
                    var gameId = System.IO.Path.GetFileName(gameFolder);
                    // Enumerate user folders
                    var userFolders = switchDevice.GetDirectories(System.IO.Path.Combine(SavesPath, gameId));
                    foreach (var userFolder in userFolders)
                    {
                        var userId = System.IO.Path.GetFileName(userFolder);
                        // Check if config exists for this game/user
                        var config = FindConfigForGameUser(gameId, userId);
                        if (config != null)
                        {
                            GameItems.Add(new GameItem
                            {
                                Name = config.Name,
                                GameId = gameId,
                                ConfigPath = config.ConfigPath,
                                // Add more fields as needed
                            });
                        }
                    }
                }
                switchDevice.Disconnect();
            }

            if (GameItems.Count == 0)
            {
                NoDataToShow();
            }
        }

        private void NoDataToShow()
        {
            // You can bind a property to your UI to show "No data to show"
            // For example, set a bool property and use a TextBlock with a trigger in XAML
        }

        private GameItem? FindConfigForGameUser(string gameId, string userId)
        {
            // Load your local JSON config and find a match for gameId and userId
            // For now, just return null if not found
            // Example:
            return GameItems.FirstOrDefault(g => g.GameId == gameId /* && g.UserId == userId */);
        }


        private void GameItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount>0)
            {
                if (sender is Border border && border.DataContext is GameItem item)
                {
                    var popup = new ConfigPopup(item);
                    popup.WindowStartupLocation =WindowStartupLocation.CenterOwner;
                    popup.Owner = this;
                    popup.ShowDialog();
                    SaveGameData();
                }
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
        public string GameId { get; set; } = string.Empty;
        public string ConfigPath { get; set; } = string.Empty;
        public bool IsConfigured => !string.IsNullOrEmpty(ConfigPath);
        public string ImageUrl => $"https://tinfoil.media/ti/{GameId}/0/0/";
    }
}
