using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using Core.Network;
using Core;
using Lobby.Core;
using Lobby.Data;
using Photon.Pun;
using Photon.Realtime;

namespace UI
{
    /// <summary>
    /// 房间场景控制器 - 修复版，支持房间属性监听
    /// 职责：场景初始化、数据传递、错误处理、场景切换协调、游戏开始信号监听
    /// 通过RoomManager和NetworkManager与网络层交互
    /// 修复：添加房间属性监听，确保所有玩家都能收到游戏开始信号
    /// </summary>
    public class RoomSceneController : MonoBehaviourPun, IConnectionCallbacks, IInRoomCallbacks
    {
        [Header("UI控制器引用")]
        [SerializeField] private RoomUIController roomUIController;

        [Header("状态显示")]
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private TMP_Text loadingText;
        [SerializeField] private GameObject errorPanel;
        [SerializeField] private TMP_Text errorText;

        [Header("初始化设置")]
        [SerializeField] private float initializationTimeout = 10f;
        [SerializeField] private float dataRecoveryTimeout = 5f;
        [SerializeField] private bool allowDirectRoomStart = false;

        [Header("设置")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool autoFindUIController = true;

        // 数据状态
        private LobbyPlayerData currentPlayerData;
        private LobbyRoomData currentRoomData;
        private bool hasValidData = false;

        // 状态管理
        private bool isInitialized = false;
        private bool isInitializing = false;
        private bool hasHandledGameStart = false;
        private bool isLeavingRoom = false;

        // 事件
        public static System.Action OnRoomSceneInitialized;
        public static System.Action<string> OnRoomSceneError;

        #region Unity生命周期

        private void Start()
        {
            StartCoroutine(InitializeRoomSceneCoroutine());
        }

        private void OnEnable()
        {
            PhotonNetwork.AddCallbackTarget(this);
        }

        private void OnDisable()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            LogDebug("房间场景控制器销毁");
        }

        #endregion

        #region 初始化流程

        /// <summary>
        /// 房间场景初始化协程
        /// </summary>
        private IEnumerator InitializeRoomSceneCoroutine()
        {
            if (isInitializing)
            {
                LogDebug("已在初始化中，跳过重复初始化");
                yield break;
            }

            isInitializing = true;
            LogDebug("=== 开始RoomScene初始化 ===");

            ShowLoadingPanel("正在初始化房间...");

            // 步骤1: 验证网络连接状态
            yield return StartCoroutine(ValidateNetworkConnectionCoroutine());

            // 步骤2: 获取场景数据
            yield return StartCoroutine(AcquireSceneDataCoroutine());

            // 步骤3: 等待核心组件准备就绪
            yield return StartCoroutine(WaitForCoreComponentsCoroutine());

            // 步骤4: 初始化UI组件
            InitializeUIComponents();

            // 步骤5: 订阅事件
            SubscribeToEvents();

            // 步骤6: 确保场景管理器存在
            EnsureSceneTransitionManager();

            // 完成初始化
            if (ValidateFinalState())
            {
                isInitialized = true;
                HideLoadingPanel();
                LogDebug("=== RoomScene初始化完成 ===");
                OnRoomSceneInitialized?.Invoke();
            }
            else
            {
                HandleInitializationFailure("最终状态验证失败");
            }

            isInitializing = false;
        }

        /// <summary>
        /// 验证网络连接状态协程
        /// </summary>
        private IEnumerator ValidateNetworkConnectionCoroutine()
        {
            LogDebug("验证网络连接状态...");
            ShowLoadingPanel("验证网络连接...");

            float elapsed = 0f;
            while (elapsed < initializationTimeout)
            {
                if (NetworkManager.Instance?.IsConnected == true)
                {
                    LogDebug($" 网络连接正常 - 房间: {NetworkManager.Instance.RoomName}");
                    yield break;
                }

                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (!allowDirectRoomStart)
            {
                Debug.LogError("[RoomSceneController] 网络连接验证失败");
                HandleInitializationFailure("网络连接验证失败");
                yield break;
            }
            else
            {
                LogDebug(" 网络连接失败，但允许直接启动（调试模式）");
            }
        }

        /// <summary>
        /// 获取场景数据协程
        /// </summary>
        private IEnumerator AcquireSceneDataCoroutine()
        {
            LogDebug("获取场景数据...");
            ShowLoadingPanel("获取房间数据...");

            // 尝试从LobbySceneManager获取数据
            if (TryGetDataFromLobbyScene())
            {
                hasValidData = true;
                LogDebug("✓ 从LobbySceneManager获取数据成功");
                yield break;
            }

            // 尝试从NetworkManager恢复数据
            if (TryRecoverDataFromNetwork())
            {
                hasValidData = true;
                LogDebug("✓ 从NetworkManager恢复数据成功");
                yield break;
            }

            // 等待数据恢复
            LogDebug("等待数据恢复...");
            ShowLoadingPanel("尝试恢复数据...");

            float elapsed = 0f;
            while (elapsed < dataRecoveryTimeout && !hasValidData)
            {
                if (TryGetDataFromLobbyScene() || TryRecoverDataFromNetwork())
                {
                    hasValidData = true;
                    LogDebug("✓ 延迟数据恢复成功");
                    yield break;
                }

                yield return new WaitForSeconds(0.2f);
                elapsed += 0.2f;
            }
        }

        /// <summary>
        /// 等待核心组件准备就绪协程
        /// </summary>
        private IEnumerator WaitForCoreComponentsCoroutine()
        {
            LogDebug("等待核心组件准备就绪...");
            ShowLoadingPanel("等待房间管理器...");

            // 等待NetworkManager
            float elapsed = 0f;
            while (NetworkManager.Instance == null && elapsed < initializationTimeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (NetworkManager.Instance == null)
            {
                Debug.LogError("[RoomSceneController] NetworkManager未找到");
                HandleInitializationFailure("网络管理器未找到");
                yield break;
            }

            LogDebug("✓ NetworkManager已找到");

            // 等待RoomManager
            elapsed = 0f;
            while (RoomManager.Instance == null && elapsed < initializationTimeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (RoomManager.Instance == null)
            {
                Debug.LogError("[RoomSceneController] RoomManager未找到");
                HandleInitializationFailure("房间管理器未找到");
                yield break;
            }

            LogDebug("✓ RoomManager已找到");

            // 等待RoomManager初始化
            ShowLoadingPanel("等待房间管理器初始化...");
            elapsed = 0f;
            while (!RoomManager.Instance.IsInitialized && elapsed < initializationTimeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (!RoomManager.Instance.IsInitialized)
            {
                Debug.LogError("[RoomSceneController] RoomManager初始化超时");
                HandleInitializationFailure("房间管理器初始化超时");
                yield break;
            }

            LogDebug("✓ RoomManager初始化完成");

            // 查找RoomUIController
            if (autoFindUIController && roomUIController == null)
            {
                roomUIController = FindObjectOfType<RoomUIController>();
            }

            if (roomUIController != null)
            {
                LogDebug("✓ RoomUIController已找到");
            }
            else
            {
                Debug.LogWarning("[RoomSceneController] RoomUIController未找到，UI功能可能受限");
            }
        }

        /// <summary>
        /// 初始化UI组件
        /// </summary>
        private void InitializeUIComponents()
        {
            LogDebug("初始化UI组件...");
            LogDebug("✓ UI组件初始化完成");
        }

        #endregion

        #region 数据获取和恢复

        /// <summary>
        /// 尝试从LobbySceneManager获取数据
        /// </summary>
        private bool TryGetDataFromLobbyScene()
        {
            if (LobbySceneManager.Instance == null) return false;

            currentPlayerData = LobbySceneManager.Instance.GetCurrentPlayerData();
            if (currentPlayerData == null) return false;

            currentRoomData = LobbySceneManager.Instance.GetLastJoinedRoomData();
            if (currentRoomData == null) return false;

            LogDebug($"从LobbySceneManager获取数据 - 玩家: {currentPlayerData.playerName}, 房间: {currentRoomData.roomName}");
            return true;
        }

        /// <summary>
        /// 尝试从NetworkManager恢复数据
        /// </summary>
        private bool TryRecoverDataFromNetwork()
        {
            if (NetworkManager.Instance?.IsConnected != true) return false;

            LogDebug("尝试从NetworkManager恢复数据...");

            // 恢复房间数据
            currentRoomData = new LobbyRoomData
            {
                roomName = NetworkManager.Instance.RoomName,
                currentPlayers = NetworkManager.Instance.PlayerCount,
                maxPlayers = NetworkManager.Instance.MaxPlayers
            };

            // 恢复玩家数据
            currentPlayerData = new LobbyPlayerData
            {
                playerName = NetworkManager.Instance.GetPlayerName(NetworkManager.Instance.ClientId),
                playerId = NetworkManager.Instance.ClientId.ToString()
            };

            LogDebug($"从NetworkManager恢复数据 - 玩家: {currentPlayerData.playerName}, 房间: {currentRoomData.roomName}");
            return true;
        }

        #endregion

        #region 验证和辅助方法

        /// <summary>
        /// 验证最终状态
        /// </summary>
        private bool ValidateFinalState()
        {
            if (!hasValidData)
            {
                Debug.LogError("[RoomSceneController] 数据状态无效");
                return false;
            }

            if (RoomManager.Instance == null || !RoomManager.Instance.IsInitialized)
            {
                Debug.LogError("[RoomSceneController] RoomManager状态无效");
                return false;
            }

            if (!allowDirectRoomStart && NetworkManager.Instance?.IsConnected != true)
            {
                Debug.LogError("[RoomSceneController] 网络连接状态无效");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 确保SceneTransitionManager存在
        /// </summary>
        private void EnsureSceneTransitionManager()
        {
            if (SceneTransitionManager.Instance == null)
            {
                LogDebug("SceneTransitionManager不存在，尝试创建");
                GameObject stmPrefab = Resources.Load<GameObject>("SceneTransitionManager");
                if (stmPrefab != null)
                {
                    Instantiate(stmPrefab);
                    LogDebug("从Resources创建了SceneTransitionManager");
                }
                else
                {
                    GameObject stmObject = new GameObject("SceneTransitionManager");
                    stmObject.AddComponent<SceneTransitionManager>();
                    LogDebug("手动创建了SceneTransitionManager");
                }
            }
            else
            {
                LogDebug("✓ SceneTransitionManager已存在");
            }
        }

        /// <summary>
        /// 处理初始化失败
        /// </summary>
        private void HandleInitializationFailure(string reason)
        {
            Debug.LogError($"[RoomSceneController] 初始化失败: {reason}");
            ShowError($"房间初始化失败: {reason}");
            OnRoomSceneError?.Invoke(reason);
            Invoke(nameof(ReturnToLobbyDelayed), 3f);
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

        #region 事件管理

        /// <summary>
        /// 订阅事件
        /// </summary>
        private void SubscribeToEvents()
        {
            LogDebug("订阅房间和场景事件...");

            // 订阅RoomManager事件
            RoomManager.OnGameStarting += OnGameStarting;
            RoomManager.OnReturnToLobby += OnReturnToLobby;

            // 订阅SceneTransitionManager事件
            SceneTransitionManager.OnSceneTransitionStarted += OnSceneTransitionStarted;

            LogDebug(" 事件订阅完成");
        }

        /// <summary>
        /// 取消订阅事件
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            LogDebug("取消订阅事件...");

            RoomManager.OnGameStarting -= OnGameStarting;
            RoomManager.OnReturnToLobby -= OnReturnToLobby;
            SceneTransitionManager.OnSceneTransitionStarted -= OnSceneTransitionStarted;

            LogDebug(" 事件取消订阅完成");
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 游戏开始事件处理（房主端）
        /// </summary>
        private void OnGameStarting()
        {
            if (hasHandledGameStart)
            {
                LogDebug("游戏开始事件已处理，忽略重复调用");
                return;
            }

            hasHandledGameStart = true;
            LogDebug("收到游戏开始事件（房主端） - 开始场景切换流程");

            ShowLoadingPanel("游戏启动中，请稍候...");

            bool switchSuccess = SceneTransitionManager.SwitchToGameScene("RoomSceneController");

            if (!switchSuccess)
            {
                LogDebug("场景切换请求失败，可能已在切换中");
                hasHandledGameStart = false;
                HideLoadingPanel();
            }
        }

        /// <summary>
        /// 返回大厅事件处理
        /// </summary>
        private void OnReturnToLobby()
        {
            LogDebug("收到返回大厅事件");
            ReturnToLobbyDelayed();
        }

        /// <summary>
        /// 场景切换开始事件
        /// </summary>
        private void OnSceneTransitionStarted(string sceneName)
        {
            LogDebug($"场景切换开始: {sceneName}");
            ShowLoadingPanel($"正在切换到 {sceneName}...");
        }

        #endregion

        #region IInRoomCallbacks实现

        void IInRoomCallbacks.OnPlayerEnteredRoom(Player newPlayer)
        {
            LogDebug($"Photon: 玩家加入房间 - {newPlayer.NickName} (ID: {newPlayer.ActorNumber})");
        }

        void IInRoomCallbacks.OnPlayerLeftRoom(Player otherPlayer)
        {
            LogDebug($"Photon: 玩家离开房间 - {otherPlayer.NickName} (ID: {otherPlayer.ActorNumber})");
        }

        void IInRoomCallbacks.OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
        {
            LogDebug($"玩家属性更新: {targetPlayer.NickName}");
        }

        void IInRoomCallbacks.OnMasterClientSwitched(Player newMasterClient)
        {
            LogDebug($"Photon: 房主切换到 {newMasterClient.NickName} (ID: {newMasterClient.ActorNumber})");
        }

        void IInRoomCallbacks.OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
        {
            LogDebug($" 房间属性更新: {string.Join(", ", propertiesThatChanged.Keys)}");

            // 关键修复：检查游戏开始信号
            if (ShouldTriggerGameStart(propertiesThatChanged))
            {
                LogDebug("检测到游戏开始信号");
                HandleGameStartSignal();
            }

            // 输出所有变化的属性（调试用）
            foreach (var prop in propertiesThatChanged)
            {
                LogDebug($"  - {prop.Key} = {prop.Value}");

                // 特别关注游戏状态相关的属性
                if (prop.Key.ToString().Contains("game") || prop.Key.ToString().Contains("state"))
                {
                    LogDebug($"游戏状态相关属性: {prop.Key} = {prop.Value}");
                }
            }
        }

        #endregion

        #region 游戏开始信号处理

        /// <summary>
        /// 检查是否应该触发游戏开始
        /// </summary>
        private bool ShouldTriggerGameStart(ExitGames.Client.Photon.Hashtable changedProps)
        {
            // 检查gameStarted属性
            if (changedProps.ContainsKey("gameStarted"))
            {
                bool gameStarted = (bool)changedProps["gameStarted"];
                if (gameStarted)
                {
                    LogDebug("检测到gameStarted=true");
                    return true;
                }
            }

            // 检查roomState属性
            if (changedProps.ContainsKey("roomState"))
            {
                int roomState = (int)changedProps["roomState"];
                if (roomState == 1) // Starting状态
                {
                    LogDebug("检测到roomState=Starting");
                    return true;
                }
            }

            // 检查GAME_STARTED_KEY（如果使用了这个键）
            if (changedProps.ContainsKey("GAME_STARTED_KEY"))
            {
                bool gameStarted = (bool)changedProps["GAME_STARTED_KEY"];
                if (gameStarted)
                {
                    LogDebug("检测到GAME_STARTED_KEY=true");
                    return true;
                }
            }

            // 检查ROOM_STATE_KEY（如果使用了这个键）
            if (changedProps.ContainsKey("ROOM_STATE_KEY"))
            {
                int roomState = (int)changedProps["ROOM_STATE_KEY"];
                if (roomState == 1) // Starting状态
                {
                    LogDebug("检测到ROOM_STATE_KEY=Starting");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 处理游戏开始信号
        /// </summary>
        private void HandleGameStartSignal()
        {
            // 如果是房主，由OnGameStarting事件处理，这里不重复处理
            if (PhotonNetwork.IsMasterClient)
            {
                LogDebug("🎯 房主端游戏开始由OnGameStarting事件处理，跳过房间属性处理");
                return;
            }

            // 防止重复处理
            if (hasHandledGameStart)
            {
                LogDebug(" 游戏开始已处理，忽略重复信号");
                return;
            }

            // 非房主玩家处理游戏开始
            LogDebug(" 非房主玩家处理游戏开始信号");
            TriggerGameStartForClient();
        }

        /// <summary>
        /// 为客户端触发游戏开始（非房主）
        /// </summary>
        private void TriggerGameStartForClient()
        {
            hasHandledGameStart = true;
            LogDebug(" 客户端收到游戏开始信号 - 开始场景切换流程");

            ShowLoadingPanel("游戏启动中，请稍候...");

            // 使用SceneTransitionManager执行场景切换
            bool switchSuccess = SceneTransitionManager.SwitchToGameScene("RoomSceneController");

            if (!switchSuccess)
            {
                LogDebug(" 场景切换请求失败，可能已在切换中");
                // 如果切换失败，重置处理标志
                hasHandledGameStart = false;
                HideLoadingPanel();
            }
        }

        #endregion

        #region 按钮事件处理

        /// <summary>
        /// 准备按钮点击
        /// </summary>
        private void OnReadyButtonClicked()
        {
            if (!isInitialized || RoomManager.Instance == null || RoomManager.Instance.IsHost)
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
            if (!isInitialized || RoomManager.Instance == null || !RoomManager.Instance.IsHost)
                return;

            if (!RoomManager.Instance.CanStartGame())
            {
                string conditions = RoomManager.Instance.GetGameStartConditions();
                ShowError($"无法开始游戏: {conditions}");
                Invoke(nameof(HideErrorPanel), 3f);
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
            isLeavingRoom = true;

            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.LeaveRoomAndReturnToLobby();
            }
            else
            {
                ReturnToLobbyDelayed();
            }
        }

        #endregion

        #region IConnectionCallbacks实现

        void IConnectionCallbacks.OnConnected() { }
        void IConnectionCallbacks.OnConnectedToMaster() { }

        void IConnectionCallbacks.OnDisconnected(DisconnectCause cause)
        {
            LogDebug($"Photon: 连接断开 - {cause}");

            if (isInitialized && !isLeavingRoom)
            {
                HandleDisconnection($"网络断开: {cause}");
            }
        }

        void IConnectionCallbacks.OnRegionListReceived(RegionHandler regionHandler) { }
        void IConnectionCallbacks.OnCustomAuthenticationResponse(System.Collections.Generic.Dictionary<string, object> data) { }
        void IConnectionCallbacks.OnCustomAuthenticationFailed(string debugMessage) { }

        #endregion

        #region 连接状态监控

        /// <summary>
        /// 监控网络连接状态
        /// </summary>
        private void Update()
        {
            if (!isInitialized || allowDirectRoomStart) return;

            // 检查网络连接状态
            if (NetworkManager.Instance?.IsConnected != true && !isLeavingRoom)
            {
                HandleDisconnection("连接丢失");
            }
        }

        /// <summary>
        /// 处理连接断开
        /// </summary>
        private void HandleDisconnection(string reason)
        {
            if (isLeavingRoom) return;

            //LogDebug($"检测到连接断开: {reason}");
            ShowError($"网络连接断开: {reason}");
            Invoke(nameof(ReturnToLobbyDelayed), 3f);
        }

        #endregion

        #region 场景控制

        /// <summary>
        /// 延迟返回大厅
        /// </summary>
        private void ReturnToLobbyDelayed()
        {
            LogDebug("执行返回大厅");
            isLeavingRoom = true;

            bool switchSuccess = SceneTransitionManager.ReturnToMainMenu("RoomSceneController");

            if (!switchSuccess)
            {
                Debug.LogWarning("[RoomSceneController] SceneTransitionManager返回失败，使用备用方案");
                SceneManager.LoadScene("LobbyScene");
            }
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

        #endregion
    }
}