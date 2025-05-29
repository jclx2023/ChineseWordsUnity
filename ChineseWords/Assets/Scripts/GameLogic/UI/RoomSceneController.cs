using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Core.Network;
using Core;

namespace UI
{
    /// <summary>
    /// 房间场景控制器 - 精简版
    /// 专注于场景控制和业务逻辑，UI刷新委托给RoomUIController
    /// </summary>
    public class RoomSceneController : MonoBehaviour
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

            // 通知UI控制器进行初始化
            if (roomUIController != null)
            {
                // UI控制器会自动处理UI刷新，我们不需要手动调用
                LogDebug("RoomUIController将处理UI刷新");
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
        /// 订阅房间事件
        /// </summary>
        private void SubscribeToRoomEvents()
        {
            // 只订阅场景控制相关的事件，UI更新事件交给RoomUIController处理
            RoomManager.OnGameStarting += OnGameStarting;
            RoomManager.OnRoomLeft += OnRoomLeft;

            // 订阅网络事件
            NetworkManager.OnDisconnected += OnNetworkDisconnected;

            // 订阅场景切换事件
            SceneTransitionManager.OnSceneTransitionStarted += OnSceneTransitionStarted;

            LogDebug("已订阅场景控制相关事件");
        }

        /// <summary>
        /// 取消订阅房间事件
        /// </summary>
        private void UnsubscribeFromRoomEvents()
        {
            RoomManager.OnGameStarting -= OnGameStarting;
            RoomManager.OnRoomLeft -= OnRoomLeft;
            NetworkManager.OnDisconnected -= OnNetworkDisconnected;
            SceneTransitionManager.OnSceneTransitionStarted -= OnSceneTransitionStarted;
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

            // 执行场景切换 - 统一的切换点
            bool switchSuccess = SceneTransitionManager.SwitchToGameScene("RoomSceneController");

            if (!switchSuccess)
            {
                LogDebug("场景切换请求失败，可能已在切换中");
                // 如果切换失败，重置处理标志
                hasHandledGameStart = false;
                HideLoadingPanel();
            }
        }

        private void OnRoomLeft()
        {
            LogDebug("离开房间");
            SceneTransitionManager.ReturnToMainMenu("RoomSceneController");
        }

        private void OnNetworkDisconnected()
        {
            LogDebug("网络断开连接");
            ShowError("网络连接断开，将返回主菜单");
            Invoke(nameof(ReturnToMainMenuDelayed), 3f);
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

            // 统一通过RoomManager启动游戏
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

            SceneTransitionManager.ReturnToMainMenu("RoomSceneController");
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
                roomUIController.ForceRefreshUI();
                LogDebug("已请求RoomUIController强制刷新");
            }
            else
            {
                LogDebug("RoomUIController不存在，无法执行刷新");
            }
        }

        #endregion

        #region 场景控制

        /// <summary>
        /// 延迟返回主菜单
        /// </summary>
        private void ReturnToMainMenuDelayed()
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
                roomUIController.ForceRefreshUI();
            }
            else
            {
                LogDebug("无法刷新UI：RoomUIController未设置");
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

        /// <summary>
        /// 显示UI控制器状态
        /// </summary>
        [ContextMenu("显示UI控制器状态")]
        public void ShowUIControllerStatus()
        {
            if (roomUIController != null)
            {
                Debug.Log($"=== UI控制器状态 ===\n{roomUIController.GetUIStatusInfo()}");
            }
            else
            {
                Debug.Log("UI控制器: 未设置");
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