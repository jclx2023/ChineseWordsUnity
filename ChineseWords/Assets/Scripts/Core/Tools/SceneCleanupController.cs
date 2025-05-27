using UnityEngine;
using Core.Network;

namespace Core
{
    /// <summary>
    /// 场景清理控制器
    /// 处理场景切换时的组件重复问题
    /// </summary>
    public class SceneCleanupController : MonoBehaviour
    {
        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        private void Awake()
        {
            CleanupDuplicateNetworkComponents();
        }

        /// <summary>
        /// 清理重复的网络组件
        /// </summary>
        private void CleanupDuplicateNetworkComponents()
        {
            LogDebug("开始清理重复的网络组件...");

            // 检查NetworkManager
            CleanupDuplicateNetworkManagers();

            // 检查RoomManager
            CleanupDuplicateRoomManagers();

            // 检查HostGameManager
            CleanupDuplicateHostGameManagers();

            LogDebug("网络组件清理完成");
        }

        /// <summary>
        /// 清理重复的NetworkManager（双层架构：保留服务器和玩家Host）
        /// </summary>
        private void CleanupDuplicateNetworkManagers()
        {
            var allNetworkManagers = FindObjectsOfType<NetworkManager>(true);
            LogDebug($"发现 {allNetworkManagers.Length} 个NetworkManager实例");

            if (allNetworkManagers.Length <= 1)
            {
                LogDebug("NetworkManager数量正常");
                return;
            }

            if (allNetworkManagers.Length == 2)
            {
                LogDebug("发现2个NetworkManager，实施双层架构方案");

                NetworkManager serverManager = null;  // ID=0, 纯服务器
                NetworkManager playerHostManager = null;  // ID=1, 玩家Host

                // 识别和标记两个管理器
                foreach (var nm in allNetworkManagers)
                {
                    bool isInDontDestroy = IsInDontDestroyOnLoad(nm.gameObject);
                    LogDebug($"NetworkManager: {nm.name}, ClientId: {nm.ClientId}, IsHost: {nm.IsHost}, IsConnected: {nm.IsConnected}, InDontDestroy: {isInDontDestroy}");

                    if (nm.ClientId == 0)
                    {
                        serverManager = nm;
                        LogDebug($"识别为服务器Manager: {nm.name} (ClientId: {nm.ClientId})");
                    }
                    else if (nm.ClientId == 1 || (nm.IsConnected && isInDontDestroy))
                    {
                        playerHostManager = nm;
                        LogDebug($"识别为玩家Host Manager: {nm.name} (ClientId: {nm.ClientId})");
                    }
                }

                // 配置双层架构
                if (serverManager != null && playerHostManager != null)
                {
                    ConfigureDualLayerArchitecture(serverManager, playerHostManager);
                }
                else
                {
                    LogDebug("无法识别服务器和玩家Host，保持原有逻辑");
                    CleanupDuplicateNetworkManagersOriginal();
                }
            }
            else
            {
                LogDebug("NetworkManager数量超过2个，使用原有清理逻辑");
                CleanupDuplicateNetworkManagersOriginal();
            }
        }

        /// <summary>
        /// 配置双层架构
        /// </summary>
        private void ConfigureDualLayerArchitecture(NetworkManager serverManager, NetworkManager playerHostManager)
        {
            LogDebug("配置双层架构：服务器层 + 玩家层");

            // 配置服务器层 (ID=0)
            ConfigureServerLayer(serverManager);

            // 配置玩家层 (ID=1) 
            ConfigurePlayerHostLayer(playerHostManager);

            // 设置全局引用
            SetGlobalNetworkManagerReference(playerHostManager);

            LogDebug("双层架构配置完成");
        }

        /// <summary>
        /// 配置服务器层 (ID=0)
        /// </summary>
        private void ConfigureServerLayer(NetworkManager serverManager)
        {
            LogDebug("配置服务器层 (ID=0)");

            // 服务器层：负责游戏逻辑，不参与UI展示
            var serverObj = serverManager.gameObject;

            // 添加服务器标记
            var serverMarker = serverObj.GetComponent<ServerLayerMarker>();
            if (serverMarker == null)
            {
                serverMarker = serverObj.AddComponent<ServerLayerMarker>();
            }

            // 确保服务器层保持活跃但不显示UI
            serverObj.SetActive(true);

            LogDebug("服务器层配置完成");
        }

        /// <summary>
        /// 配置玩家Host层 (ID=1)
        /// </summary>
        private void ConfigurePlayerHostLayer(NetworkManager playerHostManager)
        {
            LogDebug("配置玩家Host层 (ID=1)");

            // 玩家Host层：参与游戏，显示UI
            var playerHostObj = playerHostManager.gameObject;

            // 添加玩家Host标记
            var playerHostMarker = playerHostObj.GetComponent<PlayerHostLayerMarker>();
            if (playerHostMarker == null)
            {
                playerHostMarker = playerHostObj.AddComponent<PlayerHostLayerMarker>();
            }

            // 确保玩家Host层保持活跃
            playerHostObj.SetActive(true);

            LogDebug("玩家Host层配置完成");
        }

        /// <summary>
        /// 设置全局NetworkManager引用为玩家Host
        /// </summary>
        private void SetGlobalNetworkManagerReference(NetworkManager playerHostManager)
        {
            LogDebug("设置全局NetworkManager引用为玩家Host");

            // 确保NetworkManager.Instance指向玩家Host
            UpdateNetworkManagerInstance(playerHostManager);
        }

        /// <summary>
        /// 原有的清理逻辑（作为备用）
        /// </summary>
        private void CleanupDuplicateNetworkManagersOriginal()
        {
            var allNetworkManagers = FindObjectsOfType<NetworkManager>(true);

            NetworkManager keepInstance = null;
            var duplicates = new System.Collections.Generic.List<NetworkManager>();

            // 优先保留Host
            foreach (var nm in allNetworkManagers)
            {
                if (nm.IsHost && keepInstance == null)
                {
                    keepInstance = nm;
                    LogDebug($"保留Host NetworkManager: {nm.name} (ClientId: {nm.ClientId})");
                }
                else
                {
                    duplicates.Add(nm);
                }
            }

            // 如果没有Host，保留连接的
            if (keepInstance == null)
            {
                foreach (var nm in allNetworkManagers)
                {
                    if (nm.IsConnected)
                    {
                        keepInstance = nm;
                        duplicates.Remove(nm);
                        LogDebug($"保留连接的NetworkManager: {nm.name} (ClientId: {nm.ClientId})");
                        break;
                    }
                }
            }

            // 销毁重复实例
            foreach (var duplicate in duplicates)
            {
                LogDebug($"销毁重复NetworkManager: {duplicate.name} (ClientId: {duplicate.ClientId})");
                Destroy(duplicate.gameObject);
            }
        }

        /// <summary>
        /// 更新NetworkManager单例引用
        /// </summary>
        private void UpdateNetworkManagerInstance(NetworkManager newInstance)
        {
            // 这需要NetworkManager类提供一个更新Instance的方法
            // 或者通过反射来更新私有字段
            try
            {
                var field = typeof(NetworkManager).GetField("Instance",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                if (field != null)
                {
                    field.SetValue(null, newInstance);
                    LogDebug($"成功更新NetworkManager.Instance到: {newInstance.name}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"更新NetworkManager.Instance失败: {e.Message}");
            }
        }

        /// <summary>
        /// 清理重复的RoomManager
        /// </summary>
        private void CleanupDuplicateRoomManagers()
        {
            var allRoomManagers = FindObjectsOfType<RoomManager>(true);
            LogDebug($"发现 {allRoomManagers.Length} 个RoomManager实例");

            if (allRoomManagers.Length <= 1) return;

            // 保留DontDestroyOnLoad中的实例
            RoomManager keepInstance = null;
            foreach (var rm in allRoomManagers)
            {
                if (IsInDontDestroyOnLoad(rm.gameObject))
                {
                    keepInstance = rm;
                    break;
                }
            }

            // 销毁重复实例
            foreach (var rm in allRoomManagers)
            {
                if (rm != keepInstance)
                {
                    LogDebug($"销毁重复RoomManager: {rm.name}");
                    Destroy(rm.gameObject);
                }
            }
        }

        /// <summary>
        /// 清理重复的HostGameManager
        /// </summary>
        private void CleanupDuplicateHostGameManagers()
        {
            var allHostManagers = FindObjectsOfType<HostGameManager>(true);
            LogDebug($"发现 {allHostManagers.Length} 个HostGameManager实例");

            if (allHostManagers.Length <= 1) return;

            // 保留当前场景中的实例（非DontDestroyOnLoad）
            HostGameManager keepInstance = null;
            foreach (var hm in allHostManagers)
            {
                if (!IsInDontDestroyOnLoad(hm.gameObject))
                {
                    keepInstance = hm;
                    break;
                }
            }

            // 销毁重复实例
            foreach (var hm in allHostManagers)
            {
                if (hm != keepInstance)
                {
                    LogDebug($"销毁重复HostGameManager: {hm.name}");
                    Destroy(hm.gameObject);
                }
            }
        }

        /// <summary>
        /// 检查GameObject是否在DontDestroyOnLoad中
        /// </summary>
        private bool IsInDontDestroyOnLoad(GameObject obj)
        {
            if (obj == null) return false;

            GameObject temp = new GameObject("TempChecker");
            DontDestroyOnLoad(temp);
            bool isInDontDestroy = obj.scene == temp.scene;
            DestroyImmediate(temp);

            return isInDontDestroy;
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[SceneCleanup] {message}");
            }
        }

        /// <summary>
        /// 显示当前网络组件状态
        /// </summary>
        [ContextMenu("显示网络组件状态")]
        public void ShowNetworkComponentStatus()
        {
            LogDebug("=== 网络组件状态 ===");

            var networkManagers = FindObjectsOfType<NetworkManager>(true);
            LogDebug($"NetworkManager实例数: {networkManagers.Length}");
            foreach (var nm in networkManagers)
            {
                LogDebug($"  - {nm.name}: ClientId={nm.ClientId}, Connected={nm.IsConnected}, DontDestroy={IsInDontDestroyOnLoad(nm.gameObject)}");
            }

            var roomManagers = FindObjectsOfType<RoomManager>(true);
            LogDebug($"RoomManager实例数: {roomManagers.Length}");
            foreach (var rm in roomManagers)
            {
                LogDebug($"  - {rm.name}: DontDestroy={IsInDontDestroyOnLoad(rm.gameObject)}");
            }

            var hostManagers = FindObjectsOfType<HostGameManager>(true);
            LogDebug($"HostGameManager实例数: {hostManagers.Length}");
            foreach (var hm in hostManagers)
            {
                LogDebug($"  - {hm.name}: DontDestroy={IsInDontDestroyOnLoad(hm.gameObject)}");
            }

            if (NetworkManager.Instance != null)
            {
                LogDebug($"当前NetworkManager.Instance: {NetworkManager.Instance.name} (ClientId: {NetworkManager.Instance.ClientId})");
            }
        }
    }
}