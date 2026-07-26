using System;
using System.ServiceModel;
using RelayLobby.Contracts;

namespace RelayLobby.Server
{
    internal static class Program
    {
        private static int Main()
        {
            var host = new ServiceHost(typeof(LobbyService));
            NetTcpBinding binding = TransportConfiguration.CreateBinding();

            host.AddServiceEndpoint(
                typeof(ILobbyService),
                binding,
                TransportConfiguration.PollingListenAddress);
            host.AddServiceEndpoint(
                typeof(ILobbyDuplexService),
                binding,
                TransportConfiguration.DuplexListenAddress);

            try
            {
                host.Open();

                Console.WriteLine("RelayLobby server is online.");
                Console.WriteLine(
                    $"  Polling endpoint: {TransportConfiguration.PollingClientAddress}");
                Console.WriteLine(
                    $"  Duplex endpoint : {TransportConfiguration.DuplexClientAddress}");
                Console.WriteLine("Press ENTER to stop.");
                Console.ReadLine();
                return 0;
            }
            catch (AddressAlreadyInUseException exception)
            {
                Console.Error.WriteLine(
                    $"Port {TransportConfiguration.Port} is already in use: " +
                    exception.Message);
                return 1;
            }
            catch (CommunicationException exception)
            {
                Console.Error.WriteLine(
                    "The WCF service could not start: " + exception.Message);
                return 1;
            }
            finally
            {
                CloseHost(host);
            }
        }

        private static void CloseHost(ServiceHost host)
        {
            try
            {
                if (host.State == CommunicationState.Faulted)
                {
                    host.Abort();
                }
                else
                {
                    host.Close();
                }
            }
            catch (CommunicationException)
            {
                host.Abort();
            }
            catch (TimeoutException)
            {
                host.Abort();
            }
        }
    }
}
