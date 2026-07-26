using System;
using System.ServiceModel;
using System.Windows;
using RelayLobby.Contracts;

namespace RelayLobby.Client.Polling
{
    public partial class MainWindow : Window
    {
        private ChannelFactory<ILobbyService> _channelFactory;
        private ILobbyService _client;

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                Connect();
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
                if (_client.Login(username))
                {
                    LobbyWindow lobbyWindow = new LobbyWindow(username, _client);
                    lobbyWindow.Show();
                    this.Hide();
                }
                else
                {
                    MessageBox.Show(
                        "That username is already connected. Please choose another.",
                        "Username unavailable",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (CommunicationException exception)
            {
                MessageBox.Show(
                    $"Login failed because the server connection was lost.\n\n{exception.Message}",
                    "Connection lost",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        protected override void OnClosed(EventArgs e)
        {
            CloseCommunicationObject(_client as ICommunicationObject);
            CloseCommunicationObject(_channelFactory);
            base.OnClosed(e);
        }

        private void Connect()
        {
            _channelFactory = new ChannelFactory<ILobbyService>(
                TransportConfiguration.CreateBinding(),
                new EndpointAddress(TransportConfiguration.PollingClientAddress));
            _client = _channelFactory.CreateChannel();
            ((ICommunicationObject)_client).Open();
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
}
