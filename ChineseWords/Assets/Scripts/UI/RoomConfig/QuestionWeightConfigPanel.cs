using UnityEngine;
using UnityEngine.UI;
using Core;
using Core.Network;
using System.Collections.Generic;
using TMPro;

namespace UI.RoomConfig
{
    /// <summary>
    /// 完整修复版权重配置面板 - 使用WeightSliderItem预制体
    /// 关键修复：
    /// 1. 添加WeightSliderItem预制体引用字段
    /// 2. 防止重复初始化
    /// 3. 只创建8个支持的题型
    /// 4. 正确处理外部管理模式
    /// </summary>
    public class QuestionWeightConfigPanel : MonoBehaviour
    {
        [Header("预制体引用")]
        [SerializeField] private GameObject weightSliderItemPrefab; // 拖入WeightSliderItem.prefab
        [SerializeField] private string weightSliderItemPrefabPath = "Prefabs/UI/WeightSliderItem";

        [Header("UI组件引用 - 自动查找或手动绑定")]
        [SerializeField] private Transform weightItemsContainer;
        [SerializeField] private Button[] presetButtons;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("自动创建UI")]
        [SerializeField] private bool autoCreateUI = true;
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool preventDuplicateInit = true; // 防止重复初始化

        // 支持的题型（只包含8个，排除HandWriting和AbbrFill）
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
        private bool isExternallyManaged = false; // 标记是否被外部管理

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

                if (itemScript == null)
                {
                    Debug.LogError($"WeightSliderItem预制体上没有WeightSliderItem脚本: {obj.name}");
                }
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
            LogDebug("QuestionWeightConfigPanel Awake");

            // 检查是否被外部管理
            CheckExternalManagement();

            if (!isExternallyManaged)
            {
                LogDebug("独立模式，执行自动初始化");
                // 只有当不被外部管理时，才执行自动初始化
                LoadWeightSliderItemPrefab();
                if (autoCreateUI)
                {
                    AutoFindComponents();
                }
            }
            else
            {
                LogDebug("被外部管理，等待外部初始化");
            }
        }

        private void Start()
        {
            LogDebug("QuestionWeightConfigPanel Start");

            if (isExternallyManaged)
            {
                LogDebug("被外部管理，跳过Start逻辑");
                return;
            }

            // 设置预设按钮
            SetupPresetButtons();
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
                    // 尝试从Resources加载
                    weightSliderItemPrefab = Resources.Load<GameObject>(weightSliderItemPrefabPath);
                    if (weightSliderItemPrefab != null)
                    {
                        LogDebug($"✓ 成功从Resources加载WeightSliderItem: {weightSliderItemPrefabPath}");
                    }
                    else
                    {
                        // 尝试其他可能的路径
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
                                LogDebug($"✓ 从备用路径加载WeightSliderItem: {path}");
                                break;
                            }
                        }

                        if (weightSliderItemPrefab == null)
                        {
                            Debug.LogError($"[QuestionWeightConfigPanel] ❌ 无法加载WeightSliderItem预制体");
                            Debug.LogError($"请确保WeightSliderItem.prefab在以下位置之一：");
                            Debug.LogError($"- Resources/{weightSliderItemPrefabPath}");
                            Debug.LogError($"- 或在Inspector中直接拖入weightSliderItemPrefab字段");
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[QuestionWeightConfigPanel] 加载WeightSliderItem预制体失败: {e.Message}");
                }
            }
            else
            {
                LogDebug("✓ WeightSliderItem预制体已在Inspector中设置");
            }
        }

        /// <summary>
        /// 自动查找组件
        /// </summary>
        private void AutoFindComponents()
        {
            LogDebug("开始自动查找UI组件");

            try
            {
                // 查找权重项容器
                if (weightItemsContainer == null)
                {
                    weightItemsContainer = FindWeightItemsContainer();
                }

                // 查找状态文本
                if (statusText == null)
                {
                    FindStatusText();
                }

                LogDebug($"组件查找完成 - 容器: {(weightItemsContainer != null ? "✓" : "✗")}, 状态文本: {(statusText != null ? "✓" : "✗")}");
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
            // 基于预制体结构查找Content
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
                    return content;
                }
            }

            // 备用查找方法
            var scrollRect = GetComponentInChildren<ScrollRect>();
            if (scrollRect != null && scrollRect.content != null)
            {
                LogDebug($"从ScrollRect找到容器: {scrollRect.content.name}");
                return scrollRect.content;
            }

            LogDebug("未找到合适的权重项容器");
            return null;
        }

        /// <summary>
        /// 查找状态文本
        /// </summary>
        private void FindStatusText()
        {
            // 查找预制体中的PreviewText
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

            LogDebug("未找到状态文本");
        }

        /// <summary>
        /// 初始化配置面板
        /// </summary>
        public void Initialize(QuestionWeightConfig config)
        {
            LogDebug($"Initialize调用 - 已初始化: {isInitialized}, 防重复: {preventDuplicateInit}");

            // 强制防止重复初始化
            if (isInitialized && preventDuplicateInit)
            {
                LogDebug("已经初始化过，强制跳过重复初始化");
                return;
            }

            currentConfig = config;

            if (currentConfig == null)
            {
                LogDebug("警告：传入的配置为空");
                return;
            }

            try
            {
                // 如果是外部管理模式，先准备组件
                if (isExternallyManaged)
                {
                    LogDebug("外部管理模式，准备组件...");
                    LoadWeightSliderItemPrefab();
                    AutoFindComponents();
                }

                // 检查必要组件
                if (weightItemsContainer == null)
                {
                    Debug.LogError("[QuestionWeightConfigPanel] weightItemsContainer未找到，无法创建权重项");
                    return;
                }

                if (weightSliderItemPrefab == null)
                {
                    Debug.LogError("[QuestionWeightConfigPanel] weightSliderItemPrefab未设置，无法创建权重项");
                    return;
                }

                LogDebug($"开始创建权重项，支持的题型数量: {SupportedQuestionTypes.Length}");

                // 强制清空现有内容
                ClearWeightItems();

                // 创建新的权重项
                CreateWeightItems();

                // 刷新显示
                RefreshDisplay();

                isInitialized = true;
                LogDebug($"✓ 权重配置面板初始化完成，实际创建项目数: {weightItems.Count}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[QuestionWeightConfigPanel] 初始化失败: {e.Message}");
                Debug.LogError($"错误堆栈: {e.StackTrace}");
            }
        }

        /// <summary>
        /// 创建权重配置项
        /// </summary>
        private void CreateWeightItems()
        {
            LogDebug("开始创建权重配置项");

            try
            {
                // 只创建支持的题型
                foreach (QuestionType questionType in SupportedQuestionTypes)
                {
                    CreateWeightItemFromPrefab(questionType);
                }

                LogDebug($"创建了 {weightItems.Count} 个权重配置项");
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
                // 从预制体实例化
                GameObject itemObj = Instantiate(weightSliderItemPrefab, weightItemsContainer);
                itemObj.name = $"WeightSliderItem_{questionType}";

                // 创建权重项管理器
                var weightItem = new WeightSliderItemManager();
                weightItem.Initialize(questionType, itemObj);

                if (weightItem.itemScript != null)
                {
                    // 从配置设置初始值
                    weightItem.SetupFromConfig(currentConfig);

                    // 绑定事件
                    weightItem.BindEvents(OnWeightItemChanged);

                    weightItems.Add(weightItem);

                    LogDebug($"✓ 创建权重项: {questionType}");
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

                // 从UI获取当前值
                bool enabled = weightItem.GetEnabled();
                float weight = weightItem.GetWeight();

                // 更新配置
                currentConfig.SetEnabled(questionType, enabled);
                currentConfig.SetWeight(questionType, weight);

                LogDebug($"权重变更: {questionType} = {weight} (启用: {enabled})");

                // 刷新所有项目的百分比显示
                RefreshPercentages();

                // 更新状态文本
                UpdateStatusText();

                // 触发配置变更事件
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

            LogDebug("刷新权重配置显示");

            try
            {
                // 刷新所有权重项
                foreach (var weightItem in weightItems)
                {
                    weightItem.SetupFromConfig(currentConfig);
                }

                // 更新状态文本
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
        /// 设置预设按钮
        /// </summary>
        private void SetupPresetButtons()
        {
            LogDebug($"设置 {presetButtons?.Length ?? 0} 个预设按钮");

            if (presetButtons != null)
            {
                for (int i = 0; i < presetButtons.Length; i++)
                {
                    if (presetButtons[i] != null)
                    {
                        int index = i; // 避免闭包问题
                        presetButtons[i].onClick.RemoveAllListeners();
                        presetButtons[i].onClick.AddListener(() => ApplyPreset(index));
                    }
                }
            }
        }

        /// <summary>
        /// 应用预设配置
        /// </summary>
        private void ApplyPreset(int presetIndex)
        {
            if (currentConfig == null) return;

            LogDebug($"应用预设配置: {presetIndex}");

            try
            {
                // 根据预设索引应用不同配置
                switch (presetIndex)
                {
                    case 0: // 均衡模式
                        ApplyBalancedPreset();
                        break;
                    case 1: // 填空重点
                        ApplyFillFocusPreset();
                        break;
                    case 2: // 选择重点
                        ApplyChoiceFocusPreset();
                        break;
                    default:
                        ApplyBalancedPreset();
                        break;
                }

                RefreshDisplay();
                OnConfigChanged?.Invoke();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[QuestionWeightConfigPanel] 应用预设配置失败: {e.Message}");
            }
        }

        /// <summary>
        /// 应用均衡预设
        /// </summary>
        private void ApplyBalancedPreset()
        {
            foreach (var type in SupportedQuestionTypes)
            {
                currentConfig.SetEnabled(type, true);
                currentConfig.SetWeight(type, 1f);
            }
        }

        /// <summary>
        /// 应用填空重点预设
        /// </summary>
        private void ApplyFillFocusPreset()
        {
            // 填空类题型权重更高
            currentConfig.SetEnabled(QuestionType.IdiomChain, true);
            currentConfig.SetWeight(QuestionType.IdiomChain, 2f);

            currentConfig.SetEnabled(QuestionType.TextPinyin, true);
            currentConfig.SetWeight(QuestionType.TextPinyin, 3f);

            currentConfig.SetEnabled(QuestionType.HardFill, true);
            currentConfig.SetWeight(QuestionType.HardFill, 3f);

            currentConfig.SetEnabled(QuestionType.SoftFill, true);
            currentConfig.SetWeight(QuestionType.SoftFill, 2f);

            // 其他题型权重较低
            currentConfig.SetEnabled(QuestionType.SentimentTorF, true);
            currentConfig.SetWeight(QuestionType.SentimentTorF, 0.5f);

            currentConfig.SetEnabled(QuestionType.SimularWordChoice, true);
            currentConfig.SetWeight(QuestionType.SimularWordChoice, 0.5f);

            currentConfig.SetEnabled(QuestionType.UsageTorF, true);
            currentConfig.SetWeight(QuestionType.UsageTorF, 0.5f);

            currentConfig.SetEnabled(QuestionType.ExplanationChoice, true);
            currentConfig.SetWeight(QuestionType.ExplanationChoice, 0.5f);
        }

        /// <summary>
        /// 应用选择重点预设
        /// </summary>
        private void ApplyChoiceFocusPreset()
        {
            // 选择类题型权重更高
            currentConfig.SetEnabled(QuestionType.SimularWordChoice, true);
            currentConfig.SetWeight(QuestionType.SimularWordChoice, 3f);

            currentConfig.SetEnabled(QuestionType.ExplanationChoice, true);
            currentConfig.SetWeight(QuestionType.ExplanationChoice, 3f);

            currentConfig.SetEnabled(QuestionType.SentimentTorF, true);
            currentConfig.SetWeight(QuestionType.SentimentTorF, 2f);

            currentConfig.SetEnabled(QuestionType.UsageTorF, true);
            currentConfig.SetWeight(QuestionType.UsageTorF, 2f);

            // 填空类题型权重较低
            currentConfig.SetEnabled(QuestionType.IdiomChain, true);
            currentConfig.SetWeight(QuestionType.IdiomChain, 0.5f);

            currentConfig.SetEnabled(QuestionType.TextPinyin, true);
            currentConfig.SetWeight(QuestionType.TextPinyin, 0.5f);

            currentConfig.SetEnabled(QuestionType.HardFill, true);
            currentConfig.SetWeight(QuestionType.HardFill, 0.5f);

            currentConfig.SetEnabled(QuestionType.SoftFill, true);
            currentConfig.SetWeight(QuestionType.SoftFill, 0.5f);
        }

        /// <summary>
        /// 清空权重项
        /// </summary>
        private void ClearWeightItems()
        {
            LogDebug($"清空现有权重项，当前数量: {weightItems.Count}");

            try
            {
                // 解绑事件并销毁对象
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

                // 清空列表
                weightItems.Clear();

                // 额外清理：检查容器中是否还有其他权重项对象
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

                    if (existingItems.Count > 0)
                    {
                        LogDebug($"发现容器中还有 {existingItems.Count} 个残留权重项，将全部清除");
                        foreach (var item in existingItems)
                        {
                            if (Application.isPlaying)
                                Destroy(item.gameObject);
                            else
                                DestroyImmediate(item.gameObject);
                        }
                    }
                }

                LogDebug("权重项清空完成");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[QuestionWeightConfigPanel] 清空权重项失败: {e.Message}");
            }
        }

        /// <summary>
        /// 验证配置有效性
        /// </summary>
        public bool ValidateConfig()
        {
            if (currentConfig == null) return false;

            try
            {
                var weights = currentConfig.GetWeights();
                return weights.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取配置摘要
        /// </summary>
        public string GetConfigSummary()
        {
            if (currentConfig == null) return "未配置";

            try
            {
                var weights = currentConfig.GetWeights();
                return $"已启用 {weights.Count}/{SupportedQuestionTypes.Length} 种题型";
            }
            catch
            {
                return "配置读取失败";
            }
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

                    // 只重置支持的题型
                    foreach (var type in SupportedQuestionTypes)
                    {
                        currentConfig.SetEnabled(type, true);
                        currentConfig.SetWeight(type, 1f);
                    }

                    RefreshDisplay();
                    OnConfigChanged?.Invoke();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[QuestionWeightConfigPanel] 重置配置失败: {e.Message}");
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

                // 清理预设按钮事件
                if (presetButtons != null)
                {
                    foreach (var button in presetButtons)
                    {
                        if (button != null)
                            button.onClick.RemoveAllListeners();
                    }
                }

                LogDebug("权重配置面板已销毁");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[QuestionWeightConfigPanel] 销毁时清理失败: {e.Message}");
            }
        }

#if UNITY_EDITOR
        [ContextMenu("强制重新初始化")]
        private void EditorForceReinitialize()
        {
            if (Application.isPlaying && currentConfig != null)
            {
                LogDebug("编辑器：强制重新初始化");
                isInitialized = false;
                LoadWeightSliderItemPrefab();
                AutoFindComponents();
                Initialize(currentConfig);
            }
        }

        [ContextMenu("显示组件状态")]
        private void EditorShowComponentStatus()
        {
            string status = "=== 组件状态 ===\n";
            status += $"权重项容器: {(weightItemsContainer != null ? "✓" : "✗")}\n";
            status += $"权重滑条预制体: {(weightSliderItemPrefab != null ? "✓" : "✗")}\n";
            status += $"当前配置: {(currentConfig != null ? "✓" : "✗")}\n";
            status += $"权重项数量: {weightItems.Count}\n"; } } }
#endif