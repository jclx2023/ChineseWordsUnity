using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using Core.Network;

namespace UI
{
    /// <summary>
    /// 房间界面控制器 - 负责房间UI的自动刷新和同步
    /// 解决Host和Client之间的界面信息不同步问题
    /// </summary>
    public class RoomUIController : MonoBehaviour
    {
        [Header("房间信息UI")]
        [SerializeField] private TMP_Text roomNameText;
        [SerializeField] private TMP_Text roomCodeText;
        [SerializeField] private TMP_Text roomStatusText;
        [SerializeField] private TMP_Text playerCountText;

        [Header("玩家列表UI")]
        [SerializeField] private Transform playerListParent;
        [SerializeField] private GameObject playerItemPrefab;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button readyButton;
        [SerializeField] private TMP_Text readyButtonText;

        [Header("刷新设置")]
        [SerializeField] private bool enableAutoRefresh = true;
        [SerializeField] private float autoRefreshInterval = 1f;
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool enableManualRefresh = true;
        [SerializeField] private Button manualRefreshButton;

        // 玩家列表UI缓存
        private Dictionary<ushort, GameObject> playerUIItems = new Dictionary<ushort, GameObject>();

        // 状态缓存（用于检测变化）
        private string lastRoomStatus = "";
        private int lastPlayerCount = 0;
        private Dictionary<ushort, PlayerUIState> lastPlayerStates = new Dictionary<ushort, PlayerUIState>();

        // 自动刷新协程
        private Coroutine autoRefreshCoroutine;

        private struct PlayerUIState
        {
            public string playerName;
            public PlayerRoomState state;
            public bool isHost;

            public PlayerUIState(string name, PlayerRoomState roomState, bool host)
            {
                playerName = name;
                state = roomState;
                isHost = host;
            }

            public bool Equals(PlayerUIState other)
            {
                return playerName == other.playerName &&
                       state == other.state &&
                       isHost == other.isHost;
            }
        }

        private void Start()
        {
            InitializeUI();
            SubscribeToEvents();

            if (enableAutoRefresh)
            {
                StartAutoRefresh();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            StopAutoRefresh();
        }

        #region 初始化

        /// <summary>
        /// 初始化UI组件
        /// </summary>
        private void InitializeUI()
        {
            // 绑定手动刷新按钮
            if (manualRefreshButton != null && enableManualRefresh)
            {
                manualRefreshButton.onClick.RemoveAllListeners();
                manualRefreshButton.onClick.AddListener(ForceRefreshUI);
                manualRefreshButton.gameObject.SetActive(true);
            }
            else if (manualRefreshButton != null)
            {
                manualRefreshButton.gameObject.SetActive(false);
            }

            // 绑定准备按钮
            if (readyButton != null)
            {
                readyButton.onClick.RemoveAllListeners();
                readyButton.onClick.AddListener(OnReadyButtonClicked);
            }

            // 绑定开始游戏按钮
            if (startGameButton != null)
            {
                startGameButton.onClick.RemoveAllListeners();
                startGameButton.onClick.AddListener(OnStartGameButtonClicked);
            }

            LogDebug("RoomUIController 初始化完成");
        }

        /// <summary>
        /// 订阅房间管理器事件
        /// </summary>
        private void SubscribeToEvents()
        {
            if (RoomManager.Instance != null)
            {
                RoomManager.OnRoomCreated += OnRoomUpdated;
                RoomManager.OnRoomJoined += OnRoomUpdated;
                RoomManager.OnPlayerJoinedRoom += OnPlayerJoinedRoom;
                RoomManager.OnPlayerLeftRoom += OnPlayerLeftRoom;
                RoomManager.OnPlayerReadyChanged += OnPlayerReadyChanged;
                RoomManager.OnGameStarting += OnGameStarting;
                RoomManager.OnRoomLeft += OnRoomLeft;

                LogDebug("已订阅 RoomManager 事件");
            }
            else
            {
                LogDebug("RoomManager 实例不存在，将在稍后重试订阅");
                StartCoroutine(DelayedSubscribe());
            }

            // 订阅网络事件（作为备用同步机制）
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerJoined += OnNetworkPlayerJoined;
                NetworkManager.OnPlayerLeft += OnNetworkPlayerLeft;
                LogDebug("已订阅 NetworkManager 事件");
            }
        }

        /// <summary>
        /// 延迟订阅（等待RoomManager初始化）
        /// </summary>
        private IEnumerator DelayedSubscribe()
        {
            int retryCount = 0;
            while (RoomManager.Instance == null && retryCount < 10)
            {
                yield return new WaitForSeconds(0.5f);
                retryCount++;
            }

            if (RoomManager.Instance != null)
            {
                SubscribeToEvents();
                // 立即刷新一次UI
                RefreshUI();
            }
            else
            {
                Debug.LogError("[RoomUIController] RoomManager 初始化超时");
            }
        }

        /// <summary>
        /// 取消订阅事件
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (RoomManager.Instance != null)
            {
                RoomManager.OnRoomCreated -= OnRoomUpdated;
                RoomManager.OnRoomJoined -= OnRoomUpdated;
                RoomManager.OnPlayerJoinedRoom -= OnPlayerJoinedRoom;
                RoomManager.OnPlayerLeftRoom -= OnPlayerLeftRoom;
                RoomManager.OnPlayerReadyChanged -= OnPlayerReadyChanged;
                RoomManager.OnGameStarting -= OnGameStarting;
                RoomManager.OnRoomLeft -= OnRoomLeft;
            }

            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerJoined -= OnNetworkPlayerJoined;
                NetworkManager.OnPlayerLeft -= OnNetworkPlayerLeft;
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 房间创建/加入事件处理
        /// </summary>
        private void OnRoomUpdated(RoomData roomData)
        {
            LogDebug($"房间更新事件: {roomData.roomName}");
            RefreshUI();
        }

        /// <summary>
        /// 玩家加入房间事件处理
        /// </summary>
        private void OnPlayerJoinedRoom(RoomPlayer player)
        {
            LogDebug($"玩家加入事件: {player.playerName} (ID: {player.playerId})");
            RefreshPlayerList();
            RefreshRoomInfo();
        }

        /// <summary>
        /// 玩家离开房间事件处理
        /// </summary>
        private void OnPlayerLeftRoom(ushort playerId)
        {
            LogDebug($"玩家离开事件: {playerId}");
            RemovePlayerFromUI(playerId);
            RefreshRoomInfo();
        }

        /// <summary>
        /// 玩家准备状态变化事件处理
        /// </summary>
        private void OnPlayerReadyChanged(ushort playerId, bool isReady)
        {
            LogDebug($"玩家准备状态变化: {playerId} -> {isReady}");
            UpdatePlayerReadyState(playerId, isReady);
            RefreshGameControls();
        }

        /// <summary>
        /// 游戏开始事件处理
        /// </summary>
        private void OnGameStarting()
        {
            LogDebug("游戏开始事件");
            RefreshGameControls();
        }

        /// <summary>
        /// 离开房间事件处理
        /// </summary>
        private void OnRoomLeft()
        {
            LogDebug("离开房间事件");
            ClearUI();
        }

        /// <summary>
        /// 网络玩家加入事件处理（备用同步）
        /// </summary>
        private void OnNetworkPlayerJoined(ushort playerId)
        {
            LogDebug($"网络玩家加入事件: {playerId}");
            // 延迟刷新，确保RoomManager已处理完毕
            StartCoroutine(DelayedRefresh(0.1f));
        }

        /// <summary>
        /// 网络玩家离开事件处理（备用同步）
        /// </summary>
        private void OnNetworkPlayerLeft(ushort playerId)
        {
            LogDebug($"网络玩家离开事件: {playerId}");
            // 延迟刷新，确保RoomManager已处理完毕
            StartCoroutine(DelayedRefresh(0.1f));
        }

        #endregion

        #region UI刷新核心逻辑

        /// <summary>
        /// 完全刷新UI
        /// </summary>
        public void RefreshUI()
        {
            if (!RoomManager.Instance?.IsInRoom == true)
            {
                ClearUI();
                return;
            }

            RefreshRoomInfo();
            RefreshPlayerList();
            RefreshGameControls();

            LogDebug("UI 完全刷新完成");
        }

        /// <summary>
        /// 强制刷新UI（忽略缓存）
        /// </summary>
        public void ForceRefreshUI()
        {
            LogDebug("执行强制刷新");

            // 清除缓存
            lastRoomStatus = "";
            lastPlayerCount = 0;
            lastPlayerStates.Clear();

            // 强制刷新
            RefreshUI();
        }

        /// <summary>
        /// 刷新房间信息
        /// </summary>
        private void RefreshRoomInfo()
        {
            if (!RoomManager.Instance?.IsInRoom == true)
                return;

            var roomData = RoomManager.Instance.CurrentRoom;
            if (roomData == null)
                return;

            // 检查房间状态是否有变化
            string currentStatus = RoomManager.Instance.GetRoomStatusInfo();
            if (currentStatus == lastRoomStatus && roomData.players.Count == lastPlayerCount)
                return;

            // 更新房间名称
            if (roomNameText != null)
                roomNameText.text = $"房间: {roomData.roomName}";

            // 更新房间代码
            if (roomCodeText != null)
                roomCodeText.text = $"房间代码: {roomData.roomCode}";

            // 更新房间状态
            if (roomStatusText != null)
            {
                string statusText = GetRoomStateDisplayText(roomData.state);
                roomStatusText.text = $"状态: {statusText}";
            }

            // 更新玩家数量
            if (playerCountText != null)
            {
                int readyCount = roomData.GetReadyPlayerCount();
                int totalNonHost = roomData.GetNonHostPlayerCount();
                playerCountText.text = $"玩家: {roomData.players.Count}/{roomData.maxPlayers} (准备: {readyCount}/{totalNonHost})";
            }

            // 更新缓存
            lastRoomStatus = currentStatus;
            lastPlayerCount = roomData.players.Count;

            LogDebug($"房间信息已更新: {roomData.roomName}, 玩家数: {roomData.players.Count}");
        }

        /// <summary>
        /// 刷新玩家列表
        /// </summary>
        private void RefreshPlayerList()
        {
            if (!RoomManager.Instance?.IsInRoom == true || playerListParent == null)
                return;

            var roomData = RoomManager.Instance.CurrentRoom;
            if (roomData == null)
                return;

            // 检查玩家列表是否有变化
            bool hasChanges = false;
            foreach (var player in roomData.players.Values)
            {
                var newState = new PlayerUIState(player.playerName, player.state, player.isHost);
                if (!lastPlayerStates.ContainsKey(player.playerId) ||
                    !lastPlayerStates[player.playerId].Equals(newState))
                {
                    hasChanges = true;
                    lastPlayerStates[player.playerId] = newState;
                }
            }

            // 检查是否有玩家离开
            var playersToRemove = new List<ushort>();
            foreach (var playerId in lastPlayerStates.Keys)
            {
                if (!roomData.players.ContainsKey(playerId))
                {
                    playersToRemove.Add(playerId);
                    hasChanges = true;
                }
            }

            foreach (var playerId in playersToRemove)
            {
                lastPlayerStates.Remove(playerId);
            }

            if (!hasChanges)
                return;

            // 移除已离开玩家的UI
            foreach (var playerId in playersToRemove)
            {
                RemovePlayerFromUI(playerId);
            }

            // 更新或创建玩家UI项
            foreach (var player in roomData.players.Values)
            {
                UpdateOrCreatePlayerUI(player);
            }

            LogDebug($"玩家列表已更新，当前玩家数: {roomData.players.Count}");
        }

        /// <summary>
        /// 更新或创建玩家UI项
        /// </summary>
        private void UpdateOrCreatePlayerUI(RoomPlayer player)
        {
            GameObject playerItem;

            // 检查是否已存在UI项
            if (playerUIItems.ContainsKey(player.playerId))
            {
                playerItem = playerUIItems[player.playerId];
                if (playerItem == null)
                {
                    playerUIItems.Remove(player.playerId);
                    playerItem = CreatePlayerUIItem(player);
                }
            }
            else
            {
                playerItem = CreatePlayerUIItem(player);
            }

            // 更新UI项内容
            UpdatePlayerUIContent(playerItem, player);
        }

        /// <summary>
        /// 创建玩家UI项
        /// </summary>
        private GameObject CreatePlayerUIItem(RoomPlayer player)
        {
            if (playerItemPrefab == null)
            {
                LogDebug("玩家UI预制体未设置");
                return null;
            }

            GameObject item = Instantiate(playerItemPrefab, playerListParent);
            playerUIItems[player.playerId] = item;

            LogDebug($"创建玩家UI项: {player.playerName}");
            return item;
        }

        /// <summary>
        /// 更新玩家UI项内容
        /// </summary>
        private void UpdatePlayerUIContent(GameObject playerItem, RoomPlayer player)
        {
            if (playerItem == null)
                return;

            // 更新玩家名称
            var nameText = playerItem.GetComponentInChildren<TMP_Text>();
            if (nameText != null)
            {
                string displayName = player.playerName;
                if (player.isHost)
                    displayName += " (房主)";

                nameText.text = displayName;
            }

            // 更新准备状态显示
            var statusText = playerItem.transform.Find("StatusText")?.GetComponent<TMP_Text>();
            if (statusText != null)
            {
                statusText.text = GetPlayerStateDisplayText(player.state);
                statusText.color = GetPlayerStateColor(player.state);
            }

            // 更新背景颜色或图标
            var backgroundImage = playerItem.GetComponent<Image>();
            if (backgroundImage != null)
            {
                backgroundImage.color = player.isHost ?
                    new Color(1f, 0.8f, 0.2f, 0.3f) : // 房主金色背景
                    Color.white;
            }
        }

        /// <summary>
        /// 从UI中移除玩家
        /// </summary>
        private void RemovePlayerFromUI(ushort playerId)
        {
            if (playerUIItems.ContainsKey(playerId))
            {
                var item = playerUIItems[playerId];
                if (item != null)
                {
                    Destroy(item);
                }
                playerUIItems.Remove(playerId);

                LogDebug($"移除玩家UI: {playerId}");
            }
        }

        /// <summary>
        /// 更新特定玩家的准备状态
        /// </summary>
        private void UpdatePlayerReadyState(ushort playerId, bool isReady)
        {
            if (!RoomManager.Instance?.IsInRoom == true)
                return;

            var roomData = RoomManager.Instance.CurrentRoom;
            if (roomData?.players.ContainsKey(playerId) == true)
            {
                var player = roomData.players[playerId];
                UpdateOrCreatePlayerUI(player);
            }
        }

        /// <summary>
        /// 刷新游戏控制按钮
        /// </summary>
        private void RefreshGameControls()
        {
            if (!RoomManager.Instance?.IsInRoom == true)
                return;

            bool isHost = RoomManager.Instance.IsHost;
            bool canStartGame = RoomManager.Instance.CanStartGame();
            bool myReadyState = RoomManager.Instance.GetMyReadyState();

            // 更新开始游戏按钮
            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(isHost);
                startGameButton.interactable = canStartGame;

                var buttonText = startGameButton.GetComponentInChildren<TMP_Text>();
                if (buttonText != null)
                {
                    buttonText.text = canStartGame ? "开始游戏" : "等待玩家准备";
                }
            }

            // 更新准备按钮
            if (readyButton != null)
            {
                readyButton.gameObject.SetActive(!isHost);

                if (readyButtonText != null)
                {
                    readyButtonText.text = myReadyState ? "取消准备" : "准备";
                }
            }
        }

        /// <summary>
        /// 清空UI
        /// </summary>
        private void ClearUI()
        {
            LogDebug("清空房间UI");

            // 清空房间信息
            if (roomNameText != null) roomNameText.text = "";
            if (roomCodeText != null) roomCodeText.text = "";
            if (roomStatusText != null) roomStatusText.text = "";
            if (playerCountText != null) playerCountText.text = "";

            // 清空玩家列表
            foreach (var item in playerUIItems.Values)
            {
                if (item != null)
                    Destroy(item);
            }
            playerUIItems.Clear();

            // 隐藏控制按钮
            if (startGameButton != null) startGameButton.gameObject.SetActive(false);
            if (readyButton != null) readyButton.gameObject.SetActive(false);

            // 清空缓存
            lastRoomStatus = "";
            lastPlayerCount = 0;
            lastPlayerStates.Clear();
        }

        #endregion

        #region 自动刷新机制

        /// <summary>
        /// 开始自动刷新
        /// </summary>
        private void StartAutoRefresh()
        {
            if (autoRefreshCoroutine != null)
                StopCoroutine(autoRefreshCoroutine);

            autoRefreshCoroutine = StartCoroutine(AutoRefreshCoroutine());
            LogDebug($"自动刷新已启动，间隔: {autoRefreshInterval}秒");
        }

        /// <summary>
        /// 停止自动刷新
        /// </summary>
        private void StopAutoRefresh()
        {
            if (autoRefreshCoroutine != null)
            {
                StopCoroutine(autoRefreshCoroutine);
                autoRefreshCoroutine = null;
                LogDebug("自动刷新已停止");
            }
        }

        /// <summary>
        /// 自动刷新协程
        /// </summary>
        private IEnumerator AutoRefreshCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(autoRefreshInterval);

                if (RoomManager.Instance?.IsInRoom == true)
                {
                    RefreshUI();
                }
            }
        }

        /// <summary>
        /// 延迟刷新
        /// </summary>
        private IEnumerator DelayedRefresh(float delay)
        {
            yield return new WaitForSeconds(delay);
            RefreshUI();
        }

        #endregion

        #region 按钮事件处理

        /// <summary>
        /// 准备按钮点击
        /// </summary>
        private void OnReadyButtonClicked()
        {
            if (RoomManager.Instance?.IsInRoom == true)
            {
                bool currentState = RoomManager.Instance.GetMyReadyState();
                RoomManager.Instance.SetPlayerReady(!currentState);

                LogDebug($"切换准备状态: {!currentState}");
            }
        }

        /// <summary>
        /// 开始游戏按钮点击
        /// </summary>
        private void OnStartGameButtonClicked()
        {
            if (RoomManager.Instance?.IsHost == true)
            {
                RoomManager.Instance.StartGame();
                LogDebug("房主点击开始游戏");
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取房间状态显示文本
        /// </summary>
        private string GetRoomStateDisplayText(RoomState state)
        {
            switch (state)
            {
                case RoomState.Waiting: return "等待中";
                case RoomState.Starting: return "开始中";
                case RoomState.InGame: return "游戏中";
                default: return "未知";
            }
        }

        /// <summary>
        /// 获取玩家状态显示文本
        /// </summary>
        private string GetPlayerStateDisplayText(PlayerRoomState state)
        {
            switch (state)
            {
                case PlayerRoomState.Connected: return "未准备";
                case PlayerRoomState.Ready: return "已准备";
                case PlayerRoomState.InGame: return "游戏中";
                case PlayerRoomState.Disconnected: return "已断线";
                default: return "未知";
            }
        }

        /// <summary>
        /// 获取玩家状态对应的颜色
        /// </summary>
        private Color GetPlayerStateColor(PlayerRoomState state)
        {
            switch (state)
            {
                case PlayerRoomState.Connected: return Color.red;      // 未准备 - 红色
                case PlayerRoomState.Ready: return Color.green;       // 已准备 - 绿色
                case PlayerRoomState.InGame: return Color.blue;       // 游戏中 - 蓝色
                case PlayerRoomState.Disconnected: return Color.gray; // 已断线 - 灰色
                default: return Color.yellow;                         // 未知状态 - 黄色
            }
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[RoomUIController] {message}");
            }
        }

        #endregion

        #region 公共接口和调试方法

        /// <summary>
        /// 设置自动刷新状态
        /// </summary>
        public void SetAutoRefresh(bool enable)
        {
            enableAutoRefresh = enable;

            if (enable && autoRefreshCoroutine == null)
            {
                StartAutoRefresh();
            }
            else if (!enable && autoRefreshCoroutine != null)
            {
                StopAutoRefresh();
            }
        }

        /// <summary>
        /// 设置自动刷新间隔
        /// </summary>
        public void SetAutoRefreshInterval(float interval)
        {
            autoRefreshInterval = Mathf.Max(0.1f, interval);

            if (autoRefreshCoroutine != null)
            {
                StopAutoRefresh();
                StartAutoRefresh();
            }
        }

        /// <summary>
        /// 获取当前UI状态信息
        /// </summary>
        public string GetUIStatusInfo()
        {
            return $"自动刷新: {(autoRefreshCoroutine != null ? "开启" : "关闭")}, " +
                   $"刷新间隔: {autoRefreshInterval}s, " +
                   $"玩家UI数量: {playerUIItems.Count}, " +
                   $"房间连接: {RoomManager.Instance?.IsInRoom}";
        }

        #endregion

        #region 调试方法

        [ContextMenu("强制刷新UI")]
        public void DebugForceRefresh()
        {
            if (Application.isPlaying)
            {
                ForceRefreshUI();
            }
        }

        [ContextMenu("显示UI状态")]
        public void DebugShowUIStatus()
        {
            if (Application.isPlaying)
            {
                Debug.Log($"=== RoomUIController 状态 ===\n{GetUIStatusInfo()}");
            }
        }

        [ContextMenu("重启自动刷新")]
        public void DebugRestartAutoRefresh()
        {
            if (Application.isPlaying)
            {
                StopAutoRefresh();
                StartAutoRefresh();
            }
        }

        #endregion
    }
}