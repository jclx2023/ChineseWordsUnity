using UnityEngine;
using Riptide;
using Core.Network;

namespace Core.Network
{
    /// <summary>
    /// ��������Ϣ������
    /// �������Կͻ��˵���Ϣ����Ŀ���󡢴��ύ��
    /// </summary>
    public static class HostMessageHandlers
    {
        /// <summary>
        /// ����ͻ��˵���Ŀ����
        /// </summary>
        [MessageHandler((ushort)NetworkMessageType.RequestQuestion)]
        private static void HandleRequestQuestion(ushort fromClientId, Message message)
        {
            Debug.Log($"�յ���� {fromClientId} ����Ŀ����");

            // �������Hostģʽ������
            if (NetworkManager.Instance?.IsHost != true)
                return;

            // Host���Զ����ɺͷַ���Ŀ��������Է��͵�ǰ��Ŀ״̬
            // ���ߴ�������Ŀ���ɣ������Ҫ��

            // ��ʱ��¼����ʵ�ʵ���Ŀ�ַ���HostGameManager����
        }

        /// <summary>
        /// ����ͻ����ύ�Ĵ�
        /// </summary>
        [MessageHandler((ushort)NetworkMessageType.SubmitAnswer)]
        private static void HandleSubmitAnswer(ushort fromClientId, Message message)
        {
            string answer = message.GetString();
            Debug.Log($"�յ���� {fromClientId} �Ĵ�: {answer}");

            // �������Hostģʽ������
            if (NetworkManager.Instance?.IsHost != true)
                return;

            // ת����HostGameManager����
            if (HostGameManager.Instance != null)
            {
                HostGameManager.Instance.HandlePlayerAnswer(fromClientId, answer);
            }
            else
            {
                Debug.LogWarning("HostGameManagerδ�ҵ�");
            }
        }

        /// <summary>
        /// �����������
        /// </summary>
        [MessageHandler((ushort)NetworkMessageType.PlayerJoined)]
        private static void HandlePlayerJoined(ushort fromClientId, Message message)
        {
            Debug.Log($"��� {fromClientId} ������Ϸ");

            // ���ͻ�ӭ��Ϣ��ǰ��Ϸ״̬
            SendWelcomeMessage(fromClientId);
        }

        /// <summary>
        /// ���ͻ�ӭ��Ϣ���¼�������
        /// </summary>
        private static void SendWelcomeMessage(ushort clientId)
        {
            if (NetworkManager.Instance?.IsHost != true)
                return;

            // ���Է��͵�ǰ��Ϸ״̬��������Ϣ��
            Debug.Log($"����� {clientId} ���ͻ�ӭ��Ϣ");

            // ������Է����Զ���Ļ�ӭ��Ϣ
            // Message welcomeMessage = Message.Create(MessageSendMode.Reliable, CustomMessageType.Welcome);
            // welcomeMessage.AddString("��ӭ������Ϸ��");
            // NetworkManager.Instance.SendMessageToClient(clientId, welcomeMessage);
        }
    }
}