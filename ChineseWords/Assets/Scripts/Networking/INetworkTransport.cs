using System;

public interface INetworkTransport
{
    void SendMessage(NetworkMessage msg);
    event Action<NetworkMessage> OnMessageReceived;
}
