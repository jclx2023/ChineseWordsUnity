using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UI.RoomConfig
{
    /// <summary>
    /// 权重滑条项 - 支持TMPro和Legacy Text的混合版本
    /// </summary>
    public class WeightSliderItem : MonoBehaviour
    {
        [Header("UI组件 - 拖拽绑定")]
        [SerializeField] private Toggle enableToggle;
        [SerializeField] private Slider weightSlider;

        [Header("文本组件 - 优先使用TMPro")]
        [SerializeField] private TextMeshProUGUI nameLabel_TMP;
        [SerializeField] private TextMeshProUGUI percentLabel_TMP;

        [Header("文本组件 - Legacy Text回退")]
        [SerializeField] private Text nameLabel_Legacy;
        [SerializeField] private Text percentLabel_Legacy;

        [Header("自动查找设置")]
        [SerializeField] private bool autoFindComponents = true;
        [SerializeField] private bool enableDebugLogs = true;

        // 事件
        public System.Action<float, bool> OnValueChanged;

        // 内部状态
        private bool isInitialized = false;

        private void Awake()
        {
            LogDebug("WeightSliderItem Awake");

            if (autoFindComponents)
            {
                AutoFindComponents();
            }

            BindEvents();
            isInitialized = true;
        }

        /// <summary>
        /// 自动查找组件
        /// </summary>
        private void AutoFindComponents()
        {
            LogDebug("开始自动查找组件");

            // 查找Toggle
            if (enableToggle == null)
            {
                enableToggle = GetComponentInChildren<Toggle>();
                LogDebug($"自动查找Toggle: {(enableToggle != null ? "✓" : "✗")}");
            }

            // 查找Slider
            if (weightSlider == null)
            {
                weightSlider = GetComponentInChildren<Slider>();
                LogDebug($"自动查找Slider: {(weightSlider != null ? "✓" : "✗")}");
            }

            // 查找TMPro文本组件
            if (nameLabel_TMP == null || percentLabel_TMP == null)
            {
                var tmpTexts = GetComponentsInChildren<TextMeshProUGUI>();
                LogDebug($"找到 {tmpTexts.Length} 个TMPro文本组件");

                if (tmpTexts.Length >= 2)
                {
                    if (nameLabel_TMP == null) nameLabel_TMP = tmpTexts[0];
                    if (percentLabel_TMP == null) percentLabel_TMP = tmpTexts[1];
                    LogDebug("自动分配TMPro文本组件");
                }
                else if (tmpTexts.Length == 1)
                {
                    if (nameLabel_TMP == null) nameLabel_TMP = tmpTexts[0];
                    LogDebug("自动分配单个TMPro文本组件为名称标签");
                }
            }

            // 如果没有TMPro，查找Legacy Text作为回退
            if (nameLabel_TMP == null && nameLabel_Legacy == null)
            {
                var legacyTexts = GetComponentsInChildren<Text>();
                LogDebug($"TMPro未找到，查找Legacy Text: {legacyTexts.Length} 个");

                if (legacyTexts.Length >= 2)
                {
                    nameLabel_Legacy = legacyTexts[0];
                    percentLabel_Legacy = legacyTexts[1];
                    LogDebug("使用Legacy Text作为回退");
                }
                else if (legacyTexts.Length == 1)
                {
                    nameLabel_Legacy = legacyTexts[0];
                    LogDebug("使用单个Legacy Text作为名称标签");
                }
            }
        }

        /// <summary>
        /// 绑定事件
        /// </summary>
        private void BindEvents()
        {
            // 绑定Toggle事件
            if (enableToggle != null)
            {
                enableToggle.onValueChanged.RemoveAllListeners();
                enableToggle.onValueChanged.AddListener(OnToggleChanged);
                LogDebug("Toggle事件已绑定");
            }

            // 绑定Slider事件
            if (weightSlider != null)
            {
                weightSlider.onValueChanged.RemoveAllListeners();
                weightSlider.onValueChanged.AddListener(OnSliderChanged);
                LogDebug("Slider事件已绑定");
            }
        }

        /// <summary>
        /// 设置权重和启用状态
        /// </summary>
        public void SetValues(float weight, bool enabled)
        {
            LogDebug($"设置值: weight={weight}, enabled={enabled}");

            if (weightSlider != null)
            {
                weightSlider.value = weight;
                weightSlider.interactable = enabled;
            }

            if (enableToggle != null)
            {
                enableToggle.isOn = enabled;
            }
        }

        /// <summary>
        /// 设置百分比显示
        /// </summary>
        public void SetPercentage(float percentage)
        {
            string percentText = $"{percentage:F1}%";

            // 优先使用TMPro
            if (percentLabel_TMP != null)
            {
                percentLabel_TMP.text = percentText;
            }
            else if (percentLabel_Legacy != null)
            {
                percentLabel_Legacy.text = percentText;
            }

            LogDebug($"设置百分比: {percentText}");
        }

        /// <summary>
        /// 设置名称标签
        /// </summary>
        public void SetName(string name)
        {
            // 优先使用TMPro
            if (nameLabel_TMP != null)
            {
                nameLabel_TMP.text = name;
            }
            else if (nameLabel_Legacy != null)
            {
                nameLabel_Legacy.text = name;
            }

            LogDebug($"设置名称: {name}");
        }

        /// <summary>
        /// 获取当前权重值
        /// </summary>
        public float GetWeight()
        {
            return weightSlider != null ? weightSlider.value : 0f;
        }

        /// <summary>
        /// 获取当前启用状态
        /// </summary>
        public bool GetEnabled()
        {
            return enableToggle != null ? enableToggle.isOn : true;
        }

        /// <summary>
        /// Toggle变化处理
        /// </summary>
        private void OnToggleChanged(bool enabled)
        {
            LogDebug($"Toggle变化: {enabled}");

            // 根据启用状态控制Slider可交互性
            if (weightSlider != null)
            {
                weightSlider.interactable = enabled;
            }

            TriggerValueChanged();
        }

        /// <summary>
        /// Slider变化处理
        /// </summary>
        private void OnSliderChanged(float value)
        {
            LogDebug($"Slider变化: {value}");
            TriggerValueChanged();
        }

        /// <summary>
        /// 触发值变化事件
        /// </summary>
        private void TriggerValueChanged()
        {
            if (!isInitialized) return; // 防止初始化时触发

            float weight = GetWeight();
            bool enabled = GetEnabled();

            LogDebug($"触发值变化事件: weight={weight}, enabled={enabled}");
            OnValueChanged?.Invoke(weight, enabled);
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[WeightSliderItem] {message}");
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        private void OnDestroy()
        {
            // 清理事件监听
            if (enableToggle != null)
                enableToggle.onValueChanged.RemoveAllListeners();

            if (weightSlider != null)
                weightSlider.onValueChanged.RemoveAllListeners();

            LogDebug("WeightSliderItem已销毁");
        }
    }
}