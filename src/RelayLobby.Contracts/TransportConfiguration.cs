using System;
using System.ServiceModel;
using System.Xml;

namespace RelayLobby.Contracts
{
    public static class TransportConfiguration
    {
        public const int Port = 8100;
        public const int MaxFileBytes = 10 * 1024 * 1024;
        public const int MaxMessageLength = 500;
        public const int MaxUsernameLength = 40;
        public const int MaxRoomNameLength = 60;

        private const int MaxTransportBytes = 12 * 1024 * 1024;

        public const string PollingClientAddress =
            "net.tcp://localhost:8100/RelayLobby/Polling";

        public const string DuplexClientAddress =
            "net.tcp://localhost:8100/RelayLobby/Duplex";

        public const string PollingListenAddress =
            "net.tcp://localhost:8100/RelayLobby/Polling";

        public const string DuplexListenAddress =
            "net.tcp://localhost:8100/RelayLobby/Duplex";

        public static NetTcpBinding CreateBinding()
        {
            var binding = new NetTcpBinding(SecurityMode.None)
            {
                OpenTimeout = TimeSpan.FromSeconds(15),
                CloseTimeout = TimeSpan.FromSeconds(15),
                ReceiveTimeout = TimeSpan.FromMinutes(10),
                SendTimeout = TimeSpan.FromSeconds(30),
                MaxReceivedMessageSize = MaxTransportBytes,
                MaxBufferSize = MaxTransportBytes,
                MaxBufferPoolSize = MaxTransportBytes * 2L,
                TransferMode = TransferMode.Buffered
            };

            binding.ReaderQuotas = new XmlDictionaryReaderQuotas
            {
                MaxStringContentLength = MaxMessageLength * 4,
                MaxArrayLength = MaxTransportBytes,
                MaxBytesPerRead = 64 * 1024,
                MaxDepth = 32,
                MaxNameTableCharCount = 16 * 1024
            };

            return binding;
        }
    }
}
