using RelayLobby.Contracts;
using RelayLobby.Domain;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

namespace RelayLobby.Client.Duplex
{
    public partial class PrivateChatWindow : Window
    {
        private readonly string _username;
        private readonly string _otherUser;
        private readonly ILobbyDuplexService _client;

        internal string Peer => _otherUser;

        public PrivateChatWindow(string username, string otherUser, ILobbyDuplexService client)
        {
            try
            {
                InitializeComponent();
                _username = username ?? throw new ArgumentNullException(nameof(username), "Username cannot be null.");
                _otherUser = otherUser ?? throw new ArgumentNullException(nameof(otherUser), "Recipient cannot be null.");
                _client = client ?? throw new ArgumentNullException(nameof(client), "Client interface cannot be null.");
                Title = $"Private Chat with {_otherUser}";

                CallbackHandler.OnRoomStatePushed += Callback_OnRoomState;

                LoadMessages();
                LoadFiles();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Initialization failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                try { CallbackHandler.OnRoomStatePushed -= Callback_OnRoomState; } catch { }
                this.Close();
            }
        }

        /*Method to load private messages for the current chat.*/
        private void LoadMessages()
        {
            try
            {
                lstPrivateMessages.ItemsSource =
                    _client.GetPrivateMessages(_username, _otherUser).Select(m => m.ToString()).ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load messages: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /*Method to load private files for the current chat.*/
        private void LoadFiles()
        {
            try
            {
                var files = _client.GetPrivateFiles(_username, _otherUser);
                lstPrivateFiles.ItemsSource = files ?? new List<SharedFile>();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load files: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /*Callback handler to update messages and files based on room state changes.*/
        private void Callback_OnRoomState(
            List<Message> publicMessages,
            List<Message> privateMessages,
            List<SharedFile> publicFiles,
            List<SharedFile> privateFiles,
            List<Player> players)
        {
            try
            {
                var msgs = (privateMessages ?? new List<Message>())
                    .Where(m =>
                        (m.Sender?.Name == _username && m.Recipient?.Name == _otherUser) ||
                        (m.Sender?.Name == _otherUser && m.Recipient?.Name == _username))
                    .Select(m => m.ToString())
                    .ToList();

                var files = (privateFiles ?? new List<SharedFile>())
                    .Where(f =>
                        (f.Sender?.Name == _username && f.Recipient?.Name == _otherUser) ||
                        (f.Sender?.Name == _otherUser && f.Recipient?.Name == _username))
                    .ToList();

                lstPrivateMessages.ItemsSource = msgs;
                lstPrivateFiles.ItemsSource = files;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to apply callback update: {ex.Message}", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /*Event handler to send a private message to the other user.*/
        private async void SendMessage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string message = txtPrivateMsg.Text?.Trim();
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

                await Task.Run(() => _client.SendPrivateMessage(_username, _otherUser, message));
                txtPrivateMsg.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to send private message: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /*Event handler to send a private file to the other user.*/
        private async void SendFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new OpenFileDialog { Filter = "All Files|*.*" };
                if (dlg.ShowDialog() != true) return;

                var fileInfo = new FileInfo(dlg.FileName);
                if (fileInfo.Length > TransportConfiguration.MaxFileBytes)
                {
                    MessageBox.Show("File too large (max 10MB).", "Invalid File", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                byte[] file = File.ReadAllBytes(dlg.FileName);
                await Task.Run(() => _client.SendPrivateFile(_username, _otherUser, dlg.SafeFileName, file));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to send private file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /*Helper method to guess file extension based on file data signature.*/
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

        /*Event handler to download a selected file from the chat.*/
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

        /*Event handler to close the private chat window.*/
        private void ExitPrivateChat_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            CallbackHandler.OnRoomStatePushed -= Callback_OnRoomState;
            base.OnClosed(e);
        }
    }
}
