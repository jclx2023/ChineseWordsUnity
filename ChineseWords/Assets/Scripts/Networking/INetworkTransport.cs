using System;

namespace Networking
{

    /// <summary>
    /// 定义网络传输层接口：负责连接管理、消息发送与接收、错误与断开事件。
    /// </summary>
    public interface INetworkTransport
    {
        /// <summary>初始化连接（单机模式可空实现）</summary>
        void Initialize();

        /// <summary>发送一条封装好的网络消息</summary>
        void SendMessage(NetworkMessage msg);

        /// <summary>收到消息回调</summary>
        event Action<NetworkMessage> OnMessageReceived;

        /// <summary>连接成功回调</summary>
        event Action OnConnected;

        /// <summary>连接断开回调</summary>
        event Action OnDisconnected;

        /// <summary>错误回调，用于上层捕获并处理异常</summary>
        event Action<Exception> OnError;

        /// <summary>优雅关闭连接</summary>
        void Shutdown();
    }
}