using UnityEngine;
using Core.Network;

namespace Core.Network
{
    /// <summary>
    /// NetworkManager桥接器
    /// 将PhotonNetworkAdapter事件桥接到原有NetworkManager事件
    /// 实现平滑的网络系统过渡
    /// </summary>
    public class NetworkManagerBridge : MonoBehaviour
    {
        [Header("桥接配置")]
        [SerializeField] private bool enableEventBridge = true;
        [SerializeField] private bool enableMethodBridge = true;
        [SerializeField] private bool debugBridgeCalls = true;

        [Header("网络模式控制")]
        [SerializeField] private bool usePhotonForHost = true;
        [SerializeField] private bool usePhotonForClient = true;
        [SerializeField] private bool allowFallbackToRiptide = true;

        public static NetworkManagerBridge Instance { get; private set; }

        // 桥接状态
        private bool isBridgeActive = false;

        private void Awake()
        {
            // 单例模式
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LogBridge("NetworkManagerBridge 单例已创建");
            }
            else
            {
                LogBridge("销毁重复的NetworkManagerBridge实例");
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            if (enableEventBridge)
            {
                SetupEventBridge();
            }
        }

        #region 事件桥接

        /// <summary>
        /// 设置事件桥接
        /// </summary>
        private void SetupEventBridge()
        {
            if (PhotonNetworkAdapter.Instance == null)
            {
                LogBridge("PhotonNetworkAdapter.Instance 为空，无法设置事件桥接");
                return;
            }

            try
            {
                // 连接事件桥接
                PhotonNetworkAdapter.OnPhotonConnected += OnPhotonConnectedBridge;
                PhotonNetworkAdapter.OnPhotonDisconnected += OnPhotonDisconnectedBridge;

                // Host事件桥接
                PhotonNetworkAdapter.OnPhotonHostStarted += OnPhotonHostStartedBridge;
                PhotonNetworkAdapter.OnPhotonHostStopped += OnPhotonHostStoppedBridge;

                // 玩家事件桥接
                PhotonNetworkAdapter.OnPhotonPlayerJoined += OnPhotonPlayerJoinedBridge;
                PhotonNetworkAdapter.OnPhotonPlayerLeft += OnPhotonPlayerLeftBridge;

                // 房间事件桥接
                PhotonNetworkAdapter.OnPhotonRoomJoined += OnPhotonRoomJoinedBridge;
                PhotonNetworkAdapter.OnPhotonRoomLeft += OnPhotonRoomLeftBridge;

                isBridgeActive = true;
                LogBridge("✅ 事件桥接设置完成");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkManagerBridge] 事件桥接设置失败: {e.Message}");
            }
        }

        /// <summary>
        /// 清理事件桥接
        /// </summary>
        private void CleanupEventBridge()
        {
            if (PhotonNetworkAdapter.Instance != null && isBridgeActive)
            {
                PhotonNetworkAdapter.OnPhotonConnected -= OnPhotonConnectedBridge;
                PhotonNetworkAdapter.OnPhotonDisconnected -= OnPhotonDisconnectedBridge;
                PhotonNetworkAdapter.OnPhotonHostStarted -= OnPhotonHostStartedBridge;
                PhotonNetworkAdapter.OnPhotonHostStopped -= OnPhotonHostStoppedBridge;
                PhotonNetworkAdapter.OnPhotonPlayerJoined -= OnPhotonPlayerJoinedBridge;
                PhotonNetworkAdapter.OnPhotonPlayerLeft -= OnPhotonPlayerLeftBridge;
                PhotonNetworkAdapter.OnPhotonRoomJoined -= OnPhotonRoomJoinedBridge;
                PhotonNetworkAdapter.OnPhotonRoomLeft -= OnPhotonRoomLeftBridge;

                isBridgeActive = false;
                LogBridge("事件桥接已清理");
            }
        }

        #endregion

        #region 反射辅助方法

        /// <summary>
        /// 通过反射调用NetworkManager的静态事件
        /// </summary>
        private void InvokeNetworkManagerEvent(string eventName, params object[] args)
        {
            try
            {
                var networkManagerType = typeof(NetworkManager);
                var eventField = networkManagerType.GetField(eventName,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                if (eventField != null)
                {
                    var eventDelegate = eventField.GetValue(null) as System.Delegate;
                    if (eventDelegate != null)
                    {
                        eventDelegate.DynamicInvoke(args);
                        LogBridge($"✅ 成功调用 NetworkManager.{eventName}");
                    }
                    else
                    {
                        LogBridge($"⚠️ NetworkManager.{eventName} 没有订阅者");
                    }
                }
                else
                {
                    LogBridge($"❌ 未找到 NetworkManager.{eventName} 事件字段");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkManagerBridge] 反射调用 {eventName} 失败: {e.Message}");
            }
        }

        /// <summary>
        /// 检查NetworkManager事件是否存在订阅者
        /// </summary>
        private bool HasNetworkManagerEventSubscribers(string eventName)
        {
            try
            {
                var networkManagerType = typeof(NetworkManager);
                var eventField = networkManagerType.GetField(eventName,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                if (eventField != null)
                {
                    var eventDelegate = eventField.GetValue(null) as System.Delegate;
                    return eventDelegate != null && eventDelegate.GetInvocationList().Length > 0;
                }
            }
            catch (System.Exception e)
            {
                LogBridge($"检查事件订阅者失败: {e.Message}");
            }
            return false;
        }

        #endregion

        #region 事件桥接处理方法

        private void OnPhotonConnectedBridge()
        {
            LogBridge("🌉 桥接: Photon连接 -> NetworkManager.OnConnected");
            try
            {
                // 使用反射调用静态事件
                InvokeNetworkManagerEvent("OnConnected");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkManagerBridge] OnConnected桥接失败: {e.Message}");
            }
        }

        private void OnPhotonDisconnectedBridge()
        {
            LogBridge("🌉 桥接: Photon断开 -> NetworkManager.OnDisconnected");
            try
            {
                // 使用反射调用静态事件
                InvokeNetworkManagerEvent("OnDisconnected");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkManagerBridge] OnDisconnected桥接失败: {e.Message}");
            }
        }

        private void OnPhotonHostStartedBridge()
        {
            LogBridge("🌉 桥接: Photon Host启动 -> NetworkManager.OnHostStarted");
            try
            {
                // 使用反射调用静态事件
                InvokeNetworkManagerEvent("OnHostStarted");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkManagerBridge] OnHostStarted桥接失败: {e.Message}");
            }
        }

        private void OnPhotonHostStoppedBridge()
        {
            LogBridge("🌉 桥接: Photon Host停止 -> NetworkManager.OnHostStopped");
            try
            {
                // 使用反射调用静态事件
                InvokeNetworkManagerEvent("OnHostStopped");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkManagerBridge] OnHostStopped桥接失败: {e.Message}");
            }
        }

        private void OnPhotonPlayerJoinedBridge(ushort playerId)
        {
            LogBridge($"🌉 桥接: Photon玩家加入({playerId}) -> NetworkManager.OnPlayerJoined");
            try
            {
                // 使用反射调用静态事件
                InvokeNetworkManagerEvent("OnPlayerJoined", playerId);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkManagerBridge] OnPlayerJoined桥接失败: {e.Message}");
            }
        }

        private void OnPhotonPlayerLeftBridge(ushort playerId)
        {
            LogBridge($"🌉 桥接: Photon玩家离开({playerId}) -> NetworkManager.OnPlayerLeft");
            try
            {
                // 使用反射调用静态事件
                InvokeNetworkManagerEvent("OnPlayerLeft", playerId);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkManagerBridge] OnPlayerLeft桥接失败: {e.Message}");
            }
        }

        private void OnPhotonRoomJoinedBridge()
        {
            LogBridge("🌉 桥接: Photon房间加入 -> 额外处理");
            // 可以在这里添加额外的房间加入处理逻辑
        }

        private void OnPhotonRoomLeftBridge()
        {
            LogBridge("🌉 桥接: Photon房间离开 -> 额外处理");
            // 可以在这里添加额外的房间离开处理逻辑
        }

        #endregion

        #region 方法桥接（为其他脚本提供统一接口）

        /// <summary>
        /// 统一的StartAsHost接口
        /// </summary>
        public void StartAsHost(ushort port, string roomName, int maxPlayers)
        {
            LogBridge($"🔄 方法桥接: StartAsHost({roomName}, {maxPlayers})");

            if (usePhotonForHost && PhotonNetworkAdapter.Instance != null)
            {
                LogBridge("-> 使用Photon创建房间");
                PhotonNetworkAdapter.Instance.CreatePhotonRoom(roomName, maxPlayers);
            }
            else if (allowFallbackToRiptide && NetworkManager.Instance != null)
            {
                LogBridge("-> 回退到NetworkManager");
                NetworkManager.Instance.StartAsHost(port, roomName, maxPlayers);
            }
            else
            {
                Debug.LogError("[NetworkManagerBridge] 无可用的网络系统启动Host");
            }
        }

        /// <summary>
        /// 统一的ConnectAsClient接口
        /// </summary>
        public void ConnectAsClient(string hostIP, ushort port)
        {
            LogBridge($"🔄 方法桥接: ConnectAsClient({hostIP}, {port})");

            if (usePhotonForClient && PhotonNetworkAdapter.Instance != null)
            {
                LogBridge("-> 使用Photon加入房间");
                PhotonNetworkAdapter.Instance.JoinPhotonRoom();
            }
            else if (allowFallbackToRiptide && NetworkManager.Instance != null)
            {
                LogBridge("-> 回退到NetworkManager");
                NetworkManager.Instance.ConnectAsClient(hostIP, port);
            }
            else
            {
                Debug.LogError("[NetworkManagerBridge] 无可用的网络系统连接Client");
            }
        }

        /// <summary>
        /// 统一的Disconnect接口
        /// </summary>
        public void Disconnect()
        {
            LogBridge("🔄 方法桥接: Disconnect()");

            if (PhotonNetworkAdapter.Instance != null && PhotonNetworkAdapter.Instance.IsInPhotonRoom)
            {
                LogBridge("-> 使用Photon断开连接");
                PhotonNetworkAdapter.Instance.LeavePhotonRoom();
            }
            else if (NetworkManager.Instance != null)
            {
                LogBridge("-> 使用NetworkManager断开连接");
                NetworkManager.Instance.Disconnect();
            }
        }

        /// <summary>
        /// 统一的网络状态检查
        /// </summary>
        public bool IsHost()
        {
            if (PhotonNetworkAdapter.Instance != null && PhotonNetworkAdapter.Instance.IsInPhotonRoom)
            {
                return PhotonNetworkAdapter.Instance.IsPhotonMasterClient;
            }
            else if (NetworkManager.Instance != null)
            {
                return NetworkManager.Instance.IsHost;
            }
            return false;
        }

        /// <summary>
        /// 统一的连接状态检查
        /// </summary>
        public bool IsConnected()
        {
            if (PhotonNetworkAdapter.Instance != null)
            {
                return PhotonNetworkAdapter.Instance.IsInPhotonRoom;
            }
            else if (NetworkManager.Instance != null)
            {
                return NetworkManager.Instance.IsConnected;
            }
            return false;
        }

        /// <summary>
        /// 统一的客户端ID获取
        /// </summary>
        public ushort GetClientId()
        {
            if (PhotonNetworkAdapter.Instance != null && PhotonNetworkAdapter.Instance.IsInPhotonRoom)
            {
                return PhotonNetworkAdapter.Instance.PhotonClientId;
            }
            else if (NetworkManager.Instance != null)
            {
                return NetworkManager.Instance.ClientId;
            }
            return 0;
        }

        /// <summary>
        /// 统一的房间信息获取
        /// </summary>
        public string GetRoomInfo()
        {
            if (PhotonNetworkAdapter.Instance != null && PhotonNetworkAdapter.Instance.IsInPhotonRoom)
            {
                return $"[Photon] {PhotonNetworkAdapter.Instance.GetPhotonStatus()}";
            }
            else if (NetworkManager.Instance != null)
            {
                return $"[Riptide] {NetworkManager.Instance.GetRoomInfo()}";
            }
            return "未连接到任何网络";
        }

        #endregion

        #region 公共控制接口

        /// <summary>
        /// 启用/禁用事件桥接
        /// </summary>
        public void SetEventBridgeEnabled(bool enabled)
        {
            if (enabled && !isBridgeActive)
            {
                SetupEventBridge();
            }
            else if (!enabled && isBridgeActive)
            {
                CleanupEventBridge();
            }
            enableEventBridge = enabled;
            LogBridge($"事件桥接 {(enabled ? "启用" : "禁用")}");
        }

        /// <summary>
        /// 设置网络模式偏好
        /// </summary>
        public void SetNetworkModePreference(bool usePhotonForHost, bool usePhotonForClient, bool allowFallback)
        {
            this.usePhotonForHost = usePhotonForHost;
            this.usePhotonForClient = usePhotonForClient;
            this.allowFallbackToRiptide = allowFallback;

            LogBridge($"网络模式偏好已更新 - Host:{usePhotonForHost}, Client:{usePhotonForClient}, Fallback:{allowFallback}");
        }

        /// <summary>
        /// 获取当前使用的网络系统
        /// </summary>
        public string GetActiveNetworkSystem()
        {
            if (PhotonNetworkAdapter.Instance != null && PhotonNetworkAdapter.Instance.IsInPhotonRoom)
            {
                return "Photon";
            }
            else if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
            {
                return "Riptide";
            }
            return "None";
        }

        #endregion

        #region Unity事件

        private void OnDestroy()
        {
            CleanupEventBridge();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region 调试方法

        /// <summary>
        /// 桥接日志
        /// </summary>
        private void LogBridge(string message)
        {
            if (debugBridgeCalls)
            {
                Debug.Log($"[NetworkManagerBridge] {message}");
            }
        }

        [ContextMenu("显示桥接状态")]
        public void ShowBridgeStatus()
        {
            string status = "=== NetworkManagerBridge 状态 ===\n";
            status += $"事件桥接: {(isBridgeActive ? "✅ 活跃" : "❌ 非活跃")}\n";
            status += $"方法桥接: {(enableMethodBridge ? "✅ 启用" : "❌ 禁用")}\n";
            status += $"当前网络系统: {GetActiveNetworkSystem()}\n";
            status += $"Host模式偏好: {(usePhotonForHost ? "Photon" : "Riptide")}\n";
            status += $"Client模式偏好: {(usePhotonForClient ? "Photon" : "Riptide")}\n";
            status += $"允许回退: {allowFallbackToRiptide}\n";
            status += $"连接状态: {IsConnected()}\n";
            status += $"Host状态: {IsHost()}\n";
            status += $"客户端ID: {GetClientId()}\n";
            status += $"房间信息: {GetRoomInfo()}";

            Debug.Log(status);
        }

        [ContextMenu("测试Host桥接")]
        public void TestHostBridge()
        {
            StartAsHost(7777, "TestRoom", 4);
        }

        [ContextMenu("测试Client桥接")]
        public void TestClientBridge()
        {
            ConnectAsClient("127.0.0.1", 7777);
        }

        [ContextMenu("测试断开桥接")]
        public void TestDisconnectBridge()
        {
            Disconnect();
        }

        #endregion
    }
}