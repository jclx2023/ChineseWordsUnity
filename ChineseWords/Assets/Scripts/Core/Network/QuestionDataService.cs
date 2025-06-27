using UnityEngine;
using Core;
using Core.Network;
using System.Collections.Generic;
using System;

namespace Core.Network
{
    /// <summary>
    /// 题目数据服务 - 专门负责题目数据获取，不涉及UI
    /// 解决Host端重复创建题目管理器的问题
    /// </summary>
    public class QuestionDataService : MonoBehaviour
    {
        public static QuestionDataService Instance { get; private set; }

        [Header("数据提供者配置")]
        [SerializeField] private bool enableDebugLogs = false;
        [SerializeField] private bool cacheProviders = true;

        [Header("Timer配置")]
        [SerializeField] private TimerConfig timerConfig;

        // 数据提供者缓存
        private Dictionary<QuestionType, IQuestionDataProvider> cachedProviders;

        // 临时数据提供者对象池（避免重复创建GameObject）
        private Dictionary<QuestionType, GameObject> temporaryProviderObjects;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitializeService();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeService()
        {
            cachedProviders = new Dictionary<QuestionType, IQuestionDataProvider>();
            temporaryProviderObjects = new Dictionary<QuestionType, GameObject>();

            LogDebug("题目数据服务初始化完成");
        }

        /// <summary>
        /// 获取题目数据 - Host端专用
        /// </summary>
        public NetworkQuestionData GetQuestionData(QuestionType questionType)
        {
            LogDebug($"请求题目数据: {questionType}");

            try
            {
                // 1. 先尝试从缓存中获取
                if (cacheProviders && cachedProviders.ContainsKey(questionType))
                {
                    var cachedProvider = cachedProviders[questionType];
                    if (cachedProvider != null)
                    {
                        return cachedProvider.GetQuestionData();
                    }
                }

                // 2. 查找场景中现有的数据提供者
                var provider = FindExistingProviderInScene(questionType);

                // 3. 如果没有找到，创建专用的数据提供者
                if (provider == null)
                {
                    provider = CreateDataOnlyProvider(questionType);
                }

                // 4. 缓存提供者
                if (provider != null && cacheProviders)
                {
                    cachedProviders[questionType] = provider;
                }

                // 5. 获取题目数据
                if (provider != null)
                {
                    var questionData = provider.GetQuestionData();
                    if (questionData != null)
                    {
                        // 确保使用配置的时间限制
                        if (timerConfig != null)
                        {
                            float configuredTime = timerConfig.GetTimeLimitForQuestionType(questionType);
                            if (Math.Abs(questionData.timeLimit - configuredTime) > 0.1f)
                            {
                                LogDebug($"更新题目时间限制从 {questionData.timeLimit} 到 {configuredTime}");
                            }
                        }

                        LogDebug($"成功获取题目: {questionData.questionText}, 时间限制: {questionData.timeLimit}秒");
                        return questionData;
                    }
                }

                LogDebug($"无法获取 {questionType} 的题目数据，使用备用题目");
                return CreateFallbackQuestion(questionType);
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestionDataService] 获取题目数据失败: {e.Message}");
                return CreateFallbackQuestion(questionType);
            }
        }

        /// <summary>
        /// 查找场景中现有的数据提供者（避免重复创建）
        /// </summary>
        private IQuestionDataProvider FindExistingProviderInScene(QuestionType questionType)
        {
            // 查找场景中是否已有对应的题目管理器
            switch (questionType)
            {
                case QuestionType.ExplanationChoice:
                    var explManager = FindObjectOfType<GameLogic.Choice.ExplanationChoiceQuestionManager>();
                    return explManager as IQuestionDataProvider;

                case QuestionType.HardFill:
                    var hardManager = FindObjectOfType<GameLogic.FillBlank.HardFillQuestionManager>();
                    return hardManager as IQuestionDataProvider;

                case QuestionType.SoftFill:
                    var softManager = FindObjectOfType<GameLogic.FillBlank.SoftFillQuestionManager>();
                    return softManager as IQuestionDataProvider;

                case QuestionType.TextPinyin:
                    var pinyinManager = FindObjectOfType<GameLogic.FillBlank.TextPinyinQuestionManager>();
                    return pinyinManager as IQuestionDataProvider;

                case QuestionType.SimularWordChoice:
                    var simManager = FindObjectOfType<GameLogic.Choice.SimularWordChoiceQuestionManager>();
                    return simManager as IQuestionDataProvider;

                case QuestionType.IdiomChain:
                    var idiomManager = FindObjectOfType<GameLogic.FillBlank.IdiomChainQuestionManager>();
                    return idiomManager as IQuestionDataProvider;

                case QuestionType.SentimentTorF:
                    var sentimentManager = FindObjectOfType<GameLogic.TorF.SentimentTorFQuestionManager>();
                    return sentimentManager as IQuestionDataProvider;

                case QuestionType.UsageTorF:
                    var usageManager = FindObjectOfType<GameLogic.TorF.UsageTorFQuestionManager>();
                    return usageManager as IQuestionDataProvider;

                default:
                    return null;
            }
        }

        /// <summary>
        /// 创建专用于数据获取的轻量级提供者
        /// 不创建UI，只负责数据获取
        /// </summary>
        private IQuestionDataProvider CreateDataOnlyProvider(QuestionType questionType)
        {
            LogDebug($"创建数据专用提供者: {questionType}");

            // 检查是否已有临时对象
            if (temporaryProviderObjects.ContainsKey(questionType))
            {
                var existingObj = temporaryProviderObjects[questionType];
                if (existingObj != null)
                {
                    var existingProvider = existingObj.GetComponent<IQuestionDataProvider>();
                    if (existingProvider != null)
                    {
                        LogDebug($"复用现有的临时提供者: {questionType}");
                        return existingProvider;
                    }
                }
            }

            try
            {
                // 创建新的临时GameObject
                GameObject tempObj = new GameObject($"DataProvider_{questionType}");
                tempObj.transform.SetParent(this.transform); // 设为数据服务的子对象

                // 添加标记，表明这是数据专用提供者
                tempObj.tag = "DataOnlyProvider";

                QuestionManagerBase manager = null;

                switch (questionType)
                {
                    case QuestionType.ExplanationChoice:
                        manager = tempObj.AddComponent<GameLogic.Choice.ExplanationChoiceQuestionManager>();
                        break;
                    case QuestionType.HardFill:
                        manager = tempObj.AddComponent<GameLogic.FillBlank.HardFillQuestionManager>();
                        break;
                    case QuestionType.SoftFill:
                        manager = tempObj.AddComponent<GameLogic.FillBlank.SoftFillQuestionManager>();
                        break;
                    case QuestionType.TextPinyin:
                        manager = tempObj.AddComponent<GameLogic.FillBlank.TextPinyinQuestionManager>();
                        break;
                    case QuestionType.SimularWordChoice:
                        manager = tempObj.AddComponent<GameLogic.Choice.SimularWordChoiceQuestionManager>();
                        break;
                    case QuestionType.IdiomChain:
                        manager = tempObj.AddComponent<GameLogic.FillBlank.IdiomChainQuestionManager>();
                        break;
                    case QuestionType.SentimentTorF:
                        manager = tempObj.AddComponent<GameLogic.TorF.SentimentTorFQuestionManager>();
                        break;
                    case QuestionType.UsageTorF:
                        manager = tempObj.AddComponent<GameLogic.TorF.UsageTorFQuestionManager>();
                        break;
                    default:
                        Destroy(tempObj);
                        LogDebug($"不支持的题目类型: {questionType}");
                        return null;
                }

                if (manager != null && manager is IQuestionDataProvider)
                {
                    temporaryProviderObjects[questionType] = tempObj;
                    LogDebug($"成功创建数据提供者: {questionType}");
                    return manager as IQuestionDataProvider;
                }
                else
                {
                    Destroy(tempObj);
                    LogDebug($"创建的管理器不支持IQuestionDataProvider接口: {questionType}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestionDataService] 创建数据提供者失败: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// 创建备用题目（当获取失败时）
        /// </summary>
        private NetworkQuestionData CreateFallbackQuestion(QuestionType questionType)
        {
            float timeLimit = GetTimeLimitForQuestionType(questionType);

            return NetworkQuestionDataExtensions.CreateFromLocalData(
                questionType,
                $"这是一个{questionType}类型的测试题目",
                "测试答案",
                questionType == QuestionType.ExplanationChoice || questionType == QuestionType.SimularWordChoice
                    ? new string[] { "选项A", "测试答案", "选项C", "选项D" }
                    : null,
                timeLimit,
                "{\"source\": \"fallback\", \"isDefault\": true}"
            );
        }
        /// <summary>
        /// 设置Timer配置
        /// </summary>
        public void SetTimerConfig(TimerConfig config)
        {
            timerConfig = config;
            LogDebug($"Timer配置已设置: {(config != null ? config.ConfigName : "null")}");
        }

        /// <summary>
        /// 获取指定题型的时间限制
        /// </summary>
        private float GetTimeLimitForQuestionType(QuestionType questionType)
        {
            if (timerConfig != null)
            {
                float timeLimit = timerConfig.GetTimeLimitForQuestionType(questionType);
                LogDebug($"从Timer配置获取时间限制 {questionType}: {timeLimit}秒");
                return timeLimit;
            }

            // 回退到默认时间
            float defaultTime = GetDefaultTimeLimit(questionType);
            LogDebug($"使用默认时间限制 {questionType}: {defaultTime}秒");
            return defaultTime;
        }

        /// <summary>
        /// 获取默认时间限制（当没有TimerConfig时使用）
        /// </summary>
        private float GetDefaultTimeLimit(QuestionType questionType)
        {
            switch (questionType)
            {
                case QuestionType.ExplanationChoice:
                case QuestionType.SimularWordChoice:
                case QuestionType.IdiomChain:
                    return 20f;
                case QuestionType.HardFill:
                    return 30f;
                case QuestionType.SoftFill:
                case QuestionType.TextPinyin:
                    return 25f;
                case QuestionType.SentimentTorF:
                case QuestionType.UsageTorF:
                    return 15f;
                case QuestionType.HandWriting:
                    return 60f;
                default:
                    return 30f;
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void ClearCache()
        {
            LogDebug("清理数据提供者缓存");

            cachedProviders.Clear();

            // 销毁临时对象
            foreach (var obj in temporaryProviderObjects.Values)
            {
                if (obj != null)
                    Destroy(obj);
            }
            temporaryProviderObjects.Clear();
        }

        /// <summary>
        /// 获取支持的题目类型列表
        /// </summary>
        public QuestionType[] GetSupportedQuestionTypes()
        {
            return new QuestionType[]
            {
                QuestionType.ExplanationChoice,
                QuestionType.HardFill,
                QuestionType.SoftFill,
                QuestionType.TextPinyin,
                QuestionType.SimularWordChoice,
                QuestionType.IdiomChain,
                QuestionType.SentimentTorF,
                QuestionType.UsageTorF,
                // 添加其他支持的题型...
            };
        }

        /// <summary>
        /// 预加载指定类型的数据提供者
        /// </summary>
        public void PreloadProvider(QuestionType questionType)
        {
            if (!cachedProviders.ContainsKey(questionType))
            {
                var provider = CreateDataOnlyProvider(questionType);
                if (provider != null)
                {
                    cachedProviders[questionType] = provider;
                    LogDebug($"预加载数据提供者: {questionType}");
                }
            }
        }

        /// <summary>
        /// 预加载所有支持的数据提供者
        /// </summary>
        public void PreloadAllProviders()
        {
            LogDebug("预加载所有数据提供者...");
            foreach (var questionType in GetSupportedQuestionTypes())
            {
                PreloadProvider(questionType);
            }
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                //Debug.Log($"[QuestionDataService] {message}");
            }
        }

        private void OnDestroy()
        {
            ClearCache();
            if (Instance == this)
            {
                Instance = null;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("预加载所有数据提供者")]
        public void EditorPreloadAll()
        {
            if (Application.isPlaying)
                PreloadAllProviders();
        }

        [ContextMenu("清理缓存")]
        public void EditorClearCache()
        {
            if (Application.isPlaying)
                ClearCache();
        }

        [ContextMenu("显示缓存状态")]
        public void EditorShowCacheStatus()
        {
            Debug.Log($"=== 数据提供者缓存状态 ===");
            Debug.Log($"缓存的提供者数量: {cachedProviders?.Count ?? 0}");
            Debug.Log($"临时对象数量: {temporaryProviderObjects?.Count ?? 0}");

            if (cachedProviders != null)
            {
                foreach (var pair in cachedProviders)
                {
                    Debug.Log($"- {pair.Key}: {(pair.Value != null ? "✓" : "✗")}");
                }
            }
        }
#endif
    }
}