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
    /// 扩展原有的QuestionManagerController，支持单机和多人模式
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

        // 属性
        public bool IsMultiplayerMode => isMultiplayerMode;
        public bool IsMyTurn => isMyTurn;
        public bool IsGameStarted => gameStarted;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            InitializeComponents();
            RegisterNetworkEvents();
        }

        private void OnDestroy()
        {
            UnregisterNetworkEvents();
        }

        private void InitializeComponents()
        {
            // 获取或添加必要组件
            if (timerManager == null)
                timerManager = GetComponent<TimerManager>() ?? gameObject.AddComponent<TimerManager>();
            if (hpManager == null)
                hpManager = GetComponent<PlayerHealthManager>() ?? gameObject.AddComponent<PlayerHealthManager>();

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

        /// <summary>
        /// 开始游戏
        /// </summary>
        /// <param name="multiplayerMode">是否为多人模式</param>
        public void StartGame(bool multiplayerMode = false)
        {
            isMultiplayerMode = multiplayerMode;
            gameStarted = true;

            Debug.Log($"开始游戏 - 模式: {(isMultiplayerMode ? "多人" : "单机")}");

            if (isMultiplayerMode)
            {
                if (NetworkManager.Instance?.IsConnected == true)
                {
                    Debug.Log("多人模式：等待服务器分配回合");
                    // 多人模式下等待服务器通知轮次
                    isWaitingForNetworkQuestion = true;
                }
                else
                {
                    Debug.LogError("未连接到服务器，无法开始多人游戏");
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
        /// 停止游戏
        /// </summary>
        public void StopGame()
        {
            gameStarted = false;
            isMyTurn = false;
            isWaitingForNetworkQuestion = false;

            timerManager.StopTimer();

            if (manager != null)
            {
                Destroy(manager.gameObject);
                manager = null;
            }

            Debug.Log("游戏已停止");
        }

        private IEnumerator DelayedFirstQuestion()
        {
            yield return null;
            LoadNextQuestion();
        }

        private void LoadNextQuestion()
        {
            if (!gameStarted)
                return;

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
                    Debug.Log("不是我的回合或未连接服务器，等待...");
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
                hpManager.BindManager(manager);
                manager.OnAnswerResult += HandleAnswerResult;
                StartCoroutine(DelayedLoadQuestion());
            }
        }

        private void RequestNetworkQuestion()
        {
            if (NetworkManager.Instance?.IsConnected == true)
            {
                isWaitingForNetworkQuestion = true;
                NetworkManager.Instance.RequestQuestion();
                Debug.Log("请求网络题目...");
            }
        }

        private void LoadNetworkQuestion(NetworkQuestionData networkQuestion)
        {
            if (networkQuestion == null)
                return;

            currentNetworkQuestion = networkQuestion;
            manager = CreateManager(networkQuestion.questionType);

            if (manager != null)
            {
                hpManager.BindManager(manager);
                manager.OnAnswerResult += HandleNetworkAnswerResult;

                // 网络模式下，答案检查由服务器处理，所以我们修改回调
                StartCoroutine(DelayedLoadQuestion());
            }
        }

        private IEnumerator DelayedLoadQuestion()
        {
            yield return null;
            if (manager != null)
            {
                manager.LoadQuestion();
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
                    Debug.LogError("未实现的题型：" + type);
                    return null;
            }
        }

        #region 答题结果处理

        private void HandleAnswerResult(bool isCorrect)
        {
            Debug.Log($"[NetworkQMC] 单机模式答题结果: {isCorrect}");
            timerManager.StopTimer();

            if (!isCorrect)
                hpManager.HPHandleAnswerResult(false);

            Invoke(nameof(LoadNextQuestion), timeUpDelay);
        }

        private void HandleNetworkAnswerResult(bool isCorrect)
        {
            Debug.Log($"[NetworkQMC] 网络模式本地答题，等待服务器确认");
            // 网络模式下，本地答题结果不直接处理，等待服务器结果
            timerManager.StopTimer();
        }

        private void HandleTimeUp()
        {
            Debug.Log("[NetworkQMC] 答题超时");
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
                Invoke(nameof(LoadNextQuestion), timeUpDelay);
            }
        }

        #endregion

        #region 网络事件处理

        private void OnNetworkQuestionReceived(NetworkQuestionData question)
        {
            if (!isMultiplayerMode || !isWaitingForNetworkQuestion)
                return;

            Debug.Log($"收到网络题目: {question.questionType}");
            isWaitingForNetworkQuestion = false;
            LoadNetworkQuestion(question);
        }

        private void OnNetworkAnswerResult(bool isCorrect, string correctAnswer)
        {
            if (!isMultiplayerMode)
                return;

            Debug.Log($"收到服务器答题结果: {(isCorrect ? "正确" : "错误")}");

            // 处理血量变化
            if (!isCorrect)
                hpManager.HPHandleAnswerResult(false);

            // 显示结果反馈
            ShowAnswerFeedback(isCorrect, correctAnswer);
        }

        private void OnNetworkPlayerTurnChanged(ushort playerId)
        {
            if (!isMultiplayerMode)
                return;

            bool wasMyTurn = isMyTurn;
            isMyTurn = (playerId == NetworkManager.Instance?.ClientId);

            Debug.Log($"回合变更: {(isMyTurn ? "轮到我了" : $"轮到玩家{playerId}")}");

            if (isMyTurn && !wasMyTurn)
            {
                // 轮到我答题
                LoadNextQuestion();
            }
            else if (!isMyTurn && wasMyTurn)
            {
                // 不再是我的回合，停止当前题目
                timerManager.StopTimer();
            }
        }

        private void OnNetworkDisconnected()
        {
            if (isMultiplayerMode && gameStarted)
            {
                Debug.LogWarning("网络断开，游戏将暂停");
                timerManager.StopTimer();

                // 可以选择切换到单机模式或者暂停游戏
                // 这里暂停游戏，等待用户选择
                StopGame();
            }
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 提交答案（供题目管理器调用）
        /// </summary>
        public void SubmitAnswer(string answer)
        {
            if (isMultiplayerMode && isMyTurn && NetworkManager.Instance?.IsConnected == true)
            {
                NetworkManager.Instance.SubmitAnswer(answer);
                Debug.Log($"提交网络答案: {answer}");
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
            Debug.Log($"答题反馈: {feedback}");

            // 这里可以通过UI系统显示反馈
            // 或者让当前的题目管理器显示反馈
        }

        #endregion
    }
}