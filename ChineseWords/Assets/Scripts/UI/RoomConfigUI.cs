using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Core.Network;
using UI;

namespace Core.Network
{
    /// <summary>
    /// 房间配置UI管理器
    /// 提供配置界面的基础框架和标签页管理
    /// </summary>
    public class RoomConfigUI : MonoBehaviour
    {
        [Header("UI组件引用")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Button closeButton;

        [Header("标签页系统")]
        [SerializeField] private Transform tabButtonParent;
        [SerializeField] private Transform tabContentParent;
        [SerializeField] private GameObject tabButtonPrefab;
        [SerializeField] private Color activeTabColor = Color.white;
        [SerializeField] private Color inactiveTabColor = Color.gray;

        [Header("底部按钮")]
        [SerializeField] private Button applyButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button cancelButton;

        [Header("UI设置")]
        [SerializeField] private bool closeOnEscape = true;
        [SerializeField] private bool pauseGameWhenOpen = true;
        [SerializeField] private float animationDuration = 0.3f;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 组件引用
        private RoomConfigManager configManager;
        private RoomGameConfig currentConfig;

        // 标签页管理
        private Dictionary<string, TabInfo> tabs = new Dictionary<string, TabInfo>();
        private string activeTabId;

        // UI状态
        private bool isInitialized = false;
        private bool hasUnsavedChanges = false;

        /// <summary>
        /// 标签页信息
        /// </summary>
        [System.Serializable]
        private class TabInfo
        {
            public string id;
            public string displayName;
            public Button tabButton;
            public GameObject tabContent;
            public bool isActive;
        }

        private void Awake()
        {
            // 基础组件验证
            ValidateComponents();

            // 绑定基础事件
            BindBasicEvents();
        }

        private void Start()
        {
            // 初始化标签页系统
            InitializeTabSystem();
        }

        private void Update()
        {
            if (isInitialized)
            {
                HandleInput();
            }
        }

        private void OnDestroy()
        {
            // 清理事件绑定
            UnbindEvents();
        }

        /// <summary>
        /// 初始化UI（由RoomConfigManager调用）
        /// </summary>
        public void Initialize(RoomConfigManager manager, RoomGameConfig config)
        {
            LogDebug("初始化配置UI");

            configManager = manager;
            currentConfig = config;

            // 更新标题
            if (titleText != null)
            {
                titleText.text = "游戏配置 - 仅房主可操作";
            }

            // 创建配置面板
            CreateConfigPanels();

            // 刷新UI显示
            RefreshUI();

            isInitialized = true;
            LogDebug("配置UI初始化完成");
        }

        /// <summary>
        /// 验证UI组件
        /// </summary>
        private void ValidateComponents()
        {
            if (canvas == null)
                canvas = GetComponent<Canvas>();

            if (mainPanel == null)
                mainPanel = transform.Find("MainPanel")?.gameObject;

            if (titleText == null)
                titleText = GetComponentInChildren<TMP_Text>();

            // 验证必要组件
            bool hasRequiredComponents = canvas != null && mainPanel != null;

            if (!hasRequiredComponents)
            {
                Debug.LogError("[RoomConfigUI] 缺少必要的UI组件");
            }
        }

        /// <summary>
        /// 绑定基础事件
        /// </summary>
        private void BindBasicEvents()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(OnCloseButtonClicked);

            if (applyButton != null)
                applyButton.onClick.AddListener(OnApplyButtonClicked);

            if (resetButton != null)
                resetButton.onClick.AddListener(OnResetButtonClicked);

            if (cancelButton != null)
                cancelButton.onClick.AddListener(OnCancelButtonClicked);
        }

        /// <summary>
        /// 解绑事件
        /// </summary>
        private void UnbindEvents()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveAllListeners();

            if (applyButton != null)
                applyButton.onClick.RemoveAllListeners();

            if (resetButton != null)
                resetButton.onClick.RemoveAllListeners();

            if (cancelButton != null)
                cancelButton.onClick.RemoveAllListeners();

            // 清理标签页事件
            foreach (var tab in tabs.Values)
            {
                if (tab.tabButton != null)
                    tab.tabButton.onClick.RemoveAllListeners();
            }
        }

        /// <summary>
        /// 初始化标签页系统
        /// </summary>
        private void InitializeTabSystem()
        {
            LogDebug("初始化标签页系统");

            // 创建标签页 - 只保留核心的三个标签页
            CreateTab("questions", "题型权重", true);
            CreateTab("timer", "时间设置", false);
            CreateTab("health", "血量设置", false);

            // 激活第一个标签页
            if (tabs.Count > 0)
            {
                SwitchToTab("questions");
            }
        }

        /// <summary>
        /// 创建标签页
        /// </summary>
        private void CreateTab(string tabId, string displayName, bool isDefault = false)
        {
            LogDebug($"创建标签页: {displayName}");

            // 创建标签按钮
            GameObject tabButtonObj = null;
            if (tabButtonPrefab != null && tabButtonParent != null)
            {
                tabButtonObj = Instantiate(tabButtonPrefab, tabButtonParent);
                var buttonText = tabButtonObj.GetComponentInChildren<TMP_Text>();
                if (buttonText != null)
                    buttonText.text = displayName;
            }

            // 创建标签内容区域
            GameObject tabContentObj = new GameObject($"Tab_{tabId}_Content");
            if (tabContentParent != null)
            {
                tabContentObj.transform.SetParent(tabContentParent, false);
            }

            // 添加到标签字典
            var tabInfo = new TabInfo
            {
                id = tabId,
                displayName = displayName,
                tabButton = tabButtonObj?.GetComponent<Button>(),
                tabContent = tabContentObj,
                isActive = isDefault
            };

            tabs[tabId] = tabInfo;

            // 绑定点击事件
            if (tabInfo.tabButton != null)
            {
                tabInfo.tabButton.onClick.AddListener(() => SwitchToTab(tabId));
            }

            // 设置初始状态
            tabContentObj.SetActive(isDefault);
            UpdateTabButtonAppearance(tabInfo);
        }

        /// <summary>
        /// 切换到指定标签页
        /// </summary>
        private void SwitchToTab(string tabId)
        {
            if (!tabs.ContainsKey(tabId))
            {
                LogDebug($"标签页不存在: {tabId}");
                return;
            }

            LogDebug($"切换到标签页: {tabId}");

            // 隐藏所有标签页内容
            foreach (var tab in tabs.Values)
            {
                tab.isActive = false;
                if (tab.tabContent != null)
                    tab.tabContent.SetActive(false);
                UpdateTabButtonAppearance(tab);
            }

            // 激活目标标签页
            var targetTab = tabs[tabId];
            targetTab.isActive = true;
            if (targetTab.tabContent != null)
                targetTab.tabContent.SetActive(true);
            UpdateTabButtonAppearance(targetTab);

            activeTabId = tabId;
        }

        /// <summary>
        /// 更新标签按钮外观
        /// </summary>
        private void UpdateTabButtonAppearance(TabInfo tab)
        {
            if (tab.tabButton == null) return;

            var buttonImage = tab.tabButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = tab.isActive ? activeTabColor : inactiveTabColor;
            }

            // 可以添加更多视觉效果
            var buttonText = tab.tabButton.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                buttonText.fontStyle = tab.isActive ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        /// <summary>
        /// 创建配置面板
        /// </summary>
        private void CreateConfigPanels()
        {
            LogDebug("创建配置面板");

            // 创建题型权重配置面板
            CreateQuestionWeightPanel();

            // 创建计时器设置面板
            CreateTimerSettingsPanel();

            // 创建血量设置面板
            CreateHPSettingsPanel();
        }

        /// <summary>
        /// 创建题型权重配置面板
        /// </summary>
        private void CreateQuestionWeightPanel()
        {
            if (!tabs.ContainsKey("questions")) return;

            var parentTransform = tabs["questions"].tabContent.transform;

            // 第一阶段：创建占位符面板
            // 第二阶段将实现具体的权重配置界面
            CreatePlaceholderPanel(parentTransform,
                "题型权重配置面板\n\n" +
                "这里将显示各个题型的权重滑条\n" +
                "可以调整每种题型出现的概率\n\n" +
                "• 成语接龙\n" +
                "• 拼音填空\n" +
                "• 硬性填空\n" +
                "• 软性填空\n" +
                "• 情感判断\n" +
                "• 近义词选择\n" +
                "• 用法判断\n" +
                "• 词语解释\n\n" +
                "(即将在第二阶段实现)"
            );
        }

        /// <summary>
        /// 创建计时器设置面板
        /// </summary>
        private void CreateTimerSettingsPanel()
        {
            if (!tabs.ContainsKey("timer")) return;

            var parentTransform = tabs["timer"].tabContent.transform;

            // 第一阶段：创建占位符面板
            CreatePlaceholderPanel(parentTransform,
                "计时器设置面板\n\n" +
                "• 题目答题时间限制 (5-120秒)\n" +
                "• 超时延迟时间\n" +
                "• 时间警告提示设置\n" +
                "• 警告时间阈值\n\n" +
                "(即将实现)"
            );
        }

        /// <summary>
        /// 创建血量设置面板
        /// </summary>
        private void CreateHPSettingsPanel()
        {
            if (!tabs.ContainsKey("health")) return;

            var parentTransform = tabs["health"].tabContent.transform;

            // 第一阶段：创建占位符面板
            CreatePlaceholderPanel(parentTransform,
                "血量设置面板\n\n" +
                "• 玩家初始血量 (20-200)\n" +
                "• 答错题扣血数量 (5-50)\n\n" +
                "每名玩家开始时都有相同的血量\n" +
                "答错题目会扣除血量\n" +
                "血量归零时玩家被淘汰\n\n" +
                "(即将实现)"
            );
        }

        /// <summary>
        /// 创建占位符面板
        /// </summary>
        private void CreatePlaceholderPanel(Transform parent, string text)
        {
            var placeholderObj = new GameObject("PlaceholderPanel");
            placeholderObj.transform.SetParent(parent, false);

            // 添加RectTransform
            var rectTransform = placeholderObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;

            // 添加文本组件
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(placeholderObj.transform, false);

            var textRectTransform = textObj.AddComponent<RectTransform>();
            textRectTransform.anchorMin = Vector2.zero;
            textRectTransform.anchorMax = Vector2.one;
            textRectTransform.sizeDelta = Vector2.zero;
            textRectTransform.anchoredPosition = Vector2.zero;

            var tmpText = textObj.AddComponent<TMP_Text>();
            tmpText.text = text;
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.fontSize = 24;
            tmpText.color = Color.gray;
        }

        /// <summary>
        /// 处理输入
        /// </summary>
        private void HandleInput()
        {
            if (closeOnEscape && Input.GetKeyDown(KeyCode.Escape))
            {
                OnCancelButtonClicked();
            }
        }

        /// <summary>
        /// 刷新UI显示
        /// </summary>
        public void RefreshUI()
        {
            if (!isInitialized) return;

            LogDebug("刷新UI显示");

            // 第一阶段：暂无具体面板需要刷新
            // 第二阶段将添加具体面板的刷新逻辑

            // 更新按钮状态
            UpdateButtonStates();
        }

        /// <summary>
        /// 更新按钮状态
        /// </summary>
        private void UpdateButtonStates()
        {
            bool hasChanges = HasUnsavedChanges();

            if (applyButton != null)
                applyButton.interactable = hasChanges;

            if (resetButton != null)
                resetButton.interactable = true;
        }

        /// <summary>
        /// 检查是否有未保存的更改
        /// </summary>
        private bool HasUnsavedChanges()
        {
            // 第一阶段：由于没有具体的配置面板，暂时返回false
            // 第二阶段将实现具体的更改检测逻辑
            return hasUnsavedChanges;
        }

        /// <summary>
        /// 配置更改回调
        /// </summary>
        private void OnConfigChanged()
        {
            hasUnsavedChanges = true;
            UpdateButtonStates();
            LogDebug("检测到配置更改");
        }

        #region 按钮事件处理

        private void OnCloseButtonClicked()
        {
            LogDebug("点击关闭按钮");

            if (HasUnsavedChanges())
            {
                // 显示确认对话框
                ShowUnsavedChangesDialog();
            }
            else
            {
                CloseUI();
            }
        }

        private void OnApplyButtonClicked()
        {
            LogDebug("点击应用按钮");

            if (ApplyChanges())
            {
                hasUnsavedChanges = false;
                UpdateButtonStates();
            }
        }

        private void OnResetButtonClicked()
        {
            LogDebug("点击重置按钮");

            // 显示确认对话框
            ShowResetConfirmDialog();
        }

        private void OnCancelButtonClicked()
        {
            LogDebug("点击取消按钮");

            if (HasUnsavedChanges())
            {
                ShowUnsavedChangesDialog();
            }
            else
            {
                CloseUI();
            }
        }

        #endregion

        #region 对话框显示

        private void ShowUnsavedChangesDialog()
        {
            // 简单的确认对话框（实际项目中可以用更复杂的UI）
            LogDebug("显示未保存更改提示");

            // 这里可以显示自定义对话框
            // 临时使用简单逻辑
            CloseUI();
        }

        private void ShowResetConfirmDialog()
        {
            LogDebug("显示重置确认对话框");

            // 这里可以显示确认对话框
            // 临时直接执行重置
            ResetToDefault();
        }

        #endregion

        #region 核心功能

        /// <summary>
        /// 应用配置更改
        /// </summary>
        private bool ApplyChanges()
        {
            try
            {
                LogDebug("应用配置更改");

                // 第一阶段：暂无具体配置需要收集
                // 第二阶段将添加具体面板的配置收集逻辑

                // 通知配置管理器
                if (configManager != null)
                {
                    configManager.ApplyConfigChanges();
                }

                LogDebug("配置更改应用成功");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RoomConfigUI] 应用配置失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 重置为默认配置
        /// </summary>
        private void ResetToDefault()
        {
            LogDebug("重置为默认配置");

            if (configManager != null)
            {
                configManager.ResetToDefault();
                RefreshUI();
            }
        }

        /// <summary>
        /// 关闭UI
        /// </summary>
        private void CloseUI()
        {
            LogDebug("关闭配置UI");

            if (configManager != null)
            {
                configManager.CloseConfigUI();
            }
        }

        #endregion

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[RoomConfigUI] {message}");
            }
        }

        #region 公共接口

        /// <summary>
        /// 外部刷新UI
        /// </summary>
        public void ExternalRefreshUI()
        {
            RefreshUI();
        }

        /// <summary>
        /// 切换到指定标签页（外部调用）
        /// </summary>
        public void SwitchToTabExternal(string tabId)
        {
            SwitchToTab(tabId);
        }

        #endregion
    }
}