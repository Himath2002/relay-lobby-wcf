using System;
using System.Runtime.Serialization;

namespace RelayLobby.Domain
{
    [DataContract]
    public sealed class Player
    {
        public Player(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Username cannot be empty.", nameof(name));
            }

            Name = name.Trim();
        }

        [DataMember(Order = 1)]
        public string Name { get; private set; }

        [IgnoreDataMember]
        public LobbyRoom Room { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}
