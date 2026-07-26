using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using RelayLobby.Contracts;
using RelayLobby.Domain;

namespace RelayLobby.Server
{
    [ServiceBehavior(
        ConcurrencyMode = ConcurrencyMode.Multiple,
        InstanceContextMode = InstanceContextMode.Single,
        UseSynchronizationContext = false)]
    public sealed class LobbyService : ILobbyService, ILobbyDuplexService
    {
        private readonly object _syncRoot = new object();
        private readonly List<LobbyRoom> _rooms = new List<LobbyRoom>();
        private readonly List<Player> _players = new List<Player>();
        private readonly ConcurrentDictionary<string, ILobbyCallback> _callbacks =
            new ConcurrentDictionary<string, ILobbyCallback>(StringComparer.OrdinalIgnoreCase);

        public LobbyService()
        {
            _rooms.Add(new LobbyRoom("General"));
            Console.WriteLine("RelayLobby state initialized.");
        }

        public bool Login(string username)
        {
            string normalizedUsername = RequireText(
                username,
                nameof(username),
                TransportConfiguration.MaxUsernameLength);

            lock (_syncRoot)
            {
                if (FindPlayer(normalizedUsername) != null)
                {
                    Console.WriteLine($"Login rejected for duplicate username '{normalizedUsername}'.");
                    return false;
                }

                _players.Add(new Player(normalizedUsername));

                ILobbyCallback callback = IsDuplexRequest()
                    ? TryGetCallback()
                    : null;
                if (callback != null)
                {
                    _callbacks[normalizedUsername] = callback;
                }
            }

            Console.WriteLine($"User '{normalizedUsername}' logged in.");
            return true;
        }

        public void Logout(string username)
        {
            string normalizedUsername = RequireText(
                username,
                nameof(username),
                TransportConfiguration.MaxUsernameLength);

            LobbyRoom previousRoom = null;

            lock (_syncRoot)
            {
                Player player = FindPlayer(normalizedUsername);
                if (player == null)
                {
                    _callbacks.TryRemove(normalizedUsername, out _);
                    return;
                }

                previousRoom = player.Room;
                if (previousRoom != null)
                {
                    previousRoom.Players.Remove(player);
                    player.Room = null;
                }

                _players.Remove(player);
                _callbacks.TryRemove(normalizedUsername, out _);
            }

            Console.WriteLine($"User '{normalizedUsername}' logged out.");
            BroadcastRoomState(previousRoom);
        }

        public void CreateLobbyRoom(string roomName)
        {
            string normalizedRoomName = RequireText(
                roomName,
                nameof(roomName),
                TransportConfiguration.MaxRoomNameLength);

            lock (_syncRoot)
            {
                if (FindRoom(normalizedRoomName) != null)
                {
                    throw new InvalidOperationException(
                        $"A room named '{normalizedRoomName}' already exists.");
                }

                _rooms.Add(new LobbyRoom(normalizedRoomName));
            }

            Console.WriteLine($"Room '{normalizedRoomName}' created.");
        }

        public void JoinRoom(string username, string roomName)
        {
            string normalizedUsername = RequireText(
                username,
                nameof(username),
                TransportConfiguration.MaxUsernameLength);
            string normalizedRoomName = RequireText(
                roomName,
                nameof(roomName),
                TransportConfiguration.MaxRoomNameLength);

            LobbyRoom previousRoom;
            LobbyRoom destinationRoom;

            lock (_syncRoot)
            {
                Player player = GetRequiredPlayer(normalizedUsername);
                destinationRoom = FindRoom(normalizedRoomName)
                    ?? throw new InvalidOperationException(
                        $"Room '{normalizedRoomName}' does not exist.");

                previousRoom = player.Room;
                if (ReferenceEquals(previousRoom, destinationRoom))
                {
                    return;
                }

                previousRoom?.Players.Remove(player);
                destinationRoom.Players.Add(player);
                player.Room = destinationRoom;
            }

            Console.WriteLine(
                $"User '{normalizedUsername}' joined room '{normalizedRoomName}'.");
            BroadcastRoomState(previousRoom);
            BroadcastRoomState(destinationRoom);
        }

        public void LeaveRoom(string username)
        {
            string normalizedUsername = RequireText(
                username,
                nameof(username),
                TransportConfiguration.MaxUsernameLength);

            LobbyRoom previousRoom;

            lock (_syncRoot)
            {
                Player player = GetRequiredPlayer(normalizedUsername);
                previousRoom = player.Room
                    ?? throw new InvalidOperationException(
                        $"Player '{normalizedUsername}' is not currently in a room.");

                previousRoom.Players.Remove(player);
                player.Room = null;
            }

            Console.WriteLine($"User '{normalizedUsername}' left their room.");
            BroadcastRoomState(previousRoom);
        }

        public void SendPublicMessage(string senderName, string messageContent)
        {
            string normalizedSender = RequireText(
                senderName,
                nameof(senderName),
                TransportConfiguration.MaxUsernameLength);
            string normalizedContent = RequireText(
                messageContent,
                nameof(messageContent),
                TransportConfiguration.MaxMessageLength);

            LobbyRoom room;

            lock (_syncRoot)
            {
                Player sender = GetRequiredPlayer(normalizedSender);
                room = RequireRoomMembership(sender);
                room.PublicMessages.Add(new Message(sender, null, normalizedContent));
            }

            BroadcastRoomState(room);
        }

        public void SendPrivateMessage(
            string senderName,
            string recipientName,
            string messageContent)
        {
            string normalizedSender = RequireText(
                senderName,
                nameof(senderName),
                TransportConfiguration.MaxUsernameLength);
            string normalizedRecipient = RequireText(
                recipientName,
                nameof(recipientName),
                TransportConfiguration.MaxUsernameLength);
            string normalizedContent = RequireText(
                messageContent,
                nameof(messageContent),
                TransportConfiguration.MaxMessageLength);

            LobbyRoom room;

            lock (_syncRoot)
            {
                Player sender = GetRequiredPlayer(normalizedSender);
                Player recipient = GetRequiredPlayer(normalizedRecipient);
                room = RequireSharedRoom(sender, recipient);
                room.PrivateMessages.Add(
                    new Message(sender, recipient, normalizedContent));
            }

            BroadcastRoomState(room);
        }

        public void SendPublicFile(string senderName, string fileName, byte[] file)
        {
            string normalizedSender = RequireText(
                senderName,
                nameof(senderName),
                TransportConfiguration.MaxUsernameLength);
            string normalizedFileName = RequireText(fileName, nameof(fileName), 255);
            ValidateFile(file);

            LobbyRoom room;

            lock (_syncRoot)
            {
                Player sender = GetRequiredPlayer(normalizedSender);
                room = RequireRoomMembership(sender);
                room.PublicFiles.Add(
                    new SharedFile(normalizedFileName, file, sender, null));
            }

            BroadcastRoomState(room);
        }

        public void SendPrivateFile(
            string senderName,
            string recipientName,
            string fileName,
            byte[] file)
        {
            string normalizedSender = RequireText(
                senderName,
                nameof(senderName),
                TransportConfiguration.MaxUsernameLength);
            string normalizedRecipient = RequireText(
                recipientName,
                nameof(recipientName),
                TransportConfiguration.MaxUsernameLength);
            string normalizedFileName = RequireText(fileName, nameof(fileName), 255);
            ValidateFile(file);

            LobbyRoom room;

            lock (_syncRoot)
            {
                Player sender = GetRequiredPlayer(normalizedSender);
                Player recipient = GetRequiredPlayer(normalizedRecipient);
                room = RequireSharedRoom(sender, recipient);
                room.PrivateFiles.Add(
                    new SharedFile(normalizedFileName, file, sender, recipient));
            }

            BroadcastRoomState(room);
        }

        public List<Message> GetPublicMessages(string username)
        {
            string normalizedUsername = RequireText(
                username,
                nameof(username),
                TransportConfiguration.MaxUsernameLength);

            lock (_syncRoot)
            {
                Player player = GetRequiredPlayer(normalizedUsername);
                return player.Room?.PublicMessages.ToList() ?? new List<Message>();
            }
        }

        public List<Message> GetPrivateMessages(string username, string otherUser)
        {
            string normalizedUsername = RequireText(
                username,
                nameof(username),
                TransportConfiguration.MaxUsernameLength);
            string normalizedOtherUser = RequireText(
                otherUser,
                nameof(otherUser),
                TransportConfiguration.MaxUsernameLength);

            lock (_syncRoot)
            {
                Player player = GetRequiredPlayer(normalizedUsername);
                if (player.Room == null)
                {
                    return new List<Message>();
                }

                return player.Room.PrivateMessages
                    .Where(message =>
                        (SameName(message.Sender.Name, normalizedUsername) &&
                         SameName(message.Recipient?.Name, normalizedOtherUser)) ||
                        (SameName(message.Sender.Name, normalizedOtherUser) &&
                         SameName(message.Recipient?.Name, normalizedUsername)))
                    .ToList();
            }
        }

        public List<SharedFile> GetPublicFiles(string username)
        {
            string normalizedUsername = RequireText(
                username,
                nameof(username),
                TransportConfiguration.MaxUsernameLength);

            lock (_syncRoot)
            {
                Player player = GetRequiredPlayer(normalizedUsername);
                return player.Room?.PublicFiles.ToList() ?? new List<SharedFile>();
            }
        }

        public List<SharedFile> GetPrivateFiles(string username, string otherUser)
        {
            string normalizedUsername = RequireText(
                username,
                nameof(username),
                TransportConfiguration.MaxUsernameLength);
            string normalizedOtherUser = RequireText(
                otherUser,
                nameof(otherUser),
                TransportConfiguration.MaxUsernameLength);

            lock (_syncRoot)
            {
                Player player = GetRequiredPlayer(normalizedUsername);
                if (player.Room == null)
                {
                    return new List<SharedFile>();
                }

                return player.Room.PrivateFiles
                    .Where(file =>
                        (SameName(file.Sender.Name, normalizedUsername) &&
                         SameName(file.Recipient?.Name, normalizedOtherUser)) ||
                        (SameName(file.Sender.Name, normalizedOtherUser) &&
                         SameName(file.Recipient?.Name, normalizedUsername)))
                    .ToList();
            }
        }

        public List<Player> GetPlayersInRoom(string username)
        {
            string normalizedUsername = RequireText(
                username,
                nameof(username),
                TransportConfiguration.MaxUsernameLength);

            lock (_syncRoot)
            {
                Player player = GetRequiredPlayer(normalizedUsername);
                return player.Room?.Players.ToList() ?? new List<Player>();
            }
        }

        public List<LobbyRoomSummary> GetLobbyRooms()
        {
            lock (_syncRoot)
            {
                return _rooms
                    .Select(room =>
                        new LobbyRoomSummary(room.RoomName, room.Players.Count))
                    .ToList();
            }
        }

        private void BroadcastRoomState(LobbyRoom room)
        {
            if (room == null)
            {
                return;
            }

            List<Player> players;
            List<Message> publicMessages;
            List<Message> privateMessages;
            List<SharedFile> publicFiles;
            List<SharedFile> privateFiles;

            lock (_syncRoot)
            {
                players = room.Players.ToList();
                publicMessages = room.PublicMessages.ToList();
                privateMessages = room.PrivateMessages.ToList();
                publicFiles = room.PublicFiles.ToList();
                privateFiles = room.PrivateFiles.ToList();
            }

            foreach (Player player in players)
            {
                if (!_callbacks.TryGetValue(player.Name, out ILobbyCallback callback))
                {
                    continue;
                }

                List<Message> visiblePrivateMessages = privateMessages
                    .Where(message =>
                        SameName(message.Sender.Name, player.Name) ||
                        SameName(message.Recipient?.Name, player.Name))
                    .ToList();

                List<SharedFile> visiblePrivateFiles = privateFiles
                    .Where(file =>
                        SameName(file.Sender.Name, player.Name) ||
                        SameName(file.Recipient?.Name, player.Name))
                    .ToList();

                try
                {
                    callback.UpdateRoomState(
                        publicMessages,
                        visiblePrivateMessages,
                        publicFiles,
                        visiblePrivateFiles,
                        players);
                }
                catch (CommunicationException)
                {
                    _callbacks.TryRemove(player.Name, out _);
                }
                catch (TimeoutException)
                {
                    _callbacks.TryRemove(player.Name, out _);
                }
                catch (ObjectDisposedException)
                {
                    _callbacks.TryRemove(player.Name, out _);
                }
            }
        }

        private static ILobbyCallback TryGetCallback()
        {
            try
            {
                return OperationContext.Current?.GetCallbackChannel<ILobbyCallback>();
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (CommunicationException)
            {
                return null;
            }
        }

        private static bool IsDuplexRequest()
        {
            return string.Equals(
                OperationContext.Current?.EndpointDispatcher?.ContractName,
                "LobbyDuplexService",
                StringComparison.Ordinal);
        }

        private Player FindPlayer(string username)
        {
            return _players.FirstOrDefault(
                player => SameName(player.Name, username));
        }

        private Player GetRequiredPlayer(string username)
        {
            return FindPlayer(username)
                ?? throw new InvalidOperationException(
                    $"Player '{username}' is not logged in.");
        }

        private LobbyRoom FindRoom(string roomName)
        {
            return _rooms.FirstOrDefault(
                room => SameName(room.RoomName, roomName));
        }

        private static LobbyRoom RequireRoomMembership(Player player)
        {
            return player.Room
                ?? throw new InvalidOperationException(
                    $"Player '{player.Name}' is not currently in a room.");
        }

        private static LobbyRoom RequireSharedRoom(Player sender, Player recipient)
        {
            LobbyRoom room = RequireRoomMembership(sender);
            if (!ReferenceEquals(room, recipient.Room))
            {
                throw new InvalidOperationException(
                    "Private communication requires both users to be in the same room.");
            }

            return room;
        }

        private static string RequireText(string value, string parameterName, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    $"{parameterName} cannot be empty.",
                    parameterName);
            }

            string normalized = value.Trim();
            if (normalized.Length > maxLength)
            {
                throw new ArgumentException(
                    $"{parameterName} cannot exceed {maxLength} characters.",
                    parameterName);
            }

            return normalized;
        }

        private static void ValidateFile(byte[] file)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            if (file.Length > TransportConfiguration.MaxFileBytes)
            {
                throw new ArgumentException(
                    "File size exceeds the 10 MB limit.",
                    nameof(file));
            }
        }

        private static bool SameName(string left, string right)
        {
            return string.Equals(
                left,
                right,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
