using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using Core.Network;
using TMPro;
using UnityEngine.UI;
using UI.RoomConfig;

namespace Core.Network
{
    /// <summary>
    /// 简化版房间配置管理器
    /// 专注于RoomGameConfig的管理，Timer配置通过TimerConfigManager处理
    /// </summary>
    public class RoomConfigManager : MonoBehaviour
    {
        [Header("配置设置")]
        [SerializeField] private RoomGameConfig defaultConfig;

        [Header("UI设置")]
        [SerializeField] private GameObject configUIPrefab;
        [SerializeField] private KeyCode configUIHotkey = KeyCode.F1;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 单例实例
        public static RoomConfigManager Instance { get; private set; }

        // 当前配置
        private RoomGameConfig currentConfig;
        private RoomGameConfig runtimeConfig; // 运行时临时配置

        // UI相关
        private GameObject configUIInstance;
        private RoomConfigUI configUIComponent;
        private bool isUIOpen = false;

        // 属性
        public RoomGameConfig CurrentConfig => currentConfig;
        public bool IsUIOpen => isUIOpen;



        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                LogDebug("RoomConfigManager 单例已创建");

                // 不设置DontDestroyOnLoad，让它留在当前场景
                LoadDefaultConfig();
            }
            else if (Instance != this)
            {
                LogDebug("销毁重复的RoomConfigManager实例");
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            CloseConfigUI();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 加载默认配置
        /// </summary>
        private void LoadDefaultConfig()
        {
            LogDebug("加载默认配置");

            if (defaultConfig != null)
            {
                currentConfig = defaultConfig.CreateCopy();
                LogDebug($"使用预设默认配置: {currentConfig.ConfigName}");
            }
            else
            {
                // 尝试从Resources加载
                var resourceConfig = Resources.Load<RoomGameConfig>("RoomGameConfig");
                if (resourceConfig != null)
                {
                    currentConfig = resourceConfig.CreateCopy();
                    LogDebug($"从Resources加载配置: {currentConfig.ConfigName}");
                }
                else
                {
                    currentConfig = ScriptableObject.CreateInstance<RoomGameConfig>();
                    LogDebug("创建默认配置");
                }
            }

            runtimeConfig = currentConfig.CreateCopy();

            // 初始化Timer配置管理器
            InitializeTimerConfig();
        }

        /// <summary>
        /// 初始化Timer配置
        /// </summary>
        private void InitializeTimerConfig()
        {
            // 如果RoomGameConfig中设置了Timer配置，使用它
            var timerConfig = currentConfig.GetEffectiveTimerConfig();
            if (timerConfig != null)
            {
                TimerConfigManager.SetConfig(timerConfig);
                LogDebug($"Timer配置已从RoomGameConfig初始化: {timerConfig.ConfigName}");
            }
            else
            {
                LogDebug("使用TimerConfigManager的默认配置");
            }
        }

        #region RoomGameConfig管理

        /// <summary>
        /// 获取当前Timer配置
        /// </summary>
        public TimerConfig GetCurrentTimerConfig()
        {
            return runtimeConfig?.GetEffectiveTimerConfig();
        }

        /// <summary>
        /// 设置Timer配置
        /// </summary>
        public void SetTimerConfig(TimerConfig timerConfig)
        {
            if (runtimeConfig == null)
            {
                LogDebug("运行时配置为空，无法设置Timer配置");
                return;
            }

            runtimeConfig.SetTimerConfig(timerConfig);

            // 同时更新TimerConfigManager
            TimerConfigManager.SetConfig(timerConfig);

            LogDebug($"Timer配置已更新: {timerConfig?.ConfigName ?? "null"}");
        }

        /// <summary>
        /// 获取配置摘要信息
        /// </summary>
        public string GetConfigSummary()
        {
            return runtimeConfig?.GetConfigSummary() ?? "配置未加载";
        }

        #endregion

        #region 现有方法（保持不变）

        /// <summary>
        /// 检查当前玩家是否为房主
        /// </summary>
        private bool IsCurrentPlayerHost()
        {
            // 检查NetworkManager
            if (NetworkManager.Instance != null)
            {
                return NetworkManager.Instance.IsHost;
            }

            // 检查RoomManager
            if (RoomManager.Instance?.CurrentRoom != null)
            {
                var room = RoomManager.Instance.CurrentRoom;
                ushort currentPlayerId = GetCurrentPlayerId();
                return currentPlayerId != 0 && currentPlayerId == room.hostId;
            }

            // 开发模式允许
            return Application.isEditor;
        }

        /// <summary>
        /// 获取当前玩家ID
        /// </summary>
        private ushort GetCurrentPlayerId()
        {
            if (NetworkManager.Instance != null)
            {
                return NetworkManager.Instance.ClientId;
            }
            return 0;
        }

        /// <summary>
        /// 检查是否在房间场景
        /// </summary>
        private bool IsInRoomScene()
        {
            string currentScene = SceneManager.GetActiveScene().name;
            return currentScene.Contains("Room") || currentScene.Contains("Lobby");
        }

        /// <summary>
        /// 检查操作权限
        /// </summary>
        private bool CheckPermissions()
        {
            if (!IsInRoomScene())
            {
                LogDebug("当前不在房间场景，无法打开配置");
                return false;
            }

            if (!IsCurrentPlayerHost())
            {
                LogDebug("只有房主可以打开配置UI");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 处理配置UI开关
        /// </summary>
        public void HandleConfigUIToggle()
        {
            LogDebug($"检测到 {configUIHotkey} 按键");

            if (!CheckPermissions())
            {
                return;
            }

            if (isUIOpen)
            {
                CloseConfigUI();
            }
            else
            {
                OpenConfigUI();
            }
        }

        /// <summary>
        /// 打开配置UI
        /// </summary>
        public void OpenConfigUI()
        {
            if (isUIOpen)
            {
                LogDebug("配置UI已经打开");
                return;
            }

            LogDebug("打开配置UI");

            if (configUIInstance == null)
            {
                CreateConfigUIInstance();
            }

            if (configUIInstance != null)
            {
                configUIInstance.SetActive(true);
                isUIOpen = true;

                if (configUIComponent != null)
                {
                    configUIComponent.Initialize();
                }

                LogDebug($"配置UI已打开 - 场景: {configUIInstance.scene.name}");
            }
            else
            {
                LogDebug("配置UI创建失败");
            }
        }

        /// <summary>
        /// 关闭配置UI
        /// </summary>
        public void CloseConfigUI()
        {
            if (!isUIOpen)
                return;

            LogDebug("关闭配置UI");

            if (configUIInstance != null)
            {
                configUIInstance.SetActive(false);
            }

            isUIOpen = false;
        }

        /// <summary>
        /// 创建配置UI实例 - 确保在当前场景中
        /// </summary>
        private void CreateConfigUIInstance()
        {
            LogDebug("创建配置UI实例");

            if (configUIPrefab == null)
            {
                configUIPrefab = Resources.Load<GameObject>("RoomConfigUI");
                if (configUIPrefab == null)
                {
                    configUIPrefab = Resources.Load<GameObject>("UI/RoomConfigUI");
                }
            }

            if (configUIPrefab != null)
            {
                // 关键修复：在当前场景的Canvas下创建UI
                configUIInstance = CreateUIInCurrentScene();

                if (configUIInstance != null)
                {
                    configUIComponent = configUIInstance.GetComponent<RoomConfigUI>();
                    if (configUIComponent == null)
                    {
                        LogDebug("错误：配置UI预制体缺少RoomConfigUI组件");
                    }

                    configUIInstance.SetActive(false);
                    LogDebug($"配置UI实例创建成功 - 场景: {configUIInstance.scene.name}");
                }
            }
            else
            {
                LogDebug("错误：无法加载配置UI预制体");
            }
        }

        /// <summary>
        /// 在当前场景中创建UI（避免DontDestroyOnLoad问题）
        /// </summary>
        private GameObject CreateUIInCurrentScene()
        {
            LogDebug("在当前场景中创建UI");

            // 方法1：直接实例化，Unity会自动放在当前场景
            var uiInstance = Instantiate(configUIPrefab);

            // 方法2：如果UI仍然跑到DontDestroy，强制移回当前场景
            if (uiInstance.scene.name == "DontDestroyOnLoad")
            {
                LogDebug("UI被创建在DontDestroyOnLoad，强制移回当前场景");
                MoveToCurrentScene(uiInstance);
            }

            // 确保Canvas设置正确
            var canvas = uiInstance.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.enabled = true;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000;
            }

            return uiInstance;
        }

        /// <summary>
        /// 强制将对象移动到当前场景
        /// </summary>
        private void MoveToCurrentScene(GameObject obj)
        {
            var currentScene = SceneManager.GetActiveScene();
            var rootObjects = currentScene.GetRootGameObjects();

            if (rootObjects.Length > 0)
            {
                // 临时设为场景中某个对象的子对象，然后设为null
                obj.transform.SetParent(rootObjects[0].transform);
                obj.transform.SetParent(null);

                LogDebug($"对象已移动到当前场景: {obj.scene.name}");
            }
        }

        /// <summary>
        /// 应用配置更改
        /// </summary>
        public void ApplyConfigChanges()
        {
            LogDebug("应用配置更改");

            if (runtimeConfig != null && runtimeConfig.ValidateConfig())
            {
                currentConfig = runtimeConfig.CreateCopy();
                currentConfig.ClearDirty();

                // 更新Timer配置管理器
                var timerConfig = currentConfig.GetEffectiveTimerConfig();
                if (timerConfig != null)
                {
                    TimerConfigManager.SetConfig(timerConfig);
                }

                LogDebug("配置已应用");
            }
        }

        /// <summary>
        /// 重置为默认配置
        /// </summary>
        public void ResetToDefault()
        {
            LogDebug("重置为默认配置");
            runtimeConfig.ResetToDefault();

            // 重置Timer配置
            InitializeTimerConfig();
        }

        /// <summary>
        /// 获取传递给游戏场景的配置
        /// </summary>
        public RoomGameConfig GetGameSceneConfig()
        {
            return currentConfig?.CreateCopy();
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
                Debug.Log($"[RoomConfigManager] {message}");
            }
        }

        [ContextMenu("强制打开配置UI")]
        public void ForceOpenConfigUI()
        {
            LogDebug("强制打开配置UI（调试模式）");
            OpenConfigUI();
        }

        [ContextMenu("显示当前配置")]
        public void ShowCurrentConfig()
        {
            Debug.Log(GetConfigSummary());
        }

        [ContextMenu("显示Timer配置")]
        public void ShowTimerConfig()
        {
            var timerConfig = GetCurrentTimerConfig();
            if (timerConfig != null)
            {
                Debug.Log($"Timer配置: {timerConfig.GetConfigSummary()}");
            }
            else
            {
                Debug.Log("没有Timer配置");
            }
        }

        [ContextMenu("测试Timer配置设置")]
        public void TestTimerConfigSetting()
        {
            var timerConfig = TimerConfigManager.Config;
            if (timerConfig != null)
            {
                SetTimerConfig(timerConfig);
                LogDebug("测试Timer配置设置完成");
            }
            else
            {
                LogDebug("没有可用的Timer配置进行测试");
            }
        }

        #endregion
    }
}