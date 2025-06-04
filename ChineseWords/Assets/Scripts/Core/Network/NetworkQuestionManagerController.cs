using UnityEngine;
using Core;
using Core.Network;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Managers;

namespace Core.Network
{
    /// <summary>
    /// 统一网络和本地题目管理控制器
    /// 专门用于单机和多人模式的题目管理 + 网络消息处理 + 答案提交
    /// 已经统一了单机题目生成逻辑（用于Host端分发）+ 重复的管理器创建逻辑
    /// </summary>
    public class NetworkQuestionManagerController : MonoBehaviour
    {
        [Header("依赖管理器引用")]
        [SerializeField] private TimerManager timerManager;
        [SerializeField] private PlayerHealthManager hpManager;

        [Header("依赖配置")]
        [SerializeField] private float timeUpDelay = 1f;
        [SerializeField] private bool isMultiplayerMode = false;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        public static NetworkQuestionManagerController Instance { get; private set; }

        // 当前状态
        private QuestionManagerBase currentManager;
        private NetworkQuestionData currentNetworkQuestion;
        private bool isMyTurn = false;
        private bool isWaitingForNetworkQuestion = false;
        private bool gameStarted = false;
        private bool isInitialized = false;
        private ushort currentTurnPlayerId = 0;  // 当前回合玩家ID
        private bool hasReceivedTurnChange = false;  // 是否收到回合变更信息

        // 题目类型权重（重置用于供Host使用）
        public Dictionary<QuestionType, float> TypeWeights = new Dictionary<QuestionType, float>()
        {

        };

        // 事件
        public System.Action<bool> OnGameEnded;
        public System.Action<bool> OnAnswerCompleted;

        // 属性
        public bool IsMultiplayerMode => isMultiplayerMode;
        public bool IsMyTurn => isMyTurn;
        public bool IsGameStarted => gameStarted;
        public bool IsInitialized => isInitialized;
        public QuestionManagerBase CurrentManager => currentManager;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                LogDebug("NetworkQuestionManagerController 单例已创建");
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // 初始化引用但不启动依赖
            InitializeComponents();
        }

        private void Start()
        {
            RegisterNetworkEvents();
            isInitialized = true;
            LogDebug("引用已初始化，等待依赖开始指令");
        }

        private void OnDestroy()
        {
            UnregisterNetworkEvents();
            CleanupCurrentManager();

            if (Instance == this)
                Instance = null;

            LogDebug("NetworkQuestionManagerController 已销毁");
        }

        /// <summary>
        /// 初始化引用组件
        /// </summary>
        private void InitializeComponents()
        {
            LogDebug("初始化引用组件...");

            // 获取或查找必要组件
            if (timerManager == null)
                timerManager = GetComponent<TimerManager>() ?? FindObjectOfType<TimerManager>();
            if (hpManager == null)
                hpManager = GetComponent<PlayerHealthManager>() ?? FindObjectOfType<PlayerHealthManager>();

            if (timerManager == null)
            {
                Debug.LogError("[NQMC] 找不到TimerManager引用");
                return;
            }

            if (hpManager == null)
            {
                Debug.LogError("[NQMC] 找不到PlayerHealthManager引用");
                return;
            }

            // 应用配置
            var cfg = ConfigManager.Instance?.Config;
            if (cfg != null)
            {
                timerManager.ApplyConfig(cfg.timeLimit);
                hpManager.ApplyConfig(cfg.initialHealth, cfg.damagePerWrong);
            }

            // 绑定计时器事件
            timerManager.OnTimeUp += HandleTimeUp;

            LogDebug("引用组件初始化完成");
        }

        /// <summary>
        /// 注册网络事件
        /// </summary>
        private void RegisterNetworkEvents()
        {
            NetworkManager.OnQuestionReceived += OnNetworkQuestionReceived;
            NetworkManager.OnAnswerResultReceived += OnNetworkAnswerResult;
            NetworkManager.OnPlayerTurnChanged += OnNetworkPlayerTurnChanged;
            NetworkManager.OnDisconnected += OnNetworkDisconnected;

            LogDebug("网络事件已注册");
        }

        /// <summary>
        /// 取消注册网络事件
        /// </summary>
        private void UnregisterNetworkEvents()
        {
            NetworkManager.OnQuestionReceived -= OnNetworkQuestionReceived;
            NetworkManager.OnAnswerResultReceived -= OnNetworkAnswerResult;
            NetworkManager.OnPlayerTurnChanged -= OnNetworkPlayerTurnChanged;
            NetworkManager.OnDisconnected -= OnNetworkDisconnected;

            LogDebug("网络事件已取消注册");
        }

        #region 公共接口 - 用于外部系统调用

        /// <summary>
        /// 开始游戏（用于外部系统调用）
        /// </summary>
        /// <param name="multiplayerMode">是否为多人模式</param>
        public void StartGame(bool multiplayerMode = false)
        {
            if (!isInitialized)
            {
                Debug.LogError("[NQMC] 引用未初始化，无法开始游戏");
                return;
            }

            isMultiplayerMode = multiplayerMode;
            gameStarted = true;

            LogDebug($"开始游戏 - 模式: {(isMultiplayerMode ? "多人" : "单机")}");

            if (isMultiplayerMode)
            {
                StartMultiplayerGame();
            }
            else
            {
                StartSinglePlayerGame();
            }
        }

        /// <summary>
        /// 修改：停止游戏 - 重置状态
        /// </summary>
        public void StopGame()
        {
            LogDebug("停止游戏");
            gameStarted = false;
            isMyTurn = false;
            isWaitingForNetworkQuestion = false;

            currentDisplayState = QuestionDisplayState.GameEnded;
            hasReceivedTurnChange = false;
            currentTurnPlayerId = 0;

            StopTimer();
            CleanupCurrentManager();
        }

        /// <summary>
        /// 暂停游戏（用于外部系统调用）
        /// </summary>
        public void PauseGame()
        {
            LogDebug("暂停游戏");
            if (timerManager != null)
                timerManager.PauseTimer();
        }

        /// <summary>
        /// 恢复游戏（用于外部系统调用）
        /// </summary>
        public void ResumeGame()
        {
            LogDebug("恢复游戏");
            if (timerManager != null)
                timerManager.ResumeTimer();
        }

        /// <summary>
        /// 强制开始下一题（用于外部系统调用）
        /// </summary>
        public void ForceNextQuestion()
        {
            if (!gameStarted)
            {
                LogDebug("游戏未开始，无法强制下一题");
                return;
            }

            LogDebug("强制开始下一题");

            if (isMultiplayerMode)
            {
                // 多人模式下，只有轮到自己才能强制下一题
                if (isMyTurn)
                {
                    RequestNetworkQuestion();
                }
                else
                {
                    LogDebug("不是我的回合，无法强制下一题");
                }
            }
            else
            {
                // 单机模式直接加载下一题
                LoadNextLocalQuestion();
            }
        }

        #endregion

        #region 游戏模式处理

        /// <summary>
        /// 开始多人游戏 - 修复版本：不立即等待题目
        /// </summary>
        private void StartMultiplayerGame()
        {
            if (NetworkManager.Instance?.IsConnected == true)
            {
                LogDebug("多人模式：等待游戏开始和回合分配");
                currentDisplayState = QuestionDisplayState.WaitingForGame;
                // 修复：不立即设置等待题目状态
                isWaitingForNetworkQuestion = false;
                hasReceivedTurnChange = false;
            }
            else
            {
                Debug.LogError("[NQMC] 未连接到服务器，无法开始多人游戏");
                OnGameEnded?.Invoke(false);
            }
        }

        /// <summary>
        /// 开始单机游戏
        /// </summary>
        private void StartSinglePlayerGame()
        {
            LogDebug("单机模式：立即开始第一题");
            isMyTurn = true;
            StartCoroutine(DelayedFirstQuestion());
        }

        /// <summary>
        /// 延迟开始第一题
        /// </summary>
        private IEnumerator DelayedFirstQuestion()
        {
            yield return null;
            LoadNextLocalQuestion();
        }

        #endregion

        #region 题目管理 - 简化版本

        /// <summary>
        /// 加载下一本地题目（单机模式）
        /// </summary>
        private void LoadNextLocalQuestion()
        {
            if (!gameStarted)
            {
                LogDebug("游戏已停止，不加载新题目");
                return;
            }

            LogDebug("加载本地题目...");

            // 清理当前管理器
            CleanupCurrentManager();

            // 选择题目类型并创建管理器
            var selectedType = SelectRandomTypeByWeight();
            currentManager = CreateQuestionManager(selectedType, false);

            if (currentManager != null)
            {
                // 绑定管理器到生命系统
                if (hpManager != null)
                    hpManager.BindManager(currentManager);

                // 绑定答案结果事件
                currentManager.OnAnswerResult += HandleLocalAnswerResult;

                // 延迟加载题目
                StartCoroutine(DelayedLoadQuestion());

                LogDebug($"本地题目管理器创建成功: {selectedType}");
            }
            else
            {
                Debug.LogError("[NQMC] 无法创建题目管理器");
                OnGameEnded?.Invoke(false);
            }
        }

        /// <summary>
        /// 请求网络题目（多人模式）
        /// </summary>
        private void RequestNetworkQuestion()
        {
            if (NetworkManager.Instance?.IsConnected == true)
            {
                isWaitingForNetworkQuestion = true;
                NetworkManager.Instance.RequestQuestion();
                LogDebug("请求网络题目...");
            }
            else
            {
                Debug.LogError("[NQMC] 网络未连接，无法请求题目");
                OnGameEnded?.Invoke(false);
            }
        }

        /// <summary>
        /// 加载网络题目 - 修复版本
        /// </summary>
        private void LoadNetworkQuestion(NetworkQuestionData networkQuestion)
        {
            if (networkQuestion == null)
            {
                Debug.LogError("[NQMC] 网络题目数据为空");
                return;
            }

            LogDebug($"加载网络题目: {networkQuestion.questionType}");

            // 清理当前管理器
            CleanupCurrentManager();

            // 保存网络题目数据
            currentNetworkQuestion = networkQuestion;

            // 创建对应的管理器
            currentManager = CreateQuestionManager(networkQuestion.questionType, true);

            if (currentManager != null)
            {
                // 绑定管理器到生命系统
                if (hpManager != null)
                    hpManager.BindManager(currentManager);

                // 绑定网络答案提交事件
                currentManager.OnAnswerResult += HandleNetworkAnswerSubmission;

                // 延迟加载题目 - 修复：传递网络数据
                StartCoroutine(DelayedLoadNetworkQuestion(networkQuestion));

                LogDebug($"网络题目管理器创建成功: {networkQuestion.questionType}");
            }
            else
            {
                Debug.LogError("[NQMC] 无法为网络题目创建管理器");
            }
        }

        /// <summary>
        /// 延迟加载题目 - 本地模式（添加默认时间支持）
        /// </summary>
        private IEnumerator DelayedLoadQuestion()
        {
            yield return null;
            if (currentManager != null)
            {
                currentManager.LoadQuestion();

                // 本地模式也支持动态时间（通过TimerConfig获取）
                float timeLimit = GetLocalTimeLimit();
                if (timeLimit > 0)
                {
                    StartTimerWithDynamicLimit(timeLimit);
                    LogDebug($"本地题目已加载并开始计时，时间限制: {timeLimit}秒");
                }
                else
                {
                    StartTimer();
                    LogDebug("本地题目已加载并开始计时（使用默认时间）");
                }
            }
        }
        /// <summary>
        /// 获取本地模式的时间限制（从TimerConfig）
        /// </summary>
        private float GetLocalTimeLimit()
        {
            try
            {
                // 如果有当前管理器，尝试获取其题型
                if (currentManager != null)
                {
                    // 尝试获取管理器的题型信息
                    var questionTypeField = currentManager.GetType().GetField("questionType",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (questionTypeField != null && questionTypeField.GetValue(currentManager) is QuestionType questionType)
                    {
                        if (TimerConfigManager.Config != null)
                        {
                            float timeLimit = TimerConfigManager.Config.GetTimeLimitForQuestionType(questionType);
                            LogDebug($"从TimerConfig获取本地题型时间限制: {questionType} -> {timeLimit}秒");
                            return timeLimit;
                        }
                    }
                }

                LogDebug("无法获取本地题型时间限制，使用默认配置");
                return 0f; // 返回0表示使用默认时间
            }
            catch (System.Exception e)
            {
                LogDebug($"获取本地时间限制失败: {e.Message}");
                return 0f;
            }
        }

        /// <summary>
        /// 延迟加载网络题目 - 修复版本，支持动态时间限制
        /// </summary>
        private IEnumerator DelayedLoadNetworkQuestion(NetworkQuestionData networkData)
        {
            yield return null;
            if (currentManager != null)
            {
                // 修复：检查管理器是否支持网络数据加载
                if (currentManager is NetworkQuestionManagerBase networkManager)
                {
                    // 调用网络管理器的专用加载方法
                    var loadMethod = networkManager.GetType().GetMethod("LoadNetworkQuestion",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (loadMethod != null)
                    {
                        LogDebug($"通过反射调用网络题目加载方法: {networkData.questionType}");
                        loadMethod.Invoke(networkManager, new object[] { networkData });
                    }
                    else
                    {
                        // 备用方案：尝试公共方法
                        Debug.LogWarning($"[NQMC] 未找到LoadNetworkQuestion方法，尝试备用加载方式");
                        currentManager.LoadQuestion();
                    }
                }
                else
                {
                    // 不是网络管理器，使用普通加载
                    Debug.LogWarning($"[NQMC] 管理器不是NetworkQuestionManagerBase类型: {currentManager.GetType()}");
                    currentManager.LoadQuestion();
                }

                // 关键修改：使用网络题目的时间限制启动计时器
                StartTimerWithDynamicLimit(networkData.timeLimit);
                LogDebug($"网络题目已加载并开始计时，时间限制: {networkData.timeLimit}秒");
            }
        }
        /// <summary>
        /// 使用动态时间限制启动计时器
        /// </summary>
        private void StartTimerWithDynamicLimit(float timeLimit)
        {
            if (timerManager != null)
            {
                // 检查TimerManager是否支持动态时间限制
                var setTimeLimitMethod = timerManager.GetType().GetMethod("SetTimeLimit");
                var startTimerWithLimitMethod = timerManager.GetType().GetMethod("StartTimer", new System.Type[] { typeof(float) });

                if (startTimerWithLimitMethod != null)
                {
                    // 方案1：直接调用带时间参数的StartTimer方法
                    LogDebug($"使用动态时间限制启动计时器: {timeLimit}秒");
                    startTimerWithLimitMethod.Invoke(timerManager, new object[] { timeLimit });
                }
                else if (setTimeLimitMethod != null)
                {
                    // 方案2：先设置时间限制，再启动计时器
                    LogDebug($"设置时间限制后启动计时器: {timeLimit}秒");
                    setTimeLimitMethod.Invoke(timerManager, new object[] { timeLimit });
                    timerManager.StartTimer();
                }
                else
                {
                    // 方案3：尝试通过配置管理器设置
                    TrySetTimerThroughConfig(timeLimit);
                    timerManager.StartTimer();
                }
            }
            else
            {
                Debug.LogError("[NQMC] TimerManager引用为空，无法启动计时器");
            }
        }
        /// <summary>
        /// 尝试通过TimerConfig设置时间限制
        /// </summary>
        private void TrySetTimerThroughConfig(float timeLimit)
        {
            try
            {
                LogDebug($"尝试通过配置管理器设置时间限制: {timeLimit}秒");

                // 检查是否有运行时配置接口
                if (TimerConfigManager.Config != null)
                {
                    // 创建临时配置或修改当前配置的时间限制
                    // 这里需要根据TimerConfigManager的具体实现来调整

                    // 方案1：如果TimerConfigManager支持临时时间设置
                    var setTempTimeMethod = typeof(TimerConfigManager).GetMethod("SetTemporaryTimeLimit");
                    if (setTempTimeMethod != null)
                    {
                        setTempTimeMethod.Invoke(null, new object[] { timeLimit });
                        LogDebug("通过TimerConfigManager设置临时时间限制成功");
                        return;
                    }

                    // 方案2：直接通过配置应用到TimerManager
                    var applyConfigMethod = timerManager.GetType().GetMethod("ApplyConfig", new System.Type[] { typeof(float) });
                    if (applyConfigMethod != null)
                    {
                        applyConfigMethod.Invoke(timerManager, new object[] { timeLimit });
                        LogDebug("通过ApplyConfig设置时间限制成功");
                        return;
                    }
                }

                Debug.LogWarning($"[NQMC] 无法设置动态时间限制，将使用默认配置");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NQMC] 设置时间限制失败: {e.Message}");
            }
        }

        /// <summary>
        /// 创建题目管理器（使用工厂）
        /// </summary>
        private QuestionManagerBase CreateQuestionManager(QuestionType questionType, bool isNetworkMode)
        {
            // 创建独立的子GameObject用于题目管理器
            GameObject managerObj = new GameObject($"{questionType}Manager");

            // 添加UI环境标记
            managerObj.AddComponent<UIEnvironmentMarker>();

            var manager = QuestionManagerFactory.CreateManagerOnGameObject(
                managerObj,
                questionType,
                isNetworkMode,
                false
            );

            return manager;
        }

        /// <summary>
        /// 清理当前题目管理器
        /// </summary>
        private void CleanupCurrentManager()
        {
            if (currentManager != null)
            {
                LogDebug($"清理题目管理器: {currentManager.GetType().Name}");

                // 移除事件监听
                currentManager.OnAnswerResult -= HandleLocalAnswerResult;
                currentManager.OnAnswerResult -= HandleNetworkAnswerSubmission;

                // 使用工厂安全销毁
                QuestionManagerFactory.DestroyManager(currentManager);
                currentManager = null;
            }
        }

        /// <summary>
        /// 根据权重选择随机题目类型
        /// </summary>
        private QuestionType SelectRandomTypeByWeight()
        {
            // 优先使用新的权重管理器
            try
            {
                return QuestionWeightManager.SelectRandomQuestionType();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"权重管理器选择失败，使用旧版逻辑: {e.Message}");

                // 回退到旧的权重逻辑
                var typeWeights = TypeWeights;
                float total = typeWeights.Values.Sum();
                float r = Random.Range(0, total);
                float acc = 0f;

                foreach (var pair in typeWeights)
                {
                    acc += pair.Value;
                    if (r <= acc)
                        return pair.Key;
                }

                return typeWeights.Keys.First();
            }
        }

        #endregion

        #region 答题结果处理

        /// <summary>
        /// 处理本地答题结果（单机模式）
        /// </summary>
        private void HandleLocalAnswerResult(bool isCorrect)
        {
            LogDebug($"单机模式答题结果: {(isCorrect ? "正确" : "错误")}");

            StopTimer();

            // 处理生命变化
            if (!isCorrect && hpManager != null)
            {
                hpManager.HPHandleAnswerResult(false);

                // 检查是否游戏结束
                if (hpManager.CurrentHealth <= 0)
                {
                    LogDebug("生命耗尽，游戏结束");
                    OnGameEnded?.Invoke(false);
                    return;
                }
            }

            // 通知答题完成
            OnAnswerCompleted?.Invoke(isCorrect);

            // 延迟加载下一题
            Invoke(nameof(LoadNextLocalQuestion), timeUpDelay);
        }

        /// <summary>
        /// 修改：处理网络答案提交
        /// </summary>
        private void HandleNetworkAnswerSubmission(bool isCorrect)
        {
            LogDebug("多人模式本地答题，等待服务器确认");

            // 只有在我的回合才处理答案提交
            if (currentDisplayState == QuestionDisplayState.MyTurn)
            {
                StopTimer();
            }
            else
            {
                LogDebug("不是我的回合，忽略答案提交处理");
            }
        }

        /// <summary>
        /// 处理超时
        /// </summary>
        private void HandleTimeUp()
        {
            LogDebug("答题超时");

            StopTimer();

            if (isMultiplayerMode)
            {
                // 网络模式：提交空答案表示超时
                if (isMyTurn && NetworkManager.Instance?.IsConnected == true)
                {
                    NetworkManager.Instance.SubmitAnswer("");
                    LogDebug("超时，提交空答案");
                }
            }
            else
            {
                // 单机模式：直接处理超时
                if (currentManager != null)
                {
                    currentManager.OnAnswerResult?.Invoke(false);
                }
                else
                {
                    Invoke(nameof(LoadNextLocalQuestion), timeUpDelay);
                }
            }
        }

        #endregion

        #region 网络事件处理

        /// <summary>
        /// 接收到网络题目 - 修复版本：添加状态验证
        /// </summary>
        private void OnNetworkQuestionReceived(NetworkQuestionData question)
        {
            if (!isMultiplayerMode)
            {
                LogDebug("非多人模式，忽略网络题目");
                return;
            }

            LogDebug($"收到网络题目: {question.questionType}, 时间限制: {question.timeLimit}秒");
            LogDebug($"当前状态: 我的回合={isMyTurn}, 等待题目={isWaitingForNetworkQuestion}, 回合ID={currentTurnPlayerId}");

            // 关键修改：根据当前状态决定如何处理题目
            switch (currentDisplayState)
            {
                case QuestionDisplayState.MyTurn:
                    // 轮到我答题：完整加载题目
                    LogDebug("轮到我答题，完整加载题目");
                    isWaitingForNetworkQuestion = false;
                    LoadNetworkQuestion(question);
                    break;

                case QuestionDisplayState.OtherPlayerTurn:
                    // 其他玩家回合：显示题目但不启用交互
                    LogDebug($"其他玩家回合，显示题目但不可交互 (当前回合玩家: {currentTurnPlayerId})");
                    LoadNetworkQuestionAsObserver(question);
                    break;

                case QuestionDisplayState.WaitingForGame:
                    // 还在等待游戏开始，可能是时序问题
                    LogDebug("收到题目但游戏状态为等待中，检查回合状态");
                    if (hasReceivedTurnChange && isMyTurn)
                    {
                        LogDebug("回合状态确认为我的回合，加载题目");
                        currentDisplayState = QuestionDisplayState.MyTurn;
                        isWaitingForNetworkQuestion = false;
                        LoadNetworkQuestion(question);
                    }
                    else if (hasReceivedTurnChange && !isMyTurn)
                    {
                        LogDebug("回合状态确认为其他玩家回合，观察模式加载题目");
                        currentDisplayState = QuestionDisplayState.OtherPlayerTurn;
                        LoadNetworkQuestionAsObserver(question);
                    }
                    else
                    {
                        LogDebug("尚未收到回合变更信息，缓存题目");
                        // 可以选择缓存题目或忽略
                        currentNetworkQuestion = question;
                    }
                    break;

                default:
                    LogDebug($"未知状态 {currentDisplayState}，忽略题目");
                    break;
            }
        }

        /// <summary>
        /// 接收到网络答案结果
        /// </summary>
        private void OnNetworkAnswerResult(bool isCorrect, string correctAnswer)
        {
            if (!isMultiplayerMode)
                return;

            LogDebug($"收到服务器答题结果: {(isCorrect ? "正确" : "错误")}");

            // 处理生命变化
            if (!isCorrect && hpManager != null)
            {
                hpManager.HPHandleAnswerResult(false);

                // 检查是否游戏结束
                if (hpManager.CurrentHealth <= 0)
                {
                    LogDebug("生命耗尽，游戏结束");
                    OnGameEnded?.Invoke(false);
                    return;
                }
            }

            // 显示答案反馈
            ShowAnswerFeedback(isCorrect, correctAnswer);
            OnAnswerCompleted?.Invoke(isCorrect);
        }


        /// <summary>
        /// 玩家回合变更 - 修复版本：添加状态管理
        /// </summary>
        private void OnNetworkPlayerTurnChanged(ushort playerId)
        {
            if (!isMultiplayerMode)
                return;

            bool wasMyTurn = isMyTurn;
            currentTurnPlayerId = playerId;
            isMyTurn = (playerId == NetworkManager.Instance?.ClientId);
            hasReceivedTurnChange = true;

            LogDebug($"回合变更: {(isMyTurn ? "轮到我了" : $"轮到玩家{playerId}")}");

            // 更新显示状态
            if (isMyTurn)
            {
                currentDisplayState = QuestionDisplayState.MyTurn;
                LogDebug("状态变更为：MyTurn");

                // 轮到我答题 - 但不立即请求题目，等待服务器发送
                if (!wasMyTurn)
                {
                    LogDebug("轮到我答题，等待服务器发送题目");
                    isWaitingForNetworkQuestion = true;
                }
            }
            else
            {
                currentDisplayState = QuestionDisplayState.OtherPlayerTurn;
                LogDebug($"状态变更为：OtherPlayerTurn (玩家{playerId})");

                // 不再是我的回合
                if (wasMyTurn)
                {
                    StopTimer();
                    isWaitingForNetworkQuestion = false;
                }
            }
        }

        /// <summary>
        /// 网络断开连接
        /// </summary>
        private void OnNetworkDisconnected()
        {
            if (isMultiplayerMode && gameStarted)
            {
                Debug.LogWarning("[NQMC] 网络断开，游戏将结束");
                StopTimer();
                OnGameEnded?.Invoke(false);
            }
        }

        #endregion

        #region 题目管理器接口

        /// <summary>
        /// 修改：提交答案 - 添加状态验证
        /// </summary>
        public void SubmitAnswer(string answer)
        {
            // 添加状态验证
            if (isMultiplayerMode)
            {
                if (currentDisplayState != QuestionDisplayState.MyTurn)
                {
                    LogDebug($"不是我的回合，无法提交答案。当前状态: {currentDisplayState}");
                    return;
                }

                if (!isMyTurn)
                {
                    LogDebug("回合状态不匹配，无法提交答案");
                    return;
                }

                if (NetworkManager.Instance?.IsConnected == true)
                {
                    NetworkManager.Instance.SubmitAnswer(answer);
                    LogDebug($"提交网络答案: {answer}");
                }
                else
                {
                    LogDebug("网络未连接，无法提交答案");
                }
            }
            else if (!isMultiplayerMode && currentManager != null)
            {
                currentManager.CheckAnswer(answer);
                LogDebug($"提交本地答案: {answer}");
            }
            else
            {
                LogDebug("无法提交答案：模式不匹配或状态异常");
            }
        }
        /// <summary>
        /// 获取当前题目的网络数据（供题目管理器使用）
        /// </summary>
        public NetworkQuestionData GetCurrentNetworkQuestion()
        {
            return currentNetworkQuestion;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 开始计时器（原有方法，保持兼容性）
        /// </summary>
        private void StartTimer()
        {
            if (timerManager != null)
            {
                timerManager.StartTimer();
                LogDebug("计时器已开始（使用默认时间限制）");
            }
        }

        /// <summary>
        /// 使用指定时间限制开始计时器
        /// </summary>
        private void StartTimer(float timeLimit)
        {
            StartTimerWithDynamicLimit(timeLimit);
        }

        /// <summary>
        /// 停止计时器
        /// </summary>
        private void StopTimer()
        {
            if (timerManager != null)
            {
                timerManager.StopTimer();
                LogDebug("计时器已停止");
            }
        }

        /// <summary>
        /// 显示答案反馈
        /// </summary>
        private void ShowAnswerFeedback(bool isCorrect, string correctAnswer)
        {
            string feedback = isCorrect ? "回答正确！" : $"回答错误，正确答案是：{correctAnswer}";
            LogDebug($"答题反馈: {feedback}");

            // 如果当前管理器支持显示网络结果，调用相应方法
            if (currentManager != null)
            {
                // 可以通过反射或接口调用管理器的显示方法
                // 例如: if (currentManager is INetworkResultDisplayer displayer)
                //           displayer.ShowNetworkResult(isCorrect, correctAnswer);
            }
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[NQMC] {message}");
            }
        }

        #endregion

        #region 状态管理 - 新增字段


        // 在现有字段后添加状态枚举
        private enum QuestionDisplayState
        {
            WaitingForGame,     // 等待游戏开始
            MyTurn,            // 轮到我答题
            OtherPlayerTurn,   // 其他玩家回合
            GameEnded          // 游戏结束
        }

        private QuestionDisplayState currentDisplayState = QuestionDisplayState.WaitingForGame;
        #endregion
        /// <summary>
        /// 新增：以观察者模式加载网络题目
        /// </summary>
        private void LoadNetworkQuestionAsObserver(NetworkQuestionData networkQuestion)
        {
            if (networkQuestion == null)
            {
                Debug.LogError("[NQMC] 网络题目数据为空");
                return;
            }

            LogDebug($"以观察者模式加载网络题目: {networkQuestion.questionType}");

            // 清理当前管理器
            CleanupCurrentManager();

            // 保存网络题目数据
            currentNetworkQuestion = networkQuestion;

            // 创建对应的管理器（观察模式）
            currentManager = CreateQuestionManager(networkQuestion.questionType, true);

            if (currentManager != null)
            {
                // 不绑定答案提交事件，只显示题目
                LogDebug("观察者模式：不绑定答案提交事件");

                // 延迟加载题目（观察模式）
                StartCoroutine(DelayedLoadNetworkQuestionAsObserver(networkQuestion));

                LogDebug($"观察者模式题目管理器创建成功: {networkQuestion.questionType}");
            }
            else
            {
                Debug.LogError("[NQMC] 无法为观察者模式创建管理器");
            }
        }

        /// <summary>
        /// 新增：延迟加载网络题目（观察者模式）
        /// </summary>
        private IEnumerator DelayedLoadNetworkQuestionAsObserver(NetworkQuestionData networkData)
        {
            yield return null;
            if (currentManager != null)
            {
                // 加载题目但不启动计时器
                if (currentManager is NetworkQuestionManagerBase networkManager)
                {
                    var loadMethod = networkManager.GetType().GetMethod("LoadNetworkQuestion",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (loadMethod != null)
                    {
                        LogDebug($"观察者模式：通过反射调用网络题目加载方法: {networkData.questionType}");
                        loadMethod.Invoke(networkManager, new object[] { networkData });
                    }
                    else
                    {
                        Debug.LogWarning($"[NQMC] 观察者模式：未找到LoadNetworkQuestion方法，使用普通加载");
                        currentManager.LoadQuestion();
                    }
                }
                else
                {
                    LogDebug("观察者模式：使用普通加载方法");
                    currentManager.LoadQuestion();
                }

                // 关键：观察者模式下不启动计时器，或者启动只读计时器
                LogDebug($"观察者模式：题目已加载，不启动交互计时器");

                // 可选：启动只读计时器用于显示倒计时
                StartObserverTimer(networkData.timeLimit);
            }
        }

        /// <summary>
        /// 新增：启动观察者计时器（只读模式）
        /// </summary>
        private void StartObserverTimer(float timeLimit)
        {
            if (timerManager != null)
            {
                // 检查TimerManager是否支持只读模式
                var startReadOnlyMethod = timerManager.GetType().GetMethod("StartReadOnlyTimer");
                if (startReadOnlyMethod != null)
                {
                    LogDebug($"启动只读计时器: {timeLimit}秒");
                    startReadOnlyMethod.Invoke(timerManager, new object[] { timeLimit });
                }
                else
                {
                    // 如果不支持只读模式，可以选择不启动计时器
                    LogDebug("TimerManager不支持只读模式，跳过计时器启动");
                }
            }
        }

        public string GetStatusInfo()
        {
            var info = "=== NQMC 状态 ===\n";
            info += $"已初始化: {isInitialized}\n";
            info += $"游戏已开始: {gameStarted}\n";
            info += $"多人模式: {isMultiplayerMode}\n";
            info += $"我的回合: {isMyTurn}\n";
            info += $"等待网络题目: {isWaitingForNetworkQuestion}\n";
            info += $"显示状态: {currentDisplayState}\n";  // 新增
            info += $"当前回合玩家ID: {currentTurnPlayerId}\n";  // 新增
            info += $"已收到回合变更: {hasReceivedTurnChange}\n";  // 新增
            info += $"当前管理器: {(currentManager != null ? currentManager.GetType().Name : "无")}\n";

            if (hpManager != null)
                info += $"当前生命: {hpManager.CurrentHealth}\n";

            if (timerManager != null)
                info += $"计时器状态: {(timerManager.IsRunning ? "运行中" : "已停止")}\n";

            return info;
        }

        /// <summary>
        /// 重置引用状态（调试用）
        /// </summary>
        [ContextMenu("重置状态")]
        public void ResetState()
        {
            if (Application.isPlaying)
            {
                LogDebug("重置状态");
                StopGame();
                CleanupCurrentManager();
                currentNetworkQuestion = null;
                isWaitingForNetworkQuestion = false;
            }
        }
    }
}