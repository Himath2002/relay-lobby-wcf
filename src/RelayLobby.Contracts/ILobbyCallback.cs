using System.Collections.Generic;
using System.ServiceModel;
using RelayLobby.Domain;

namespace RelayLobby.Contracts
{
    [ServiceContract]
    public interface ILobbyCallback
    {
        [OperationContract(IsOneWay = true)]
        void UpdateRoomState(
            List<Message> publicMessages,
            List<Message> privateMessages,
            List<SharedFile> publicFiles,
            List<SharedFile> privateFiles,
            List<Player> players);
    }
}
