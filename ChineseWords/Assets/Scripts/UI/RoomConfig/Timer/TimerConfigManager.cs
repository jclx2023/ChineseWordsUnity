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

            if (newConfig == null)
            {
                LogDebug("警告：尝试设置空的Timer配置，保持当前配置不变");
                return;
            }

            initialized = true;
            Config = newConfig;
        }

        /// <summary>
        /// 强制设置配置（跳过所有检查）
        /// </summary>
        /// <param name="newConfig">新的Timer配置</param>
        public static void ForceSetConfig(TimerConfig newConfig)
        {
            // 关键修复：先标记为已初始化，避免后续Initialize覆盖
            initialized = true;
            config = newConfig;

            LogDebug($"强制设置完成，当前配置引用: {config?.GetInstanceID()}");
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
        /// 加载默认配置
        /// </summary>
        private static void LoadDefaultConfig()
        {
            LogDebug("尝试加载默认Timer配置");

            // 方法1：从Resources加载
            var resourceConfig = Resources.Load<TimerConfig>("Questions/TimerConfig");
            if (resourceConfig != null)
            {
                Config = resourceConfig;
                LogDebug($"从Resources加载Timer配置: {resourceConfig.ConfigName} (ID: {resourceConfig.GetInstanceID()})");
                return;
            }

            // 方法2：创建运行时默认配置
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
    }
}