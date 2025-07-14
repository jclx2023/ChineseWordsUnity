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
    /// 房间界面控制器 - 完全重构版本，支持3D模型选择和动态布局
    /// 每个玩家显示：3D模型预览 + 模型切换按钮 + 个人准备按钮 + 玩家信息
    /// 支持2-8人房间的动态布局切换（2-4人单行，5-8人双行）
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

        [Header("布局配置")]
        [SerializeField] private Vector2 playerItemSize = new Vector2(300, 320);    // 单个玩家项尺寸
        [SerializeField] private Vector2 singleRowSpacing = new Vector2(68, 0);     // 单行布局间距
        [SerializeField] private Vector2 doubleRowSpacing = new Vector2(84, 62);    // 双行布局间距
        [SerializeField] private int singleRowMaxPlayers = 4;                       // 单行布局最大玩家数

        [Header("UI刷新设置")]
        [SerializeField] private float autoRefreshInterval = 3f;
        [SerializeField] private bool enableAutoRefresh = true;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 玩家UI管理
        private Dictionary<ushort, PlayerModelItemUI> playerUIItems = new Dictionary<ushort, PlayerModelItemUI>();
        private Dictionary<ushort, RoomPlayerData> playerDataCache = new Dictionary<ushort, RoomPlayerData>();

        // 布局管理
        private GridLayoutGroup gridLayoutGroup;
        private int currentMaxPlayers = 0;
        private bool isLayoutInitialized = false;

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

            // 初始化布局组件
            InitializeLayoutComponents();

            LogDebug("UI组件初始化完成");
        }

        /// <summary>
        /// 初始化布局组件
        /// </summary>
        private void InitializeLayoutComponents()
        {
            if (playerListParent == null)
            {
                Debug.LogError("[RoomUIController] PlayerListParent 未设置！");
                return;
            }

            // 获取或添加GridLayoutGroup组件
            gridLayoutGroup = playerListParent.GetComponent<GridLayoutGroup>();
            if (gridLayoutGroup == null)
            {
                gridLayoutGroup = playerListParent.gameObject.AddComponent<GridLayoutGroup>();
                LogDebug("已添加GridLayoutGroup组件");
            }

            // 基础设置
            gridLayoutGroup.cellSize = playerItemSize;
            gridLayoutGroup.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayoutGroup.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayoutGroup.childAlignment = TextAnchor.MiddleCenter;

            LogDebug("布局组件初始化完成");
        }

        /// <summary>
        /// 初始化玩家数据
        /// </summary>
        private void InitializePlayerData()
        {
            // 获取房间最大玩家数并设置布局
            int maxPlayers = NetworkManager.Instance?.MaxPlayers ?? 4;
            SetupPlayerListLayout(maxPlayers);

            foreach (var player in PhotonNetwork.PlayerList)
            {
                ushort playerId = (ushort)player.ActorNumber;

                // 尝试从Photon玩家属性中获取模型ID
                int selectedModelId = PlayerModelManager.Instance.GetDefaultModelId();
                if (player.CustomProperties != null && player.CustomProperties.ContainsKey("selectedModelId"))
                {
                    selectedModelId = (int)player.CustomProperties["selectedModelId"];
                    LogDebug($"从玩家属性恢复模型ID: 玩家{playerId} -> 模型{selectedModelId}");
                }

                var playerData = new RoomPlayerData
                {
                    playerId = playerId,
                    playerName = player.NickName ?? $"Player_{playerId}",
                    isHost = player.IsMasterClient,
                    isReady = NetworkManager.Instance.GetPlayerReady(playerId),
                    selectedModelId = selectedModelId // 使用实际的模型ID
                };

                // 安全地初始化同步时间
                playerData.InitializeSyncTime();

                playerDataCache[playerId] = playerData;

                LogDebug($"初始化玩家数据: {playerData.playerName} (模型ID: {selectedModelId})");
            }

            LogDebug($"初始化了 {playerDataCache.Count} 个玩家数据，最大玩家数: {maxPlayers}");

            // 如果是新加入的玩家，请求同步所有玩家的模型数据
            RequestModelSyncFromHost();
        }
        /// <summary>
        /// 请求从房主同步模型数据
        /// </summary>
        private void RequestModelSyncFromHost()
        {
            if (NetworkManager.Instance != null && !NetworkManager.Instance.IsHost)
            {
                LogDebug("请求从房主同步模型数据");
                NetworkManager.Instance.RequestAllPlayerModels();
            }
        }

        #endregion

        #region 动态布局管理

        /// <summary>
        /// 根据最大玩家数设置玩家列表布局
        /// </summary>
        /// <param name="maxPlayers">房间最大玩家数</param>
        private void SetupPlayerListLayout(int maxPlayers)
        {
            if (gridLayoutGroup == null)
            {
                Debug.LogError("[RoomUIController] GridLayoutGroup 未初始化！");
                return;
            }

            // 如果布局已经为这个玩家数设置过，则跳过
            if (isLayoutInitialized && currentMaxPlayers == maxPlayers)
            {
                LogDebug($"布局已为 {maxPlayers} 人设置，跳过重复设置");
                return;
            }

            currentMaxPlayers = maxPlayers;

            // 更新单元格大小（以防运行时修改了配置）
            gridLayoutGroup.cellSize = playerItemSize;

            if (maxPlayers <= singleRowMaxPlayers)
            {
                SetupSingleRowLayout(maxPlayers);
            }
            else
            {
                SetupDoubleRowLayout(maxPlayers);
            }

            isLayoutInitialized = true;
            LogDebug($"布局设置完成: {maxPlayers} 人 {(maxPlayers <= singleRowMaxPlayers ? "单行" : "双行")} 布局");
        }

        /// <summary>
        /// 设置单行布局（2-4人）
        /// </summary>
        /// <param name="maxPlayers">最大玩家数</param>
        private void SetupSingleRowLayout(int maxPlayers)
        {
            gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            gridLayoutGroup.constraintCount = 1;

            // 动态计算水平间距以实现居中效果
            float parentWidth = GetPlayerListPanelWidth();
            float totalItemWidth = maxPlayers * playerItemSize.x;

            if (parentWidth > totalItemWidth)
            {
                float availableSpaceForSpacing = parentWidth - totalItemWidth;
                float horizontalSpacing = availableSpaceForSpacing / (maxPlayers + 1);

                // 限制间距的最小和最大值
                horizontalSpacing = Mathf.Clamp(horizontalSpacing, 20f, 200f);

                gridLayoutGroup.spacing = new Vector2(horizontalSpacing, singleRowSpacing.y);

                LogDebug($"单行布局 - 玩家数: {maxPlayers}, 容器宽度: {parentWidth:F0}, 水平间距: {horizontalSpacing:F0}");
            }
            else
            {
                // 如果空间不够，使用默认间距
                gridLayoutGroup.spacing = singleRowSpacing;
                LogDebug($"单行布局 - 空间不足，使用默认间距: {singleRowSpacing}");
            }
        }

        /// <summary>
        /// 设置双行布局（5-8人）
        /// </summary>
        /// <param name="maxPlayers">最大玩家数</param>
        private void SetupDoubleRowLayout(int maxPlayers)
        {
            gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            gridLayoutGroup.constraintCount = 2;
            gridLayoutGroup.spacing = doubleRowSpacing;

            LogDebug($"双行布局 - 玩家数: {maxPlayers}, 间距: {doubleRowSpacing}");
        }

        /// <summary>
        /// 获取PlayerListPanel的宽度
        /// </summary>
        /// <returns>面板宽度</returns>
        private float GetPlayerListPanelWidth()
        {
            if (playerListParent == null) return 1536f; // 默认值

            RectTransform rectTransform = playerListParent.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                return rectTransform.rect.width;
            }

            return 1536f; // 1920x1080下80%宽度的默认值
        }

        /// <summary>
        /// 强制重新计算布局（用于运行时调整）
        /// </summary>
        public void RecalculateLayout()
        {
            if (gridLayoutGroup != null)
            {
                // 强制重新布局
                LayoutRebuilder.ForceRebuildLayoutImmediate(playerListParent.GetComponent<RectTransform>());
                LogDebug("强制重新计算布局");
            }
        }

        /// <summary>
        /// 重置布局（用于房间人数变化）
        /// </summary>
        public void ResetLayout()
        {
            isLayoutInitialized = false;
            currentMaxPlayers = 0;

            if (NetworkManager.Instance != null)
            {
                int maxPlayers = NetworkManager.Instance.MaxPlayers;
                SetupPlayerListLayout(maxPlayers);
                RecalculateLayout();
            }

            LogDebug("布局已重置");
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
                NetworkManager.OnAllPlayerModelsReceived += OnAllPlayerModelsReceived;
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
                NetworkManager.OnAllPlayerModelsReceived -= OnAllPlayerModelsReceived;
            }

            LogDebug("已取消订阅事件");
        }

        #endregion

        #region 事件处理

        private void OnRoomEntered()
        {
            LogDebug("房间进入事件");

            // 房间进入时重新设置布局
            ResetLayout();

            // 延迟刷新UI，确保所有数据都已同步
            StartCoroutine(DelayedUIRefresh());
        }

        /// <summary>
        /// 延迟刷新UI
        /// </summary>
        private IEnumerator DelayedUIRefresh()
        {
            yield return new WaitForSeconds(0.2f);

            // 重新初始化玩家数据（包括模型ID）
            InitializePlayerData();

            yield return new WaitForSeconds(0.2f);

            // 刷新所有UI
            RefreshAllUI();

            LogDebug("延迟UI刷新完成");
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

            // 安全地初始化同步时间
            playerData.InitializeSyncTime();

            playerDataCache[playerId] = playerData;

            // 创建UI
            CreatePlayerUI(playerId);

            // 检查是否需要重新布局（玩家数量变化）
            CheckAndUpdateLayout();

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

            // 检查是否需要重新布局（玩家数量变化）
            CheckAndUpdateLayout();

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

                    // 安全地初始化同步时间
                    playerData.InitializeSyncTime();

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

            // 在刷新完成后检查布局
            CheckAndUpdateLayout();
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

        #region 布局检查和更新

        /// <summary>
        /// 检查并更新布局（当玩家数量变化时）
        /// </summary>
        private void CheckAndUpdateLayout()
        {
            if (NetworkManager.Instance == null) return;

            int currentMaxPlayers = NetworkManager.Instance.MaxPlayers;
            int currentPlayerCount = playerDataCache.Count;

            // 检查是否需要重新设置布局
            bool needsLayoutUpdate = false;

            // 如果最大玩家数变化，需要重新布局
            if (this.currentMaxPlayers != currentMaxPlayers)
            {
                needsLayoutUpdate = true;
                LogDebug($"最大玩家数变化: {this.currentMaxPlayers} -> {currentMaxPlayers}");
            }

            // 如果从单行布局范围切换到双行布局范围（或反之），需要重新布局
            bool wasInSingleRowRange = this.currentMaxPlayers <= singleRowMaxPlayers;
            bool isInSingleRowRange = currentMaxPlayers <= singleRowMaxPlayers;

            if (wasInSingleRowRange != isInSingleRowRange)
            {
                needsLayoutUpdate = true;
                LogDebug($"布局模式变化: {(wasInSingleRowRange ? "单行" : "双行")} -> {(isInSingleRowRange ? "单行" : "双行")}");
            }

            if (needsLayoutUpdate)
            {
                SetupPlayerListLayout(currentMaxPlayers);
                RecalculateLayout();
            }
        }

        #endregion

        #region 玩家UI管理
        /// <summary>
        /// 处理接收到的所有玩家模型数据
        /// </summary>
        private void OnAllPlayerModelsReceived(ushort[] playerIds, int[] modelIds)
        {
            if (playerIds.Length != modelIds.Length)
            {
                Debug.LogError("[RoomUIController] 玩家ID和模型ID数组长度不匹配");
                return;
            }

            LogDebug($"收到所有玩家模型数据: {playerIds.Length} 个玩家");

            for (int i = 0; i < playerIds.Length; i++)
            {
                ushort playerId = playerIds[i];
                int modelId = modelIds[i];

                if (playerDataCache.ContainsKey(playerId))
                {
                    string modelName = PlayerModelManager.Instance.GetModelName(modelId);
                    playerDataCache[playerId].SetSelectedModel(modelId, modelName);
                    UpdatePlayerUI(playerId);

                    LogDebug($"更新玩家模型: {playerId} -> 模型{modelId}({modelName})");
                }
            }

            LogDebug("所有玩家模型数据已更新");
        }
        /// <summary>
        /// 创建玩家UI
        /// </summary>
        private void CreatePlayerUI(ushort playerId)
        {

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

        /// <summary>
        /// 获取当前布局信息（调试用）
        /// </summary>
        public string GetLayoutInfo()
        {
            if (gridLayoutGroup == null) return "布局组件未初始化";

            return $"布局信息: 最大玩家数={currentMaxPlayers}, " +
                   $"约束={gridLayoutGroup.constraint}, " +
                   $"约束数量={gridLayoutGroup.constraintCount}, " +
                   $"单元格大小={gridLayoutGroup.cellSize}, " +
                   $"间距={gridLayoutGroup.spacing}, " +
                   $"面板宽度={GetPlayerListPanelWidth():F0}";
        }

        /// <summary>
        /// 手动触发布局重置（供外部调用）
        /// </summary>
        [ContextMenu("重置布局")]
        public void ManualResetLayout()
        {
            LogDebug("手动重置布局");
            ResetLayout();
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

        // 移除重复的CanStartGame方法，已在公共接口部分定义

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