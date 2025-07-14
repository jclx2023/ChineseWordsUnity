using UnityEngine;
using UnityEngine.UI;
using Core;
using Core.Network;
using System.Collections.Generic;
using TMPro;

namespace UI.RoomConfig
{
    /// <summary>
    /// 精简版权重配置面板 - 移除预设按钮功能
    /// </summary>
    public class QuestionWeightConfigPanel : MonoBehaviour
    {
        [Header("预制体引用")]
        [SerializeField] private GameObject weightSliderItemPrefab;
        [SerializeField] private string weightSliderItemPrefabPath = "Prefabs/UI/WeightSliderItem";

        [Header("UI组件引用")]
        [SerializeField] private Transform weightItemsContainer;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("设置")]
        [SerializeField] private bool autoCreateUI = true;
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool preventDuplicateInit = true;

        // 支持的题型
        private static readonly QuestionType[] SupportedQuestionTypes = new QuestionType[]
        {
            QuestionType.IdiomChain,        // 成语接龙
            QuestionType.TextPinyin,        // 拼音填空
            QuestionType.HardFill,          // 硬性填空
            QuestionType.SoftFill,          // 软性填空
            QuestionType.SentimentTorF,     // 情感判断
            QuestionType.SimularWordChoice, // 近义词选择
            QuestionType.UsageTorF,         // 用法判断
            QuestionType.ExplanationChoice  // 词语解释
        };

        // 配置引用
        private QuestionWeightConfig currentConfig;

        // 权重项管理
        private List<WeightSliderItemManager> weightItems = new List<WeightSliderItemManager>();

        // 初始化状态
        private bool isInitialized = false;
        private bool isExternallyManaged = false;

        // 事件
        public System.Action OnConfigChanged;

        /// <summary>
        /// 权重滑条项管理器
        /// </summary>
        [System.Serializable]
        private class WeightSliderItemManager
        {
            public QuestionType questionType;
            public GameObject itemObject;
            public WeightSliderItem itemScript;

            public void Initialize(QuestionType type, GameObject obj)
            {
                questionType = type;
                itemObject = obj;
                itemScript = obj.GetComponent<WeightSliderItem>();
            }

            public void SetupFromConfig(QuestionWeightConfig config)
            {
                if (config == null || itemScript == null) return;

                bool enabled = config.IsEnabled(questionType);
                float weight = config.GetWeight(questionType);

                // 设置题型名称
                itemScript.SetName(GetQuestionTypeDisplayName(questionType));

                // 设置权重和启用状态
                itemScript.SetValues(weight, enabled);

                // 计算并设置百分比
                float percentage = CalculatePercentage(config, weight, enabled);
                itemScript.SetPercentage(percentage);
            }

            public void BindEvents(System.Action<QuestionType> onChanged)
            {
                if (itemScript != null)
                {
                    itemScript.OnValueChanged = (weight, enabled) => onChanged?.Invoke(questionType);
                }
            }

            public void UnbindEvents()
            {
                if (itemScript != null)
                {
                    itemScript.OnValueChanged = null;
                }
            }

            public bool GetEnabled()
            {
                return itemScript != null ? itemScript.GetEnabled() : true;
            }

            public float GetWeight()
            {
                return itemScript != null ? itemScript.GetWeight() : 1f;
            }

            private float CalculatePercentage(QuestionWeightConfig config, float weight, bool enabled)
            {
                if (!enabled) return 0f;

                var weights = config.GetWeights();
                float totalWeight = 0f;
                foreach (var w in weights.Values)
                    totalWeight += w;

                return totalWeight > 0 ? (weight / totalWeight) * 100f : 0f;
            }

            private string GetQuestionTypeDisplayName(QuestionType questionType)
            {
                switch (questionType)
                {
                    case QuestionType.IdiomChain: return "成语接龙";
                    case QuestionType.TextPinyin: return "拼音填空";
                    case QuestionType.HardFill: return "硬性填空";
                    case QuestionType.SoftFill: return "软性填空";
                    case QuestionType.SentimentTorF: return "情感判断";
                    case QuestionType.SimularWordChoice: return "近义词选择";
                    case QuestionType.UsageTorF: return "用法判断";
                    case QuestionType.ExplanationChoice: return "词语解释";
                    default: return questionType.ToString();
                }
            }
        }

        private void Awake()
        {
            // 检查是否被外部管理
            CheckExternalManagement();

            if (!isExternallyManaged)
            {
                LoadWeightSliderItemPrefab();
                if (autoCreateUI)
                {
                    AutoFindComponents();
                }
            }
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
        /// 加载WeightSliderItem预制体
        /// </summary>
        private void LoadWeightSliderItemPrefab()
        {
            if (weightSliderItemPrefab == null)
            {
                try
                {
                    weightSliderItemPrefab = Resources.Load<GameObject>(weightSliderItemPrefabPath);
                    if (weightSliderItemPrefab != null)
                    {
                        LogDebug($"成功加载WeightSliderItem: {weightSliderItemPrefabPath}");
                    }
                    else
                    {
                        string[] alternativePaths = {
                            "UI/WeightSliderItem",
                            "WeightSliderItem",
                            "Prefabs/WeightSliderItem"
                        };

                        foreach (string path in alternativePaths)
                        {
                            weightSliderItemPrefab = Resources.Load<GameObject>(path);
                            if (weightSliderItemPrefab != null)
                            {
                                LogDebug($"从备用路径加载WeightSliderItem: {path}");
                                break;
                            }
                        }

                        if (weightSliderItemPrefab == null)
                        {
                            Debug.LogError($"[QuestionWeightConfigPanel] 无法加载WeightSliderItem预制体");
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[QuestionWeightConfigPanel] 加载WeightSliderItem预制体失败: {e.Message}");
                }
            }
        }

        /// <summary>
        /// 自动查找组件
        /// </summary>
        private void AutoFindComponents()
        {
            try
            {
                if (weightItemsContainer == null)
                {
                    weightItemsContainer = FindWeightItemsContainer();
                }

                if (statusText == null)
                {
                    FindStatusText();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[QuestionWeightConfigPanel] 组件查找失败: {e.Message}");
            }
        }

        /// <summary>
        /// 查找权重项容器
        /// </summary>
        private Transform FindWeightItemsContainer()
        {
            string[] contentPaths = {
                "WeightContainer/Scroll View/Viewport/Content",
                "WeightContainer/ScrollView/Viewport/Content",
                "ScrollView/Viewport/Content",
                "Scroll View/Viewport/Content"
            };

            foreach (string path in contentPaths)
            {
                Transform content = transform.Find(path);
                if (content != null)
                {
                    LogDebug($"找到Content容器: {path}");
                    SetupContentLayout(content);
                    return content;
                }
            }

            var scrollRect = GetComponentInChildren<ScrollRect>();
            if (scrollRect != null && scrollRect.content != null)
            {
                LogDebug($"从ScrollRect找到容器: {scrollRect.content.name}");
                SetupContentLayout(scrollRect.content);
                return scrollRect.content;
            }

            return null;
        }

        /// <summary>
        /// 为Content容器设置布局组件
        /// </summary>
        private void SetupContentLayout(Transform content)
        {
            var verticalLayoutGroup = content.GetComponent<VerticalLayoutGroup>();
            if (verticalLayoutGroup == null)
            {
                verticalLayoutGroup = content.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            verticalLayoutGroup.childAlignment = TextAnchor.UpperCenter;
            verticalLayoutGroup.childControlWidth = true;
            verticalLayoutGroup.childControlHeight = false;
            verticalLayoutGroup.childForceExpandWidth = true;
            verticalLayoutGroup.childForceExpandHeight = false;
            verticalLayoutGroup.spacing = 5f;
            verticalLayoutGroup.padding = new RectOffset(10, 10, 10, 10);

            var contentSizeFitter = content.GetComponent<ContentSizeFitter>();
            if (contentSizeFitter == null)
            {
                contentSizeFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            }

            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        /// <summary>
        /// 查找状态文本
        /// </summary>
        private void FindStatusText()
        {
            string[] previewPaths = {
                "PreviewContainer/PreviewText",
                "PreviewText"
            };

            foreach (string path in previewPaths)
            {
                Transform previewText = transform.Find(path);
                if (previewText != null)
                {
                    statusText = previewText.GetComponent<TextMeshProUGUI>();
                    if (statusText != null)
                    {
                        LogDebug($"找到状态文本: {path}");
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 初始化配置面板
        /// </summary>
        public void Initialize(QuestionWeightConfig config)
        {
            // 防止重复初始化
            if (isInitialized && preventDuplicateInit)
            {
                LogDebug("已经初始化过，跳过重复初始化");
                return;
            }

            currentConfig = config;

            try
            {
                // 如果是外部管理模式，先准备组件
                if (isExternallyManaged)
                {
                    LoadWeightSliderItemPrefab();
                    AutoFindComponents();
                }

                // 清空现有内容并创建新的权重项
                ClearWeightItems();
                CreateWeightItems();
                RefreshDisplay();

                isInitialized = true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[QuestionWeightConfigPanel] 初始化失败: {e.Message}");
            }
        }

        /// <summary>
        /// 创建权重配置项
        /// </summary>
        private void CreateWeightItems()
        {
            try
            {
                foreach (QuestionType questionType in SupportedQuestionTypes)
                {
                    CreateWeightItemFromPrefab(questionType);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[QuestionWeightConfigPanel] 创建权重配置项失败: {e.Message}");
            }
        }

        /// <summary>
        /// 从WeightSliderItem预制体创建权重配置项
        /// </summary>
        private void CreateWeightItemFromPrefab(QuestionType questionType)
        {
            try
            {
                GameObject itemObj = Instantiate(weightSliderItemPrefab, weightItemsContainer);
                itemObj.name = $"WeightSliderItem_{questionType}";

                // 添加Layout Element组件
                var layoutElement = itemObj.GetComponent<LayoutElement>();
                if (layoutElement == null)
                {
                    layoutElement = itemObj.AddComponent<LayoutElement>();
                }

                layoutElement.preferredHeight = 40f;
                layoutElement.flexibleWidth = 1f;
                layoutElement.minHeight = 35f;

                // 设置RectTransform
                var rectTransform = itemObj.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.anchorMin = new Vector2(0, 1);
                    rectTransform.anchorMax = new Vector2(1, 1);
                    rectTransform.pivot = new Vector2(0.5f, 1f);
                    rectTransform.sizeDelta = new Vector2(0, 40);
                    rectTransform.anchoredPosition = Vector2.zero;
                }

                // 创建权重项管理器
                var weightItem = new WeightSliderItemManager();
                weightItem.Initialize(questionType, itemObj);

                if (weightItem.itemScript != null)
                {
                    weightItem.SetupFromConfig(currentConfig);
                    weightItem.BindEvents(OnWeightItemChanged);
                    weightItems.Add(weightItem);
                }
                else
                {
                    Debug.LogError($"WeightSliderItem预制体缺少WeightSliderItem脚本，删除对象: {questionType}");
                    Destroy(itemObj);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[QuestionWeightConfigPanel] 创建权重项 {questionType} 失败: {e.Message}");
            }
        }

        /// <summary>
        /// 权重项变化处理
        /// </summary>
        private void OnWeightItemChanged(QuestionType questionType)
        {
            if (currentConfig == null) return;

            try
            {
                var weightItem = weightItems.Find(item => item.questionType == questionType);
                if (weightItem == null) return;

                bool enabled = weightItem.GetEnabled();
                float weight = weightItem.GetWeight();

                currentConfig.SetEnabled(questionType, enabled);
                currentConfig.SetWeight(questionType, weight);

                RefreshPercentages();
                UpdateStatusText();
                OnConfigChanged?.Invoke();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[QuestionWeightConfigPanel] 处理权重变更失败: {e.Message}");
            }
        }

        /// <summary>
        /// 刷新显示
        /// </summary>
        public void RefreshDisplay()
        {
            if (currentConfig == null) return;

            try
            {
                foreach (var weightItem in weightItems)
                {
                    weightItem.SetupFromConfig(currentConfig);
                }
                UpdateStatusText();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[QuestionWeightConfigPanel] 刷新显示失败: {e.Message}");
            }
        }

        /// <summary>
        /// 刷新百分比显示
        /// </summary>
        private void RefreshPercentages()
        {
            if (currentConfig == null) return;

            foreach (var weightItem in weightItems)
            {
                weightItem.SetupFromConfig(currentConfig);
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
                var weights = currentConfig.GetWeights();
                float totalWeight = 0f;
                foreach (var weight in weights.Values)
                    totalWeight += weight;

                string statusMessage = $"启用题型: {weights.Count}/{SupportedQuestionTypes.Length}, 总权重: {totalWeight:F1}";
                statusText.text = statusMessage;
            }
            catch (System.Exception e)
            {
                LogDebug($"更新状态文本失败: {e.Message}");
            }
        }

        /// <summary>
        /// 清空权重项
        /// </summary>
        private void ClearWeightItems()
        {
            try
            {
                foreach (var item in weightItems)
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

                weightItems.Clear();

                // 额外清理容器中的残留对象
                if (weightItemsContainer != null)
                {
                    var existingItems = new System.Collections.Generic.List<Transform>();
                    foreach (Transform child in weightItemsContainer)
                    {
                        if (child.name.Contains("WeightItem") || child.name.Contains("WeightSlider"))
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
                Debug.LogError($"[QuestionWeightConfigPanel] 清空权重项失败: {e.Message}");
            }
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[QuestionWeightConfigPanel] {message}");
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        private void OnDestroy()
        {
            try
            {
                ClearWeightItems();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[QuestionWeightConfigPanel] 销毁时清理失败: {e.Message}");
            }
        }
    }
}