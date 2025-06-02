using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UI.RoomConfig
{
    /// <summary>
    /// Timer滑条项 - 用于配置单个题型组的时间限制
    /// 类似于WeightSliderItem的设计
    /// </summary>
    public class TimerSliderItem : MonoBehaviour
    {
        [Header("UI组件 - 手动拖拽绑定")]
        [SerializeField] private Slider timeLimitSlider;
        [SerializeField] private TextMeshProUGUI nameLabel_TMP;
        [SerializeField] private TextMeshProUGUI timeLabel_TMP;
        [SerializeField] private TextMeshProUGUI rangeLabel_TMP;

        [Header("文本组件 - Legacy Text回退（可选）")]
        [SerializeField] private Text nameLabel_Legacy;
        [SerializeField] private Text timeLabel_Legacy;
        [SerializeField] private Text rangeLabel_Legacy;

        [Header("滑条配置")]
        [Range(5f, 120f)]
        [SerializeField] private float minTimeLimit = 5f;
        [Range(5f, 120f)]
        [SerializeField] private float maxTimeLimit = 120f;

        [Header("设置")]
        [SerializeField] private bool autoFindComponents = false; // 改为false，使用手动绑定
        [SerializeField] private bool enableDebugLogs = true;

        // 事件
        public System.Action<float> OnValueChanged;

        // 内部状态
        private bool isInitialized = false;

        private void Awake()
        {
            LogDebug("TimerSliderItem Awake");

            // 验证手动绑定的组件
            ValidateManualBindings();

            SetupSlider();
            BindEvents();
            isInitialized = true;
        }

        /// <summary>
        /// 验证手动绑定的组件
        /// </summary>
        private void ValidateManualBindings()
        {
            bool isValid = true;

            if (timeLimitSlider == null)
            {
                Debug.LogError("[TimerSliderItem] timeLimitSlider未绑定！请在Inspector中拖拽Slider组件到此字段");
                isValid = false;
            }

            if (nameLabel_TMP == null && nameLabel_Legacy == null)
            {
                Debug.LogError("[TimerSliderItem] 名称标签未绑定！请至少绑定nameLabel_TMP或nameLabel_Legacy");
                isValid = false;
            }

            if (timeLabel_TMP == null && timeLabel_Legacy == null)
            {
                LogDebug("时间标签未绑定，时间显示功能将不可用");
            }

            LogDebug($"手动绑定验证完成 - {(isValid ? "✓ 通过" : "✗ 失败")}");
        }

        /// <summary>
        /// 设置滑条配置
        /// </summary>
        private void SetupSlider()
        {
            if (timeLimitSlider == null)
            {
                LogDebug("警告：时间限制滑条未找到");
                return;
            }

            // 设置滑条范围
            timeLimitSlider.minValue = minTimeLimit;
            timeLimitSlider.maxValue = maxTimeLimit;
            timeLimitSlider.wholeNumbers = true; // 只允许整数值

            // 设置默认值
            if (timeLimitSlider.value < minTimeLimit || timeLimitSlider.value > maxTimeLimit)
            {
                timeLimitSlider.value = 30f; // 默认30秒
            }

            LogDebug($"滑条配置完成: 范围 {minTimeLimit}-{maxTimeLimit}秒，当前值 {timeLimitSlider.value}秒");
        }

        /// <summary>
        /// 绑定事件
        /// </summary>
        private void BindEvents()
        {
            if (timeLimitSlider != null)
            {
                timeLimitSlider.onValueChanged.RemoveAllListeners();
                timeLimitSlider.onValueChanged.AddListener(OnSliderChanged);
                LogDebug("Slider事件已绑定");
            }
        }

        /// <summary>
        /// 设置时间限制
        /// </summary>
        public void SetTimeLimit(float timeLimit)
        {
            LogDebug($"设置时间限制: {timeLimit}秒");

            if (timeLimitSlider != null)
            {
                timeLimitSlider.value = Mathf.Clamp(timeLimit, minTimeLimit, maxTimeLimit);
            }

            UpdateTimeDisplay();
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
        /// 设置时间范围显示
        /// </summary>
        public void SetTimeRange(string rangeText)
        {
            // 优先使用TMPro
            if (rangeLabel_TMP != null)
            {
                rangeLabel_TMP.text = rangeText;
            }
            else if (rangeLabel_Legacy != null)
            {
                rangeLabel_Legacy.text = rangeText;
            }

            LogDebug($"设置时间范围: {rangeText}");
        }

        /// <summary>
        /// 获取当前时间限制
        /// </summary>
        public float GetTimeLimit()
        {
            return timeLimitSlider != null ? timeLimitSlider.value : 30f;
        }

        /// <summary>
        /// 设置滑条范围
        /// </summary>
        public void SetTimeRange(float min, float max)
        {
            minTimeLimit = Mathf.Clamp(min, 5f, 120f);
            maxTimeLimit = Mathf.Clamp(max, minTimeLimit, 120f);

            if (timeLimitSlider != null)
            {
                timeLimitSlider.minValue = minTimeLimit;
                timeLimitSlider.maxValue = maxTimeLimit;

                // 确保当前值在新范围内
                if (timeLimitSlider.value < minTimeLimit)
                    timeLimitSlider.value = minTimeLimit;
                else if (timeLimitSlider.value > maxTimeLimit)
                    timeLimitSlider.value = maxTimeLimit;
            }

            LogDebug($"设置时间范围: {minTimeLimit}-{maxTimeLimit}秒");
        }

        /// <summary>
        /// Slider变化处理
        /// </summary>
        private void OnSliderChanged(float value)
        {
            LogDebug($"Slider变化: {value}秒");

            UpdateTimeDisplay();
            TriggerValueChanged();
        }

        /// <summary>
        /// 更新时间显示
        /// </summary>
        private void UpdateTimeDisplay()
        {
            if (timeLimitSlider == null) return;

            float currentTime = timeLimitSlider.value;
            string timeText = FormatTime(currentTime);

            // 更新时间显示
            if (timeLabel_TMP != null)
            {
                timeLabel_TMP.text = timeText;
            }
            else if (timeLabel_Legacy != null)
            {
                timeLabel_Legacy.text = timeText;
            }

            // 根据时间长短设置颜色
            UpdateTimeColor(currentTime);
        }

        /// <summary>
        /// 格式化时间显示
        /// </summary>
        private string FormatTime(float timeInSeconds)
        {
            int seconds = Mathf.RoundToInt(timeInSeconds);

            if (seconds >= 60)
            {
                int minutes = seconds / 60;
                int remainingSeconds = seconds % 60;
                return $"{minutes}:{remainingSeconds:00}";
            }
            else
            {
                return $"{seconds}秒";
            }
        }

        /// <summary>
        /// 根据时间长短更新颜色
        /// </summary>
        private void UpdateTimeColor(float timeInSeconds)
        {
            Color timeColor;

            if (timeInSeconds <= 10f)
            {
                timeColor = Color.red; // 短时间：红色
            }
            else if (timeInSeconds <= 20f)
            {
                timeColor = Color.yellow; // 中等时间：黄色
            }
            else
            {
                timeColor = Color.green; // 长时间：绿色
            }

            // 应用颜色
            if (timeLabel_TMP != null)
            {
                timeLabel_TMP.color = timeColor;
            }
            else if (timeLabel_Legacy != null)
            {
                timeLabel_Legacy.color = timeColor;
            }
        }

        /// <summary>
        /// 触发值变化事件
        /// </summary>
        private void TriggerValueChanged()
        {
            if (!isInitialized) return; // 防止初始化时触发

            float timeLimit = GetTimeLimit();
            LogDebug($"触发值变化事件: {timeLimit}秒");
            OnValueChanged?.Invoke(timeLimit);
        }

        /// <summary>
        /// 获取组件状态信息
        /// </summary>
        public string GetComponentStatus()
        {
            var status = "=== TimerSliderItem组件状态 ===\n";
            status += $"Slider: {(timeLimitSlider != null ? "✓" : "✗")}\n";
            status += $"名称标签(TMPro): {(nameLabel_TMP != null ? "✓" : "✗")}\n";
            status += $"时间标签(TMPro): {(timeLabel_TMP != null ? "✓" : "✗")}\n";
            status += $"范围标签(TMPro): {(rangeLabel_TMP != null ? "✓" : "✗")}\n";
            status += $"名称标签(Legacy): {(nameLabel_Legacy != null ? "✓" : "✗")}\n";
            status += $"时间标签(Legacy): {(timeLabel_Legacy != null ? "✓" : "✗")}\n";
            status += $"范围标签(Legacy): {(rangeLabel_Legacy != null ? "✓" : "✗")}\n";
            status += $"初始化完成: {isInitialized}\n";
            status += $"当前时间: {GetTimeLimit()}秒\n";
            status += $"时间范围: {minTimeLimit}-{maxTimeLimit}秒\n";
            return status;
        }

        /// <summary>
        /// 设置启用状态
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            if (timeLimitSlider != null)
            {
                timeLimitSlider.interactable = enabled;
            }

            // 设置文本透明度
            float alpha = enabled ? 1f : 0.5f;
            SetTextAlpha(alpha);

            LogDebug($"设置启用状态: {enabled}");
        }

        /// <summary>
        /// 设置文本透明度
        /// </summary>
        private void SetTextAlpha(float alpha)
        {
            if (nameLabel_TMP != null)
            {
                var color = nameLabel_TMP.color;
                color.a = alpha;
                nameLabel_TMP.color = color;
            }
            else if (nameLabel_Legacy != null)
            {
                var color = nameLabel_Legacy.color;
                color.a = alpha;
                nameLabel_Legacy.color = color;
            }

            if (timeLabel_TMP != null)
            {
                var color = timeLabel_TMP.color;
                color.a = alpha;
                timeLabel_TMP.color = color;
            }
            else if (timeLabel_Legacy != null)
            {
                var color = timeLabel_Legacy.color;
                color.a = alpha;
                timeLabel_Legacy.color = color;
            }
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
            // 清理事件监听
            if (timeLimitSlider != null)
                timeLimitSlider.onValueChanged.RemoveAllListeners();

            LogDebug("TimerSliderItem已销毁");
        }

#if UNITY_EDITOR
        [ContextMenu("验证手动绑定")]
        private void EditorValidateManualBindings()
        {
            ValidateManualBindings();
        }

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
                SetTimeRange("30-60秒");
            }
        }

        [ContextMenu("测试时间格式化")]
        private void EditorTestTimeFormatting()
        {
            LogDebug("测试时间格式化:");
            LogDebug($"10秒 -> {FormatTime(10f)}");
            LogDebug($"30秒 -> {FormatTime(30f)}");
            LogDebug($"60秒 -> {FormatTime(60f)}");
            LogDebug($"90秒 -> {FormatTime(90f)}");
            LogDebug($"125秒 -> {FormatTime(125f)}");
        }
#endif
    }
}