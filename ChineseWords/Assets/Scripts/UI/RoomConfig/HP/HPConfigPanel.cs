using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Core;

namespace UI.RoomConfig
{
    /// <summary>
    /// HP配置面板 - 仿照TimerConfigPanel模式
    /// 管理生命值和扣血量配置
    /// </summary>
    public class HPConfigPanel : MonoBehaviour
    {
        [Header("UI组件引用 - 手动拖拽绑定")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider damageSlider;
        [SerializeField] private TextMeshProUGUI healthValueText;
        [SerializeField] private TextMeshProUGUI damageValueText;
        [SerializeField] private TextMeshProUGUI previewText;

        [Header("滑条配置")]
        [Range(10f, 500f)]
        [SerializeField] private float minHealth = 10f;
        [Range(10f, 500f)]
        [SerializeField] private float maxHealth = 500f;
        [Range(1f, 100f)]
        [SerializeField] private float minDamage = 1f;
        [Range(1f, 100f)]
        [SerializeField] private float maxDamage = 100f;

        [Header("显示格式")]
        [SerializeField] private string healthFormat = "{0}";
        [SerializeField] private string damageFormat = "{0}";
        [SerializeField] private string previewFormat = "最多可答错 {0} 题";

        [Header("设置")]
        [SerializeField] private bool enableDebugLogs = false;
        [SerializeField] private bool preventDuplicateInit = true;
        [SerializeField] private bool updatePreviewRealtime = true;

        // 配置引用
        private HPConfig currentConfig;

        // 初始化状态
        private bool isInitialized = false;
        private bool isExternallyManaged = false;
        private bool isUpdatingUI = false; // 防止循环更新

        // 事件
        public System.Action OnConfigChanged;

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
            SetupSliders();
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
        /// 验证手动绑定的组件
        /// </summary>
        private void ValidateManualBindings()
        {
            bool isValid = true;

            if (healthSlider == null)
            {
                Debug.LogError("[HPConfigPanel] healthSlider未绑定！");
                isValid = false;
            }

            if (damageSlider == null)
            {
                Debug.LogError("[HPConfigPanel] damageSlider未绑定！");
                isValid = false;
            }

            if (healthValueText == null)
            {
                Debug.LogError("[HPConfigPanel] healthValueText未绑定！");
                isValid = false;
            }

            if (damageValueText == null)
            {
                Debug.LogError("[HPConfigPanel] damageValueText未绑定！");
                isValid = false;
            }

            if (previewText == null)
            {
                Debug.LogError("[HPConfigPanel] previewText未绑定！");
                isValid = false;
            }

            LogDebug($"手动绑定验证完成 - {(isValid ? "✓ 通过" : "✗ 失败")}");
        }

        /// <summary>
        /// 设置滑条属性和事件
        /// </summary>
        private void SetupSliders()
        {
            if (healthSlider != null)
            {
                healthSlider.minValue = minHealth;
                healthSlider.maxValue = maxHealth;
                healthSlider.wholeNumbers = true;
                healthSlider.onValueChanged.RemoveAllListeners();
                healthSlider.onValueChanged.AddListener(OnHealthSliderChanged);
            }

            if (damageSlider != null)
            {
                damageSlider.minValue = minDamage;
                damageSlider.maxValue = maxDamage;
                damageSlider.wholeNumbers = true;
                damageSlider.onValueChanged.RemoveAllListeners();
                damageSlider.onValueChanged.AddListener(OnDamageSliderChanged);
            }

            LogDebug("滑条设置完成");
        }

        /// <summary>
        /// 初始化配置面板
        /// </summary>
        public void Initialize(HPConfig config)
        {
            LogDebug($"Initialize调用 - 已初始化: {isInitialized}, 防重复: {preventDuplicateInit}");

            // 防止重复初始化
            if (isInitialized && preventDuplicateInit)
            {
                LogDebug("已经初始化过，跳过重复初始化");
                return;
            }

            currentConfig = config;

            if (currentConfig == null)
            {
                LogDebug("传入的配置为空，使用默认配置");
                currentConfig = HPConfigManager.Config ?? CreateDefaultConfig();
            }

            try
            {
                if (!ValidateRequiredComponents())
                {
                    return;
                }

                SetupSliders();
                RefreshDisplay();

                isInitialized = true;
                LogDebug("HP配置面板初始化完成");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HPConfigPanel] 初始化失败: {e.Message}");
            }
        }

        /// <summary>
        /// 验证必要组件
        /// </summary>
        private bool ValidateRequiredComponents()
        {
            return healthSlider != null && damageSlider != null &&
                   healthValueText != null && damageValueText != null && previewText != null;
        }

        /// <summary>
        /// 刷新显示
        /// </summary>
        public void RefreshDisplay()
        {
            if (currentConfig == null) return;

            LogDebug("刷新HP配置显示");

            try
            {
                isUpdatingUI = true;

                // 更新滑条值
                if (healthSlider != null)
                    healthSlider.value = currentConfig.GetCurrentHealth();

                if (damageSlider != null)
                    damageSlider.value = currentConfig.GetDamagePerWrong();

                // 更新文本显示
                UpdateValueTexts();
                UpdatePreviewText();

                isUpdatingUI = false;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HPConfigPanel] 刷新显示失败: {e.Message}");
                isUpdatingUI = false;
            }
        }

        /// <summary>
        /// 生命值滑条变化处理
        /// </summary>
        private void OnHealthSliderChanged(float value)
        {
            if (isUpdatingUI || currentConfig == null) return;

            try
            {
                float health = Mathf.Round(value);
                currentConfig.SetCurrentHealth(health);

                LogDebug($"生命值变更: {health}");

                UpdateValueTexts();
                if (updatePreviewRealtime)
                    UpdatePreviewText();

                OnConfigChanged?.Invoke();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HPConfigPanel] 处理生命值变更失败: {e.Message}");
            }
        }

        /// <summary>
        /// 扣血量滑条变化处理
        /// </summary>
        private void OnDamageSliderChanged(float value)
        {
            if (isUpdatingUI || currentConfig == null) return;

            try
            {
                float damage = Mathf.Round(value);
                currentConfig.SetDamagePerWrong(damage);

                LogDebug($"扣血量变更: {damage}");

                UpdateValueTexts();
                if (updatePreviewRealtime)
                    UpdatePreviewText();

                OnConfigChanged?.Invoke();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HPConfigPanel] 处理扣血量变更失败: {e.Message}");
            }
        }

        /// <summary>
        /// 更新数值文本
        /// </summary>
        private void UpdateValueTexts()
        {
            if (currentConfig == null) return;

            if (healthValueText != null)
            {
                healthValueText.text = string.Format(healthFormat, currentConfig.GetCurrentHealth());
            }

            if (damageValueText != null)
            {
                damageValueText.text = string.Format(damageFormat, currentConfig.GetDamagePerWrong());
            }
        }

        /// <summary>
        /// 更新预览文本
        /// </summary>
        private void UpdatePreviewText()
        {
            if (currentConfig == null || previewText == null) return;

            try
            {
                int maxWrong = currentConfig.GetMaxWrongAnswers();
                float survivalRate = maxWrong > 0 ? (float)maxWrong / (maxWrong + 1) * 100f : 0f;

                string preview = string.Format(previewFormat, maxWrong);
                preview += $"\n容错率: {survivalRate:F1}%";

                previewText.text = preview;
            }
            catch (System.Exception e)
            {
                LogDebug($"更新预览文本失败: {e.Message}");
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
                    LogDebug("重置HP配置为默认值");
                    currentConfig.ResetToDefault();
                    RefreshDisplay();
                    OnConfigChanged?.Invoke();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[HPConfigPanel] 重置配置失败: {e.Message}");
                }
            }
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
        /// 创建默认配置
        /// </summary>
        private HPConfig CreateDefaultConfig()
        {
            var defaultConfig = ScriptableObject.CreateInstance<HPConfig>();
            defaultConfig.ResetToDefault();
            return defaultConfig;
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[HPConfigPanel] {message}");
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        private void OnDestroy()
        {
            if (healthSlider != null)
                healthSlider.onValueChanged.RemoveAllListeners();

            if (damageSlider != null)
                damageSlider.onValueChanged.RemoveAllListeners();

            LogDebug("HP配置面板已销毁");
        }

#if UNITY_EDITOR
        [ContextMenu("显示组件状态")]
        private void EditorShowComponentStatus()
        {
            string status = "=== HP配置面板组件状态 ===\n";
            status += $"生命值滑条: {(healthSlider != null ? "✓" : "✗")}\n";
            status += $"扣血量滑条: {(damageSlider != null ? "✓" : "✗")}\n";
            status += $"生命值文本: {(healthValueText != null ? "✓" : "✗")}\n";
            status += $"扣血量文本: {(damageValueText != null ? "✓" : "✗")}\n";
            status += $"预览文本: {(previewText != null ? "✓" : "✗")}\n";
            status += $"当前配置: {(currentConfig != null ? "✓" : "✗")}\n";
            status += $"初始化完成: {isInitialized}\n";
            status += $"外部管理: {isExternallyManaged}\n";
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

        [ContextMenu("测试刷新显示")]
        private void EditorTestRefreshDisplay()
        {
            if (Application.isPlaying)
            {
                RefreshDisplay();
                LogDebug("测试刷新显示完成");
            }
        }
#endif
    }
}