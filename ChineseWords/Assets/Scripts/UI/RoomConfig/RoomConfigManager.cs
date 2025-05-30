﻿using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using Core.Network;
using TMPro;
using UnityEngine.UI;
using UI.RoomConfig;

namespace Core.Network
{
    /// <summary>
    /// 精简版房间配置管理器
    /// 专注于基础的配置UI管理功能
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
        }

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
        }

        /// <summary>
        /// 获取传递给游戏场景的配置
        /// </summary>
        public RoomGameConfig GetGameSceneConfig()
        {
            return currentConfig?.CreateCopy();
        }

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

        #region 调试方法

        [ContextMenu("强制打开配置UI")]
        public void ForceOpenConfigUI()
        {
            LogDebug("强制打开配置UI（调试模式）");
            OpenConfigUI();
        }

        [ContextMenu("显示当前配置")]
        public void ShowCurrentConfig()
        {
            Debug.Log(currentConfig?.GetConfigSummary() ?? "配置未加载");
        }

        [ContextMenu("创建完整测试UI")]
        public void CreateCompleteTestUI()
        {
            // 销毁现有UI
            if (configUIInstance != null)
            {
                DestroyImmediate(configUIInstance);
                configUIInstance = null;
            }

            LogDebug("创建完整测试UI");

            // 创建Canvas
            var canvas = new GameObject("TestConfigUI");
            var canvasComponent = canvas.AddComponent<Canvas>();
            canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasComponent.sortingOrder = 1000;
            canvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // 创建主面板
            var mainPanel = new GameObject("MainPanel");
            mainPanel.transform.SetParent(canvas.transform, false);

            var panelRect = mainPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.2f, 0.2f);
            panelRect.anchorMax = new Vector2(0.8f, 0.8f);
            panelRect.sizeDelta = Vector2.zero;
            panelRect.anchoredPosition = Vector2.zero;

            var panelImage = mainPanel.AddComponent<UnityEngine.UI.Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f); // 深色半透明背景

            // 创建标题
            var title = new GameObject("Title");
            title.transform.SetParent(mainPanel.transform, false);

            var titleRect = title.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.85f);
            titleRect.anchorMax = new Vector2(1, 1f);
            titleRect.sizeDelta = Vector2.zero;
            titleRect.anchoredPosition = Vector2.zero;

            var titleText = title.AddComponent<TMP_Text>();
            titleText.text = "游戏配置 - 测试版本";
            titleText.color = Color.white;
            titleText.fontSize = 20;
            titleText.alignment = TextAlignmentOptions.Center;

            // 创建标签按钮容器
            var tabButtonContainer = new GameObject("TabButtonContainer");
            tabButtonContainer.transform.SetParent(mainPanel.transform, false);

            var tabButtonRect = tabButtonContainer.AddComponent<RectTransform>();
            tabButtonRect.anchorMin = new Vector2(0, 0.7f);
            tabButtonRect.anchorMax = new Vector2(1, 0.85f);
            tabButtonRect.sizeDelta = Vector2.zero;
            tabButtonRect.anchoredPosition = Vector2.zero;

            // 添加水平布局组件
            var layoutGroup = tabButtonContainer.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layoutGroup.spacing = 10f;
            layoutGroup.padding = new RectOffset(20, 20, 5, 5);
            layoutGroup.childAlignment = TextAnchor.MiddleLeft;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = true;

            // 创建三个测试按钮
            string[] tabNames = { "题型权重", "时间设置", "血量设置" };
            Color[] buttonColors = {
                new Color(0.2f, 0.6f, 0.2f, 1f), // 绿色
                new Color(0.2f, 0.2f, 0.6f, 1f), // 蓝色
                new Color(0.6f, 0.2f, 0.2f, 1f)  // 红色
            };

            for (int i = 0; i < tabNames.Length; i++)
            {
                var button = CreateTestButton(tabNames[i], buttonColors[i], tabButtonContainer.transform);
            }

            // 创建内容区域
            var contentArea = new GameObject("ContentArea");
            contentArea.transform.SetParent(mainPanel.transform, false);

            var contentRect = contentArea.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 0.1f);
            contentRect.anchorMax = new Vector2(1, 0.7f);
            contentRect.sizeDelta = Vector2.zero;
            contentRect.anchoredPosition = Vector2.zero;

            var contentImage = contentArea.AddComponent<UnityEngine.UI.Image>();
            contentImage.color = new Color(0.05f, 0.05f, 0.05f, 0.8f); // 更深的背景

            // 在内容区域添加说明文本
            var contentText = new GameObject("ContentText");
            contentText.transform.SetParent(contentArea.transform, false);

            var contentTextRect = contentText.AddComponent<RectTransform>();
            contentTextRect.anchorMin = Vector2.zero;
            contentTextRect.anchorMax = Vector2.one;
            contentTextRect.sizeDelta = Vector2.zero;
            contentTextRect.anchoredPosition = Vector2.zero;

            var text = contentText.AddComponent<TMP_Text>();
            text.text = "配置UI布局测试\n\n✓ 标签按钮应该水平排列\n✓ 按钮之间有适当间距\n✓ 内容区域显示正常\n\n如果看到此界面说明布局正确！";
            text.color = Color.white;
            text.fontSize = 16;
            text.alignment = TextAlignmentOptions.Center;

            // 创建关闭按钮
            var closeButton = CreateTestButton("关闭", Color.gray, mainPanel.transform);
            var closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.8f, 0.02f);
            closeRect.anchorMax = new Vector2(0.95f, 0.08f);
            closeRect.sizeDelta = Vector2.zero;
            closeRect.anchoredPosition = Vector2.zero;

            // 绑定关闭事件
            var closeButtonComponent = closeButton.GetComponent<Button>();
            closeButtonComponent.onClick.AddListener(() => {
                DestroyImmediate(canvas);
                configUIInstance = null;
                isUIOpen = false;
                LogDebug("测试UI已关闭");
            });

            configUIInstance = canvas;
            isUIOpen = true;

            LogDebug($"完整测试UI创建完成 - 场景: {canvas.scene.name}");
            LogDebug("应该能看到带有三个标签按钮的配置界面");
        }

        /// <summary>
        /// 创建测试按钮
        /// </summary>
        private GameObject CreateTestButton(string text, Color color, Transform parent)
        {
            var buttonObj = new GameObject($"Button_{text}");
            buttonObj.transform.SetParent(parent, false);

            var rectTransform = buttonObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(100, 30);

            var button = buttonObj.AddComponent<Button>();
            var image = buttonObj.AddComponent<UnityEngine.UI.Image>();
            image.color = color;

            // 创建按钮文本
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);

            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;

            var textComponent = textObj.AddComponent<TMP_Text>();
            textComponent.text = text;
            textComponent.color = Color.white;
            textComponent.fontSize = 14;
            textComponent.alignment = TextAlignmentOptions.Center;

            return buttonObj;
        }

        #endregion
    }
}