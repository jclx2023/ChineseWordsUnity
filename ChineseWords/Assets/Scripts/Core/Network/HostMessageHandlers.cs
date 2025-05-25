using UnityEngine;
using Riptide;
using Core.Network;

namespace Core.Network
{
    /// <summary>
    /// 主机端消息处理器
    /// 处理来自客户端的消息：题目请求、答案提交等
    /// 注意：这个类的方法会自动被Riptide调用，不需要手动实例化
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
            {
                Debug.LogWarning("收到题目请求，但当前不是Host模式");
                return;
            }

            // Host会自动生成和分发题目，这里可以发送当前题目状态
            // 或者触发新题目生成（如果需要）

            // 记录请求，实际的题目分发由HostGameManager处理
            Debug.Log($"已记录玩家 {fromClientId} 的题目请求");
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
            {
                Debug.LogWarning("收到答案提交，但当前不是Host模式");
                return;
            }

            // 转发给HostGameManager处理
            if (HostGameManager.Instance != null)
            {
                HostGameManager.Instance.HandlePlayerAnswer(fromClientId, answer);
            }
            else
            {
                Debug.LogWarning("HostGameManager未找到，无法处理答案");
            }
        }

        /// <summary>
        /// 处理玩家连接（可选）
        /// 注意：玩家连接/断开主要通过NetworkManager的事件处理，这里可以处理额外的游戏逻辑
        /// </summary>
        [MessageHandler((ushort)NetworkMessageType.PlayerJoined)]
        private static void HandlePlayerJoined(ushort fromClientId, Message message)
        {
            Debug.Log($"玩家 {fromClientId} 加入游戏");

            // 如果不是Host模式，忽略
            if (NetworkManager.Instance?.IsHost != true)
                return;

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
            Debug.Log($"向玩家 {clientId} 发送欢迎信息");

            // 这里可以发送自定义的欢迎消息
            // Message welcomeMessage = Message.Create(MessageSendMode.Reliable, CustomMessageType.Welcome);
            // welcomeMessage.AddString("欢迎加入游戏！");
            // NetworkManager.Instance.SendMessageToClient(clientId, welcomeMessage);

            // 或者发送当前游戏状态
            if (HostGameManager.Instance != null)
            {
                // 可以发送当前玩家列表、游戏进度等信息
                SendGameStatusToClient(clientId);
            }
        }

        /// <summary>
        /// 发送游戏状态给指定客户端
        /// </summary>
        private static void SendGameStatusToClient(ushort clientId)
        {
            if (NetworkManager.Instance?.IsHost != true || HostGameManager.Instance == null)
                return;

            // 这里可以发送当前游戏状态
            Debug.Log($"向玩家 {clientId} 发送游戏状态");

            // 示例：发送当前是否在游戏中、轮到谁答题等信息
            // 可以根据需要扩展消息类型和内容
        }

        /// <summary>
        /// 处理客户端的游戏状态查询（可选扩展）
        /// </summary>
        /*
        [MessageHandler((ushort)NetworkMessageType.RequestGameStatus)]
        private static void HandleRequestGameStatus(ushort fromClientId, Message message)
        {
            Debug.Log($"收到玩家 {fromClientId} 的游戏状态查询");
            
            if (NetworkManager.Instance?.IsHost != true)
                return;
            
            SendGameStatusToClient(fromClientId);
        }
        */

        /// <summary>
        /// 处理客户端的聊天消息（可选扩展）
        /// </summary>
        /*
        [MessageHandler((ushort)NetworkMessageType.ChatMessage)]
        private static void HandleChatMessage(ushort fromClientId, Message message)
        {
            string chatMessage = message.GetString();
            Debug.Log($"玩家 {fromClientId} 发送聊天消息: {chatMessage}");
            
            if (NetworkManager.Instance?.IsHost != true)
                return;
            
            // 广播聊天消息给所有客户端
            Message broadcastMessage = Message.Create(MessageSendMode.Reliable, NetworkMessageType.ChatMessage);
            broadcastMessage.AddUShort(fromClientId);
            broadcastMessage.AddString(chatMessage);
            NetworkManager.Instance.BroadcastMessage(broadcastMessage);
        }
        */
    }
}