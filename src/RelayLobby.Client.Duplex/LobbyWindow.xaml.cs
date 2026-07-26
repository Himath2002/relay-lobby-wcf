using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using RelayLobby.Contracts;
using RelayLobby.Domain;

namespace RelayLobby.Client.Duplex
{
    public partial class LobbyWindow : Window
    {
        private readonly string _username;
        private readonly ILobbyDuplexService _client;
        private readonly DispatcherTimer _roomSyncTimer;
        private List<string> _allRooms = new List<string>();
        private bool _isLoggingOut;

        public LobbyWindow(string username, ILobbyDuplexService client)
        {
            try
            {
                InitializeComponent();
                _username = username ?? throw new ArgumentNullException(nameof(username), "Username cannot be null.");
                _client = client ?? throw new ArgumentNullException(nameof(client), "Client interface cannot be null.");

                _roomSyncTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(5)
                };
                _roomSyncTimer.Tick += (sender, eventArgs) =>
                {
                    if (IsVisible)
                    {
                        LoadRooms();
                    }
                };
                _roomSyncTimer.Start();

                Loaded += Window_Loaded;
                LoadRooms();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Initialization failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
        }

        /*Event handler for the window's Loaded event.
         It animates the appearance of the room list and scales the room list UI.*/
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                DoubleAnimation animation = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromSeconds(1),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                lstRooms.BeginAnimation(OpacityProperty, animation);

                DoubleAnimation scaleAnimation = new DoubleAnimation
                {
                    From = 0.9,
                    To = 1,
                    Duration = TimeSpan.FromSeconds(0.8),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                RoomListScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
                RoomListScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to apply animations: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadRooms()
        {
            try
            {
                _allRooms = _client.GetLobbyRooms()
                    .Select(room => room.RoomName)
                    .OrderBy(roomName => roomName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                ApplySearchFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load rooms: {ex.Message}\nPlease check server configuration and try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void txtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ApplySearchFilter();
        }

        private void CreateRoom_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string roomName = Interaction.InputBox("Enter room name:", "Create Room");
                roomName = roomName?.Trim();
                if (string.IsNullOrWhiteSpace(roomName) ||
                    roomName.Length > TransportConfiguration.MaxRoomNameLength)
                {
                    MessageBox.Show(
                        $"Room name must be 1-{TransportConfiguration.MaxRoomNameLength} characters.",
                        "Invalid input",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (_allRooms.Contains(roomName, StringComparer.OrdinalIgnoreCase))
                {
                    MessageBox.Show($"Room '{roomName}' already exists. Please choose a different name.", "Duplicate Room", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _client.CreateLobbyRoom(roomName);
                LoadRooms();
                MessageBox.Show($"Room '{roomName}' created.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create room: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void JoinRoom_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string roomName = lstRooms.SelectedItem as string;
                if (string.IsNullOrEmpty(roomName))
                {
                    MessageBox.Show("Please select a room.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                _roomSyncTimer?.Stop();
                await Task.Run(() => _client.JoinRoom(_username, roomName));
                var roomWindow = new RoomWindow(_username, roomName, _client, this);
                roomWindow.Show();
                Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to join room: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _roomSyncTimer?.Start();
            }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MessageBox.Show("Are you sure you want to exit the application?", "Confirm Exit",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _isLoggingOut = true;
                    _roomSyncTimer?.Stop();
                    _client.Logout(_username);
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Logout failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _roomSyncTimer?.Stop();

            if (!_isLoggingOut)
            {
                try
                {
                    _client.Logout(_username);
                }
                catch
                {
                    // Application shutdown still closes the underlying WCF channel.
                }

                Application.Current?.Shutdown();
            }

            base.OnClosed(e);
        }

        public void ReturnToLobby()
        {
            try
            {
                _roomSyncTimer?.Start();
                LoadRooms();
                this.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to return to lobby: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplySearchFilter()
        {
            string searchText = txtSearch.Text?.Trim();
            lstRooms.ItemsSource = string.IsNullOrEmpty(searchText)
                ? _allRooms
                : _allRooms
                    .Where(roomName =>
                        roomName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
        }
    }
}
