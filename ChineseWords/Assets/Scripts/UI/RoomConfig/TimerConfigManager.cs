using UnityEngine;
using Core;

namespace Core
{
    /// <summary>
    /// Timer配置管理器 - 修复配置引用问题
    /// 确保UI修改能正确反映到配置中
    /// </summary>
    public static class TimerConfigManager
    {
        private static TimerConfig config;
        private static bool initialized = false;
        private static bool enableDebugLogs = true;

        /// <summary>
        /// 当前Timer配置
        /// </summary>
        public static TimerConfig Config
        {
            get
            {
                if (!initialized)
                {
                    Initialize();
                }
                return config;
            }
            private set
            {
                config = value;
                LogDebug($"Timer配置引用已更新: {config?.ConfigName ?? "null"} (InstanceID: {config?.GetInstanceID()})");
            }
        }

        /// <summary>
        /// 初始化Timer配置管理器
        /// </summary>
        public static void Initialize()
        {
            if (initialized)
            {
                LogDebug("TimerConfigManager 已经初始化");
                return;
            }

            LogDebug("初始化 TimerConfigManager");

            // 尝试加载默认配置
            LoadDefaultConfig();

            initialized = true;
            LogDebug($"TimerConfigManager 初始化完成，当前配置: {config?.ConfigName ?? "未设置"} (InstanceID: {config?.GetInstanceID()})");
        }

        /// <summary>
        /// 设置Timer配置 - 修复版本，确保引用正确传递
        /// </summary>
        /// <param name="newConfig">新的Timer配置</param>
        public static void SetConfig(TimerConfig newConfig)
        {
            LogDebug($"尝试设置Timer配置: {newConfig?.ConfigName ?? "null"} (InstanceID: {newConfig?.GetInstanceID()})");

            if (newConfig == null)
            {
                LogDebug("警告：尝试设置空的Timer配置，保持当前配置不变");
                return;
            }

            // 关键修复：确保初始化标记正确，避免后续被覆盖
            initialized = true;
            Config = newConfig;

            LogDebug($"Timer配置已成功设置: {newConfig.ConfigName} (InstanceID: {newConfig.GetInstanceID()})");

            // 验证设置是否成功
            var currentConfig = config;  // 直接访问私有字段，避免触发getter逻辑
            bool referenceMatch = (newConfig == currentConfig);
            LogDebug($"设置后引用验证: {(referenceMatch ? "✓ 成功" : "✗ 失败")}");

            if (!referenceMatch)
            {
                Debug.LogError($"[TimerConfigManager] 配置设置失败！预期ID: {newConfig.GetInstanceID()}, 实际ID: {currentConfig?.GetInstanceID()}");
            }

            // 可选：输出配置摘要用于调试
            if (enableDebugLogs)
            {
                LogDebug($"配置摘要: {newConfig.GetConfigSummary()}");
            }
        }

        /// <summary>
        /// 强制设置配置（跳过所有检查）
        /// </summary>
        /// <param name="newConfig">新的Timer配置</param>
        public static void ForceSetConfig(TimerConfig newConfig)
        {
            LogDebug($"强制设置Timer配置: {newConfig?.ConfigName ?? "null"} (ID: {newConfig?.GetInstanceID()})");

            // 关键修复：先标记为已初始化，避免后续Initialize覆盖
            initialized = true;
            config = newConfig;

            LogDebug($"强制设置完成，当前配置引用: {config?.GetInstanceID()}");
        }

        /// <summary>
        /// 验证当前配置的有效性
        /// </summary>
        /// <returns>配置是否有效</returns>
        public static bool ValidateCurrentConfig()
        {
            if (Config == null)
            {
                LogDebug("配置验证失败：配置为空");
                return false;
            }

            try
            {
                bool isValid = Config.ValidateConfig();
                LogDebug($"配置验证结果: {(isValid ? "有效" : "无效")}");
                return isValid;
            }
            catch (System.Exception e)
            {
                LogDebug($"配置验证异常: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取指定题型的答题时间限制
        /// </summary>
        /// <param name="questionType">题目类型</param>
        /// <returns>答题时间限制（秒）</returns>
        public static float GetTimeLimitForQuestionType(QuestionType questionType)
        {
            if (Config == null)
            {
                LogDebug($"Timer配置未设置，使用默认时间: {GetDefaultTimeLimit()}秒");
                return GetDefaultTimeLimit();
            }

            try
            {
                float timeLimit = Config.GetTimeLimitForQuestionType(questionType);
                LogDebug($"获取题型 {questionType} 的时间限制: {timeLimit}秒 (配置ID: {Config.GetInstanceID()})");
                return timeLimit;
            }
            catch (System.Exception e)
            {
                LogDebug($"获取题型时间失败: {e.Message}，使用默认时间");
                return GetDefaultTimeLimit();
            }
        }

        /// <summary>
        /// 获取指定题型的完整Timer设置
        /// </summary>
        /// <param name="questionType">题目类型</param>
        /// <returns>Timer设置</returns>
        public static TimerConfig.QuestionTypeTimer GetTimerSettingsForQuestionType(QuestionType questionType)
        {
            if (Config == null)
            {
                LogDebug("Timer配置未设置，创建默认设置");
                return CreateDefaultTimerSettings(questionType);
            }

            try
            {
                var settings = Config.GetTimerForQuestionType(questionType);
                LogDebug($"获取题型 {questionType} 的Timer设置: {settings.baseTimeLimit}秒 (配置ID: {Config.GetInstanceID()})");
                return settings;
            }
            catch (System.Exception e)
            {
                LogDebug($"获取题型Timer设置失败: {e.Message}，使用默认设置");
                return CreateDefaultTimerSettings(questionType);
            }
        }

        /// <summary>
        /// 获取全局Timer设置
        /// </summary>
        /// <returns>全局设置信息</returns>
        public static (float timeUpDelay, bool showTimeWarning, float warningThreshold, float criticalThreshold, Color normalColor, Color warningColor, Color criticalColor) GetGlobalSettings()
        {
            if (Config == null)
            {
                LogDebug("Timer配置未设置，使用默认全局设置");
                return (1f, true, 5f, 3f, Color.white, Color.yellow, Color.red);
            }

            var settings = (Config.timeUpDelay, Config.showTimeWarning, Config.warningThreshold, Config.criticalThreshold, Config.normalColor, Config.warningColor, Config.criticalColor);
            LogDebug($"获取全局设置: 警告阈值={settings.warningThreshold}秒, 危险阈值={settings.criticalThreshold}秒");
            return settings;
        }

        /// <summary>
        /// 检查Timer配置是否已设置
        /// </summary>
        /// <returns>是否已设置配置</returns>
        public static bool IsConfigured()
        {
            bool configured = initialized && Config != null;
            LogDebug($"配置状态检查: 已初始化={initialized}, 配置存在={Config != null}, 整体状态={configured}");
            return configured;
        }

        /// <summary>
        /// 获取配置摘要信息
        /// </summary>
        /// <returns>配置摘要字符串</returns>
        public static string GetConfigSummary()
        {
            if (Config == null)
            {
                return "Timer配置未设置";
            }

            try
            {
                string summary = Config.GetConfigSummary();
                LogDebug($"获取配置摘要: {summary} (配置ID: {Config.GetInstanceID()})");
                return summary;
            }
            catch (System.Exception e)
            {
                LogDebug($"获取配置摘要失败: {e.Message}");
                return "配置摘要获取失败";
            }
        }

        /// <summary>
        /// 重置为默认配置
        /// </summary>
        public static void ResetToDefault()
        {
            LogDebug("重置Timer配置为默认值");

            if (Config != null)
            {
                try
                {
                    // 优先重置当前配置，保持引用不变
                    Config.ResetToDefault();
                    LogDebug($"当前配置已重置 (配置ID: {Config.GetInstanceID()})");
                    return;
                }
                catch (System.Exception e)
                {
                    LogDebug($"重置当前配置失败: {e.Message}，将加载新的默认配置");
                }
            }

            // 如果重置失败或配置为空，加载新的默认配置
            LoadDefaultConfig();
        }

        /// <summary>
        /// 获取配置状态信息（调试用）
        /// </summary>
        /// <returns>详细的配置状态信息</returns>
        public static string GetConfigStatus()
        {
            var status = "=== TimerConfigManager状态 ===\n";
            status += $"已初始化: {initialized}\n";
            status += $"配置实例: {(Config != null ? "存在" : "不存在")}\n";

            if (Config != null)
            {
                status += $"配置名称: {Config.ConfigName}\n";
                status += $"配置ID: {Config.GetInstanceID()}\n";
                status += $"配置有效性: {ValidateCurrentConfig()}\n";

                try
                {
                    var timers = Config.GetAllTimers();
                    status += $"配置的题型数量: {timers.Length}\n";

                    if (timers.Length > 0)
                    {
                        status += "题型时间配置:\n";
                        foreach (var timer in timers)
                        {
                            status += $"  {timer.questionType}: {timer.baseTimeLimit}秒\n";
                        }
                    }
                }
                catch (System.Exception e)
                {
                    status += $"获取配置详情失败: {e.Message}\n";
                }
            }

            return status;
        }

        /// <summary>
        /// 加载默认配置
        /// </summary>
        private static void LoadDefaultConfig()
        {
            LogDebug("尝试加载默认Timer配置");

            // 方法1：从Resources加载
            var resourceConfig = Resources.Load<TimerConfig>("TimerConfig");
            if (resourceConfig != null)
            {
                Config = resourceConfig;
                LogDebug($"从Resources加载Timer配置: {resourceConfig.ConfigName} (ID: {resourceConfig.GetInstanceID()})");
                return;
            }

            // 方法2：尝试其他可能的路径
            string[] possiblePaths = {
                "Config/TimerConfig",
                "Configs/TimerConfig",
                "Game/TimerConfig"
            };

            foreach (string path in possiblePaths)
            {
                var config = Resources.Load<TimerConfig>(path);
                if (config != null)
                {
                    Config = config;
                    LogDebug($"从Resources路径 {path} 加载Timer配置: {config.ConfigName} (ID: {config.GetInstanceID()})");
                    return;
                }
            }

            // 方法3：创建运行时默认配置
            LogDebug("未找到Timer配置文件，创建运行时默认配置");
            CreateRuntimeDefaultConfig();
        }

        /// <summary>
        /// 创建运行时默认配置
        /// </summary>
        private static void CreateRuntimeDefaultConfig()
        {
            var defaultConfig = ScriptableObject.CreateInstance<TimerConfig>();
            defaultConfig.ResetToDefault();
            Config = defaultConfig;
            LogDebug($"运行时默认Timer配置创建完成 (ID: {defaultConfig.GetInstanceID()})");
        }

        /// <summary>
        /// 获取默认时间限制
        /// </summary>
        private static float GetDefaultTimeLimit()
        {
            return 30f; // 默认30秒
        }

        /// <summary>
        /// 创建默认的Timer设置
        /// </summary>
        private static TimerConfig.QuestionTypeTimer CreateDefaultTimerSettings(QuestionType questionType)
        {
            var settings = new TimerConfig.QuestionTypeTimer
            {
                questionType = questionType,
                baseTimeLimit = GetDefaultTimeLimit()
            };

            // 根据题型调整默认时间
            switch (questionType)
            {
                case QuestionType.ExplanationChoice:
                case QuestionType.SimularWordChoice:
                    settings.baseTimeLimit = 20f;
                    break;

                case QuestionType.SentimentTorF:
                case QuestionType.UsageTorF:
                    settings.baseTimeLimit = 15f;
                    break;

                case QuestionType.HandWriting:
                    settings.baseTimeLimit = 60f;
                    break;

                default:
                    // 填空题等使用默认30秒
                    break;
            }

            return settings;
        }

        /// <summary>
        /// 调试日志输出
        /// </summary>
        private static void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[TimerConfigManager] {message}");
#endif
            }
        }

        /// <summary>
        /// 设置调试日志开关
        /// </summary>
        public static void SetDebugLogs(bool enabled)
        {
            enableDebugLogs = enabled;
            LogDebug($"调试日志已{(enabled ? "启用" : "禁用")}");
        }

#if UNITY_EDITOR
        /// <summary>
        /// 编辑器专用：强制重新初始化
        /// </summary>
        [UnityEditor.MenuItem("Tools/Timer Config/Force Reinitialize")]
        public static void ForceReinitialize()
        {
            initialized = false;
            config = null;
            Initialize();
            Debug.Log("[TimerConfigManager] 强制重新初始化完成");
        }

        /// <summary>
        /// 编辑器专用：显示当前配置
        /// </summary>
        [UnityEditor.MenuItem("Tools/Timer Config/Show Current Config")]
        public static void ShowCurrentConfig()
        {
            Debug.Log($"[TimerConfigManager] 当前配置状态:\n{GetConfigStatus()}");
        }

        /// <summary>
        /// 编辑器专用：显示配置摘要
        /// </summary>
        [UnityEditor.MenuItem("Tools/Timer Config/Show Config Summary")]
        public static void ShowConfigSummary()
        {
            Debug.Log($"[TimerConfigManager] 当前配置摘要:\n{GetConfigSummary()}");
        }

        /// <summary>
        /// 编辑器专用：测试获取各题型时间
        /// </summary>
        [UnityEditor.MenuItem("Tools/Timer Config/Test All Question Types")]
        public static void TestAllQuestionTypes()
        {
            Debug.Log("[TimerConfigManager] 测试所有题型时间配置:");

            var questionTypes = System.Enum.GetValues(typeof(QuestionType));
            foreach (QuestionType questionType in questionTypes)
            {
                float timeLimit = GetTimeLimitForQuestionType(questionType);
                Debug.Log($"  {questionType}: {timeLimit}秒");
            }
        }

        /// <summary>
        /// 编辑器专用：验证配置引用一致性
        /// </summary>
        [UnityEditor.MenuItem("Tools/Timer Config/Validate Config References")]
        public static void ValidateConfigReferences()
        {
            Debug.Log("[TimerConfigManager] 验证配置引用一致性:");

            var config1 = Config;
            var config2 = Config;

            Debug.Log($"两次获取的配置引用是否相同: {config1 == config2}");
            Debug.Log($"配置1 ID: {config1?.GetInstanceID()}");
            Debug.Log($"配置2 ID: {config2?.GetInstanceID()}");

            if (config1 != null)
            {
                var timersBefore = config1.GetAllTimers().Length;
                Debug.Log($"修改前题型数量: {timersBefore}");

                // 模拟修改
                var testSettings = new TimerConfig.QuestionTypeTimer
                {
                    questionType = QuestionType.ExplanationChoice,
                    baseTimeLimit = 999f
                };
                config1.SetTimerForQuestionType(QuestionType.ExplanationChoice, testSettings);

                var timersAfter = Config.GetAllTimers().Length;
                var modifiedTime = Config.GetTimeLimitForQuestionType(QuestionType.ExplanationChoice);

                Debug.Log($"修改后题型数量: {timersAfter}");
                Debug.Log($"修改后ExplanationChoice时间: {modifiedTime}秒");
                Debug.Log($"修改是否生效: {modifiedTime == 999f}");
            }
        }
#endif
    }
}