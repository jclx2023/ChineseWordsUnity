using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Core;
using System.Collections.Generic;
using System.Linq;

namespace UI.RoomConfig
{
    /// <summary>
    /// Timer配置面板 - 修复布局问题
    /// </summary>
    public class TimerConfigPanel : MonoBehaviour
    {
        [Header("UI组件引用 - 手动拖拽绑定")]
        [SerializeField] private Transform timerItemsContainer;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("预制体引用 - 手动拖拽绑定")]
        [SerializeField] private GameObject timerSliderItemPrefab;

        [Header("布局设置")]
        [SerializeField] private bool autoSetupLayout = true; // 自动设置布局
        [SerializeField] private float itemSpacing = 5f;
        [SerializeField] private float itemHeight = 40f;
        [SerializeField] private int paddingLeft = 10;
        [SerializeField] private int paddingRight = 10;
        [SerializeField] private int paddingTop = 10;
        [SerializeField] private int paddingBottom = 10;

        [Header("自动创建UI")]
        [SerializeField] private bool autoFindComponents = false;
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool preventDuplicateInit = true;

        // 支持的题型分组（简化为4大类）
        private static readonly Dictionary<string, QuestionType[]> SupportedQuestionGroups = new Dictionary<string, QuestionType[]>
        {
            { "选择题", new[] { QuestionType.ExplanationChoice, QuestionType.SimularWordChoice } },
            { "填空题", new[] { QuestionType.HardFill, QuestionType.SoftFill, QuestionType.TextPinyin, QuestionType.IdiomChain } },
            { "判断题", new[] { QuestionType.SentimentTorF, QuestionType.UsageTorF } },
            { "手写题", new[] { QuestionType.HandWriting } }
        };

        // 配置引用
        private TimerConfig currentConfig;

        // Timer项管理
        private List<TimerSliderItemManager> timerItems = new List<TimerSliderItemManager>();

        // 初始化状态
        private bool isInitialized = false;
        private bool isExternallyManaged = false;

        // 事件
        public System.Action OnConfigChanged;

        /// <summary>
        /// Timer滑条项管理器
        /// </summary>
        [System.Serializable]
        private class TimerSliderItemManager
        {
            public string groupName;
            public QuestionType[] questionTypes;
            public GameObject itemObject;
            public TimerSliderItem itemScript;

            public void Initialize(string name, QuestionType[] types, GameObject obj)
            {
                groupName = name;
                questionTypes = types;
                itemObject = obj;
                itemScript = obj.GetComponent<TimerSliderItem>();

                if (itemScript == null)
                {
                    Debug.LogError($"TimerSliderItem预制体上没有TimerSliderItem脚本: {obj.name}");
                }
            }

            public void SetupFromConfig(TimerConfig config)
            {
                if (config == null || itemScript == null) return;

                // 获取该组第一个题型的时间作为代表
                var representativeType = questionTypes.First();
                var timerSettings = config.GetTimerForQuestionType(representativeType);
                float timeLimit = timerSettings.baseTimeLimit;

                // 设置名称和时间
                itemScript.SetName(groupName);
                itemScript.SetTimeLimit(timeLimit);

                // 显示时间范围（如果组内有不同时间）
                var allTimes = questionTypes.Select(type => config.GetTimeLimitForQuestionType(type)).Distinct().ToArray();
                if (allTimes.Length > 1)
                {
                    itemScript.SetTimeRange($"{allTimes.Min()}-{allTimes.Max()}秒");
                }
                else
                {
                    itemScript.SetTimeRange($"{timeLimit}秒");
                }
            }

            public void BindEvents(System.Action<string> onChanged)
            {
                if (itemScript != null)
                {
                    itemScript.OnValueChanged = (timeLimit) => onChanged?.Invoke(groupName);
                }
            }

            public void UnbindEvents()
            {
                if (itemScript != null)
                {
                    itemScript.OnValueChanged = null;
                }
            }

            public float GetTimeLimit()
            {
                return itemScript != null ? itemScript.GetTimeLimit() : 30f;
            }
        }

        private void Awake()
        {
            LogDebug("TimerConfigPanel Awake");
            CheckExternalManagement();

            if (!isExternallyManaged)
            {
                LogDebug("独立模式，验证手动绑定的组件");
                ValidateManualBindings();
            }
        }

        private void Start()
        {
            LogDebug("TimerConfigPanel Start");

            // 如果启用自动布局设置，设置容器布局
            if (autoSetupLayout && timerItemsContainer != null)
            {
                SetupContainerLayout();
            }
        }

        /// <summary>
        /// 设置容器布局组件 - 修复滑条挤在一起的问题
        /// </summary>
        private void SetupContainerLayout()
        {
            LogDebug("设置Timer容器布局");

            try
            {
                // 添加或获取VerticalLayoutGroup
                var verticalLayoutGroup = timerItemsContainer.GetComponent<VerticalLayoutGroup>();
                if (verticalLayoutGroup == null)
                {
                    verticalLayoutGroup = timerItemsContainer.gameObject.AddComponent<VerticalLayoutGroup>();
                    LogDebug("添加VerticalLayoutGroup组件");
                }

                // 创建RectOffset（在运行时创建，避免构造函数问题）
                var containerPadding = new RectOffset(paddingLeft, paddingRight, paddingTop, paddingBottom);

                // 配置VerticalLayoutGroup
                verticalLayoutGroup.childAlignment = TextAnchor.UpperCenter;
                verticalLayoutGroup.childControlWidth = true;
                verticalLayoutGroup.childControlHeight = false;
                verticalLayoutGroup.childForceExpandWidth = true;
                verticalLayoutGroup.childForceExpandHeight = false;
                verticalLayoutGroup.spacing = itemSpacing;
                verticalLayoutGroup.padding = containerPadding;

                LogDebug($"VerticalLayoutGroup配置: spacing={itemSpacing}, padding=({paddingLeft},{paddingRight},{paddingTop},{paddingBottom})");

                // 添加或获取ContentSizeFitter
                var contentSizeFitter = timerItemsContainer.GetComponent<ContentSizeFitter>();
                if (contentSizeFitter == null)
                {
                    contentSizeFitter = timerItemsContainer.gameObject.AddComponent<ContentSizeFitter>();
                    LogDebug("添加ContentSizeFitter组件");
                }

                // 配置ContentSizeFitter
                contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

                LogDebug("Timer容器布局设置完成");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TimerConfigPanel] 设置容器布局失败: {e.Message}");
            }
        }

        /// <summary>
        /// 验证手动绑定的组件
        /// </summary>
        private void ValidateManualBindings()
        {
            bool isValid = true;

            if (timerItemsContainer == null)
            {
                Debug.LogError("[TimerConfigPanel] timerItemsContainer未绑定！请在Inspector中拖拽Content到此字段");
                isValid = false;
            }

            if (timerSliderItemPrefab == null)
            {
                Debug.LogError("[TimerConfigPanel] timerSliderItemPrefab未绑定！请在Inspector中拖拽TimerSliderItem预制体到此字段");
                isValid = false;
            }

            if (statusText == null)
            {
                LogDebug("statusText未绑定，状态显示功能将不可用");
            }

            LogDebug($"手动绑定验证完成 - {(isValid ? "✓ 通过" : "✗ 失败")}");
        }

        /// <summary>
        /// 检查是否被外部管理
        /// </summary>
        private void CheckExternalManagement()
        {
            Transform parent = transform.parent;
            while (parent != null)
            {
                if (parent.name.Contains("TabContent") || parent.name.Contains("Instance") || parent.name.Contains("RoomConfig"))
                {
                    isExternallyManaged = true;
                    LogDebug($"检测到被外部管理，父对象: {parent.name}");
                    break;
                }
                parent = parent.parent;
            }
        }

        /// <summary>
        /// 初始化配置面板
        /// </summary>
        public void Initialize(TimerConfig config)
        {
            LogDebug($"Initialize调用 - 已初始化: {isInitialized}, 防重复: {preventDuplicateInit}");

            if (isInitialized && preventDuplicateInit)
            {
                LogDebug("已经初始化过，强制跳过重复初始化");
                return;
            }

            currentConfig = config;

            if (currentConfig == null)
            {
                LogDebug("警告：传入的配置为空，使用默认配置");
                currentConfig = TimerConfigManager.Config ?? CreateDefaultConfig();
            }

            try
            {
                // 验证必要组件
                if (!ValidateRequiredComponents())
                {
                    return;
                }

                // 确保布局设置完成
                if (autoSetupLayout && timerItemsContainer != null)
                {
                    SetupContainerLayout();
                }

                LogDebug($"开始创建Timer项，题型组数量: {SupportedQuestionGroups.Count}");

                ClearTimerItems();
                CreateTimerItems();
                RefreshDisplay();

                isInitialized = true;
                LogDebug($"✓ Timer配置面板初始化完成，实际创建项目数: {timerItems.Count}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TimerConfigPanel] 初始化失败: {e.Message}");
            }
        }

        /// <summary>
        /// 验证必要组件
        /// </summary>
        private bool ValidateRequiredComponents()
        {
            if (timerItemsContainer == null)
            {
                Debug.LogError("[TimerConfigPanel] timerItemsContainer未设置，无法创建Timer项");
                return false;
            }

            if (timerSliderItemPrefab == null)
            {
                Debug.LogError("[TimerConfigPanel] timerSliderItemPrefab未设置，无法创建Timer项");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 创建Timer配置项
        /// </summary>
        private void CreateTimerItems()
        {
            LogDebug("开始创建Timer配置项");

            try
            {
                foreach (var group in SupportedQuestionGroups)
                {
                    CreateTimerItemFromPrefab(group.Key, group.Value);
                }

                LogDebug($"创建了 {timerItems.Count} 个Timer配置项");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TimerConfigPanel] 创建Timer配置项失败: {e.Message}");
            }
        }

        /// <summary>
        /// 从预制体创建Timer配置项 - 添加布局支持
        /// </summary>
        private void CreateTimerItemFromPrefab(string groupName, QuestionType[] questionTypes)
        {
            try
            {
                GameObject itemObj = Instantiate(timerSliderItemPrefab, timerItemsContainer);
                itemObj.name = $"TimerSliderItem_{groupName}";

                // 添加LayoutElement组件 - 关键修复
                var layoutElement = itemObj.GetComponent<LayoutElement>();
                if (layoutElement == null)
                {
                    layoutElement = itemObj.AddComponent<LayoutElement>();
                    LogDebug($"为{groupName}添加LayoutElement组件");
                }

                // 配置LayoutElement
                layoutElement.preferredHeight = itemHeight;
                layoutElement.flexibleWidth = 1f;
                layoutElement.minHeight = itemHeight - 5f;

                // 设置RectTransform
                var rectTransform = itemObj.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.anchorMin = new Vector2(0, 1);
                    rectTransform.anchorMax = new Vector2(1, 1);
                    rectTransform.pivot = new Vector2(0.5f, 1f);
                    rectTransform.sizeDelta = new Vector2(0, itemHeight);
                    rectTransform.anchoredPosition = Vector2.zero;
                }

                // 创建Timer项管理器
                var timerItem = new TimerSliderItemManager();
                timerItem.Initialize(groupName, questionTypes, itemObj);

                if (timerItem.itemScript != null)
                {
                    timerItem.SetupFromConfig(currentConfig);
                    timerItem.BindEvents(OnTimerItemChanged);
                    timerItems.Add(timerItem);

                    LogDebug($"✓ 创建Timer项: {groupName} (高度: {itemHeight})");
                }
                else
                {
                    Debug.LogError($"TimerSliderItem预制体缺少TimerSliderItem脚本，删除对象: {groupName}");
                    Destroy(itemObj);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TimerConfigPanel] 创建Timer项 {groupName} 失败: {e.Message}");
            }
        }

        /// <summary>
        /// Timer项变化处理
        /// </summary>
        private void OnTimerItemChanged(string groupName)
        {
            if (currentConfig == null) return;

            try
            {
                var timerItem = timerItems.Find(item => item.groupName == groupName);
                if (timerItem == null) return;

                float newTimeLimit = timerItem.GetTimeLimit();

                // 更新该组所有题型的时间
                foreach (var questionType in timerItem.questionTypes)
                {
                    var timerSettings = new TimerConfig.QuestionTypeTimer
                    {
                        questionType = questionType,
                        baseTimeLimit = newTimeLimit
                    };
                    currentConfig.SetTimerForQuestionType(questionType, timerSettings);
                }

                LogDebug($"Timer变更: {groupName} = {newTimeLimit}秒");

                UpdateStatusText();
                OnConfigChanged?.Invoke();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TimerConfigPanel] 处理Timer变更失败: {e.Message}");
            }
        }

        /// <summary>
        /// 刷新显示
        /// </summary>
        public void RefreshDisplay()
        {
            if (currentConfig == null) return;

            LogDebug("刷新Timer配置显示");

            try
            {
                foreach (var timerItem in timerItems)
                {
                    timerItem.SetupFromConfig(currentConfig);
                }

                UpdateStatusText();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TimerConfigPanel] 刷新显示失败: {e.Message}");
            }
        }

        /// <summary>
        /// 更新状态文本
        /// </summary>
        private void UpdateStatusText()
        {
            if (statusText == null || currentConfig == null) return;

            try
            {
                var globalSettings = TimerConfigManager.GetGlobalSettings();
                string statusMessage = $"警告阈值: {globalSettings.warningThreshold}秒, 危险阈值: {globalSettings.criticalThreshold}秒";

                // 添加平均时间信息
                var allTimers = currentConfig.GetAllTimers();
                if (allTimers.Length > 0)
                {
                    float avgTime = allTimers.Average(t => t.baseTimeLimit);
                    statusMessage += $"\n平均答题时间: {avgTime:F1}秒";
                }

                statusText.text = statusMessage;
            }
            catch (System.Exception e)
            {
                LogDebug($"更新状态文本失败: {e.Message}");
            }
        }

        /// <summary>
        /// 清空Timer项
        /// </summary>
        private void ClearTimerItems()
        {
            LogDebug($"清空现有Timer项，当前数量: {timerItems.Count}");

            try
            {
                foreach (var item in timerItems)
                {
                    item.UnbindEvents();

                    if (item.itemObject != null)
                    {
                        if (Application.isPlaying)
                            Destroy(item.itemObject);
                        else
                            DestroyImmediate(item.itemObject);
                    }
                }

                timerItems.Clear();

                if (timerItemsContainer != null)
                {
                    var existingItems = new System.Collections.Generic.List<Transform>();
                    foreach (Transform child in timerItemsContainer)
                    {
                        if (child.name.Contains("TimerItem") || child.name.Contains("TimerSlider"))
                        {
                            existingItems.Add(child);
                        }
                    }

                    if (existingItems.Count > 0)
                    {
                        LogDebug($"发现容器中还有 {existingItems.Count} 个残留Timer项，将全部清除");
                        foreach (var item in existingItems)
                        {
                            if (Application.isPlaying)
                                Destroy(item.gameObject);
                            else
                                DestroyImmediate(item.gameObject);
                        }
                    }
                }

                LogDebug("Timer项清空完成");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TimerConfigPanel] 清空Timer项失败: {e.Message}");
            }
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        private TimerConfig CreateDefaultConfig()
        {
            var defaultConfig = ScriptableObject.CreateInstance<TimerConfig>();
            defaultConfig.ResetToDefault();
            return defaultConfig;
        }

        /// <summary>
        /// 验证配置有效性
        /// </summary>
        public bool ValidateConfig()
        {
            return currentConfig?.ValidateConfig() ?? false;
        }

        /// <summary>
        /// 获取配置摘要
        /// </summary>
        public string GetConfigSummary()
        {
            return currentConfig?.GetConfigSummary() ?? "未配置";
        }

        /// <summary>
        /// 重置为默认配置
        /// </summary>
        public void ResetToDefault()
        {
            if (currentConfig != null)
            {
                try
                {
                    LogDebug("重置为默认配置");
                    currentConfig.ResetToDefault();
                    RefreshDisplay();
                    OnConfigChanged?.Invoke();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[TimerConfigPanel] 重置配置失败: {e.Message}");
                }
            }
        }

        /// <summary>
        /// 强制重新设置布局
        /// </summary>
        public void ForceSetupLayout()
        {
            if (timerItemsContainer != null)
            {
                SetupContainerLayout();
                LogDebug("强制重新设置布局完成");
            }
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[TimerConfigPanel] {message}");
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        private void OnDestroy()
        {
            try
            {
                ClearTimerItems();
                LogDebug("Timer配置面板已销毁");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TimerConfigPanel] 销毁时清理失败: {e.Message}");
            }
        }

#if UNITY_EDITOR
        [ContextMenu("验证手动绑定")]
        private void EditorValidateManualBindings()
        {
            ValidateManualBindings();
        }

        [ContextMenu("强制设置布局")]
        private void EditorForceSetupLayout()
        {
            if (Application.isPlaying)
            {
                ForceSetupLayout();
            }
        }

        [ContextMenu("显示组件状态")]
        private void EditorShowComponentStatus()
        {
            string status = "=== Timer配置面板组件状态 ===\n";
            status += $"Timer项容器: {(timerItemsContainer != null ? "✓" : "✗")}\n";
            status += $"Timer滑条预制体: {(timerSliderItemPrefab != null ? "✓" : "✗")}\n";
            status += $"当前配置: {(currentConfig != null ? "✓" : "✗")}\n";
            status += $"Timer项数量: {timerItems.Count}\n";
            status += $"状态文本: {(statusText != null ? "✓" : "✗")}\n";
            status += $"自动布局: {autoSetupLayout}\n";
            status += $"项目间距: {itemSpacing}\n";
            status += $"项目高度: {itemHeight}\n";
            status += $"容器内边距: ({paddingLeft},{paddingRight},{paddingTop},{paddingBottom})\n";

            if (timerItemsContainer != null)
            {
                var vlg = timerItemsContainer.GetComponent<VerticalLayoutGroup>();
                var csf = timerItemsContainer.GetComponent<ContentSizeFitter>();
                status += $"VerticalLayoutGroup: {(vlg != null ? "✓" : "✗")}\n";
                status += $"ContentSizeFitter: {(csf != null ? "✓" : "✗")}\n";
            }

            LogDebug(status);
        }

        [ContextMenu("测试创建Timer项")]
        private void EditorTestCreateTimerItems()
        {
            if (Application.isPlaying && ValidateRequiredComponents())
            {
                if (currentConfig == null)
                    currentConfig = CreateDefaultConfig();

                // 强制设置布局
                SetupContainerLayout();

                ClearTimerItems();
                CreateTimerItems();
                LogDebug("测试创建Timer项完成");
            }
        }
#endif
    }
}