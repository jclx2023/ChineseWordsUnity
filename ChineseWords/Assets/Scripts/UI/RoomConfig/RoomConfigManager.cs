using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using Core.Network;
using TMPro;
using UnityEngine.UI;
using UI.RoomConfig;
using Photon.Pun;

namespace Core.Network
{
    /// <summary>
    /// 房间配置管理器 - Photon优化版
    /// 专注于RoomGameConfig的管理，简化权限检查，完全适配Photon系统
    /// </summary>
    public class RoomConfigManager : MonoBehaviour
    {
        [Header("配置设置")]
        [SerializeField] private RoomGameConfig defaultConfig;

        [Header("UI设置")]
        [SerializeField] private GameObject configUIPrefab;
        [SerializeField] private KeyCode configUIHotkey = KeyCode.F1;
        [SerializeField] private bool enableHotkeyInGame = true;

        [Header("场景设置")]
        [SerializeField] private bool allowInAllScenes = false;
        [SerializeField] private string[] allowedScenes = { "RoomScene", "NetworkGameScene" };

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool forceAllowInEditor = true;

        // 单例实例
        public static RoomConfigManager Instance { get; private set; }

        // 当前配置
        private RoomGameConfig currentConfig;
        private RoomGameConfig runtimeConfig; // 运行时临时配置

        // UI相关
        private GameObject configUIInstance;
        private RoomConfigUI configUIComponent;
        private bool isUIOpen = false;

        // 初始化状态
        private bool isInitialized = false;

        // 属性
        public RoomGameConfig CurrentConfig => currentConfig;
        public bool IsUIOpen => isUIOpen;
        public bool IsInitialized => isInitialized;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                LogDebug("RoomConfigManager (Photon版) 单例已创建");

                // 根据场景决定是否保持
                if (ShouldPersistAcrossScenes())
                {
                    DontDestroyOnLoad(gameObject);
                    LogDebug("设置为跨场景持久化");
                }
            }
            else if (Instance != this)
            {
                LogDebug("销毁重复的RoomConfigManager实例");
                Destroy(gameObject);
                return;
            }

            StartCoroutine(InitializeConfigManager());
        }

        private void Update()
        {
            // 处理配置UI热键
            if (enableHotkeyInGame && Input.GetKeyDown(configUIHotkey))
            {
                HandleConfigUIToggle();
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

        #region 初始化

        /// <summary>
        /// 初始化配置管理器
        /// </summary>
        private IEnumerator InitializeConfigManager()
        {
            LogDebug("开始初始化RoomConfigManager");

            // 加载默认配置
            LoadDefaultConfig();

            // 等待一帧确保其他管理器初始化
            yield return null;

            isInitialized = true;
            LogDebug("RoomConfigManager初始化完成");
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
                    currentConfig.name = "DefaultRoomGameConfig";
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

        /// <summary>
        /// 判断是否应该跨场景持久化
        /// </summary>
        private bool ShouldPersistAcrossScenes()
        {
            // 如果允许在所有场景使用，则持久化
            return allowInAllScenes;
        }

        #endregion

        #region 权限和场景检查 - Photon优化版

        /// <summary>
        /// 检查当前玩家是否为房主 - Photon版本
        /// </summary>
        private bool IsCurrentPlayerHost()
        {
            // 开发模式特权
            if (Application.isEditor && forceAllowInEditor)
            {
                LogDebug("编辑器模式，允许配置访问");
                return true;
            }

            // 使用Photon的MasterClient机制
            if (PhotonNetwork.InRoom)
            {
                bool isMaster = PhotonNetwork.IsMasterClient;
                LogDebug($"Photon房间中，是否为MasterClient: {isMaster}");
                return isMaster;
            }

            // 如果不在Photon房间中，检查是否有其他网络管理器
            if (NetworkManager.Instance != null)
            {
                bool isHost = NetworkManager.Instance.IsHost;
                LogDebug($"NetworkManager中，是否为Host: {isHost}");
                return isHost;
            }

            LogDebug("无法确定房主状态，拒绝访问");
            return false;
        }

        /// <summary>
        /// 获取当前玩家ID - Photon版本
        /// </summary>
        private int GetCurrentPlayerId()
        {
            if (PhotonNetwork.InRoom)
            {
                return PhotonNetwork.LocalPlayer.ActorNumber;
            }

            if (NetworkManager.Instance != null)
            {
                return NetworkManager.Instance.ClientId;
            }

            return 0;
        }

        /// <summary>
        /// 检查是否在允许的场景中
        /// </summary>
        private bool IsInAllowedScene()
        {
            if (allowInAllScenes)
            {
                LogDebug("允许在所有场景中使用配置");
                return true;
            }

            string currentScene = SceneManager.GetActiveScene().name;

            foreach (string allowedScene in allowedScenes)
            {
                if (currentScene.Contains(allowedScene))
                {
                    LogDebug($"当前场景 {currentScene} 在允许列表中");
                    return true;
                }
            }

            LogDebug($"当前场景 {currentScene} 不在允许列表中: [{string.Join(", ", allowedScenes)}]");
            return false;
        }

        /// <summary>
        /// 检查是否在房间环境中
        /// </summary>
        private bool IsInRoomEnvironment()
        {
            // 优先检查Photon房间状态
            if (PhotonNetwork.InRoom)
            {
                LogDebug("在Photon房间中");
                return true;
            }

            // 备用检查：RoomManager状态
            if (RoomManager.Instance != null && RoomManager.Instance.IsInRoom)
            {
                LogDebug("RoomManager显示在房间中");
                return true;
            }

            LogDebug("不在房间环境中");
            return false;
        }

        /// <summary>
        /// 检查操作权限 - 简化版
        /// </summary>
        private bool CheckPermissions()
        {
            // 检查场景权限
            if (!IsInAllowedScene())
            {
                LogDebug("当前场景不允许打开配置UI");
                return false;
            }

            // 检查房间环境（可选）
            if (!allowInAllScenes && !IsInRoomEnvironment())
            {
                LogDebug("不在房间环境中，无法打开配置UI");
                return false;
            }

            // 检查房主权限
            if (!IsCurrentPlayerHost())
            {
                LogDebug("只有房主可以打开配置UI");
                return false;
            }

            return true;
        }

        #endregion

        #region 配置管理

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
            if (runtimeConfig == null)
                return "配置未加载";

            var summary = runtimeConfig.GetConfigSummary();

            // 添加权限和场景信息
            summary += $"\n权限状态: 房主={IsCurrentPlayerHost()}, 场景={IsInAllowedScene()}, 房间={IsInRoomEnvironment()}";
            summary += $"\n玩家ID: {GetCurrentPlayerId()}";

            if (PhotonNetwork.InRoom)
            {
                summary += $"\nPhoton房间: {PhotonNetwork.CurrentRoom.Name}, MasterClient: {PhotonNetwork.MasterClient?.ActorNumber}";
            }

            return summary;
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

                // 如果在房间中，可以考虑广播配置更新
                BroadcastConfigUpdate();
            }
            else
            {
                Debug.LogError("[RoomConfigManager] 配置验证失败，无法应用更改");
            }
        }

        /// <summary>
        /// 重置为默认配置
        /// </summary>
        public void ResetToDefault()
        {
            LogDebug("重置为默认配置");

            if (runtimeConfig != null)
            {
                runtimeConfig.ResetToDefault();
            }
            else
            {
                LoadDefaultConfig();
            }

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

        /// <summary>
        /// 广播配置更新（如果在多人房间中）
        /// </summary>
        private void BroadcastConfigUpdate()
        {
            if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
            {
                // 可以通过房间属性或RPC广播配置更新
                // 这里是扩展点，具体实现取决于需求
                LogDebug("房主配置更新，可以广播给其他玩家");
            }
        }

        #endregion

        #region UI管理

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

            if (!CheckPermissions())
            {
                LogDebug("没有权限打开配置UI");
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
                Debug.LogError("[RoomConfigManager] 配置UI创建失败");
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
        /// 创建配置UI实例
        /// </summary>
        private void CreateConfigUIInstance()
        {
            LogDebug("创建配置UI实例");

            if (configUIPrefab == null)
            {
                // 尝试从Resources加载
                configUIPrefab = Resources.Load<GameObject>("RoomConfigUI");
                if (configUIPrefab == null)
                {
                    configUIPrefab = Resources.Load<GameObject>("UI/RoomConfigUI");
                }
                if (configUIPrefab == null)
                {
                    configUIPrefab = Resources.Load<GameObject>("Prefabs/UI/RoomConfigUI");
                }
            }

            if (configUIPrefab != null)
            {
                // 在当前场景中创建UI
                configUIInstance = CreateUIInCurrentScene();

                if (configUIInstance != null)
                {
                    configUIComponent = configUIInstance.GetComponent<RoomConfigUI>();
                    if (configUIComponent == null)
                    {
                        Debug.LogError("[RoomConfigManager] 配置UI预制体缺少RoomConfigUI组件");
                    }

                    configUIInstance.SetActive(false);
                    LogDebug($"配置UI实例创建成功 - 场景: {configUIInstance.scene.name}");
                }
            }
            else
            {
                Debug.LogError("[RoomConfigManager] 无法加载配置UI预制体，请检查Resources路径");
            }
        }

        /// <summary>
        /// 在当前场景中创建UI
        /// </summary>
        private GameObject CreateUIInCurrentScene()
        {
            LogDebug("在当前场景中创建UI");

            // 直接实例化
            var uiInstance = Instantiate(configUIPrefab);
            uiInstance.name = "RoomConfigUI_Instance";

            // 如果意外创建在DontDestroyOnLoad场景，移回当前场景
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
                canvas.sortingOrder = 1000; // 确保在最上层
            }

            // 添加GraphicRaycaster（如果没有）
            var raycaster = uiInstance.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                uiInstance.AddComponent<GraphicRaycaster>();
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

        #endregion

        #region 公共接口

        /// <summary>
        /// 强制打开配置UI（忽略权限检查，调试用）
        /// </summary>
        public void ForceOpenConfigUI()
        {
            LogDebug("强制打开配置UI（调试模式）");

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
            }
        }

        /// <summary>
        /// 检查当前是否有权限访问配置
        /// </summary>
        public bool HasConfigPermission()
        {
            return CheckPermissions();
        }

        /// <summary>
        /// 获取权限状态详情
        /// </summary>
        public string GetPermissionStatus()
        {
            return $"房主权限: {IsCurrentPlayerHost()}, " +
                   $"场景权限: {IsInAllowedScene()}, " +
                   $"房间环境: {IsInRoomEnvironment()}, " +
                   $"整体权限: {CheckPermissions()}";
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
        public void DebugForceOpenConfigUI()
        {
            ForceOpenConfigUI();
        }

        [ContextMenu("显示当前配置")]
        public void DebugShowCurrentConfig()
        {
            Debug.Log("=== 当前配置 ===\n" + GetConfigSummary());
        }

        [ContextMenu("显示Timer配置")]
        public void DebugShowTimerConfig()
        {
            var timerConfig = GetCurrentTimerConfig();
            if (timerConfig != null)
            {
                Debug.Log($"=== Timer配置 ===\n{timerConfig.GetConfigSummary()}");
            }
            else
            {
                Debug.Log("没有Timer配置");
            }
        }

        [ContextMenu("显示权限状态")]
        public void DebugShowPermissionStatus()
        {
            Debug.Log($"=== 权限状态 ===\n{GetPermissionStatus()}");
        }

        [ContextMenu("测试权限检查")]
        public void DebugTestPermissions()
        {
            bool hasPermission = CheckPermissions();
            Debug.Log($"权限检查结果: {(hasPermission ? "通过" : "失败")}");
            Debug.Log(GetPermissionStatus());
        }

        [ContextMenu("重置配置")]
        public void DebugResetConfig()
        {
            ResetToDefault();
            Debug.Log("配置已重置为默认值");
        }

        [ContextMenu("应用配置")]
        public void DebugApplyConfig()
        {
            ApplyConfigChanges();
            Debug.Log("配置已应用");
        }

        [ContextMenu("测试Timer配置设置")]
        public void DebugTestTimerConfigSetting()
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