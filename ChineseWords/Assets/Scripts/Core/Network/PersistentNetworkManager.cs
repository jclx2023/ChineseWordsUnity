using UnityEngine;
using Photon.Pun;
using System.Collections;
using Lobby.Core;

namespace Core.Network
{
    /// <summary>
    /// 持久化网络管理器 - 游戏启动时创建，跨场景持续存在
    /// 统一管理所有网络组件的生命周期和PhotonView ID分配
    /// </summary>
    [DefaultExecutionOrder(-100)] // 确保最先初始化
    public class PersistentNetworkManager : MonoBehaviourPun
    {
        [Header("网络组件引用")]
        [SerializeField] private PhotonNetworkAdapter photonAdapter;
        [SerializeField] private LobbyNetworkManager lobbyNetworkManager;
        [SerializeField] private NetworkManager networkManager;

        [Header("自动连接设置")]
        [SerializeField] private bool autoConnectOnStart = true;
        [SerializeField] private float connectionDelay = 1f;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        public static PersistentNetworkManager Instance { get; private set; }

        // PhotonView ID 管理
        private static int nextViewID = 1000; // 从1000开始，避免与系统ID冲突

        // 组件状态
        private bool isInitialized = false;

        #region 公共属性

        public PhotonNetworkAdapter PhotonAdapter => photonAdapter;
        public LobbyNetworkManager LobbyNetwork => lobbyNetworkManager;
        public NetworkManager GameNetwork => networkManager;
        public bool IsInitialized => isInitialized;

        #endregion

        #region Unity生命周期

        private void Awake()
        {
            // 单例模式 - 确保只有一个实例
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LogDebug("PersistentNetworkManager 创建成功，设置为跨场景持久化");

                InitializeNetworkComponents();
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
                StartCoroutine(DelayedAutoConnect());
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
        /// 初始化所有网络组件
        /// </summary>
        private void InitializeNetworkComponents()
        {
            LogDebug("开始初始化网络组件...");

            // 自动查找组件（如果没有手动分配）
            FindNetworkComponents();

            // 验证组件完整性
            if (!ValidateComponents())
            {
                Debug.LogError("[PersistentNetworkManager] 网络组件验证失败！");
                return;
            }

            // 设置组件为持久化
            SetupComponentPersistence();

            // 配置PhotonView
            SetupPhotonView();

            isInitialized = true;
            LogDebug("网络组件初始化完成");
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

            if (networkManager == null)
            {
                Debug.LogError("[PersistentNetworkManager] NetworkManager 组件缺失！");
                isValid = false;
            }

            return isValid;
        }

        /// <summary>
        /// 设置组件持久化属性
        /// </summary>
        private void SetupComponentPersistence()
        {
            // 确保所有网络组件都标记为持久化处理
            // 由于它们都在同一个持久化对象上，会自动继承持久化属性
            LogDebug("网络组件持久化设置完成");
        }

        /// <summary>
        /// 配置PhotonView
        /// </summary>
        private void SetupPhotonView()
        {
            // 确保PersistentNetworkManager有PhotonView组件
            if (photonView == null)
            {
                var pv = gameObject.AddComponent<PhotonView>();
                pv.ViewID = AllocatePhotonViewID();
                LogDebug($"为PersistentNetworkManager分配PhotonView ID: {pv.ViewID}");
            }
        }

        #endregion

        #region 自动连接

        /// <summary>
        /// 延迟自动连接
        /// </summary>
        private IEnumerator DelayedAutoConnect()
        {
            if (!autoConnectOnStart)
            {
                LogDebug("自动连接已禁用");
                yield break;
            }

            LogDebug($"等待 {connectionDelay} 秒后开始自动连接...");
            yield return new WaitForSeconds(connectionDelay);

            // 检查是否已经连接
            if (PhotonNetwork.IsConnected)
            {
                LogDebug("检测到已连接到Photon，跳过自动连接");
                yield break;
            }

            LogDebug("开始自动连接到Photon...");

            // 使用LobbyNetworkManager进行连接
            if (lobbyNetworkManager != null)
            {
                // 强制启动连接流程
                lobbyNetworkManager.enabled = true;
                lobbyNetworkManager.ForceReconnect();
            }
            else
            {
                Debug.LogError("[PersistentNetworkManager] LobbyNetworkManager 不可用，无法自动连接");
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
        /// 重置PhotonView ID计数器（调试用）
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
            if (lobbyNetworkManager != null)
            {
                LogDebug("手动触发Photon连接");
                lobbyNetworkManager.ForceReconnect();
            }
        }

        /// <summary>
        /// 断开Photon连接
        /// </summary>
        public void DisconnectFromPhoton()
        {
            if (lobbyNetworkManager != null)
            {
                LogDebug("手动断开Photon连接");
                lobbyNetworkManager.Disconnect();
            }
        }

        /// <summary>
        /// 获取网络状态信息
        /// </summary>
        public string GetNetworkStatus()
        {
            string status = "=== 持久化网络管理器状态 ===\n";
            status += $"初始化状态: {isInitialized}\n";
            status += $"PhotonAdapter: {(photonAdapter != null ? "✓" : "✗")}\n";
            status += $"LobbyNetwork: {(lobbyNetworkManager != null ? "✓" : "✗")}\n";
            status += $"GameNetwork: {(networkManager != null ? "✓" : "✗")}\n";

            if (PhotonNetwork.IsConnected)
            {
                status += $"Photon状态: 已连接\n";
                status += $"房间状态: {(PhotonNetwork.InRoom ? $"在房间 '{PhotonNetwork.CurrentRoom.Name}'" : "未在房间")}\n";
                status += $"玩家ID: {PhotonNetwork.LocalPlayer?.ActorNumber}\n";
            }
            else
            {
                status += $"Photon状态: 未连接\n";
            }

            status += $"下一个PhotonView ID: {nextViewID}";
            return status;
        }

        /// <summary>
        /// 强制重新初始化网络组件
        /// </summary>
        public void ForceReinitialize()
        {
            LogDebug("强制重新初始化网络组件");
            isInitialized = false;
            InitializeNetworkComponents();
        }

        /// <summary>
        /// 检查网络连接状态
        /// </summary>
        public bool IsConnectedToPhoton()
        {
            return PhotonNetwork.IsConnected;
        }

        /// <summary>
        /// 检查是否在房间中
        /// </summary>
        public bool IsInRoom()
        {
            return PhotonNetwork.InRoom;
        }

        /// <summary>
        /// 获取当前房间名称
        /// </summary>
        public string GetCurrentRoomName()
        {
            return PhotonNetwork.CurrentRoom?.Name ?? "";
        }

        #endregion

        #region 场景切换支持

        /// <summary>
        /// 场景切换前的准备工作
        /// </summary>
        public void PrepareForSceneTransition(string targetScene)
        {
            LogDebug($"准备切换到场景: {targetScene}");

            // 这里可以添加场景切换前的网络状态保存逻辑
            // 例如保存玩家状态、房间状态等
        }

        /// <summary>
        /// 场景切换后的恢复工作
        /// </summary>
        public void OnSceneTransitionComplete(string currentScene)
        {
            LogDebug($"场景切换完成: {currentScene}");

            // 这里可以添加场景切换后的网络状态恢复逻辑
            // 例如重新建立UI连接、同步数据等
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

        [ContextMenu("显示网络状态")]
        public void ShowNetworkStatus()
        {
            Debug.Log(GetNetworkStatus());
        }

        [ContextMenu("强制连接Photon")]
        public void ForceConnectToPhoton()
        {
            ConnectToPhoton();
        }

        [ContextMenu("强制断开Photon")]
        public void ForceDisconnectFromPhoton()
        {
            DisconnectFromPhoton();
        }

        [ContextMenu("重新初始化")]
        public void ForceReinitializeDebug()
        {
            ForceReinitialize();
        }

        [ContextMenu("分配测试PhotonView")]
        public void AllocateTestPhotonView()
        {
            AllocatePhotonView(gameObject);
        }

        #endregion
    }
}