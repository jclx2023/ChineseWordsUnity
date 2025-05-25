using UnityEngine;
using Riptide;
using Riptide.Utils;
using Core.Network;
using System;
using System.Collections;
using UI;

namespace Core.Network
{
    /// <summary>
    /// 网络管理器（支持Host-Client架构）
    /// 支持Host-Client架构：既可以作为主机（服务器+客户端），也可以作为纯客户端
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        [Header("网络配置")]
        [SerializeField] private ushort defaultPort = 7777;
        [SerializeField] private ushort maxClients = 8;
        [SerializeField] private ushort timeoutTime = 5000;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        public static NetworkManager Instance { get; private set; }

        // 网络组件
        private Server server;  // Host模式下的服务器
        private Client client;  // 客户端（Host和Client模式都需要）

        // 模式状态
        public bool IsHost { get; private set; }
        public bool IsClient => client?.IsConnected ?? false;
        public bool IsServer => server?.IsRunning ?? false;
        public bool IsConnected => IsClient; // 保持与原NetworkManager的兼容性
        public ushort ClientId => client?.Id ?? 0;
        public ushort Port { get; private set; }

        // Host模式下的服务器信息
        public string RoomName { get; private set; }
        public int MaxPlayers { get; private set; }
        public int ConnectedPlayerCount => server?.ClientCount ?? 0;

        // 初始化状态
        private bool isHostInitialized = false;

        // 事件（保持与原NetworkManager兼容）
        public static event Action OnConnected;
        public static event Action OnDisconnected;
        public static event Action<NetworkQuestionData> OnQuestionReceived;
        public static event Action<bool, string> OnAnswerResultReceived;
        public static event Action<ushort, int> OnHealthUpdated;
        public static event Action<ushort> OnPlayerTurnChanged;

        // 新增Host-Client特有事件
        public static event Action OnHostStarted;
        public static event Action OnHostStopped;
        public static event Action<ushort> OnPlayerJoined;  // 有新玩家加入
        public static event Action<ushort> OnPlayerLeft;    // 有玩家离开

        private void Awake()
        {
            LogDebug($"NetworkManager Awake 执行时间: {Time.time}");

            // 单例模式
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeNetwork();
                LogDebug("NetworkManager 单例已创建");
            }
            else
            {
                LogDebug("销毁重复的NetworkManager实例");
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            LogDebug($"NetworkManager Start 执行时间: {Time.time}");
            // 根据主菜单选择的模式进行初始化
            InitializeFromMainMenu();
        }

        private void Update()
        {
            // 更新网络组件
            if (server != null && server.IsRunning)
                server.Update();

            if (client != null)
                client.Update();
        }

        private void OnApplicationQuit()
        {
            Shutdown();
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        /// <summary>
        /// 初始化网络组件
        /// </summary>
        private void InitializeNetwork()
        {
            // 设置Riptide日志级别
            RiptideLogger.Initialize(Debug.Log, Debug.Log, Debug.LogWarning, Debug.LogError, false);
            LogDebug("Riptide日志系统初始化完成");
        }

        /// <summary>
        /// 根据主菜单选择初始化
        /// </summary>
        private void InitializeFromMainMenu()
        {
            LogDebug($"根据主菜单初始化，选定模式: {MainMenuManager.SelectedGameMode}");
            LogDebug($"MainMenuManager 配置 - Port: {MainMenuManager.Port}, RoomName: {MainMenuManager.RoomName}, MaxPlayers: {MainMenuManager.MaxPlayers}");

            switch (MainMenuManager.SelectedGameMode)
            {
                case MainMenuManager.GameMode.Host:
                    // 使用协程来确保正确的初始化顺序
                    StartCoroutine(StartHostWithDelay());
                    break;

                case MainMenuManager.GameMode.Client:
                    Connect(MainMenuManager.HostIP, MainMenuManager.Port);
                    break;

                case MainMenuManager.GameMode.SinglePlayer:
                    LogDebug("单机模式，不需要网络连接");
                    break;

                default:
                    Debug.LogWarning("未知的游戏模式");
                    break;
            }
        }

        /// <summary>
        /// 延迟启动Host，确保所有组件都已准备就绪
        /// </summary>
        private IEnumerator StartHostWithDelay()
        {
            LogDebug("开始Host启动流程...");

            // 等待几帧确保所有组件初始化完成
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            // 启动Host
            StartAsHost(MainMenuManager.Port, MainMenuManager.RoomName, MainMenuManager.MaxPlayers);
        }

        /// <summary>
        /// 连接到服务器（保持与原NetworkManager兼容）
        /// </summary>
        public void Connect(string ip = null, ushort? serverPort = null)
        {
            string targetIP = ip ?? "127.0.0.1";
            ushort targetPort = serverPort ?? defaultPort;

            ConnectAsClient(targetIP, targetPort);
        }

        /// <summary>
        /// 断开连接（保持与原NetworkManager兼容）
        /// </summary>
        public void Disconnect()
        {
            if (IsHost)
                StopHost();
            else
                DisconnectClient();
        }

        /// <summary>
        /// 作为主机启动（Host模式）
        /// </summary>
        public void StartAsHost(ushort port, string roomName, int maxPlayers)
        {
            if (IsHost || IsServer)
            {
                LogDebug("已经在运行主机模式");
                return;
            }

            Port = port;
            RoomName = roomName;
            MaxPlayers = maxPlayers;

            LogDebug($"启动主机模式 - 房间: {roomName}, 端口: {port}, 最大玩家: {maxPlayers}");

            try
            {
                // 启动服务器
                server = new Server();
                server.ClientConnected += OnServerClientConnected;
                server.ClientDisconnected += OnServerClientDisconnected;

                LogDebug("正在启动服务器...");
                server.Start(port, (ushort)maxPlayers);
                LogDebug($"服务器启动成功，IsRunning: {server.IsRunning}");

                // 设置主机状态
                IsHost = true;
                LogDebug($"IsHost 设置为: {IsHost}");

                // 立即触发 OnHostStarted 事件
                LogDebug("触发 OnHostStarted 事件");
                OnHostStarted?.Invoke();

                // 同时作为客户端连接到自己的服务器（延迟一点确保服务器完全启动）
                StartCoroutine(ConnectSelfClientWithDelay(port));
            }
            catch (System.Exception e)
            {
                Debug.LogError($"启动主机失败: {e.Message}");
                StopHost();
            }
        }

        /// <summary>
        /// 延迟连接自己的客户端到服务器
        /// </summary>
        private IEnumerator ConnectSelfClientWithDelay(ushort port)
        {
            // 等待服务器完全启动
            yield return new WaitForSeconds(0.1f);

            LogDebug("开始连接自己的客户端到服务器...");

            try
            {
                client = new Client();
                client.Connected += OnSelfClientConnected;
                client.Disconnected += OnSelfClientDisconnected;
                client.ConnectionFailed += OnSelfClientConnectionFailed;

                LogDebug($"客户端连接到: 127.0.0.1:{port}");
                client.Connect($"127.0.0.1:{port}", timeoutTime);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"自连接失败: {e.Message}");
                StopHost();
            }
        }

        /// <summary>
        /// 作为客户端连接（Client模式）
        /// </summary>
        public void ConnectAsClient(string hostIP, ushort port)
        {
            if (IsClient)
            {
                LogDebug("已经连接到主机");
                return;
            }

            Port = port;
            IsHost = false;

            LogDebug($"连接到主机: {hostIP}:{port}");

            try
            {
                client = new Client();
                client.Connected += OnClientConnectedToHost;
                client.Disconnected += OnClientDisconnectedFromHost;
                client.ConnectionFailed += OnClientConnectionFailed;
                client.Connect($"{hostIP}:{port}", timeoutTime);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"连接主机失败: {e.Message}");
            }
        }

        /// <summary>
        /// 停止主机
        /// </summary>
        public void StopHost()
        {
            if (!IsHost)
                return;

            LogDebug("停止主机");

            // 停止服务器
            if (server != null)
            {
                server.ClientConnected -= OnServerClientConnected;
                server.ClientDisconnected -= OnServerClientDisconnected;
                server.Stop();
                server = null;
                LogDebug("服务器已停止");
            }

            // 断开客户端
            DisconnectClient();

            IsHost = false;
            isHostInitialized = false;

            LogDebug("触发 OnHostStopped 事件");
            OnHostStopped?.Invoke();
        }

        /// <summary>
        /// 断开客户端连接
        /// </summary>
        public void DisconnectClient()
        {
            if (client != null)
            {
                if (IsClient)
                {
                    LogDebug("断开客户端连接");
                    client.Disconnect();
                }

                // 清理事件订阅
                client.Connected -= OnSelfClientConnected;
                client.Connected -= OnClientConnectedToHost;
                client.Disconnected -= OnSelfClientDisconnected;
                client.Disconnected -= OnClientDisconnectedFromHost;
                client.ConnectionFailed -= OnSelfClientConnectionFailed;
                client.ConnectionFailed -= OnClientConnectionFailed;

                client = null;
            }
        }

        /// <summary>
        /// 完全关闭网络
        /// </summary>
        public void Shutdown()
        {
            LogDebug("网络管理器关闭");
            StopHost();
            DisconnectClient();
        }

        /// <summary>
        /// 发送消息（保持与原NetworkManager兼容）
        /// </summary>
        public void SendMessage(Message message)
        {
            if (IsClient)
            {
                client.Send(message);
            }
            else
            {
                Debug.LogWarning("未连接到服务器，无法发送消息");
            }
        }

        /// <summary>
        /// 广播消息给所有客户端（主机使用）
        /// </summary>
        public void BroadcastMessage(Message message)
        {
            if (IsServer)
            {
                server.SendToAll(message);
            }
            else
            {
                Debug.LogWarning("不是主机，无法广播消息");
            }
        }

        /// <summary>
        /// 发送消息给指定客户端（主机使用）
        /// </summary>
        public void SendMessageToClient(ushort clientId, Message message)
        {
            if (IsServer)
            {
                server.Send(message, clientId);
            }
            else
            {
                Debug.LogWarning("不是主机，无法发送消息给特定客户端");
            }
        }

        #region 服务器事件处理（Host模式）

        private void OnServerClientConnected(object sender, ServerConnectedEventArgs e)
        {
            LogDebug($"玩家加入房间: ID={e.Client.Id}");
            OnPlayerJoined?.Invoke(e.Client.Id);
        }

        private void OnServerClientDisconnected(object sender, ServerDisconnectedEventArgs e)
        {
            LogDebug($"玩家离开房间: ID={e.Client.Id}");
            OnPlayerLeft?.Invoke(e.Client.Id);
        }

        #endregion

        #region 客户端事件处理（Host模式下的自连接）

        private void OnSelfClientConnected(object sender, EventArgs e)
        {
            LogDebug($"主机客户端连接成功! ID: {ClientId}");

            if (!isHostInitialized)
            {
                isHostInitialized = true;
                LogDebug("主机完全初始化完成");

                // 如果还没有触发过 OnHostStarted，在这里再次触发
                // 这确保了即使时序有问题也能正确通知
                OnHostStarted?.Invoke();
            }

            OnConnected?.Invoke(); // 触发兼容事件
        }

        private void OnSelfClientDisconnected(object sender, EventArgs e)
        {
            LogDebug("主机客户端断开连接");
            OnDisconnected?.Invoke(); // 触发兼容事件
        }

        private void OnSelfClientConnectionFailed(object sender, EventArgs e)
        {
            Debug.LogError("主机客户端连接失败");
            StopHost();
        }

        #endregion

        #region 客户端事件处理（Client模式）

        private void OnClientConnectedToHost(object sender, EventArgs e)
        {
            LogDebug($"成功连接到主机! ID: {ClientId}");
            OnConnected?.Invoke(); // 触发兼容事件
        }

        private void OnClientDisconnectedFromHost(object sender, EventArgs e)
        {
            LogDebug("与主机断开连接");
            OnDisconnected?.Invoke(); // 触发兼容事件
        }

        private void OnClientConnectionFailed(object sender, EventArgs e)
        {
            Debug.LogError("连接主机失败");
        }

        #endregion

        #region 游戏消息处理器（保持与原NetworkManager兼容）

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

        #region 公共接口方法（保持兼容）

        /// <summary>
        /// 获取房间信息
        /// </summary>
        public string GetRoomInfo()
        {
            if (IsHost)
            {
                return $"房间: {RoomName} | 玩家: {ConnectedPlayerCount}/{MaxPlayers} | 端口: {Port}";
            }
            else if (IsClient)
            {
                return $"已连接到主机 | 玩家ID: {ClientId}";
            }
            else
            {
                return "未连接";
            }
        }

        /// <summary>
        /// 请求题目（客户端调用）
        /// </summary>
        public void RequestQuestion()
        {
            if (!IsClient)
            {
                Debug.LogWarning("未连接到服务器");
                return;
            }

            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.RequestQuestion);
            SendMessage(message);
            LogDebug("请求题目...");
        }

        /// <summary>
        /// 提交答案（客户端调用）
        /// </summary>
        public void SubmitAnswer(string answer)
        {
            if (!IsClient)
            {
                Debug.LogWarning("未连接到服务器");
                return;
            }

            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.SubmitAnswer);
            message.AddString(answer);
            SendMessage(message);
            LogDebug($"提交答案: {answer}");
        }

        /// <summary>
        /// 获取当前网络状态（调试用）
        /// </summary>
        public string GetNetworkStatus()
        {
            return $"IsHost: {IsHost}, IsServer: {IsServer}, IsClient: {IsClient}, " +
                   $"IsConnected: {IsConnected}, ClientId: {ClientId}, " +
                   $"HostInitialized: {isHostInitialized}";
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[NetworkManager] {message}");
            }
        }

        #endregion
    }
}