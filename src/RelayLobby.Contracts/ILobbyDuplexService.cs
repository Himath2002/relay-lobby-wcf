using System.ServiceModel;

namespace RelayLobby.Contracts
{
    [ServiceContract(Name = "LobbyDuplexService", CallbackContract = typeof(ILobbyCallback))]
    public interface ILobbyDuplexService : ILobbyService
    {
    }
}
