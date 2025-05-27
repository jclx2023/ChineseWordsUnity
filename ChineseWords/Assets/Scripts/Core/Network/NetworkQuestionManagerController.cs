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
    /// 优化后的网络题目管理控制器
    /// 专注于：客户端题目显示 + 网络消息处理 + 答案提交
    /// 移除了：题目生成逻辑（由Host负责）+ 重复的管理器创建逻辑
    /// </summary>
    public class NetworkQuestionManagerController : MonoBehaviour
    {
        [Header("游戏管理器组件")]
        [SerializeField] private TimerManager timerManager;
        [SerializeField] private PlayerHealthManager hpManager;

        [Header("游戏配置")]
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

        // 题目类型权重（供Host使用）
        public Dictionary<QuestionType, float> TypeWeights = new Dictionary<QuestionType, float>()
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

            // 初始化组件但不启动游戏
            InitializeComponents();
        }

        private void Start()
        {
            RegisterNetworkEvents();
            isInitialized = true;
            LogDebug("组件已初始化，等待游戏开始指令");
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
        /// 初始化组件依赖
        /// </summary>
        private void InitializeComponents()
        {
            LogDebug("初始化组件依赖...");

            // 获取或查找必要组件
            if (timerManager == null)
                timerManager = GetComponent<TimerManager>() ?? FindObjectOfType<TimerManager>();
            if (hpManager == null)
                hpManager = GetComponent<PlayerHealthManager>() ?? FindObjectOfType<PlayerHealthManager>();

            if (timerManager == null)
            {
                Debug.LogError("[NQMC] 找不到TimerManager组件");
                return;
            }

            if (hpManager == null)
            {
                Debug.LogError("[NQMC] 找不到PlayerHealthManager组件");
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

            LogDebug("组件依赖初始化完成");
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

        #region 公共接口 - 由外部系统调用

        /// <summary>
        /// 开始游戏（由外部系统调用）
        /// </summary>
        /// <param name="multiplayerMode">是否为多人模式</param>
        public void StartGame(bool multiplayerMode = false)
        {
            if (!isInitialized)
            {
                Debug.LogError("[NQMC] 组件未初始化，无法开始游戏");
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
        /// 停止游戏（由外部系统调用）
        /// </summary>
        public void StopGame()
        {
            LogDebug("停止游戏");
            gameStarted = false;
            isMyTurn = false;
            isWaitingForNetworkQuestion = false;

            StopTimer();
            CleanupCurrentManager();
        }

        /// <summary>
        /// 暂停游戏（由外部系统调用）
        /// </summary>
        public void PauseGame()
        {
            LogDebug("暂停游戏");
            if (timerManager != null)
                timerManager.PauseTimer();
        }

        /// <summary>
        /// 恢复游戏（由外部系统调用）
        /// </summary>
        public void ResumeGame()
        {
            LogDebug("恢复游戏");
            if (timerManager != null)
                timerManager.ResumeTimer();
        }

        /// <summary>
        /// 强制开始下一题（由外部系统调用）
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
        /// 开始多人游戏
        /// </summary>
        private void StartMultiplayerGame()
        {
            if (NetworkManager.Instance?.IsConnected == true)
            {
                LogDebug("多人模式：等待服务器分配回合");
                isWaitingForNetworkQuestion = true;
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
        /// 加载下一个本地题目（单机模式）
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
                // 绑定管理器到血量系统
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
        /// 加载网络题目
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
                // 绑定管理器到血量系统
                if (hpManager != null)
                    hpManager.BindManager(currentManager);

                // 绑定网络答案结果事件
                currentManager.OnAnswerResult += HandleNetworkAnswerSubmission;

                // 延迟加载题目
                StartCoroutine(DelayedLoadQuestion());

                LogDebug($"网络题目管理器创建成功: {networkQuestion.questionType}");
            }
            else
            {
                Debug.LogError("[NQMC] 无法为网络题目创建管理器");
            }
        }

        /// <summary>
        /// 延迟加载题目
        /// </summary>
        private IEnumerator DelayedLoadQuestion()
        {
            yield return null;
            if (currentManager != null)
            {
                currentManager.LoadQuestion();
                StartTimer();
                LogDebug("题目已加载并开始计时");
            }
        }

        /// <summary>
        /// 创建题目管理器（使用工厂）
        /// </summary>
        private QuestionManagerBase CreateQuestionManager(QuestionType questionType, bool isNetworkMode)
        {
            // 使用工厂创建管理器
            return QuestionManagerFactory.CreateManagerOnGameObject(
                this.gameObject,
                questionType,
                isNetworkMode,
                false // 不是仅数据模式，需要UI
            );
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

        #endregion

        #region 答题结果处理

        /// <summary>
        /// 处理本地答题结果（单机模式）
        /// </summary>
        private void HandleLocalAnswerResult(bool isCorrect)
        {
            LogDebug($"单机模式答题结果: {(isCorrect ? "正确" : "错误")}");

            StopTimer();

            // 处理血量变化
            if (!isCorrect && hpManager != null)
            {
                hpManager.HPHandleAnswerResult(false);

                // 检查是否游戏结束
                if (hpManager.CurrentHealth <= 0)
                {
                    LogDebug("血量耗尽，游戏结束");
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
        /// 处理网络答案提交（多人模式）
        /// </summary>
        private void HandleNetworkAnswerSubmission(bool isCorrect)
        {
            LogDebug("多人模式本地答题，等待服务器确认");
            // 多人模式下，本地答题结果不立即处理，等待服务器结果
            StopTimer();
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
        /// 接收到网络题目
        /// </summary>
        private void OnNetworkQuestionReceived(NetworkQuestionData question)
        {
            if (!isMultiplayerMode || !isWaitingForNetworkQuestion)
                return;

            LogDebug($"收到网络题目: {question.questionType}");
            isWaitingForNetworkQuestion = false;
            LoadNetworkQuestion(question);
        }

        /// <summary>
        /// 接收到网络答案结果
        /// </summary>
        private void OnNetworkAnswerResult(bool isCorrect, string correctAnswer)
        {
            if (!isMultiplayerMode)
                return;

            LogDebug($"收到服务器答题结果: {(isCorrect ? "正确" : "错误")}");

            // 处理血量变化
            if (!isCorrect && hpManager != null)
            {
                hpManager.HPHandleAnswerResult(false);

                // 检查是否游戏结束
                if (hpManager.CurrentHealth <= 0)
                {
                    LogDebug("血量耗尽，游戏结束");
                    OnGameEnded?.Invoke(false);
                    return;
                }
            }

            // 显示答案反馈
            ShowAnswerFeedback(isCorrect, correctAnswer);
            OnAnswerCompleted?.Invoke(isCorrect);
        }

        /// <summary>
        /// 玩家回合变更
        /// </summary>
        private void OnNetworkPlayerTurnChanged(ushort playerId)
        {
            if (!isMultiplayerMode)
                return;

            bool wasMyTurn = isMyTurn;
            isMyTurn = (playerId == NetworkManager.Instance?.ClientId);

            LogDebug($"回合变更: {(isMyTurn ? "轮到我了" : $"轮到玩家{playerId}")}");

            if (isMyTurn && !wasMyTurn)
            {
                // 轮到我答题
                RequestNetworkQuestion();
            }
            else if (!isMyTurn && wasMyTurn)
            {
                // 不再是我的回合，停止当前题目
                StopTimer();
                // 可以选择清理当前管理器或保持显示状态
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
        /// 提交答案（供题目管理器调用）
        /// </summary>
        public void SubmitAnswer(string answer)
        {
            if (isMultiplayerMode && isMyTurn && NetworkManager.Instance?.IsConnected == true)
            {
                NetworkManager.Instance.SubmitAnswer(answer);
                LogDebug($"提交网络答案: {answer}");
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
        /// 开始计时器
        /// </summary>
        private void StartTimer()
        {
            if (timerManager != null)
            {
                timerManager.StartTimer();
                LogDebug("计时器已开始");
            }
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

        #region 调试方法

        /// <summary>
        /// 获取当前状态信息（调试用）
        /// </summary>
        public string GetStatusInfo()
        {
            var info = "=== NQMC 状态 ===\n";
            info += $"已初始化: {isInitialized}\n";
            info += $"游戏已开始: {gameStarted}\n";
            info += $"多人模式: {isMultiplayerMode}\n";
            info += $"我的回合: {isMyTurn}\n";
            info += $"等待网络题目: {isWaitingForNetworkQuestion}\n";
            info += $"当前管理器: {(currentManager != null ? currentManager.GetType().Name : "无")}\n";

            if (hpManager != null)
                info += $"当前血量: {hpManager.CurrentHealth}\n";

            if (timerManager != null)
                info += $"计时器状态: {(timerManager.IsRunning ? "运行中" : "已停止")}\n";

            return info;
        }

        /// <summary>
        /// 重置组件状态（调试用）
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

        #endregion
    }
}