
using MediaDevices;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace CrosSave
{
    //TODO: select config save/backup save paths, optional backup with boolean,implement other customs,loading screen,popup design/custom steamID
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
            var dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                GameItem.ConfigPath = dialog.FolderName;
            }
        }

        private void Push_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var mainWindow = Application.Current.Windows[0] as MainWindow;
                var user = mainWindow?.SelectedUser?.UserId ?? "";
                var gameName = GameItem.Name;
                var pcSavePath = GameItem.ConfigPath;
                var steamId64 = MainWindow.LoadAppSettings().SteamId64;

                if (steamId64 == null)
                {
                    MessageBox.Show("SteamID64 is not configured in settings. Please set it in the app settings.");
                    return;
                }

                if (string.IsNullOrEmpty(pcSavePath) || !Directory.Exists(pcSavePath))
                {
                    MessageBox.Show("PC save path is not configured or does not exist.");
                    return;
                }

                var devices = MediaDevice.GetDevices();
                var switchDevice = devices.FirstOrDefault(d => d.FriendlyName.Contains("Switch", StringComparison.OrdinalIgnoreCase));
                if (switchDevice == null)
                {
                    MessageBox.Show("Switch not found.");
                    return;
                }

                string switchSavePath = Path.Combine(MainWindow.SavesPath, gameName, user);

                switchDevice.Connect();
                if (!switchDevice.DirectoryExists(switchSavePath))
                {
                    MessageBox.Show("Switch save path does not exist.");
                    switchDevice.Disconnect();
                    return;
                }

                // Backup PC files before overwrite
                BackupFiles(pcSavePath, "pc");

                // Copy from Switch to PC (overwrite)
                var switchFiles = switchDevice.GetFiles(switchSavePath);
                foreach (var file in switchFiles)
                {
                    var fileName = Path.GetFileName(file);
                    var dest = Path.Combine(pcSavePath, fileName);
                    switchDevice.DownloadFile(file, dest);
                }

                if (GameItem.GameId == "0100B8E016F76000") // NieR:Automata
                {
                    string[] nierFiles = Directory.GetFiles(pcSavePath);
                    foreach (var filePath in nierFiles)
                    {
                        PatchNieRAutomataSteamID64(filePath, steamId64.Value);
                    }
                }

                switchDevice.Disconnect();
                MessageBox.Show("Pushed Switch saves to PC and backed up PC files.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Push failed: " + ex.Message);
            }
        }

        private void Pull_Click(object sender, RoutedEventArgs e)
        {
            MediaDevice? switchDevice = null;
            try
            {
                var mainWindow = Application.Current.Windows[0] as MainWindow;
                var user = mainWindow?.SelectedUser?.UserId ?? "";
                var gameName = GameItem.Name;
                var pcSavePath = GameItem.ConfigPath;

                if (string.IsNullOrEmpty(pcSavePath) || !Directory.Exists(pcSavePath))
                {
                    MessageBox.Show("PC save path is not configured or does not exist.");
                    return;
                }

                var devices = MediaDevice.GetDevices();
                switchDevice = devices.FirstOrDefault(d => d.FriendlyName.Contains("Switch", StringComparison.OrdinalIgnoreCase));
                if (switchDevice == null)
                {
                    MessageBox.Show("Switch not found.");
                    return;
                }

                string switchSavePath = Path.Combine(MainWindow.SavesPath, gameName, user);

                switchDevice.Connect();
                if (!switchDevice.DirectoryExists(switchSavePath))
                {
                    MessageBox.Show("Switch save path does not exist.");
                    return;
                }

                // Backup Switch files before overwrite
                BackupSwitchFiles(switchDevice, switchSavePath, "switch");

                // Copy from PC to Switch (overwrite)
                foreach (var file in Directory.GetFiles(pcSavePath))
                {
                    var fileName = Path.GetFileName(file);
                    string dest;

                    if (GameItem.GameId == "0100B8E016F76000" && fileName.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
                    {
                        // Remove .dat extension for Switch
                        var baseName = Path.GetFileNameWithoutExtension(file);

                        // Delete both extensionless and .dat files on Switch (if they exist)
                        var destNoExt = Path.Combine(switchSavePath, baseName);
                        var destWithExt = Path.Combine(switchSavePath, baseName + ".dat");

                        if (switchDevice.FileExists(destNoExt))
                            switchDevice.DeleteFile(destNoExt);
                        if (switchDevice.FileExists(destWithExt))
                            switchDevice.DeleteFile(destWithExt);

                        // Upload as extensionless
                        dest = destNoExt;
                    }
                    else
                    {
                        dest = Path.Combine(switchSavePath, fileName);

                        // Delete file with same name if it exists
                        if (switchDevice.FileExists(dest))
                            switchDevice.DeleteFile(dest);
                    }

                    switchDevice.UploadFile(file, dest);
                }

                MessageBox.Show("Pulled PC saves to Switch and backed up Switch files.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Pull failed: " + ex.Message);
            }
            finally
            {
                // Always disconnect to avoid device lock
                if (switchDevice != null && switchDevice.IsConnected)
                {
                    switchDevice.Disconnect();
                }
            }
        }

        private void BackupFiles(string sourceDir, string type)
        {
            // Backup to: <AppData>\CrosSaveBackups\<GameName>\<type>_yyyyMMdd_HHmmss\
            var backupRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CrosSaveBackups",
                GameItem.Name,
                $"{type}_{DateTime.Now:yyyyMMdd_HHmmss}"
            );
            Directory.CreateDirectory(backupRoot);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var dest = Path.Combine(backupRoot, Path.GetFileName(file));
                File.Copy(file, dest, true);
            }
        }

        private void BackupSwitchFiles(MediaDevice switchDevice, string switchSavePath, string type)
        {
            var backupRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CrosSaveBackups",
                GameItem.Name,
                $"{type}_{DateTime.Now:yyyyMMdd_HHmmss}"
            );
            Directory.CreateDirectory(backupRoot);

            var switchFiles = switchDevice.GetFiles(switchSavePath);
            foreach (var file in switchFiles)
            {
                var fileName = Path.GetFileName(file);
                var dest = Path.Combine(backupRoot, fileName);
                switchDevice.DownloadFile(file, dest);
            }
        }

        public static void PatchNieRAutomataSteamID64(string filePath, ulong newSteamId64)
        {
            // Determine offset: SlotData_*.dat = 4, others = 0
            int offset = filePath.Contains("SlotData", StringComparison.OrdinalIgnoreCase) ? 4 : 0;

            // Patch SteamID64
            byte[] idBytes = BitConverter.GetBytes(newSteamId64); // little-endian
            byte[] fileBytes = File.ReadAllBytes(filePath);
            if (fileBytes.Length < offset + 8)
                throw new InvalidDataException("File too short to contain a SteamID64 at the expected offset.");

            Array.Copy(idBytes, 0, fileBytes, offset, 8);
            File.WriteAllBytes(filePath+".dat", fileBytes);
            File.Delete(filePath);
        }
    }
}