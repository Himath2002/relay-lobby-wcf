using System;
using System.Runtime.Serialization;

namespace RelayLobby.Domain
{
    [DataContract]
    public sealed class LobbyRoomSummary
    {
        public LobbyRoomSummary(string roomName, int playerCount)
        {
            RoomName = roomName ?? throw new ArgumentNullException(nameof(roomName));
            PlayerCount = playerCount;
        }

        [DataMember(Order = 1)]
        public string RoomName { get; private set; }

        [DataMember(Order = 2)]
        public int PlayerCount { get; private set; }

        public override string ToString()
        {
            return $"{RoomName} ({PlayerCount})";
        }
    }
}
