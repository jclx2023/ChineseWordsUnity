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
    /// 房间场景控制器 - 完善版
    /// 集成场景管理、数据获取、UI控制的完整功能
    /// 适配LobbySceneManager数据传递和完整的初始化流程
    /// </summary>
    public class RoomSceneController : MonoBehaviourPun, IInRoomCallbacks, IConnectionCallbacks
    {
        [Header("UI控制器引用")]
        [SerializeField] private RoomUIController roomUIController;

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

        [Header("初始化设置")]
        [SerializeField] private float initializationTimeout = 10f;
        [SerializeField] private float dataRecoveryTimeout = 5f;
        [SerializeField] private bool allowDirectRoomStart = false; // 允许直接启动RoomScene（调试用）

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
        private bool hasSubscribedToEvents = false;

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
            // 注册Photon回调
            PhotonNetwork.AddCallbackTarget(this);
        }

        private void OnDisable()
        {
            // 取消注册Photon回调
            PhotonNetwork.RemoveCallbackTarget(this);
        }

        private void OnDestroy()
        {
            // 取消所有延迟调用
            CancelInvoke();

            // 取消事件订阅
            UnsubscribeFromRoomEvents();

            LogDebug("房间场景控制器销毁");
        }

        #endregion

        #region 完整的初始化流程

        /// <summary>
        /// 房间场景初始化协程 - 完整版
        /// </summary>
        private IEnumerator InitializeRoomSceneCoroutine()
        {
            if (isInitializing)
            {
                LogDebug("已在初始化中，跳过重复初始化");
                yield break;
            }

            isInitializing = true;
            LogDebug("=== 开始RoomScene完整初始化 ===");

            // 显示加载界面
            ShowLoadingPanel("正在初始化房间...");

            // 步骤1: 验证Photon连接状态
            yield return StartCoroutine(ValidatePhotonConnectionCoroutine());

            // 步骤2: 获取场景数据
            yield return StartCoroutine(AcquireSceneDataCoroutine());

            // 步骤3: 等待核心组件准备就绪
            yield return StartCoroutine(WaitForCoreComponentsCoroutine());

            // 步骤4: 初始化UI组件
            InitializeUIComponents();

            // 步骤5: 订阅事件
            SubscribeToRoomEvents();

            // 步骤6: 确保SceneTransitionManager存在
            EnsureSceneTransitionManager();

            // 步骤7: 最终验证和完成
            if (ValidateFinalState())
            {
                isInitialized = true;
                HideLoadingPanel();
                LogDebug("=== RoomScene初始化完成 ===");
                OnRoomSceneInitialized?.Invoke();
            }
            else
            {
                Debug.LogError("[RoomSceneController] 初始化最终验证失败");
                HandleInitializationFailure("最终状态验证失败");
            }

            isInitializing = false;
        }

        /// <summary>
        /// 验证Photon连接状态协程
        /// </summary>
        private IEnumerator ValidatePhotonConnectionCoroutine()
        {
            LogDebug("验证Photon连接状态...");
            ShowLoadingPanel("验证网络连接...");

            float elapsed = 0f;
            while (elapsed < initializationTimeout)
            {
                if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
                {
                    LogDebug($"✓ Photon连接正常 - 房间: {PhotonNetwork.CurrentRoom.Name}");
                    yield break;
                }

                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            // Photon连接验证失败
            if (!allowDirectRoomStart)
            {
                Debug.LogError("[RoomSceneController] Photon连接验证失败 - 不在房间中");
                HandleInitializationFailure("网络连接验证失败");
                yield break;
            }
            else
            {
                LogDebug("⚠ Photon连接失败，但允许直接启动（调试模式）");
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

            // 尝试从Photon恢复数据
            if (PhotonNetwork.InRoom && TryRecoverDataFromPhoton())
            {
                hasValidData = true;
                LogDebug("✓ 从Photon恢复数据成功");
                yield break;
            }

            // 等待一段时间看是否能恢复数据
            LogDebug("等待数据恢复...");
            ShowLoadingPanel("尝试恢复数据...");

            float elapsed = 0f;
            while (elapsed < dataRecoveryTimeout && !hasValidData)
            {
                if (TryGetDataFromLobbyScene() || (PhotonNetwork.InRoom && TryRecoverDataFromPhoton()))
                {
                    hasValidData = true;
                    LogDebug("✓ 延迟数据恢复成功");
                    yield break;
                }

                yield return new WaitForSeconds(0.2f);
                elapsed += 0.2f;
            }

            // 数据获取失败处理
            if (!hasValidData)
            {
                if (allowDirectRoomStart)
                {
                    CreateFallbackData();
                    hasValidData = true;
                    LogDebug("⚠ 使用备用数据（调试模式）");
                }
                else
                {
                    Debug.LogError("[RoomSceneController] 无法获取场景数据");
                    HandleInitializationFailure("数据获取失败");
                }
            }
        }

        /// <summary>
        /// 等待核心组件准备就绪协程
        /// </summary>
        private IEnumerator WaitForCoreComponentsCoroutine()
        {
            LogDebug("等待核心组件准备就绪...");
            ShowLoadingPanel("等待房间管理器...");

            // 等待RoomManager
            float elapsed = 0f;
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

            // 绑定UI事件
            BindUIEvents();

            // 向RoomUIController提供数据（如果存在）
            if (roomUIController != null && hasValidData)
            {
                LogDebug("UI控制器将通过事件系统自动更新");
            }

            LogDebug("✓ UI组件初始化完成");
        }

        #endregion

        #region 数据获取和恢复

        /// <summary>
        /// 尝试从LobbySceneManager获取数据
        /// </summary>
        private bool TryGetDataFromLobbyScene()
        {
            if (LobbySceneManager.Instance == null)
            {
                LogDebug("LobbySceneManager实例不存在");
                return false;
            }

            // 获取玩家数据
            currentPlayerData = LobbySceneManager.Instance.GetCurrentPlayerData();
            if (currentPlayerData == null)
            {
                LogDebug("无法从LobbySceneManager获取玩家数据");
                return false;
            }

            // 获取房间数据
            currentRoomData = LobbySceneManager.Instance.GetLastJoinedRoomData();
            if (currentRoomData == null)
            {
                LogDebug("无法从LobbySceneManager获取房间数据");
                return false;
            }

            LogDebug($"从LobbySceneManager获取数据 - 玩家: {currentPlayerData.playerName}, 房间: {currentRoomData.roomName}");
            return true;
        }

        /// <summary>
        /// 尝试从Photon恢复数据
        /// </summary>
        private bool TryRecoverDataFromPhoton()
        {
            if (!PhotonNetwork.InRoom)
            {
                LogDebug("不在Photon房间中，无法恢复数据");
                return false;
            }

            LogDebug("尝试从Photon恢复数据...");

            // 恢复房间数据
            var photonRoom = PhotonNetwork.CurrentRoom;
            currentRoomData = LobbyRoomData.FromPhotonRoom(photonRoom);
            if (currentRoomData == null)
            {
                LogDebug("从Photon房间恢复数据失败");
                return false;
            }

            // 恢复玩家数据
            var localPlayer = PhotonNetwork.LocalPlayer;
            currentPlayerData = new LobbyPlayerData();

            if (localPlayer.CustomProperties.TryGetValue("playerName", out object playerName))
            {
                currentPlayerData.playerName = (string)playerName;
            }
            else
            {
                currentPlayerData.playerName = localPlayer.NickName ?? "恢复的玩家";
            }

            if (localPlayer.CustomProperties.TryGetValue("playerId", out object playerId))
            {
                currentPlayerData.playerId = (string)playerId;
            }
            else
            {
                currentPlayerData.playerId = System.Guid.NewGuid().ToString();
            }

            LogDebug($"从Photon恢复数据 - 玩家: {currentPlayerData.playerName}, 房间: {currentRoomData.roomName}");
            return true;
        }

        /// <summary>
        /// 创建备用数据（调试模式）
        /// </summary>
        private void CreateFallbackData()
        {
            LogDebug("创建备用数据（调试模式）");

            // 创建备用玩家数据
            currentPlayerData = new LobbyPlayerData();
            currentPlayerData.playerName = "调试玩家";
            currentPlayerData.playerId = System.Guid.NewGuid().ToString();

            // 创建备用房间数据
            currentRoomData = new LobbyRoomData();
            currentRoomData.roomName = "调试房间";
            currentRoomData.roomId = System.Guid.NewGuid().ToString();
            currentRoomData.hostPlayerName = currentPlayerData.playerName;
            currentRoomData.maxPlayers = 4;
            currentRoomData.currentPlayers = 1;
            currentRoomData.Initialize();

            LogDebug("备用数据创建完成");
        }

        #endregion

        #region 验证和辅助方法

        /// <summary>
        /// 验证房间状态 - 完善版
        /// </summary>
        private bool ValidateRoomStatus()
        {
            // 验证Photon状态（如果不是调试模式）
            if (!allowDirectRoomStart)
            {
                if (!PhotonNetwork.InRoom)
                {
                    LogDebug("未在Photon房间中");
                    return false;
                }
            }

            // 验证RoomManager
            if (RoomManager.Instance == null)
            {
                LogDebug("RoomManager实例不存在");
                return false;
            }

            if (!RoomManager.Instance.IsInitialized)
            {
                LogDebug("RoomManager未初始化");
                return false;
            }

            // 验证数据
            if (!hasValidData)
            {
                LogDebug("缺少有效的场景数据");
                return false;
            }

            LogDebug($"房间状态验证通过 - 房间: {RoomManager.Instance.RoomName}, 玩家数: {RoomManager.Instance.PlayerCount}");
            return true;
        }

        /// <summary>
        /// 验证最终状态
        /// </summary>
        private bool ValidateFinalState()
        {
            bool isValid = true;

            // 验证数据状态
            if (!hasValidData)
            {
                Debug.LogError("[RoomSceneController] 数据状态无效");
                isValid = false;
            }

            // 验证核心组件
            if (RoomManager.Instance == null || !RoomManager.Instance.IsInitialized)
            {
                Debug.LogError("[RoomSceneController] RoomManager状态无效");
                isValid = false;
            }

            // 验证Photon状态（如果不是调试模式）
            if (!allowDirectRoomStart && (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom))
            {
                Debug.LogError("[RoomSceneController] Photon连接状态无效");
                isValid = false;
            }

            return isValid;
        }

        /// <summary>
        /// 确保SceneTransitionManager存在
        /// </summary>
        private void EnsureSceneTransitionManager()
        {
            if (SceneTransitionManager.Instance == null)
            {
                LogDebug("SceneTransitionManager不存在，尝试创建");

                // 尝试从Resources加载
                GameObject stmPrefab = Resources.Load<GameObject>("SceneTransitionManager");
                if (stmPrefab != null)
                {
                    Instantiate(stmPrefab);
                    LogDebug("从Resources创建了SceneTransitionManager");
                }
                else
                {
                    // 手动创建
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

            // 延迟返回大厅
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

            LogDebug("✓ UI事件绑定完成");
        }

        /// <summary>
        /// 订阅房间事件 - 完善版
        /// </summary>
        private void SubscribeToRoomEvents()
        {
            if (hasSubscribedToEvents)
            {
                LogDebug("已订阅事件，跳过重复订阅");
                return;
            }

            LogDebug("订阅房间和场景事件...");

            // 订阅RoomManager事件（场景控制相关）
            RoomManager.OnGameStarting += OnGameStarting;
            RoomManager.OnReturnToLobby += OnReturnToLobby;

            // 订阅SceneTransitionManager事件
            SceneTransitionManager.OnSceneTransitionStarted += OnSceneTransitionStarted;

            hasSubscribedToEvents = true;
            LogDebug("✓ 事件订阅完成");
        }

        /// <summary>
        /// 取消订阅房间事件
        /// </summary>
        private void UnsubscribeFromRoomEvents()
        {
            if (!hasSubscribedToEvents) return;

            LogDebug("取消订阅事件...");

            RoomManager.OnGameStarting -= OnGameStarting;
            RoomManager.OnReturnToLobby -= OnReturnToLobby;
            SceneTransitionManager.OnSceneTransitionStarted -= OnSceneTransitionStarted;

            hasSubscribedToEvents = false;
            LogDebug("✓ 事件取消订阅完成");
        }

        #endregion

        #region 房间事件处理

        /// <summary>
        /// 游戏开始事件处理 - 完善版
        /// </summary>
        private void OnGameStarting()
        {
            // 防止重复处理
            if (hasHandledGameStart)
            {
                LogDebug("游戏开始事件已处理，忽略重复调用");
                return;
            }

            hasHandledGameStart = true;
            LogDebug("收到游戏开始事件 - 开始场景切换流程");

            ShowLoadingPanel("游戏启动中，请稍候...");

            // 使用SceneTransitionManager执行场景切换
            bool switchSuccess = SceneTransitionManager.SwitchToGameScene("RoomSceneController");

            if (!switchSuccess)
            {
                LogDebug("场景切换请求失败，可能已在切换中");
                // 如果切换失败，重置处理标志
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

            // 直接调用RoomManager的StartGame方法
            RoomManager.Instance.StartGame();
        }

        /// <summary>
        /// 离开房间按钮点击
        /// </summary>
        private void OnLeaveRoomButtonClicked()
        {
            LogDebug("用户点击离开房间");

            // 标记为主动离开
            isLeavingRoom = true;

            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.LeaveRoomAndReturnToLobby();
            }
            else
            {
                // 备用方案：直接使用SceneTransitionManager
                ReturnToLobbyDelayed();
            }
        }

        /// <summary>
        /// 刷新按钮点击
        /// </summary>
        private void OnRefreshButtonClicked()
        {
            LogDebug("手动刷新请求");

            // 委托给RoomUIController处理UI刷新
            if (roomUIController != null)
            {
                roomUIController.RefreshAllUI();
                LogDebug("已请求RoomUIController强制刷新");
            }
            else
            {
                LogDebug("RoomUIController不存在，无法执行刷新");
            }
        }

        #endregion

        #region Photon回调实现

        // IInRoomCallbacks实现
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
            // 准备状态变更等由RoomManager和RoomUIController处理
        }

        void IInRoomCallbacks.OnMasterClientSwitched(Player newMasterClient)
        {
            LogDebug($"Photon: 房主切换到 {newMasterClient.NickName} (ID: {newMasterClient.ActorNumber})");

            // 强制刷新UI以反映新的房主状态
            if (roomUIController != null)
            {
                roomUIController.RefreshAllUI();
            }
        }

        void IInRoomCallbacks.OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
        {
            // 房间属性变更由RoomManager处理
        }

        // IConnectionCallbacks实现
        void IConnectionCallbacks.OnConnected()
        {
            LogDebug("Photon: 连接到服务器");
        }

        void IConnectionCallbacks.OnConnectedToMaster()
        {
            LogDebug("Photon: 连接到主服务器");
        }

        void IConnectionCallbacks.OnDisconnected(DisconnectCause cause)
        {
            LogDebug($"Photon: 连接断开 - {cause}");

            // 网络断开时的处理
            if (isInitialized && !isLeavingRoom)
            {
                HandleDisconnection($"网络断开: {cause}");
            }
        }

        void IConnectionCallbacks.OnRegionListReceived(RegionHandler regionHandler) { }
        void IConnectionCallbacks.OnCustomAuthenticationResponse(System.Collections.Generic.Dictionary<string, object> data) { }
        void IConnectionCallbacks.OnCustomAuthenticationFailed(string debugMessage) { }

        #endregion

        #region Photon连接状态监控

        /// <summary>
        /// 监控Photon连接状态（通过Update检查）
        /// </summary>
        private void Update()
        {
            if (!isInitialized || allowDirectRoomStart) return;

            // 检查是否意外断开连接
            if (!PhotonNetwork.IsConnected)
            {
                HandleDisconnection("连接丢失");
            }
            // 检查是否意外离开房间
            else if (!PhotonNetwork.InRoom && !isLeavingRoom)
            {
                HandleRoomLeft("意外离开房间");
            }
        }

        /// <summary>
        /// 处理连接断开
        /// </summary>
        private void HandleDisconnection(string reason)
        {
            if (isLeavingRoom) return;

            LogDebug($"检测到连接断开: {reason}");
            ShowError($"网络连接断开: {reason}");
            Invoke(nameof(ReturnToLobbyDelayed), 3f);
        }

        /// <summary>
        /// 处理离开房间
        /// </summary>
        private void HandleRoomLeft(string reason)
        {
            LogDebug($"检测到离开房间: {reason}");

            // 触发返回大厅事件
            OnReturnToLobby();
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
                // 备用方案：直接切换场景
                Debug.LogWarning("[RoomSceneController] SceneTransitionManager返回失败，使用备用方案");
                SceneManager.LoadScene("LobbyScene");
            }
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 获取当前玩家数据
        /// </summary>
        public LobbyPlayerData GetCurrentPlayerData()
        {
            return currentPlayerData;
        }

        /// <summary>
        /// 获取当前房间数据
        /// </summary>
        public LobbyRoomData GetCurrentRoomData()
        {
            return currentRoomData;
        }

        /// <summary>
        /// 检查是否已初始化
        /// </summary>
        public bool IsInitialized()
        {
            return isInitialized;
        }

        /// <summary>
        /// 检查是否有有效数据
        /// </summary>
        public bool HasValidData()
        {
            return hasValidData;
        }

        /// <summary>
        /// 获取UI控制器引用
        /// </summary>
        public RoomUIController GetUIController()
        {
            return roomUIController;
        }

        /// <summary>
        /// 设置UI控制器引用
        /// </summary>
        public void SetUIController(RoomUIController controller)
        {
            roomUIController = controller;
            LogDebug("UI控制器引用已设置");
        }

        /// <summary>
        /// 强制刷新UI（通过UI控制器）
        /// </summary>
        public void ForceRefreshUI()
        {
            if (roomUIController != null)
            {
                roomUIController.RefreshAllUI();
            }
            else
            {
                LogDebug("无法刷新UI：RoomUIController未设置");
            }
        }

        /// <summary>
        /// 手动触发返回大厅
        /// </summary>
        public void ReturnToLobby()
        {
            LogDebug("手动触发返回大厅");
            OnReturnToLobby();
        }

        /// <summary>
        /// 强制重新初始化（调试用）
        /// </summary>
        public void ForceReinitialize()
        {
            LogDebug("强制重新初始化");
            isInitialized = false;
            isInitializing = false;
            hasValidData = false;
            hasHandledGameStart = false;
            StartCoroutine(InitializeRoomSceneCoroutine());
        }

        /// <summary>
        /// 获取场景状态信息
        /// </summary>
        public string GetSceneStatusInfo()
        {
            return $"初始化: {isInitialized}, " +
                   $"初始化中: {isInitializing}, " +
                   $"有效数据: {hasValidData}, " +
                   $"事件订阅: {hasSubscribedToEvents}, " +
                   $"玩家: {currentPlayerData?.playerName ?? "无"}, " +
                   $"房间: {currentRoomData?.roomName ?? "无"}, " +
                   $"游戏开始处理: {hasHandledGameStart}, " +
                   $"离开中: {isLeavingRoom}";
        }

        /// <summary>
        /// 获取UI状态信息
        /// </summary>
        public string GetUIStatusInfo()
        {
            if (roomUIController != null)
            {
                return roomUIController.GetUIStatusInfo();
            }
            return "RoomUIController: 未设置";
        }

        /// <summary>
        /// 获取房间详细信息
        /// </summary>
        public string GetDetailedDebugInfo()
        {
            if (RoomManager.Instance != null)
            {
                return RoomManager.Instance.GetRoomStatusInfo() + "\n" +
                       RoomManager.Instance.GetPlayerListInfo();
            }
            return "RoomManager: 未初始化";
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

        #region 调试方法

        /// <summary>
        /// 显示场景详细状态
        /// </summary>
        [ContextMenu("显示场景详细状态")]
        public void ShowSceneDetailedStatus()
        {
            Debug.Log("=== RoomScene详细状态 ===");
            Debug.Log($"场景状态: {GetSceneStatusInfo()}");
            Debug.Log($"UI状态: {GetUIStatusInfo()}");
            Debug.Log($"房间信息: {GetDetailedDebugInfo()}");
        }

        /// <summary>
        /// 显示房间详细信息
        /// </summary>
        [ContextMenu("显示房间详细信息")]
        public void ShowRoomDetailedInfo()
        {
            Debug.Log("=== 房间详细信息 ===\n" + GetDetailedDebugInfo());
        }

        /// <summary>
        /// 显示UI控制器状态
        /// </summary>
        [ContextMenu("显示UI控制器状态")]
        public void ShowUIControllerStatus()
        {
            Debug.Log($"=== UI控制器状态 ===\n{GetUIStatusInfo()}");
        }

        /// <summary>
        /// 显示数据状态
        /// </summary>
        [ContextMenu("显示数据状态")]
        public void ShowDataStatus()
        {
            Debug.Log("=== 数据状态 ===");
            Debug.Log($"有效数据: {hasValidData}");

            if (currentPlayerData != null)
            {
                Debug.Log($"玩家数据: {currentPlayerData.playerName} (ID: {currentPlayerData.playerId})");
            }
            else
            {
                Debug.Log("玩家数据: 无");
            }

            if (currentRoomData != null)
            {
                Debug.Log($"房间数据: {currentRoomData.GetDetailedInfo()}");
            }
            else
            {
                Debug.Log("房间数据: 无");
            }
        }

        /// <summary>
        /// 测试开始游戏
        /// </summary>
        [ContextMenu("测试开始游戏")]
        public void TestStartGame()
        {
            if (Application.isPlaying && RoomManager.Instance?.IsHost == true)
            {
                OnStartGameButtonClicked();
            }
            else
            {
                Debug.Log("需要在游戏运行时且为房主才能测试");
            }
        }

        /// <summary>
        /// 测试准备状态切换
        /// </summary>
        [ContextMenu("测试准备状态切换")]
        public void TestReadyToggle()
        {
            if (Application.isPlaying && RoomManager.Instance?.IsHost == false)
            {
                OnReadyButtonClicked();
            }
            else
            {
                Debug.Log("需要在游戏运行时且为非房主才能测试");
            }
        }

        /// <summary>
        /// 测试强制刷新UI
        /// </summary>
        [ContextMenu("测试强制刷新UI")]
        public void TestForceRefreshUI()
        {
            if (Application.isPlaying)
            {
                ForceRefreshUI();
            }
        }

        /// <summary>
        /// 测试返回大厅
        /// </summary>
        [ContextMenu("测试返回大厅")]
        public void TestReturnToLobby()
        {
            if (Application.isPlaying)
            {
                ReturnToLobby();
            }
        }

        /// <summary>
        /// 测试重新初始化
        /// </summary>
        [ContextMenu("测试重新初始化")]
        public void TestReinitialize()
        {
            if (Application.isPlaying)
            {
                ForceReinitialize();
            }
        }

        /// <summary>
        /// 显示SceneTransitionManager状态
        /// </summary>
        [ContextMenu("显示场景切换状态")]
        public void ShowSceneTransitionStatus()
        {
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.ShowTransitionStatus();
            }
            else
            {
                Debug.Log("SceneTransitionManager实例不存在");
            }
        }

        /// <summary>
        /// 测试获取数据
        /// </summary>
        [ContextMenu("测试获取数据")]
        public void TestDataAcquisition()
        {
            if (Application.isPlaying)
            {
                LogDebug("=== 测试数据获取 ===");

                bool fromLobby = TryGetDataFromLobbyScene();
                LogDebug($"从LobbyScene获取数据: {fromLobby}");

                if (PhotonNetwork.InRoom)
                {
                    bool fromPhoton = TryRecoverDataFromPhoton();
                    LogDebug($"从Photon恢复数据: {fromPhoton}");
                }

                LogDebug($"最终数据状态: {hasValidData}");
            }
        }

        #endregion

    }
}