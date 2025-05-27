using UnityEngine;
using Core.Network;
using UI;
using System.Collections;

namespace Core
{
    /// <summary>
    /// 修复后的场景模式控制器
    /// 根据游戏模式自动激活/禁用相应组件，支持DontDestroyOnLoad引用查找
    /// </summary>
    public class SceneModeController : MonoBehaviour
    {
        [Header("组件引用")]
        [SerializeField] private GameObject networkManager;
        [SerializeField] private GameObject hostGameManager;
        [SerializeField] private GameObject networkCanvas;
        [SerializeField] private GameObject gameCanvas;
        [SerializeField] private NetworkUI networkUI;

        [Header("引用查找设置")]
        [SerializeField] private bool autoFindReferences = true;
        [SerializeField] private bool searchInDontDestroy = true;
        [SerializeField] private float referenceSearchTimeout = 5f;
        [SerializeField] private string[] networkManagerNames = { "NetworkManager", "Network", "NetworkSystem" };
        [SerializeField] private string[] gameCanvasNames = { "GameCanvas", "Canvas", "MainCanvas" };

        [Header("调试设置")]
        [SerializeField] private bool showDebugLogs = true;

        // 引用查找状态
        private bool referencesFound = false;
        private bool configurationCompleted = false;

        private void Start()
        {
            if (autoFindReferences)
            {
                StartCoroutine(FindReferencesAndConfigureCoroutine());
            }
            else
            {
                ConfigureSceneBasedOnGameMode();
            }
        }

        /// <summary>
        /// 查找引用并配置场景的协程
        /// </summary>
        private IEnumerator FindReferencesAndConfigureCoroutine()
        {
            LogDebug("开始查找组件引用...");

            float startTime = Time.time;

            while (Time.time - startTime < referenceSearchTimeout)
            {
                if (TryFindAllReferences())
                {
                    LogDebug("所有引用查找成功");
                    referencesFound = true;
                    break;
                }

                yield return new WaitForSeconds(0.1f);
            }

            if (!referencesFound)
            {
                LogDebug("引用查找超时，使用找到的部分引用");
                ShowMissingReferences();
            }

            // 无论是否找全引用，都尝试配置场景
            ConfigureSceneBasedOnGameMode();
            configurationCompleted = true;
        }

        /// <summary>
        /// 尝试查找所有必要的引用
        /// </summary>
        private bool TryFindAllReferences()
        {
            bool foundAll = true;

            // 查找 NetworkManager
            if (networkManager == null)
            {
                networkManager = FindGameObjectByNames(networkManagerNames);
                if (networkManager != null)
                    LogDebug($"找到 NetworkManager: {networkManager.name}");
                else
                    foundAll = false;
            }

            // 查找 HostGameManager
            if (hostGameManager == null)
            {
                var hostComponent = SafeFindComponent<HostGameManager>();
                if (hostComponent != null)
                {
                    hostGameManager = hostComponent.gameObject;
                    LogDebug($"找到 HostGameManager: {hostGameManager.name}");
                }
                else
                    foundAll = false;
            }

            // 查找 GameCanvas
            if (gameCanvas == null)
            {
                gameCanvas = FindGameObjectByNames(gameCanvasNames);
                if (gameCanvas != null)
                    LogDebug($"找到 GameCanvas: {gameCanvas.name}");
                else
                    foundAll = false;
            }

            // 查找 NetworkCanvas（通常在DontDestroyOnLoad中）
            if (networkCanvas == null)
            {
                networkCanvas = SafeFindGameObject("NetworkCanvas");
                if (networkCanvas == null)
                    networkCanvas = SafeFindGameObject("NetworkUI");
                if (networkCanvas != null)
                    LogDebug($"找到 NetworkCanvas: {networkCanvas.name}");
                else
                    foundAll = false;
            }

            // 查找 NetworkUI 组件
            if (networkUI == null)
            {
                networkUI = SafeFindComponent<NetworkUI>();
                if (networkUI != null)
                    LogDebug($"找到 NetworkUI: {networkUI.name}");
                else
                    foundAll = false;
            }

            return foundAll;
        }

        /// <summary>
        /// 按多个可能的名称查找GameObject
        /// </summary>
        private GameObject FindGameObjectByNames(string[] possibleNames)
        {
            foreach (string name in possibleNames)
            {
                GameObject obj = SafeFindGameObject(name);
                if (obj != null)
                    return obj;
            }
            return null;
        }

        /// <summary>
        /// 安全查找GameObject（包括DontDestroyOnLoad）
        /// </summary>
        private GameObject SafeFindGameObject(string name)
        {
            // 1. 先在当前场景查找
            GameObject obj = GameObject.Find(name);
            if (obj != null)
                return obj;

            // 2. 如果启用，在DontDestroyOnLoad中查找
            if (searchInDontDestroy)
            {
                obj = FindGameObjectInDontDestroy(name);
            }

            return obj;
        }

        /// <summary>
        /// 安全查找组件（包括DontDestroyOnLoad）
        /// </summary>
        private T SafeFindComponent<T>() where T : Component
        {
            T component = null;

            // 1. 先在当前场景查找
            component = FindObjectOfType<T>();
            if (component != null)
                return component;

            // 2. 如果启用，在DontDestroyOnLoad中查找
            if (searchInDontDestroy)
            {
                component = FindComponentInDontDestroy<T>();
            }

            return component;
        }

        /// <summary>
        /// 在DontDestroyOnLoad中查找GameObject
        /// </summary>
        private GameObject FindGameObjectInDontDestroy(string name)
        {
            GameObject[] rootObjects = GetDontDestroyOnLoadObjects();

            foreach (var rootObj in rootObjects)
            {
                if (rootObj.name == name)
                    return rootObj;

                // 递归查找子对象
                Transform found = rootObj.transform.Find(name);
                if (found != null)
                    return found.gameObject;

                // 深度查找
                Transform deepFound = FindChildRecursive(rootObj.transform, name);
                if (deepFound != null)
                    return deepFound.gameObject;
            }

            return null;
        }

        /// <summary>
        /// 在DontDestroyOnLoad中查找组件
        /// </summary>
        private T FindComponentInDontDestroy<T>() where T : Component
        {
            GameObject[] rootObjects = GetDontDestroyOnLoadObjects();

            foreach (var rootObj in rootObjects)
            {
                T component = rootObj.GetComponentInChildren<T>(true);
                if (component != null)
                    return component;
            }

            return null;
        }

        /// <summary>
        /// 获取DontDestroyOnLoad场景中的所有根对象
        /// </summary>
        private GameObject[] GetDontDestroyOnLoadObjects()
        {
            // 创建一个临时对象并移动到DontDestroyOnLoad
            GameObject temp = new GameObject("TempFinder");
            DontDestroyOnLoad(temp);

            // 获取DontDestroyOnLoad场景
            UnityEngine.SceneManagement.Scene dontDestroyScene = temp.scene;

            // 获取场景中的所有根对象
            GameObject[] rootObjects = dontDestroyScene.GetRootGameObjects();

            // 销毁临时对象
            DestroyImmediate(temp);

            return rootObjects;
        }

        /// <summary>
        /// 递归查找子对象
        /// </summary>
        private Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child;

                Transform found = FindChildRecursive(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// 显示缺失的引用
        /// </summary>
        private void ShowMissingReferences()
        {
            LogDebug("=== 缺失的引用 ===");
            if (networkManager == null)
                LogDebug("- 缺少 NetworkManager");
            if (hostGameManager == null)
                LogDebug("- 缺少 HostGameManager");
            if (networkCanvas == null)
                LogDebug("- 缺少 NetworkCanvas");
            if (gameCanvas == null)
                LogDebug("- 缺少 GameCanvas");
            if (networkUI == null)
                LogDebug("- 缺少 NetworkUI");
        }

        /// <summary>
        /// 根据游戏模式配置场景
        /// </summary>
        private void ConfigureSceneBasedOnGameMode()
        {
            var gameMode = MainMenuManager.SelectedGameMode;
            LogDebug($"配置场景模式: {gameMode}");

            switch (gameMode)
            {
                case MainMenuManager.GameMode.SinglePlayer:
                    ConfigureSinglePlayerMode();
                    break;

                case MainMenuManager.GameMode.Host:
                    ConfigureHostMode();
                    break;

                case MainMenuManager.GameMode.Client:
                    ConfigureClientMode();
                    break;

                default:
                    Debug.LogWarning($"未知游戏模式: {gameMode}，使用单机模式配置");
                    ConfigureSinglePlayerMode();
                    break;
            }

            LogDebug("场景配置完成");
        }

        /// <summary>
        /// 配置单机模式
        /// </summary>
        private void ConfigureSinglePlayerMode()
        {
            LogDebug("配置单机模式");

            // 网络组件
            SetGameObjectActive(networkManager, false);
            SetGameObjectActive(hostGameManager, false);

            // UI组件
            SetGameObjectActive(networkCanvas, false);
            SetGameObjectActive(gameCanvas, true);

            // 隐藏网络UI
            if (networkUI != null)
            {
                networkUI.HideNetworkPanel();
                LogDebug("网络UI已隐藏");
            }
        }

        /// <summary>
        /// 配置主机模式
        /// </summary>
        private void ConfigureHostMode()
        {
            LogDebug("配置主机模式");

            // 网络组件
            SetGameObjectActive(networkManager, true);
            SetGameObjectActive(hostGameManager, true);

            // UI组件
            SetGameObjectActive(networkCanvas, true);
            SetGameObjectActive(gameCanvas, true);

            // 显示网络UI
            if (networkUI != null)
            {
                networkUI.ShowNetworkPanel();
                LogDebug("网络UI已显示");
            }
        }

        /// <summary>
        /// 配置客户端模式
        /// </summary>
        private void ConfigureClientMode()
        {
            LogDebug("配置客户端模式");

            // 网络组件
            SetGameObjectActive(networkManager, true);
            SetGameObjectActive(hostGameManager, false);

            // UI组件
            SetGameObjectActive(networkCanvas, true);
            SetGameObjectActive(gameCanvas, true);

            // 显示网络UI
            if (networkUI != null)
            {
                networkUI.ShowNetworkPanel();
                LogDebug("网络UI已显示");
            }
        }

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
            else
            {
                LogDebug($"GameObject引用为空，无法设置激活状态");
            }
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[SceneModeController] {message}");
            }
        }

        /// <summary>
        /// 动态切换模式（用于运行时测试）
        /// </summary>
        [ContextMenu("重新配置场景")]
        public void ReconfigureScene()
        {
            ConfigureSceneBasedOnGameMode();
        }

        /// <summary>
        /// 强制重新查找引用
        /// </summary>
        [ContextMenu("重新查找引用")]
        public void RefreshReferences()
        {
            referencesFound = false;
            configurationCompleted = false;
            StartCoroutine(FindReferencesAndConfigureCoroutine());
        }

        /// <summary>
        /// 显示当前引用状态
        /// </summary>
        [ContextMenu("显示引用状态")]
        public void ShowReferenceStatus()
        {
            LogDebug("=== 当前引用状态 ===");
            LogDebug($"NetworkManager: {(networkManager != null ? "✓ " + networkManager.name : "✗")}");
            LogDebug($"HostGameManager: {(hostGameManager != null ? "✓ " + hostGameManager.name : "✗")}");
            LogDebug($"NetworkCanvas: {(networkCanvas != null ? "✓ " + networkCanvas.name : "✗")}");
            LogDebug($"GameCanvas: {(gameCanvas != null ? "✓ " + gameCanvas.name : "✗")}");
            LogDebug($"NetworkUI: {(networkUI != null ? "✓ " + networkUI.name : "✗")}");
            LogDebug($"引用查找完成: {(referencesFound ? "✓" : "✗")}");
            LogDebug($"配置完成: {(configurationCompleted ? "✓" : "✗")}");
        }

        /// <summary>
        /// 获取配置状态信息
        /// </summary>
        public string GetStatusInfo()
        {
            return $"ReferencesFound: {referencesFound}, " +
                   $"ConfigurationCompleted: {configurationCompleted}, " +
                   $"GameMode: {MainMenuManager.SelectedGameMode}, " +
                   $"NetworkManager: {networkManager != null}, " +
                   $"HostGameManager: {hostGameManager != null}, " +
                   $"GameCanvas: {gameCanvas != null}, " +
                   $"NetworkCanvas: {networkCanvas != null}, " +
                   $"NetworkUI: {networkUI != null}";
        }

        /// <summary>
        /// 手动设置引用（用于运行时或特殊情况）
        /// </summary>
        public void SetReferences(GameObject netManager = null,
                                GameObject hostManager = null,
                                GameObject netCanvas = null,
                                GameObject gCanvas = null,
                                NetworkUI netUI = null)
        {
            if (netManager != null) networkManager = netManager;
            if (hostManager != null) hostGameManager = hostManager;
            if (netCanvas != null) networkCanvas = netCanvas;
            if (gCanvas != null) gameCanvas = gCanvas;
            if (netUI != null) networkUI = netUI;

            LogDebug("手动设置引用完成");

            // 重新配置场景
            ConfigureSceneBasedOnGameMode();
        }

        /// <summary>
        /// 验证组件是否在DontDestroyOnLoad中
        /// </summary>
        private bool IsInDontDestroyOnLoad(GameObject obj)
        {
            if (obj == null) return false;

            // 创建临时对象获取DontDestroyOnLoad场景
            GameObject temp = new GameObject("TempChecker");
            DontDestroyOnLoad(temp);
            bool isInDontDestroy = obj.scene == temp.scene;
            DestroyImmediate(temp);

            return isInDontDestroy;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 编辑器中显示详细的场景信息
        /// </summary>
        [ContextMenu("显示场景详细信息")]
        public void ShowDetailedSceneInfo()
        {
            LogDebug("=== 详细场景信息 ===");

            var allObjects = FindObjectsOfType<GameObject>();
            LogDebug($"当前场景总对象数: {allObjects.Length}");

            var dontDestroyObjects = GetDontDestroyOnLoadObjects();
            LogDebug($"DontDestroyOnLoad对象数: {dontDestroyObjects.Length}");

            LogDebug("DontDestroyOnLoad中的对象:");
            foreach (var obj in dontDestroyObjects)
            {
                LogDebug($"  - {obj.name}");
            }

            // 检查当前引用的对象是否在DontDestroyOnLoad中
            if (networkManager != null)
                LogDebug($"NetworkManager在DontDestroy中: {IsInDontDestroyOnLoad(networkManager)}");
            if (gameCanvas != null)
                LogDebug($"GameCanvas在DontDestroy中: {IsInDontDestroyOnLoad(gameCanvas)}");
        }
#endif
    }
}