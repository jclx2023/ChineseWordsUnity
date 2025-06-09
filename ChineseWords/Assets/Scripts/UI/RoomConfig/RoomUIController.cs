using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using Core.Network;
using Photon.Pun;
using Photon.Realtime;

namespace UI
{
    /// <summary>
    /// 房间界面控制器 - Photon优化版
    /// 专注于RoomScene的UI管理，使用纯事件驱动更新
    /// 移除自动刷新机制，完全依赖Photon的可靠事件同步
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
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button readyButton;
        [SerializeField] private TMP_Text readyButtonText;
        [SerializeField] private Button leaveRoomButton;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private Button debugRefreshButton;

        // 玩家UI缓存 - 使用Photon的ActorNumber作为键
        private Dictionary<int, GameObject> playerUIItems = new Dictionary<int, GameObject>();

        // 初始化状态
        private bool isInitialized = false;

        private void Start()
        {
            StartCoroutine(InitializeUIController());
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
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

            // 等待确保在房间中
            while (!PhotonNetwork.InRoom)
            {
                LogDebug("等待进入Photon房间...");
                yield return new WaitForSeconds(0.1f);
            }

            // 初始化UI组件
            InitializeUIComponents();

            // 订阅事件
            SubscribeToEvents();

            // 立即刷新一次UI
            RefreshAllUI();

            isInitialized = true;
            LogDebug("RoomUIController初始化完成");
        }

        /// <summary>
        /// 初始化UI组件
        /// </summary>
        private void InitializeUIComponents()
        {
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

            // 绑定离开房间按钮
            if (leaveRoomButton != null)
            {
                leaveRoomButton.onClick.RemoveAllListeners();
                leaveRoomButton.onClick.AddListener(OnLeaveRoomButtonClicked);
            }

            // 绑定调试刷新按钮
            if (debugRefreshButton != null)
            {
                debugRefreshButton.onClick.RemoveAllListeners();
                debugRefreshButton.onClick.AddListener(RefreshAllUI);
                debugRefreshButton.gameObject.SetActive(enableDebugLogs);
            }

            LogDebug("UI组件初始化完成");
        }

        /// <summary>
        /// 订阅RoomManager事件 - Photon版本
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
            else
            {
                Debug.LogError("[RoomUIController] RoomManager实例不存在");
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

                LogDebug("已取消订阅RoomManager事件");
            }
        }

        #endregion

        #region 事件处理 - Photon适配版

        /// <summary>
        /// 房间进入事件处理
        /// </summary>
        private void OnRoomEntered()
        {
            LogDebug("收到房间进入事件");
            RefreshAllUI();
        }

        /// <summary>
        /// 玩家加入房间事件处理 - 使用Photon Player对象
        /// </summary>
        private void OnPlayerJoinedRoom(Player player)
        {
            LogDebug($"玩家加入: {player.NickName} (ID: {player.ActorNumber})");
            CreateOrUpdatePlayerUI(player);
            RefreshRoomInfo();
            RefreshGameControls();
        }

        /// <summary>
        /// 玩家离开房间事件处理 - 使用Photon Player对象
        /// </summary>
        private void OnPlayerLeftRoom(Player player)
        {
            LogDebug($"玩家离开: {player.NickName} (ID: {player.ActorNumber})");
            RemovePlayerUI(player.ActorNumber);
            RefreshRoomInfo();
            RefreshGameControls();
        }

        /// <summary>
        /// 玩家准备状态变化事件处理 - 使用Photon Player对象
        /// </summary>
        private void OnPlayerReadyChanged(Player player, bool isReady)
        {
            LogDebug($"玩家准备状态变化: {player.NickName} -> {isReady}");
            UpdatePlayerReadyState(player);
            RefreshGameControls();
        }

        /// <summary>
        /// 所有玩家准备就绪事件处理
        /// </summary>
        private void OnAllPlayersReady()
        {
            LogDebug("所有玩家都已准备就绪");
            RefreshGameControls();

            // 可以在这里添加特效或提示
            ShowAllReadyNotification();
        }

        /// <summary>
        /// 游戏开始事件处理
        /// </summary>
        private void OnGameStarting()
        {
            LogDebug("游戏即将开始");
            RefreshGameControls();

            // 可以在这里添加游戏开始倒计时UI
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

        #endregion

        #region UI刷新方法 - 简化版

        /// <summary>
        /// 刷新所有UI - 简化的事件驱动版本
        /// </summary>
        public void RefreshAllUI()
        {
            if (!PhotonNetwork.InRoom)
            {
                LogDebug("不在房间中，清空UI");
                ClearAllUI();
                return;
            }

            LogDebug("刷新所有UI");
            RefreshRoomInfo();
            RefreshPlayerList();
            RefreshGameControls();
        }

        /// <summary>
        /// 刷新房间信息 - 直接使用Photon数据
        /// </summary>
        private void RefreshRoomInfo()
        {
            if (!PhotonNetwork.InRoom) return;

            var room = PhotonNetwork.CurrentRoom;

            // 更新房间名称
            if (roomNameText != null)
            {
                roomNameText.text = $"房间: {room.Name}";
            }

            // 更新房间代码（从房间属性获取）
            if (roomCodeText != null)
            {
                string roomCode = GetRoomCode();
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
                int totalPlayers = PhotonNetwork.PlayerList.Length;
                int maxPlayers = room.MaxPlayers;
                int readyCount = GetReadyPlayerCount();
                int nonHostCount = GetNonHostPlayerCount();

                playerCountText.text = $"玩家: {totalPlayers}/{maxPlayers} (准备: {readyCount}/{nonHostCount})";
            }

            LogDebug($"房间信息已更新: {room.Name}, 玩家数: {PhotonNetwork.PlayerList.Length}");
        }

        /// <summary>
        /// 刷新玩家列表 - 重建所有玩家UI
        /// </summary>
        private void RefreshPlayerList()
        {
            if (!PhotonNetwork.InRoom || playerListParent == null) return;

            LogDebug("刷新玩家列表");

            // 清空现有UI（简化方案）
            ClearPlayerUIItems();

            // 重新创建所有玩家UI
            foreach (var player in PhotonNetwork.PlayerList)
            {
                CreateOrUpdatePlayerUI(player);
            }

            LogDebug($"玩家列表刷新完成，当前玩家数: {PhotonNetwork.PlayerList.Length}");
        }

        /// <summary>
        /// 创建或更新玩家UI
        /// </summary>
        private void CreateOrUpdatePlayerUI(Player player)
        {
            if (playerItemPrefab == null || playerListParent == null) return;

            GameObject playerItem;

            // 检查是否已存在
            if (playerUIItems.ContainsKey(player.ActorNumber))
            {
                playerItem = playerUIItems[player.ActorNumber];
                if (playerItem == null)
                {
                    // UI对象被意外销毁，重新创建
                    playerUIItems.Remove(player.ActorNumber);
                    playerItem = CreatePlayerUIItem(player);
                }
            }
            else
            {
                playerItem = CreatePlayerUIItem(player);
            }

            // 更新UI内容
            if (playerItem != null)
            {
                UpdatePlayerUIContent(playerItem, player);
            }
        }

        /// <summary>
        /// 创建玩家UI项
        /// </summary>
        private GameObject CreatePlayerUIItem(Player player)
        {
            GameObject item = Instantiate(playerItemPrefab, playerListParent);
            item.name = $"Player_{player.ActorNumber}_{player.NickName}";
            playerUIItems[player.ActorNumber] = item;

            LogDebug($"创建玩家UI: {player.NickName} (ID: {player.ActorNumber})");
            return item;
        }

        /// <summary>
        /// 更新玩家UI内容
        /// </summary>
        private void UpdatePlayerUIContent(GameObject playerItem, Player player)
        {
            if (playerItem == null) return;

            // 更新玩家名称
            var nameText = playerItem.GetComponentInChildren<TMP_Text>();
            if (nameText != null)
            {
                string displayName = player.NickName;
                if (player.IsMasterClient)
                {
                    displayName += " (房主)";
                }
                nameText.text = displayName;
            }

            // 更新准备状态显示
            var statusTexts = playerItem.GetComponentsInChildren<TMP_Text>();
            TMP_Text statusText = null;

            // 查找状态文本（通常是第二个Text组件或名为StatusText的组件）
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
                bool isReady = GetPlayerReady(player);
                bool isHost = player.IsMasterClient;

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
                if (player.IsMasterClient)
                {
                    backgroundImage.color = new Color(1f, 0.8f, 0.2f, 0.3f); // 房主金色背景
                }
                else
                {
                    bool isReady = GetPlayerReady(player);
                    backgroundImage.color = isReady ?
                        new Color(0.2f, 1f, 0.2f, 0.2f) : // 准备：淡绿色
                        new Color(1f, 0.2f, 0.2f, 0.2f);  // 未准备：淡红色
                }
            }
        }

        /// <summary>
        /// 更新特定玩家的准备状态UI
        /// </summary>
        private void UpdatePlayerReadyState(Player player)
        {
            if (playerUIItems.ContainsKey(player.ActorNumber))
            {
                var playerItem = playerUIItems[player.ActorNumber];
                if (playerItem != null)
                {
                    UpdatePlayerUIContent(playerItem, player);
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
        /// 刷新游戏控制按钮
        /// </summary>
        private void RefreshGameControls()
        {
            if (!PhotonNetwork.InRoom) return;

            bool isHost = PhotonNetwork.IsMasterClient;
            bool canStartGame = RoomManager.Instance?.CanStartGame() ?? false;
            bool myReadyState = GetMyReadyState();

            // 更新开始游戏按钮
            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(isHost);
                startGameButton.interactable = canStartGame;

                var buttonText = startGameButton.GetComponentInChildren<TMP_Text>();
                if (buttonText != null)
                {
                    if (canStartGame)
                    {
                        buttonText.text = "开始游戏";
                    }
                    else
                    {
                        string condition = RoomManager.Instance?.GetGameStartConditions() ?? "检查中...";
                        buttonText.text = $"无法开始: {condition}";
                    }
                }
            }

            // 更新准备按钮
            if (readyButton != null)
            {
                readyButton.gameObject.SetActive(!isHost);
                readyButton.interactable = !GetGameStarted(); // 游戏开始后禁用

                if (readyButtonText != null)
                {
                    readyButtonText.text = myReadyState ? "取消准备" : "准备";
                }
            }

            // 更新离开房间按钮
            if (leaveRoomButton != null)
            {
                leaveRoomButton.interactable = !GetGameStarted(); // 游戏开始后可能禁用
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
            if (startGameButton != null) startGameButton.gameObject.SetActive(false);
            if (readyButton != null) readyButton.gameObject.SetActive(false);
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
        /// 准备按钮点击
        /// </summary>
        private void OnReadyButtonClicked()
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient)
            {
                LogDebug("房主不需要准备或未在房间中");
                return;
            }

            bool currentState = GetMyReadyState();
            bool newState = !currentState;

            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.SetPlayerReady(newState);
                LogDebug($"设置准备状态: {currentState} -> {newState}");
            }
        }

        /// <summary>
        /// 开始游戏按钮点击
        /// </summary>
        private void OnStartGameButtonClicked()
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                LogDebug("只有房主可以开始游戏");
                return;
            }

            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.StartGame();
                LogDebug("房主点击开始游戏");
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
            else
            {
                // 备用方案：直接离开Photon房间
                PhotonNetwork.LeaveRoom();
            }
        }

        #endregion

        #region Photon数据获取方法

        /// <summary>
        /// 获取房间代码
        /// </summary>
        private string GetRoomCode()
        {
            if (!PhotonNetwork.InRoom) return "";

            var room = PhotonNetwork.CurrentRoom;
            if (room.CustomProperties.TryGetValue("roomCode", out object code))
            {
                return (string)code;
            }
            return "";
        }

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

        /// <summary>
        /// 获取玩家准备状态
        /// </summary>
        private bool GetPlayerReady(Player player)
        {
            if (RoomManager.Instance != null)
            {
                return RoomManager.Instance.GetPlayerReady(player);
            }
            return false;
        }

        /// <summary>
        /// 获取本地玩家准备状态
        /// </summary>
        private bool GetMyReadyState()
        {
            if (RoomManager.Instance != null)
            {
                return RoomManager.Instance.GetMyReadyState();
            }
            return false;
        }

        /// <summary>
        /// 获取准备玩家数量
        /// </summary>
        private int GetReadyPlayerCount()
        {
            if (RoomManager.Instance != null)
            {
                return RoomManager.Instance.GetReadyPlayerCount();
            }
            return 0;
        }

        /// <summary>
        /// 获取非房主玩家数量
        /// </summary>
        private int GetNonHostPlayerCount()
        {
            if (RoomManager.Instance != null)
            {
                return RoomManager.Instance.GetNonHostPlayerCount();
            }
            return 0;
        }

        #endregion

        #region UI效果和通知

        /// <summary>
        /// 显示所有玩家准备就绪通知
        /// </summary>
        private void ShowAllReadyNotification()
        {
            // 可以在这里添加特效、音效或UI提示
            LogDebug("所有玩家准备就绪 - 可以添加特效");
        }

        /// <summary>
        /// 显示游戏开始通知
        /// </summary>
        private void ShowGameStartingNotification()
        {
            // 可以在这里添加倒计时UI或特效
            LogDebug("游戏即将开始 - 可以添加倒计时");
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

        #region 公共接口和调试

        /// <summary>
        /// 获取UI状态信息
        /// </summary>
        public string GetUIStatusInfo()
        {
            return $"初始化: {isInitialized}, " +
                   $"在房间中: {PhotonNetwork.InRoom}, " +
                   $"玩家UI数量: {playerUIItems.Count}, " +
                   $"房间名: {PhotonNetwork.CurrentRoom?.Name ?? "无"}";
        }

        /// <summary>
        /// 手动触发UI刷新（调试用）
        /// </summary>
        public void ManualRefresh()
        {
            LogDebug("手动触发UI刷新");
            RefreshAllUI();
        }

        #endregion

        #region 调试方法

        [ContextMenu("刷新UI")]
        public void DebugRefreshUI()
        {
            if (Application.isPlaying)
            {
                RefreshAllUI();
            }
        }

        [ContextMenu("显示UI状态")]
        public void DebugShowUIStatus()
        {
            if (Application.isPlaying)
            {
                Debug.Log($"=== RoomUIController状态 ===\n{GetUIStatusInfo()}");

                if (RoomManager.Instance != null)
                {
                    Debug.Log($"RoomManager状态: {RoomManager.Instance.GetRoomStatusInfo()}");
                }
            }
        }

        [ContextMenu("显示玩家列表")]
        public void DebugShowPlayerList()
        {
            if (Application.isPlaying && PhotonNetwork.InRoom)
            {
                Debug.Log("=== 当前玩家列表 ===");
                foreach (var player in PhotonNetwork.PlayerList)
                {
                    bool isReady = GetPlayerReady(player);
                    Debug.Log($"- {player.NickName} (ID: {player.ActorNumber}) " +
                             $"[{(player.IsMasterClient ? "房主" : "玩家")}] " +
                             $"[{(isReady ? "已准备" : "未准备")}]");
                }
            }
        }

        [ContextMenu("切换准备状态")]
        public void DebugToggleReady()
        {
            if (Application.isPlaying)
            {
                OnReadyButtonClicked();
            }
        }

        [ContextMenu("重新创建玩家列表")]
        public void DebugRecreatePlayerList()
        {
            if (Application.isPlaying)
            {
                LogDebug("重新创建玩家列表");
                RefreshPlayerList();
            }
        }

        #endregion
    }
}