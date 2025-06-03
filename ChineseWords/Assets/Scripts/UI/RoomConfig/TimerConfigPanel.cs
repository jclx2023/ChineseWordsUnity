using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Core;
using System.Collections.Generic;
using System.Linq;

namespace UI.RoomConfig
{
    /// <summary>
    /// Timer配置面板 - 支持单题型独立配置
    /// </summary>
    public class TimerConfigPanel : MonoBehaviour
    {
        [Header("UI组件引用 - 手动拖拽绑定")]
        [SerializeField] private Transform timerItemsContainer;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("预制体引用 - 手动拖拽绑定")]
        [SerializeField] private GameObject timerSliderItemPrefab;

        [Header("布局设置")]
        [SerializeField] private bool autoSetupLayout = true;
        [SerializeField] private float itemSpacing = 5f;
        [SerializeField] private float itemHeight = 40f;
        [SerializeField] private int paddingLeft = 10;
        [SerializeField] private int paddingRight = 10;
        [SerializeField] private int paddingTop = 10;
        [SerializeField] private int paddingBottom = 10;

        [Header("设置")]
        [SerializeField] private bool enableDebugLogs = false;
        [SerializeField] private bool preventDuplicateInit = true;

        // 支持的题型（单独配置，不分组）
        private static readonly QuestionType[] SupportedQuestionTypes = new QuestionType[]
        {
            QuestionType.ExplanationChoice,  // 词语解释
            QuestionType.SimularWordChoice,  // 近义词选择
            QuestionType.HardFill,           // 硬性填空
            QuestionType.SoftFill,           // 软性填空
            QuestionType.TextPinyin,         // 拼音填空
            QuestionType.IdiomChain,         // 成语接龙
            QuestionType.SentimentTorF,      // 情感判断
            QuestionType.UsageTorF,          // 用法判断
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
        /// Timer滑条项管理器 - 简化为单题型版本
        /// </summary>
        [System.Serializable]
        private class TimerSliderItemManager
        {
            public QuestionType questionType;
            public GameObject itemObject;
            public TimerSliderItem itemScript;

            public void Initialize(QuestionType type, GameObject obj)
            {
                questionType = type;
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

                var timerSettings = config.GetTimerForQuestionType(questionType);
                float timeLimit = timerSettings.baseTimeLimit;

                // 设置题型名称和时间
                itemScript.SetName(GetQuestionTypeDisplayName(questionType));
                itemScript.SetTimeLimit(timeLimit);
            }

            public void BindEvents(System.Action<QuestionType> onChanged)
            {
                if (itemScript != null)
                {
                    itemScript.OnValueChanged = (timeLimit) => onChanged?.Invoke(questionType);
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

            private string GetQuestionTypeDisplayName(QuestionType questionType)
            {
                switch (questionType)
                {
                    case QuestionType.ExplanationChoice: return "词语解释";
                    case QuestionType.SimularWordChoice: return "近义词选择";
                    case QuestionType.HardFill: return "硬性填空";
                    case QuestionType.SoftFill: return "软性填空";
                    case QuestionType.TextPinyin: return "拼音填空";
                    case QuestionType.IdiomChain: return "成语接龙";
                    case QuestionType.SentimentTorF: return "情感判断";
                    case QuestionType.UsageTorF: return "用法判断";
                    default: return questionType.ToString();
                }
            }
        }

        private void Awake()
        {
            CheckExternalManagement();

            if (!isExternallyManaged)
            {
                ValidateManualBindings();
            }
        }

        private void Start()
        {
            if (autoSetupLayout && timerItemsContainer != null)
            {
                SetupContainerLayout();
            }
        }

        /// <summary>
        /// 设置容器布局组件
        /// </summary>
        private void SetupContainerLayout()
        {
            try
            {
                var verticalLayoutGroup = timerItemsContainer.GetComponent<VerticalLayoutGroup>();
                if (verticalLayoutGroup == null)
                {
                    verticalLayoutGroup = timerItemsContainer.gameObject.AddComponent<VerticalLayoutGroup>();
                }

                var containerPadding = new RectOffset(paddingLeft, paddingRight, paddingTop, paddingBottom);

                verticalLayoutGroup.childAlignment = TextAnchor.UpperCenter;
                verticalLayoutGroup.childControlWidth = true;
                verticalLayoutGroup.childControlHeight = false;
                verticalLayoutGroup.childForceExpandWidth = true;
                verticalLayoutGroup.childForceExpandHeight = false;
                verticalLayoutGroup.spacing = itemSpacing;
                verticalLayoutGroup.padding = containerPadding;

                var contentSizeFitter = timerItemsContainer.GetComponent<ContentSizeFitter>();
                if (contentSizeFitter == null)
                {
                    contentSizeFitter = timerItemsContainer.gameObject.AddComponent<ContentSizeFitter>();
                }

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
                Debug.LogError("[TimerConfigPanel] timerItemsContainer未绑定！");
                isValid = false;
            }

            if (timerSliderItemPrefab == null)
            {
                Debug.LogError("[TimerConfigPanel] timerSliderItemPrefab未绑定！");
                isValid = false;
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

            // 关键修复：防止重复初始化 - 与QuestionWeightConfigPanel保持一致
            if (isInitialized && preventDuplicateInit)
            {
                LogDebug("已经初始化过，跳过重复初始化");
                return;
            }

            currentConfig = config;

            if (currentConfig == null)
            {
                LogDebug("传入的配置为空，使用默认配置");
                currentConfig = TimerConfigManager.Config ?? CreateDefaultConfig();
            }

            try
            {
                if (!ValidateRequiredComponents())
                {
                    return;
                }

                if (autoSetupLayout && timerItemsContainer != null)
                {
                    SetupContainerLayout();
                }

                ClearTimerItems();
                CreateTimerItems();
                RefreshDisplay();

                // 关键：标记为已初始化
                isInitialized = true;
                LogDebug($"Timer配置面板初始化完成，创建了 {timerItems.Count} 个配置项");
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
                Debug.LogError("[TimerConfigPanel] timerItemsContainer未设置");
                return false;
            }

            if (timerSliderItemPrefab == null)
            {
                Debug.LogError("[TimerConfigPanel] timerSliderItemPrefab未设置");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 创建Timer配置项 - 为每个题型创建独立配置
        /// </summary>
        private void CreateTimerItems()
        {
            try
            {
                foreach (var questionType in SupportedQuestionTypes)
                {
                    CreateTimerItemFromPrefab(questionType);
                }

                LogDebug($"创建了 {timerItems.Count} 个Timer配置项");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TimerConfigPanel] 创建Timer配置项失败: {e.Message}");
            }
        }

        /// <summary>
        /// 从预制体创建单个题型的Timer配置项
        /// </summary>
        private void CreateTimerItemFromPrefab(QuestionType questionType)
        {
            try
            {
                GameObject itemObj = Instantiate(timerSliderItemPrefab, timerItemsContainer);
                itemObj.name = $"TimerSliderItem_{questionType}";

                // 添加LayoutElement组件
                var layoutElement = itemObj.GetComponent<LayoutElement>();
                if (layoutElement == null)
                {
                    layoutElement = itemObj.AddComponent<LayoutElement>();
                }

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
                timerItem.Initialize(questionType, itemObj);

                if (timerItem.itemScript != null)
                {
                    timerItem.SetupFromConfig(currentConfig);
                    timerItem.BindEvents(OnTimerItemChanged);
                    timerItems.Add(timerItem);

                    LogDebug($"✓ 创建Timer项: {questionType}");
                }
                else
                {
                    Debug.LogError($"TimerSliderItem预制体缺少TimerSliderItem脚本: {questionType}");
                    Destroy(itemObj);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TimerConfigPanel] 创建Timer项 {questionType} 失败: {e.Message}");
            }
        }

        /// <summary>
        /// Timer项变化处理 - 处理单个题型的时间变更
        /// </summary>
        private void OnTimerItemChanged(QuestionType questionType)
        {
            if (currentConfig == null) return;

            try
            {
                var timerItem = timerItems.Find(item => item.questionType == questionType);
                if (timerItem == null) return;

                float newTimeLimit = timerItem.GetTimeLimit();

                // 更新单个题型的时间配置
                var timerSettings = new TimerConfig.QuestionTypeTimer
                {
                    questionType = questionType,
                    baseTimeLimit = newTimeLimit
                };
                currentConfig.SetTimerForQuestionType(questionType, timerSettings);

                LogDebug($"Timer变更: {questionType} = {newTimeLimit}秒");

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

                var allTimers = currentConfig.GetAllTimers();
                if (allTimers.Length > 0)
                {
                    float avgTime = allTimers.Average(t => t.baseTimeLimit);
                    float minTime = allTimers.Min(t => t.baseTimeLimit);
                    float maxTime = allTimers.Max(t => t.baseTimeLimit);

                    statusMessage += $"\n时间范围: {minTime:F0}-{maxTime:F0}秒, 平均: {avgTime:F1}秒";
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

                // 清理容器中的残留对象
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

                    foreach (var item in existingItems)
                    {
                        if (Application.isPlaying)
                            Destroy(item.gameObject);
                        else
                            DestroyImmediate(item.gameObject);
                    }
                }
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
                    LogDebug("重置Timer配置为默认值");
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
        [ContextMenu("显示组件状态")]
        private void EditorShowComponentStatus()
        {
            string status = "=== Timer配置面板组件状态 ===\n";
            status += $"Timer项容器: {(timerItemsContainer != null ? "✓" : "✗")}\n";
            status += $"Timer滑条预制体: {(timerSliderItemPrefab != null ? "✓" : "✗")}\n";
            status += $"当前配置: {(currentConfig != null ? "✓" : "✗")}\n";
            status += $"Timer项数量: {timerItems.Count}\n";
            status += $"状态文本: {(statusText != null ? "✓" : "✗")}\n";
            status += $"初始化完成: {isInitialized}\n";
            status += $"外部管理: {isExternallyManaged}\n";
            status += $"防重复初始化: {preventDuplicateInit}\n";
            LogDebug(status);
        }

        [ContextMenu("重置初始化状态")]
        private void EditorResetInitState()
        {
            if (Application.isPlaying)
            {
                isInitialized = false;
                LogDebug("初始化状态已重置");
            }
        }
#endif
    }
}