using UnityEngine;
using System;
using UI;

namespace Core.Network
{
    /// <summary>
    /// 房间管理器 - 处理房间逻辑和状态管理
    /// 修复版本：使用统一的Host ID管理，延迟房间创建直到Host客户端准备就绪
    /// </summary>
    public class RoomManager : MonoBehaviour
    {
        [Header("房间配置")]
        [SerializeField] private int maxPlayersPerRoom = 4;
        [SerializeField] private bool enableDebugLogs = false;

        [Header("游戏开始控制")]
        [SerializeField] private bool hasGameStarted = false; // 防止重复开始
        [SerializeField] private float gameStartDelay = 1f;

        public static RoomManager Instance { get; private set; }

        // 房间数据
        private RoomData currentRoom;
        private bool isHost;
        private bool isInitialized = false;

        // **新增：房间创建等待状态**
        private bool isWaitingForHostReady = false;
        private string pendingRoomName = "";
        private string pendingPlayerName = "";

        // 事件
        public static event Action<RoomData> OnRoomCreated;
        public static event Action<RoomData> OnRoomJoined;
        public static event Action<RoomPlayer> OnPlayerJoinedRoom;
        public static event Action<ushort> OnPlayerLeftRoom;
        public static event Action<ushort, bool> OnPlayerReadyChanged;
        public static event Action OnGameStarting; // 唯一的游戏开始事件
        public static event Action OnRoomLeft;

        // 属性
        public RoomData CurrentRoom => currentRoom;
        public bool IsHost => isHost;
        public bool IsInRoom => currentRoom != null;
        public bool IsInitialized => isInitialized;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LogDebug("RoomManager 初始化");
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            // 订阅网络事件
            SubscribeToNetworkEvents();
            isInitialized = true;
        }

        private void SubscribeToNetworkEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnHostStarted += OnNetworkHostStarted;
                NetworkManager.OnPlayerJoined += OnNetworkPlayerJoined;
                NetworkManager.OnPlayerLeft += OnNetworkPlayerLeft;
                NetworkManager.OnDisconnected += OnNetworkDisconnected;

                // **关键新增：监听Host玩家准备就绪事件**
                NetworkManager.OnHostPlayerReady += OnHostPlayerReady;
            }
        }

        private void UnsubscribeFromNetworkEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnHostStarted -= OnNetworkHostStarted;
                NetworkManager.OnPlayerJoined -= OnNetworkPlayerJoined;
                NetworkManager.OnPlayerLeft -= OnNetworkPlayerLeft;
                NetworkManager.OnDisconnected -= OnNetworkDisconnected;
                NetworkManager.OnHostPlayerReady -= OnHostPlayerReady;
            }
        }

        #region 房间操作

        /// <summary>
        /// 创建房间（Host调用）- 修复版本：延迟创建直到Host客户端准备就绪
        /// </summary>
        public bool CreateRoom(string roomName, string playerName)
        {
            if (IsInRoom)
            {
                LogDebug("已在房间中，无法创建新房间");
                return false;
            }

            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsHost)
            {
                LogDebug("不是Host模式，无法创建房间");
                return false;
            }

            LogDebug($"请求创建房间: {roomName}, Host玩家: {playerName}");

            // **关键修复：检查Host客户端是否准备就绪**
            if (!NetworkManager.Instance.IsHostClientReady)
            {
                LogDebug("Host客户端尚未准备就绪，等待连接完成后创建房间");

                // 保存待创建的房间信息
                isWaitingForHostReady = true;
                pendingRoomName = roomName;
                pendingPlayerName = playerName;

                return true; // 返回true表示请求已接受，但实际创建会延迟
            }

            // Host客户端已准备就绪，立即创建房间
            return CreateRoomImmediate(roomName, playerName);
        }

        /// <summary>
        /// 立即创建房间（私有方法）
        /// </summary>
        private bool CreateRoomImmediate(string roomName, string playerName)
        {
            // **使用统一的Host玩家ID接口**
            ushort hostPlayerId = NetworkManager.Instance.GetHostPlayerId();

            if (hostPlayerId == 0)
            {
                Debug.LogError("[RoomManager] Host玩家ID无效，无法创建房间");
                return false;
            }

            LogDebug($"开始创建房间 - 房间名: {roomName}, Host玩家: {playerName}, Host ID: {hostPlayerId}");

            // 生成房间代码
            string roomCode = GenerateRoomCode();

            // **使用正确的Host玩家ID创建房间**
            currentRoom = new RoomData(roomName, roomCode, hostPlayerId, maxPlayersPerRoom);
            isHost = true;

            // **确保Host被添加到玩家列表**
            bool hostAdded = currentRoom.AddPlayer(hostPlayerId, playerName);

            if (!hostAdded)
            {
                Debug.LogError($"[RoomManager] 无法将Host添加到房间：ID={hostPlayerId}, Name={playerName}");
                currentRoom = null;
                isHost = false;
                return false;
            }

            LogDebug($"房间创建成功: {roomName} (代码: {roomCode})");
            LogDebug($"Host已添加到玩家列表: ID={hostPlayerId}, Name={playerName}");
            LogDebug($"当前房间玩家数量: {currentRoom.players.Count}");

            // 重置游戏开始状态
            hasGameStarted = false;

            // 清理等待状态
            isWaitingForHostReady = false;
            pendingRoomName = "";
            pendingPlayerName = "";

            // 触发事件
            OnRoomCreated?.Invoke(currentRoom);

            // 验证房间数据
            ValidateRoomDataAfterCreation();

            return true;
        }

        /// <summary>
        /// Host玩家准备就绪事件处理
        /// </summary>
        private void OnHostPlayerReady()
        {
            LogDebug("Host玩家客户端准备就绪");

            // 如果正在等待Host准备就绪来创建房间
            if (isWaitingForHostReady && !string.IsNullOrEmpty(pendingRoomName))
            {
                LogDebug($"Host准备就绪，现在创建等待中的房间: {pendingRoomName}");
                CreateRoomImmediate(pendingRoomName, pendingPlayerName);
            }
        }

        /// <summary>
        /// 验证房间创建后的数据（调试用）
        /// </summary>
        private void ValidateRoomDataAfterCreation()
        {
            if (currentRoom == null)
            {
                Debug.LogError("[RoomManager] 房间数据为空");
                return;
            }

            LogDebug($"=== 房间创建后验证 ===");
            LogDebug($"房间名: {currentRoom.roomName}");
            LogDebug($"房间代码: {currentRoom.roomCode}");
            LogDebug($"Host ID: {currentRoom.hostId}");
            LogDebug($"玩家总数: {currentRoom.players.Count}/{currentRoom.maxPlayers}");
            LogDebug($"非Host玩家数: {currentRoom.GetNonHostPlayerCount()}");

            // 检查Host是否在玩家列表中
            if (currentRoom.players.ContainsKey(currentRoom.hostId))
            {
                var hostPlayer = currentRoom.players[currentRoom.hostId];
                LogDebug($"Host玩家信息: {hostPlayer.playerName} (isHost: {hostPlayer.isHost})");
            }
            else
            {
                Debug.LogError("[RoomManager] Host不在玩家列表中！");
            }

            // **验证与NetworkManager的一致性**
            if (NetworkManager.Instance != null)
            {
                ushort networkHostId = NetworkManager.Instance.GetHostPlayerId();
                if (currentRoom.hostId != networkHostId)
                {
                    Debug.LogError($"[RoomManager] Host ID不一致！房间中: {currentRoom.hostId}, NetworkManager中: {networkHostId}");
                }
                else
                {
                    LogDebug($"Host ID一致性验证通过: {currentRoom.hostId}");
                }
            }
        }

        /// <summary>
        /// 加入房间（Client调用）
        /// </summary>
        public bool JoinRoom(string playerName)
        {
            if (IsInRoom)
            {
                LogDebug("已在房间中");
                return false;
            }

            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected)
            {
                LogDebug("未连接到网络，无法加入房间");
                return false;
            }

            isHost = false;
            hasGameStarted = false;  // 重置游戏开始状态

            LogDebug($"请求加入房间: 玩家 {playerName}");

            // 客户端连接后自动请求房间信息
            NetworkManager.Instance.RequestRoomInfo();

            return true;
        }

        /// <summary>
        /// 离开房间
        /// </summary>
        public void LeaveRoom()
        {
            if (!IsInRoom)
                return;

            LogDebug("离开房间");

            // 重置状态
            currentRoom = null;
            isHost = false;
            hasGameStarted = false;
            isWaitingForHostReady = false;
            pendingRoomName = "";
            pendingPlayerName = "";

            // 触发事件
            OnRoomLeft?.Invoke();
        }

        /// <summary>
        /// 设置玩家准备状态
        /// </summary>
        public bool SetPlayerReady(bool ready)
        {
            if (!IsInRoom || NetworkManager.Instance == null)
                return false;

            ushort playerId = NetworkManager.Instance.ClientId;

            // 房主不需要准备状态
            if (isHost)
            {
                LogDebug("房主不需要设置准备状态");
                return false;
            }

            // 发送网络请求而不是直接修改本地状态
            NetworkManager.Instance.RequestReadyStateChange(ready);
            return true;
        }

        /// <summary>
        /// 启动游戏 - 统一入口点（修复版本）
        /// **这是游戏开始的唯一入口，所有其他调用都应该通过这里**
        /// </summary>
        public void StartGame()
        {
            // 防止重复启动
            if (hasGameStarted)
            {
                LogDebug("游戏已经开始，忽略重复调用");
                return;
            }

            if (!IsHost)
            {
                Debug.LogError("[RoomManager] 只有房主可以启动游戏");
                return;
            }

            if (!CanStartGame())
            {
                Debug.LogError("[RoomManager] 游戏启动条件不满足");
                LogDebug($"详细检查 - 房间状态: {GetRoomStatusInfo()}");
                return;
            }

            LogDebug("房主发起游戏开始");

            // 设置标志防止重复启动
            hasGameStarted = true;

            // 更新房间状态
            if (currentRoom != null)
            {
                currentRoom.state = RoomState.Starting;
                LogDebug($"房间状态已更新为: {currentRoom.state}");
            }

            // 广播游戏开始消息给所有客户端
            if (NetworkManager.Instance != null)
            {
                LogDebug("广播游戏开始消息");
                NetworkManager.Instance.BroadcastGameStart();
            }

            // 延迟触发游戏开始事件（给网络消息传播时间）
            Invoke(nameof(TriggerGameStartingEvent), gameStartDelay);
        }

        /// <summary>
        /// 触发游戏开始事件 - 私有方法
        /// </summary>
        private void TriggerGameStartingEvent()
        {
            LogDebug("触发 OnGameStarting 事件 - 这将被RoomSceneController捕获并执行场景切换");

            // 触发游戏开始事件 - 这会被RoomSceneController捕获并执行场景切换
            OnGameStarting?.Invoke();
        }

        /// <summary>
        /// 网络游戏开始处理 - 由NetworkManager调用
        /// **修改：统一路由到StartGame方法，但跳过网络广播**
        /// </summary>
        public void OnNetworkGameStart()
        {
            LogDebug("收到网络游戏开始消息");

            // 防止重复处理
            if (hasGameStarted)
            {
                LogDebug("游戏已经开始，忽略网络消息");
                return;
            }

            // 客户端收到游戏开始消息
            if (!IsHost)
            {
                LogDebug("客户端收到游戏开始通知");
                hasGameStarted = true;

                // 更新房间状态
                if (currentRoom != null)
                {
                    currentRoom.state = RoomState.Starting;
                }

                // 直接触发游戏开始事件
                TriggerGameStartingEvent();
            }
            else
            {
                // Host端收到自己的广播消息，忽略
                LogDebug("Host收到自己的广播消息，忽略处理");
            }
        }

        #endregion

        #region 网络事件处理

        private void OnNetworkHostStarted()
        {
            LogDebug("网络Host已启动（服务器层）");
        }

        private void OnNetworkPlayerJoined(ushort playerId)
        {
            if (!IsInRoom || !isHost)
                return;

            // **使用统一接口检查是否为Host玩家**
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsHostPlayer(playerId))
            {
                LogDebug($"忽略Host自己的玩家连接: {playerId}");
                return;
            }

            // Host模式下，为新加入的玩家创建房间数据
            string playerName = $"玩家{playerId}";
            bool success = currentRoom.AddPlayer(playerId, playerName);

            if (success)
            {
                LogDebug($"玩家 {playerId} 加入房间");
                LogDebug($"当前房间玩家数: {currentRoom.players.Count}");

                // 广播给所有客户端
                NetworkManager.Instance.BroadcastPlayerJoinRoom(currentRoom.players[playerId]);

                // 发送完整房间数据给新玩家
                NetworkManager.Instance.SendRoomDataToClient(playerId, currentRoom);

                // 触发本地事件
                OnPlayerJoinedRoom?.Invoke(currentRoom.players[playerId]);
            }
            else
            {
                LogDebug($"玩家 {playerId} 加入失败（房间已满或重复）");
            }
        }

        private void OnNetworkPlayerLeft(ushort playerId)
        {
            if (!IsInRoom)
                return;

            if (isHost)
            {
                // **使用统一接口检查是否为Host玩家**
                if (NetworkManager.Instance != null && NetworkManager.Instance.IsHostPlayer(playerId))
                {
                    LogDebug($"Host玩家断开连接: {playerId}");
                    // Host玩家断开不从房间移除，只标记状态
                    return;
                }

                // Host处理其他玩家离开
                bool success = currentRoom.RemovePlayer(playerId);
                if (success)
                {
                    LogDebug($"玩家 {playerId} 离开房间");
                    LogDebug($"当前房间玩家数: {currentRoom.players.Count}");

                    // 广播给其他客户端
                    NetworkManager.Instance.BroadcastPlayerLeaveRoom(playerId);

                    // 触发本地事件
                    OnPlayerLeftRoom?.Invoke(playerId);
                }
            }
            else
            {
                // Client处理玩家离开通知
                LogDebug($"网络玩家离开: {playerId}");

                if (currentRoom != null && currentRoom.players.ContainsKey(playerId))
                {
                    currentRoom.players.Remove(playerId);
                    OnPlayerLeftRoom?.Invoke(playerId);
                }
            }
        }

        private void OnNetworkDisconnected()
        {
            LogDebug("网络断开，离开房间");
            LeaveRoom();
        }

        #endregion

        #region 网络同步方法（供NetworkManager调用）

        /// <summary>
        /// 从网络更新房间数据（客户端调用）
        /// </summary>
        public void UpdateRoomFromNetwork(RoomData networkRoomData)
        {
            if (isHost) return; // Host不接受网络房间数据

            LogDebug($"从网络更新房间数据: {networkRoomData.roomName}");

            bool wasFirstSync = currentRoom == null;
            currentRoom = networkRoomData;

            // 重置游戏开始状态（因为收到了新的房间数据）
            if (currentRoom.state == RoomState.Waiting)
            {
                hasGameStarted = false;
            }

            if (wasFirstSync)
            {
                // 首次同步，触发加入房间事件
                LogDebug($"首次同步房间数据，玩家数: {currentRoom.players.Count}");
                OnRoomJoined?.Invoke(currentRoom);
            }
            else
            {
                // 后续同步，可能需要更新UI
                LogDebug($"更新房间数据，玩家数: {currentRoom.players.Count}");
            }
        }

        /// <summary>
        /// 处理网络玩家加入（客户端调用）
        /// </summary>
        public void OnNetworkPlayerJoined(RoomPlayer player)
        {
            if (isHost || !IsInRoom) return;

            LogDebug($"网络玩家加入: {player.playerName} (ID: {player.playerId})");

            if (!currentRoom.players.ContainsKey(player.playerId))
            {
                currentRoom.players[player.playerId] = player;
                LogDebug($"客户端房间玩家数更新为: {currentRoom.players.Count}");
                OnPlayerJoinedRoom?.Invoke(player);
            }
        }

        /// <summary>
        /// 处理网络玩家离开（供NetworkManager调用，处理网络消息）
        /// </summary>
        public void OnNetworkPlayerLeftMessage(ushort playerId)
        {
            if (isHost || !IsInRoom) return;

            LogDebug($"收到网络玩家离开消息: {playerId}");

            if (currentRoom.players.ContainsKey(playerId))
            {
                currentRoom.players.Remove(playerId);
                LogDebug($"客户端房间玩家数更新为: {currentRoom.players.Count}");
                OnPlayerLeftRoom?.Invoke(playerId);
            }
        }

        /// <summary>
        /// 处理网络玩家准备状态变化
        /// </summary>
        public void OnNetworkPlayerReadyChanged(ushort playerId, bool isReady)
        {
            if (!IsInRoom) return;

            LogDebug($"网络玩家准备状态变化: {playerId} -> {isReady}");

            if (currentRoom.players.ContainsKey(playerId))
            {
                currentRoom.SetPlayerReady(playerId, isReady);
                OnPlayerReadyChanged?.Invoke(playerId, isReady);
            }
        }

        #endregion

        #region 状态查询方法

        /// <summary>
        /// 检查是否可以开始游戏（修复版本）
        /// </summary>
        public bool CanStartGame()
        {
            if (!IsHost)
            {
                LogDebug("只有房主可以开始游戏");
                return false;
            }

            if (currentRoom == null)
            {
                LogDebug("房间数据为空");
                return false;
            }

            if (hasGameStarted)
            {
                LogDebug("游戏已经开始");
                return false;
            }

            if (currentRoom.state != RoomState.Waiting)
            {
                LogDebug($"房间状态不是等待中: {currentRoom.state}");
                return false;
            }

            // **检查Host客户端是否准备就绪**
            if (NetworkManager.Instance != null && !NetworkManager.Instance.IsHostClientReady)
            {
                LogDebug("Host客户端尚未准备就绪");
                return false;
            }

            // 检查是否有非房主玩家
            int nonHostPlayerCount = currentRoom.GetNonHostPlayerCount();
            if (nonHostPlayerCount == 0)
            {
                LogDebug("没有其他玩家，无法开始游戏");
                return false;
            }

            // 检查所有非房主玩家是否都已准备
            if (!currentRoom.AreAllPlayersReady())
            {
                int readyCount = currentRoom.GetReadyPlayerCount();
                LogDebug($"还有玩家未准备: {readyCount}/{nonHostPlayerCount}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 获取当前玩家的准备状态
        /// </summary>
        public bool GetMyReadyState()
        {
            if (!IsInRoom || NetworkManager.Instance == null)
                return false;

            ushort myId = NetworkManager.Instance.ClientId;
            if (currentRoom.players.ContainsKey(myId))
            {
                return currentRoom.players[myId].state == PlayerRoomState.Ready;
            }

            return false;
        }

        /// <summary>
        /// 获取房间状态信息
        /// </summary>
        public string GetRoomStatusInfo()
        {
            if (!IsInRoom)
                return "未在房间中";

            try
            {
                string hostInfo = "";
                if (NetworkManager.Instance != null)
                {
                    hostInfo = $", Host玩家ID: {NetworkManager.Instance.GetHostPlayerId()}, Host客户端就绪: {NetworkManager.Instance.IsHostClientReady}";
                }

                return $"房间: {currentRoom.roomName}, " +
                       $"状态: {currentRoom.state}, " +
                       $"玩家: {currentRoom.players.Count}/{currentRoom.maxPlayers}, " +
                       $"准备: {currentRoom.GetReadyPlayerCount()}/{currentRoom.GetNonHostPlayerCount()}, " +
                       $"是否房主: {isHost}, " +
                       $"游戏已开始: {hasGameStarted}" +
                       hostInfo;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"获取房间状态信息失败: {e.Message}");
                return "房间状态异常";
            }
        }

        /// <summary>
        /// 获取房间内玩家列表
        /// </summary>
        public RoomPlayer[] GetPlayerList()
        {
            if (!IsInRoom)
                return new RoomPlayer[0];

            RoomPlayer[] players = new RoomPlayer[currentRoom.players.Count];
            int index = 0;
            foreach (var player in currentRoom.players.Values)
            {
                players[index++] = player;
            }

            return players;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 生成房间代码
        /// </summary>
        private string GenerateRoomCode()
        {
            return UnityEngine.Random.Range(100000, 999999).ToString();
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[RoomManager] {message}");
            }
        }

        /// <summary>
        /// 重置游戏开始状态（用于调试或错误恢复）
        /// </summary>
        public void ResetGameStartState()
        {
            LogDebug("重置游戏开始状态");
            hasGameStarted = false;

            if (currentRoom != null)
            {
                currentRoom.state = RoomState.Waiting;
            }
        }

        #endregion

        #region 调试和验证方法

        /// <summary>
        /// 验证房间数据完整性
        /// </summary>
        public bool ValidateRoomData()
        {
            if (!IsInRoom)
            {
                LogDebug("未在房间中，无法验证");
                return false;
            }

            try
            {
                // 检查房间基本信息
                if (string.IsNullOrEmpty(currentRoom.roomName) ||
                    string.IsNullOrEmpty(currentRoom.roomCode))
                {
                    Debug.LogWarning("房间基本信息不完整");
                    return false;
                }

                // 检查玩家数据
                if (currentRoom.players.Count == 0)
                {
                    Debug.LogWarning("房间没有玩家");
                    return false;
                }

                // 检查房主是否存在
                if (!currentRoom.players.ContainsKey(currentRoom.hostId))
                {
                    Debug.LogWarning($"房主(ID: {currentRoom.hostId})不在玩家列表中");
                    return false;
                }

                // 验证房主标志
                var hostPlayer = currentRoom.players[currentRoom.hostId];
                if (!hostPlayer.isHost)
                {
                    Debug.LogWarning($"房主玩家的isHost标志为false");
                    return false;
                }

                // **验证与NetworkManager的Host ID一致性**
                if (NetworkManager.Instance != null)
                {
                    ushort networkHostId = NetworkManager.Instance.GetHostPlayerId();
                    if (currentRoom.hostId != networkHostId)
                    {
                        Debug.LogWarning($"房间Host ID与NetworkManager不一致: 房间={currentRoom.hostId}, 网络={networkHostId}");
                        return false;
                    }
                }

                LogDebug("房间数据验证通过");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"房间数据验证异常: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取详细的调试信息
        /// </summary>
        public string GetDetailedDebugInfo()
        {
            var info = "=== 房间管理器详细信息 ===\n";
            info += $"是否为房主: {IsHost}\n";
            info += $"是否在房间中: {IsInRoom}\n";
            info += $"游戏是否已开始: {hasGameStarted}\n";
            info += $"是否已初始化: {isInitialized}\n";
            info += $"等待Host准备就绪: {isWaitingForHostReady}\n";

            if (isWaitingForHostReady)
            {
                info += $"待创建房间名: {pendingRoomName}\n";
                info += $"待创建玩家名: {pendingPlayerName}\n";
            }

            if (currentRoom != null)
            {
                try
                {
                    info += $"房间名: {currentRoom.roomName}\n";
                    info += $"房间代码: {currentRoom.roomCode}\n";
                    info += $"房间状态: {currentRoom.state}\n";
                    info += $"房主ID: {currentRoom.hostId}\n";
                    info += $"玩家数量: {currentRoom.players.Count}/{currentRoom.maxPlayers}\n";
                    info += $"非房主玩家数: {currentRoom.GetNonHostPlayerCount()}\n";
                    info += $"准备玩家数: {currentRoom.GetReadyPlayerCount()}\n";
                    info += $"所有玩家是否准备: {currentRoom.AreAllPlayersReady()}\n";
                    info += $"可以开始游戏: {CanStartGame()}\n";
                    info += $"房间数据验证: {(ValidateRoomData() ? "通过" : "失败")}\n";

                    info += "玩家列表:\n";
                    foreach (var player in currentRoom.players.Values)
                    {
                        info += $"  - {player.playerName} (ID:{player.playerId}, Host:{player.isHost}, State:{player.state})\n";
                    }
                }
                catch (System.Exception e)
                {
                    info += $"获取房间信息时发生异常: {e.Message}\n";
                }
            }
            else
            {
                info += "当前房间: null\n";
            }

            if (NetworkManager.Instance != null)
            {
                info += $"NetworkManager状态: 连接={NetworkManager.Instance.IsConnected}, Host={NetworkManager.Instance.IsHost}\n";
                info += $"NetworkManager ClientID={NetworkManager.Instance.ClientId}, HostPlayerID={NetworkManager.Instance.GetHostPlayerId()}\n";
                info += $"Host客户端就绪: {NetworkManager.Instance.IsHostClientReady}\n";
            }
            else
            {
                info += "NetworkManager: null\n";
            }

            return info;
        }

        /// <summary>
        /// 调试方法：显示房间状态
        /// </summary>
        [ContextMenu("显示房间状态")]
        public void ShowRoomStatus()
        {
            if (Application.isPlaying)
            {
                Debug.Log(GetDetailedDebugInfo());
            }
        }

        /// <summary>
        /// 调试方法：强制开始游戏
        /// </summary>
        [ContextMenu("强制开始游戏")]
        public void ForceStartGame()
        {
            if (Application.isPlaying)
            {
                LogDebug("强制开始游戏（调试用）");
                ResetGameStartState();
                StartGame();
            }
        }

        /// <summary>
        /// 调试方法：验证房间数据
        /// </summary>
        [ContextMenu("验证房间数据")]
        public void DebugValidateRoomData()
        {
            if (Application.isPlaying)
            {
                bool isValid = ValidateRoomData();
                Debug.Log($"房间数据验证结果: {(isValid ? "通过" : "失败")}");
                if (!isValid)
                {
                    Debug.Log(GetDetailedDebugInfo());
                }
            }
        }

        /// <summary>
        /// 调试方法：测试Host ID一致性
        /// </summary>
        [ContextMenu("测试Host ID一致性")]
        public void TestHostIdConsistency()
        {
            if (Application.isPlaying && IsInRoom && IsHost)
            {
                ushort roomHostId = currentRoom.hostId;
                ushort networkHostId = NetworkManager.Instance?.GetHostPlayerId() ?? 0;
                ushort clientId = NetworkManager.Instance?.ClientId ?? 0;

                Debug.Log($"=== Host ID 一致性测试 ===");
                Debug.Log($"房间中Host ID: {roomHostId}");
                Debug.Log($"NetworkManager Host玩家ID: {networkHostId}");
                Debug.Log($"NetworkManager 客户端ID: {clientId}");
                Debug.Log($"一致性检查: {(roomHostId == networkHostId && networkHostId == clientId ? "通过" : "失败")}");
            }
        }

        /// <summary>
        /// 强制刷新房间状态（调试用）
        /// </summary>
        [ContextMenu("刷新房间状态")]
        public void RefreshRoomState()
        {
            if (IsInRoom)
            {
                LogDebug($"当前房间状态: {GetRoomStatusInfo()}");
            }
            else
            {
                LogDebug("未在房间中");
            }
        }

        /// <summary>
        /// 调试方法：强制创建房间（忽略等待状态）
        /// </summary>
        [ContextMenu("强制创建房间")]
        public void ForceCreateRoom()
        {
            if (Application.isPlaying && isWaitingForHostReady)
            {
                LogDebug("强制创建等待中的房间");
                CreateRoomImmediate(pendingRoomName, pendingPlayerName);
            }
        }

        #endregion

        private void OnDestroy()
        {
            // 取消网络事件订阅
            UnsubscribeFromNetworkEvents();

            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}