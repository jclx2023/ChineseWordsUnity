using UnityEngine;
using Core;
using Core.Network;
using GameLogic;
using GameLogic.FillBlank;
using GameLogic.TorF;
using GameLogic.Choice;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Managers;

namespace Core.Network
{
    /// <summary>
    /// 网络化题目管理控制器
    /// 负责在网络游戏中管理题目生成、答题逻辑，但不控制游戏开始
    /// 游戏开始由HostGameManager控制
    /// </summary>
    public class NetworkQuestionManagerController : MonoBehaviour
    {
        [Header("游戏管理器组件")]
        [SerializeField] private TimerManager timerManager;
        [SerializeField] private PlayerHealthManager hpManager;

        [Header("游戏配置")]
        [SerializeField] private float timeUpDelay = 1f;
        [SerializeField] private bool isMultiplayerMode = false;

        public static NetworkQuestionManagerController Instance { get; private set; }

        // 当前状态
        private QuestionManagerBase manager;
        private NetworkQuestionData currentNetworkQuestion;
        private bool isMyTurn = false;
        private bool isWaitingForNetworkQuestion = false;
        private bool gameStarted = false;
        private bool isInitialized = false;

        // 题目类型权重（与原QMC保持一致）
        public Dictionary<QuestionType, float> TypeWeights = new Dictionary<QuestionType, float>()
        {
            //{ QuestionType.HandWriting, 0.5f },
            { QuestionType.IdiomChain, 1f },
            { QuestionType.TextPinyin, 1f },
            { QuestionType.HardFill, 1f },
            { QuestionType.SoftFill, 1f },
            //{ QuestionType.AbbrFill, 1f },
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

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // 注意：不要DontDestroyOnLoad，因为这是场景特定的组件
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // 初始化但不启动游戏
            InitializeComponents();
        }

        private void Start()
        {
            RegisterNetworkEvents();
            isInitialized = true;
            Debug.Log("[NQMC] 组件已初始化，等待游戏开始指令");
        }

        private void OnDestroy()
        {
            UnregisterNetworkEvents();
            if (Instance == this)
                Instance = null;
        }

        private void InitializeComponents()
        {
            // 获取或添加必要组件
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
        }

        private void RegisterNetworkEvents()
        {
            NetworkManager.OnQuestionReceived += OnNetworkQuestionReceived;
            NetworkManager.OnAnswerResultReceived += OnNetworkAnswerResult;
            NetworkManager.OnPlayerTurnChanged += OnNetworkPlayerTurnChanged;
            NetworkManager.OnDisconnected += OnNetworkDisconnected;
        }

        private void UnregisterNetworkEvents()
        {
            NetworkManager.OnQuestionReceived -= OnNetworkQuestionReceived;
            NetworkManager.OnAnswerResultReceived -= OnNetworkAnswerResult;
            NetworkManager.OnPlayerTurnChanged -= OnNetworkPlayerTurnChanged;
            NetworkManager.OnDisconnected -= OnNetworkDisconnected;
        }

        #region 公共接口 - 由HostGameManager调用

        /// <summary>
        /// 开始游戏（由HostGameManager调用）
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

            Debug.Log($"[NQMC] 开始游戏 - 模式: {(isMultiplayerMode ? "多人" : "单机")}");

            if (isMultiplayerMode)
            {
                if (NetworkManager.Instance?.IsConnected == true)
                {
                    Debug.Log("[NQMC] 多人模式：等待服务器分配回合");
                    // 多人模式下等待服务器通知轮次
                    isWaitingForNetworkQuestion = true;
                }
                else
                {
                    Debug.LogError("[NQMC] 未连接到服务器，无法开始多人游戏");
                    OnGameEnded?.Invoke(false);
                    return;
                }
            }
            else
            {
                // 单机模式：立即开始第一题
                isMyTurn = true;
                StartCoroutine(DelayedFirstQuestion());
            }
        }

        /// <summary>
        /// 停止游戏（由HostGameManager调用）
        /// </summary>
        public void StopGame()
        {
            Debug.Log("[NQMC] 停止游戏");
            gameStarted = false;
            isMyTurn = false;
            isWaitingForNetworkQuestion = false;

            if (timerManager != null)
                timerManager.StopTimer();

            if (manager != null)
            {
                Destroy(manager.gameObject);
                manager = null;
            }
        }

        /// <summary>
        /// 暂停游戏（由HostGameManager调用）
        /// </summary>
        public void PauseGame()
        {
            Debug.Log("[NQMC] 暂停游戏");
            if (timerManager != null)
                timerManager.PauseTimer();
        }

        /// <summary>
        /// 恢复游戏（由HostGameManager调用）
        /// </summary>
        public void ResumeGame()
        {
            Debug.Log("[NQMC] 恢复游戏");
            if (timerManager != null)
                timerManager.ResumeTimer();
        }

        /// <summary>
        /// 强制开始下一题（由HostGameManager调用）
        /// </summary>
        public void ForceNextQuestion()
        {
            if (!gameStarted)
            {
                Debug.LogWarning("[NQMC] 游戏未开始，无法强制下一题");
                return;
            }

            Debug.Log("[NQMC] 强制开始下一题");
            LoadNextQuestion();
        }

        #endregion

        #region 内部题目管理逻辑

        private IEnumerator DelayedFirstQuestion()
        {
            yield return null;
            LoadNextQuestion();
        }

        private void LoadNextQuestion()
        {
            if (!gameStarted)
            {
                Debug.Log("[NQMC] 游戏已停止，不加载新题目");
                return;
            }

            // 清理当前题目管理器
            if (manager != null)
            {
                Destroy(manager.gameObject);
                manager = null;
            }

            if (isMultiplayerMode)
            {
                if (isMyTurn && NetworkManager.Instance?.IsConnected == true)
                {
                    // 网络模式：请求服务器分配题目
                    RequestNetworkQuestion();
                }
                else
                {
                    Debug.Log("[NQMC] 不是我的回合或未连接服务器，等待...");
                }
            }
            else
            {
                // 单机模式：使用原有逻辑
                LoadLocalQuestion();
            }
        }

        private void LoadLocalQuestion()
        {
            var selectedType = SelectRandomTypeByWeight();
            manager = CreateManager(selectedType);

            if (manager != null)
            {
                if (hpManager != null)
                    hpManager.BindManager(manager);

                manager.OnAnswerResult += HandleAnswerResult;
                StartCoroutine(DelayedLoadQuestion());
            }
            else
            {
                Debug.LogError("[NQMC] 无法创建题目管理器");
                OnGameEnded?.Invoke(false);
            }
        }

        private void RequestNetworkQuestion()
        {
            if (NetworkManager.Instance?.IsConnected == true)
            {
                isWaitingForNetworkQuestion = true;
                NetworkManager.Instance.RequestQuestion();
                Debug.Log("[NQMC] 请求网络题目...");
            }
            else
            {
                Debug.LogError("[NQMC] 网络未连接，无法请求题目");
                OnGameEnded?.Invoke(false);
            }
        }

        private void LoadNetworkQuestion(NetworkQuestionData networkQuestion)
        {
            if (networkQuestion == null)
            {
                Debug.LogError("[NQMC] 网络题目数据为空");
                return;
            }

            currentNetworkQuestion = networkQuestion;
            manager = CreateManager(networkQuestion.questionType);

            if (manager != null)
            {
                if (hpManager != null)
                    hpManager.BindManager(manager);

                manager.OnAnswerResult += HandleNetworkAnswerResult;
                StartCoroutine(DelayedLoadQuestion());
            }
            else
            {
                Debug.LogError("[NQMC] 无法为网络题目创建管理器");
            }
        }

        private IEnumerator DelayedLoadQuestion()
        {
            yield return null;
            if (manager != null)
            {
                manager.LoadQuestion();
                if (timerManager != null)
                    timerManager.StartTimer();
            }
        }

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

        private QuestionManagerBase CreateManager(QuestionType type)
        {
            switch (type)
            {
                //case QuestionType.HandWriting: return gameObject.AddComponent<HandWritingQuestionManager>();
                case QuestionType.IdiomChain: return gameObject.AddComponent<IdiomChainQuestionManager>();
                case QuestionType.TextPinyin: return gameObject.AddComponent<TextPinyinQuestionManager>();
                case QuestionType.HardFill: return gameObject.AddComponent<HardFillQuestionManager>();
                case QuestionType.SoftFill: return gameObject.AddComponent<SoftFillQuestionManager>();
                //case QuestionType.AbbrFill: return gameObject.AddComponent<AbbrFillQuestionManager>();
                case QuestionType.SentimentTorF: return gameObject.AddComponent<SentimentTorFQuestionManager>();
                case QuestionType.SimularWordChoice: return gameObject.AddComponent<SimularWordChoiceQuestionManager>();
                case QuestionType.UsageTorF: return gameObject.AddComponent<UsageTorFQuestionManager>();
                case QuestionType.ExplanationChoice: return gameObject.AddComponent<ExplanationChoiceQuestionManager>();
                default:
                    Debug.LogError($"[NQMC] 未实现的题型：{type}");
                    return null;
            }
        }

        #endregion

        #region 答题结果处理

        private void HandleAnswerResult(bool isCorrect)
        {
            Debug.Log($"[NQMC] 单机模式答题结果: {isCorrect}");

            if (timerManager != null)
                timerManager.StopTimer();

            if (!isCorrect && hpManager != null)
            {
                hpManager.HPHandleAnswerResult(false);

                // 检查是否游戏结束
                if (hpManager.CurrentHealth <= 0)
                {
                    Debug.Log("[NQMC] 血量归零，游戏结束");
                    OnGameEnded?.Invoke(false);
                    return;
                }
            }

            OnAnswerCompleted?.Invoke(isCorrect);
            Invoke(nameof(LoadNextQuestion), timeUpDelay);
        }

        private void HandleNetworkAnswerResult(bool isCorrect)
        {
            Debug.Log($"[NQMC] 网络模式本地答题，等待服务器确认");
            // 网络模式下，本地答题结果不直接处理，等待服务器结果
            if (timerManager != null)
                timerManager.StopTimer();
        }

        private void HandleTimeUp()
        {
            Debug.Log("[NQMC] 答题超时");

            if (timerManager != null)
                timerManager.StopTimer();

            if (isMultiplayerMode)
            {
                // 网络模式：提交空答案表示超时
                if (isMyTurn && NetworkManager.Instance?.IsConnected == true)
                {
                    NetworkManager.Instance.SubmitAnswer("");
                }
            }
            else
            {
                // 单机模式：直接处理超时
                if (manager != null)
                    manager.OnAnswerResult?.Invoke(false);
                else
                    Invoke(nameof(LoadNextQuestion), timeUpDelay);
            }
        }

        #endregion

        #region 网络事件处理

        private void OnNetworkQuestionReceived(NetworkQuestionData question)
        {
            if (!isMultiplayerMode || !isWaitingForNetworkQuestion)
                return;

            Debug.Log($"[NQMC] 收到网络题目: {question.questionType}");
            isWaitingForNetworkQuestion = false;
            LoadNetworkQuestion(question);
        }

        private void OnNetworkAnswerResult(bool isCorrect, string correctAnswer)
        {
            if (!isMultiplayerMode)
                return;

            Debug.Log($"[NQMC] 收到服务器答题结果: {(isCorrect ? "正确" : "错误")}");

            // 处理血量变化
            if (!isCorrect && hpManager != null)
            {
                hpManager.HPHandleAnswerResult(false);

                // 检查是否游戏结束
                if (hpManager.CurrentHealth <= 0)
                {
                    Debug.Log("[NQMC] 血量归零，游戏结束");
                    OnGameEnded?.Invoke(false);
                    return;
                }
            }

            // 显示结果反馈
            ShowAnswerFeedback(isCorrect, correctAnswer);
            OnAnswerCompleted?.Invoke(isCorrect);
        }

        private void OnNetworkPlayerTurnChanged(ushort playerId)
        {
            if (!isMultiplayerMode)
                return;

            bool wasMyTurn = isMyTurn;
            isMyTurn = (playerId == NetworkManager.Instance?.ClientId);

            Debug.Log($"[NQMC] 回合变更: {(isMyTurn ? "轮到我了" : $"轮到玩家{playerId}")}");

            if (isMyTurn && !wasMyTurn)
            {
                // 轮到我答题
                LoadNextQuestion();
            }
            else if (!isMyTurn && wasMyTurn)
            {
                // 不再是我的回合，停止当前题目
                if (timerManager != null)
                    timerManager.StopTimer();
            }
        }

        private void OnNetworkDisconnected()
        {
            if (isMultiplayerMode && gameStarted)
            {
                Debug.LogWarning("[NQMC] 网络断开，游戏将结束");

                if (timerManager != null)
                    timerManager.StopTimer();

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
                Debug.Log($"[NQMC] 提交网络答案: {answer}");
            }
            else if (!isMultiplayerMode && manager != null)
            {
                manager.CheckAnswer(answer);
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

        private void ShowAnswerFeedback(bool isCorrect, string correctAnswer)
        {
            string feedback = isCorrect ? "回答正确！" : $"回答错误，正确答案是：{correctAnswer}";
            Debug.Log($"[NQMC] 答题反馈: {feedback}");

            // 这里可以通过UI系统显示反馈
            // 或者让当前的题目管理器显示反馈
            if (manager != null)
            {
                // 如果题目管理器支持显示网络结果，可以调用相应方法
                // 例如: manager.ShowNetworkResult(isCorrect, correctAnswer);
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
            info += $"当前管理器: {(manager != null ? manager.GetType().Name : "无")}\n";

            if (hpManager != null)
                info += $"当前血量: {hpManager.CurrentHealth}\n";

            if (timerManager != null)
                info += $"计时器状态: {(timerManager.IsRunning ? "运行中" : "已停止")}\n";

            return info;
        }

        #endregion
    }
}