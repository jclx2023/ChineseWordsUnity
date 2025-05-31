using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Core;
using Core.Network;
using TMPro;

namespace UI.RoomConfig
{
    /// <summary>
    /// 房间配置UI扩展组件 - 负责Footer按钮逻辑
    /// 修复按钮功能：Cancel取消权重改动，Close关闭整个UI
    /// </summary>
    public class RoomConfigUIExtension : MonoBehaviour
    {
        [Header("默认配置")]
        [SerializeField] private QuestionWeightConfig defaultWeightConfig;

        [Header("Footer按钮")]
        [SerializeField] private Button applyButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button closeButton;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 权重配置面板引用
        private QuestionWeightConfigPanel questionWeightPanel;
        private RoomConfigUI mainConfigUI;

        // 配置状态
        private bool hasUnsavedChanges = false;
        private QuestionWeightConfig originalConfig; // 保存原始配置用于取消操作

        private void Awake()
        {
            mainConfigUI = GetComponent<RoomConfigUI>();
            LogDebug("RoomConfigUIExtension 初始化");
        }

        private void Start()
        {
            SetupDefaultConfig();
            SetupButtons();
            StartCoroutine(DelayedFindWeightPanel());
        }

        /// <summary>
        /// 设置默认配置
        /// </summary>
        private void SetupDefaultConfig()
        {
            if (defaultWeightConfig != null)
            {
                // 保存原始配置的副本
                originalConfig = ScriptableObject.CreateInstance<QuestionWeightConfig>();
                CopyWeightConfig(defaultWeightConfig, originalConfig);

                QuestionWeightManager.SetConfig(defaultWeightConfig);
                LogDebug("已设置默认权重配置");
            }
            else
            {
                LogDebug("警告：未设置默认权重配置");
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
            }
            catch (System.Exception e)
            {
                LogDebug($"权重配置复制失败: {e.Message}");
            }
        }

        /// <summary>
        /// 延迟查找权重配置面板
        /// </summary>
        private IEnumerator DelayedFindWeightPanel()
        {
            yield return null;

            float timeout = 5f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                questionWeightPanel = FindObjectOfType<QuestionWeightConfigPanel>();

                if (questionWeightPanel != null)
                {
                    LogDebug($"找到权重配置面板: {questionWeightPanel.name}");
                    SetupWeightPanelEvents();
                    break;
                }

                elapsed += 0.1f;
                yield return new WaitForSeconds(0.1f);
            }

            if (questionWeightPanel == null)
            {
                LogDebug("警告：未找到权重配置面板");
            }
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
            hasUnsavedChanges = true;
            UpdateButtonStates();
        }

        /// <summary>
        /// 应用按钮点击 - 应用权重配置变更
        /// </summary>
        private void OnApplyClicked()
        {
            LogDebug("应用配置变更");

            try
            {
                ApplyWeightConfigChanges();

                // 更新原始配置
                if (originalConfig != null && QuestionWeightManager.Config != null)
                {
                    CopyWeightConfig(QuestionWeightManager.Config, originalConfig);
                }

                hasUnsavedChanges = false;
                UpdateButtonStates();

                LogDebug("配置应用成功");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RoomConfigUIExtension] 应用配置失败: {e.Message}");
            }
        }

        /// <summary>
        /// 重置按钮点击 - 重置到默认配置
        /// </summary>
        private void OnResetClicked()
        {
            LogDebug("重置配置到默认值");

            try
            {
                var config = QuestionWeightManager.Config;
                if (config != null)
                {
                    config.ResetToDefault();

                    if (questionWeightPanel != null)
                    {
                        questionWeightPanel.RefreshDisplay();
                    }
                }

                hasUnsavedChanges = false;
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
                if (hasUnsavedChanges)
                {
                    LogDebug("有未保存的变更，关闭UI将丢弃变更");
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
                hasUnsavedChanges = false;
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
        private void ApplyWeightConfigChanges()
        {
            var config = QuestionWeightManager.Config;
            if (config == null)
            {
                LogDebug("警告：无法获取当前权重配置");
                return;
            }

            // 更新NQMC的权重配置
            UpdateNQMCWeights(config);

            // 通知RoomConfigManager
            if (RoomConfigManager.Instance != null)
            {
                try
                {
                    RoomConfigManager.Instance.ApplyConfigChanges();
                    LogDebug("已通知RoomConfigManager应用配置");
                }
                catch (System.Exception e)
                {
                    LogDebug($"通知RoomConfigManager失败: {e.Message}");
                }
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
        /// 更新按钮状态
        /// </summary>
        private void UpdateButtonStates()
        {
            // Apply按钮：有变更时可用且变绿
            if (applyButton != null)
            {
                applyButton.interactable = hasUnsavedChanges;

                var buttonImage = applyButton.GetComponent<Image>();
                if (buttonImage != null)
                {
                    buttonImage.color = hasUnsavedChanges ?
                        new Color(0.2f, 0.8f, 0.2f, 1f) : // 有变更：绿色
                        new Color(0.5f, 0.5f, 0.5f, 1f);  // 无变更：灰色
                }

                var tmpText = applyButton.GetComponentInChildren<TMP_Text>();
                var legacyText = applyButton.GetComponentInChildren<Text>();

                if (tmpText != null)
                {
                    tmpText.color = hasUnsavedChanges ? Color.white : Color.gray;
                }
                else if (legacyText != null)
                {
                    legacyText.color = hasUnsavedChanges ? Color.white : Color.gray;
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
        /// 检查是否有未保存的变更
        /// </summary>
        public bool HasUnsavedChanges()
        {
            return hasUnsavedChanges;
        }

        /// <summary>
        /// 强制更新按钮状态
        /// </summary>
        public void ForceUpdateButtonStates()
        {
            UpdateButtonStates();
        }

        /// <summary>
        /// 获取权重配置摘要
        /// </summary>
        public string GetWeightConfigSummary()
        {
            var config = QuestionWeightManager.Config;
            if (config != null)
            {
                try
                {
                    return config.GetConfigSummary();
                }
                catch
                {
                    return "配置摘要获取失败";
                }
            }
            return "未配置";
        }

        /// <summary>
        /// 手动触发配置变更事件
        /// </summary>
        public void TriggerConfigChanged()
        {
            OnWeightConfigChanged();
        }

        #endregion

        #region 生命周期管理

        private void OnDestroy()
        {
            // 清理权重配置面板事件
            if (questionWeightPanel != null)
            {
                questionWeightPanel.OnConfigChanged -= OnWeightConfigChanged;
            }

            // 清理按钮事件
            if (applyButton != null)
                applyButton.onClick.RemoveAllListeners();
            if (resetButton != null)
                resetButton.onClick.RemoveAllListeners();
            if (closeButton != null)
                closeButton.onClick.RemoveAllListeners();

            // 清理临时创建的配置对象
            if (originalConfig != null)
            {
                if (Application.isPlaying)
                    Destroy(originalConfig);
                else
                    DestroyImmediate(originalConfig);
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
            status += $"Apply按钮: {(applyButton != null ? "✓" : "✗")}\n";
            status += $"Reset按钮: {(resetButton != null ? "✓" : "✗")}\n";
            status += $"Close按钮: {(closeButton != null ? "✓" : "✗")}\n";
            status += $"有未保存变更: {(hasUnsavedChanges ? "是" : "否")}\n";
            status += $"默认配置: {(defaultWeightConfig != null ? "✓" : "✗")}\n";
            status += $"权重配置摘要: {GetWeightConfigSummary()}\n";

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
#endif
    }
}