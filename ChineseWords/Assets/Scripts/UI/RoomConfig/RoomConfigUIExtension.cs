using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Core;
using Core.Network;
using TMPro;

namespace UI.RoomConfig
{
    /// <summary>
    /// 房间配置UI扩展组件 - 支持Timer和Weight两种配置的Footer按钮逻辑
    /// 修复：Apply按钮现在对Timer配置也有作用
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
        private QuestionWeightConfig originalWeightConfig;
        private TimerConfig originalTimerConfig;

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
        /// 设置默认配置
        /// </summary>
        private void SetupDefaultConfigs()
        {
            // 设置权重配置
            if (defaultWeightConfig != null)
            {
                originalWeightConfig = ScriptableObject.CreateInstance<QuestionWeightConfig>();
                CopyWeightConfig(defaultWeightConfig, originalWeightConfig);
                QuestionWeightManager.SetConfig(defaultWeightConfig);
                LogDebug("已设置默认权重配置");
            }

            // 设置Timer配置
            if (defaultTimerConfig != null)
            {
                originalTimerConfig = ScriptableObject.CreateInstance<TimerConfig>();
                CopyTimerConfig(defaultTimerConfig, originalTimerConfig);
                TimerConfigManager.SetConfig(defaultTimerConfig);
                LogDebug("已设置默认Timer配置");
            }
            else
            {
                LogDebug("警告：未设置默认Timer配置，将创建默认配置");
                var defaultConfig = ScriptableObject.CreateInstance<TimerConfig>();
                defaultConfig.ResetToDefault();
                originalTimerConfig = ScriptableObject.CreateInstance<TimerConfig>();
                CopyTimerConfig(defaultConfig, originalTimerConfig);
                TimerConfigManager.SetConfig(defaultConfig);
            }
        }

        /// <summary>
        /// 复制权重配置
        /// </summary>
        private void CopyWeightConfig(QuestionWeightConfig source, QuestionWeightConfig target)
        {
            if (source == null || target == null) return;

            try
            {
                target.CopyFrom(source);
                LogDebug("权重配置复制完成");
            }
            catch (System.Exception e)
            {
                LogDebug($"权重配置复制失败: {e.Message}");
            }
        }

        /// <summary>
        /// 复制Timer配置
        /// </summary>
        private void CopyTimerConfig(TimerConfig source, TimerConfig target)
        {
            if (source == null || target == null) return;

            try
            {
                // 假设TimerConfig也有CopyFrom方法，或者手动复制
                if (target.GetType().GetMethod("CopyFrom") != null)
                {
                    target.GetType().GetMethod("CopyFrom").Invoke(target, new object[] { source });
                }
                else
                {
                    // 手动复制Timer配置
                    var sourceTimers = source.GetAllTimers();
                    foreach (var timer in sourceTimers)
                    {
                        target.SetTimerForQuestionType(timer.questionType, timer);
                    }
                }
                LogDebug("Timer配置复制完成");
            }
            catch (System.Exception e)
            {
                LogDebug($"Timer配置复制失败: {e.Message}");
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
                        // 更新原始权重配置
                        if (originalWeightConfig != null && QuestionWeightManager.Config != null)
                        {
                            CopyWeightConfig(QuestionWeightManager.Config, originalWeightConfig);
                        }
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
                        // 更新原始Timer配置
                        if (originalTimerConfig != null && TimerConfigManager.Config != null)
                        {
                            CopyTimerConfig(TimerConfigManager.Config, originalTimerConfig);
                        }
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
        /// 应用权重配置变更到游戏系统
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
                // 更新NQMC的权重配置
                UpdateNQMCWeights(config);

                // 通知RoomConfigManager
                if (RoomConfigManager.Instance != null)
                {
                    RoomConfigManager.Instance.ApplyConfigChanges();
                    LogDebug("已通知RoomConfigManager应用权重配置");
                }

                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"应用权重配置失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 应用Timer配置变更到游戏系统
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
                // 更新NQMC的Timer配置
                UpdateNQMCTimers(config);

                // 通知RoomConfigManager
                if (RoomConfigManager.Instance != null)
                {
                    RoomConfigManager.Instance.ApplyConfigChanges();
                    LogDebug("已通知RoomConfigManager应用Timer配置");
                }

                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"应用Timer配置失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 更新NQMC的权重配置
        /// </summary>
        private void UpdateNQMCWeights(QuestionWeightConfig config)
        {
            var nqmc = NetworkQuestionManagerController.Instance;
            if (nqmc == null)
            {
                LogDebug("NQMC实例不存在，跳过权重更新");
                return;
            }

            try
            {
                // 使用反射更新NQMC的TypeWeights字段
                var typeWeightsField = typeof(NetworkQuestionManagerController).GetField("TypeWeights",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (typeWeightsField != null)
                {
                    var typeWeights = typeWeightsField.GetValue(nqmc) as System.Collections.Generic.Dictionary<QuestionType, float>;
                    if (typeWeights != null)
                    {
                        var currentWeights = config.GetWeights();
                        typeWeights.Clear();

                        foreach (var weight in currentWeights)
                        {
                            typeWeights[weight.Key] = weight.Value;
                        }

                        LogDebug($"NQMC权重配置已更新，包含 {typeWeights.Count} 种题型");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"更新NQMC权重配置失败: {e.Message}");
            }
        }

        /// <summary>
        /// 更新NQMC的Timer配置
        /// </summary>
        private void UpdateNQMCTimers(TimerConfig config)
        {
            var nqmc = NetworkQuestionManagerController.Instance;
            if (nqmc == null)
            {
                LogDebug("NQMC实例不存在，跳过Timer更新");
                return;
            }

            try
            {
                // 假设NQMC有Timer相关字段，使用反射更新
                // 如果NQMC没有Timer字段，可能需要通过其他方式应用Timer配置
                var questionTimeLimitField = typeof(NetworkQuestionManagerController).GetField("questionTimeLimit",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (questionTimeLimitField != null)
                {
                    // 获取所有Timer的平均时间作为全局时间限制
                    var allTimers = config.GetAllTimers();
                    if (allTimers.Length > 0)
                    {
                        float avgTimeLimit = 0f;
                        foreach (var timer in allTimers)
                        {
                            avgTimeLimit += timer.baseTimeLimit;
                        }
                        avgTimeLimit /= allTimers.Length;

                        questionTimeLimitField.SetValue(nqmc, avgTimeLimit);
                        LogDebug($"NQMC时间限制已更新为平均值: {avgTimeLimit}秒");
                    }
                }
                else
                {
                    LogDebug("NQMC没有questionTimeLimit字段，Timer配置可能需要其他方式应用");
                }

                LogDebug("NQMC Timer配置更新完成");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"更新NQMC Timer配置失败: {e.Message}");
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

            LogDebug($"按钮状态已更新 - Apply: {(hasChanges ? "可用(绿)" : "禁用(灰)")}, 权重变更: {hasUnsavedWeightChanges}, Timer变更: {hasUnsavedTimerChanges}");
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
            string weightSummary = "未配置";
            string timerSummary = "未配置";

            var weightConfig = QuestionWeightManager.Config;
            if (weightConfig != null)
            {
                try
                {
                    weightSummary = weightConfig.GetConfigSummary();
                }
                catch
                {
                    weightSummary = "配置摘要获取失败";
                }
            }

            var timerConfig = TimerConfigManager.Config;
            if (timerConfig != null)
            {
                try
                {
                    timerSummary = timerConfig.GetConfigSummary();
                }
                catch
                {
                    timerSummary = "配置摘要获取失败";
                }
            }

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

            // 清理临时创建的配置对象
            if (originalWeightConfig != null)
            {
                if (Application.isPlaying)
                    Destroy(originalWeightConfig);
                else
                    DestroyImmediate(originalWeightConfig);
            }

            if (originalTimerConfig != null)
            {
                if (Application.isPlaying)
                    Destroy(originalTimerConfig);
                else
                    DestroyImmediate(originalTimerConfig);
            }

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

        [ContextMenu("测试Apply按钮")]
        private void EditorTestApplyButton()
        {
            if (Application.isPlaying)
            {
                OnApplyClicked();
            }
        }

        [ContextMenu("测试Reset按钮")]
        private void EditorTestResetButton()
        {
            if (Application.isPlaying)
            {
                OnResetClicked();
            }
        }

        [ContextMenu("测试Close按钮")]
        private void EditorTestCloseButton()
        {
            if (Application.isPlaying)
            {
                OnCloseClicked();
            }
        }

        [ContextMenu("触发Timer配置变更")]
        private void EditorTriggerTimerConfigChanged()
        {
            if (Application.isPlaying)
            {
                OnTimerConfigChanged();
            }
        }
#endif
    }
}