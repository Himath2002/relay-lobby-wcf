using RelayLobby.Contracts;
using RelayLobby.Domain;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;

namespace RelayLobby.Client.Polling
{

    public partial class RoomWindow : Window
    {
        private readonly string _username;
        private readonly string _roomName;
        private ILobbyService _client;
        private DispatcherTimer pollingTimer;
        private DispatcherTimer lobbyTimer;
        private DateTime joinTime;
        private Player selectedPlayer;
        private bool _sawFaultOnce;
        private bool _returningToLobby;

        private readonly Dictionary<string, int> _lastPrivateCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private bool _privateCountersInitialized = false;

        /*Constructor to initialize the RoomWindow.
         Initializes and starts the polling timer
        Initializes and starts the lobby timer*/
        public RoomWindow(string username, string roomName, ILobbyService client)
        {
            try
            {
                InitializeComponent();
                _username = username ?? throw new ArgumentNullException(nameof(username));
                _roomName = roomName ?? throw new ArgumentNullException(nameof(roomName));
                _client = client ?? throw new ArgumentNullException(nameof(client));
                txtRoomName.Text = _roomName;
                joinTime = DateTime.Now;
                SetupPollingTimer();
                SetupLobbyTimer();
                UpdateDisplay();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize RoomWindow: {ex.Message}");
                this.Close();
            }
        }

        /* Sets up a timer to periodically update the room's display by calling the UpdateDisplay
         method every 3 seconds.*/
        private void SetupPollingTimer()
        {
            pollingTimer = new DispatcherTimer();
            pollingTimer.Interval = TimeSpan.FromSeconds(3);
            pollingTimer.Tick += (sender, e) => UpdateDisplay();
            pollingTimer.Start();
        }

        /*Sets up a timer that displays the time spent in the room since joining.
         The timer updates every second.*/
        private void SetupLobbyTimer()
        {
            lobbyTimer = new DispatcherTimer();
            lobbyTimer.Interval = TimeSpan.FromSeconds(1);
            lobbyTimer.Tick += (sender, e) =>
            {
                TimeSpan elapsed = DateTime.Now - joinTime;
                txtLobbyTimer.Text = elapsed.ToString(@"hh\:mm\:ss");
            };
            lobbyTimer.Start();
        }

        /*Updates the display of messages, players, and files, and also manages the private message counters.*/
        private void UpdateDisplay()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    var messages = _client.GetPublicMessages(_username);
                    lstMessages.ItemsSource = messages.Select(m => m.ToString());

                    var players = _client.GetPlayersInRoom(_username);
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
                        if (restoredIndex >= 0) lstPlayers.SelectedIndex = restoredIndex;
                    }

                    var files = _client.GetPublicFiles(_username);
                    lstFiles.ItemsSource = files;

                    TryInitOrDetectNewPrivateMessages(players);
                });
            }
            catch (CommunicationObjectFaultedException)
            {
                if (!_sawFaultOnce)
                {
                    _sawFaultOnce = true;
                    ReconnectClient();
                }
            }
            catch (CommunicationException)
            {
                if (!_sawFaultOnce)
                {
                    _sawFaultOnce = true;
                    ReconnectClient();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update display: {ex.Message}");
            }
        }

        /*Initializes private message counters for players and detects if there are any new private messages.*/
        private void TryInitOrDetectNewPrivateMessages(List<Player> players)
        {
            if (players == null || players.Count == 0) return;

            var present = new HashSet<string>(players.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
            var toRemove = _lastPrivateCounts.Keys.Where(k => !present.Contains(k)).ToList();
            foreach (var k in toRemove) _lastPrivateCounts.Remove(k);

            foreach (var p in players)
            {
                if (p == null || string.Equals(p.Name, _username, StringComparison.OrdinalIgnoreCase)) continue;

                int currentCount = 0;
                List<Message> msgs = null;
                try
                {
                    msgs = _client.GetPrivateMessages(_username, p.Name);
                    currentCount = msgs?.Count ?? 0;
                }
                catch
                {
                    continue;
                }

                if (!_privateCountersInitialized)
                {
                    _lastPrivateCounts[p.Name] = currentCount;
                    continue;
                }

                var had = _lastPrivateCounts.TryGetValue(p.Name, out var prev) ? prev : 0;
                if (currentCount > had && msgs != null && msgs.Count > 0)
                {
                    var last = msgs[msgs.Count - 1];
                    if (last?.Recipient?.Name == _username &&
                        last?.Sender?.Name == p.Name)
                    {
                        OpenOrActivatePrivateChat(p.Name);
                    }
                }

                _lastPrivateCounts[p.Name] = currentCount;
            }

            _privateCountersInitialized = true;
        }

        /*Opens or activates the private chat window for a specific player*/
        private void OpenOrActivatePrivateChat(string otherUser)
        {
            try
            {
                var existing = Application.Current.Windows
                    .OfType<PrivateChatWindow>()
                    .FirstOrDefault(w =>
                        string.Equals(w.CurrentUser, _username, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(w.OtherUser, otherUser, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
                    existing.Activate();
                    existing.Topmost = true;
                    existing.Topmost = false;
                    existing.Focus();
                }
                else
                {
                    new PrivateChatWindow(_username, otherUser, _client).Show();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open private chat with '{otherUser}': {ex.Message}");
            }
        }

        /*Sends a public message to the room*/
        private void SendMessage_Click(object sender, RoutedEventArgs e)
        {
            string message = txtMessage.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(message) &&
                message.Length <= TransportConfiguration.MaxMessageLength)
            {
                try
                {
                    _client.SendPublicMessage(_username, message);
                    txtMessage.Clear();
                    UpdateDisplay();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to send message: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show(
                    $"Message must be 1-{TransportConfiguration.MaxMessageLength} characters.");
            }
        }

        /*Opens a private chat with the selected player*/
        private void PrivateMsg_Click(object sender, RoutedEventArgs e)
        {
            if (lstPlayers.SelectedItem is PlayerDisplayWrapper p && p.OriginalPlayer.Name != _username)
            {
                new PrivateChatWindow(_username, p.OriginalPlayer.Name, _client).Show();
                selectedPlayer = p.OriginalPlayer;
            }
            else
            {
                MessageBox.Show("Select a different player for private chat.");
            }
        }

        /*Leaves the current room and returns to the lobby*/
        private void LeaveRoom_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _client.LeaveRoom(_username);
                LobbyWindow lobbyWindow = new LobbyWindow(_username, _client);
                lobbyWindow.Show();
                _returningToLobby = true;
                pollingTimer.Stop();
                lobbyTimer.Stop();
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to leave room: {ex.Message}");
            }
        }

        /*Uploads a file to the room*/
        private void UploadFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "All Files|*.*" };
            if (dlg.ShowDialog() != true)
            {
                return;
            }

            if (new FileInfo(dlg.FileName).Length <= TransportConfiguration.MaxFileBytes)
            {
                byte[] file = File.ReadAllBytes(dlg.FileName);
                try
                {
                    _client.SendPublicFile(_username, Path.GetFileName(dlg.FileName), file);
                    UpdateDisplay();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to upload file: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("File too large (max 10MB) or invalid.");
            }
        }

        /*Downloads a file when the corresponding hyperlink is clicked*/
        private void Hyperlink_DownloadFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Hyperlink hyperlink && hyperlink.DataContext is SharedFile file)
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = file.FileName,
                    Filter = "All Files (*.*)|*.*"
                };

                if (dlg.ShowDialog() == true)
                {
                    File.WriteAllBytes(dlg.FileName, file.FileData);
                    MessageBox.Show($"File '{file.FileName}' downloaded successfully!", "Download", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show("Please select a file first.");
            }
        }

        /*Ensures all timers are stopped when the window is closed*/
        protected override void OnClosed(EventArgs e)
        {
            pollingTimer?.Stop();
            lobbyTimer?.Stop();

            if (!_returningToLobby)
            {
                try
                {
                    _client.LeaveRoom(_username);
                }
                catch
                {
                    // Application shutdown closes any remaining communication objects.
                }

                Application.Current?.Shutdown();
            }

            base.OnClosed(e);
        }

        /*Wrapper class to display player information along with an indicator for the current user*/
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

        /*Reconnects the client in case of communication failures*/
        private void ReconnectClient()
        {
            try
            {
                if (_client is ICommunicationObject ico)
                {
                    try { ico.Abort(); } catch { }
                }

                _client = new ChannelFactory<ILobbyService>(
                    TransportConfiguration.CreateBinding(),
                    new EndpointAddress(TransportConfiguration.PollingClientAddress))
                    .CreateChannel();

                ((ICommunicationObject)_client).Open();
                _sawFaultOnce = false;
            }
            catch (Exception rex)
            {
                pollingTimer?.Stop();
                MessageBox.Show($"Connection lost. Reconnect failed: {rex.Message}");
            }
        }
    }
}
