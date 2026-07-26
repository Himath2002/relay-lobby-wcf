using RelayLobby.Contracts;
using Microsoft.VisualBasic;
using System;
using System.Linq;
using System.ServiceModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Collections.Generic;

namespace RelayLobby.Client.Polling
{
    public partial class LobbyWindow : Window
    {
        private readonly string _username;
        private ILobbyService _client;
        private DispatcherTimer pollingTimer;
        private List<string> allRooms = new List<string>();
        private bool _navigatingToRoom;
        private bool _isLoggingOut;

        /*Initializes the LobbyWindow with username and server client connection.
         Sets up the polling timer and loads the available rooms.*/
        public LobbyWindow(string username, ILobbyService client)
        {
            InitializeComponent();
            _username = username;
            _client = client;
            SetupPollingTimer();
            LoadRooms();
        }

        /*Handles the event when the window has loaded.
         Applies animations to the room list for smooth visibility and scaling effect.*/
        private void Window_Loaded(object sender, RoutedEventArgs e)
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

        /*Sets up a polling timer to check for updates on available rooms every second.*/
        private void SetupPollingTimer()
        {
            try
            {
                pollingTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                pollingTimer.Tick += (sender, e) => LoadRooms();
                pollingTimer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to set up polling timer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /*Loads the list of available rooms from the server and applies the search filter.
         Handles any failures by attempting to reconnect and retry.*/
        private void LoadRooms()
        {
            try
            {
                allRooms = _client.GetLobbyRooms().Select(r => r.RoomName).ToList();
                ApplySearchFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load rooms: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ReconnectClient();
                try
                {
                    allRooms = _client.GetLobbyRooms().Select(r => r.RoomName).ToList();
                    ApplySearchFilter();
                }
                catch (Exception retryEx)
                {
                    MessageBox.Show($"Retry failed: {retryEx.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /*Filters the room list based on the search text entered by the user.*/
        private void ApplySearchFilter()
        {
            try
            {
                string searchText = txtSearch.Text?.Trim();
                var filteredRooms = string.IsNullOrEmpty(searchText)
                    ? allRooms
                    : allRooms
                        .Where(roomName =>
                            roomName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();
                lstRooms.ItemsSource = filteredRooms;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to apply search filter: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /*Handles text change events in the search textbox. Triggers the search filter.*/
        private void txtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ApplySearchFilter();
        }

        /*Handles the creation of a new room. Prompts the user for the room name
         checks for validity, and attempts to create the room on the server.*/
        private void CreateRoom_Click(object sender, RoutedEventArgs e)
        {
            string roomName = Interaction.InputBox("Enter room name:", "Create Room");
            roomName = roomName?.Trim();
            if (!string.IsNullOrWhiteSpace(roomName) &&
                roomName.Length <= TransportConfiguration.MaxRoomNameLength)
            {
                if (allRooms != null && allRooms.Contains(roomName, StringComparer.OrdinalIgnoreCase))
                {
                    MessageBox.Show($"Cannot create room '{roomName}', it already exists.", "Room Creation Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    _client.CreateLobbyRoom(roomName);
                    LoadRooms();
                    MessageBox.Show($"Room '{roomName}' created.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to create room: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show(
                    $"Room name must be 1-{TransportConfiguration.MaxRoomNameLength} characters.",
                    "Invalid input",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        /*Handles joining an existing room. Checks if the user has selected a room,
         and attempts to join the selected room.*/
        private void JoinRoom_Click(object sender, RoutedEventArgs e)
        {
            if (lstRooms.SelectedItem is string roomName)
            {
                try
                {
                    _client.JoinRoom(_username, roomName);
                    RoomWindow roomWindow = new RoomWindow(_username, roomName, _client);
                    roomWindow.Show();
                    _navigatingToRoom = true;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to join room: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Please select a room.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /*Logs the user out of the application and closes the application after confirmation.*/
        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to exit the application?", "Confirm Exit",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    _isLoggingOut = true;
                    pollingTimer?.Stop();
                    _client.Logout(_username);
                    Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to clean up: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /*Attempts to reconnect the client to the server by aborting the current connection
         and establishing a new connection with updated settings.*/
        private void ReconnectClient()
        {
            if (_client != null)
            {
                try { ((ICommunicationObject)_client).Abort(); } catch { }
            }
            try
            {
                _client = new ChannelFactory<ILobbyService>(
                    TransportConfiguration.CreateBinding(),
                    new EndpointAddress(TransportConfiguration.PollingClientAddress))
                    .CreateChannel();
                ((ICommunicationObject)_client).Open();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Reconnection failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /*Called when the window is closed. Stops the polling timer to avoid unnecessary background activity.*/
        protected override void OnClosed(EventArgs e)
        {
            pollingTimer?.Stop();

            if (!_navigatingToRoom && !_isLoggingOut)
            {
                try
                {
                    _client.Logout(_username);
                }
                catch
                {
                    // Application shutdown closes any remaining communication objects.
                }

                Application.Current?.Shutdown();
            }

            base.OnClosed(e);
        }
    }
}
