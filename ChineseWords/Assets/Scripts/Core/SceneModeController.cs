using UnityEngine;
using Core.Network;
using UI;
using System.Collections;

namespace Core
{
    /// <summary>
    /// 修复后的场景模式控制器
    /// 核心修复：Client端只禁用HostGameManager组件，保留其他题目处理组件
    /// </summary>
    public class SceneModeController : MonoBehaviour
    {
        [Header("组件引用")]
        [SerializeField] private GameObject networkManager;
        [SerializeField] private GameObject gameControllers;           // 整个GameControllers GameObject
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

        // 运行时找到的组件引用
        private HostGameManager hostGameManagerComponent;

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
        /// 查找引用并配置场景的协程（修复引用失效问题）
        /// </summary>
        private IEnumerator FindReferencesAndConfigureCoroutine()
        {
            yield return new WaitForSeconds(0.5f);
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

            // 关键修复：等待组件可能的DontDestroyOnLoad移动完成
            LogDebug("等待组件DontDestroyOnLoad移动完成...");
            yield return new WaitForSeconds(0.5f);

            // 验证引用是否仍然有效
            bool needRefresh = ValidateReferences();
            if (needRefresh)
            {
                LogDebug("检测到引用失效，重新查找...");
                yield return StartCoroutine(RefreshInvalidReferences());
            }

            // 配置场景
            ConfigureSceneBasedOnGameMode();
            configurationCompleted = true;

            // 启动定期验证（防止后续失效）
            StartCoroutine(PeriodicReferenceValidation());
        }

        /// <summary>
        /// 尝试查找所有必要的引用
        /// </summary>
        private bool TryFindAllReferences()
        {
            bool foundAll = true;

            // 查找 NetworkManager（优先从DontDestroyOnLoad查找）
            if (networkManager == null)
            {
                networkManager = FindGameObjectInDontDestroy("NetworkManager");
                if (networkManager == null)
                {
                    networkManager = FindGameObjectByNames(networkManagerNames);
                }

                if (networkManager != null)
                    LogDebug($"找到 NetworkManager: {networkManager.name} (在DontDestroy: {IsInDontDestroyOnLoad(networkManager)})");
                else
                    foundAll = false;
            }

            // 查找 GameControllers
            if (gameControllers == null)
            {
                gameControllers = SafeFindGameObject("GameControllers");
                if (gameControllers != null)
                    LogDebug($"找到 GameControllers: {gameControllers.name}");
                else
                    foundAll = false;
            }

            // 运行时查找 HostGameManager 组件（从GameControllers中获取）
            if (hostGameManagerComponent == null && gameControllers != null)
            {
                hostGameManagerComponent = gameControllers.GetComponent<HostGameManager>();
                if (hostGameManagerComponent != null)
                    LogDebug($"从GameControllers找到 HostGameManager组件");
                else
                {
                    // 如果GameControllers上没有，尝试在子对象中查找
                    hostGameManagerComponent = gameControllers.GetComponentInChildren<HostGameManager>();
                    if (hostGameManagerComponent != null)
                        LogDebug($"从GameControllers子对象找到 HostGameManager组件");
                }
            }

            // 如果还没找到，全局查找
            if (hostGameManagerComponent == null)
            {
                hostGameManagerComponent = SafeFindComponent<HostGameManager>();
                if (hostGameManagerComponent != null)
                    LogDebug($"全局找到 HostGameManager组件: {hostGameManagerComponent.name}");
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

            // 查找 NetworkCanvas
            if (networkCanvas == null)
            {
                networkCanvas = FindGameObjectInDontDestroy("NetworkCanvas");
                if (networkCanvas == null)
                    networkCanvas = FindGameObjectInDontDestroy("NetworkUI");
                if (networkCanvas == null)
                    networkCanvas = SafeFindGameObject("NetworkCanvas");

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

            // 配置完成后，确保NQMC正确启动
            StartCoroutine(EnsureNQMCStarted());
        }

        /// <summary>
        /// 配置单机模式
        /// </summary>
        private void ConfigureSinglePlayerMode()
        {
            LogDebug("配置单机模式");

            // 网络组件
            SetGameObjectActive(networkManager, false);
            SetComponentEnabled(hostGameManagerComponent, false);

            // UI组件
            SetGameObjectActive(networkCanvas, false);
            SetGameObjectActive(gameCanvas, true);

            // 确保GameControllers保持活跃（单机也需要题目管理器）
            SetGameObjectActive(gameControllers, true);

            // 隐藏网络UI
            if (networkUI != null)
            {
                networkUI.HideNetworkPanel();
                LogDebug("网络UI已隐藏");
            }
        }

        /// <summary>
        /// 配置主机模式（双层架构适配）
        /// </summary>
        private void ConfigureHostMode()
        {
            LogDebug("配置主机模式");

            // 检查是否为双层架构
            var allNetworkManagers = FindObjectsOfType<NetworkManager>();
            bool isDualLayer = allNetworkManagers.Length == 2;

            if (isDualLayer)
            {
                LogDebug("检测到双层架构，分别配置服务器层和玩家层");
                ConfigureDualLayerHostMode();
            }
            else
            {
                LogDebug("使用传统单层架构配置");
                ConfigureTraditionalHostMode();
            }
        }

        /// <summary>
        /// 配置双层架构的Host模式
        /// </summary>
        private void ConfigureDualLayerHostMode()
        {
            // 找到并配置服务器层
            var serverLayer = FindObjectOfType<ServerLayerMarker>();
            if (serverLayer != null)
            {
                var serverHostManager = serverLayer.GetComponent<HostGameManager>();
                if (serverHostManager != null)
                {
                    serverHostManager.enabled = true;
                    LogDebug("启用服务器层HostGameManager");
                }
            }

            // 找到并配置玩家Host层
            var playerHostLayer = FindObjectOfType<PlayerHostLayerMarker>();
            if (playerHostLayer != null)
            {
                // 玩家Host层不需要HostGameManager，只需要NQMC等
                var playerHostManager = playerHostLayer.GetComponent<HostGameManager>();
                if (playerHostManager != null)
                {
                    playerHostManager.enabled = false;
                    LogDebug("禁用玩家Host层HostGameManager（由服务器层处理）");
                }
            }

            // UI和其他组件配置
            ConfigureUIForDualLayer();
        }

        /// <summary>
        /// 配置传统Host模式
        /// </summary>
        private void ConfigureTraditionalHostMode()
        {
            // 网络组件
            SetGameObjectActive(networkManager, true);
            SetComponentEnabled(hostGameManagerComponent, true);

            // 确保GameControllers保持活跃
            SetGameObjectActive(gameControllers, true);

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
        /// 为双层架构配置UI
        /// </summary>
        private void ConfigureUIForDualLayer()
        {
            // 确保GameControllers保持活跃（两层都需要）
            SetGameObjectActive(gameControllers, true);

            // UI组件
            SetGameObjectActive(networkCanvas, true);
            SetGameObjectActive(gameCanvas, true);

            // 显示网络UI
            if (networkUI != null)
            {
                networkUI.ShowNetworkPanel();
                LogDebug("双层架构：网络UI已显示");
            }
        }

        /// <summary>
        /// 配置客户端模式（关键修复）
        /// </summary>
        private void ConfigureClientMode()
        {
            LogDebug("配置客户端模式");

            // 网络组件 - 保持NetworkManager活跃
            SetGameObjectActive(networkManager, true);

            // 关键修复：只禁用HostGameManager组件，不禁用GameControllers
            SetComponentEnabled(hostGameManagerComponent, false);
            LogDebug("禁用HostGameManager组件");

            // 确保GameControllers保持活跃（客户端需要NQMC等组件）
            SetGameObjectActive(gameControllers, true);
            LogDebug("保持GameControllers活跃以支持题目接收和显示");

            // UI组件
            SetGameObjectActive(networkCanvas, true);
            SetGameObjectActive(gameCanvas, true);

            // 显示网络UI
            if (networkUI != null)
            {
                networkUI.ShowNetworkPanel();
                LogDebug("网络UI已显示");
            }

            LogDebug("客户端模式配置完成 - 保留题目管理功能");
        }

        /// <summary>
        /// 确保NQMC正确启动
        /// </summary>
        private IEnumerator EnsureNQMCStarted()
        {
            // 等待组件初始化
            yield return new WaitForSeconds(0.5f);

            var nqmc = NetworkQuestionManagerController.Instance;
            if (nqmc != null)
            {
                var gameMode = MainMenuManager.SelectedGameMode;

                if (!nqmc.IsGameStarted)
                {
                    switch (gameMode)
                    {
                        case MainMenuManager.GameMode.SinglePlayer:
                            LogDebug("启动NQMC单机模式");
                            nqmc.StartGame(false);
                            break;

                        case MainMenuManager.GameMode.Host:
                        case MainMenuManager.GameMode.Client:
                            LogDebug("启动NQMC多人模式");
                            nqmc.StartGame(true);
                            break;
                    }
                }
                else
                {
                    LogDebug($"NQMC已启动 - 模式: {(nqmc.IsMultiplayerMode ? "多人" : "单机")}");
                }
            }
            else
            {
                Debug.LogError("找不到NetworkQuestionManagerController实例");
            }
        }

        /// <summary>
        /// 安全设置组件启用状态
        /// </summary>
        private void SetComponentEnabled(MonoBehaviour component, bool enabled)
        {
            if (component != null)
            {
                bool wasEnabled = component.enabled;
                component.enabled = enabled;
                LogDebug($"设置 {component.GetType().Name} 启用状态: {wasEnabled} → {enabled}");
            }
            else
            {
                LogDebug($"组件引用为空，无法设置启用状态");
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

        // 以下方法保持不变...

        /// <summary>
        /// 验证当前引用是否仍然有效
        /// </summary>
        private bool ValidateReferences()
        {
            bool anyInvalid = false;

            if (networkManager != null && networkManager.gameObject == null)
            {
                LogDebug("NetworkManager引用失效");
                networkManager = null;
                anyInvalid = true;
            }

            if (gameControllers != null && gameControllers.gameObject == null)
            {
                LogDebug("GameControllers引用失效");
                gameControllers = null;
                anyInvalid = true;
            }

            if (hostGameManagerComponent != null && hostGameManagerComponent.gameObject == null)
            {
                LogDebug("HostGameManager组件引用失效");
                hostGameManagerComponent = null;
                anyInvalid = true;
            }

            if (networkCanvas != null && networkCanvas.gameObject == null)
            {
                LogDebug("NetworkCanvas引用失效");
                networkCanvas = null;
                anyInvalid = true;
            }

            if (gameCanvas != null && gameCanvas.gameObject == null)
            {
                LogDebug("GameCanvas引用失效");
                gameCanvas = null;
                anyInvalid = true;
            }

            if (networkUI != null && networkUI.gameObject == null)
            {
                LogDebug("NetworkUI引用失效");
                networkUI = null;
                anyInvalid = true;
            }

            return anyInvalid;
        }

        /// <summary>
        /// 刷新失效的引用
        /// </summary>
        private IEnumerator RefreshInvalidReferences()
        {
            LogDebug("开始刷新失效的引用...");

            float refreshStartTime = Time.time;
            const float refreshTimeout = 3f;

            while (Time.time - refreshStartTime < refreshTimeout)
            {
                bool allFound = TryFindAllReferences();

                if (allFound)
                {
                    LogDebug("引用刷新成功");
                    referencesFound = true;
                    break;
                }

                yield return new WaitForSeconds(0.2f);
            }

            if (!referencesFound)
            {
                LogDebug("引用刷新超时");
                ShowMissingReferences();
            }
        }

        /// <summary>
        /// 定期验证引用有效性
        /// </summary>
        private IEnumerator PeriodicReferenceValidation()
        {
            while (this != null && gameObject.activeInHierarchy)
            {
                yield return new WaitForSeconds(2f);

                if (configurationCompleted && ValidateReferences())
                {
                    LogDebug("定期检查发现引用失效，尝试修复...");
                    yield return StartCoroutine(RefreshInvalidReferences());

                    if (referencesFound)
                    {
                        ConfigureSceneBasedOnGameMode();
                    }
                }
            }
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
        /// 强化的安全查找GameObject方法
        /// </summary>
        private GameObject SafeFindGameObject(string name)
        {
            GameObject obj = FindGameObjectInDontDestroy(name);
            if (obj != null)
                return obj;

            obj = GameObject.Find(name);
            if (obj != null)
                return obj;

            return null;
        }

        /// <summary>
        /// 安全查找组件（包括DontDestroyOnLoad）
        /// </summary>
        private T SafeFindComponent<T>() where T : Component
        {
            T component = FindComponentInDontDestroy<T>();
            if (component != null)
                return component;

            component = FindObjectOfType<T>();
            if (component != null)
                return component;

            return null;
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

                Transform found = rootObj.transform.Find(name);
                if (found != null)
                    return found.gameObject;

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
            GameObject temp = new GameObject("TempFinder");
            DontDestroyOnLoad(temp);
            UnityEngine.SceneManagement.Scene dontDestroyScene = temp.scene;
            GameObject[] rootObjects = dontDestroyScene.GetRootGameObjects();
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
            if (gameControllers == null)
                LogDebug("- 缺少 GameControllers");
            if (hostGameManagerComponent == null)
                LogDebug("- 缺少 HostGameManager组件");
            if (networkCanvas == null)
                LogDebug("- 缺少 NetworkCanvas");
            if (gameCanvas == null)
                LogDebug("- 缺少 GameCanvas");
            if (networkUI == null)
                LogDebug("- 缺少 NetworkUI");
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
        /// 验证组件是否在DontDestroyOnLoad中
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

        // 调试方法
        [ContextMenu("重新配置场景")]
        public void ReconfigureScene()
        {
            ConfigureSceneBasedOnGameMode();
        }

        [ContextMenu("强制重新查找引用")]
        public void RefreshReferences()
        {
            referencesFound = false;
            configurationCompleted = false;
            StartCoroutine(FindReferencesAndConfigureCoroutine());
        }

        [ContextMenu("显示引用状态")]
        public void ShowReferenceStatus()
        {
            LogDebug("=== 当前引用状态 ===");
            LogDebug($"NetworkManager: {(networkManager != null ? "✓ " + networkManager.name : "✗")}");
            LogDebug($"GameControllers: {(gameControllers != null ? "✓ " + gameControllers.name : "✗")}");
            LogDebug($"HostGameManager组件: {(hostGameManagerComponent != null ? "✓ " + hostGameManagerComponent.name : "✗")}");
            LogDebug($"NetworkCanvas: {(networkCanvas != null ? "✓ " + networkCanvas.name : "✗")}");
            LogDebug($"GameCanvas: {(gameCanvas != null ? "✓ " + gameCanvas.name : "✗")}");
            LogDebug($"NetworkUI: {(networkUI != null ? "✓ " + networkUI.name : "✗")}");
            LogDebug($"引用查找完成: {(referencesFound ? "✓" : "✗")}");
            LogDebug($"配置完成: {(configurationCompleted ? "✓" : "✗")}");
        }
    }
}