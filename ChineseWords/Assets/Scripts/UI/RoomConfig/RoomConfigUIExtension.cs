using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Core;
using Core.Network;
using TMPro;

namespace UI.RoomConfig
{
    /// <summary>
    /// 房间配置UI扩展组件 - 简化版
    /// 专注于配置管理，不涉及NQMC更新
    /// </summary>
    public class RoomConfigUIExtension : MonoBehaviour
    {
        [Header("默认配置")]
        [SerializeField] private QuestionWeightConfig defaultWeightConfig;
        [SerializeField] private TimerConfig defaultTimerConfig;

        [Header("Footer按钮")]
        [SerializeField] private Button applyButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button closeButton;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 配置面板引用
        private QuestionWeightConfigPanel questionWeightPanel;
        private TimerConfigPanel timerConfigPanel;
        private RoomConfigUI mainConfigUI;

        // 配置状态
        private bool hasUnsavedWeightChanges = false;
        private bool hasUnsavedTimerChanges = false;

        // 计算属性
        private bool HasUnsavedChanges => hasUnsavedWeightChanges || hasUnsavedTimerChanges;

        private void Awake()
        {
            mainConfigUI = GetComponent<RoomConfigUI>();
            LogDebug("RoomConfigUIExtension 初始化");
        }

        private void Start()
        {
            SetupDefaultConfigs();
            SetupButtons();
            StartCoroutine(DelayedFindConfigPanels());
        }

        /// <summary>
        /// 设置默认配置 - 确保引用一致性
        /// </summary>
        private void SetupDefaultConfigs()
        {
            LogDebug("设置默认配置");

            // 设置权重配置
            if (defaultWeightConfig != null)
            {
                QuestionWeightManager.SetConfig(defaultWeightConfig);
                LogDebug($"权重配置已设置");
            }

            // 设置Timer配置 - 关键修复：多步骤验证
            if (defaultTimerConfig != null)
            {
                LogDebug($"开始设置Timer配置: {defaultTimerConfig.ConfigName} (ID: {defaultTimerConfig.GetInstanceID()})");

                // 步骤1：检查TimerConfigManager初始化状态
                bool wasInitialized = TimerConfigManager.IsConfigured();
                LogDebug($"设置前TimerConfigManager初始化状态: {wasInitialized}");

                // 步骤2：使用ForceSetConfig
                TimerConfigManager.ForceSetConfig(defaultTimerConfig);
                LogDebug("ForceSetConfig 调用完成");

                // 步骤3：立即验证
                var managerConfig = TimerConfigManager.Config;
                LogDebug($"设置后管理器配置ID: {managerConfig?.GetInstanceID()}");
                LogDebug($"设置后管理器配置名称: {managerConfig?.ConfigName}");

                bool referenceMatch = (defaultTimerConfig == managerConfig);
                LogDebug($"Timer配置引用验证: {(referenceMatch ? "✓ 一致" : "✗ 不一致")}");

                if (!referenceMatch)
                {
                    Debug.LogError("[RoomConfigUIExtension] Timer配置引用不一致，这会导致实时更新失败！");

                    // 调试信息：显示详细状态
                    LogDebug($"详细调试信息:");
                    LogDebug($"  defaultTimerConfig: {defaultTimerConfig} (ID: {defaultTimerConfig.GetInstanceID()})");
                    LogDebug($"  managerConfig: {managerConfig} (ID: {managerConfig?.GetInstanceID()})");
                    LogDebug($"  TimerConfigManager.IsConfigured(): {TimerConfigManager.IsConfigured()}");

                    // 尝试再次强制设置
                    LogDebug("尝试第二次强制设置...");
                    TimerConfigManager.ForceSetConfig(defaultTimerConfig);

                    var secondCheck = TimerConfigManager.Config;
                    bool secondMatch = (defaultTimerConfig == secondCheck);
                    LogDebug($"第二次设置结果: {(secondMatch ? "✓ 成功" : "✗ 仍然失败")}");

                    if (!secondMatch)
                    {
                        Debug.LogError("Timer配置引用问题无法解决，请检查TimerConfigManager.Initialize()逻辑");
                    }
                }
                else
                {
                    LogDebug("Timer配置设置成功");
                }
            }
            else
            {
                LogDebug("未设置默认Timer配置，使用系统默认");
                TimerConfigManager.Initialize();
            }
        }

        /// <summary>
        /// 延迟查找配置面板
        /// </summary>
        private IEnumerator DelayedFindConfigPanels()
        {
            yield return null;

            float timeout = 5f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                // 查找权重配置面板
                if (questionWeightPanel == null)
                {
                    questionWeightPanel = FindObjectOfType<QuestionWeightConfigPanel>();
                    if (questionWeightPanel != null)
                    {
                        LogDebug($"找到权重配置面板: {questionWeightPanel.name}");
                        SetupWeightPanelEvents();
                    }
                }

                // 查找Timer配置面板
                if (timerConfigPanel == null)
                {
                    timerConfigPanel = FindObjectOfType<TimerConfigPanel>();
                    if (timerConfigPanel != null)
                    {
                        LogDebug($"找到Timer配置面板: {timerConfigPanel.name}");
                        SetupTimerPanelEvents();
                    }
                }

                // 如果都找到了，退出循环
                if (questionWeightPanel != null && timerConfigPanel != null)
                {
                    break;
                }

                elapsed += 0.1f;
                yield return new WaitForSeconds(0.1f);
            }

            if (questionWeightPanel == null)
                LogDebug("警告：未找到权重配置面板");
            if (timerConfigPanel == null)
                LogDebug("警告：未找到Timer配置面板");
        }

        /// <summary>
        /// 设置权重面板事件
        /// </summary>
        private void SetupWeightPanelEvents()
        {
            if (questionWeightPanel != null)
            {
                questionWeightPanel.OnConfigChanged -= OnWeightConfigChanged;
                questionWeightPanel.OnConfigChanged += OnWeightConfigChanged;
                LogDebug("权重配置面板事件已绑定");
            }
        }

        /// <summary>
        /// 设置Timer面板事件
        /// </summary>
        private void SetupTimerPanelEvents()
        {
            if (timerConfigPanel != null)
            {
                timerConfigPanel.OnConfigChanged -= OnTimerConfigChanged;
                timerConfigPanel.OnConfigChanged += OnTimerConfigChanged;
                LogDebug("Timer配置面板事件已绑定");
            }
        }

        /// <summary>
        /// 设置Footer按钮
        /// </summary>
        private void SetupButtons()
        {
            // 自动查找按钮
            if (applyButton == null)
                applyButton = transform.Find("Footer/ApplyButton")?.GetComponent<Button>();
            if (resetButton == null)
                resetButton = transform.Find("Footer/ResetButton")?.GetComponent<Button>();
            if (closeButton == null)
                closeButton = transform.Find("Footer/CloseButton")?.GetComponent<Button>();

            LogDebug($"按钮查找结果 - Apply: {applyButton != null}, Reset: {resetButton != null}, Close: {closeButton != null}");

            // 绑定按钮事件
            if (applyButton != null)
            {
                applyButton.onClick.RemoveAllListeners();
                applyButton.onClick.AddListener(OnApplyClicked);
            }

            if (resetButton != null)
            {
                resetButton.onClick.RemoveAllListeners();
                resetButton.onClick.AddListener(OnResetClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(OnCloseClicked);
            }

            UpdateButtonStates();
        }

        /// <summary>
        /// 权重配置变更处理
        /// </summary>
        private void OnWeightConfigChanged()
        {
            hasUnsavedWeightChanges = true;
            UpdateButtonStates();
            LogDebug("检测到权重配置变更");
        }

        /// <summary>
        /// Timer配置变更处理
        /// </summary>
        private void OnTimerConfigChanged()
        {
            hasUnsavedTimerChanges = true;
            UpdateButtonStates();
            LogDebug("检测到Timer配置变更");
        }

        /// <summary>
        /// 应用按钮点击 - 应用所有配置变更
        /// </summary>
        private void OnApplyClicked()
        {
            LogDebug($"应用配置变更 - 权重变更: {hasUnsavedWeightChanges}, Timer变更: {hasUnsavedTimerChanges}");

            try
            {
                bool success = true;

                // 应用权重配置变更
                if (hasUnsavedWeightChanges)
                {
                    success &= ApplyWeightConfigChanges();
                    if (success)
                    {
                        hasUnsavedWeightChanges = false;
                        LogDebug("权重配置应用成功");
                    }
                }

                // 应用Timer配置变更
                if (hasUnsavedTimerChanges)
                {
                    success &= ApplyTimerConfigChanges();
                    if (success)
                    {
                        hasUnsavedTimerChanges = false;
                        LogDebug("Timer配置应用成功");
                    }
                }

                UpdateButtonStates();

                if (success)
                {
                    LogDebug("所有配置应用成功");
                }
                else
                {
                    LogDebug("部分配置应用失败");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RoomConfigUIExtension] 应用配置失败: {e.Message}");
            }
        }

        /// <summary>
        /// 重置按钮点击 - 重置所有配置到默认值
        /// </summary>
        private void OnResetClicked()
        {
            LogDebug("重置所有配置到默认值");

            try
            {
                // 重置权重配置
                var weightConfig = QuestionWeightManager.Config;
                if (weightConfig != null)
                {
                    weightConfig.ResetToDefault();
                    if (questionWeightPanel != null)
                    {
                        questionWeightPanel.RefreshDisplay();
                    }
                    LogDebug("权重配置已重置");
                }

                // 重置Timer配置
                var timerConfig = TimerConfigManager.Config;
                if (timerConfig != null)
                {
                    timerConfig.ResetToDefault();
                    if (timerConfigPanel != null)
                    {
                        timerConfigPanel.RefreshDisplay();
                    }
                    LogDebug("Timer配置已重置");
                }

                hasUnsavedWeightChanges = false;
                hasUnsavedTimerChanges = false;
                UpdateButtonStates();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RoomConfigUIExtension] 重置配置失败: {e.Message}");
            }
        }

        /// <summary>
        /// 关闭按钮点击 - 关闭整个配置UI
        /// </summary>
        private void OnCloseClicked()
        {
            LogDebug("关闭配置UI");

            try
            {
                // 如果有未保存的变更，提醒用户
                if (HasUnsavedChanges)
                {
                    LogDebug($"有未保存的变更，关闭UI将丢弃变更 - 权重: {hasUnsavedWeightChanges}, Timer: {hasUnsavedTimerChanges}");
                }

                // 关闭整个配置UI
                if (mainConfigUI != null)
                {
                    mainConfigUI.Hide();
                }

                // 也可以通过RoomConfigManager关闭
                if (RoomConfigManager.Instance != null)
                {
                    RoomConfigManager.Instance.CloseConfigUI();
                }

                // 重置变更状态
                hasUnsavedWeightChanges = false;
                hasUnsavedTimerChanges = false;
                UpdateButtonStates();

                LogDebug("配置UI已关闭");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RoomConfigUIExtension] 关闭UI失败: {e.Message}");
            }
        }

        /// <summary>
        /// 应用权重配置变更到管理器
        /// </summary>
        private bool ApplyWeightConfigChanges()
        {
            var config = QuestionWeightManager.Config;
            if (config == null)
            {
                LogDebug("警告：无法获取当前权重配置");
                return false;
            }

            try
            {
                // 通知RoomConfigManager配置已更新
                if (RoomConfigManager.Instance != null)
                {
                    RoomConfigManager.Instance.ApplyConfigChanges();
                    LogDebug("已通知RoomConfigManager权重配置变更");
                }

                LogDebug($"权重配置已确认应用: {config.GetConfigSummary()}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"应用权重配置失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 应用Timer配置变更到管理器
        /// </summary>
        private bool ApplyTimerConfigChanges()
        {
            var config = TimerConfigManager.Config;
            if (config == null)
            {
                LogDebug("警告：无法获取当前Timer配置");
                return false;
            }

            try
            {
                // 通知RoomConfigManager配置已更新
                if (RoomConfigManager.Instance != null)
                {
                    RoomConfigManager.Instance.ApplyConfigChanges();
                    LogDebug("已通知RoomConfigManager Timer配置变更");
                }

                LogDebug($"Timer配置已确认应用: {config.GetConfigSummary()}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"应用Timer配置失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 更新按钮状态
        /// </summary>
        private void UpdateButtonStates()
        {
            bool hasChanges = HasUnsavedChanges;

            // Apply按钮：有任何变更时可用且变绿
            if (applyButton != null)
            {
                applyButton.interactable = hasChanges;

                var buttonImage = applyButton.GetComponent<Image>();
                if (buttonImage != null)
                {
                    buttonImage.color = hasChanges ?
                        new Color(0.2f, 0.8f, 0.2f, 1f) : // 有变更：绿色
                        new Color(0.5f, 0.5f, 0.5f, 1f);  // 无变更：灰色
                }

                var tmpText = applyButton.GetComponentInChildren<TMP_Text>();
                var legacyText = applyButton.GetComponentInChildren<Text>();

                if (tmpText != null)
                {
                    tmpText.color = hasChanges ? Color.white : Color.gray;
                }
                else if (legacyText != null)
                {
                    legacyText.color = hasChanges ? Color.white : Color.gray;
                }
            }

            // Reset按钮：始终可用
            if (resetButton != null)
            {
                resetButton.interactable = true;
            }

            // Close按钮：始终可用
            if (closeButton != null)
            {
                closeButton.interactable = true;
            }
        }

        #region 公共接口

        /// <summary>
        /// 手动设置权重配置面板引用
        /// </summary>
        public void SetWeightConfigPanel(QuestionWeightConfigPanel panel)
        {
            if (questionWeightPanel != null)
            {
                questionWeightPanel.OnConfigChanged -= OnWeightConfigChanged;
            }

            questionWeightPanel = panel;

            if (questionWeightPanel != null)
            {
                SetupWeightPanelEvents();
                LogDebug("手动设置权重配置面板成功");
            }
        }

        /// <summary>
        /// 手动设置Timer配置面板引用
        /// </summary>
        public void SetTimerConfigPanel(TimerConfigPanel panel)
        {
            if (timerConfigPanel != null)
            {
                timerConfigPanel.OnConfigChanged -= OnTimerConfigChanged;
            }

            timerConfigPanel = panel;

            if (timerConfigPanel != null)
            {
                SetupTimerPanelEvents();
                LogDebug("手动设置Timer配置面板成功");
            }
        }

        /// <summary>
        /// 强制更新按钮状态
        /// </summary>
        public void ForceUpdateButtonStates()
        {
            UpdateButtonStates();
        }

        /// <summary>
        /// 获取配置摘要
        /// </summary>
        public string GetConfigSummary()
        {
            string weightSummary = QuestionWeightManager.Config?.GetConfigSummary() ?? "未配置";
            string timerSummary = TimerConfigManager.Config?.GetConfigSummary() ?? "未配置";
            return $"权重: {weightSummary}, Timer: {timerSummary}";
        }

        /// <summary>
        /// 手动触发配置变更事件
        /// </summary>
        public void TriggerConfigChanged(bool isWeight = true, bool isTimer = true)
        {
            if (isWeight)
                OnWeightConfigChanged();
            if (isTimer)
                OnTimerConfigChanged();
        }

        #endregion

        #region 生命周期管理

        private void OnDestroy()
        {
            // 清理配置面板事件
            if (questionWeightPanel != null)
            {
                questionWeightPanel.OnConfigChanged -= OnWeightConfigChanged;
            }

            if (timerConfigPanel != null)
            {
                timerConfigPanel.OnConfigChanged -= OnTimerConfigChanged;
            }

            // 清理按钮事件
            if (applyButton != null)
                applyButton.onClick.RemoveAllListeners();
            if (resetButton != null)
                resetButton.onClick.RemoveAllListeners();
            if (closeButton != null)
                closeButton.onClick.RemoveAllListeners();

            LogDebug("RoomConfigUIExtension资源已清理");
        }

        #endregion

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[RoomConfigUIExtension] {message}");
            }
        }

#if UNITY_EDITOR
        [ContextMenu("显示Extension状态")]
        private void EditorShowExtensionStatus()
        {
            string status = "=== RoomConfigUIExtension状态 ===\n";
            status += $"权重配置面板: {(questionWeightPanel != null ? "✓" : "✗")}\n";
            status += $"Timer配置面板: {(timerConfigPanel != null ? "✓" : "✗")}\n";
            status += $"Apply按钮: {(applyButton != null ? "✓" : "✗")}\n";
            status += $"Reset按钮: {(resetButton != null ? "✓" : "✗")}\n";
            status += $"Close按钮: {(closeButton != null ? "✓" : "✗")}\n";
            status += $"权重未保存变更: {(hasUnsavedWeightChanges ? "是" : "否")}\n";
            status += $"Timer未保存变更: {(hasUnsavedTimerChanges ? "是" : "否")}\n";
            status += $"总体有变更: {(HasUnsavedChanges ? "是" : "否")}\n";
            status += $"配置摘要: {GetConfigSummary()}\n";

            LogDebug(status);
        }

        [ContextMenu("验证Timer配置引用")]
        private void EditorValidateTimerConfigReference()
        {
            if (Application.isPlaying)
            {
                var defaultConfig = defaultTimerConfig;
                var managerConfig = TimerConfigManager.Config;

                Debug.Log($"默认配置ID: {defaultConfig?.GetInstanceID()}");
                Debug.Log($"管理器配置ID: {managerConfig?.GetInstanceID()}");
                Debug.Log($"引用一致性: {(defaultConfig == managerConfig ? "✓ 一致" : "✗ 不一致")}");

                if (timerConfigPanel != null)
                {
                    // 通过反射获取面板配置
                    var currentConfigField = typeof(TimerConfigPanel).GetField("currentConfig",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (currentConfigField != null)
                    {
                        var panelConfig = currentConfigField.GetValue(timerConfigPanel) as TimerConfig;
                        Debug.Log($"面板配置ID: {panelConfig?.GetInstanceID()}");
                        Debug.Log($"面板与管理器一致: {(panelConfig == managerConfig ? "✓ 一致" : "✗ 不一致")}");
                    }
                }
            }
        }

        [ContextMenu("测试Apply按钮")]
        private void EditorTestApplyButton()
        {
            if (Application.isPlaying)
            {
                OnApplyClicked();
            }
        }
#endif
    }
}