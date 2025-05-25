using UnityEngine;
using Riptide;
using Core.Network;

namespace Core.Network
{
    /// <summary>
    /// 主机端消息处理器
    /// 处理来自客户端的消息：题目请求、答案提交等
    /// </summary>
    public static class HostMessageHandlers
    {
        /// <summary>
        /// 处理客户端的题目请求
        /// </summary>
        [MessageHandler((ushort)NetworkMessageType.RequestQuestion)]
        private static void HandleRequestQuestion(ushort fromClientId, Message message)
        {
            Debug.Log($"收到玩家 {fromClientId} 的题目请求");

            // 如果不是Host模式，忽略
            if (NetworkManager.Instance?.IsHost != true)
                return;

            // Host会自动生成和分发题目，这里可以发送当前题目状态
            // 或者触发新题目生成（如果需要）

            // 暂时记录请求，实际的题目分发由HostGameManager处理
        }

        /// <summary>
        /// 处理客户端提交的答案
        /// </summary>
        [MessageHandler((ushort)NetworkMessageType.SubmitAnswer)]
        private static void HandleSubmitAnswer(ushort fromClientId, Message message)
        {
            string answer = message.GetString();
            Debug.Log($"收到玩家 {fromClientId} 的答案: {answer}");

            // 如果不是Host模式，忽略
            if (NetworkManager.Instance?.IsHost != true)
                return;

            // 转发给HostGameManager处理
            if (HostGameManager.Instance != null)
            {
                HostGameManager.Instance.HandlePlayerAnswer(fromClientId, answer);
            }
            else
            {
                Debug.LogWarning("HostGameManager未找到");
            }
        }

        /// <summary>
        /// 处理玩家连接
        /// </summary>
        [MessageHandler((ushort)NetworkMessageType.PlayerJoined)]
        private static void HandlePlayerJoined(ushort fromClientId, Message message)
        {
            Debug.Log($"玩家 {fromClientId} 加入游戏");

            // 发送欢迎消息或当前游戏状态
            SendWelcomeMessage(fromClientId);
        }

        /// <summary>
        /// 发送欢迎消息给新加入的玩家
        /// </summary>
        private static void SendWelcomeMessage(ushort clientId)
        {
            if (NetworkManager.Instance?.IsHost != true)
                return;

            // 可以发送当前游戏状态、房间信息等
            Debug.Log($"向玩家 {clientId} 发送欢迎消息");

            // 这里可以发送自定义的欢迎消息
            // Message welcomeMessage = Message.Create(MessageSendMode.Reliable, CustomMessageType.Welcome);
            // welcomeMessage.AddString("欢迎加入游戏！");
            // NetworkManager.Instance.SendMessageToClient(clientId, welcomeMessage);
        }
    }
}