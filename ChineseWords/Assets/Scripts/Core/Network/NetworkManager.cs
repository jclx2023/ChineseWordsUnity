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
    /// 修复版本：解决Host ID不一致问题，统一Host玩家身份管理
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        [Header("网络配置")]
        [SerializeField] private ushort defaultPort = 7777;
        [SerializeField] private ushort maxClients = 8;
        [SerializeField] private ushort timeoutTime = 20000;

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

        // **关键修复：统一Host身份管理**
        private ushort hostPlayerId = 0;  // Host作为玩家的真实ID
        private bool isHostClientReady = false;  // Host客户端是否准备就绪

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
        public static event Action<ushort, int, int> OnHealthUpdated;
        public static event Action<ushort> OnPlayerTurnChanged;

        // 新增Host-Client特有事件
        public static event Action OnHostStarted;
        public static event Action OnHostStopped;
        public static event Action<ushort> OnPlayerJoined;
        public static event Action<ushort> OnPlayerLeft;

        // **新增：Host玩家准备就绪事件**
        public static event Action OnHostPlayerReady;  // Host作为玩家准备就绪


        private ushort lastTurnPlayerId = 0;  // 上一次的回合玩家ID
        private int gameProgressSequence = 0;  // 游戏进度序列号
        private bool gameStartReceived = false;  // 是否已收到游戏开始消息

        // 消息缓冲区（防止消息乱序）
        private struct PendingQuestionMessage
        {
            public NetworkQuestionData question;
            public float receivedTime;
        }
        private PendingQuestionMessage? pendingQuestion = null;
        private const float QUESTION_BUFFER_TIMEOUT = 2f;  // 缓冲超时时间

        private void Awake()
        {
            Application.runInBackground = true;
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

            // 检查当前场景，如果是主菜单场景则不自动初始化
            string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (IsMainMenuScene(currentSceneName))
            {
                LogDebug("检测到主菜单场景，NetworkManager保持待命状态");
                return;
            }

            // 只有在网络游戏场景才根据主菜单选择的模式进行初始化
            InitializeFromMainMenu();
        }

        /// <summary>
        /// 检查是否为主菜单场景
        /// </summary>
        private bool IsMainMenuScene(string sceneName)
        {
            string[] mainMenuScenes = { "MainMenuScene", "MainMenu", "Menu" };

            foreach (string menuScene in mainMenuScenes)
            {
                if (sceneName.Equals(menuScene, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 手动启动网络初始化（从MainMenuManager调用）
        /// </summary>
        public void ManualInitializeNetwork()
        {
            LogDebug("手动启动网络初始化");
            InitializeFromMainMenu();
        }

        private void Update()
        {
            // 更新网络组件（原有逻辑）
            if (server != null && server.IsRunning)
                server.Update();

            if (client != null)
                client.Update();

            // 新增：检查待处理消息
            CheckPendingMessages();
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
        /// 作为主机启动（Host模式）- 修复版本
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

            // **重置Host状态**
            hostPlayerId = 0;
            isHostClientReady = false;

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

                // **修改：先触发服务器启动事件，但不触发Host玩家准备事件**
                LogDebug("触发 OnHostStarted 事件（服务器层启动）");
                OnHostStarted?.Invoke();

                // 同时作为客户端连接到自己的服务器
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

            // **重置Host状态**
            IsHost = false;
            isHostInitialized = false;
            hostPlayerId = 0;
            isHostClientReady = false;

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

                // 清理事件订阅（原有逻辑）
                client.Connected -= OnSelfClientConnected;
                client.Connected -= OnClientConnectedToHost;
                client.Disconnected -= OnSelfClientDisconnected;
                client.Disconnected -= OnClientDisconnectedFromHost;
                client.ConnectionFailed -= OnSelfClientConnectionFailed;
                client.ConnectionFailed -= OnClientConnectionFailed;

                client = null;
            }

            // 新增：清理消息状态
            gameStartReceived = false;
            lastTurnPlayerId = 0;
            gameProgressSequence = 0;
            pendingQuestion = null;
            LogDebug("网络消息状态已清理");
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

        #region 统一Host身份管理接口

        /// <summary>
        /// 获取Host玩家ID（统一接口）
        /// </summary>
        public ushort GetHostPlayerId()
        {
            return hostPlayerId;
        }

        /// <summary>
        /// 检查指定ID是否为Host玩家
        /// </summary>
        public bool IsHostPlayer(ushort playerId)
        {
            return IsHost && playerId == hostPlayerId;
        }

        /// <summary>
        /// Host客户端是否准备就绪
        /// </summary>
        public bool IsHostClientReady => isHostClientReady;

        /// <summary>
        /// 获取用于房间创建的Host信息
        /// </summary>
        public (ushort hostId, bool isReady) GetHostRoomInfo()
        {
            return (hostPlayerId, isHostClientReady);
        }

        #endregion

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

        #region 房间管理消息处理

        /// <summary>
        /// 发送房间数据给指定客户端
        /// </summary>
        public void SendRoomDataToClient(ushort clientId, RoomData roomData)
        {
            if (!IsHost || server == null)
                return;

            Message message = Message.Create(MessageSendMode.Reliable, (ushort)NetworkMessageType.RoomDataSync);
            message.SerializeRoomData(roomData);
            server.Send(message, clientId);

            LogDebug($"发送房间数据给客户端 {clientId}");
        }

        /// <summary>
        /// 广播玩家加入房间
        /// </summary>
        public void BroadcastPlayerJoinRoom(RoomPlayer player)
        {
            if (!IsHost || server == null)
                return;

            Message message = Message.Create(MessageSendMode.Reliable, (ushort)NetworkMessageType.PlayerJoinRoom);
            message.SerializePlayer(player);
            server.SendToAll(message);

            LogDebug($"广播玩家加入: {player.playerName}");
        }

        /// <summary>
        /// 广播玩家离开房间
        /// </summary>
        public void BroadcastPlayerLeaveRoom(ushort playerId)
        {
            if (!IsHost || server == null)
                return;

            Message message = Message.Create(MessageSendMode.Reliable, (ushort)NetworkMessageType.PlayerLeaveRoom);
            message.AddUShort(playerId);
            server.SendToAll(message);

            LogDebug($"广播玩家离开: {playerId}");
        }

        /// <summary>
        /// 广播玩家准备状态更新
        /// </summary>
        public void BroadcastPlayerReadyUpdate(ushort playerId, bool isReady)
        {
            if (!IsHost || server == null)
                return;

            Message message = Message.Create(MessageSendMode.Reliable, (ushort)NetworkMessageType.PlayerReadyUpdate);
            message.SerializeReadyChange(playerId, isReady);
            server.SendToAll(message);

            LogDebug($"广播准备状态: 玩家{playerId} -> {isReady}");
        }

        /// <summary>
        /// 广播游戏开始命令
        /// </summary>
        public void BroadcastGameStart()
        {
            if (!IsHost || server == null)
                return;

            Message message = Message.Create(MessageSendMode.Reliable, (ushort)NetworkMessageType.GameStartRequest);
            server.SendToAll(message);

            LogDebug("广播游戏开始命令");
        }

        /// <summary>
        /// 请求房间信息
        /// </summary>
        public void RequestRoomInfo()
        {
            if (IsHost || client == null)
                return;

            Message message = Message.Create(MessageSendMode.Reliable, (ushort)NetworkMessageType.RoomInfoRequest);
            client.Send(message);

            LogDebug("请求房间信息");
        }

        /// <summary>
        /// 请求改变准备状态
        /// </summary>
        public void RequestReadyStateChange(bool isReady)
        {
            if (IsHost || client == null)
                return;

            Message message = Message.Create(MessageSendMode.Reliable, (ushort)NetworkMessageType.PlayerReadyRequest);
            message.SerializeReadyChange(ClientId, isReady);
            client.Send(message);

            LogDebug($"请求改变准备状态: {isReady}");
        }

        #endregion

        #region 服务器事件处理（Host模式）

        private void OnServerClientConnected(object sender, ServerConnectedEventArgs e)
        {
            LogDebug($"客户端连接到服务器: ID={e.Client.Id}");

            // **关键修复：区分Host自己的客户端和其他玩家**
            if (IsHost && !isHostClientReady)
            {
                // 这是Host自己的客户端连接
                hostPlayerId = e.Client.Id;
                LogDebug($"Host玩家ID确定为: {hostPlayerId}");
                // 不在这里设置isHostClientReady，等待OnSelfClientConnected
                return;
            }

            // 其他玩家加入
            LogDebug($"新玩家加入房间: ID={e.Client.Id}");
            OnPlayerJoined?.Invoke(e.Client.Id);
        }

        private void OnServerClientDisconnected(object sender, ServerDisconnectedEventArgs e)
        {
            LogDebug($"客户端断开连接: ID={e.Client.Id}");

            // 检查是否是Host玩家断开
            if (IsHost && e.Client.Id == hostPlayerId)
            {
                LogDebug("Host玩家客户端断开连接");
                isHostClientReady = false;
                return;
            }

            // 其他玩家离开
            LogDebug($"玩家离开房间: ID={e.Client.Id}");
            OnPlayerLeft?.Invoke(e.Client.Id);
        }

        #endregion

        #region 客户端事件处理（Host模式下的自连接）

        /// <summary>
        /// 修改Host启动事件处理 - 重置消息状态
        /// </summary>
        private void OnSelfClientConnected(object sender, EventArgs e)
        {
            LogDebug($"Host客户端连接成功! 玩家ID: {ClientId}");

            // 原有逻辑...
            if (hostPlayerId == 0 || hostPlayerId != ClientId)
            {
                hostPlayerId = ClientId;
                LogDebug($"Host玩家ID更新为: {hostPlayerId}");
            }

            isHostClientReady = true;

            if (!isHostInitialized)
            {
                isHostInitialized = true;
                LogDebug("Host完全初始化完成");
            }

            // 新增：重置消息状态
            gameStartReceived = false;
            lastTurnPlayerId = 0;
            gameProgressSequence = 0;
            pendingQuestion = null;
            LogDebug("Host消息状态已重置");

            OnHostPlayerReady?.Invoke();
            OnConnected?.Invoke();
        }

        private void OnSelfClientDisconnected(object sender, EventArgs e)
        {
            LogDebug("Host客户端断开连接");
            isHostClientReady = false;
            OnDisconnected?.Invoke(); // 触发兼容事件
        }

        private void OnSelfClientConnectionFailed(object sender, EventArgs e)
        {
            Debug.LogError("Host客户端连接失败");
            StopHost();
        }

        #endregion

        #region 客户端事件处理（Client模式）

        private void OnClientConnectedToHost(object sender, EventArgs e)
        {
            LogDebug($"成功连接到主机! ID: {ClientId}");

            // 新增：重置消息状态
            gameStartReceived = false;
            lastTurnPlayerId = 0;
            gameProgressSequence = 0;
            pendingQuestion = null;
            LogDebug("Client消息状态已重置");

            OnConnected?.Invoke();
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

        /// <summary>
        /// 处理游戏进度消息 - 修复版本：添加序列验证
        /// </summary>
        [MessageHandler((ushort)NetworkMessageType.GameProgress)]
        private static void HandleGameProgress(Message message)
        {
            int questionNumber = message.GetInt();
            int alivePlayerCount = message.GetInt();
            ushort turnPlayerId = message.GetUShort();

            // 可选：读取额外的进度信息
            int questionTypeInt = -1;
            float timeLimit = 0f;
            try
            {
                questionTypeInt = message.GetInt();
                timeLimit = message.GetFloat();
            }
            catch
            {
                // 兼容旧版本消息格式
            }

            Debug.Log($"[NetworkManager] 收到游戏进度: 第{questionNumber}题, 存活{alivePlayerCount}人, 回合玩家{turnPlayerId}");

            if (Instance != null)
            {
                // 验证进度序列（防止乱序）
                if (questionNumber < Instance.gameProgressSequence)
                {
                    Debug.LogWarning($"[NetworkManager] 收到过期的游戏进度消息: {questionNumber} < {Instance.gameProgressSequence}");
                    return;
                }

                Instance.gameProgressSequence = questionNumber;
                Debug.Log($"[NetworkManager] 游戏进度序列已更新: {questionNumber}");
            }

            // 转发给NetworkUI
            var networkUI = FindObjectOfType<NetworkUI>();
            if (networkUI != null)
            {
                networkUI.OnGameProgressReceived(questionNumber, alivePlayerCount, turnPlayerId);
            }
        }

        /// <summary>
        /// 处理回合变更消息 - 修复版本：添加状态验证和缓冲处理
        /// </summary>
        [MessageHandler((ushort)NetworkMessageType.PlayerTurnChanged)]
        private static void HandlePlayerTurnChanged(Message message)
        {
            ushort newTurnPlayerId = message.GetUShort();

            Debug.Log($"[NetworkManager] 收到回合变更: 玩家{newTurnPlayerId}");

            if (Instance != null)
            {
                // 验证回合变更的合理性
                if (Instance.lastTurnPlayerId == newTurnPlayerId)
                {
                    Debug.LogWarning($"[NetworkManager] 重复的回合变更消息: {newTurnPlayerId}");
                    return;
                }

                Instance.lastTurnPlayerId = newTurnPlayerId;
                Debug.Log($"[NetworkManager] 回合状态已更新: {newTurnPlayerId}");

                // 检查是否有待处理的题目
                Instance.ProcessPendingQuestion();
            }

            // 先更新UI中的回合状态
            var networkUI = FindObjectOfType<NetworkUI>();
            if (networkUI != null)
            {
                networkUI.OnTurnChangedReceived(newTurnPlayerId);
            }

            // 再通知NQMC（确保UI状态先更新）
            if (NetworkQuestionManagerController.Instance != null)
            {
                NetworkQuestionManagerController.Instance.GetType()
                    .GetMethod("OnNetworkPlayerTurnChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(NetworkQuestionManagerController.Instance, new object[] { newTurnPlayerId });
            }
        }

        /// <summary>
        /// 处理血量更新消息（如果还没有的话）
        /// </summary>
        [MessageHandler((ushort)NetworkMessageType.HealthUpdate)]
        private static void HandleHealthUpdate(Message message)
        {
            ushort playerId = message.GetUShort();
            int newHealth = message.GetInt();
            int maxHealth = message.GetInt();

            Debug.Log($"[NetworkManager] 收到血量更新: 玩家{playerId} {newHealth}/{maxHealth}");

            // 转发给NetworkUI
            var networkUI = FindObjectOfType<NetworkUI>();
            if (networkUI != null)
            {
                networkUI.OnHealthUpdateReceived(playerId, newHealth, maxHealth);
            }
        }

        /// <summary>
        /// 处理玩家答题结果消息（修复版本）
        /// </summary>
        [MessageHandler((ushort)NetworkMessageType.PlayerAnswerResult)]
        private static void HandlePlayerAnswerResult(Message message)
        {
            ushort playerId = message.GetUShort();
            bool isCorrect = message.GetBool();
            string answer = message.GetString();

            Debug.Log($"[NetworkManager] 收到答题结果: 玩家{playerId} {(isCorrect ? "正确" : "错误")} - {answer}");

            // 转发给NetworkUI（如果NetworkUI需要显示答题结果）
            var networkUI = FindObjectOfType<NetworkUI>();
            if (networkUI != null)
            {
                // 需要在NetworkUI中添加对应的处理方法
                networkUI.OnPlayerAnswerResultReceived(playerId, isCorrect, answer);
            }
        }

        [MessageHandler((ushort)NetworkMessageType.SendQuestion)]
        private static void HandleQuestionReceived(Message message)
        {
            byte[] questionData = message.GetBytes();
            NetworkQuestionData question = NetworkQuestionData.Deserialize(questionData);

            if (question != null)
            {
                Debug.Log($"[NetworkManager] 收到题目: {question.questionType} - {question.questionText}");

                if (Instance != null)
                {
                    // 验证接收时机
                    if (!Instance.gameStartReceived)
                    {
                        Debug.LogWarning("[NetworkManager] 收到题目但游戏尚未开始，忽略");
                        return;
                    }

                    // 检查是否应该缓冲题目（等待回合状态更新）
                    if (Instance.ShouldBufferQuestion())
                    {
                        Debug.Log("[NetworkManager] 缓冲题目，等待回合状态更新");
                        Instance.pendingQuestion = new PendingQuestionMessage
                        {
                            question = question,
                            receivedTime = Time.time
                        };
                        return;
                    }

                    // 立即处理题目
                    Instance.ProcessQuestionImmediate(question);
                }
                else
                {
                    // 备用处理（无Instance时）
                    ProcessQuestionFallback(question);
                }
            }
            else
            {
                Debug.LogError("[NetworkManager] 题目数据解析失败");
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

        #endregion

        #region 房间消息处理器

        [MessageHandler((ushort)NetworkMessageType.RoomInfoRequest)]
        private static void HandleRoomInfoRequest(ushort fromClientId, Message message)
        {
            Debug.Log($"[NetworkManager] 收到房间信息请求来自客户端 {fromClientId}");

            // 获取当前房间数据
            if (RoomManager.Instance?.CurrentRoom != null)
            {
                Instance.SendRoomDataToClient(fromClientId, RoomManager.Instance.CurrentRoom);
            }
        }

        [MessageHandler((ushort)NetworkMessageType.PlayerReadyRequest)]
        private static void HandlePlayerReadyRequest(ushort fromClientId, Message message)
        {
            var (playerId, isReady) = message.DeserializeReadyChange();

            Debug.Log($"[NetworkManager] 收到准备状态请求: 玩家{playerId} -> {isReady}");

            // 更新房间中玩家的准备状态
            if (RoomManager.Instance?.CurrentRoom != null)
            {
                bool success = RoomManager.Instance.CurrentRoom.SetPlayerReady(playerId, isReady);
                if (success)
                {
                    // 广播准备状态变化给所有客户端
                    Instance.BroadcastPlayerReadyUpdate(playerId, isReady);
                }
            }
        }

        [MessageHandler((ushort)NetworkMessageType.RoomDataSync)]
        private static void HandleRoomDataSync(Message message)
        {
            RoomData roomData = message.DeserializeRoomData();
            Debug.Log($"[NetworkManager] 收到房间数据同步: {roomData.roomName}");

            // 通知RoomManager更新房间数据
            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.UpdateRoomFromNetwork(roomData);
            }
        }

        [MessageHandler((ushort)NetworkMessageType.PlayerJoinRoom)]
        private static void HandlePlayerJoinRoom(Message message)
        {
            RoomPlayer player = message.DeserializePlayer();
            Debug.Log($"[NetworkManager] 收到玩家加入通知: {player.playerName}");

            // 通知RoomManager
            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.OnNetworkPlayerJoined(player);
            }
        }

        [MessageHandler((ushort)NetworkMessageType.PlayerLeaveRoom)]
        private static void HandlePlayerLeaveRoom(Message message)
        {
            ushort playerId = message.GetUShort();
            Debug.Log($"[NetworkManager] 收到玩家离开通知: {playerId}");

            // 通知RoomManager
            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.OnNetworkPlayerLeftMessage(playerId);
            }
        }

        [MessageHandler((ushort)NetworkMessageType.PlayerReadyUpdate)]
        private static void HandlePlayerReadyUpdate(Message message)
        {
            var (playerId, isReady) = message.DeserializeReadyChange();
            Debug.Log($"[NetworkManager] 收到准备状态更新: 玩家{playerId} -> {isReady}");

            // 通知RoomManager
            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.OnNetworkPlayerReadyChanged(playerId, isReady);
            }
        }
        [MessageHandler((ushort)NetworkMessageType.GameStartRequest)]
        private static void HandleGameStartRequest(Message message)
        {
            Debug.Log("[NetworkManager] 收到游戏开始请求");

            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.OnNetworkGameStart();
                Debug.Log("[NetworkManager] 已通知RoomManager处理游戏开始");
            }
            else
            {
                Debug.LogError("[NetworkManager] RoomManager实例不存在，无法处理游戏开始请求");
            }
        }
        /// <summary>
        /// 处理游戏开始消息 - 修复版本：重置状态
        /// </summary>
        [MessageHandler((ushort)NetworkMessageType.GameStart)]
        private static void HandleGameStart(Message message)
        {
            int totalPlayerCount = message.GetInt();
            int alivePlayerCount = message.GetInt();
            ushort firstTurnPlayerId = message.GetUShort();

            Debug.Log($"[NetworkManager] 收到游戏开始: 总玩家{totalPlayerCount}, 存活{alivePlayerCount}, 首回合玩家{firstTurnPlayerId}");

            // 重置状态
            if (Instance != null)
            {
                Instance.gameStartReceived = true;
                Instance.lastTurnPlayerId = 0;
                Instance.gameProgressSequence = 0;
                Instance.pendingQuestion = null;

                Debug.Log("[NetworkManager] 游戏状态已重置");
            }

            // 转发给NetworkUI
            var networkUI = FindObjectOfType<NetworkUI>();
            if (networkUI != null)
            {
                networkUI.OnGameStartReceived(totalPlayerCount, alivePlayerCount, firstTurnPlayerId);
            }
        }
        /// <summary>
        /// 处理玩家状态同步消息
        /// </summary>
        [MessageHandler((ushort)NetworkMessageType.PlayerStateSync)]
        private static void HandlePlayerStateSync(Message message)
        {
            ushort playerId = message.GetUShort();
            string playerName = message.GetString();
            bool isHost = message.GetBool();
            int currentHealth = message.GetInt();
            int maxHealth = message.GetInt();
            bool isAlive = message.GetBool();

            Debug.Log($"[NetworkManager] 收到玩家状态同步: {playerName} (ID:{playerId}) HP:{currentHealth}/{maxHealth}");

            var networkUI = FindObjectOfType<NetworkUI>();
            if (networkUI != null)
            {
                networkUI.OnPlayerStateSyncReceived(playerId, playerName, isHost, currentHealth, maxHealth, isAlive);
            }
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
                return $"房间: {RoomName} | 玩家: {ConnectedPlayerCount}/{MaxPlayers} | 端口: {Port} | Host玩家ID: {hostPlayerId}";
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

            Message message = Message.Create(MessageSendMode.Reliable, (ushort)NetworkMessageType.RequestQuestion);
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

            Message message = Message.Create(MessageSendMode.Reliable, (ushort)NetworkMessageType.SubmitAnswer);
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
                   $"HostPlayerId: {hostPlayerId}, HostClientReady: {isHostClientReady}, " +
                   $"HostInitialized: {isHostInitialized}";
        }

        #endregion
        #region 新增辅助方法

        /// <summary>
        /// 判断是否应该缓冲题目
        /// </summary>
        private bool ShouldBufferQuestion()
        {
            // 如果是Host，不需要缓冲
            if (IsHost)
            {
                return false;
            }

            // 如果还没有收到任何回合变更，需要缓冲
            if (lastTurnPlayerId == 0)
            {
                Debug.Log("[NetworkManager] 尚未收到回合变更，缓冲题目");
                return true;
            }

            // 检查当前是否为Client模式且回合状态明确
            return false;
        }

        /// <summary>
        /// 处理待处理的题目
        /// </summary>
        private void ProcessPendingQuestion()
        {
            if (pendingQuestion.HasValue)
            {
                var pending = pendingQuestion.Value;

                // 检查是否超时
                if (Time.time - pending.receivedTime > QUESTION_BUFFER_TIMEOUT)
                {
                    Debug.LogWarning("[NetworkManager] 待处理题目超时，丢弃");
                    pendingQuestion = null;
                    return;
                }

                Debug.Log("[NetworkManager] 处理待处理的题目");
                ProcessQuestionImmediate(pending.question);
                pendingQuestion = null;
            }
        }

        /// <summary>
        /// 立即处理题目
        /// </summary>
        private void ProcessQuestionImmediate(NetworkQuestionData question)
        {
            Debug.Log($"[NetworkManager] 立即处理题目: {question.questionType}");

            // 触发题目接收事件
            OnQuestionReceived?.Invoke(question);

            // 直接通知NQMC
            if (NetworkQuestionManagerController.Instance != null)
            {
                var onQuestionMethod = NetworkQuestionManagerController.Instance.GetType()
                    .GetMethod("OnNetworkQuestionReceived", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (onQuestionMethod != null)
                {
                    onQuestionMethod.Invoke(NetworkQuestionManagerController.Instance, new object[] { question });
                }
                else
                {
                    Debug.LogWarning("[NetworkManager] 无法找到NQMC的题目接收方法");
                }
            }
        }

        /// <summary>
        /// 备用题目处理（无Instance时）
        /// </summary>
        private static void ProcessQuestionFallback(NetworkQuestionData question)
        {
            Debug.Log($"[NetworkManager] 备用处理题目: {question.questionType}");
            OnQuestionReceived?.Invoke(question);
        }

        /// <summary>
        /// 定期检查待处理消息（在Update中调用）
        /// </summary>
        private void CheckPendingMessages()
        {
            if (pendingQuestion.HasValue)
            {
                var pending = pendingQuestion.Value;

                // 超时检查
                if (Time.time - pending.receivedTime > QUESTION_BUFFER_TIMEOUT)
                {
                    Debug.LogWarning("[NetworkManager] 待处理题目超时，强制处理");
                    ProcessQuestionImmediate(pending.question);
                    pendingQuestion = null;
                }
            }
        }

        #endregion
        #region 客户端血量管理

        /// <summary>
        /// 处理本地玩家血量更新
        /// </summary>
        private static void ProcessLocalPlayerHealthUpdate(ushort playerId, int newHealth, int maxHealth)
        {
            // 检查是否是本地玩家
            if (IsLocalPlayer(playerId))
            {
                var healthManager = FindObjectOfType<Managers.PlayerHealthManager>();
                if (healthManager != null)
                {
                    healthManager.OnNetworkHealthUpdate(newHealth, maxHealth);
                    Debug.Log($"[NetworkManager] 更新本地玩家血量: {newHealth}/{maxHealth}");
                }
                else
                {
                    Debug.LogWarning("[NetworkManager] 未找到PlayerHealthManager组件");
                }
            }
        }

        /// <summary>
        /// 检查指定玩家ID是否为本地玩家
        /// </summary>
        private static bool IsLocalPlayer(ushort playerId)
        {
            if (Instance == null) return false;

            // Host模式：检查是否是Host玩家
            if (Instance.IsHost)
            {
                return Instance.IsHostPlayer(playerId);
            }
            // Client模式：检查是否是自己的ClientId
            else if (Instance.IsClient)
            {
                return playerId == Instance.ClientId;
            }

            return false;
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