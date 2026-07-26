using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using RelayLobby.Contracts;
using RelayLobby.Domain;

namespace RelayLobby.Client.Duplex
{
    public partial class RoomWindow : Window
    {
        private readonly string _username;
        private readonly string _roomName;
        private readonly ILobbyDuplexService _client;
        private readonly LobbyWindow _lobbyWindow;
        private DispatcherTimer lobbyTimer;
        private DateTime joinTime;
        private Player selectedPlayer;
        private bool _returningToLobby;

        public RoomWindow(
            string username,
            string roomName,
            ILobbyDuplexService client,
            LobbyWindow lobbyWindow)
        {
            try
            {
                InitializeComponent();
                _username = username ?? throw new ArgumentNullException(nameof(username), "Username cannot be null.");
                _roomName = roomName ?? throw new ArgumentNullException(nameof(roomName), "Room name cannot be null.");
                _client = client ?? throw new ArgumentNullException(nameof(client), "Client interface cannot be null.");
                _lobbyWindow = lobbyWindow ?? throw new ArgumentNullException(nameof(lobbyWindow));
                txtRoomName.Text = _roomName;
                joinTime = DateTime.Now;
                SetupLobbyTimer();
                UpdateDisplay(null, null, null, null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize RoomWindow: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
        }

        private void SetupLobbyTimer()
        {
            try
            {
                lobbyTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                lobbyTimer.Tick += (sender, e) =>
                {
                    TimeSpan elapsed = DateTime.Now - joinTime;
                    txtLobbyTimer.Text = elapsed.ToString(@"hh\:mm\:ss");
                };
                lobbyTimer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to set up lobby timer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void UpdateDisplay(List<Message> publicMessages,
                                 List<Message> privateMessages,
                                 List<SharedFile> publicFiles,
                                 List<SharedFile> privateFiles,
                                 List<Player> players)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (publicMessages != null)
                        lstMessages.ItemsSource = publicMessages.Select(m => m.ToString());

                    if (players != null)
                    {
                        var currentIndex = lstPlayers.SelectedIndex;
                        var displayPlayers = players.Select(p => new PlayerDisplayWrapper(p, _username)).ToList();
                        lstPlayers.ItemsSource = displayPlayers;
                        txtPlayerCount.Text = players.Count.ToString();
                        if (currentIndex >= 0 && currentIndex < displayPlayers.Count)
                        {
                            lstPlayers.SelectedIndex = currentIndex;
                            selectedPlayer = players[currentIndex];
                        }
                        else if (selectedPlayer != null)
                        {
                            var restoredIndex = players.IndexOf(selectedPlayer);
                            if (restoredIndex >= 0)
                            {
                                lstPlayers.SelectedIndex = restoredIndex;
                            }
                        }
                    }

                    if (publicFiles != null)
                        lstFiles.ItemsSource = publicFiles;
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update display: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SendMessage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string message = txtMessage.Text?.Trim();
                if (string.IsNullOrWhiteSpace(message) ||
                    message.Length > TransportConfiguration.MaxMessageLength)
                {
                    MessageBox.Show(
                        $"Message must be 1-{TransportConfiguration.MaxMessageLength} characters.",
                        "Invalid input",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                await Task.Run(() => _client.SendPublicMessage(_username, message));
                txtMessage.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to send message: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrivateMsg_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (lstPlayers.SelectedItem is PlayerDisplayWrapper p && p.OriginalPlayer.Name != _username)
                {
                    selectedPlayer = p.OriginalPlayer;
                    new PrivateChatWindow(_username, p.OriginalPlayer.Name, _client).Show();
                }
                else
                {
                    MessageBox.Show("Select a different player for private chat.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open private chat: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LeaveRoom_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Task.Run(() => _client.LeaveRoom(_username));
                _returningToLobby = true;
                lobbyTimer.Stop();
                _lobbyWindow.ReturnToLobby();
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to leave room: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void UploadFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new OpenFileDialog
                {
                    Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp|Text Files|*.txt|All Files|*.*"
                };

                if (dlg.ShowDialog() != true)
                    return;

                var fileInfo = new FileInfo(dlg.FileName);
                if (fileInfo.Length > TransportConfiguration.MaxFileBytes)
                {
                    MessageBox.Show("File too large (max 10MB).", "Invalid File", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                byte[] file = File.ReadAllBytes(dlg.FileName);
                await Task.Run(() => _client.SendPublicFile(_username, Path.GetFileName(dlg.FileName), file));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to upload file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string GuessExt(byte[] data)
        {
            if (data == null || data.Length < 4) return "";
            if (data[0] == 0xFF && data[1] == 0xD8) return ".jpg";
            if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47) return ".png";
            if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46) return ".gif";
            if (data[0] == 0x42 && data[1] == 0x4D) return ".bmp";
            if (data[0] == 0x25 && data[1] == 0x50 && data[2] == 0x44 && data[3] == 0x46) return ".pdf";
            if (data[0] == 0x50 && data[1] == 0x4B) return ".zip";
            return "";
        }

        private void Hyperlink_DownloadFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Hyperlink hyperlink && hyperlink.DataContext is SharedFile file && file.FileData != null)
                {
                    var baseName = string.IsNullOrWhiteSpace(file.FileName) ? "download" : file.FileName;
                    var ext = Path.GetExtension(baseName);

                    if (string.IsNullOrEmpty(ext)) ext = GuessExt(file.FileData);

                    var suggested = baseName;
                    if (!string.IsNullOrEmpty(ext) &&
                        !suggested.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                        suggested += ext;

                    var dlg = new SaveFileDialog
                    {
                        FileName = suggested,
                        DefaultExt = string.IsNullOrEmpty(ext) ? "" : ext.TrimStart('.'),
                        AddExtension = !string.IsNullOrEmpty(ext),
                        Filter = string.IsNullOrEmpty(ext)
                                         ? "All Files (*.*)|*.*"
                                         : $"{ext.TrimStart('.').ToUpper()} (*{ext})|*{ext}|All Files (*.*)|*.*",
                        OverwritePrompt = true
                    };

                    if (dlg.ShowDialog() == true)
                    {
                        var path = dlg.FileName;
                        if (!string.IsNullOrEmpty(ext) && string.IsNullOrEmpty(Path.GetExtension(path)))
                            path += ext;

                        File.WriteAllBytes(path, file.FileData);
                        MessageBox.Show($"File '{Path.GetFileName(path)}' downloaded successfully!",
                                        "Download", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    MessageBox.Show("Please select a file first.", "Selection Required",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to download file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            lobbyTimer?.Stop();

            if (!_returningToLobby)
            {
                try
                {
                    _client.LeaveRoom(_username);
                }
                catch
                {
                    // Application shutdown closes the underlying channel.
                }

                Application.Current?.Shutdown();
            }

            base.OnClosed(e);
        }

        private class PlayerDisplayWrapper
        {
            public Player OriginalPlayer { get; }
            private readonly string _currentUser;
            public string DisplayName => OriginalPlayer.Name == _currentUser ? $"{OriginalPlayer.Name} (me)" : OriginalPlayer.Name;

            public PlayerDisplayWrapper(Player player, string currentUser)
            {
                OriginalPlayer = player ?? throw new ArgumentNullException(nameof(player));
                _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            }
        }
    }
}
