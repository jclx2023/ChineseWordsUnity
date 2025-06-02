using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Core;
using Core.Network;
using TMPro;

namespace UI.RoomConfig
{
    /// <summary>
    /// 房间配置UI管理器
    /// 支持权重配置（标签页0）和Timer配置（标签页1）
    /// </summary>
    public class RoomConfigUI : MonoBehaviour
    {
        [Header("预制体引用")]
        [SerializeField] private GameObject questionWeightPrefab;
        [SerializeField] private GameObject timerConfigPrefab;
        [SerializeField] private string questionWeightPrefabPath = "Prefabs/UI/QuestionWeight";
        [SerializeField] private string timerConfigPrefabPath = "Prefabs/UI/TimerConfig";

        [Header("标签页系统")]
        [SerializeField] private Transform[] tabContents;
        [SerializeField] private Button[] tabButtons;
        [SerializeField] private int currentTabIndex = 0;

        [Header("配置")]
        [SerializeField] private QuestionWeightConfig defaultWeightConfig;
        [SerializeField] private TimerConfig defaultTimerConfig;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 配置面板引用
        private QuestionWeightConfigPanel questionWeightPanel;
        private GameObject questionWeightPanelInstance;

        private TimerConfigPanel timerConfigPanel;
        private GameObject timerConfigPanelInstance;

        // UI状态
        private bool isInitialized = false;
        private bool isInstantiating = false;

        private void Awake()
        {
            LogDebug("RoomConfigUI 初始化开始");
        }

        private void Start()
        {
            StartCoroutine(InitializeUICoroutine());
        }

        /// <summary>
        /// 初始化UI协程
        /// </summary>
        private IEnumerator InitializeUICoroutine()
        {
            LogDebug("开始初始化UI");

            // 设置默认配置
            SetupDefaultConfigs();

            // 等待一帧确保所有组件就绪
            yield return null;

            // 加载预制体引用
            yield return StartCoroutine(LoadConfigPrefabs());

            // 查找或创建标签页容器
            yield return StartCoroutine(SetupTabContainers());

            // 设置标签页按钮
            SetupTabButtons();

            // 实例化配置预制体
            yield return StartCoroutine(InstantiateConfigPrefabs());

            // 显示默认标签页
            SwitchToTab(currentTabIndex);

            isInitialized = true;
            LogDebug("UI初始化完成");
        }

        /// <summary>
        /// 设置默认配置
        /// </summary>
        private void SetupDefaultConfigs()
        {
            // 设置权重配置
            if (defaultWeightConfig != null)
            {
                QuestionWeightManager.SetConfig(defaultWeightConfig);
                LogDebug("已设置默认权重配置");
            }
            else
            {
                LogDebug("警告：未设置默认权重配置");
            }

            // 设置Timer配置
            if (defaultTimerConfig != null)
            {
                TimerConfigManager.SetConfig(defaultTimerConfig);
                LogDebug("已设置默认Timer配置");
            }
            else
            {
                LogDebug("警告：未设置默认Timer配置");
            }
        }

        /// <summary>
        /// 加载配置预制体
        /// </summary>
        private IEnumerator LoadConfigPrefabs()
        {
            LogDebug("加载配置预制体");

            // 加载权重配置预制体
            if (questionWeightPrefab == null)
            {
                LogDebug($"从Resources加载权重配置预制体: {questionWeightPrefabPath}");
                ResourceRequest request = Resources.LoadAsync<GameObject>(questionWeightPrefabPath);
                yield return request;

                if (request.asset != null)
                {
                    questionWeightPrefab = request.asset as GameObject;
                    LogDebug("权重配置预制体加载成功");
                }
                else
                {
                    Debug.LogError($"[RoomConfigUI] 无法加载权重配置预制体: {questionWeightPrefabPath}");
                }
            }

            // 加载Timer配置预制体
            if (timerConfigPrefab == null)
            {
                LogDebug($"从Resources加载Timer配置预制体: {timerConfigPrefabPath}");
                ResourceRequest request = Resources.LoadAsync<GameObject>(timerConfigPrefabPath);
                yield return request;

                if (request.asset != null)
                {
                    timerConfigPrefab = request.asset as GameObject;
                    LogDebug("Timer配置预制体加载成功");
                }
                else
                {
                    Debug.LogError($"[RoomConfigUI] 无法加载Timer配置预制体: {timerConfigPrefabPath}");
                }
            }
        }

        /// <summary>
        /// 设置标签页容器
        /// </summary>
        private IEnumerator SetupTabContainers()
        {
            LogDebug("设置标签页容器");

            // 如果没有预设容器，自动查找或创建
            if (tabContents == null || tabContents.Length == 0)
            {
                tabContents = new Transform[3]; // 3个标签页

                for (int i = 0; i < 3; i++)
                {
                    Transform container = FindTabContainer(i);
                    if (container == null)
                    {
                        container = CreateTabContainer(i);
                    }
                    tabContents[i] = container;
                }
            }

            // 确保所有容器都存在
            for (int i = 0; i < tabContents.Length; i++)
            {
                if (tabContents[i] == null)
                {
                    tabContents[i] = CreateTabContainer(i);
                }
            }

            LogDebug($"标签页容器设置完成，共 {tabContents.Length} 个");
            yield return null;
        }

        /// <summary>
        /// 实例化配置预制体
        /// </summary>
        private IEnumerator InstantiateConfigPrefabs()
        {
            LogDebug("实例化配置预制体");

            // 防止重复调用
            if (isInstantiating)
            {
                LogDebug("正在实例化中，跳过重复调用");
                yield break;
            }

            isInstantiating = true;

            try
            {
                // 实例化权重配置预制体（标签页0）
                yield return StartCoroutine(InstantiateQuestionWeightPrefab());

                // 实例化Timer配置预制体（标签页1）
                yield return StartCoroutine(InstantiateTimerConfigPrefab());
            }
            finally
            {
                isInstantiating = false;
            }
        }

        /// <summary>
        /// 实例化权重配置预制体
        /// </summary>
        private IEnumerator InstantiateQuestionWeightPrefab()
        {
            LogDebug("实例化权重配置预制体");

            if (questionWeightPrefab == null)
            {
                Debug.LogError("[RoomConfigUI] 权重配置预制体为空，无法实例化");
                yield break;
            }

            if (tabContents == null || tabContents.Length == 0 || tabContents[0] == null)
            {
                Debug.LogError("[RoomConfigUI] 权重配置容器不存在");
                yield break;
            }

            // 清空标签页0的内容
            Transform weightContainer = tabContents[0];
            ClearContainer(weightContainer);

            // 实例化预制体
            try
            {
                questionWeightPanelInstance = Instantiate(questionWeightPrefab, weightContainer);
                questionWeightPanelInstance.name = "QuestionWeightPanel_Instance";

                SetPanelRectTransform(questionWeightPanelInstance);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RoomConfigUI] 实例化权重配置预制体失败: {e.Message}");
                yield break;
            }

            yield return null;

            // 获取组件并初始化
            try
            {
                questionWeightPanel = questionWeightPanelInstance.GetComponent<QuestionWeightConfigPanel>();

                if (questionWeightPanel != null)
                {
                    LogDebug("成功获取QuestionWeightConfigPanel组件");

                    var config = QuestionWeightManager.Config;
                    if (config != null)
                    {
                        questionWeightPanel.Initialize(config);
                        LogDebug("权重配置面板初始化完成");
                    }

                    // 通知Extension组件
                    NotifyExtensionAboutWeightPanel();
                }
                else
                {
                    Debug.LogError("[RoomConfigUI] 预制体上没有QuestionWeightConfigPanel组件");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RoomConfigUI] 权重配置组件初始化失败: {e.Message}");
            }
        }

        /// <summary>
        /// 实例化Timer配置预制体
        /// </summary>
        private IEnumerator InstantiateTimerConfigPrefab()
        {
            LogDebug("实例化Timer配置预制体");

            if (timerConfigPrefab == null)
            {
                Debug.LogError("[RoomConfigUI] Timer配置预制体为空，无法实例化");
                yield break;
            }

            if (tabContents == null || tabContents.Length < 2 || tabContents[1] == null)
            {
                Debug.LogError("[RoomConfigUI] Timer配置容器不存在");
                yield break;
            }

            // 清空标签页1的内容
            Transform timerContainer = tabContents[1];
            ClearContainer(timerContainer);

            // 实例化预制体
            try
            {
                timerConfigPanelInstance = Instantiate(timerConfigPrefab, timerContainer);
                timerConfigPanelInstance.name = "TimerConfigPanel_Instance";

                SetPanelRectTransform(timerConfigPanelInstance);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RoomConfigUI] 实例化Timer配置预制体失败: {e.Message}");
                yield break;
            }

            yield return null;

            // 获取组件并初始化
            try
            {
                timerConfigPanel = timerConfigPanelInstance.GetComponent<TimerConfigPanel>();

                if (timerConfigPanel != null)
                {
                    LogDebug("成功获取TimerConfigPanel组件");

                    var config = TimerConfigManager.Config;
                    if (config != null)
                    {
                        timerConfigPanel.Initialize(config);
                        LogDebug("Timer配置面板初始化完成");
                    }

                    // 通知Extension组件（如果需要）
                    NotifyExtensionAboutTimerPanel();
                }
                else
                {
                    Debug.LogError("[RoomConfigUI] 预制体上没有TimerConfigPanel组件");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RoomConfigUI] Timer配置组件初始化失败: {e.Message}");
            }
        }

        /// <summary>
        /// 设置面板RectTransform
        /// </summary>
        private void SetPanelRectTransform(GameObject panelInstance)
        {
            RectTransform panelRect = panelInstance.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;
            }
        }

        /// <summary>
        /// 通知Extension组件权重面板已创建
        /// </summary>
        private void NotifyExtensionAboutWeightPanel()
        {
            var extension = GetComponent<RoomConfigUIExtension>();
            if (extension != null && questionWeightPanel != null)
            {
                extension.SetWeightConfigPanel(questionWeightPanel);
                LogDebug("已通知Extension组件权重面板创建完成");
            }
            else
            {
                LogDebug("Extension组件不存在或权重面板为空");
            }
        }

        /// <summary>
        /// 通知Extension组件Timer面板已创建
        /// </summary>
        private void NotifyExtensionAboutTimerPanel()
        {
            var extension = GetComponent<RoomConfigUIExtension>();
            if (extension != null && timerConfigPanel != null)
            {
                // 如果Extension支持Timer配置，可以在这里通知
                // extension.SetTimerConfigPanel(timerConfigPanel);
                LogDebug("Timer面板创建完成（Extension支持待实现）");
            }
        }

        /// <summary>
        /// 查找标签页容器
        /// </summary>
        private Transform FindTabContainer(int tabIndex)
        {
            string[] possibleNames = {
                $"TabContent_{tabIndex}",
                $"Tab{tabIndex}Content",
                $"Content{tabIndex}",
                $"Panel{tabIndex}"
            };

            foreach (string name in possibleNames)
            {
                Transform found = transform.Find(name);
                if (found != null)
                {
                    LogDebug($"找到标签页{tabIndex}容器: {name}");
                    return found;
                }

                // 递归查找
                found = FindChildRecursive(transform, name);
                if (found != null)
                {
                    LogDebug($"递归找到标签页{tabIndex}容器: {name}");
                    return found;
                }
            }

            return null;
        }

        /// <summary>
        /// 创建标签页容器
        /// </summary>
        private Transform CreateTabContainer(int tabIndex)
        {
            LogDebug($"创建标签页{tabIndex}容器");

            GameObject containerObj = new GameObject($"TabContent_{tabIndex}");
            containerObj.transform.SetParent(transform);

            RectTransform containerRect = containerObj.AddComponent<RectTransform>();

            // 修复：为Footer按钮预留更多空间，避免遮挡Apply等按钮
            containerRect.anchorMin = new Vector2(0.05f, 0.1f);   // 左边和底部留更多空间
            containerRect.anchorMax = new Vector2(0.7f, 0.85f);   // 右边留30%空间给按钮，顶部留15%
            containerRect.offsetMin = Vector2.zero;               // 去掉额外边距
            containerRect.offsetMax = Vector2.zero;               // 去掉额外边距

            // 添加背景（降低透明度，便于调试）
            Image bg = containerObj.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.1f, 0.2f); // 降低透明度

            // 初始时隐藏非当前标签页
            containerObj.SetActive(tabIndex == currentTabIndex);

            LogDebug($"标签页{tabIndex}容器创建完成 - 锚点: ({containerRect.anchorMin}, {containerRect.anchorMax})");

            return containerObj.transform;
        }

        /// <summary>
        /// 设置标签页按钮
        /// </summary>
        private void SetupTabButtons()
        {
            if (tabButtons == null || tabButtons.Length == 0)
            {
                LogDebug("未设置标签页按钮，跳过按钮设置");
                return;
            }

            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] != null)
                {
                    int index = i; // 避免闭包问题
                    tabButtons[i].onClick.RemoveAllListeners();
                    tabButtons[i].onClick.AddListener(() => SwitchToTab(index));
                }
            }

            LogDebug($"设置了 {tabButtons.Length} 个标签页按钮");
        }

        /// <summary>
        /// 切换标签页
        /// </summary>
        public void SwitchToTab(int tabIndex)
        {
            if (tabContents == null || tabIndex < 0 || tabIndex >= tabContents.Length)
            {
                LogDebug($"无效的标签页索引: {tabIndex}");
                return;
            }

            LogDebug($"切换到标签页: {tabIndex}");

            // 隐藏所有标签页内容
            for (int i = 0; i < tabContents.Length; i++)
            {
                if (tabContents[i] != null)
                {
                    tabContents[i].gameObject.SetActive(i == tabIndex);
                }
            }

            // 更新标签页按钮状态
            UpdateTabButtonStates(tabIndex);

            currentTabIndex = tabIndex;
        }

        /// <summary>
        /// 更新标签页按钮状态
        /// </summary>
        private void UpdateTabButtonStates(int activeIndex)
        {
            if (tabButtons == null) return;

            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] != null)
                {
                    // 更新按钮颜色
                    var buttonImage = tabButtons[i].GetComponent<Image>();
                    if (buttonImage != null)
                    {
                        buttonImage.color = (i == activeIndex) ?
                            new Color(0.3f, 0.6f, 1f, 1f) : // 激活：蓝色
                            new Color(0.5f, 0.5f, 0.5f, 1f); // 非激活：灰色
                    }

                    // 更新按钮文本颜色
                    var tmpText = tabButtons[i].GetComponentInChildren<TMP_Text>();
                    var legacyText = tabButtons[i].GetComponentInChildren<Text>();

                    if (tmpText != null)
                    {
                        tmpText.color = (i == activeIndex) ? Color.white : Color.gray;
                    }
                    else if (legacyText != null)
                    {
                        legacyText.color = (i == activeIndex) ? Color.white : Color.gray;
                    }
                }
            }
        }

        /// <summary>
        /// 清空容器
        /// </summary>
        private void ClearContainer(Transform container)
        {
            if (container == null)
            {
                LogDebug("ClearContainer: 容器为空");
                return;
            }

            LogDebug($"ClearContainer: 开始清理容器 {container.name}, 当前子对象数: {container.childCount}");

            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Transform child = container.GetChild(i);
                LogDebug($"ClearContainer: 销毁子对象 {child.name}");

                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }

            LogDebug($"ClearContainer: 清理完成，剩余子对象数: {container.childCount}");
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

        #region 公共接口

        /// <summary>
        /// 初始化房间配置UI（兼容外部调用）
        /// </summary>
        public void Initialize()
        {
            LogDebug("外部调用Initialize");

            if (!isInitialized)
            {
                StartCoroutine(InitializeUICoroutine());
            }
            else
            {
                LogDebug("UI已初始化，跳过重复初始化");
            }
        }

        /// <summary>
        /// 初始化房间配置UI（带参数版本）
        /// </summary>
        public void Initialize(object config)
        {
            Initialize();
        }

        /// <summary>
        /// 显示UI
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
            LogDebug("显示房间配置UI");
        }

        /// <summary>
        /// 隐藏UI
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
            LogDebug("隐藏房间配置UI");
        }

        /// <summary>
        /// 切换UI显示状态
        /// </summary>
        public void Toggle()
        {
            if (gameObject.activeSelf)
                Hide();
            else
                Show();
        }

        /// <summary>
        /// 检查UI是否显示
        /// </summary>
        public bool IsVisible()
        {
            return gameObject.activeSelf;
        }

        /// <summary>
        /// 获取权重配置面板
        /// </summary>
        public QuestionWeightConfigPanel GetQuestionWeightPanel()
        {
            return questionWeightPanel;
        }

        /// <summary>
        /// 获取Timer配置面板
        /// </summary>
        public TimerConfigPanel GetTimerConfigPanel()
        {
            return timerConfigPanel;
        }

        /// <summary>
        /// 获取当前标签页索引
        /// </summary>
        public int GetActiveTabIndex()
        {
            return currentTabIndex;
        }

        /// <summary>
        /// 设置标签页内容容器（运行时设置）
        /// </summary>
        public void SetTabContents(Transform[] contents)
        {
            tabContents = contents;
            LogDebug($"设置了 {contents?.Length ?? 0} 个标签页容器");
        }

        /// <summary>
        /// 设置标签页按钮（运行时设置）
        /// </summary>
        public void SetTabButtons(Button[] buttons)
        {
            tabButtons = buttons;
            SetupTabButtons();
            LogDebug($"设置了 {buttons?.Length ?? 0} 个标签页按钮");
        }

        /// <summary>
        /// 强制重新创建权重配置面板
        /// </summary>
        public void RecreateWeightConfigPanel()
        {
            LogDebug("强制重新创建权重配置面板");

            if (questionWeightPanelInstance != null)
            {
                if (Application.isPlaying)
                    Destroy(questionWeightPanelInstance);
                else
                    DestroyImmediate(questionWeightPanelInstance);

                questionWeightPanelInstance = null;
                questionWeightPanel = null;
            }

            StartCoroutine(InstantiateQuestionWeightPrefab());
        }

        /// <summary>
        /// 强制重新创建Timer配置面板
        /// </summary>
        public void RecreateTimerConfigPanel()
        {
            LogDebug("强制重新创建Timer配置面板");

            if (timerConfigPanelInstance != null)
            {
                if (Application.isPlaying)
                    Destroy(timerConfigPanelInstance);
                else
                    DestroyImmediate(timerConfigPanelInstance);

                timerConfigPanelInstance = null;
                timerConfigPanel = null;
            }

            StartCoroutine(InstantiateTimerConfigPrefab());
        }

        /// <summary>
        /// 获取UI状态信息
        /// </summary>
        public string GetUIStatus()
        {
            var status = "=== RoomConfigUI状态 ===\n";
            status += $"初始化完成: {(isInitialized ? "✓" : "✗")}\n";
            status += $"当前标签页: {currentTabIndex}\n";
            status += $"标签页容器数量: {(tabContents?.Length ?? 0)}\n";
            status += $"标签页按钮数量: {(tabButtons?.Length ?? 0)}\n";
            status += $"权重配置预制体: {(questionWeightPrefab != null ? "✓" : "✗")}\n";
            status += $"权重配置面板实例: {(questionWeightPanelInstance != null ? "✓" : "✗")}\n";
            status += $"权重配置面板组件: {(questionWeightPanel != null ? "✓" : "✗")}\n";
            status += $"Timer配置预制体: {(timerConfigPrefab != null ? "✓" : "✗")}\n";
            status += $"Timer配置面板实例: {(timerConfigPanelInstance != null ? "✓" : "✗")}\n";
            status += $"Timer配置面板组件: {(timerConfigPanel != null ? "✓" : "✗")}\n";
            status += $"默认权重配置: {(defaultWeightConfig != null ? "✓" : "✗")}\n";
            status += $"默认Timer配置: {(defaultTimerConfig != null ? "✓" : "✗")}\n";

            if (tabContents != null)
            {
                for (int i = 0; i < tabContents.Length; i++)
                {
                    status += $"标签页{i}容器: {(tabContents[i] != null ? "✓" : "✗")}\n";
                }
            }

            return status;
        }

        #endregion

        #region 兼容性接口

        /// <summary>
        /// 应用配置变更（兼容方法）
        /// </summary>
        public void ApplyChanges()
        {
            LogDebug("收到ApplyChanges调用");
        }

        /// <summary>
        /// 刷新权重配置面板（兼容方法）
        /// </summary>
        public void RefreshWeightConfigPanel()
        {
            if (questionWeightPanel != null)
            {
                questionWeightPanel.RefreshDisplay();
                LogDebug("权重配置面板已刷新");
            }
            else
            {
                LogDebug("权重配置面板不存在，无法刷新");
            }
        }

        /// <summary>
        /// 刷新Timer配置面板
        /// </summary>
        public void RefreshTimerConfigPanel()
        {
            if (timerConfigPanel != null)
            {
                timerConfigPanel.RefreshDisplay();
                LogDebug("Timer配置面板已刷新");
            }
            else
            {
                LogDebug("Timer配置面板不存在，无法刷新");
            }
        }

        /// <summary>
        /// 验证当前配置有效性（兼容方法）
        /// </summary>
        public bool ValidateCurrentConfig()
        {
            bool isValid = true;

            // 验证权重配置
            var weightConfig = QuestionWeightManager.Config;
            if (weightConfig != null)
            {
                var weights = weightConfig.GetWeights();
                if (weights.Count == 0)
                {
                    LogDebug("权重配置无效");
                    isValid = false;
                }
            }
            else
            {
                LogDebug("权重配置未设置");
                isValid = false;
            }

            // 验证Timer配置
            var timerConfig = TimerConfigManager.Config;
            if (timerConfig != null)
            {
                if (!timerConfig.ValidateConfig())
                {
                    LogDebug("Timer配置无效");
                    isValid = false;
                }
            }
            else
            {
                LogDebug("Timer配置未设置");
                isValid = false;
            }

            LogDebug($"配置验证结果: {(isValid ? "有效" : "无效")}");
            return isValid;
        }

        #endregion

        #region 生命周期管理

        private void OnDestroy()
        {
            // 清理标签页按钮事件
            if (tabButtons != null)
            {
                foreach (var button in tabButtons)
                {
                    if (button != null)
                        button.onClick.RemoveAllListeners();
                }
            }

            LogDebug("RoomConfigUI资源已清理");
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
    }
}