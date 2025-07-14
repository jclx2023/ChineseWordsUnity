using UnityEngine;
using Core.Network;
using UI;
using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;

namespace Core
{
    /// <summary>
    /// 场景模式控制器 - Photon精简版
    /// 专注于多人游戏的组件管理，移除单机模式和复杂架构
    /// 基于Photon状态进行简单的组件激活/禁用控制
    /// </summary>
    public class SceneModeController : MonoBehaviourPun, IConnectionCallbacks, IMatchmakingCallbacks
    {
        [Header("核心组件引用")]
        [SerializeField] private GameObject gameCanvas;
        [SerializeField] private NetworkUI networkUI;

        [Header("可选组件引用")]
        [SerializeField] private GameObject networkCanvas;

        [Header("自动查找设置")]
        [SerializeField] private bool autoFindComponents = true;
        [SerializeField] private float initializationDelay = 0.3f;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 组件状态
        private bool isInitialized = false;
        private bool configurationCompleted = false;

        // 当前状态
        private bool isInRoom = false;
        private bool isMasterClient = false;

        private void Start()
        {
            StartCoroutine(InitializeSceneController());
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

        #region 初始化

        /// <summary>
        /// 初始化场景控制器
        /// </summary>
        private IEnumerator InitializeSceneController()
        {
            LogDebug("开始初始化SceneModeController");

            // 等待一下确保其他组件就绪
            yield return new WaitForSeconds(initializationDelay);

            // 自动查找组件
            if (autoFindComponents)
            {
                FindEssentialComponents();
            }

            isInitialized = true;
            LogDebug("SceneModeController初始化完成");

            // 配置场景
            ConfigureSceneForPhoton();

            // 启动组件协调
            StartCoroutine(EnsureComponentsStarted());

            configurationCompleted = true;
        }

        /// <summary>
        /// 查找必要组件
        /// </summary>
        private void FindEssentialComponents()
        {
            // 查找NetworkUI
            if (networkUI == null)
            {
                networkUI = FindObjectOfType<NetworkUI>();
                LogDebug($"找到NetworkUI: {networkUI?.name ?? "未找到"}");
            }

            // 查找NetworkCanvas（可选）
            if (networkCanvas == null)
            {
                networkCanvas = GameObject.Find("NetworkCanvas");
                if (networkCanvas == null)
                {
                    networkCanvas = GameObject.Find("NetworkUI");
                }
                LogDebug($"找到NetworkCanvas: {networkCanvas?.name ?? "未找到"}");
            }

        }

        #endregion

        #region 场景配置 - 基于Photon状态

        /// <summary>
        /// 基于Photon状态配置场景
        /// </summary>
        private void ConfigureSceneForPhoton()
        {
            LogDebug("基于Photon状态配置场景");

            // 获取当前Photon状态
            UpdatePhotonStatus();

            if (isInRoom)
            {
                ConfigureMultiplayerMode();
            }
            else
            {
                ConfigureOfflineMode();
            }
        }

        /// <summary>
        /// 更新Photon状态
        /// </summary>
        private void UpdatePhotonStatus()
        {
            bool wasInRoom = isInRoom;
            bool wasMasterClient = isMasterClient;

            isInRoom = PhotonNetwork.InRoom;
            isMasterClient = PhotonNetwork.IsMasterClient;

            if (wasInRoom != isInRoom || wasMasterClient != isMasterClient)
            {
                LogDebug($"Photon状态变化: 房间={isInRoom}, MasterClient={isMasterClient}");
            }
        }

        /// <summary>
        /// 配置多人游戏模式
        /// </summary>
        private void ConfigureMultiplayerMode()
        {
            LogDebug($"配置多人模式 - MasterClient: {isMasterClient}");

            // UI组件配置
            SetGameObjectActive(gameCanvas, true);
            SetGameObjectActive(networkCanvas, true);

            // NetworkUI配置
            if (networkUI != null)
            {
                networkUI.ShowNetworkPanel();
                LogDebug("显示网络UI");
            }

            // HostGameManager配置
            ConfigureHostGameManager();

        }

        /// <summary>
        /// 配置离线模式（未在房间中）
        /// </summary>
        private void ConfigureOfflineMode()
        {
            LogDebug("配置离线模式");

            // UI组件配置
            SetGameObjectActive(gameCanvas, true);
            SetGameObjectActive(networkCanvas, false);

            // NetworkUI配置
            if (networkUI != null)
            {
                networkUI.HideNetworkPanel();
                LogDebug("隐藏网络UI");
            }

            // 禁用游戏管理器
            ConfigureHostGameManager(false);
        }

        /// <summary>
        /// 配置HostGameManager
        /// </summary>
        private void ConfigureHostGameManager(bool? forceState = null)
        {
            var hostManager = HostGameManager.Instance;
            if (hostManager != null)
            {
                bool shouldEnable = forceState ?? (isInRoom && isMasterClient);
                hostManager.enabled = shouldEnable;

                LogDebug($"HostGameManager 启用状态: {shouldEnable}");
            }
        }

        #endregion

        #region 组件启动协调

        /// <summary>
        /// 确保组件正确启动
        /// </summary>
        private IEnumerator EnsureComponentsStarted()
        {
            LogDebug("协调组件启动");

            // 等待一下确保配置生效
            yield return new WaitForSeconds(0.5f);

            // 启动NQMC
            StartNetworkQuestionController();

            // 通知HostGameManager
            NotifyHostGameManager();
        }

        /// <summary>
        /// 启动网络题目控制器
        /// </summary>
        private void StartNetworkQuestionController()
        {
            var nqmc = NetworkQuestionManagerController.Instance;
            if (nqmc != null)
            {
                if (!nqmc.IsGameStarted)
                {
                    if (isInRoom)
                    {
                        LogDebug("启动NQMC多人模式");
                        nqmc.StartGame(true);
                    }
                    else
                    {
                        LogDebug("Photon未连接，跳过NQMC启动");
                    }
                }
                else
                {
                    LogDebug($"NQMC已启动 - 多人模式");
                }
            }
            else
            {
            }
        }

        /// <summary>
        /// 通知HostGameManager场景已就绪
        /// </summary>
        private void NotifyHostGameManager()
        {
            if (!isInRoom || !isMasterClient) return;

            var hostManager = HostGameManager.Instance;
            if (hostManager != null && hostManager.enabled)
            {
                // 检查HostGameManager是否有场景加载完成的方法
                var method = hostManager.GetType().GetMethod("OnGameSceneLoaded");
                if (method != null)
                {
                    LogDebug("通知HostGameManager场景加载完成");
                    method.Invoke(hostManager, null);
                }
            }
        }

        #endregion

        #region IConnectionCallbacks 实现

        public void OnConnected()
        {
        }

        public void OnConnectedToMaster()
        {
        }

        public void OnDisconnected(DisconnectCause cause)
        {
            LogDebug($"Photon: 断开连接 - 原因: {cause}");

            if (isInitialized)
            {
                StartCoroutine(DelayedReconfigure());
            }
        }

        public void OnRegionListReceived(RegionHandler regionHandler)
        {
            // 可选实现
        }

        public void OnCustomAuthenticationResponse(Dictionary<string, object> data)
        {
            // 可选实现
        }

        public void OnCustomAuthenticationFailed(string debugMessage)
        {
            LogDebug($"Photon: 自定义认证失败 - {debugMessage}");
        }

        #endregion

        #region IMatchmakingCallbacks 实现

        public void OnFriendListUpdate(List<FriendInfo> friendList)
        {
            // 可选实现
        }

        public void OnCreatedRoom()
        {
            LogDebug("Photon: 房间创建成功");
        }

        public void OnCreateRoomFailed(short returnCode, string message)
        {
            LogDebug($"Photon: 房间创建失败 - {returnCode}: {message}");
        }

        public void OnJoinedRoom()
        {
            LogDebug("Photon: 加入房间");

            if (isInitialized)
            {
                StartCoroutine(DelayedReconfigure());
            }
        }

        public void OnJoinRoomFailed(short returnCode, string message)
        {
            LogDebug($"Photon: 加入房间失败 - {returnCode}: {message}");
        }

        public void OnJoinRandomFailed(short returnCode, string message)
        {
            LogDebug($"Photon: 随机加入房间失败 - {returnCode}: {message}");
        }

        public void OnLeftRoom()
        {
            LogDebug("Photon: 离开房间");

            if (isInitialized)
            {
                StartCoroutine(DelayedReconfigure());
            }
        }

        public void OnRoomListUpdate(List<RoomInfo> roomList)
        {
            // 可选实现
        }

        public void OnLobbyStatisticsUpdate(List<TypedLobbyInfo> lobbyStatistics)
        {
            // 可选实现
        }

        public void OnJoinedLobby()
        {
            LogDebug("Photon: 加入大厅");
        }

        public void OnLeftLobby()
        {
            LogDebug("Photon: 离开大厅");
        }

        #endregion

        #region Photon事件处理 - 使用IPunObservable替代

        /// <summary>
        /// 监听MasterClient变化（通过Update检查）
        /// </summary>
        private void Update()
        {
            if (!isInitialized) return;

            // 每秒检查一次MasterClient状态
            if (Time.frameCount % 60 == 0)
            {
                bool currentIsMasterClient = PhotonNetwork.IsMasterClient;
                if (currentIsMasterClient != isMasterClient)
                {
                    LogDebug($"Photon: MasterClient状态变化 - 当前: {currentIsMasterClient}");
                    isMasterClient = currentIsMasterClient;
                    StartCoroutine(DelayedReconfigure());
                }
            }
        }

        /// <summary>
        /// 延迟重新配置
        /// </summary>
        private IEnumerator DelayedReconfigure()
        {
            yield return new WaitForSeconds(0.2f);
            ConfigureSceneForPhoton();
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 安全设置GameObject激活状态
        /// </summary>
        private void SetGameObjectActive(GameObject obj, bool active)
        {
            if (obj != null)
            {
                bool wasActive = obj.activeSelf;
                obj.SetActive(active);
                LogDebug($"设置 {obj.name} 激活状态: {wasActive} → {active}");
            }
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[SceneModeController] {message}");
            }
        }

        #endregion
    }
}