using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace RelayLobby.Domain
{
    [DataContract]
    public sealed class LobbyRoom
    {
        public LobbyRoom(string roomName)
        {
            if (string.IsNullOrWhiteSpace(roomName))
            {
                throw new ArgumentException("Room name cannot be empty.", nameof(roomName));
            }

            RoomName = roomName.Trim();
            Players = new List<Player>();
            PublicMessages = new List<Message>();
            PrivateMessages = new List<Message>();
            PublicFiles = new List<SharedFile>();
            PrivateFiles = new List<SharedFile>();
        }

        [DataMember(Order = 1)]
        public string RoomName { get; private set; }

        [DataMember(Order = 2)]
        public List<Player> Players { get; private set; }

        [DataMember(Order = 3)]
        public List<Message> PublicMessages { get; private set; }

        [DataMember(Order = 4)]
        public List<Message> PrivateMessages { get; private set; }

        [DataMember(Order = 5)]
        public List<SharedFile> PublicFiles { get; private set; }

        [DataMember(Order = 6)]
        public List<SharedFile> PrivateFiles { get; private set; }

        public override string ToString()
        {
            return RoomName;
        }
    }
}
