using UnityEngine;
using Photon.Pun;
using System.Collections;
using Lobby.Core;
using Photon.Realtime;

namespace Core.Network
{
    /// <summary>
    /// 修复后的持久化网络管理器
    /// 统一管理所有网络组件，避免重复连接问题
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PersistentNetworkManager : MonoBehaviourPun
    {
        [Header("网络组件引用")]
        [SerializeField] private PhotonNetworkAdapter photonAdapter;
        [SerializeField] private LobbyNetworkManager lobbyNetworkManager;
        [SerializeField] private NetworkManager networkManager;

        [Header("连接管理设置")]
        [SerializeField] private bool autoConnectOnStart = true;
        [SerializeField] private float initializationDelay = 1f;
        [SerializeField] private float connectionDelay = 2f;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        [Header("启动加载UI")]
        [SerializeField] private StartupLoadingUI startupLoadingUI;

        public static PersistentNetworkManager Instance { get; private set; }

        // 连接状态管理
        private bool isInitialized = false;
        private bool isConnecting = false;
        private bool hasAttemptedConnection = false;

        // PhotonView ID 管理
        private static int nextViewID = 1000;

        #region 公共属性

        public PhotonNetworkAdapter PhotonAdapter => photonAdapter;
        public LobbyNetworkManager LobbyNetwork => lobbyNetworkManager;
        public NetworkManager GameNetwork => networkManager;
        public bool IsInitialized => isInitialized;
        public bool IsConnecting => isConnecting;

        #endregion

        #region Unity生命周期

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LogDebug("PersistentNetworkManager 创建成功，设置为跨场景持久化");

                // 立即初始化组件引用，但不启动连接
                InitializeComponentReferences();
            }
            else
            {
                LogDebug("检测到重复的PersistentNetworkManager，销毁多余实例");
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            if (Instance == this)
            {
                if (startupLoadingUI == null)
                {
                    startupLoadingUI = FindObjectOfType<StartupLoadingUI>();
                }
                StartCoroutine(DelayedInitialization());
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                LogDebug("PersistentNetworkManager 正在销毁");
                Instance = null;
            }
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 延迟初始化流程
        /// </summary>
        private IEnumerator DelayedInitialization()
        {
            LogDebug("开始延迟初始化流程");

            // 等待初始化延迟，确保所有组件就绪
            yield return new WaitForSeconds(initializationDelay);

            // 初始化所有网络组件
            InitializeNetworkComponents();

            // 检查当前连接状态
            CheckCurrentConnectionState();

            // 如果需要自动连接且未连接，则启动连接
            if (autoConnectOnStart && !PhotonNetwork.IsConnected && !hasAttemptedConnection)
            {
                yield return new WaitForSeconds(connectionDelay);
                StartManagedConnection();
            }

            isInitialized = true;
            LogDebug("PersistentNetworkManager 初始化完成");
        }

        /// <summary>
        /// 初始化组件引用
        /// </summary>
        private void InitializeComponentReferences()
        {
            LogDebug("初始化组件引用...");

            // 自动查找组件（如果没有手动分配）
            FindNetworkComponents();

            // 验证组件完整性
            ValidateComponents();
        }

        /// <summary>
        /// 初始化网络组件
        /// </summary>
        private void InitializeNetworkComponents()
        {
            LogDebug("初始化网络组件...");

            // 设置组件持久化属性
            SetupComponentPersistence();

            // 配置PhotonView
            SetupPhotonView();

            // 禁用其他组件的自动连接，由PersistentNetworkManager统一管理
            DisableAutoConnectInOtherComponents();

            LogDebug("网络组件初始化完成");
        }

        /// <summary>
        /// 禁用其他组件的自动连接
        /// </summary>
        private void DisableAutoConnectInOtherComponents()
        {
            // 禁用LobbyNetworkManager的自动连接
            if (lobbyNetworkManager != null)
            {
                // 通过反射禁用autoConnectOnStart
                var autoConnectField = typeof(LobbyNetworkManager).GetField("autoConnectOnStart",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (autoConnectField != null)
                {
                    autoConnectField.SetValue(lobbyNetworkManager, false);
                    LogDebug("已禁用LobbyNetworkManager的自动连接");
                }
            }

            LogDebug("其他组件的自动连接已禁用，由PersistentNetworkManager统一管理");
        }

        /// <summary>
        /// 检查当前连接状态
        /// </summary>
        private void CheckCurrentConnectionState()
        {
            if (PhotonNetwork.IsConnected)
            {
                LogDebug("检测到已连接到Photon，跳过自动连接");
                hasAttemptedConnection = true;

                // 确保LobbyNetworkManager状态同步
                if (lobbyNetworkManager != null)
                {
                    // 调用LobbyNetworkManager的状态同步方法
                    var syncMethod = typeof(LobbyNetworkManager).GetMethod("CheckAndSyncCurrentState",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (syncMethod != null)
                    {
                        syncMethod.Invoke(lobbyNetworkManager, null);
                        LogDebug("已同步LobbyNetworkManager状态");
                    }
                }
            }
            else
            {
                LogDebug($"当前Photon状态: {PhotonNetwork.NetworkClientState}");
            }
        }

        /// <summary>
        /// 自动查找网络组件
        /// </summary>
        private void FindNetworkComponents()
        {
            if (photonAdapter == null)
            {
                photonAdapter = GetComponentInChildren<PhotonNetworkAdapter>();
                LogDebug($"自动查找PhotonNetworkAdapter: {(photonAdapter != null ? "成功" : "失败")}");
            }

            if (lobbyNetworkManager == null)
            {
                lobbyNetworkManager = GetComponentInChildren<LobbyNetworkManager>();
                LogDebug($"自动查找LobbyNetworkManager: {(lobbyNetworkManager != null ? "成功" : "失败")}");
            }

            if (networkManager == null)
            {
                networkManager = GetComponentInChildren<NetworkManager>();
                LogDebug($"自动查找NetworkManager: {(networkManager != null ? "成功" : "失败")}");
            }
        }

        /// <summary>
        /// 验证组件完整性
        /// </summary>
        private bool ValidateComponents()
        {
            bool isValid = true;

            if (photonAdapter == null)
            {
                Debug.LogError("[PersistentNetworkManager] PhotonNetworkAdapter 组件缺失！");
                isValid = false;
            }

            if (lobbyNetworkManager == null)
            {
                Debug.LogError("[PersistentNetworkManager] LobbyNetworkManager 组件缺失！");
                isValid = false;
            }

            // NetworkManager是可选的，不会导致验证失败
            if (networkManager == null)
            {
                LogDebug("NetworkManager 组件缺失（可选组件）");
            }

            return isValid;
        }

        /// <summary>
        /// 设置组件持久化属性
        /// </summary>
        private void SetupComponentPersistence()
        {
            LogDebug("网络组件持久化设置完成");
        }

        /// <summary>
        /// 配置PhotonView
        /// </summary>
        private void SetupPhotonView()
        {
            if (photonView == null)
            {
                var pv = gameObject.AddComponent<PhotonView>();
                pv.ViewID = AllocatePhotonViewID();
                LogDebug($"为PersistentNetworkManager分配PhotonView ID: {pv.ViewID}");
            }
        }

        #endregion

        #region 统一连接管理

        /// <summary>
        /// 启动受管理的连接流程
        /// </summary>
        private void StartManagedConnection()
        {
            if (isConnecting || hasAttemptedConnection)
            {
                LogDebug("连接已在进行中或已尝试过，跳过重复连接");
                return;
            }

            LogDebug("开始受管理的Photon连接流程");
            isConnecting = true;
            hasAttemptedConnection = true;

            StartCoroutine(ManagedConnectionCoroutine());
        }

        /// <summary>
        /// 受管理的连接协程
        /// </summary>
        private IEnumerator ManagedConnectionCoroutine()
        {
            LogDebug("=== 开始受管理的Photon连接 ===");

            try
            {
                // 检查PhotonNetworkAdapter是否就绪
                if (photonAdapter == null)
                {
                    Debug.LogError("PhotonNetworkAdapter未就绪，无法连接");
                    yield break;
                }

                // 检查当前状态
                if (PhotonNetwork.IsConnected)
                {
                    LogDebug("已连接到Photon，连接流程完成");

                    if (startupLoadingUI != null)
                    {
                        startupLoadingUI.OnConnectionSuccess();
                    }

                    yield break;
                }

                // 使用PhotonNetworkAdapter进行连接
                if (PhotonNetwork.NetworkClientState == ClientState.PeerCreated)
                {
                    LogDebug("通过PhotonNetwork.ConnectUsingSettings开始连接");
                    bool connectResult = PhotonNetwork.ConnectUsingSettings();

                    if (!connectResult)
                    {
                        Debug.LogError("PhotonNetwork.ConnectUsingSettings返回false");
                        yield break;
                    }
                }
                else
                {
                    LogDebug($"Photon状态不正确: {PhotonNetwork.NetworkClientState}，等待状态重置");

                    // 等待状态重置
                    float waitTime = 0f;
                    while (PhotonNetwork.NetworkClientState != ClientState.PeerCreated && waitTime < 5f)
                    {
                        yield return new WaitForSeconds(0.5f);
                        waitTime += 0.5f;
                    }

                    if (PhotonNetwork.NetworkClientState == ClientState.PeerCreated)
                    {
                        PhotonNetwork.ConnectUsingSettings();
                    }
                    else
                    {
                        Debug.LogError("Photon状态重置失败");
                        yield break;
                    }
                }

                // 等待连接完成
                float connectionTimeout = 15f;
                float elapsed = 0f;

                while (!PhotonNetwork.IsConnectedAndReady && elapsed < connectionTimeout)
                {
                    LogDebug($"等待连接... 状态: {PhotonNetwork.NetworkClientState}, 时间: {elapsed:F1}s");
                    yield return new WaitForSeconds(1f);
                    elapsed += 1f;
                }

                if (PhotonNetwork.IsConnectedAndReady)
                {
                    LogDebug("✓ Photon连接成功！");
                    if (startupLoadingUI != null)
                    {
                        startupLoadingUI.OnConnectionSuccess();
                    }
                    // 通知LobbyNetworkManager同步状态
                    NotifyLobbyManagerConnectionSuccess();
                }
                else
                {
                    Debug.LogError($"✗ Photon连接超时！最终状态: {PhotonNetwork.NetworkClientState}");
                    if (startupLoadingUI != null)
                    {
                        startupLoadingUI.OnConnectionFailed("连接超时");
                    }
                }
            }
            finally
            {
                isConnecting = false;
            }
        }

        /// <summary>
        /// 通知LobbyNetworkManager连接成功
        /// </summary>
        private void NotifyLobbyManagerConnectionSuccess()
        {
            if (lobbyNetworkManager != null)
            {
                // 调用状态同步方法
                var syncMethod = typeof(LobbyNetworkManager).GetMethod("CheckAndSyncCurrentState",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (syncMethod != null)
                {
                    syncMethod.Invoke(lobbyNetworkManager, null);
                    LogDebug("已通知LobbyNetworkManager连接成功");
                }
            }
        }

        #endregion

        #region PhotonView ID 管理

        /// <summary>
        /// 分配新的PhotonView ID
        /// </summary>
        public int AllocatePhotonViewID()
        {
            int allocatedID = nextViewID++;
            LogDebug($"分配PhotonView ID: {allocatedID}");
            return allocatedID;
        }

        /// <summary>
        /// 为GameObject分配PhotonView
        /// </summary>
        public PhotonView AllocatePhotonView(GameObject target)
        {
            if (target == null)
            {
                Debug.LogError("[PersistentNetworkManager] 目标GameObject为空，无法分配PhotonView");
                return null;
            }

            PhotonView pv = target.GetComponent<PhotonView>();
            if (pv == null)
            {
                pv = target.AddComponent<PhotonView>();
            }

            pv.ViewID = AllocatePhotonViewID();
            LogDebug($"为 {target.name} 分配PhotonView ID: {pv.ViewID}");
            return pv;
        }

        /// <summary>
        /// 重置PhotonView ID计数器
        /// </summary>
        public void ResetPhotonViewIDCounter()
        {
            nextViewID = 1000;
            LogDebug("PhotonView ID计数器已重置");
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 手动连接Photon
        /// </summary>
        public void ConnectToPhoton()
        {
            if (isConnecting)
            {
                LogDebug("连接已在进行中");
                return;
            }

            if (PhotonNetwork.IsConnected)
            {
                LogDebug("已连接到Photon");
                return;
            }

            LogDebug("手动触发Photon连接");
            hasAttemptedConnection = false; // 重置标志，允许重新连接
            StartManagedConnection();
        }

        /// <summary>
        /// 断开Photon连接
        /// </summary>
        public void DisconnectFromPhoton()
        {
            if (PhotonNetwork.IsConnected)
            {
                LogDebug("手动断开Photon连接");
                hasAttemptedConnection = false; // 重置标志
                PhotonNetwork.Disconnect();
            }
        }

        /// <summary>
        /// 强制重新初始化
        /// </summary>
        public void ForceReinitialize()
        {
            LogDebug("强制重新初始化网络组件");
            isInitialized = false;
            hasAttemptedConnection = false;
            isConnecting = false;

            StopAllCoroutines();
            StartCoroutine(DelayedInitialization());
        }

        #endregion

        #region 调试方法

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[PersistentNetworkManager] {message}");
            }
        }

        #endregion
    }
}