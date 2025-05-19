
using MediaDevices;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace CrosSave
{
    //TODO: select config save/backup save paths, optional backup with boolean,implement nier backup and other customs,loading screen,popup design
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
                    var dest = Path.Combine(switchSavePath, fileName);

                    // If file exists on Switch, delete it first
                    if (switchDevice.FileExists(dest))
                    {
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
    }
}