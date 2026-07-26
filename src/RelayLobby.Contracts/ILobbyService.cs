using System.Collections.Generic;
using System.ServiceModel;
using RelayLobby.Domain;

namespace RelayLobby.Contracts
{
    [ServiceContract(Name = "LobbyService")]
    public interface ILobbyService
    {
        [OperationContract]
        bool Login(string username);

        [OperationContract]
        void Logout(string username);

        [OperationContract]
        void CreateLobbyRoom(string roomName);

        [OperationContract]
        void JoinRoom(string username, string roomName);

        [OperationContract]
        void LeaveRoom(string username);

        [OperationContract]
        void SendPublicMessage(string senderName, string messageContent);

        [OperationContract]
        void SendPrivateMessage(string senderName, string recipientName, string messageContent);

        [OperationContract]
        void SendPublicFile(string senderName, string fileName, byte[] file);

        [OperationContract]
        void SendPrivateFile(string senderName, string recipientName, string fileName, byte[] file);

        [OperationContract]
        List<Message> GetPublicMessages(string username);

        [OperationContract]
        List<Message> GetPrivateMessages(string username, string otherUser);

        [OperationContract]
        List<SharedFile> GetPublicFiles(string username);

        [OperationContract]
        List<SharedFile> GetPrivateFiles(string username, string otherUser);

        [OperationContract]
        List<Player> GetPlayersInRoom(string username);

        [OperationContract]
        List<LobbyRoomSummary> GetLobbyRooms();
    }
}
