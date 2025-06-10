using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Core.Network;
using Core;
using Photon.Pun;
using Photon.Realtime;

namespace UI
{
    /// <summary>
    /// 房间场景控制器 - 完全适配Photon版本
    /// 专注于场景控制和业务逻辑，UI刷新委托给RoomUIController
    /// 完全基于你现有的RoomManager、SceneTransitionManager架构
    /// </summary>
    public class RoomSceneController : MonoBehaviourPun, IInRoomCallbacks
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

        [Header("设置")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool autoFindUIController = true;

        // 状态管理
        private bool isInitialized = false;
        private bool hasHandledGameStart = false;
        private bool isLeavingRoom = false;

        private void Start()
        {
            InitializeRoomScene();
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

        /// <summary>
        /// 初始化房间场景
        /// </summary>
        private void InitializeRoomScene()
        {
            LogDebug("初始化房间场景");

            // 显示加载界面
            ShowLoadingPanel("正在加载房间信息...");

            // 查找UI控制器
            if (autoFindUIController && roomUIController == null)
            {
                roomUIController = FindObjectOfType<RoomUIController>();
                if (roomUIController != null)
                {
                    LogDebug("自动找到RoomUIController");
                }
                else
                {
                    LogDebug("警告: 未找到RoomUIController，部分UI功能可能不可用");
                }
            }

            // 绑定UI事件
            BindUIEvents();

            // 订阅房间事件
            SubscribeToRoomEvents();

            // 验证状态
            if (!ValidateRoomStatus())
            {
                ShowError("房间状态异常，请返回大厅重试");
                return;
            }

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
        /// 订阅房间事件 - 基于你的RoomManager架构
        /// </summary>
        private void SubscribeToRoomEvents()
        {
            // 订阅RoomManager事件（场景控制相关）
            RoomManager.OnGameStarting += OnGameStarting;
            RoomManager.OnReturnToLobby += OnReturnToLobby;

            // 订阅SceneTransitionManager事件
            SceneTransitionManager.OnSceneTransitionStarted += OnSceneTransitionStarted;

            LogDebug("已订阅房间和场景切换事件");
        }

        /// <summary>
        /// 取消订阅房间事件
        /// </summary>
        private void UnsubscribeFromRoomEvents()
        {
            RoomManager.OnGameStarting -= OnGameStarting;
            RoomManager.OnReturnToLobby -= OnReturnToLobby;
            SceneTransitionManager.OnSceneTransitionStarted -= OnSceneTransitionStarted;
        }

        #region 验证方法

        /// <summary>
        /// 验证房间状态
        /// </summary>
        private bool ValidateRoomStatus()
        {
            if (!PhotonNetwork.InRoom)
            {
                LogDebug("未在Photon房间中");
                return false;
            }

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

            LogDebug($"房间状态验证通过 - 房间: {RoomManager.Instance.RoomName}, 玩家数: {RoomManager.Instance.PlayerCount}");
            return true;
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

        #region 房间事件处理 - 基于你的架构

        /// <summary>
        /// 游戏开始事件处理 - 唯一的场景切换执行点
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

            // 触发HostGameManager开始游戏（仅Host端）
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

            // 使用你的SceneTransitionManager执行场景切换
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
            SceneTransitionManager.ReturnToMainMenu("RoomSceneController");
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

        #region 按钮事件处理 - 委托给RoomManager

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
                SceneTransitionManager.ReturnToMainMenu("RoomSceneController");
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
                roomUIController.RefreshAllUI(); // 使用你的RefreshAllUI方法
                LogDebug("已请求RoomUIController强制刷新");
            }
            else
            {
                LogDebug("RoomUIController不存在，无法执行刷新");
            }
        }

        #endregion

        #region IInRoomCallbacks实现 - 最小化处理

        void IInRoomCallbacks.OnPlayerEnteredRoom(Player newPlayer)
        {
            LogDebug($"Photon: 玩家加入房间 - {newPlayer.NickName} (ID: {newPlayer.ActorNumber})");
            // UI更新交给RoomUIController处理
        }

        void IInRoomCallbacks.OnPlayerLeftRoom(Player otherPlayer)
        {
            LogDebug($"Photon: 玩家离开房间 - {otherPlayer.NickName} (ID: {otherPlayer.ActorNumber})");
            // UI更新交给RoomUIController处理
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

        #endregion

        #region Photon连接状态监控

        /// <summary>
        /// 监控Photon连接状态（通过Update检查）
        /// </summary>
        private void Update()
        {
            if (!isInitialized) return;

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
            SceneTransitionManager.ReturnToMainMenu("RoomSceneController");
        }

        #endregion

        #region 公共接口

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
                roomUIController.RefreshAllUI(); // 使用正确的方法名
            }
            else
            {
                LogDebug("无法刷新UI：RoomUIController未设置");
            }
        }

        /// <summary>
        /// 获取UI状态信息 - 适配你的架构
        /// </summary>
        public string GetUIStatusInfo()
        {
            if (roomUIController != null)
            {
                return roomUIController.GetUIStatusInfo(); // 使用你的方法
            }
            return "RoomUIController: 未设置";
        }

        /// <summary>
        /// 获取房间详细信息 - 使用你的RoomManager
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

        #region 调试方法 - 适配你的架构

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
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && isInitialized)
            {
                LogDebug("应用失去焦点");
            }
        }

        #endregion
    }
}