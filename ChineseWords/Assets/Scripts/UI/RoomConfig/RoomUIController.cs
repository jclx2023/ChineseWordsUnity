using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using Core.Network;
using Photon.Realtime;
using RoomScene.Manager;
using RoomScene.Data;
using Photon.Pun;
using System.Linq;

namespace UI
{
    /// <summary>
    /// 房间界面控制器 - 完全重构版本，支持3D模型选择
    /// 每个玩家显示：3D模型预览 + 模型切换按钮 + 个人准备按钮 + 玩家信息
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
        [SerializeField] private GameObject playerModelItemPrefab;    // 包含3D模型预览的玩家项预制体

        [Header("房主控制UI")]
        [SerializeField] private Button leaveRoomButton;             // 只保留离开房间按钮，开始游戏合并到各自的actionButton

        [Header("UI刷新设置")]
        [SerializeField] private float autoRefreshInterval = 3f;
        [SerializeField] private bool enableAutoRefresh = true;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 玩家UI管理
        private Dictionary<ushort, PlayerModelItemUI> playerUIItems = new Dictionary<ushort, PlayerModelItemUI>();
        private Dictionary<ushort, RoomPlayerData> playerDataCache = new Dictionary<ushort, RoomPlayerData>();

        // 状态管理
        private bool isInitialized = false;
        private Coroutine autoRefreshCoroutine;

        private void Start()
        {
            StartCoroutine(InitializeUIController());
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            StopAutoRefresh();
            ClearAllPlayerUI();
        }

        #region 初始化

        /// <summary>
        /// 初始化UI控制器
        /// </summary>
        private IEnumerator InitializeUIController()
        {
            LogDebug("开始初始化RoomUIController (模型选择版本)");

            // 等待依赖组件
            while (RoomManager.Instance == null || NetworkManager.Instance == null ||
                   PlayerModelManager.Instance == null)
            {
                yield return new WaitForSeconds(0.1f);
            }

            while (!NetworkManager.Instance.IsConnected)
            {
                LogDebug("等待网络连接...");
                yield return new WaitForSeconds(0.1f);
            }

            // 验证必要组件
            if (playerModelItemPrefab == null)
            {
                Debug.LogError("[RoomUIController] 未设置playerModelItemPrefab！");
                yield break;
            }

            // 初始化UI组件
            InitializeUIComponents();

            // 订阅事件
            SubscribeToEvents();

            // 初始化玩家数据
            InitializePlayerData();

            // 立即刷新UI
            RefreshAllUI();

            // 启动自动刷新
            StartAutoRefresh();

            isInitialized = true;
            LogDebug("RoomUIController初始化完成");
        }

        /// <summary>
        /// 初始化UI组件
        /// </summary>
        private void InitializeUIComponents()
        {
            // 绑定离开房间按钮
            if (leaveRoomButton != null)
            {
                leaveRoomButton.onClick.RemoveAllListeners();
                leaveRoomButton.onClick.AddListener(OnLeaveRoomButtonClicked);
            }

            LogDebug("UI组件初始化完成");
        }

        /// <summary>
        /// 初始化玩家数据
        /// </summary>
        private void InitializePlayerData()
        {
            foreach (var player in PhotonNetwork.PlayerList)
            {
                ushort playerId = (ushort)player.ActorNumber;
                var playerData = new RoomPlayerData
                {
                    playerId = playerId,
                    playerName = player.NickName ?? $"Player_{playerId}",
                    isHost = player.IsMasterClient,
                    isReady = NetworkManager.Instance.GetPlayerReady(playerId),
                    selectedModelId = PlayerModelManager.Instance.GetDefaultModelId()
                };

                playerDataCache[playerId] = playerData;
            }

            LogDebug($"初始化了 {playerDataCache.Count} 个玩家数据");
        }

        #endregion

        #region 事件订阅

        /// <summary>
        /// 订阅事件
        /// </summary>
        private void SubscribeToEvents()
        {
            // RoomManager事件
            if (RoomManager.Instance != null)
            {
                RoomManager.OnRoomEntered += OnRoomEntered;
                RoomManager.OnPlayerJoinedRoom += OnPlayerJoinedRoom;
                RoomManager.OnPlayerLeftRoom += OnPlayerLeftRoom;
                RoomManager.OnPlayerReadyChanged += OnPlayerReadyChanged;
                RoomManager.OnGameStarting += OnGameStarting;
                RoomManager.OnReturnToLobby += OnReturnToLobby;
            }

            // NetworkManager事件
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerReadyChanged += OnNetworkPlayerReadyChanged;
                NetworkManager.OnPlayerModelChanged += OnPlayerModelChanged;
                NetworkManager.OnModelSyncRequested += OnModelSyncRequested;
            }

            LogDebug("已订阅所有事件");
        }

        /// <summary>
        /// 取消订阅事件
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (RoomManager.Instance != null)
            {
                RoomManager.OnRoomEntered -= OnRoomEntered;
                RoomManager.OnPlayerJoinedRoom -= OnPlayerJoinedRoom;
                RoomManager.OnPlayerLeftRoom -= OnPlayerLeftRoom;
                RoomManager.OnPlayerReadyChanged -= OnPlayerReadyChanged;
                RoomManager.OnGameStarting -= OnGameStarting;
                RoomManager.OnReturnToLobby -= OnReturnToLobby;
            }

            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerReadyChanged -= OnNetworkPlayerReadyChanged;
                NetworkManager.OnPlayerModelChanged -= OnPlayerModelChanged;
                NetworkManager.OnModelSyncRequested -= OnModelSyncRequested;
            }

            LogDebug("已取消订阅事件");
        }

        #endregion

        #region 事件处理

        private void OnRoomEntered()
        {
            LogDebug("房间进入事件");
            RefreshAllUI();
        }

        private void OnPlayerJoinedRoom(Player player)
        {
            if (player == null) return;

            ushort playerId = (ushort)player.ActorNumber;
            LogDebug($"玩家加入: {player.NickName} (ID: {playerId})");

            // 创建玩家数据
            var playerData = new RoomPlayerData
            {
                playerId = playerId,
                playerName = player.NickName ?? $"Player_{playerId}",
                isHost = player.IsMasterClient,
                isReady = false,
                selectedModelId = PlayerModelManager.Instance.GetDefaultModelId()
            };
            playerDataCache[playerId] = playerData;

            // 创建UI
            CreatePlayerUI(playerId);
            RefreshRoomInfo();
            RefreshHostControls();
        }

        private void OnPlayerLeftRoom(Player player)
        {
            if (player == null) return;

            ushort playerId = (ushort)player.ActorNumber;
            LogDebug($"玩家离开: {player.NickName} (ID: {playerId})");

            RemovePlayerUI(playerId);
            playerDataCache.Remove(playerId);

            RefreshRoomInfo();
            RefreshHostControls();
        }

        private void OnPlayerReadyChanged(Player player, bool isReady)
        {
            if (player == null) return;

            ushort playerId = (ushort)player.ActorNumber;
            LogDebug($"玩家准备状态变化: {player.NickName} -> {isReady}");

            if (playerDataCache.ContainsKey(playerId))
            {
                playerDataCache[playerId].SetReady(isReady);
            }

            UpdatePlayerUI(playerId);
            RefreshHostControls();
        }

        private void OnNetworkPlayerReadyChanged(ushort playerId, bool isReady)
        {
            LogDebug($"网络玩家准备状态变化: ID {playerId} -> {isReady}");

            if (playerDataCache.ContainsKey(playerId))
            {
                playerDataCache[playerId].SetReady(isReady);
            }

            UpdatePlayerUI(playerId);
            RefreshHostControls();
        }

        private void OnPlayerModelChanged(ushort playerId, int modelId, string modelName)
        {
            LogDebug($"玩家模型变化: ID {playerId} -> 模型 {modelId} ({modelName})");

            if (playerDataCache.ContainsKey(playerId))
            {
                playerDataCache[playerId].SetSelectedModel(modelId, modelName);
            }

            UpdatePlayerUI(playerId);
        }

        private void OnModelSyncRequested(ushort requestingPlayerId)
        {
            if (!NetworkManager.Instance.IsHost) return;

            LogDebug($"收到玩家 {requestingPlayerId} 的模型同步请求");

            // 发送当前所有玩家的模型数据
            var playerIds = new List<ushort>();
            var modelIds = new List<int>();

            foreach (var kvp in playerDataCache)
            {
                playerIds.Add(kvp.Key);
                modelIds.Add(kvp.Value.selectedModelId);
            }

            if (playerIds.Count > 0)
            {
                NetworkManager.Instance.BroadcastAllPlayerModels(playerIds.ToArray(), modelIds.ToArray());
            }
        }

        private void OnGameStarting()
        {
            LogDebug("游戏即将开始，保存模型选择数据");
            SavePlayerModelSelections();
            RefreshHostControls();
        }

        private void OnReturnToLobby()
        {
            LogDebug("返回大厅");
            ClearAllUI();
        }

        #endregion

        #region UI刷新

        /// <summary>
        /// 刷新所有UI
        /// </summary>
        public void RefreshAllUI()
        {
            if (NetworkManager.Instance?.IsConnected != true)
            {
                LogDebug("未连接房间，清空UI");
                ClearAllUI();
                return;
            }

            LogDebug("刷新所有UI");
            RefreshRoomInfo();
            RefreshPlayerList();
            RefreshHostControls();
        }

        /// <summary>
        /// 刷新房间信息
        /// </summary>
        private void RefreshRoomInfo()
        {
            if (NetworkManager.Instance?.IsConnected != true) return;

            if (roomNameText != null)
                roomNameText.text = $"房间: {NetworkManager.Instance.RoomName}";

            if (roomCodeText != null)
            {
                string roomCode = NetworkManager.Instance.GetRoomProperty<string>("roomCode", "");
                roomCodeText.text = string.IsNullOrEmpty(roomCode) ? "房间代码: 无" : $"房间代码: {roomCode}";
            }

            if (roomStatusText != null)
            {
                var roomState = GetRoomState();
                roomStatusText.text = $"状态: {GetRoomStateDisplayText(roomState)}";
            }

            if (playerCountText != null)
            {
                int totalPlayers = NetworkManager.Instance.PlayerCount;
                int maxPlayers = NetworkManager.Instance.MaxPlayers;
                int readyCount = GetReadyPlayerCount();
                int nonHostCount = GetNonHostPlayerCount();

                playerCountText.text = $"玩家: {totalPlayers}/{maxPlayers} (准备: {readyCount}/{nonHostCount})";
            }
        }

        /// <summary>
        /// 刷新玩家列表
        /// </summary>
        private void RefreshPlayerList()
        {
            if (NetworkManager.Instance?.IsConnected != true) return;

            LogDebug("刷新玩家列表");

            // 获取当前房间所有玩家
            var currentPlayerIds = new HashSet<ushort>();
            foreach (var player in PhotonNetwork.PlayerList)
            {
                ushort playerId = (ushort)player.ActorNumber;
                currentPlayerIds.Add(playerId);

                // 确保玩家数据存在
                if (!playerDataCache.ContainsKey(playerId))
                {
                    var playerData = new RoomPlayerData
                    {
                        playerId = playerId,
                        playerName = player.NickName ?? $"Player_{playerId}",
                        isHost = player.IsMasterClient,
                        isReady = NetworkManager.Instance.GetPlayerReady(playerId),
                        selectedModelId = PlayerModelManager.Instance.GetDefaultModelId()
                    };
                    playerDataCache[playerId] = playerData;
                }

                // 创建或更新UI
                if (!playerUIItems.ContainsKey(playerId))
                {
                    CreatePlayerUI(playerId);
                }
                else
                {
                    UpdatePlayerUI(playerId);
                }
            }

            // 移除已离开玩家的UI
            var playersToRemove = new List<ushort>();
            foreach (var playerId in playerUIItems.Keys)
            {
                if (!currentPlayerIds.Contains(playerId))
                {
                    playersToRemove.Add(playerId);
                }
            }

            foreach (var playerId in playersToRemove)
            {
                RemovePlayerUI(playerId);
                playerDataCache.Remove(playerId);
            }
        }

        /// <summary>
        /// 刷新房主控制
        /// </summary>
        private void RefreshHostControls()
        {
            bool gameStarted = GetGameStarted();

            // 离开房间按钮
            if (leaveRoomButton != null)
            {
                leaveRoomButton.interactable = !gameStarted;
            }
        }

        #endregion

        #region 玩家UI管理

        /// <summary>
        /// 创建玩家UI
        /// </summary>
        private void CreatePlayerUI(ushort playerId)
        {
            if (playerModelItemPrefab == null || playerListParent == null)
            {
                Debug.LogError("[RoomUIController] 缺少必要的UI预制体或父级");
                return;
            }

            if (playerUIItems.ContainsKey(playerId))
            {
                LogDebug($"玩家 {playerId} 的UI已存在，先移除");
                RemovePlayerUI(playerId);
            }

            // 实例化UI项
            GameObject uiItem = Instantiate(playerModelItemPrefab, playerListParent);
            uiItem.name = $"PlayerModelItem_{playerId}";

            // 获取PlayerModelItemUI组件
            var itemUI = uiItem.GetComponent<PlayerModelItemUI>();
            if (itemUI == null)
            {
                itemUI = uiItem.AddComponent<PlayerModelItemUI>();
            }

            // 初始化UI项
            var playerData = playerDataCache[playerId];
            itemUI.Initialize(playerId, playerData, this);

            // 缓存UI项
            playerUIItems[playerId] = itemUI;

            LogDebug($"创建玩家UI: {playerData.playerName} (ID: {playerId})");
        }

        /// <summary>
        /// 更新玩家UI
        /// </summary>
        private void UpdatePlayerUI(ushort playerId)
        {
            if (!playerUIItems.ContainsKey(playerId) || !playerDataCache.ContainsKey(playerId))
                return;

            var itemUI = playerUIItems[playerId];
            var playerData = playerDataCache[playerId];

            itemUI.UpdateDisplay(playerData);
        }

        /// <summary>
        /// 移除玩家UI
        /// </summary>
        private void RemovePlayerUI(ushort playerId)
        {
            if (playerUIItems.ContainsKey(playerId))
            {
                var itemUI = playerUIItems[playerId];
                if (itemUI != null && itemUI.gameObject != null)
                {
                    Destroy(itemUI.gameObject);
                }
                playerUIItems.Remove(playerId);
                LogDebug($"移除玩家UI: ID {playerId}");
            }
        }

        /// <summary>
        /// 清空所有玩家UI
        /// </summary>
        private void ClearAllPlayerUI()
        {
            foreach (var itemUI in playerUIItems.Values)
            {
                if (itemUI != null && itemUI.gameObject != null)
                {
                    Destroy(itemUI.gameObject);
                }
            }
            playerUIItems.Clear();
            playerDataCache.Clear();
        }

        /// <summary>
        /// 清空所有UI
        /// </summary>
        private void ClearAllUI()
        {
            // 清空房间信息
            if (roomNameText != null) roomNameText.text = "房间: 未连接";
            if (roomCodeText != null) roomCodeText.text = "房间代码: 无";
            if (roomStatusText != null) roomStatusText.text = "状态: 断线";
            if (playerCountText != null) playerCountText.text = "玩家: 0/0";

            // 清空玩家列表
            ClearAllPlayerUI();

            // 隐藏控制按钮
            // 移除了startGameButton相关代码，因为开始游戏功能已合并到各自的actionButton
        }

        #endregion

        #region 公共接口 - 供PlayerModelItemUI调用

        /// <summary>
        /// 玩家模型选择变化
        /// </summary>
        public void OnPlayerModelSelectionChanged(ushort playerId, int newModelId)
        {
            if (!playerDataCache.ContainsKey(playerId)) return;

            string modelName = PlayerModelManager.Instance.GetModelName(newModelId);
            playerDataCache[playerId].SetSelectedModel(newModelId, modelName);

            // 通过网络同步给其他玩家
            NetworkManager.Instance.SendPlayerModelChangeRPC(playerId, newModelId, modelName);

            LogDebug($"玩家 {playerId} 选择模型: {modelName} (ID: {newModelId})");
        }

        /// <summary>
        /// 玩家准备状态变化（支持房主开始游戏）
        /// </summary>
        public void OnPlayerReadyStateChanged(ushort playerId, bool isReady)
        {
            if (playerId != NetworkManager.Instance.ClientId)
            {
                LogDebug("只能改变自己的状态");
                return;
            }

            var playerData = GetPlayerData(playerId);
            if (playerData == null) return;

            if (playerData.isHost)
            {
                // 房主点击开始游戏
                if (CanStartGame())
                {
                    RoomManager.Instance.StartGame();
                    LogDebug("房主点击开始游戏");
                }
                else
                {
                    LogDebug("无法开始游戏 - 条件不满足");
                }
            }
            else
            {
                // 普通玩家改变准备状态
                RoomManager.Instance.SetPlayerReady(isReady);
                LogDebug($"设置本地玩家准备状态: {isReady}");
            }
        }

        /// <summary>
        /// 获取玩家数据
        /// </summary>
        public RoomPlayerData GetPlayerData(ushort playerId)
        {
            return playerDataCache.TryGetValue(playerId, out RoomPlayerData data) ? data : null;
        }

        /// <summary>
        /// 检查是否可以开始游戏（供PlayerModelItemUI调用）
        /// </summary>
        public bool CanStartGame()
        {
            return RoomManager.Instance?.CanStartGame() ?? false;
        }

        #endregion

        #region 数据保存

        /// <summary>
        /// 保存玩家模型选择数据
        /// </summary>
        private void SavePlayerModelSelections()
        {
            var modelSelections = new Dictionary<ushort, int>();
            foreach (var kvp in playerDataCache)
            {
                modelSelections[kvp.Key] = kvp.Value.selectedModelId;
            }

            PlayerModelSelectionData.SaveSelections(modelSelections);
            LogDebug($"保存了 {modelSelections.Count} 个玩家的模型选择数据");
        }

        #endregion

        #region 按钮事件

        private void OnLeaveRoomButtonClicked()
        {
            LogDebug("点击离开房间");
            RoomManager.Instance.LeaveRoomAndReturnToLobby();
        }

        #endregion

        #region 自动刷新

        private void StartAutoRefresh()
        {
            if (enableAutoRefresh && autoRefreshCoroutine == null)
            {
                autoRefreshCoroutine = StartCoroutine(AutoRefreshCoroutine());
                LogDebug($"启动自动刷新，间隔: {autoRefreshInterval}秒");
            }
        }

        private void StopAutoRefresh()
        {
            if (autoRefreshCoroutine != null)
            {
                StopCoroutine(autoRefreshCoroutine);
                autoRefreshCoroutine = null;
            }
        }

        private IEnumerator AutoRefreshCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(autoRefreshInterval);

                if (isInitialized && NetworkManager.Instance?.IsConnected == true)
                {
                    RefreshAllUI();
                }
            }
        }

        #endregion

        #region 辅助方法

        private RoomState GetRoomState()
        {
            return RoomManager.Instance?.GetRoomState() ?? RoomState.Waiting;
        }

        private bool GetGameStarted()
        {
            return RoomManager.Instance?.GetGameStarted() ?? false;
        }

        private int GetReadyPlayerCount()
        {
            return playerDataCache.Values.Count(p => p.isReady && !p.isHost);
        }

        private int GetNonHostPlayerCount()
        {
            return playerDataCache.Values.Count(p => !p.isHost);
        }

        private string GetRoomStateDisplayText(RoomState state)
        {
            switch (state)
            {
                case RoomState.Waiting: return "等待中";
                case RoomState.Starting: return "开始中";
                case RoomState.InGame: return "游戏中";
                case RoomState.Ended: return "已结束";
                default: return "未知";
            }
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[RoomUIController] {message}");
            }
        }

        #endregion
    }
}