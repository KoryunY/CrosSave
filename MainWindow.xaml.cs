using MediaDevices;
using System.Collections.ObjectModel;
using System.ComponentModel; 
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CrosSave
{
    public class UserProfile
    {
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private static readonly string DataFile = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CrosSave",
                    "game_data.json"
                );
        public static string InstalledGamesPath = @"4: Installed games";
        public static string SwitchDeviceName = "Switch";
        public static string SavesPath = @"7: Saves\Installed games";

        private Dictionary<string, List<GameItem>> UserGameItems { get; set; } = new();
        public ObservableCollection<GameItem> GameItems { get; set; } = new();
        private ObservableCollection<GameItem> AllGameItems { get; set; } = new();
        public ObservableCollection<UserProfile> UserProfiles { get; set; } = new();
        private UserProfile? _selectedUser;
        public UserProfile? SelectedUser
        {
            get => _selectedUser;
            set
            {
                if (_selectedUser != value)
                {
                    _selectedUser = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedUser)));
                    LoadGamesForSelectedUser();
                }
            }
        }

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
            DataContext = this;
            LoadUserProfiles();
        }

        private void LoadUserProfiles()
        {
            var devices = MediaDevice.GetDevices();
            var switchDevice = devices.FirstOrDefault(d => d.FriendlyName.Contains(SwitchDeviceName, StringComparison.OrdinalIgnoreCase));
            if (switchDevice == null)
            {
                UserProfiles.Clear();
                return;
            }

            var userNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (switchDevice)
            {
                switchDevice.Connect();
                if (switchDevice.DirectoryExists(SavesPath))
                {
                    var gameNameFolders = switchDevice.GetDirectories(SavesPath);
                    foreach (var gameNameFolder in gameNameFolders)
                    {
                        var userFolders = switchDevice.GetDirectories(gameNameFolder);
                        foreach (var userFolder in userFolders)
                        {
                            var userName = Path.GetFileName(userFolder);
                            userNames.Add(userName);
                        }
                    }
                }
                switchDevice.Disconnect();
            }

            UserProfiles.Clear();
            foreach (var userName in userNames)
                UserProfiles.Add(new UserProfile { UserId = userName, DisplayName = userName });

            if (SelectedUser == null && UserProfiles.Count > 0)
                SelectedUser = UserProfiles[0];
        }

        private void LoadGameData()
        {
            if (File.Exists(DataFile))
            {
                var json = File.ReadAllText(DataFile);
                var data = JsonSerializer.Deserialize<Dictionary<string, List<GameItem>>>(json);
                if (data != null)
                    UserGameItems = data;
            }
        }

        private void LoadGamesForSelectedUser()
        {
            LoadGameData();

            var devices = MediaDevice.GetDevices();
            var switchDevice = devices.FirstOrDefault(d => d.FriendlyName.Contains(SwitchDeviceName, StringComparison.OrdinalIgnoreCase));
            if (switchDevice == null)
            {
                GameItems.Clear();
                NoDataToShow();
                return;
            }

            var gameIdToName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            using (switchDevice)
            {
                switchDevice.Connect();

                // Build gameIdToName map from installed games
                if (switchDevice.DirectoryExists(InstalledGamesPath))
                {
                    var gameNameFolders = switchDevice.GetDirectories(InstalledGamesPath);
                    foreach (var gameNameFolder in gameNameFolders)
                    {
                        var gameName = Path.GetFileName(gameNameFolder);
                        var gameIdFolders = switchDevice.GetDirectories(Path.Combine(InstalledGamesPath, gameName));
                        foreach (var gameIdFolder in gameIdFolders)
                        {
                            var gameId = Path.GetFileName(gameIdFolder);
                            gameIdToName[gameId] = gameName;
                        }
                    }
                }

                // For selected user, enumerate all their games
                GameItems.Clear();
                if (SelectedUser != null && switchDevice.DirectoryExists(SavesPath))
                {
                    var gameNameFolders = switchDevice.GetDirectories(SavesPath);
                    foreach (var gameNameFolder in gameNameFolders)
                    {
                        var gameName = Path.GetFileName(gameNameFolder);
                        var userFolders = switchDevice.GetDirectories(gameNameFolder);
                        foreach (var userFolder in userFolders)
                        {
                            var userName = Path.GetFileName(userFolder);
                            if (!userName.Equals(SelectedUser.UserId, StringComparison.OrdinalIgnoreCase))
                                continue;

                            // Find gameId for this gameName
                            var gameId = gameIdToName.FirstOrDefault(x => x.Value == gameName).Key ?? string.Empty;
                            if (string.IsNullOrEmpty(gameId))
                                continue;

                            // Try to get config info from JSON
                            var config = UserGameItems.GetValueOrDefault(userName)?.FirstOrDefault(g => g.GameId == gameId);

                            GameItems.Add(new GameItem
                            {
                                Name = gameName,
                                GameId = gameId,
                                ConfigPath = config?.ConfigPath ?? string.Empty
                            });
                        }
                    }
                }

                switchDevice.Disconnect();
            }

            // Only store configured games in JSON for the selected user
            if (SelectedUser != null)
                UserGameItems[SelectedUser.UserId] = GameItems.Where(g => !string.IsNullOrEmpty(g.ConfigPath)).ToList();

            if (GameItems.Count == 0)
                NoDataToShow();
            else
                NoDataVisibility = Visibility.Collapsed;
        }

        private void SaveGameData()
        {
            // Only save configured items
            var toSave = UserGameItems.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Where(g => !string.IsNullOrEmpty(g.ConfigPath)).ToList()
            );
            var dir = Path.GetDirectoryName(DataFile);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir!);

            var json = JsonSerializer.Serialize(toSave, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(DataFile, json);
        }

        private void RefreshData_Click(object sender, RoutedEventArgs e)
        {
            //LoadSwitchGameSaves();
            LoadUserProfiles();
        }


        private void NoDataToShow()
        {
            NoDataVisibility = Visibility.Visible;
        }

        private void GameItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount > 0)
            {
                if (sender is Border border && border.DataContext is GameItem item)
                {
                    var popup = new ConfigPopup(item);
                    popup.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    popup.Owner = this;
                    popup.ShowDialog();

                    // Update UserGameItems for the current user
                    if (SelectedUser != null)
                    {
                        // Remove any previous entry for this GameId
                        var userList = UserGameItems.GetValueOrDefault(SelectedUser.UserId) ?? new List<GameItem>();
                        userList.RemoveAll(g => g.GameId == item.GameId);

                        // Only add if configured
                        if (!string.IsNullOrEmpty(item.ConfigPath))
                            userList.Add(new GameItem
                            {
                                Name = item.Name,
                                GameId = item.GameId,
                                ConfigPath = item.ConfigPath
                            });

                        UserGameItems[SelectedUser.UserId] = userList;
                        SaveGameData();
                    }
                }
            }
        }
    }

    public class GameItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _gameId = string.Empty;
        private string _configPath = string.Empty;

        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value; OnPropertyChanged(nameof(Name)); } }
        }

        public string GameId
        {
            get => _gameId;
            set { if (_gameId != value) { _gameId = value; OnPropertyChanged(nameof(GameId)); } }
        }

        public string ConfigPath
        {
            get => _configPath;
            set { if (_configPath != value) { _configPath = value; OnPropertyChanged(nameof(ConfigPath)); OnPropertyChanged(nameof(IsConfigured)); } }
        }

        [JsonIgnore]
        public bool IsConfigured => !string.IsNullOrEmpty(ConfigPath);

        public string ImageUrl => $"https://tinfoil.media/ti/{GameId}/0/0/";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    /*public class GameItem
    {
        public string Name { get; set; } = string.Empty;
        public string GameId { get; set; } = string.Empty;
        public string ConfigPath { get; set; } = string.Empty;
        [JsonIgnore]
        public bool IsConfigured => !string.IsNullOrEmpty(ConfigPath);
        public string ImageUrl => $"https://tinfoil.media/ti/{GameId}/0/0/";
    }*/
}
