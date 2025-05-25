using UnityEngine;
using Riptide;
using Core.Network;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UI;

namespace Core.Network
{
    /// <summary>
    /// 主机游戏管理器
    /// 只在Host模式下激活，负责游戏逻辑控制
    /// 使用事件驱动的初始化方式，解决脚本执行顺序问题
    /// </summary>
    public class HostGameManager : MonoBehaviour
    {
        [Header("游戏配置")]
        [SerializeField] private float questionTimeLimit = 30f;
        [SerializeField] private int initialPlayerHealth = 100;
        [SerializeField] private int damagePerWrongAnswer = 20;
        [SerializeField] private bool autoStartGame = true;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        public static HostGameManager Instance { get; private set; }

        // 初始化状态
        private bool isInitialized = false;
        private bool isWaitingForNetworkManager = false;

        // 游戏状态
        private Dictionary<ushort, PlayerGameState> playerStates;
        private ushort currentTurnPlayerId;
        private bool gameInProgress;
        private NetworkQuestionData currentQuestion;
        private float gameStartDelay = 2f;

        // 题目生成
        private NetworkQuestionManagerController questionController;

        // 属性
        public bool IsGameInProgress => gameInProgress;
        public int PlayerCount => playerStates?.Count ?? 0;
        public ushort CurrentTurnPlayer => currentTurnPlayerId;
        public bool IsInitialized => isInitialized;

        private void Awake()
        {
            LogDebug($"Awake 执行时间: {Time.time}");

            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LogDebug("HostGameManager 单例已创建");
            }
            else
            {
                LogDebug("销毁重复的HostGameManager实例");
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            LogDebug($"Start 执行时间: {Time.time}");

            // 检查是否应该激活（基于选定的游戏模式）
            if (MainMenuManager.SelectedGameMode != MainMenuManager.GameMode.Host)
            {
                LogDebug($"当前游戏模式: {MainMenuManager.SelectedGameMode}，非Host模式，禁用HostGameManager");
                this.enabled = false;
                return;
            }

            // 开始初始化流程
            StartCoroutine(InitializeHostManagerCoroutine());
        }

        /// <summary>
        /// 初始化主机管理器的协程
        /// </summary>
        private IEnumerator InitializeHostManagerCoroutine()
        {
            LogDebug("开始初始化HostGameManager");

            // 1. 等待 NetworkManager 准备就绪
            yield return StartCoroutine(WaitForNetworkManager());

            // 2. 检查网络状态并订阅事件
            yield return StartCoroutine(CheckNetworkStatusAndSubscribe());

            LogDebug("HostGameManager 初始化流程完成");
        }

        /// <summary>
        /// 等待 NetworkManager 实例准备就绪
        /// </summary>
        private IEnumerator WaitForNetworkManager()
        {
            LogDebug("等待 NetworkManager 实例准备就绪...");
            isWaitingForNetworkManager = true;

            int waitFrames = 0;
            const int maxWaitFrames = 300; // 5秒超时（60fps）

            while (NetworkManager.Instance == null && waitFrames < maxWaitFrames)
            {
                yield return null;
                waitFrames++;
            }

            isWaitingForNetworkManager = false;

            if (NetworkManager.Instance == null)
            {
                Debug.LogError("[HostGameManager] 等待 NetworkManager 超时，禁用 HostGameManager");
                this.enabled = false;
                yield break;
            }

            LogDebug($"NetworkManager 实例已准备就绪，等待了 {waitFrames} 帧");
        }

        /// <summary>
        /// 检查网络状态并订阅相关事件
        /// </summary>
        private IEnumerator CheckNetworkStatusAndSubscribe()
        {
            LogDebug($"NetworkManager 状态检查 - IsHost: {NetworkManager.Instance.IsHost}, IsConnected: {NetworkManager.Instance.IsConnected}");

            if (NetworkManager.Instance.IsHost)
            {
                // 已经是主机，直接初始化
                LogDebug("检测到已是Host模式，立即初始化");
                yield return StartCoroutine(InitializeHostGameCoroutine());
            }
            else
            {
                // 还不是主机，订阅主机启动事件
                LogDebug("等待主机启动事件...");
                SubscribeToNetworkEvents();

                // 设置超时检查
                StartCoroutine(HostStartTimeoutCheck());
            }
        }

        /// <summary>
        /// 订阅网络事件
        /// </summary>
        private void SubscribeToNetworkEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnHostStarted += OnHostStarted;
                NetworkManager.OnPlayerJoined += OnPlayerJoined;
                NetworkManager.OnPlayerLeft += OnPlayerLeft;
                LogDebug("已订阅网络事件");
            }
        }

        /// <summary>
        /// 取消订阅网络事件
        /// </summary>
        private void UnsubscribeFromNetworkEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnHostStarted -= OnHostStarted;
                NetworkManager.OnPlayerJoined -= OnPlayerJoined;
                NetworkManager.OnPlayerLeft -= OnPlayerLeft;
                LogDebug("已取消订阅网络事件");
            }
        }

        /// <summary>
        /// 主机启动超时检查
        /// </summary>
        private IEnumerator HostStartTimeoutCheck()
        {
            yield return new WaitForSeconds(10f); // 10秒超时

            if (!isInitialized && this.enabled)
            {
                Debug.LogWarning("[HostGameManager] 主机启动超时，可能不是Host模式，禁用HostGameManager");
                this.enabled = false;
            }
        }

        /// <summary>
        /// 主机启动事件处理
        /// </summary>
        private void OnHostStarted()
        {
            LogDebug("收到主机启动事件");

            if (!isInitialized)
            {
                StartCoroutine(InitializeHostGameCoroutine());
            }
        }

        /// <summary>
        /// 初始化主机游戏的协程
        /// </summary>
        private IEnumerator InitializeHostGameCoroutine()
        {
            if (isInitialized)
            {
                LogDebug("HostGameManager 已经初始化，跳过重复初始化");
                yield break;
            }

            LogDebug("开始初始化主机游戏逻辑");

            // 初始化游戏状态
            playerStates = new Dictionary<ushort, PlayerGameState>();
            gameInProgress = false;
            currentQuestion = null;

            // 查找题目控制器
            yield return StartCoroutine(FindQuestionController());

            if (questionController == null)
            {
                Debug.LogError("[HostGameManager] 未找到NetworkQuestionManagerController，初始化失败");
                this.enabled = false;
                yield break;
            }

            // 如果Host已经连接，立即添加
            if (NetworkManager.Instance.IsConnected)
            {
                AddPlayer(NetworkManager.Instance.ClientId, "房主");
            }

            // 标记为已初始化
            isInitialized = true;
            LogDebug("主机游戏初始化完成");

            // 检查游戏开始条件
            CheckGameStartConditions();
        }

        /// <summary>
        /// 查找题目控制器
        /// </summary>
        private IEnumerator FindQuestionController()
        {
            LogDebug("查找 NetworkQuestionManagerController...");

            int attempts = 0;
            const int maxAttempts = 10;

            while (questionController == null && attempts < maxAttempts)
            {
                questionController = FindObjectOfType<NetworkQuestionManagerController>();

                if (questionController == null)
                {
                    LogDebug($"第 {attempts + 1} 次查找 NetworkQuestionManagerController 失败，继续尝试...");
                    yield return new WaitForSeconds(0.5f);
                    attempts++;
                }
            }

            if (questionController != null)
            {
                LogDebug("NetworkQuestionManagerController 找到");
            }
            else
            {
                Debug.LogError("[HostGameManager] 查找 NetworkQuestionManagerController 失败");
            }
        }

        /// <summary>
        /// 判断是否应该激活Host管理器（保留原有逻辑）
        /// </summary>
        private bool ShouldActivateHostManager()
        {
            bool shouldActivate = MainMenuManager.SelectedGameMode == MainMenuManager.GameMode.Host &&
                                 NetworkManager.Instance?.IsHost == true;

            LogDebug($"ShouldActivateHostManager: {shouldActivate} (GameMode: {MainMenuManager.SelectedGameMode}, IsHost: {NetworkManager.Instance?.IsHost})");

            return shouldActivate;
        }

        /// <summary>
        /// 添加玩家
        /// </summary>
        private void AddPlayer(ushort playerId, string playerName = null)
        {
            if (!isInitialized)
            {
                LogDebug($"HostGameManager 未初始化，延迟添加玩家 {playerId}");
                StartCoroutine(DelayedAddPlayer(playerId, playerName));
                return;
            }

            if (playerStates.ContainsKey(playerId))
            {
                LogDebug($"玩家 {playerId} 已存在，跳过添加");
                return;
            }

            playerStates[playerId] = new PlayerGameState
            {
                playerId = playerId,
                playerName = playerName ?? $"玩家{playerId}",
                health = initialPlayerHealth,
                isAlive = true,
                score = 0,
                isReady = false
            };

            LogDebug($"添加玩家: {playerId} ({playerName}), 当前玩家数: {playerStates.Count}");

            // 检查是否可以开始游戏
            CheckGameStartConditions();
        }

        /// <summary>
        /// 延迟添加玩家（等待初始化完成）
        /// </summary>
        private IEnumerator DelayedAddPlayer(ushort playerId, string playerName)
        {
            while (!isInitialized)
            {
                yield return new WaitForSeconds(0.1f);
            }

            AddPlayer(playerId, playerName);
        }

        /// <summary>
        /// 移除玩家
        /// </summary>
        private void RemovePlayer(ushort playerId)
        {
            if (!isInitialized || !playerStates.ContainsKey(playerId))
                return;

            string playerName = playerStates[playerId].playerName;
            playerStates.Remove(playerId);

            LogDebug($"移除玩家: {playerId} ({playerName}), 剩余玩家数: {playerStates.Count}");

            // 如果当前回合的玩家离开，切换到下一个玩家
            if (currentTurnPlayerId == playerId && gameInProgress)
            {
                NextPlayerTurn();
            }

            // 检查游戏是否应该结束
            CheckGameEndConditions();
        }

        /// <summary>
        /// 检查游戏开始条件
        /// </summary>
        private void CheckGameStartConditions()
        {
            if (!isInitialized || gameInProgress || !autoStartGame)
                return;

            // 至少需要1个玩家才能开始游戏
            if (playerStates.Count >= 1)
            {
                LogDebug("满足游戏开始条件，准备开始游戏");
                Invoke(nameof(StartHostGame), gameStartDelay);
            }
        }

        /// <summary>
        /// 检查游戏结束条件
        /// </summary>
        private void CheckGameEndConditions()
        {
            if (!gameInProgress)
                return;

            var alivePlayers = playerStates.Where(p => p.Value.isAlive).ToList();

            if (alivePlayers.Count == 0)
            {
                LogDebug("游戏结束：没有存活的玩家");
                EndGame("游戏结束：所有玩家都被淘汰");
            }
            else if (alivePlayers.Count == 1)
            {
                var winner = alivePlayers.First();
                LogDebug($"游戏结束：玩家 {winner.Key} 获胜");
                EndGame($"游戏结束：{winner.Value.playerName} 获胜！");
            }
            else if (playerStates.Count == 0)
            {
                LogDebug("游戏结束：没有玩家");
                EndGame("游戏结束：所有玩家都离开了");
            }
        }

        /// <summary>
        /// 开始主机游戏
        /// </summary>
        public void StartHostGame()
        {
            if (!isInitialized)
            {
                LogDebug("HostGameManager 未初始化，无法开始游戏");
                return;
            }

            if (gameInProgress)
            {
                LogDebug("游戏已经在进行中");
                return;
            }

            LogDebug("主机开始游戏");
            gameInProgress = true;

            // 选择第一个玩家开始
            var firstPlayer = playerStates.Keys.FirstOrDefault();
            if (firstPlayer != 0)
            {
                currentTurnPlayerId = firstPlayer;
                BroadcastPlayerTurnChanged(currentTurnPlayerId);

                // 生成第一题
                Invoke(nameof(GenerateAndSendQuestion), 1f);
            }
            else
            {
                Debug.LogError("没有玩家可以开始游戏");
                gameInProgress = false;
            }
        }

        /// <summary>
        /// 结束游戏
        /// </summary>
        public void EndGame(string reason = "游戏结束")
        {
            if (!gameInProgress)
                return;

            LogDebug($"结束游戏: {reason}");
            gameInProgress = false;
            currentQuestion = null;

            // 这里可以发送游戏结束消息给所有客户端
            // BroadcastGameEnd(reason);
        }

        /// <summary>
        /// 生成并发送题目
        /// </summary>
        private void GenerateAndSendQuestion()
        {
            if (!gameInProgress || !isInitialized)
                return;

            LogDebug("生成新题目");

            // 使用现有的题目生成逻辑（简化版演示）
            var questionType = SelectRandomQuestionType();
            currentQuestion = CreateSampleQuestion(questionType);

            if (currentQuestion != null)
            {
                // 广播题目给所有客户端
                BroadcastQuestion(currentQuestion);
                LogDebug($"题目已发送: {questionType}");
            }
            else
            {
                Debug.LogError("题目生成失败");
                // 可以重试或跳过
                Invoke(nameof(GenerateAndSendQuestion), 1f);
            }
        }

        /// <summary>
        /// 选择随机题目类型
        /// </summary>
        private QuestionType SelectRandomQuestionType()
        {
            // 复用NetworkQuestionManagerController的权重逻辑
            var weights = questionController?.TypeWeights ?? GetDefaultTypeWeights();

            float total = weights.Values.Sum();
            float random = Random.value * total;
            float accumulator = 0f;

            foreach (var pair in weights)
            {
                accumulator += pair.Value;
                if (random <= accumulator)
                    return pair.Key;
            }

            return weights.Keys.First();
        }

        /// <summary>
        /// 获取默认题目类型权重
        /// </summary>
        private Dictionary<QuestionType, float> GetDefaultTypeWeights()
        {
            return new Dictionary<QuestionType, float>
            {
                { QuestionType.ExplanationChoice, 1f },
                { QuestionType.SimularWordChoice, 1f },
                { QuestionType.TextPinyin, 1f },
                { QuestionType.HardFill, 1f }
            };
        }

        /// <summary>
        /// 创建示例题目（临时实现，实际应该连接数据库）
        /// </summary>
        private NetworkQuestionData CreateSampleQuestion(QuestionType questionType)
        {
            return new NetworkQuestionData
            {
                questionType = questionType,
                questionText = $"示例{questionType}题目：请选择正确答案",
                correctAnswer = "正确答案",
                options = new string[] { "选项A", "正确答案", "选项C", "选项D" },
                timeLimit = questionTimeLimit,
                additionalData = "{\"sampleData\": true}"
            };
        }

        /// <summary>
        /// 处理玩家答案
        /// </summary>
        public void HandlePlayerAnswer(ushort playerId, string answer)
        {
            if (!isInitialized || !gameInProgress || playerId != currentTurnPlayerId || currentQuestion == null)
            {
                LogDebug($"无效的答案提交: playerId={playerId}, currentTurn={currentTurnPlayerId}, gameInProgress={gameInProgress}, initialized={isInitialized}");
                return;
            }

            LogDebug($"处理玩家 {playerId} 的答案: {answer}");

            // 验证答案
            bool isCorrect = ValidateAnswer(answer, currentQuestion);

            // 更新玩家状态
            UpdatePlayerState(playerId, isCorrect);

            // 广播答题结果
            BroadcastAnswerResult(isCorrect, currentQuestion.correctAnswer);

            // 检查游戏是否结束
            CheckGameEndConditions();

            // 如果游戏仍在进行，切换到下一个玩家
            if (gameInProgress)
            {
                Invoke(nameof(NextPlayerTurn), 2f);
            }
        }

        /// <summary>
        /// 更新玩家状态
        /// </summary>
        private void UpdatePlayerState(ushort playerId, bool isCorrect)
        {
            if (!playerStates.ContainsKey(playerId))
                return;

            var playerState = playerStates[playerId];

            if (isCorrect)
            {
                playerState.score += 10; // 答对得分
                LogDebug($"玩家 {playerId} 答对，得分: {playerState.score}");
            }
            else
            {
                playerState.health -= damagePerWrongAnswer;
                if (playerState.health <= 0)
                {
                    playerState.health = 0;
                    playerState.isAlive = false;
                    LogDebug($"玩家 {playerId} 被淘汰");
                }

                // 广播血量更新
                BroadcastHealthUpdate(playerId, playerState.health);
                LogDebug($"玩家 {playerId} 答错，血量: {playerState.health}");
            }
        }

        /// <summary>
        /// 验证答案
        /// </summary>
        private bool ValidateAnswer(string answer, NetworkQuestionData question)
        {
            if (question == null || string.IsNullOrEmpty(answer))
                return false;

            // 简单的字符串比较，实际可能需要更复杂的逻辑
            return answer.Trim().Equals(question.correctAnswer.Trim(), System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 切换到下一个玩家
        /// </summary>
        private void NextPlayerTurn()
        {
            var alivePlayers = playerStates.Where(p => p.Value.isAlive).Select(p => p.Key).ToList();

            if (alivePlayers.Count == 0)
            {
                LogDebug("没有存活的玩家，游戏结束");
                EndGame("没有存活的玩家");
                return;
            }

            if (alivePlayers.Count == 1)
            {
                var winner = playerStates[alivePlayers[0]];
                LogDebug($"游戏结束：{winner.playerName} 获胜");
                EndGame($"{winner.playerName} 获胜！");
                return;
            }

            // 找到下一个存活的玩家
            int currentIndex = alivePlayers.IndexOf(currentTurnPlayerId);
            if (currentIndex == -1)
            {
                // 当前玩家不在存活列表中，选择第一个存活的玩家
                currentTurnPlayerId = alivePlayers[0];
            }
            else
            {
                // 选择下一个存活的玩家
                int nextIndex = (currentIndex + 1) % alivePlayers.Count;
                currentTurnPlayerId = alivePlayers[nextIndex];
            }

            // 广播回合变更并生成新题目
            BroadcastPlayerTurnChanged(currentTurnPlayerId);

            // 短暂延迟后生成新题目
            Invoke(nameof(GenerateAndSendQuestion), 1f);
        }

        #region 网络消息广播

        /// <summary>
        /// 广播题目
        /// </summary>
        private void BroadcastQuestion(NetworkQuestionData question)
        {
            if (!isInitialized || NetworkManager.Instance == null)
                return;

            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.SendQuestion);
            message.AddBytes(question.Serialize());
            NetworkManager.Instance.BroadcastMessage(message);
        }

        /// <summary>
        /// 广播玩家回合变更
        /// </summary>
        private void BroadcastPlayerTurnChanged(ushort playerId)
        {
            if (!isInitialized || NetworkManager.Instance == null)
                return;

            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.PlayerTurnChanged);
            message.AddUShort(playerId);
            NetworkManager.Instance.BroadcastMessage(message);

            LogDebug($"广播回合变更: 轮到玩家 {playerId}");
        }

        /// <summary>
        /// 广播血量更新
        /// </summary>
        private void BroadcastHealthUpdate(ushort playerId, int newHealth)
        {
            if (!isInitialized || NetworkManager.Instance == null)
                return;

            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.HealthUpdate);
            message.AddUShort(playerId);
            message.AddInt(newHealth);
            NetworkManager.Instance.BroadcastMessage(message);
        }

        /// <summary>
        /// 广播答题结果
        /// </summary>
        private void BroadcastAnswerResult(bool isCorrect, string correctAnswer)
        {
            if (!isInitialized || NetworkManager.Instance == null)
                return;

            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.AnswerResult);
            message.AddBool(isCorrect);
            message.AddString(correctAnswer);
            NetworkManager.Instance.BroadcastMessage(message);
        }

        #endregion

        #region 网络事件处理

        private void OnPlayerJoined(ushort playerId)
        {
            LogDebug($"网络事件: 玩家 {playerId} 加入");
            AddPlayer(playerId);
        }

        private void OnPlayerLeft(ushort playerId)
        {
            LogDebug($"网络事件: 玩家 {playerId} 离开");
            RemovePlayer(playerId);
        }

        #endregion

        #region 公共接口和状态检查

        /// <summary>
        /// 强制重新初始化（用于调试）
        /// </summary>
        [ContextMenu("强制重新初始化")]
        public void ForceReinitialize()
        {
            if (Application.isPlaying)
            {
                LogDebug("强制重新初始化");
                isInitialized = false;
                StartCoroutine(InitializeHostManagerCoroutine());
            }
        }

        /// <summary>
        /// 获取当前状态信息（用于调试）
        /// </summary>
        public string GetStatusInfo()
        {
            return $"Initialized: {isInitialized}, " +
                   $"GameInProgress: {gameInProgress}, " +
                   $"PlayerCount: {PlayerCount}, " +
                   $"CurrentTurn: {currentTurnPlayerId}, " +
                   $"NetworkManager: {NetworkManager.Instance != null}, " +
                   $"IsHost: {NetworkManager.Instance?.IsHost}";
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[HostGameManager] {message}");
            }
        }

        #endregion

        #region Unity 生命周期

        private void OnDestroy()
        {
            LogDebug("HostGameManager 被销毁");
            UnsubscribeFromNetworkEvents();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && isInitialized)
            {
                LogDebug("应用暂停，暂停游戏逻辑");
            }
            else if (!pauseStatus && isInitialized)
            {
                LogDebug("应用恢复，恢复游戏逻辑");
            }
        }

        #endregion
    }

    /// <summary>
    /// 玩家游戏状态（扩展版）
    /// </summary>
    [System.Serializable]
    public class PlayerGameState
    {
        public ushort playerId;
        public string playerName;
        public int health;
        public bool isAlive;
        public int score;
        public bool isReady;
        public float lastActiveTime;

        public PlayerGameState()
        {
            lastActiveTime = Time.time;
        }
    }
}