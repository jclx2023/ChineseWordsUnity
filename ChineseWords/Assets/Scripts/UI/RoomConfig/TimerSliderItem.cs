using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UI.RoomConfig
{
    /// <summary>
    /// Timer滑条项 - 简化版，移除Range显示功能
    /// 用于单题型时间配置
    /// </summary>
    public class TimerSliderItem : MonoBehaviour
    {
        [Header("UI组件 - 拖拽绑定")]
        [SerializeField] private Slider timeSlider;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI timeLabel;

        [Header("时间设置")]
        [SerializeField] private float minTime = 10f;
        [SerializeField] private float maxTime = 120f;
        [SerializeField] private bool useIntegerValues = true;

        [Header("自动查找设置")]
        [SerializeField] private bool autoFindComponents = true;
        [SerializeField] private bool enableDebugLogs = false;

        // 事件
        public System.Action<float> OnValueChanged;

        // 内部状态
        private bool isInitialized = false;

        private void Awake()
        {
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
            // 查找Slider
            if (timeSlider == null)
            {
                timeSlider = GetComponentInChildren<Slider>();
                LogDebug($"自动查找Slider: {(timeSlider != null ? "✓" : "✗")}");
            }

            // 查找TMPro文本组件
            if (nameLabel == null || timeLabel == null)
            {
                var tmpTexts = GetComponentsInChildren<TextMeshProUGUI>();
                LogDebug($"找到 {tmpTexts.Length} 个TMPro文本组件");

                if (tmpTexts.Length >= 2)
                {
                    if (nameLabel == null) nameLabel = tmpTexts[0];
                    if (timeLabel == null) timeLabel = tmpTexts[1];
                    LogDebug("自动分配TMPro文本组件");
                }
                else if (tmpTexts.Length == 1)
                {
                    if (nameLabel == null) nameLabel = tmpTexts[0];
                    LogDebug("自动分配单个TMPro文本组件为名称标签");
                }
            }

            // 如果没有TMPro，查找Legacy Text作为回退
            if (nameLabel == null)
            {
                var legacyTexts = GetComponentsInChildren<Text>();
                LogDebug($"TMPro未找到，查找Legacy Text: {legacyTexts.Length} 个");

                if (legacyTexts.Length >= 2)
                {
                    // 这里需要创建适配逻辑，因为TMPro和Text不兼容
                    LogDebug("发现Legacy Text，但当前脚本使用TMPro类型，需要手动绑定");
                }
            }
        }

        /// <summary>
        /// 绑定事件
        /// </summary>
        private void BindEvents()
        {
            if (timeSlider != null)
            {
                // 设置滑条范围
                timeSlider.minValue = minTime;
                timeSlider.maxValue = maxTime;
                timeSlider.wholeNumbers = useIntegerValues;

                // 绑定事件
                timeSlider.onValueChanged.RemoveAllListeners();
                timeSlider.onValueChanged.AddListener(OnSliderChanged);
                LogDebug("Slider事件已绑定");
            }
        }

        /// <summary>
        /// 设置题型名称
        /// </summary>
        public void SetName(string name)
        {
            if (nameLabel != null)
            {
                nameLabel.text = name;
                LogDebug($"设置名称: {name}");
            }
        }

        /// <summary>
        /// 设置时间限制
        /// </summary>
        public void SetTimeLimit(float timeLimit)
        {
            if (timeSlider != null)
            {
                timeSlider.value = Mathf.Clamp(timeLimit, minTime, maxTime);
            }

            UpdateTimeDisplay(timeLimit);
        }

        /// <summary>
        /// 获取当前时间限制
        /// </summary>
        public float GetTimeLimit()
        {
            if (timeSlider != null)
            {
                return useIntegerValues ? Mathf.Round(timeSlider.value) : timeSlider.value;
            }
            return 30f; // 默认值
        }

        /// <summary>
        /// 滑条变化处理
        /// </summary>
        private void OnSliderChanged(float value)
        {
            if (!isInitialized) return; // 防止初始化时触发

            float finalValue = useIntegerValues ? Mathf.Round(value) : value;
            UpdateTimeDisplay(finalValue);

            LogDebug($"Slider变化: {finalValue}秒");
            OnValueChanged?.Invoke(finalValue);
        }

        /// <summary>
        /// 更新时间显示
        /// </summary>
        private void UpdateTimeDisplay(float timeLimit)
        {
            if (timeLabel != null)
            {
                string timeText = useIntegerValues ?
                    $"{timeLimit:F0}秒" :
                    $"{timeLimit:F1}秒";

                timeLabel.text = timeText;
            }
        }

        /// <summary>
        /// 设置时间范围限制
        /// </summary>
        public void SetTimeRange(float min, float max)
        {
            minTime = min;
            maxTime = max;

            if (timeSlider != null)
            {
                timeSlider.minValue = minTime;
                timeSlider.maxValue = maxTime;

                // 确保当前值在新范围内
                timeSlider.value = Mathf.Clamp(timeSlider.value, minTime, maxTime);
            }

            LogDebug($"设置时间范围: {min}-{max}秒");
        }

        /// <summary>
        /// 获取组件状态信息
        /// </summary>
        public string GetComponentStatus()
        {
            var status = "=== TimerSliderItem组件状态 ===\n";
            status += $"Slider: {(timeSlider != null ? "✓" : "✗")}\n";
            status += $"名称标签: {(nameLabel != null ? "✓" : "✗")}\n";
            status += $"时间标签: {(timeLabel != null ? "✓" : "✗")}\n";
            status += $"初始化完成: {isInitialized}\n";
            status += $"时间范围: {minTime}-{maxTime}秒\n";
            status += $"当前时间: {GetTimeLimit()}秒\n";
            status += $"整数值: {useIntegerValues}\n";
            return status;
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[TimerSliderItem] {message}");
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        private void OnDestroy()
        {
            if (timeSlider != null)
                timeSlider.onValueChanged.RemoveAllListeners();

            LogDebug("TimerSliderItem已销毁");
        }

#if UNITY_EDITOR
        [ContextMenu("显示组件状态")]
        private void EditorShowComponentStatus()
        {
            LogDebug(GetComponentStatus());
        }

        [ContextMenu("测试设置值")]
        private void EditorTestSetValues()
        {
            if (Application.isPlaying)
            {
                SetName("测试题型");
                SetTimeLimit(45f);
            }
        }

        [ContextMenu("重新查找组件")]
        private void EditorRefindComponents()
        {
            if (Application.isPlaying)
            {
                AutoFindComponents();
            }
        }
#endif
    }
}