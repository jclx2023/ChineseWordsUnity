using UnityEngine;
using Riptide;
using Core.Network;

namespace Core.Network
{
    /// <summary>
    /// ��������Ϣ������
    /// �������Կͻ��˵���Ϣ����Ŀ���󡢴��ύ��
    /// ע�⣺�����ķ������Զ���Riptide���ã�����Ҫ�ֶ�ʵ����
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
            {
                Debug.LogWarning("�յ���Ŀ���󣬵���ǰ����Hostģʽ");
                return;
            }

            // Host���Զ����ɺͷַ���Ŀ��������Է��͵�ǰ��Ŀ״̬
            // ���ߴ�������Ŀ���ɣ������Ҫ��

            // ��¼����ʵ�ʵ���Ŀ�ַ���HostGameManager����
            Debug.Log($"�Ѽ�¼��� {fromClientId} ����Ŀ����");
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
            {
                Debug.LogWarning("�յ����ύ������ǰ����Hostģʽ");
                return;
            }

            // ת����HostGameManager����
            if (HostGameManager.Instance != null)
            {
                HostGameManager.Instance.HandlePlayerAnswer(fromClientId, answer);
            }
            else
            {
                Debug.LogWarning("HostGameManagerδ�ҵ����޷������");
            }
        }

        /// <summary>
        /// ����������ӣ���ѡ��
        /// ע�⣺�������/�Ͽ���Ҫͨ��NetworkManager���¼�����������Դ���������Ϸ�߼�
        /// </summary>
        [MessageHandler((ushort)NetworkMessageType.PlayerJoined)]
        private static void HandlePlayerJoined(ushort fromClientId, Message message)
        {
            Debug.Log($"��� {fromClientId} ������Ϸ");

            // �������Hostģʽ������
            if (NetworkManager.Instance?.IsHost != true)
                return;

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

            // ���߷��͵�ǰ��Ϸ״̬
            if (HostGameManager.Instance != null)
            {
                // ���Է��͵�ǰ����б���Ϸ���ȵ���Ϣ
                SendGameStatusToClient(clientId);
            }
        }

        /// <summary>
        /// ������Ϸ״̬��ָ���ͻ���
        /// </summary>
        private static void SendGameStatusToClient(ushort clientId)
        {
            if (NetworkManager.Instance?.IsHost != true || HostGameManager.Instance == null)
                return;

            // ������Է��͵�ǰ��Ϸ״̬
            Debug.Log($"����� {clientId} ������Ϸ״̬");

            // ʾ�������͵�ǰ�Ƿ�����Ϸ�С��ֵ�˭�������Ϣ
            // ���Ը�����Ҫ��չ��Ϣ���ͺ�����
        }

        /// <summary>
        /// ����ͻ��˵���Ϸ״̬��ѯ����ѡ��չ��
        /// </summary>
        /*
        [MessageHandler((ushort)NetworkMessageType.RequestGameStatus)]
        private static void HandleRequestGameStatus(ushort fromClientId, Message message)
        {
            Debug.Log($"�յ���� {fromClientId} ����Ϸ״̬��ѯ");
            
            if (NetworkManager.Instance?.IsHost != true)
                return;
            
            SendGameStatusToClient(fromClientId);
        }
        */

        /// <summary>
        /// ����ͻ��˵�������Ϣ����ѡ��չ��
        /// </summary>
        /*
        [MessageHandler((ushort)NetworkMessageType.ChatMessage)]
        private static void HandleChatMessage(ushort fromClientId, Message message)
        {
            string chatMessage = message.GetString();
            Debug.Log($"��� {fromClientId} ����������Ϣ: {chatMessage}");
            
            if (NetworkManager.Instance?.IsHost != true)
                return;
            
            // �㲥������Ϣ�����пͻ���
            Message broadcastMessage = Message.Create(MessageSendMode.Reliable, NetworkMessageType.ChatMessage);
            broadcastMessage.AddUShort(fromClientId);
            broadcastMessage.AddString(chatMessage);
            NetworkManager.Instance.BroadcastMessage(broadcastMessage);
        }
        */
    }
}