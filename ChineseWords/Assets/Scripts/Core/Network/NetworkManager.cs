using UnityEngine;
using Riptide;
using Riptide.Utils;
using Core.Network;
using System;

namespace Core.Network
{
    /// <summary>
    /// 客户端网络管理器
    /// 负责与服务端的连接和消息收发
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        [Header("网络配置")]
        [SerializeField] private string serverIP = "127.0.0.1";
        [SerializeField] private ushort port = 7777;
        [SerializeField] private ushort timeoutTime = 5000;

        public static NetworkManager Instance { get; private set; }

        private Client client;

        // 事件
        public static event Action OnConnected;
        public static event Action OnDisconnected;
        public static event Action<NetworkQuestionData> OnQuestionReceived;
        public static event Action<bool, string> OnAnswerResultReceived;
        public static event Action<ushort, int> OnHealthUpdated;
        public static event Action<ushort> OnPlayerTurnChanged;

        // 属性
        public bool IsConnected => client?.IsConnected ?? false;
        public ushort ClientId => client?.Id ?? 0;

        private void Awake()
        {
            // 单例模式
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeClient();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeClient()
        {
            // 设置Riptide日志级别
            RiptideLogger.Initialize(Debug.Log, Debug.Log, Debug.LogWarning, Debug.LogError, false);

            // 创建客户端
            client = new Client();

            // 绑定连接事件
            client.Connected += OnClientConnected;
            client.Disconnected += OnClientDisconnected;
            client.ConnectionFailed += OnClientConnectionFailed;
        }

        private void Update()
        {
            // 每帧处理网络消息
            client?.Update();
        }

        private void OnApplicationQuit()
        {
            Disconnect();
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        /// <summary>
        /// 连接到服务器
        /// </summary>
        public void Connect(string ip = null, ushort? serverPort = null)
        {
            if (IsConnected)
            {
                Debug.LogWarning("已经连接到服务器了");
                return;
            }

            string targetIP = ip ?? serverIP;
            ushort targetPort = serverPort ?? port;

            Debug.Log($"正在连接服务器 {targetIP}:{targetPort}...");
            client.Connect($"{targetIP}:{targetPort}", timeoutTime);
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            if (client != null)
            {
                if (IsConnected)
                {
                    Debug.Log("断开服务器连接");
                }

                client.Disconnect();
                client = null;
            }
        }

        /// <summary>
        /// 发送消息到服务器
        /// </summary>
        public void SendMessage(Message message)
        {
            if (IsConnected)
            {
                client.Send(message);
            }
            else
            {
                Debug.LogWarning("未连接到服务器，无法发送消息");
            }
        }

        /// <summary>
        /// 请求题目
        /// </summary>
        public void RequestQuestion()
        {
            if (!IsConnected)
            {
                Debug.LogWarning("未连接到服务器");
                return;
            }

            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.RequestQuestion);
            SendMessage(message);
            Debug.Log("请求题目...");
        }

        /// <summary>
        /// 提交答案
        /// </summary>
        public void SubmitAnswer(string answer)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("未连接到服务器");
                return;
            }

            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.SubmitAnswer);
            message.AddString(answer);
            SendMessage(message);
            Debug.Log($"提交答案: {answer}");
        }

        #region 连接事件处理

        private void OnClientConnected(object sender, EventArgs e)
        {
            Debug.Log($"成功连接到服务器! 客户端ID: {ClientId}");
            OnConnected?.Invoke();
        }

        private void OnClientDisconnected(object sender, EventArgs e)
        {
            Debug.Log("与服务器断开连接");
            OnDisconnected?.Invoke();
        }

        private void OnClientConnectionFailed(object sender, EventArgs e)
        {
            Debug.LogError("连接服务器失败");
        }

        #endregion

        #region 消息处理器 - 注册接收服务端消息

        [MessageHandler((ushort)NetworkMessageType.SendQuestion)]
        private static void HandleQuestionReceived(Message message)
        {
            byte[] questionData = message.GetBytes();
            NetworkQuestionData question = NetworkQuestionData.Deserialize(questionData);

            if (question != null)
            {
                Debug.Log($"收到题目: {question.questionType} - {question.questionText}");
                OnQuestionReceived?.Invoke(question);
            }
            else
            {
                Debug.LogError("题目数据解析失败");
            }
        }

        [MessageHandler((ushort)NetworkMessageType.AnswerResult)]
        private static void HandleAnswerResult(Message message)
        {
            bool isCorrect = message.GetBool();
            string correctAnswer = message.GetString();

            Debug.Log($"答题结果: {(isCorrect ? "正确" : "错误")} - 正确答案: {correctAnswer}");
            OnAnswerResultReceived?.Invoke(isCorrect, correctAnswer);
        }

        [MessageHandler((ushort)NetworkMessageType.HealthUpdate)]
        private static void HandleHealthUpdate(Message message)
        {
            ushort playerId = message.GetUShort();
            int newHealth = message.GetInt();

            Debug.Log($"玩家 {playerId} 血量更新: {newHealth}");
            OnHealthUpdated?.Invoke(playerId, newHealth);
        }

        [MessageHandler((ushort)NetworkMessageType.PlayerTurnChanged)]
        private static void HandlePlayerTurnChanged(Message message)
        {
            ushort currentPlayerId = message.GetUShort();

            Debug.Log($"轮到玩家 {currentPlayerId} 答题");
            OnPlayerTurnChanged?.Invoke(currentPlayerId);
        }

        #endregion
    }
}