using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Windows;
using RelayLobby.Contracts;
using RelayLobby.Domain;

namespace RelayLobby.Client.Duplex
{
    public partial class MainWindow : Window
    {
        private DuplexChannelFactory<ILobbyDuplexService> _channelFactory;
        private ILobbyDuplexService _client;

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                SetupClient();
            }
            catch (CommunicationException exception)
            {
                MessageBox.Show(
                    $"RelayLobby could not connect to the server.\n\n{exception.Message}",
                    "Server unavailable",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        private void SetupClient()
        {
            var callbackContext = new InstanceContext(new CallbackHandler());
            _channelFactory = new DuplexChannelFactory<ILobbyDuplexService>(
                callbackContext,
                TransportConfiguration.CreateBinding(),
                new EndpointAddress(TransportConfiguration.DuplexClientAddress));
            _client = _channelFactory.CreateChannel();
            ((ICommunicationObject)_client).Open();
            CallbackHandler.SharedClient = _client;
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text?.Trim();
            if (string.IsNullOrWhiteSpace(username) ||
                username.Length > TransportConfiguration.MaxUsernameLength)
            {
                MessageBox.Show(
                    $"Username must be 1-{TransportConfiguration.MaxUsernameLength} characters.",
                    "Invalid username",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (_client != null && _client.Login(username))
                {
                    CallbackHandler.CurrentUser = username;
                    var lobbyWindow = new LobbyWindow(username, _client);
                    lobbyWindow.Show();
                    Hide();
                }
                else
                {
                    MessageBox.Show("Username already exists. Please choose another.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Login failed: {ex.Message}");
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        protected override void OnClosed(EventArgs e)
        {
            CallbackHandler.SharedClient = null;
            CloseCommunicationObject(_client as ICommunicationObject);
            CloseCommunicationObject(_channelFactory);
            base.OnClosed(e);
        }

        private static void CloseCommunicationObject(ICommunicationObject communicationObject)
        {
            if (communicationObject == null)
            {
                return;
            }

            try
            {
                if (communicationObject.State == CommunicationState.Faulted)
                {
                    communicationObject.Abort();
                }
                else
                {
                    communicationObject.Close();
                }
            }
            catch (CommunicationException)
            {
                communicationObject.Abort();
            }
            catch (TimeoutException)
            {
                communicationObject.Abort();
            }
        }
    }

    [CallbackBehavior(ConcurrencyMode = ConcurrencyMode.Reentrant, UseSynchronizationContext = false)]
    public class CallbackHandler : ILobbyCallback
    {
        public static string CurrentUser { get; set; }
        public static ILobbyDuplexService SharedClient { get; set; }

        public static event Action<
            List<Message>,
            List<Message>,
            List<SharedFile>,
            List<SharedFile>,
            List<Player>
        > OnRoomStatePushed;

        private static readonly Dictionary<string, string> _lastShownMsgIdByPeer =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public void UpdateRoomState(
            List<Message> publicMessages,
            List<Message> privateMessages,
            List<SharedFile> publicFiles,
            List<SharedFile> privateFiles,
            List<Player> players)
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                var roomWindow = Application.Current.Windows.OfType<RoomWindow>().FirstOrDefault();
                roomWindow?.UpdateDisplay(publicMessages, privateMessages, publicFiles, privateFiles, players);

                OnRoomStatePushed?.Invoke(publicMessages, privateMessages, publicFiles, privateFiles, players);

                if (!string.IsNullOrWhiteSpace(CurrentUser) && privateMessages != null && privateMessages.Count > 0)
                {
                    var incomingByPeer = privateMessages
                        .Where(m => m?.Recipient?.Name == CurrentUser)
                        .GroupBy(m => m.Sender.Name);

                    foreach (var g in incomingByPeer)
                    {
                        var latest = g.OrderBy(m => m.SentAtUtc).LastOrDefault();
                        if (latest == null) continue;

                        var peer = latest.Sender.Name;
                        if (_lastShownMsgIdByPeer.TryGetValue(peer, out var lastId) && lastId == latest.Id)
                            continue;

                        _lastShownMsgIdByPeer[peer] = latest.Id;
                        FocusOrOpenPrivateChat(peer);
                    }
                }
            }));
        }

        private static void FocusOrOpenPrivateChat(string peer)
        {
            var existing = Application.Current.Windows
                .OfType<RelayLobby.Client.Duplex.PrivateChatWindow>()
                .FirstOrDefault(w => string.Equals(w.Peer, peer, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                if (existing.WindowState == WindowState.Minimized)
                    existing.WindowState = WindowState.Normal;

                existing.Activate();
                existing.Focus();
            }
            else if (SharedClient != null && !string.IsNullOrEmpty(CurrentUser))
            {
                new RelayLobby.Client.Duplex.PrivateChatWindow(CurrentUser, peer, SharedClient).Show();
            }
        }
    }
}
