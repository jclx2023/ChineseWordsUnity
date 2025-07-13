using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using Core.Network;
using Photon.Realtime;

namespace UI
{
    /// <summary>
    /// 房间界面控制器 - 简化解耦版
    /// 职责：UI显示更新、用户交互处理
    /// 通过RoomManager和NetworkManager获取数据，完全事件驱动
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

        [Header("游戏控制UI")]
        [SerializeField] private Button actionButton; // 合并后的准备/开始游戏按钮
        [SerializeField] private Image actionButtonIcon; // 按钮子物体的图标Image
        [SerializeField] private Button leaveRoomButton;

        [Header("按钮图标设置")]
        [SerializeField] private Sprite readySprite;
        [SerializeField] private Sprite cancelReadySprite;
        [SerializeField] private Sprite startGameSprite;

        [Header("UI刷新设置")]
        [SerializeField] private float autoRefreshInterval = 3f;
        [SerializeField] private bool enableAutoRefresh = true;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 玩家UI缓存
        private Dictionary<int, GameObject> playerUIItems = new Dictionary<int, GameObject>();

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
            ClearPlayerUIItems();
        }

        #region 初始化

        /// <summary>
        /// 初始化UI控制器
        /// </summary>
        private IEnumerator InitializeUIController()
        {
            LogDebug("开始初始化RoomUIController");

            // 等待RoomManager初始化
            while (RoomManager.Instance == null || !RoomManager.Instance.IsInitialized)
            {
                yield return new WaitForSeconds(0.1f);
            }

            // 等待NetworkManager准备就绪
            while (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected)
            {
                LogDebug("等待NetworkManager连接...");
                yield return new WaitForSeconds(0.1f);
            }

            // 初始化UI组件
            InitializeUIComponents();

            // 订阅事件
            SubscribeToEvents();

            // 立即刷新一次UI
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
            // 绑定动作按钮（合并的准备/开始游戏按钮）
            if (actionButton != null)
            {
                actionButton.onClick.RemoveAllListeners();
                actionButton.onClick.AddListener(OnActionButtonClicked);
            }

            // 绑定离开房间按钮
            if (leaveRoomButton != null)
            {
                leaveRoomButton.onClick.RemoveAllListeners();
                leaveRoomButton.onClick.AddListener(OnLeaveRoomButtonClicked);
            }

            LogDebug("UI组件初始化完成");
        }

        /// <summary>
        /// 订阅RoomManager事件
        /// </summary>
        private void SubscribeToEvents()
        {
            if (RoomManager.Instance != null)
            {
                RoomManager.OnRoomEntered += OnRoomEntered;
                RoomManager.OnPlayerJoinedRoom += OnPlayerJoinedRoom;
                RoomManager.OnPlayerLeftRoom += OnPlayerLeftRoom;
                RoomManager.OnPlayerReadyChanged += OnPlayerReadyChanged;
                RoomManager.OnAllPlayersReady += OnAllPlayersReady;
                RoomManager.OnGameStarting += OnGameStarting;
                RoomManager.OnReturnToLobby += OnReturnToLobby;

                LogDebug("已订阅RoomManager事件");
            }

            // 订阅NetworkManager事件
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnRoomStateReset += OnRoomStateReset;
                NetworkManager.OnPlayerReadyChanged += OnNetworkPlayerReadyChanged;
                LogDebug("已订阅NetworkManager事件");
            }
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
                RoomManager.OnAllPlayersReady -= OnAllPlayersReady;
                RoomManager.OnGameStarting -= OnGameStarting;
                RoomManager.OnReturnToLobby -= OnReturnToLobby;
            }

            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnRoomStateReset -= OnRoomStateReset;
                NetworkManager.OnPlayerReadyChanged -= OnNetworkPlayerReadyChanged;
            }

            LogDebug("已取消订阅事件");
        }

        #endregion

        #region 自动刷新机制

        /// <summary>
        /// 启动自动刷新
        /// </summary>
        private void StartAutoRefresh()
        {
            if (enableAutoRefresh && autoRefreshCoroutine == null)
            {
                autoRefreshCoroutine = StartCoroutine(AutoRefreshCoroutine());
                LogDebug($"启动自动刷新，间隔: {autoRefreshInterval}秒");
            }
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
                LogDebug("停止自动刷新");
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

                if (isInitialized && NetworkManager.Instance?.IsConnected == true)
                {
                    RefreshAllUI();
                }
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 房间进入事件处理
        /// </summary>
        private void OnRoomEntered()
        {
            LogDebug("收到房间进入事件");
            RefreshAllUI();
        }

        /// <summary>
        /// 玩家加入房间事件处理
        /// </summary>
        private void OnPlayerJoinedRoom(Player player)
        {
            if (player == null) return;
            LogDebug($"玩家加入: {player.NickName} (ID: {player.ActorNumber})");
            CreateOrUpdatePlayerUI(player);
            RefreshRoomInfo();
            RefreshActionButton();
        }

        /// <summary>
        /// 玩家离开房间事件处理
        /// </summary>
        private void OnPlayerLeftRoom(Player player)
        {
            if (player != null)
            {
                LogDebug($"玩家离开: {player.NickName} (ID: {player.ActorNumber})");
                RemovePlayerUI(player.ActorNumber);
            }
            RefreshRoomInfo();
            RefreshActionButton();
        }

        /// <summary>
        /// 玩家准备状态变化事件处理
        /// </summary>
        private void OnPlayerReadyChanged(Player player, bool isReady)
        {
            if (player == null) return;
            LogDebug($"玩家准备状态变化: {player.NickName} -> {isReady}");
            UpdatePlayerReadyState(player);
            RefreshActionButton();
        }

        /// <summary>
        /// NetworkManager玩家准备状态变化事件处理
        /// </summary>
        private void OnNetworkPlayerReadyChanged(ushort playerId, bool isReady)
        {
            LogDebug($"网络玩家准备状态变化: ID {playerId} -> {isReady}");
            RefreshPlayerList();
            RefreshActionButton();
        }

        /// <summary>
        /// 所有玩家准备就绪事件处理
        /// </summary>
        private void OnAllPlayersReady()
        {
            LogDebug("所有玩家都已准备就绪");
            RefreshActionButton();
            ShowAllReadyNotification();
        }

        /// <summary>
        /// 游戏开始事件处理
        /// </summary>
        private void OnGameStarting()
        {
            LogDebug("游戏即将开始");
            RefreshActionButton();
            ShowGameStartingNotification();
        }

        /// <summary>
        /// 返回大厅事件处理
        /// </summary>
        private void OnReturnToLobby()
        {
            LogDebug("返回大厅");
            ClearAllUI();
        }

        /// <summary>
        /// 房间状态重置事件处理
        /// </summary>
        private void OnRoomStateReset()
        {
            LogDebug("收到房间状态重置事件");
            RefreshAllUI();
        }

        #endregion

        #region UI刷新方法

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
            RefreshActionButton();
        }

        /// <summary>
        /// 刷新房间信息
        /// </summary>
        private void RefreshRoomInfo()
        {
            if (NetworkManager.Instance?.IsConnected != true) return;

            // 更新房间名称
            if (roomNameText != null)
            {
                roomNameText.text = $"房间: {NetworkManager.Instance.RoomName}";
            }

            // 更新房间代码
            if (roomCodeText != null)
            {
                string roomCode = NetworkManager.Instance.GetRoomProperty<string>("roomCode", "");
                roomCodeText.text = string.IsNullOrEmpty(roomCode) ? "房间代码: 无" : $"房间代码: {roomCode}";
            }

            // 更新房间状态
            if (roomStatusText != null)
            {
                var roomState = GetRoomState();
                roomStatusText.text = $"状态: {GetRoomStateDisplayText(roomState)}";
            }

            // 更新玩家数量和准备状态
            if (playerCountText != null)
            {
                int totalPlayers = NetworkManager.Instance.PlayerCount;
                int maxPlayers = NetworkManager.Instance.MaxPlayers;
                int readyCount = NetworkManager.Instance.GetReadyPlayerCount();
                int nonHostCount = NetworkManager.Instance.GetNonHostPlayerCount();

                playerCountText.text = $"玩家: {totalPlayers}/{maxPlayers} (准备: {readyCount}/{nonHostCount})";
            }

            LogDebug($"房间信息已更新: {NetworkManager.Instance.RoomName}, 玩家数: {NetworkManager.Instance.PlayerCount}");
        }

        /// <summary>
        /// 刷新玩家列表
        /// </summary>
        private void RefreshPlayerList()
        {
            if (NetworkManager.Instance?.IsConnected != true || playerListParent == null) return;

            LogDebug("刷新玩家列表");

            // 清空现有UI
            ClearPlayerUIItems();

            // 重新创建所有玩家UI
            var playerIds = NetworkManager.Instance.GetAllOnlinePlayerIds();
            foreach (var playerId in playerIds)
            {
                CreatePlayerUIFromId(playerId);
            }

            LogDebug($"玩家列表刷新完成，当前玩家数: {playerIds.Count}");
        }

        /// <summary>
        /// 从玩家ID创建UI
        /// </summary>
        private void CreatePlayerUIFromId(ushort playerId)
        {
            if (playerItemPrefab == null || playerListParent == null) return;

            GameObject playerItem = Instantiate(playerItemPrefab, playerListParent);
            playerItem.name = $"Player_{playerId}";
            playerUIItems[playerId] = playerItem;

            // 更新UI内容
            UpdatePlayerUIFromId(playerItem, playerId);

            LogDebug($"创建玩家UI: ID {playerId}");
        }

        /// <summary>
        /// 从玩家ID更新UI内容
        /// </summary>
        private void UpdatePlayerUIFromId(GameObject playerItem, ushort playerId)
        {
            if (playerItem == null) return;

            string playerName = NetworkManager.Instance.GetPlayerName(playerId);
            bool isHost = NetworkManager.Instance.IsHostPlayer(playerId);
            bool isReady = NetworkManager.Instance.GetPlayerReady(playerId);

            // 更新玩家名称
            var nameText = playerItem.GetComponentInChildren<TMP_Text>();
            if (nameText != null)
            {
                string displayName = playerName;
                if (isHost)
                {
                    displayName += " (房主)";
                }
                nameText.text = displayName;
            }

            // 更新准备状态显示
            var statusTexts = playerItem.GetComponentsInChildren<TMP_Text>();
            TMP_Text statusText = null;

            foreach (var text in statusTexts)
            {
                if (text != nameText && (text.name.Contains("Status") || statusTexts.Length > 1))
                {
                    statusText = text;
                    break;
                }
            }

            if (statusText != null)
            {
                if (isHost)
                {
                    statusText.text = "房主";
                    statusText.color = Color.yellow;
                }
                else
                {
                    statusText.text = isReady ? "已准备" : "未准备";
                    statusText.color = isReady ? Color.green : Color.red;
                }
            }

            // 更新背景颜色
            var backgroundImage = playerItem.GetComponent<Image>();
            if (backgroundImage != null)
            {
                if (isHost)
                {
                    backgroundImage.color = new Color(1f, 0.8f, 0.2f, 0.3f); // 房主金色背景
                }
                else
                {
                    backgroundImage.color = isReady ?
                        new Color(0.2f, 1f, 0.2f, 0.2f) : // 准备：淡绿色
                        new Color(1f, 0.2f, 0.2f, 0.2f);  // 未准备：淡红色
                }
            }
        }

        /// <summary>
        /// 创建或更新玩家UI（兼容Player对象）
        /// </summary>
        private void CreateOrUpdatePlayerUI(Player player)
        {
            if (player == null) return;
            CreatePlayerUIFromId((ushort)player.ActorNumber);
        }

        /// <summary>
        /// 更新特定玩家的准备状态UI
        /// </summary>
        private void UpdatePlayerReadyState(Player player)
        {
            if (player == null) return;

            if (playerUIItems.ContainsKey(player.ActorNumber))
            {
                var playerItem = playerUIItems[player.ActorNumber];
                if (playerItem != null)
                {
                    UpdatePlayerUIFromId(playerItem, (ushort)player.ActorNumber);
                }
            }
        }

        /// <summary>
        /// 移除玩家UI
        /// </summary>
        private void RemovePlayerUI(int actorNumber)
        {
            if (playerUIItems.ContainsKey(actorNumber))
            {
                var item = playerUIItems[actorNumber];
                if (item != null)
                {
                    Destroy(item);
                }
                playerUIItems.Remove(actorNumber);

                LogDebug($"移除玩家UI: ActorNumber {actorNumber}");
            }
        }

        /// <summary>
        /// 刷新动作按钮（合并的准备/开始游戏按钮）
        /// </summary>
        private void RefreshActionButton()
        {
            if (NetworkManager.Instance?.IsConnected != true || actionButton == null) return;

            bool isHost = NetworkManager.Instance.IsHost;
            bool canStartGame = RoomManager.Instance?.CanStartGame() ?? false;
            bool myReadyState = NetworkManager.Instance.GetMyReadyState();
            bool gameStarted = GetGameStarted();

            if (isHost)
            {
                // 房主显示开始游戏按钮
                actionButton.gameObject.SetActive(true);
                actionButton.interactable = canStartGame && !gameStarted;

                // 设置按钮图标
                SetButtonIcon(startGameSprite);
            }
            else
            {
                // 玩家显示准备按钮
                actionButton.gameObject.SetActive(true);
                actionButton.interactable = !gameStarted;

                // 根据准备状态设置图标
                SetButtonIcon(myReadyState ? cancelReadySprite : readySprite);
            }

            // 更新离开房间按钮
            if (leaveRoomButton != null)
            {
                leaveRoomButton.interactable = !gameStarted;
            }
        }

        /// <summary>
        /// 设置按钮图标（切换子物体Image的Sprite）
        /// </summary>
        private void SetButtonIcon(Sprite sprite)
        {
            if (actionButtonIcon != null && sprite != null)
            {
                actionButtonIcon.sprite = sprite;
            }
        }

        /// <summary>
        /// 清空所有UI
        /// </summary>
        private void ClearAllUI()
        {
            LogDebug("清空所有UI");

            // 清空房间信息
            if (roomNameText != null) roomNameText.text = "房间: 未连接";
            if (roomCodeText != null) roomCodeText.text = "房间代码: 无";
            if (roomStatusText != null) roomStatusText.text = "状态: 断线";
            if (playerCountText != null) playerCountText.text = "玩家: 0/0";

            // 清空玩家列表
            ClearPlayerUIItems();

            // 隐藏控制按钮
            if (actionButton != null) actionButton.gameObject.SetActive(false);
        }

        /// <summary>
        /// 清空玩家UI项
        /// </summary>
        private void ClearPlayerUIItems()
        {
            foreach (var item in playerUIItems.Values)
            {
                if (item != null)
                {
                    Destroy(item);
                }
            }
            playerUIItems.Clear();
        }

        #endregion

        #region 按钮事件处理

        /// <summary>
        /// 动作按钮点击（合并的准备/开始游戏按钮）
        /// </summary>
        private void OnActionButtonClicked()
        {
            if (NetworkManager.Instance?.IsConnected != true) return;

            bool isHost = NetworkManager.Instance.IsHost;

            if (isHost)
            {
                // 房主点击开始游戏
                if (RoomManager.Instance != null)
                {
                    if (RoomManager.Instance.CanStartGame())
                    {
                        RoomManager.Instance.StartGame();
                        LogDebug("房主点击开始游戏");
                    }
                    else
                    {
                        LogDebug($"无法开始游戏: {RoomManager.Instance.GetGameStartConditions()}");
                    }
                }
            }
            else
            {
                // 玩家点击准备/取消准备
                bool currentState = NetworkManager.Instance.GetMyReadyState();
                bool newState = !currentState;

                if (RoomManager.Instance != null)
                {
                    RoomManager.Instance.SetPlayerReady(newState);
                    LogDebug($"设置准备状态: {currentState} -> {newState}");
                }
            }
        }

        /// <summary>
        /// 离开房间按钮点击
        /// </summary>
        private void OnLeaveRoomButtonClicked()
        {
            LogDebug("点击离开房间");

            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.LeaveRoomAndReturnToLobby();
            }
        }

        #endregion

        #region 数据获取方法

        /// <summary>
        /// 获取房间状态
        /// </summary>
        private RoomState GetRoomState()
        {
            if (RoomManager.Instance != null)
            {
                return RoomManager.Instance.GetRoomState();
            }
            return RoomState.Waiting;
        }

        /// <summary>
        /// 获取游戏是否已开始
        /// </summary>
        private bool GetGameStarted()
        {
            if (RoomManager.Instance != null)
            {
                return RoomManager.Instance.GetGameStarted();
            }
            return false;
        }

        #endregion

        #region UI效果和通知

        /// <summary>
        /// 显示所有玩家准备就绪通知
        /// </summary>
        private void ShowAllReadyNotification()
        {
            LogDebug("所有玩家准备就绪 - 可以添加特效");
            // 可以在这里添加特效、音效或UI提示
        }

        /// <summary>
        /// 显示游戏开始通知
        /// </summary>
        private void ShowGameStartingNotification()
        {
            LogDebug("游戏即将开始 - 可以添加倒计时");
            // 可以在这里添加倒计时UI或特效
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
                case RoomState.Ended: return "已结束";
                default: return "未知";
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

    }
}