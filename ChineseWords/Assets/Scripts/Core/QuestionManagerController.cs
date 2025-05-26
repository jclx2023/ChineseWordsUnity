using UnityEngine;
using Core;
using GameLogic;
using GameLogic.FillBlank;
using GameLogic.TorF;
using GameLogic.Choice;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Managers;

namespace Core
{
    /// <summary>
    /// 题目管理控制器
    /// - 负责题目类型的选择和切换
    /// - 管理题目管理器的生命周期
    /// - 协调计时器和血量系统
    /// - 处理答题结果和超时逻辑
    /// </summary>
    public class QuestionManagerController : MonoBehaviour
    {
        [Header("游戏配置")]
        [SerializeField] private float defaultTimeLimit = 15f;
        [SerializeField] private int defaultInitialHealth = 3;
        [SerializeField] private int defaultDamagePerWrong = 1;

        [Header("题目流程配置")]
        [SerializeField] private float timeUpDelay = 1f;
        [SerializeField] private float questionTransitionDelay = 0.5f;
        [SerializeField] private bool autoStartOnAwake = false;

        [Header("调试信息")]
        [SerializeField] private bool enableDebugLog = true;

        // 核心组件
        private QuestionManagerBase currentManager;
        private TimerManager timerManager;
        private PlayerHealthManager healthManager;

        // 游戏状态
        private bool isGameRunning = false;
        private bool isTransitioning = false;
        private int questionCount = 0;

        // 题型权重配置
        private Dictionary<QuestionType, float> typeWeights = new Dictionary<QuestionType, float>()
        {
            { QuestionType.IdiomChain, 1f },
            { QuestionType.TextPinyin, 1f },
            { QuestionType.HardFill, 1f },
            { QuestionType.SoftFill, 1f },
            { QuestionType.SentimentTorF, 1f },
            { QuestionType.SimularWordChoice, 1f },
            { QuestionType.UsageTorF, 1f },
            { QuestionType.ExplanationChoice, 1f },
        };

        #region Unity生命周期

        private void Awake()
        {
            InitializeComponents();
        }

        private void Start()
        {
            SetupGameConfiguration();
            BindEvents();

            if (autoStartOnAwake)
            {
                StartQuestionFlow();
            }
        }

        private void OnDestroy()
        {
            UnbindEvents();
            CleanupCurrentManager();
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化组件引用
        /// </summary>
        private void InitializeComponents()
        {
            timerManager = GetComponent<TimerManager>();
            healthManager = GetComponent<PlayerHealthManager>();

            if (timerManager == null)
            {
                Debug.LogError("[QMC] 找不到 TimerManager 组件");
            }

            if (healthManager == null)
            {
                Debug.LogError("[QMC] 找不到 PlayerHealthManager 组件");
            }

            LogDebug("组件初始化完成");
        }

        /// <summary>
        /// 设置游戏配置
        /// </summary>
        private void SetupGameConfiguration()
        {
            // 尝试从 ConfigManager 获取配置，如果没有则使用默认值
            float timeLimit = defaultTimeLimit;
            int initialHealth = defaultInitialHealth;
            int damagePerWrong = defaultDamagePerWrong;

            // 如果有 ConfigManager，优先使用其配置
            if (ConfigManager.Instance != null && ConfigManager.Instance.Config != null)
            {
                var config = ConfigManager.Instance.Config;
                timeLimit = config.timeLimit > 0 ? config.timeLimit : defaultTimeLimit;
                initialHealth = config.initialHealth > 0 ? config.initialHealth : defaultInitialHealth;
                damagePerWrong = config.damagePerWrong > 0 ? config.damagePerWrong : defaultDamagePerWrong;

                LogDebug($"使用 ConfigManager 配置: 时间={timeLimit}, 血量={initialHealth}, 扣血={damagePerWrong}");
            }
            else
            {
                LogDebug($"使用默认配置: 时间={timeLimit}, 血量={initialHealth}, 扣血={damagePerWrong}");
            }

            // 应用配置到各个管理器
            if (timerManager != null)
                timerManager.ApplyConfig(timeLimit);

            if (healthManager != null)
                healthManager.ApplyConfig(initialHealth, damagePerWrong);
        }

        /// <summary>
        /// 绑定事件
        /// </summary>
        private void BindEvents()
        {
            if (timerManager != null)
            {
                timerManager.OnTimeUp += HandleTimeUp;
                LogDebug("计时器事件绑定完成");
            }

            // 血量管理器的事件在每个题目管理器创建时绑定
        }

        /// <summary>
        /// 解绑事件
        /// </summary>
        private void UnbindEvents()
        {
            if (timerManager != null)
            {
                timerManager.OnTimeUp -= HandleTimeUp;
            }

            if (currentManager != null && currentManager.OnAnswerResult != null)
            {
                currentManager.OnAnswerResult -= HandleAnswerResult;
            }

            LogDebug("事件解绑完成");
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 开始题目流程
        /// </summary>
        public void StartQuestionFlow()
        {
            if (isGameRunning)
            {
                LogDebug("题目流程已经在运行");
                return;
            }

            LogDebug("开始题目流程");
            isGameRunning = true;
            questionCount = 0;

            StartCoroutine(DelayedFirstQuestion());
        }

        /// <summary>
        /// 停止题目流程
        /// </summary>
        public void StopQuestionFlow()
        {
            if (!isGameRunning)
            {
                LogDebug("题目流程未在运行");
                return;
            }

            LogDebug("停止题目流程");
            isGameRunning = false;

            // 停止计时器
            if (timerManager != null)
                timerManager.StopTimer();

            // 清理当前题目管理器
            CleanupCurrentManager();

            // 停止所有协程
            StopAllCoroutines();
        }

        /// <summary>
        /// 暂停题目流程
        /// </summary>
        public void PauseQuestionFlow()
        {
            if (!isGameRunning)
            {
                LogDebug("题目流程未在运行，无法暂停");
                return;
            }

            LogDebug("暂停题目流程");

            if (timerManager != null)
                timerManager.PauseTimer();
        }

        /// <summary>
        /// 恢复题目流程
        /// </summary>
        public void ResumeQuestionFlow()
        {
            if (!isGameRunning)
            {
                LogDebug("题目流程未在运行，无法恢复");
                return;
            }

            LogDebug("恢复题目流程");

            if (timerManager != null)
                timerManager.ResumeTimer();
        }

        /// <summary>
        /// 跳过当前题目
        /// </summary>
        public void SkipCurrentQuestion()
        {
            if (!isGameRunning || isTransitioning)
            {
                LogDebug("无法跳过题目：游戏未运行或正在转换中");
                return;
            }

            LogDebug("跳过当前题目");

            // 停止计时器
            if (timerManager != null)
                timerManager.StopTimer();

            // 直接加载下一题
            StartCoroutine(DelayedLoadNextQuestion(0f));
        }

        #endregion

        #region 题目管理

        /// <summary>
        /// 延迟加载第一题
        /// </summary>
        public IEnumerator DelayedFirstQuestion()
        {
            yield return new WaitForEndOfFrame();

            if (isGameRunning)
            {
                LoadNextQuestion();
            }
        }

        /// <summary>
        /// 加载下一题
        /// </summary>
        private void LoadNextQuestion()
        {
            if (!isGameRunning)
            {
                LogDebug("游戏未运行，停止加载题目");
                return;
            }

            if (isTransitioning)
            {
                LogDebug("正在转换中，跳过加载题目");
                return;
            }

            isTransitioning = true;
            questionCount++;

            LogDebug($"开始加载第 {questionCount} 题");

            // 清理当前管理器
            CleanupCurrentManager();

            // 选择题型并创建管理器
            var selectedType = SelectRandomTypeByWeight();
            currentManager = CreateManager(selectedType);

            if (currentManager == null)
            {
                Debug.LogError("[QMC] 创建题目管理器失败");
                isTransitioning = false;
                return;
            }



            // 绑定血量管理器
            //if (healthManager != null)
            //{
            //    healthManager.BindManager(currentManager);
            //}
            // 绑定答题结果事件
            currentManager.OnAnswerResult += HandleAnswerResult;
            // 延迟加载题目
            StartCoroutine(DelayedLoadQuestion());
        }

        /// <summary>
        /// 延迟加载题目
        /// </summary>
        private IEnumerator DelayedLoadQuestion()
        {
            yield return new WaitForSeconds(questionTransitionDelay);

            if (currentManager != null && isGameRunning)
            {
                currentManager.LoadQuestion();

                // 启动计时器
                if (timerManager != null)
                {
                    timerManager.StartTimer();
                }

                LogDebug($"第 {questionCount} 题加载完成，计时器已启动");
            }

            isTransitioning = false;
        }

        /// <summary>
        /// 延迟加载下一题
        /// </summary>
        private IEnumerator DelayedLoadNextQuestion(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (isGameRunning)
            {
                LoadNextQuestion();
            }
        }

        /// <summary>
        /// 清理当前题目管理器
        /// </summary>
        private void CleanupCurrentManager()
        {
            if (currentManager != null)
            {
                // 解绑事件
                currentManager.OnAnswerResult -= HandleAnswerResult;

                // 销毁组件
                if (currentManager is Component component)
                {
                    Destroy(component);
                }

                currentManager = null;
                LogDebug("当前题目管理器已清理");
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 处理答题结果
        /// </summary>
        private void HandleAnswerResult(bool isCorrect)
        {
            if (!isGameRunning)
            {
                LogDebug("游戏未运行，忽略答题结果");
                return;
            }

            LogDebug($"答题结果: {(isCorrect ? "正确" : "错误")}");

            // 停止计时器
            if (timerManager != null)
                timerManager.StopTimer();

            // 如果答错，血量管理器会自动处理扣血
            if (!isCorrect && healthManager != null)
            {
                healthManager.HPHandleAnswerResult(false);
            }

            // 检查游戏是否结束
            if (healthManager != null && healthManager.IsGameOver)
            {
                LogDebug("游戏结束，停止题目流程");
                StopQuestionFlow();
                return;
            }

            // 延迟加载下一题
            StartCoroutine(DelayedLoadNextQuestion(timeUpDelay));
        }

        /// <summary>
        /// 处理超时
        /// </summary>
        private void HandleTimeUp()
        {
            if (!isGameRunning)
            {
                LogDebug("游戏未运行，忽略超时事件");
                return;
            }

            LogDebug("答题超时");

            // 停止计时器
            if (timerManager != null)
                timerManager.StopTimer();

            // 触发答错结果
            if (currentManager != null)
            {
                currentManager.OnAnswerResult?.Invoke(false);
            }
            else
            {
                // 如果没有当前管理器，直接处理超时
                HandleAnswerResult(false);
            }
        }

        #endregion

        #region 题型管理

        /// <summary>
        /// 根据权重随机选择题型
        /// </summary>
        private QuestionType SelectRandomTypeByWeight()
        {
            float total = typeWeights.Values.Sum();
            float random = Random.Range(0f, total);
            float accumulator = 0f;

            foreach (var pair in typeWeights)
            {
                accumulator += pair.Value;
                if (random <= accumulator)
                {
                    LogDebug($"选择题型: {pair.Key}");
                    return pair.Key;
                }
            }

            // fallback
            var fallbackType = typeWeights.Keys.First();
            LogDebug($"使用fallback题型: {fallbackType}");
            return fallbackType;
        }

        /// <summary>
        /// 创建题目管理器
        /// </summary>
        private QuestionManagerBase CreateManager(QuestionType type)
        {
            QuestionManagerBase manager = null;

            try
            {
                switch (type)
                {
                    case QuestionType.IdiomChain:
                        manager = gameObject.AddComponent<IdiomChainQuestionManager>();
                        break;
                    case QuestionType.TextPinyin:
                        manager = gameObject.AddComponent<TextPinyinQuestionManager>();
                        break;
                    case QuestionType.HardFill:
                        manager = gameObject.AddComponent<HardFillQuestionManager>();
                        break;
                    case QuestionType.SoftFill:
                        manager = gameObject.AddComponent<SoftFillQuestionManager>();
                        break;
                    case QuestionType.SentimentTorF:
                        manager = gameObject.AddComponent<SentimentTorFQuestionManager>();
                        break;
                    case QuestionType.SimularWordChoice:
                        manager = gameObject.AddComponent<SimularWordChoiceQuestionManager>();
                        break;
                    case QuestionType.UsageTorF:
                        manager = gameObject.AddComponent<UsageTorFQuestionManager>();
                        break;
                    case QuestionType.ExplanationChoice:
                        manager = gameObject.AddComponent<ExplanationChoiceQuestionManager>();
                        break;
                    default:
                        Debug.LogError($"[QMC] 未实现的题型: {type}");
                        return null;
                }

                LogDebug($"创建题目管理器: {type} -> {manager.GetType().Name}");
                return manager;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[QMC] 创建题目管理器失败: {type}, 错误: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 设置题型权重
        /// </summary>
        /// <param name="weights">题型权重字典</param>
        public void SetTypeWeights(Dictionary<QuestionType, float> weights)
        {
            if (weights == null || weights.Count == 0)
            {
                Debug.LogWarning("[QMC] 题型权重为空，保持原有配置");
                return;
            }

            typeWeights = new Dictionary<QuestionType, float>(weights);
            LogDebug("题型权重已更新");
        }

        /// <summary>
        /// 获取当前题型权重
        /// </summary>
        public Dictionary<QuestionType, float> GetTypeWeights()
        {
            return new Dictionary<QuestionType, float>(typeWeights);
        }

        #endregion

        #region 公共属性

        /// <summary>
        /// 获取当前题目管理器
        /// </summary>
        public QuestionManagerBase CurrentManager => currentManager;

        /// <summary>
        /// 检查游戏是否在运行
        /// </summary>
        public bool IsGameRunning => isGameRunning;

        /// <summary>
        /// 检查是否正在转换题目
        /// </summary>
        public bool IsTransitioning => isTransitioning;

        /// <summary>
        /// 获取当前题目数量
        /// </summary>
        public int QuestionCount => questionCount;

        #endregion

        #region 调试工具

        /// <summary>
        /// 调试日志输出
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLog)
            {
                Debug.Log($"[QMC] {message}");
            }
        }

        #endregion

        #region Editor工具方法

#if UNITY_EDITOR
        /// <summary>
        /// 在编辑器中测试开始游戏
        /// </summary>
        [ContextMenu("测试开始游戏")]
        private void TestStartGame()
        {
            StartQuestionFlow();
        }

        /// <summary>
        /// 在编辑器中测试停止游戏
        /// </summary>
        [ContextMenu("测试停止游戏")]
        private void TestStopGame()
        {
            StopQuestionFlow();
        }

        /// <summary>
        /// 在编辑器中测试跳过题目
        /// </summary>
        [ContextMenu("测试跳过题目")]
        private void TestSkipQuestion()
        {
            SkipCurrentQuestion();
        }
#endif

        #endregion
    }
}