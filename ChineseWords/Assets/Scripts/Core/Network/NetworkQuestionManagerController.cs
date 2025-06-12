using UnityEngine;
using Core;
using Core.Network;
using System.Collections;
using Managers;
using Photon.Pun;

namespace Core.Network
{
    /// <summary>
    /// 网络题目管理控制器 - 纯多人模式版
    /// 专门用于多人模式的题目管理 + 网络消息处理 + 答案提交
    /// 已移除单机模式，专注于Photon多人游戏流程
    /// </summary>
    public class NetworkQuestionManagerController : MonoBehaviourPun
    {
        [Header("依赖管理器引用")]
        [SerializeField] private TimerManager timerManager;

        [Header("依赖配置")]
        [SerializeField] private float timeUpDelay = 1f;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        public static NetworkQuestionManagerController Instance { get; private set; }

        // 当前状态
        private QuestionManagerBase currentManager;
        private NetworkQuestionData currentNetworkQuestion;
        private bool isMyTurn = false;
        private bool gameStarted = false;
        private bool isInitialized = false;
        private ushort currentTurnPlayerId = 0;
        private bool hasReceivedTurnChange = false;

        // 事件
        public System.Action<bool> OnGameEnded;
        public System.Action<bool> OnAnswerCompleted;

        // 属性
        public bool IsMyTurn => isMyTurn;
        public bool IsGameStarted => gameStarted;
        public bool IsInitialized => isInitialized;
        public QuestionManagerBase CurrentManager => currentManager;

        // 状态管理枚举
        private enum QuestionDisplayState
        {
            WaitingForGame,     // 等待游戏开始
            MyTurn,            // 轮到我答题
            OtherPlayerTurn,   // 其他玩家回合
            GameEnded          // 游戏结束
        }

        private QuestionDisplayState currentDisplayState = QuestionDisplayState.WaitingForGame;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                LogDebug("NetworkQuestionManagerController (纯多人版) 单例已创建");
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            InitializeComponents();
        }

        private void Start()
        {
            isInitialized = true;
            LogDebug("纯多人版NQMC初始化完成，等待外部启动指令");
        }

        private void Update()
        {
            // 监听网络状态变化
            if (gameStarted)
            {
                if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom)
                {
                    LogDebug("检测到Photon连接断开，停止游戏");
                    StopGame();
                    OnGameEnded?.Invoke(false);
                }
            }
        }

        private void OnDestroy()
        {
            CleanupCurrentManager();

            if (Instance == this)
                Instance = null;

            LogDebug("NetworkQuestionManagerController (纯多人版) 已销毁");
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

            if (timerManager == null)
            {
                Debug.LogError("[NQMC] 找不到TimerManager引用");
                return;
            }


            // 应用配置
            var cfg = ConfigManager.Instance?.Config;
            if (cfg != null)
            {
                timerManager.ApplyConfig(cfg.timeLimit);
            }

            // 绑定计时器事件
            timerManager.OnTimeUp += HandleTimeUp;

            LogDebug("引用组件初始化完成");
        }

        #region 公共接口 - 用于外部系统调用

        /// <summary>
        /// 开始多人游戏（用于外部系统调用）
        /// </summary>
        public void StartGame(bool multiplayerMode = true)
        {
            if (!isInitialized)
            {
                Debug.LogError("[NQMC] 引用未初始化，无法开始游戏");
                return;
            }

            // 参数已忽略，永远是多人模式
            gameStarted = true;
            LogDebug("开始多人游戏");

            StartMultiplayerGame();
        }

        /// <summary>
        /// 停止游戏 - 重置状态
        /// </summary>
        public void StopGame()
        {
            LogDebug("停止游戏");
            gameStarted = false;
            isMyTurn = false;

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

        #endregion

        #region 多人游戏流程

        /// <summary>
        /// 开始多人游戏
        /// </summary>
        private void StartMultiplayerGame()
        {
            if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
            {
                LogDebug("多人模式：等待HostGameManager分配回合和发送题目");
                currentDisplayState = QuestionDisplayState.WaitingForGame;
                hasReceivedTurnChange = false;
            }
            else
            {
                Debug.LogError("[NQMC] 未连接到Photon房间，无法开始多人游戏");
                OnGameEnded?.Invoke(false);
            }
        }

        #endregion

        #region 网络题目管理

        /// <summary>
        /// 加载网络题目 - 我的回合
        /// </summary>
        private void LoadNetworkQuestion(NetworkQuestionData networkQuestion)
        {
            if (networkQuestion == null)
            {
                Debug.LogError("[NQMC] 网络题目数据为空");
                return;
            }

            LogDebug($"加载网络题目: {networkQuestion.questionType}");

            CleanupCurrentManager();
            currentNetworkQuestion = networkQuestion;

            currentManager = CreateQuestionManager(networkQuestion.questionType, true);

            if (currentManager != null)
            {

                currentManager.OnAnswerResult += HandleNetworkAnswerSubmission;

                StartCoroutine(DelayedLoadNetworkQuestion(networkQuestion));
                LogDebug($"网络题目管理器创建成功: {networkQuestion.questionType}");
            }
            else
            {
                Debug.LogError("[NQMC] 无法为网络题目创建管理器");
            }
        }

        /// <summary>
        /// 以观察者模式加载网络题目 - 其他玩家回合
        /// </summary>
        private void LoadNetworkQuestionAsObserver(NetworkQuestionData networkQuestion)
        {
            if (networkQuestion == null)
            {
                Debug.LogError("[NQMC] 网络题目数据为空");
                return;
            }

            LogDebug($"以观察者模式加载网络题目: {networkQuestion.questionType}");

            CleanupCurrentManager();
            currentNetworkQuestion = networkQuestion;

            currentManager = CreateQuestionManager(networkQuestion.questionType, true);

            if (currentManager != null)
            {
                // 观察者模式：不绑定答案提交事件
                LogDebug("观察者模式：不绑定答案提交事件");

                StartCoroutine(DelayedLoadNetworkQuestionAsObserver(networkQuestion));
                LogDebug($"观察者模式题目管理器创建成功: {networkQuestion.questionType}");
            }
            else
            {
                Debug.LogError("[NQMC] 无法为观察者模式创建管理器");
            }
        }

        /// <summary>
        /// 延迟加载网络题目 - 我的回合
        /// </summary>
        private IEnumerator DelayedLoadNetworkQuestion(NetworkQuestionData networkData)
        {
            yield return null;
            if (currentManager != null)
            {
                LoadQuestionWithNetworkData(networkData);
                StartTimerWithDynamicLimit(networkData.timeLimit);
                LogDebug($"网络题目已加载并开始计时，时间限制: {networkData.timeLimit}秒");
            }
        }

        /// <summary>
        /// 延迟加载网络题目（观察者模式）
        /// </summary>
        private IEnumerator DelayedLoadNetworkQuestionAsObserver(NetworkQuestionData networkData)
        {
            yield return null;
            if (currentManager != null)
            {
                LoadQuestionWithNetworkData(networkData);
                StartObserverTimer(networkData.timeLimit);
                LogDebug($"观察者模式：题目已加载，启动只读计时器");
            }
        }

        /// <summary>
        /// 使用网络数据加载题目
        /// </summary>
        private void LoadQuestionWithNetworkData(NetworkQuestionData networkData)
        {
            if (currentManager is NetworkQuestionManagerBase networkManager)
            {
                var loadMethod = networkManager.GetType().GetMethod("LoadNetworkQuestion",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (loadMethod != null)
                {
                    LogDebug($"通过反射调用网络题目加载方法: {networkData.questionType}");
                    loadMethod.Invoke(networkManager, new object[] { networkData });
                }
                else
                {
                    Debug.LogWarning($"[NQMC] 未找到LoadNetworkQuestion方法，使用备用加载方式");
                    currentManager.LoadQuestion();
                }
            }
            else
            {
                Debug.LogWarning($"[NQMC] 管理器不是NetworkQuestionManagerBase类型: {currentManager.GetType()}");
                currentManager.LoadQuestion();
            }
        }

        /// <summary>
        /// 使用动态时间限制启动计时器
        /// </summary>
        private void StartTimerWithDynamicLimit(float timeLimit)
        {
            if (timerManager != null)
            {
                var startTimerWithLimitMethod = timerManager.GetType().GetMethod("StartTimer", new System.Type[] { typeof(float) });
                var setTimeLimitMethod = timerManager.GetType().GetMethod("SetTimeLimit");

                if (startTimerWithLimitMethod != null)
                {
                    LogDebug($"使用动态时间限制启动计时器: {timeLimit}秒");
                    startTimerWithLimitMethod.Invoke(timerManager, new object[] { timeLimit });
                }
                else if (setTimeLimitMethod != null)
                {
                    LogDebug($"设置时间限制后启动计时器: {timeLimit}秒");
                    setTimeLimitMethod.Invoke(timerManager, new object[] { timeLimit });
                    timerManager.StartTimer();
                }
                else
                {
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
        /// 启动观察者计时器（只读模式）
        /// </summary>
        private void StartObserverTimer(float timeLimit)
        {
            if (timerManager != null)
            {
                var startReadOnlyMethod = timerManager.GetType().GetMethod("StartReadOnlyTimer");
                if (startReadOnlyMethod != null)
                {
                    LogDebug($"启动只读计时器: {timeLimit}秒");
                    startReadOnlyMethod.Invoke(timerManager, new object[] { timeLimit });
                }
                else
                {
                    LogDebug("TimerManager不支持只读模式，跳过计时器启动");
                }
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

                if (TimerConfigManager.Config != null)
                {
                    var setTempTimeMethod = typeof(TimerConfigManager).GetMethod("SetTemporaryTimeLimit");
                    if (setTempTimeMethod != null)
                    {
                        setTempTimeMethod.Invoke(null, new object[] { timeLimit });
                        LogDebug("通过TimerConfigManager设置临时时间限制成功");
                        return;
                    }

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
            GameObject managerObj = new GameObject($"{questionType}Manager");
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

                currentManager.OnAnswerResult -= HandleNetworkAnswerSubmission;
                QuestionManagerFactory.DestroyManager(currentManager);
                currentManager = null;
            }
        }

        #endregion

        #region 答题结果处理

        /// <summary>
        /// 处理网络答案提交
        /// </summary>
        private void HandleNetworkAnswerSubmission(bool isCorrect)
        {
            LogDebug("多人模式本地答题，等待服务器确认");

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

            if (isMyTurn && PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
            {
                if (NetworkManager.Instance != null)
                {
                    NetworkManager.Instance.SubmitAnswer("");
                    LogDebug("超时，提交空答案");
                }
            }
        }

        #endregion

        #region 网络事件处理 - 由NetworkManager反射调用

        /// <summary>
        /// 接收到网络题目
        /// </summary>
        public void OnNetworkQuestionReceived(NetworkQuestionData question)
        {
            LogDebug($"收到网络题目: {question.questionType}, 时间限制: {question.timeLimit}秒");
            LogDebug($"当前状态: 我的回合={isMyTurn}, 回合ID={currentTurnPlayerId}");

            switch (currentDisplayState)
            {
                case QuestionDisplayState.MyTurn:
                    LogDebug("轮到我答题，完整加载题目");
                    LoadNetworkQuestion(question);
                    break;

                case QuestionDisplayState.OtherPlayerTurn:
                    LogDebug($"其他玩家回合，显示题目但不可交互 (当前回合玩家: {currentTurnPlayerId})");
                    LoadNetworkQuestionAsObserver(question);
                    break;

                case QuestionDisplayState.WaitingForGame:
                    LogDebug("收到题目但游戏状态为等待中，检查回合状态");
                    if (hasReceivedTurnChange && isMyTurn)
                    {
                        LogDebug("回合状态确认为我的回合，加载题目");
                        currentDisplayState = QuestionDisplayState.MyTurn;
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
        public void OnNetworkAnswerResult(bool isCorrect, string correctAnswer)
        {
            LogDebug($"收到服务器答题结果: {(isCorrect ? "正确" : "错误")}");

            ShowAnswerFeedback(isCorrect, correctAnswer);
            OnAnswerCompleted?.Invoke(isCorrect);
        }

        /// <summary>
        /// 玩家回合变更
        /// </summary>
        public void OnNetworkPlayerTurnChanged(ushort playerId)
        {
            bool wasMyTurn = isMyTurn;
            currentTurnPlayerId = playerId;
            isMyTurn = (playerId == PhotonNetwork.LocalPlayer.ActorNumber);
            hasReceivedTurnChange = true;

            LogDebug($"回合变更: {(isMyTurn ? "轮到我了" : $"轮到玩家{playerId}")}");

            if (isMyTurn)
            {
                currentDisplayState = QuestionDisplayState.MyTurn;
                LogDebug("状态变更为：MyTurn");

                if (!wasMyTurn)
                {
                    LogDebug("轮到我答题，等待HostGameManager发送题目");
                }
            }
            else
            {
                currentDisplayState = QuestionDisplayState.OtherPlayerTurn;
                LogDebug($"状态变更为：OtherPlayerTurn (玩家{playerId})");

                if (wasMyTurn)
                {
                    StopTimer();
                }
            }
        }

        #endregion

        #region 题目管理器接口

        /// <summary>
        /// 提交答案
        /// </summary>
        public void SubmitAnswer(string answer)
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

            if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom && NetworkManager.Instance != null)
            {
                NetworkManager.Instance.SubmitAnswer(answer);
                LogDebug($"提交网络答案: {answer}");
            }
            else
            {
                LogDebug("网络未连接或NetworkManager不可用，无法提交答案");
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
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[NQMC-Pure] {message}");
            }
        }

        #endregion

        #region 状态查询和调试

        /// <summary>
        /// 获取状态信息
        /// </summary>
        public string GetStatusInfo()
        {
            var info = "=== NQMC (纯多人版) 状态 ===\n";
            info += $"已初始化: {isInitialized}\n";
            info += $"游戏已开始: {gameStarted}\n";
            info += $"我的回合: {isMyTurn}\n";
            info += $"显示状态: {currentDisplayState}\n";
            info += $"当前回合玩家ID: {currentTurnPlayerId}\n";
            info += $"已收到回合变更: {hasReceivedTurnChange}\n";
            info += $"当前管理器: {(currentManager != null ? currentManager.GetType().Name : "无")}\n";

            // Photon网络状态
            info += $"Photon连接: {PhotonNetwork.IsConnected}\n";
            info += $"在房间中: {PhotonNetwork.InRoom}\n";
            info += $"我的ActorNumber: {(PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0)}\n";
            info += $"是否MasterClient: {PhotonNetwork.IsMasterClient}\n";

            if (timerManager != null)
                info += $"计时器状态: {(timerManager.IsRunning ? "运行中" : "已停止")}\n";

            return info;
        }

        /// <summary>
        /// 重置状态（调试用）
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
            }
        }

        /// <summary>
        /// 显示当前状态（调试用）
        /// </summary>
        [ContextMenu("显示当前状态")]
        public void ShowCurrentStatus()
        {
            if (Application.isPlaying)
            {
                Debug.Log(GetStatusInfo());
            }
        }

        #endregion
    }
}