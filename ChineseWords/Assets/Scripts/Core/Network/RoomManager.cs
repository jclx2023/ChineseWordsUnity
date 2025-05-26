using UnityEngine;
using System;
using UI;

namespace Core.Network
{
    /// <summary>
    /// 房间管理器 - 处理房间逻辑和状态管理
    /// </summary>
    public class RoomManager : MonoBehaviour
    {
        [Header("房间配置")]
        [SerializeField] private int maxPlayersPerRoom = 4;
        [SerializeField] private bool enableDebugLogs = true;

        public static RoomManager Instance { get; private set; }

        // 房间数据
        private RoomData currentRoom;
        private bool isHost;
        private bool isInitialized = false;

        // 事件
        public static event Action<RoomData> OnRoomCreated;
        public static event Action<RoomData> OnRoomJoined;
        public static event Action<RoomPlayer> OnPlayerJoinedRoom;
        public static event Action<ushort> OnPlayerLeftRoom;
        public static event Action<ushort, bool> OnPlayerReadyChanged;
        public static event Action OnGameStarting;
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
            }
        }

        #region 房间操作

        /// <summary>
        /// 创建房间（Host调用）
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

            // 生成房间代码
            string roomCode = GenerateRoomCode();
            ushort hostId = NetworkManager.Instance.ClientId;

            // 创建房间数据
            currentRoom = new RoomData(roomName, roomCode, hostId, maxPlayersPerRoom);
            isHost = true;

            // 添加房主到房间
            currentRoom.AddPlayer(hostId, playerName);

            LogDebug($"房间创建成功: {roomName} (代码: {roomCode})");

            // 触发事件
            OnRoomCreated?.Invoke(currentRoom);

            return true;
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

            currentRoom = null;
            isHost = false;

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
        /// 开始游戏（仅房主可调用）
        /// </summary>
        public bool StartGame()
        {
            if (!IsInRoom || !isHost)
            {
                LogDebug("只有房主可以开始游戏");
                return false;
            }

            if (currentRoom.state != RoomState.Waiting)
            {
                LogDebug($"房间状态不正确: {currentRoom.state}");
                return false;
            }

            if (currentRoom.players.Count < 2)
            {
                LogDebug("至少需要2个玩家才能开始游戏");
                return false;
            }

            if (!currentRoom.AreAllPlayersReady())
            {
                LogDebug("还有玩家未准备");
                return false;
            }

            // 设置房间状态为开始中
            currentRoom.state = RoomState.Starting;

            LogDebug("开始游戏");

            // 通过网络广播游戏开始
            NetworkManager.Instance.BroadcastGameStart();

            // 触发本地事件
            OnGameStarting?.Invoke();

            return true;
        }

        #endregion

        #region 网络事件处理

        private void OnNetworkHostStarted()
        {
            LogDebug("网络Host已启动");
        }

        private void OnNetworkPlayerJoined(ushort playerId)
        {
            if (!IsInRoom || !isHost)
                return;

            // 检查是否是Host自己的客户端连接，如果是则忽略
            if (NetworkManager.Instance != null && playerId == NetworkManager.Instance.ClientId)
            {
                LogDebug($"忽略Host自己的客户端连接: {playerId}");
                return;
            }

            // Host模式下，为新加入的玩家创建房间数据
            string playerName = $"玩家{playerId}";
            bool success = currentRoom.AddPlayer(playerId, playerName);

            if (success)
            {
                LogDebug($"玩家 {playerId} 加入房间");

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
                // Host处理玩家离开
                bool success = currentRoom.RemovePlayer(playerId);
                if (success)
                {
                    LogDebug($"玩家 {playerId} 离开房间");

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

                if (currentRoom.players.ContainsKey(playerId))
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

            if (wasFirstSync)
            {
                // 首次同步，触发加入房间事件
                OnRoomJoined?.Invoke(currentRoom);
            }
            else
            {
                // 后续同步，可能需要更新UI
                // 这里可以添加增量更新逻辑
            }
        }

        /// <summary>
        /// 处理网络玩家加入（客户端调用）
        /// </summary>
        public void OnNetworkPlayerJoined(RoomPlayer player)
        {
            if (isHost || !IsInRoom) return;

            LogDebug($"网络玩家加入: {player.playerName}");

            if (!currentRoom.players.ContainsKey(player.playerId))
            {
                currentRoom.players[player.playerId] = player;
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

        /// <summary>
        /// 处理网络游戏开始命令
        /// </summary>
        public void OnNetworkGameStart()
        {
            if (!IsInRoom) return;

            LogDebug("收到网络游戏开始命令");

            currentRoom.state = RoomState.Starting;
            OnGameStarting?.Invoke();
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
        /// 获取房间状态信息
        /// </summary>
        public string GetRoomStatusInfo()
        {
            if (!IsInRoom)
                return "未在房间中";

            return $"房间: {currentRoom.roomName}, " +
                   $"状态: {currentRoom.state}, " +
                   $"玩家: {currentRoom.players.Count}/{currentRoom.maxPlayers}, " +
                   $"准备: {currentRoom.GetReadyPlayerCount()}/{currentRoom.GetNonHostPlayerCount()}, " +
                   $"是否房主: {isHost}";
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
        /// 检查是否可以开始游戏
        /// </summary>
        public bool CanStartGame()
        {
            if (!IsInRoom || !isHost)
                return false;

            return currentRoom.players.Count >= 2 &&
                   currentRoom.AreAllPlayersReady() &&
                   currentRoom.state == RoomState.Waiting;
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

        #endregion

        private void OnDestroy()
        {
            UnsubscribeFromNetworkEvents();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #region 调试和验证方法

        /// <summary>
        /// 验证房间数据完整性
        /// </summary>
        public bool ValidateRoomData()
        {
            if (!IsInRoom)
                return false;

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
                Debug.LogWarning("房主不在玩家列表中");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 获取详细的调试信息
        /// </summary>
        public string GetDetailedDebugInfo()
        {
            if (!IsInRoom)
                return "未在房间中";

            string info = $"=== 房间详细信息 ===\n";
            info += $"房间名: {currentRoom.roomName}\n";
            info += $"房间代码: {currentRoom.roomCode}\n";
            info += $"房间状态: {currentRoom.state}\n";
            info += $"房主ID: {currentRoom.hostId}\n";
            info += $"最大玩家数: {currentRoom.maxPlayers}\n";
            info += $"当前玩家数: {currentRoom.players.Count}\n";
            info += $"准备玩家数: {currentRoom.GetReadyPlayerCount()}\n";
            info += $"是否房主: {isHost}\n";
            info += $"网络状态: {(NetworkManager.Instance?.IsConnected ?? false)}\n";
            info += $"玩家列表:\n";

            foreach (var player in currentRoom.players.Values)
            {
                info += $"  - {player.playerName} (ID:{player.playerId}) ";
                info += $"[{(player.isHost ? "房主" : "玩家")}] ";
                info += $"[{player.state}]\n";
            }

            return info;
        }

        #endregion
    }
}