using System;
using System.Runtime.Serialization;

namespace RelayLobby.Domain
{
    [DataContract]
    public sealed class Message
    {
        public Message(Player sender, Player recipient, string content)
        {
            Sender = sender ?? throw new ArgumentNullException(nameof(sender));
            Recipient = recipient;
            Content = content ?? throw new ArgumentNullException(nameof(content));
            Id = Guid.NewGuid().ToString("N");
            SentAtUtc = DateTime.UtcNow;
        }

        [DataMember(Order = 1)]
        public string Id { get; private set; }

        [DataMember(Order = 2)]
        public DateTime SentAtUtc { get; private set; }

        [DataMember(Order = 3)]
        public Player Sender { get; private set; }

        [DataMember(Order = 4)]
        public Player Recipient { get; private set; }

        [DataMember(Order = 5)]
        public string Content { get; private set; }

        public override string ToString()
        {
            return $"{Sender.Name}: {Content}";
        }
    }
}
