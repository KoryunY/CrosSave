using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MediaDevices;
using System.ComponentModel; 

namespace CrosSave
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollection<GameItem> GameItems { get; set; } = new();
        private ObservableCollection<GameItem> AllGameItems { get; set; } = new();
        private static readonly string DataFile = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CrosSave",
                    "game_data.json"
                );
        private const string InstalledGamesPath = @"4: Installed games";
        private const string SwitchDeviceName = "Switch";
        private const string SavesPath = @"7: Saves\Installed games";
        public event PropertyChangedEventHandler? PropertyChanged;
        private Visibility _noDataVisibility = Visibility.Collapsed;
        public Visibility NoDataVisibility
        {
            get => _noDataVisibility;
            set
            {
                if (_noDataVisibility != value)
                {
                    _noDataVisibility = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NoDataVisibility)));
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            LoadSwitchGameSaves();
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
                    if (data != null)
                    {
                        AllGameItems = data;
                    }
                }
            }
        }

        private void SaveGameData()
        {
            var dir = Path.GetDirectoryName(DataFile);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir!);

            var json = JsonSerializer.Serialize(AllGameItems, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(DataFile, json);
        }
        private void RefreshData_Click(object sender, RoutedEventArgs e)
        {
            LoadSwitchGameSaves();
        }

        private void LoadSwitchGameSaves()
        {
            // Load all saved data first
            LoadGameData();

            var devices = MediaDevice.GetDevices();
            var switchDevice = devices.FirstOrDefault(d => d.FriendlyName.Contains(SwitchDeviceName, StringComparison.OrdinalIgnoreCase));
            if (switchDevice == null)
            {
                GameItems.Clear();
                NoDataToShow();
                return;
            }

            var installedGameIds = new HashSet<string>();

            using (switchDevice)
            {
                switchDevice.Connect();
                if (!switchDevice.DirectoryExists(InstalledGamesPath))
                {
                    GameItems.Clear();
                    NoDataToShow();
                    return;
                }

                // Enumerate game name folders
                var gameNameFolders = switchDevice.GetDirectories(InstalledGamesPath);
                foreach (var gameNameFolder in gameNameFolders)
                {
                    var gameName = Path.GetFileName(gameNameFolder);
                    // Enumerate game ID folders inside each game name folder
                    var gameIdFolders = switchDevice.GetDirectories(Path.Combine(InstalledGamesPath, gameName));
                    foreach (var gameIdFolder in gameIdFolders)
                    {
                        var gameId = Path.GetFileName(gameIdFolder);
                        installedGameIds.Add(gameId);

                        // Enumerate user folders
                        /*var userFolders = switchDevice.GetDirectories(System.IO.Path.Combine(SavesPath, gameId));
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
                        }*/
                        // Try to find existing item in AllGameItems
                        var existing = AllGameItems.FirstOrDefault(g => g.GameId == gameId);
                        if (existing != null)
                        {
                            // Update name if changed
                            existing.Name = gameName;
                        }
                        else
                        {
                            AllGameItems.Add(new GameItem
                            {
                                Name = gameName,
                                GameId = gameId,
                                ConfigPath = "",
                            });
                        }
                    }
                }
                switchDevice.Disconnect();
            }

            // Only show items that are currently installed
            GameItems.Clear();
            foreach (var item in AllGameItems.Where(g => installedGameIds.Contains(g.GameId)))
            {
                GameItems.Add(item);
            }

            SaveGameData();

            if (GameItems.Count == 0)
            {
                NoDataToShow();
            }
            else
            {
                NoDataVisibility = Visibility.Collapsed;
            }
        }

        private void NoDataToShow()
        {
            NoDataVisibility = Visibility.Visible;
        }

        private GameItem? FindConfigForGameUser(string gameId, string userId)
        {
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
