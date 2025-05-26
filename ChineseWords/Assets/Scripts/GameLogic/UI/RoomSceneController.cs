using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
using Core.Network;

namespace UI
{
    /// <summary>
    /// 房间场景控制器
    /// 管理房间UI显示和用户交互
    /// </summary>
    public class RoomSceneController : MonoBehaviour
    {
        [Header("房间信息UI")]
        [SerializeField] private TMP_Text roomNameText;
        [SerializeField] private TMP_Text roomCodeText;
        [SerializeField] private TMP_Text playerCountText;
        [SerializeField] private TMP_Text roomStatusText;

        [Header("玩家列表")]
        [SerializeField] private Transform playerListParent;
        [SerializeField] private GameObject playerItemPrefab;
        [SerializeField] private ScrollRect playerListScrollRect;

        [Header("控制按钮")]
        [SerializeField] private Button readyButton;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button leaveRoomButton;
        [SerializeField] private Button refreshButton;

        [Header("状态显示")]
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private TMP_Text loadingText;
        [SerializeField] private GameObject errorPanel;
        [SerializeField] private TMP_Text errorText;

        [Header("设置")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private float uiUpdateInterval = 0.5f;

        // 玩家列表UI项
        private Dictionary<ushort, GameObject> playerListItems = new Dictionary<ushort, GameObject>();

        // 状态管理
        private bool isInitialized = false;
        private float lastUIUpdateTime = 0f;

        private void Start()
        {
            InitializeRoomScene();
        }

        /// <summary>
        /// 初始化房间场景
        /// </summary>
        private void InitializeRoomScene()
        {
            LogDebug("初始化房间场景");

            // 显示加载界面
            ShowLoadingPanel("正在加载房间信息...");

            // 绑定UI事件
            BindUIEvents();

            // 订阅房间事件
            SubscribeToRoomEvents();

            // 验证网络状态
            if (!ValidateNetworkStatus())
            {
                ShowError("网络连接异常，请返回主菜单重试");
                return;
            }

            // 验证房间状态
            if (!ValidateRoomStatus())
            {
                ShowError("房间状态异常，请返回主菜单重试");
                return;
            }

            // 初始化UI状态
            InitializeUIState();

            // 标记为已初始化
            isInitialized = true;
            HideLoadingPanel();

            LogDebug("房间场景初始化完成");
        }

        /// <summary>
        /// 绑定UI事件
        /// </summary>
        private void BindUIEvents()
        {
            if (readyButton != null)
                readyButton.onClick.AddListener(OnReadyButtonClicked);

            if (startGameButton != null)
                startGameButton.onClick.AddListener(OnStartGameButtonClicked);

            if (leaveRoomButton != null)
                leaveRoomButton.onClick.AddListener(OnLeaveRoomButtonClicked);

            if (refreshButton != null)
                refreshButton.onClick.AddListener(OnRefreshButtonClicked);
        }

        /// <summary>
        /// 订阅房间事件
        /// </summary>
        private void SubscribeToRoomEvents()
        {
            RoomManager.OnRoomCreated += OnRoomCreated;
            RoomManager.OnRoomJoined += OnRoomJoined;
            RoomManager.OnPlayerJoinedRoom += OnPlayerJoinedRoom;
            RoomManager.OnPlayerLeftRoom += OnPlayerLeftRoom;
            RoomManager.OnPlayerReadyChanged += OnPlayerReadyChanged;
            RoomManager.OnGameStarting += OnGameStarting;
            RoomManager.OnRoomLeft += OnRoomLeft;

            // 订阅网络事件
            NetworkManager.OnDisconnected += OnNetworkDisconnected;
        }

        /// <summary>
        /// 取消订阅房间事件
        /// </summary>
        private void UnsubscribeFromRoomEvents()
        {
            RoomManager.OnRoomCreated -= OnRoomCreated;
            RoomManager.OnRoomJoined -= OnRoomJoined;
            RoomManager.OnPlayerJoinedRoom -= OnPlayerJoinedRoom;
            RoomManager.OnPlayerLeftRoom -= OnPlayerLeftRoom;
            RoomManager.OnPlayerReadyChanged -= OnPlayerReadyChanged;
            RoomManager.OnGameStarting -= OnGameStarting;
            RoomManager.OnRoomLeft -= OnRoomLeft;

            NetworkManager.OnDisconnected -= OnNetworkDisconnected;
        }

        private void Update()
        {
            // 定期更新UI状态
            if (isInitialized && Time.time - lastUIUpdateTime > uiUpdateInterval)
            {
                UpdateUIState();
                lastUIUpdateTime = Time.time;
            }
        }

        #region 验证方法

        /// <summary>
        /// 验证网络状态
        /// </summary>
        private bool ValidateNetworkStatus()
        {
            if (NetworkManager.Instance == null)
            {
                LogDebug("NetworkManager 实例不存在");
                return false;
            }

            if (!NetworkManager.Instance.IsConnected && !NetworkManager.Instance.IsHost)
            {
                LogDebug("网络未连接且不是Host");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 验证房间状态
        /// </summary>
        private bool ValidateRoomStatus()
        {
            if (RoomManager.Instance == null)
            {
                LogDebug("RoomManager 实例不存在");
                return false;
            }

            if (!RoomManager.Instance.IsInRoom)
            {
                LogDebug("未在房间中");
                return false;
            }

            return true;
        }

        #endregion

        #region UI状态管理

        /// <summary>
        /// 初始化UI状态
        /// </summary>
        private void InitializeUIState()
        {
            if (RoomManager.Instance?.CurrentRoom != null)
            {
                UpdateRoomInfo(RoomManager.Instance.CurrentRoom);
                UpdatePlayerList();
            }

            UpdateButtonStates();
        }

        /// <summary>
        /// 更新UI状态
        /// </summary>
        private void UpdateUIState()
        {
            if (!isInitialized || RoomManager.Instance?.CurrentRoom == null)
                return;

            UpdateRoomInfo(RoomManager.Instance.CurrentRoom);
            UpdateButtonStates();
        }

        /// <summary>
        /// 更新房间信息显示
        /// </summary>
        private void UpdateRoomInfo(RoomData roomData)
        {
            if (roomNameText != null)
                roomNameText.text = $"房间: {roomData.roomName}";

            if (roomCodeText != null)
                roomCodeText.text = $"房间代码: {roomData.roomCode}";

            if (playerCountText != null)
                playerCountText.text = $"玩家: {roomData.players.Count}/{roomData.maxPlayers}";

            if (roomStatusText != null)
            {
                string statusText = GetRoomStatusText(roomData);
                roomStatusText.text = statusText;
            }
        }

        /// <summary>
        /// 获取房间状态文本
        /// </summary>
        private string GetRoomStatusText(RoomData roomData)
        {
            switch (roomData.state)
            {
                case RoomState.Waiting:
                    // 显示准备状态，但排除房主
                    int readyCount = roomData.GetReadyPlayerCount();
                    int nonHostCount = roomData.GetNonHostPlayerCount();
                    if (nonHostCount == 0)
                        return "等待玩家加入";
                    else
                        return $"准备状态: {readyCount}/{nonHostCount}";
                case RoomState.Ready:
                    return "准备开始";
                case RoomState.Starting:
                    return "游戏启动中...";
                case RoomState.InGame:
                    return "游戏进行中";
                case RoomState.Ended:
                    return "游戏已结束";
                default:
                    return "未知状态";
            }
        }

        /// <summary>
        /// 更新按钮状态
        /// </summary>
        private void UpdateButtonStates()
        {
            bool isHost = RoomManager.Instance?.IsHost ?? false;
            bool canStartGame = RoomManager.Instance?.CanStartGame() ?? false;
            bool isReady = RoomManager.Instance?.GetMyReadyState() ?? false;

            // 准备按钮（只有客户端显示）
            if (readyButton != null)
            {
                readyButton.gameObject.SetActive(!isHost);
                if (!isHost)
                {
                    var buttonText = readyButton.GetComponentInChildren<TMP_Text>();
                    if (buttonText != null)
                        buttonText.text = isReady ? "取消准备" : "准备";
                }
            }

            // 开始游戏按钮（只有房主显示）
            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(isHost);
                if (isHost)
                {
                    startGameButton.interactable = canStartGame;
                    var buttonText = startGameButton.GetComponentInChildren<TMP_Text>();
                    if (buttonText != null)
                    {
                        var room = RoomManager.Instance?.CurrentRoom;
                        if (room == null)
                        {
                            buttonText.text = "等待房间数据";
                        }
                        else if (room.GetNonHostPlayerCount() == 0)
                        {
                            buttonText.text = "等待玩家加入";
                        }
                        else if (!room.AreAllPlayersReady())
                        {
                            int readyCount = room.GetReadyPlayerCount();
                            int totalNonHost = room.GetNonHostPlayerCount();
                            buttonText.text = $"等待准备 ({readyCount}/{totalNonHost})";
                        }
                        else
                        {
                            buttonText.text = "开始游戏";
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 更新玩家列表
        /// </summary>
        private void UpdatePlayerList()
        {
            if (RoomManager.Instance?.CurrentRoom == null)
                return;

            // 清空现有列表
            ClearPlayerList();

            // 重新添加玩家
            foreach (var player in RoomManager.Instance.CurrentRoom.players.Values)
            {
                AddPlayerToList(player);
            }
        }

        /// <summary>
        /// 添加玩家到列表
        /// </summary>
        private void AddPlayerToList(RoomPlayer player)
        {
            if (playerItemPrefab == null || playerListParent == null)
                return;

            if (playerListItems.ContainsKey(player.playerId))
                return;

            GameObject playerItem = Instantiate(playerItemPrefab, playerListParent);
            playerListItems[player.playerId] = playerItem;

            UpdatePlayerItemInfo(playerItem, player);
        }

        /// <summary>
        /// 移除玩家从列表
        /// </summary>
        private void RemovePlayerFromList(ushort playerId)
        {
            if (playerListItems.ContainsKey(playerId))
            {
                Destroy(playerListItems[playerId]);
                playerListItems.Remove(playerId);
            }
        }

        /// <summary>
        /// 清空玩家列表
        /// </summary>
        private void ClearPlayerList()
        {
            foreach (var item in playerListItems.Values)
            {
                if (item != null)
                    Destroy(item);
            }
            playerListItems.Clear();
        }

        /// <summary>
        /// 更新玩家项信息
        /// </summary>
        private void UpdatePlayerItemInfo(GameObject playerItem, RoomPlayer player)
        {
            var nameText = playerItem.GetComponentInChildren<TMP_Text>();
            if (nameText != null)
            {
                string statusText = "";
                if (player.isHost)
                    statusText = " (房主)";
                else if (player.state == PlayerRoomState.Ready)
                    statusText = " (已准备)";
                else
                    statusText = " (未准备)";

                nameText.text = player.playerName + statusText;

                // 设置颜色
                if (player.isHost)
                    nameText.color = Color.yellow;
                else if (player.state == PlayerRoomState.Ready)
                    nameText.color = Color.green;
                else
                    nameText.color = Color.white;
            }
        }

        #endregion

        #region UI显示控制

        /// <summary>
        /// 显示加载面板
        /// </summary>
        private void ShowLoadingPanel(string message)
        {
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(true);
                if (loadingText != null)
                    loadingText.text = message;
            }
        }

        /// <summary>
        /// 隐藏加载面板
        /// </summary>
        private void HideLoadingPanel()
        {
            if (loadingPanel != null)
                loadingPanel.SetActive(false);
        }

        /// <summary>
        /// 显示错误信息
        /// </summary>
        private void ShowError(string message)
        {
            LogDebug($"显示错误: {message}");

            HideLoadingPanel();

            if (errorPanel != null)
            {
                errorPanel.SetActive(true);
                if (errorText != null)
                    errorText.text = message;
            }
        }

        /// <summary>
        /// 隐藏错误面板
        /// </summary>
        private void HideErrorPanel()
        {
            if (errorPanel != null)
                errorPanel.SetActive(false);
        }

        #endregion

        #region 房间事件处理

        private void OnRoomCreated(RoomData roomData)
        {
            LogDebug("房间创建事件");
            UpdateRoomInfo(roomData);
            UpdatePlayerList();
        }

        private void OnRoomJoined(RoomData roomData)
        {
            LogDebug("房间加入事件");
            UpdateRoomInfo(roomData);
            UpdatePlayerList();
        }

        private void OnPlayerJoinedRoom(RoomPlayer player)
        {
            LogDebug($"玩家加入: {player.playerName}");
            AddPlayerToList(player);
        }

        private void OnPlayerLeftRoom(ushort playerId)
        {
            LogDebug($"玩家离开: {playerId}");
            RemovePlayerFromList(playerId);
        }

        private void OnPlayerReadyChanged(ushort playerId, bool isReady)
        {
            LogDebug($"玩家 {playerId} 准备状态: {isReady}");

            if (playerListItems.ContainsKey(playerId) &&
                RoomManager.Instance?.CurrentRoom?.players.ContainsKey(playerId) == true)
            {
                var player = RoomManager.Instance.CurrentRoom.players[playerId];
                UpdatePlayerItemInfo(playerListItems[playerId], player);
            }
        }

        private void OnGameStarting()
        {
            LogDebug("游戏开始");
            ShowLoadingPanel("游戏启动中，请稍候...");

            // 触发HostGameManager开始游戏（仅Host调用）
            if (RoomManager.Instance?.IsHost == true)
            {
                if (HostGameManager.Instance != null)
                {
                    LogDebug("触发HostGameManager开始游戏");
                    HostGameManager.Instance.StartGameFromRoom();
                }
                else
                {
                    LogDebug("HostGameManager实例不存在");
                }
            }

            // 延迟切换场景，给玩家一些反应时间
            Invoke(nameof(SwitchToGameScene), 2f);
        }

        private void OnRoomLeft()
        {
            LogDebug("离开房间");
            ReturnToMainMenu();
        }

        private void OnNetworkDisconnected()
        {
            LogDebug("网络断开连接");
            ShowError("网络连接断开，将返回主菜单");
            Invoke(nameof(ReturnToMainMenu), 3f);
        }

        #endregion

        #region 按钮事件处理

        /// <summary>
        /// 准备按钮点击
        /// </summary>
        private void OnReadyButtonClicked()
        {
            if (RoomManager.Instance == null || RoomManager.Instance.IsHost)
                return;

            bool currentReady = RoomManager.Instance.GetMyReadyState();
            bool newReady = !currentReady;

            LogDebug($"切换准备状态: {currentReady} -> {newReady}");
            RoomManager.Instance.SetPlayerReady(newReady);
        }

        /// <summary>
        /// 开始游戏按钮点击
        /// </summary>
        private void OnStartGameButtonClicked()
        {
            if (RoomManager.Instance == null || !RoomManager.Instance.IsHost)
                return;

            if (!RoomManager.Instance.CanStartGame())
            {
                ShowError("还有玩家未准备或人数不足");
                Invoke(nameof(HideErrorPanel), 2f);
                return;
            }

            LogDebug("房主启动游戏");
            RoomManager.Instance.StartGame();
        }

        /// <summary>
        /// 离开房间按钮点击
        /// </summary>
        private void OnLeaveRoomButtonClicked()
        {
            LogDebug("用户点击离开房间");

            if (RoomManager.Instance != null)
                RoomManager.Instance.LeaveRoom();

            ReturnToMainMenu();
        }

        /// <summary>
        /// 刷新按钮点击
        /// </summary>
        private void OnRefreshButtonClicked()
        {
            LogDebug("刷新房间状态");

            if (RoomManager.Instance?.CurrentRoom != null)
            {
                UpdateRoomInfo(RoomManager.Instance.CurrentRoom);
                UpdatePlayerList();
                UpdateButtonStates();
            }
        }

        #endregion

        #region 场景控制

        /// <summary>
        /// 切换到游戏场景
        /// </summary>
        private void SwitchToGameScene()
        {
            LogDebug("切换到游戏场景");
            SceneManager.LoadScene("NetworkGameScene");
        }

        /// <summary>
        /// 返回主菜单
        /// </summary>
        private void ReturnToMainMenu()
        {
            LogDebug("返回主菜单");

            // 清理网络连接
            if (NetworkManager.Instance != null)
                NetworkManager.Instance.Disconnect();

            SceneManager.LoadScene("MainMenuScene");
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
                Debug.Log($"[RoomSceneController] {message}");
            }
        }

        /// <summary>
        /// 获取房间详细信息（调试用）
        /// </summary>
        [ContextMenu("显示房间详细信息")]
        public void ShowRoomDetailedInfo()
        {
            if (RoomManager.Instance != null)
            {
                string info = RoomManager.Instance.GetDetailedDebugInfo();
                Debug.Log(info);
            }
        }

        #endregion

        #region Unity生命周期

        private void OnDestroy()
        {
            // 取消所有延迟调用
            CancelInvoke();

            // 取消事件订阅
            UnsubscribeFromRoomEvents();

            LogDebug("房间场景控制器销毁");
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && isInitialized)
            {
                LogDebug("应用暂停");
                // 这里可以添加暂停时的处理逻辑
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && isInitialized)
            {
                LogDebug("应用失去焦点");
                // 这里可以添加失去焦点时的处理逻辑
            }
        }

        #endregion
    }
}