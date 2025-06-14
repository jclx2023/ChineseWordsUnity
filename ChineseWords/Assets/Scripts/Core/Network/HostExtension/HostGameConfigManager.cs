using UnityEngine;
using System.Collections.Generic;
using Core;

namespace Core.Network
{
    /// <summary>
    /// 游戏配置管理器
    /// 专门负责Timer配置、权重配置、题目时间限制等配置管理
    /// </summary>
    public class GameConfigManager
    {
        [Header("调试设置")]
        private bool enableDebugLogs = true;

        // 配置引用
        private TimerConfig timerConfig;
        private QuestionWeightConfig questionWeightConfig;

        // 配置状态
        private bool isTimerConfigInitialized = false;
        private bool isWeightConfigInitialized = false;

        // 缓存的配置值
        private Dictionary<QuestionType, float> cachedTimeLimits;

        // 事件定义
        public System.Action OnConfigurationChanged;
        public System.Action<TimerConfig> OnTimerConfigChanged;
        public System.Action<QuestionWeightConfig> OnWeightConfigChanged;

        /// <summary>
        /// 构造函数
        /// </summary>
        public GameConfigManager()
        {
            cachedTimeLimits = new Dictionary<QuestionType, float>();
            LogDebug("GameConfigManager 实例已创建");
        }

        #region 初始化

        /// <summary>
        /// 初始化配置管理器
        /// </summary>
        /// <param name="timerCfg">Timer配置</param>
        /// <param name="weightCfg">权重配置</param>
        public void Initialize(TimerConfig timerCfg = null, QuestionWeightConfig weightCfg = null)
        {
            LogDebug("初始化GameConfigManager...");

            try
            {
                // 初始化Timer配置
                InitializeTimerConfig(timerCfg);

                // 初始化权重配置
                InitializeWeightConfig(weightCfg);

                // 刷新缓存
                RefreshConfigCache();

                LogDebug($"GameConfigManager初始化完成 - Timer: {GetTimerConfigSource()}, Weight: {GetWeightConfigSource()}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameConfigManager] 初始化失败: {e.Message}");
                SetDefaultConfigurations();
            }
        }

        /// <summary>
        /// 初始化Timer配置
        /// </summary>
        /// <param name="customTimerConfig">自定义Timer配置</param>
        private void InitializeTimerConfig(TimerConfig customTimerConfig)
        {
            if (customTimerConfig != null)
            {
                timerConfig = customTimerConfig;
                LogDebug($"使用传入的Timer配置: {timerConfig.ConfigName}");
            }
            else if (TimerConfigManager.Config != null)
            {
                timerConfig = TimerConfigManager.Config;
                LogDebug($"使用全局Timer配置: {timerConfig.ConfigName}");
            }
            else
            {
                LogDebug("未找到Timer配置，将使用默认时间限制");
                timerConfig = null;
            }

            isTimerConfigInitialized = true;
        }

        /// <summary>
        /// 初始化权重配置
        /// </summary>
        /// <param name="customWeightConfig">自定义权重配置</param>
        private void InitializeWeightConfig(QuestionWeightConfig customWeightConfig)
        {
            if (customWeightConfig != null)
            {
                questionWeightConfig = customWeightConfig;
                LogDebug($"使用传入的权重配置");
            }
            else if (QuestionWeightManager.Config != null)
            {
                questionWeightConfig = QuestionWeightManager.Config;
                LogDebug($"使用全局权重配置");
            }
            else
            {
                LogDebug("未找到权重配置，将使用默认权重");
                questionWeightConfig = null;
            }

            isWeightConfigInitialized = true;
        }

        /// <summary>
        /// 设置默认配置
        /// </summary>
        private void SetDefaultConfigurations()
        {
            timerConfig = null;
            questionWeightConfig = null;
            isTimerConfigInitialized = true;
            isWeightConfigInitialized = true;
            RefreshConfigCache();
            LogDebug("已设置为默认配置");
        }

        #endregion

        #region Timer配置管理

        /// <summary>
        /// 获取指定题型的时间限制
        /// </summary>
        public float GetTimeLimitForQuestionType(QuestionType questionType)
        {
            // 优先从缓存获取
            if (cachedTimeLimits.ContainsKey(questionType))
            {
                return cachedTimeLimits[questionType];
            }

            float timeLimit = CalculateTimeLimitForQuestionType(questionType);

            // 缓存结果
            cachedTimeLimits[questionType] = timeLimit;

            LogDebug($"获取题型时间限制: {questionType} -> {timeLimit}秒 (配置源: {GetTimerConfigSource()})");
            return timeLimit;
        }

        /// <summary>
        /// 计算指定题型的时间限制
        /// </summary>
        private float CalculateTimeLimitForQuestionType(QuestionType questionType)
        {
            try
            {
                // 优先使用Timer配置
                if (timerConfig != null)
                {
                    float configTime = timerConfig.GetTimeLimitForQuestionType(questionType);
                    LogDebug($"使用Timer配置: {questionType} -> {configTime}秒");
                    return configTime;
                }

                // 回退到默认值
                float defaultTime = GetDefaultTimeLimitForQuestionType(questionType);
                LogDebug($"使用默认时间配置: {questionType} -> {defaultTime}秒");
                return defaultTime;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameConfigManager] 计算时间限制失败: {e.Message}，使用默认值");
                return GetDefaultTimeLimitForQuestionType(questionType);
            }
        }

        /// <summary>
        /// 获取题型的默认时间限制
        /// </summary>
        private float GetDefaultTimeLimitForQuestionType(QuestionType questionType)
        {
            switch (questionType)
            {
                case QuestionType.ExplanationChoice:
                case QuestionType.SimularWordChoice:
                    return 20f;
                case QuestionType.HardFill:
                    return 30f;
                case QuestionType.SoftFill:
                case QuestionType.TextPinyin:
                    return 25f;
                case QuestionType.IdiomChain:
                    return 20f;
                case QuestionType.SentimentTorF:
                case QuestionType.UsageTorF:
                    return 15f;
                case QuestionType.HandWriting:
                    return 60f;
                default:
                    return 30f; // 通用默认值
            }
        }

        /// <summary>
        /// 设置Timer配置
        /// </summary>
        public void SetTimerConfig(TimerConfig newTimerConfig)
        {
            var oldConfig = timerConfig;
            timerConfig = newTimerConfig;

            // 清空缓存，强制重新计算
            cachedTimeLimits.Clear();

            LogDebug($"Timer配置已更新: {oldConfig?.ConfigName ?? "null"} -> {newTimerConfig?.ConfigName ?? "null"}");

            // 触发配置变更事件
            OnTimerConfigChanged?.Invoke(newTimerConfig);
            OnConfigurationChanged?.Invoke();
        }

        /// <summary>
        /// 获取Timer配置源信息
        /// </summary>
        public string GetTimerConfigSource()
        {
            if (!isTimerConfigInitialized)
                return "未初始化";

            if (timerConfig == null)
                return "默认值";

            return $"Timer配置({timerConfig.ConfigName})";
        }

        #endregion

        #region 权重配置管理

        /// <summary>
        /// 选择随机题目类型（根据权重）
        /// </summary>
        public QuestionType SelectRandomQuestionType()
        {
            try
            {
                // 优先使用当前权重配置
                if (questionWeightConfig != null)
                {
                    var selectedType = questionWeightConfig.SelectRandomType();
                    LogDebug($"根据权重配置选择题型: {selectedType} (配置)");
                    return selectedType;
                }

                // 回退到全局权重管理器
                var globalType = QuestionWeightManager.SelectRandomQuestionType();
                LogDebug($"根据全局权重选择题型: {globalType}");
                return globalType;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameConfigManager] 选择随机题型失败: {e.Message}，使用默认题型");
                return QuestionType.HardFill; // 默认题型
            }
        }

        /// <summary>
        /// 获取当前权重配置
        /// </summary>
        public Dictionary<QuestionType, float> GetCurrentWeights()
        {
            try
            {
                if (questionWeightConfig != null)
                {
                    return questionWeightConfig.GetWeights();
                }

                return QuestionWeightManager.GetWeights();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameConfigManager] 获取权重配置失败: {e.Message}");
                return GetDefaultWeights();
            }
        }

        /// <summary>
        /// 获取默认权重配置
        /// </summary>
        private Dictionary<QuestionType, float> GetDefaultWeights()
        {
            return new Dictionary<QuestionType, float>
            {
                { QuestionType.HardFill, 1.0f },
                { QuestionType.SoftFill, 1.0f },
                { QuestionType.TextPinyin, 1.0f },
                { QuestionType.ExplanationChoice, 1.0f },
                { QuestionType.SimularWordChoice, 1.0f },
                { QuestionType.IdiomChain, 1.0f },
                { QuestionType.SentimentTorF, 1.0f },
                { QuestionType.UsageTorF, 1.0f }
            };
        }

        /// <summary>
        /// 设置权重配置
        /// </summary>
        public void SetWeightConfig(QuestionWeightConfig newWeightConfig)
        {
            var oldConfig = questionWeightConfig;
            questionWeightConfig = newWeightConfig;

            LogDebug($"权重配置已更新");

            // 触发配置变更事件
            OnWeightConfigChanged?.Invoke(newWeightConfig);
            OnConfigurationChanged?.Invoke();
        }

        /// <summary>
        /// 获取权重配置源信息
        /// </summary>
        public string GetWeightConfigSource()
        {
            if (!isWeightConfigInitialized)
                return "未初始化";

            if (questionWeightConfig == null)
                return "默认权重";

            return $"权重配置";
        }

        /// <summary>
        /// 检查题型是否启用
        /// </summary>
        public bool IsQuestionTypeEnabled(QuestionType questionType)
        {
            try
            {
                if (questionWeightConfig != null)
                {
                    return questionWeightConfig.IsEnabled(questionType);
                }

                // 检查全局配置
                var weights = QuestionWeightManager.GetWeights();
                return weights.ContainsKey(questionType) && weights[questionType] > 0;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameConfigManager] 检查题型启用状态失败: {e.Message}");
                return true; // 默认启用
            }
        }

        /// <summary>
        /// 获取题型权重
        /// </summary>
        public float GetQuestionTypeWeight(QuestionType questionType)
        {
            try
            {
                if (questionWeightConfig != null)
                {
                    return questionWeightConfig.GetWeight(questionType);
                }

                var weights = QuestionWeightManager.GetWeights();
                return weights.ContainsKey(questionType) ? weights[questionType] : 1.0f;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameConfigManager] 获取题型权重失败: {e.Message}");
                return 1.0f;
            }
        }

        #endregion

        #region 配置缓存管理

        /// <summary>
        /// 刷新配置缓存
        /// </summary>
        public void RefreshConfigCache()
        {
            LogDebug("刷新配置缓存...");

            // 清空时间限制缓存
            cachedTimeLimits.Clear();

            // 预缓存常用题型的时间限制
            var commonQuestionTypes = new QuestionType[]
            {
                QuestionType.HardFill,
                QuestionType.SoftFill,
                QuestionType.TextPinyin,
                QuestionType.ExplanationChoice,
                QuestionType.SimularWordChoice,
                QuestionType.IdiomChain,
                QuestionType.SentimentTorF,
                QuestionType.UsageTorF
            };

            foreach (var questionType in commonQuestionTypes)
            {
                GetTimeLimitForQuestionType(questionType); // 这会触发缓存
            }

            LogDebug($"配置缓存刷新完成，缓存了 {cachedTimeLimits.Count} 个题型的时间限制");
        }

        /// <summary>
        /// 清空配置缓存
        /// </summary>
        public void ClearConfigCache()
        {
            cachedTimeLimits.Clear();
            LogDebug("配置缓存已清空");
        }

        #endregion

        #region 工具方法
        /// <summary>
        /// 调试日志输出
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[GameConfigManager] {message}");
            }
        }

        #endregion

        #region 销毁和清理

        /// <summary>
        /// 销毁配置管理器
        /// </summary>
        public void Dispose()
        {
            // 清理事件
            OnConfigurationChanged = null;
            OnTimerConfigChanged = null;
            OnWeightConfigChanged = null;

            // 清理缓存
            ClearConfigCache();

            // 清理配置引用
            timerConfig = null;
            questionWeightConfig = null;

            // 重置状态
            isTimerConfigInitialized = false;
            isWeightConfigInitialized = false;

            LogDebug("GameConfigManager已销毁");
        }

        #endregion
    }
}