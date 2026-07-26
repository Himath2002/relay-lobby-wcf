using System;
using System.IO;
using System.Runtime.Serialization;

namespace RelayLobby.Domain
{
    [DataContract]
    public sealed class SharedFile
    {
        public SharedFile(string fileName, byte[] fileData, Player sender, Player recipient)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name cannot be empty.", nameof(fileName));
            }

            FileName = Path.GetFileName(fileName);
            FileData = fileData ?? throw new ArgumentNullException(nameof(fileData));
            Sender = sender ?? throw new ArgumentNullException(nameof(sender));
            Recipient = recipient;
        }

        [DataMember(Order = 1)]
        public string FileName { get; private set; }

        [DataMember(Order = 2)]
        public byte[] FileData { get; private set; }

        [DataMember(Order = 3)]
        public Player Sender { get; private set; }

        [DataMember(Order = 4)]
        public Player Recipient { get; private set; }
    }
}
