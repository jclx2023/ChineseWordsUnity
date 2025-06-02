using UnityEngine;
using Core;

namespace Core
{
    /// <summary>
    /// Timer配置管理器
    /// 提供全局的Timer配置访问接口
    /// </summary>
    public static class TimerConfigManager
    {
        private static TimerConfig config;
        private static bool initialized = false;

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
                LogDebug($"Timer配置已更新: {config?.ConfigName ?? "null"}");
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
            LogDebug($"TimerConfigManager 初始化完成，当前配置: {config?.ConfigName ?? "未设置"}");
        }

        /// <summary>
        /// 设置Timer配置
        /// </summary>
        /// <param name="newConfig">新的Timer配置</param>
        public static void SetConfig(TimerConfig newConfig)
        {
            if (newConfig == null)
            {
                LogDebug("警告：尝试设置空的Timer配置");
                return;
            }

            if (!newConfig.ValidateConfig())
            {
                LogDebug("警告：Timer配置验证失败，使用默认配置");
                LoadDefaultConfig();
                return;
            }

            Config = newConfig;
            LogDebug($"Timer配置已设置: {newConfig.ConfigName}");
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
                LogDebug($"获取题型 {questionType} 的时间限制: {timeLimit}秒");
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
                return Config.GetTimerForQuestionType(questionType);
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

            return (Config.timeUpDelay, Config.showTimeWarning, Config.warningThreshold, Config.criticalThreshold, Config.normalColor, Config.warningColor, Config.criticalColor);
        }

        /// <summary>
        /// 检查Timer配置是否已设置
        /// </summary>
        /// <returns>是否已设置配置</returns>
        public static bool IsConfigured()
        {
            return initialized && Config != null;
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

            return Config.GetConfigSummary();
        }

        /// <summary>
        /// 重置为默认配置
        /// </summary>
        public static void ResetToDefault()
        {
            LogDebug("重置Timer配置为默认值");
            LoadDefaultConfig();
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
                LogDebug($"从Resources加载Timer配置: {resourceConfig.ConfigName}");
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
                    LogDebug($"从Resources路径 {path} 加载Timer配置: {config.ConfigName}");
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
            LogDebug("运行时默认Timer配置创建完成");
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[TimerConfigManager] {message}");
#endif
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
#endif
    }
}