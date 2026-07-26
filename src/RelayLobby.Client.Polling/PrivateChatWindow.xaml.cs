using RelayLobby.Contracts;
using RelayLobby.Domain;
using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;

namespace RelayLobby.Client.Polling
{
    public partial class PrivateChatWindow : Window
    {
        private readonly string _username;
        private readonly string _recipient;
        private readonly ILobbyService _client;
        private DispatcherTimer pollingTimer;

        public string CurrentUser => _username;
        public string OtherUser => _recipient;

        /*Constructor to initialize the private chat window, set the username, recipient, and client.
         It also sets up the polling timer and updates the display.*/
        public PrivateChatWindow(string username, string recipient, ILobbyService client)
        {
            try
            {
                InitializeComponent();
                _username = username ?? throw new ArgumentNullException(nameof(username), "Username cannot be null.");
                _recipient = recipient ?? throw new ArgumentNullException(nameof(recipient), "Recipient cannot be null.");
                _client = client ?? throw new ArgumentNullException(nameof(client), "Client interface cannot be null.");
                this.Title = $"Private Chat with {_recipient}";
                SetupPollingTimer();
                UpdateDisplay();
            }
            catch (Exception ex)
            {
                HandleError($"Initialization failed: {ex.Message}");
                this.Close();
            }
        }

        /*Sets up a polling timer that triggers every second to update the chat window.
         The polling timer calls the UpdateDisplay method to refresh messages and files.*/
        private void SetupPollingTimer()
        {
            try
            {
                pollingTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                pollingTimer.Tick += (sender, e) => UpdateDisplay();
                pollingTimer.Start();
            }
            catch (Exception ex)
            {
                HandleError($"Failed to set up polling timer: {ex.Message}");
            }
        }

        /*Updates the chat window by fetching and displaying private messages and files.
         It updates the list of messages and files from the client, or shows "No messages" and "No files"
        if there is no data.*/
        private void UpdateDisplay()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    var messages = _client?.GetPrivateMessages(_username, _recipient);
                    if (messages != null)
                    {
                        lstPrivateMessages.ItemsSource = messages.Select(m => m?.ToString() ?? "Invalid message");
                    }
                    else
                    {
                        lstPrivateMessages.ItemsSource = new[] { "No messages available" };
                    }

                    var files = _client?.GetPrivateFiles(_username, _recipient);
                    if (files != null)
                    {
                        lstPrivateFiles.ItemsSource = files;
                    }
                    else
                    {
                        lstPrivateFiles.ItemsSource = new[] { new { FileName = "No files available" } };
                    }
                });
            }
            catch (Exception ex)
            {
                HandleError($"Failed to update display: {ex.Message}");
            }
        }

        /*Sends a private message from the current user to the recipient
         It ensures the message is not empty and is within the allowed character limit before sending.*/
        private void SendPrivate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string message = txtPrivateMsg.Text?.Trim();
                if (string.IsNullOrWhiteSpace(message))
                {
                    throw new ArgumentException("Message cannot be empty.");
                }
                if (message.Length > TransportConfiguration.MaxMessageLength)
                {
                    throw new ArgumentException(
                        $"Message must be 1-{TransportConfiguration.MaxMessageLength} characters.");
                }

                _client?.SendPrivateMessage(_username, _recipient, message);
                txtPrivateMsg.Clear();
                UpdateDisplay();
            }
            catch (ArgumentException ex)
            {
                HandleError(ex.Message);
            }
            catch (Exception ex)
            {
                HandleError($"Failed to send private message: {ex.Message}");
            }
        }

        /*Allows the user to select and send a file to the recipient.
         The file is validated  and is sent via the client interface.*/
        private void SendPrivateFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new OpenFileDialog { Filter = "All Files|*.*" };
                if (dlg.ShowDialog() != true)
                {
                    return;
                }

                var fileInfo = new FileInfo(dlg.FileName);
                if (fileInfo.Length > TransportConfiguration.MaxFileBytes)
                {
                    throw new ArgumentException("File too large (max 10MB).");
                }

                byte[] file = File.ReadAllBytes(dlg.FileName);
                _client?.SendPrivateFile(_username, _recipient, Path.GetFileName(dlg.FileName), file);
                UpdateDisplay();
            }
            catch (ArgumentException ex)
            {
                HandleError(ex.Message);
            }
            catch (Exception ex)
            {
                HandleError($"Failed to send private file: {ex.Message}");
            }
        }

        /*Closes the private chat window and stops the polling time
         This is called when the user wants to exit the chat.*/
        private void ExitPrivateChat_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                pollingTimer?.Stop();
                this.Close();
            }
            catch (Exception ex)
            {
                HandleError($"Failed to exit private chat: {ex.Message}");
            }
        }

        /*Handles the file download by showing a SaveFileDialog to allow the user to save the selected file.
         The file is then written to the specified location on the local machine.*/
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

        /*Displays an error message in a MessageBox with the specified message.
         This method is used to show error notifications to the user.*/
        private void HandleError(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /*Handles the cleanup when the window is closed by stopping the polling timer.
         This is called when the window is being closed, ensuring that resources are properly cleaned up.*/
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                pollingTimer?.Stop();
            }
            catch (Exception ex)
            {
                HandleError($"Failed to stop timer: {ex.Message}");
            }
            base.OnClosed(e);
        }
    }
}
