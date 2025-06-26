using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Network
{
    /// <summary>
    /// PersistentNetworkManager初始化器
    /// 确保只在第一次进入MainMenuScene时实例化一次PersistentNetworkManager预制体
    /// 并在整个游戏生命周期中保持持久化
    /// </summary>
[DefaultExecutionOrder(-200)]
    public class PersistentNetworkManagerInitializer : MonoBehaviour
    {
        [Header("预制体配置")]
        [SerializeField] private GameObject persistentNetworkManagerPrefab;
        [SerializeField] private string persistentNetworkManagerPrefabPath = "Prefabs/Network/PersistentNetworkManager";

        [Header("场景配置")]
        [SerializeField] private string mainMenuSceneName = "MainMenuScene";
        [SerializeField] private bool onlyInMainMenu = true;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = false;

        // 静态标记，确保全局唯一性
        private static bool hasInitialized = false;
        private static PersistentNetworkManagerInitializer instance;

        // 初始化状态
        private bool isInitializing = false;
        private bool initializationCompleted = false;

        #region Unity生命周期

        private void Awake()
        {
            // 检查是否已经有实例存在
            if (instance != null && instance != this)
            {
                LogDebug("检测到重复的初始化器，销毁当前实例");
                Destroy(gameObject);
                return;
            }

            instance = this;
            LogDebug("PersistentNetworkManagerInitializer 实例已创建");
        }

        private void Start()
        {
            // 检查是否需要初始化
            CheckAndInitialize();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        #endregion

        #region 初始化逻辑

        /// <summary>
        /// 检查并初始化PersistentNetworkManager
        /// </summary>
        private void CheckAndInitialize()
        {
            LogDebug("开始检查是否需要初始化PersistentNetworkManager");

            // 如果已经全局初始化过，直接返回
            if (hasInitialized)
            {
                LogDebug("PersistentNetworkManager已经初始化过，跳过");
                initializationCompleted = true;
                return;
            }

            // 如果正在初始化中，避免重复
            if (isInitializing)
            {
                LogDebug("正在初始化中，跳过重复调用");
                return;
            }

            // 检查当前场景是否符合初始化条件
            if (!ShouldInitializeInCurrentScene())
            {
                LogDebug($"当前场景不符合初始化条件: {SceneManager.GetActiveScene().name}");
                return;
            }

            // 检查是否已经存在PersistentNetworkManager实例
            if (IsPersistentNetworkManagerExists())
            {
                LogDebug("PersistentNetworkManager实例已存在，标记为已初始化");
                hasInitialized = true;
                initializationCompleted = true;
                return;
            }

            // 开始初始化
            StartInitialization();
        }

        /// <summary>
        /// 判断是否应该在当前场景初始化
        /// </summary>
        private bool ShouldInitializeInCurrentScene()
        {
            string currentSceneName = SceneManager.GetActiveScene().name;

            // 如果设置为只在主菜单初始化
            if (onlyInMainMenu)
            {
                return currentSceneName.Equals(mainMenuSceneName, System.StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        /// <summary>
        /// 检查是否已经存在PersistentNetworkManager实例
        /// </summary>
        private bool IsPersistentNetworkManagerExists()
        {
            // 方法1: 通过类型查找
            var existingManager = FindObjectOfType<PersistentNetworkManager>();
            if (existingManager != null)
            {
                LogDebug($"找到现有的PersistentNetworkManager实例: {existingManager.name}");
                return true;
            }

            // 方法2: 通过名称查找DontDestroyOnLoad对象
            GameObject[] dontDestroyObjects = GetDontDestroyOnLoadObjects();
            foreach (var obj in dontDestroyObjects)
            {
                if (obj.name.Contains("PersistentNetworkManager"))
                {
                    LogDebug($"在DontDestroyOnLoad中找到PersistentNetworkManager: {obj.name}");
                    return true;
                }

                // 检查子对象
                var manager = obj.GetComponentInChildren<PersistentNetworkManager>();
                if (manager != null)
                {
                    LogDebug($"在DontDestroyOnLoad对象的子对象中找到PersistentNetworkManager: {obj.name}");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取DontDestroyOnLoad场景中的所有对象
        /// </summary>
        private GameObject[] GetDontDestroyOnLoadObjects()
        {
            try
            {
                // 创建一个临时对象并移动到DontDestroyOnLoad场景
                GameObject temp = new GameObject("TempForDontDestroyCheck");
                DontDestroyOnLoad(temp);

                Scene dontDestroyScene = temp.scene;
                var rootObjects = dontDestroyScene.GetRootGameObjects();

                // 销毁临时对象
                Destroy(temp);

                return rootObjects;
            }
            catch (System.Exception e)
            {
                LogDebug($"获取DontDestroyOnLoad对象时出错: {e.Message}");
                return new GameObject[0];
            }
        }

        /// <summary>
        /// 开始初始化过程
        /// </summary>
        private void StartInitialization()
        {
            LogDebug("开始初始化PersistentNetworkManager");

            isInitializing = true;

            try
            {
                // 确保有预制体引用
                if (!EnsurePrefabReference())
                {
                    Debug.LogError("[PersistentNetworkManagerInitializer] 无法获取预制体引用，初始化失败");
                    isInitializing = false;
                    return;
                }

                // 实例化预制体
                GameObject managerInstance = InstantiatePersistentNetworkManager();
                if (managerInstance == null)
                {
                    Debug.LogError("[PersistentNetworkManagerInitializer] 预制体实例化失败");
                    isInitializing = false;
                    return;
                }

                // 标记为已初始化
                hasInitialized = true;
                initializationCompleted = true;
                isInitializing = false;

                LogDebug($"PersistentNetworkManager初始化成功: {managerInstance.name}");

                // 验证初始化结果
                ValidateInitialization(managerInstance);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PersistentNetworkManagerInitializer] 初始化过程中发生异常: {e.Message}");
                isInitializing = false;
            }
        }

        /// <summary>
        /// 确保预制体引用存在
        /// </summary>
        private bool EnsurePrefabReference()
        {
            // 如果已经有预制体引用，直接返回
            if (persistentNetworkManagerPrefab != null)
            {
                LogDebug("使用已分配的预制体引用");
                return true;
            }

            Debug.LogError($"[PersistentNetworkManagerInitializer] 无法加载预制体，请检查路径: {persistentNetworkManagerPrefabPath}");
            return false;
        }

        /// <summary>
        /// 实例化PersistentNetworkManager
        /// </summary>
        private GameObject InstantiatePersistentNetworkManager()
        {
            LogDebug("实例化PersistentNetworkManager预制体");

            try
            {
                // 实例化预制体
                GameObject instance = Instantiate(persistentNetworkManagerPrefab);
                instance.name = "PersistentNetworkManager_Instance";

                // 设置为DontDestroyOnLoad
                DontDestroyOnLoad(instance);

                LogDebug($"预制体实例化成功，已设置为DontDestroyOnLoad: {instance.name}");

                return instance;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PersistentNetworkManagerInitializer] 实例化预制体时发生异常: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 验证初始化结果
        /// </summary>
        private void ValidateInitialization(GameObject managerInstance)
        {
            LogDebug("验证PersistentNetworkManager初始化结果");

            // 检查实例是否有PersistentNetworkManager组件
            var managerComponent = managerInstance.GetComponent<PersistentNetworkManager>();
            if (managerComponent == null)
            {
                Debug.LogWarning("[PersistentNetworkManagerInitializer] 实例化的对象没有PersistentNetworkManager组件");
            }
            else
            {
                LogDebug("PersistentNetworkManager组件验证成功");
            }

            // 检查是否在DontDestroyOnLoad场景中
            if (managerInstance.scene.name == "DontDestroyOnLoad")
            {
                LogDebug("实例已正确移动到DontDestroyOnLoad场景");
            }
            else
            {
                Debug.LogWarning($"[PersistentNetworkManagerInitializer] 实例未在DontDestroyOnLoad场景中: {managerInstance.scene.name}");
            }

            // 检查是否真的只有一个实例
            var allManagers = FindObjectsOfType<PersistentNetworkManager>();
            if (allManagers.Length > 1)
            {
                Debug.LogWarning($"[PersistentNetworkManagerInitializer] 检测到多个PersistentNetworkManager实例: {allManagers.Length}");
            }
            else
            {
                LogDebug("PersistentNetworkManager实例唯一性验证通过");
            }
        }

        #endregion

        #region 场景事件处理

        private void OnEnable()
        {
            // 订阅场景加载事件
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            // 取消订阅场景加载事件
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>
        /// 场景加载完成事件处理
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            LogDebug($"场景加载完成: {scene.name}, 模式: {mode}");

            // 如果是主菜单场景且尚未初始化，尝试初始化
            if (scene.name.Equals(mainMenuSceneName, System.StringComparison.OrdinalIgnoreCase) && !hasInitialized)
            {
                LogDebug("检测到主菜单场景加载，检查初始化需求");
                CheckAndInitialize();
            }
        }

        #endregion


        #region 调试方法

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[PersistentNetworkManagerInitializer] {message}");
            }
        }

        #endregion
    }
}