using System;

namespace Networking
{

    /// <summary>
    /// �������紫���ӿڣ��������ӹ�����Ϣ��������ա�������Ͽ��¼���
    /// </summary>
    public interface INetworkTransport
    {
        /// <summary>��ʼ�����ӣ�����ģʽ�ɿ�ʵ�֣�</summary>
        void Initialize();

        /// <summary>����һ����װ�õ�������Ϣ</summary>
        void SendMessage(NetworkMessage msg);

        /// <summary>�յ���Ϣ�ص�</summary>
        event Action<NetworkMessage> OnMessageReceived;

        /// <summary>���ӳɹ��ص�</summary>
        event Action OnConnected;

        /// <summary>���ӶϿ��ص�</summary>
        event Action OnDisconnected;

        /// <summary>����ص��������ϲ㲶�񲢴����쳣</summary>
        event Action<Exception> OnError;

        /// <summary>���Źر�����</summary>
        void Shutdown();
    }
}