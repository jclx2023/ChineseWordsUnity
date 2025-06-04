using UnityEngine;
using Riptide;
using Core.Network;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UI;
using GameLogic.FillBlank;

namespace Core.Network
{
    /// <summary>
    /// 重构后的Host游戏管理器
    /// 主要变化：使用独立的管理器模块，减少代码复杂度
    /// </summary>
    public class HostGameManager : MonoBehaviour
    {
        [Header("双层架构设置")]
        [SerializeField] private bool isDualLayerArchitecture = true;
        [SerializeField] private bool isServerLayerOnly = false;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        [Header("配置引用")]
        [SerializeField] private QuestionWeightConfig questionWeightConfig;
        [SerializeField] private TimerConfig timerConfig;
        [SerializeField] private HPConfig hpConfig;
        [SerializeField] private bool useCustomHPConfig = false;

        [Header("成语接龙设置")]
        [SerializeField] private bool enableIdiomChain = true;

        // 管理器实例
        private PlayerStateManager playerStateManager;
        private GameConfigManager gameConfigManager;
        private AnswerValidationManager answerValidationManager;
        private PlayerHPManager hpManager;

        // 双层架构相关
        private NetworkManager serverNetworkManager;
        private NetworkManager playerHostNetworkManager;

        public static HostGameManager Instance { get; private set; }

        // 简化的状态管理
        private bool isInitialized = false;
        private bool isWaitingForNetworkManager = false;
        private ushort currentTurnPlayerId;
        private bool gameInProgress;
        private NetworkQuestionData currentQuestion;
        private float gameStartDelay = 2f;

        // 服务依赖
        private QuestionDataService questionDataService;
        private NetworkQuestionManagerController questionController;

        // 成语接龙状态（保留在主类中，因为与游戏流程紧密相关）
        private bool isIdiomChainActive = false;
        private string currentIdiomChainWord = null;
        private int idiomChainCount = 0;
        private const int MAX_IDIOM_CHAIN = 10;

        // 玩家答案缓存（用于成语接龙）
        private Dictionary<ushort, string> playerAnswerCache = new Dictionary<ushort, string>();

        // 属性
        public bool IsGameInProgress => gameInProgress;
        public int PlayerCount => playerStateManager?.GetPlayerCount() ?? 0;
        public ushort CurrentTurnPlayer => currentTurnPlayerId;
        public bool IsInitialized => isInitialized;

        #region Unity生命周期

        private void Awake()
        {
            LogDebug($"Awake 执行时间: {Time.time}");

            if (Instance == null)
            {
                Instance = this;
                LogDebug("HostGameManager 单例已创建");
            }
            else
            {
                LogDebug("销毁重复的HostGameManager实例");
                Destroy(gameObject);
                return;
            }

            CheckDualLayerArchitecture();
        }

        private void Start()
        {
            LogDebug($"[HostGameManager]Start 执行时间: {Time.time}");

            if (MainMenuManager.SelectedGameMode != MainMenuManager.GameMode.Host)
            {
                LogDebug($"当前游戏模式: {MainMenuManager.SelectedGameMode}，非Host模式，禁用HostGameManager");
                this.enabled = false;
                return;
            }

            StartCoroutine(InitializeHostManagerCoroutine());
        }

        private void OnDestroy()
        {
            LogDebug("HostGameManager 被销毁");

            // 销毁管理器实例
            playerStateManager?.Dispose();
            gameConfigManager?.Dispose();
            answerValidationManager?.Dispose();
            hpManager?.Dispose();

            // 取消网络事件订阅
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

        #region 初始化流程

        /// <summary>
        /// 初始化主机管理器的协程
        /// </summary>
        private IEnumerator InitializeHostManagerCoroutine()
        {
            LogDebug("开始初始化HostGameManager");

            // 1. 等待 NetworkManager 准备就绪
            yield return StartCoroutine(WaitForNetworkManager());

            // 2. 初始化管理器模块
            yield return StartCoroutine(InitializeManagerModules());

            // 3. 检查网络状态并订阅事件
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
        /// 初始化管理器模块
        /// </summary>
        private IEnumerator InitializeManagerModules()
        {
            LogDebug("初始化管理器模块...");

            // 初始化玩家状态管理器
            try
            {
                playerStateManager = new PlayerStateManager();
                playerStateManager.Initialize(NetworkManager.Instance);

                // 绑定玩家状态事件
                playerStateManager.OnPlayerAdded += OnPlayerStateAdded;
                playerStateManager.OnPlayerRemoved += OnPlayerStateRemoved;
                playerStateManager.OnHostValidationFailed += OnHostValidationFailed;
                playerStateManager.OnHostValidationPassed += OnHostValidationPassed;

                LogDebug("PlayerStateManager 初始化完成");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HostGameManager] PlayerStateManager 初始化失败: {e.Message}");
                this.enabled = false;
                yield break;
            }

            // 初始化配置管理器
            try
            {
                gameConfigManager = new GameConfigManager();
                gameConfigManager.Initialize(timerConfig, questionWeightConfig);

                // 绑定配置变更事件
                gameConfigManager.OnConfigurationChanged += OnConfigurationChanged;

                LogDebug("GameConfigManager 初始化完成");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HostGameManager] GameConfigManager 初始化失败: {e.Message}");
                this.enabled = false;
                yield break;
            }

            // 初始化答案验证管理器
            try
            {
                answerValidationManager = new AnswerValidationManager();
                answerValidationManager.Initialize();

                // 绑定验证事件
                answerValidationManager.OnAnswerValidated += OnAnswerValidated;
                answerValidationManager.OnValidationError += OnValidationError;

                LogDebug("AnswerValidationManager 初始化完成");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HostGameManager] AnswerValidationManager 初始化失败: {e.Message}");
                this.enabled = false;
                yield break;
            }

            // 初始化HP管理器
            try
            {
                hpManager = new PlayerHPManager();
                hpManager.Initialize(hpConfig, useCustomHPConfig);

                // 绑定HP事件
                hpManager.OnHealthChanged += OnPlayerHealthChanged;
                hpManager.OnPlayerDied += OnPlayerDied;

                LogDebug("PlayerHPManager 初始化完成");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HostGameManager] PlayerHPManager 初始化失败: {e.Message}");
                this.enabled = false;
                yield break;
            }

            // 初始化服务依赖（协程部分）
            yield return StartCoroutine(InitializeServices());

            LogDebug("管理器模块初始化完成");
        }

        /// 检查网络状态并订阅相关事件
        private IEnumerator CheckNetworkStatusAndSubscribe()
        {
            LogDebug($"NetworkManager 状态检查 - IsHost: {NetworkManager.Instance.IsHost}, IsConnected: {NetworkManager.Instance.IsConnected}");

            if (NetworkManager.Instance.IsHost)
            {
                LogDebug("检测到已是Host模式，立即初始化");
                yield return StartCoroutine(InitializeHostGameCoroutine());
            }
            else
            {
                LogDebug("等待主机启动事件...");
                SubscribeToNetworkEvents();
                StartCoroutine(HostStartTimeoutCheck());
            }
        }

        /// 初始化主机游戏的协程
        private IEnumerator InitializeHostGameCoroutine()
        {
            if (isInitialized)
            {
                LogDebug("HostGameManager 已经初始化，跳过重复初始化");
                yield break;
            }

            LogDebug("开始初始化主机游戏逻辑");

            // 初始化游戏状态
            gameInProgress = false;
            currentQuestion = null;

            if (questionDataService == null)
            {
                Debug.LogError("[HostGameManager] QuestionDataService 初始化失败");
                this.enabled = false;
                yield break;
            }

            // 记录NetworkManager的当前状态
            if (NetworkManager.Instance != null)
            {
                LogDebug($"NetworkManager状态 - ClientId: {NetworkManager.Instance.ClientId}, IsHost: {NetworkManager.Instance.IsHost}, IsConnected: {NetworkManager.Instance.IsConnected}");
            }

            SyncPlayersFromRoomSystem();

            isInitialized = true;
            LogDebug($"主机游戏初始化完成，当前玩家数: {playerStateManager.GetPlayerCount()}");

            playerStateManager.ValidateAndFixHostCount();

            if (playerStateManager.GetPlayerCount() > 0)
            {
                LogDebug("从房间系统进入，直接开始游戏");
                Invoke(nameof(StartHostGame), gameStartDelay);
            }
        }

        /// <summary>
        /// 初始化服务依赖
        /// </summary>
        private IEnumerator InitializeServices()
        {
            LogDebug("初始化服务依赖...");

            // 获取或创建 QuestionDataService
            questionDataService = QuestionDataService.Instance;
            if (questionDataService == null)
            {
                questionDataService = FindObjectOfType<QuestionDataService>();
                if (questionDataService == null)
                {
                    GameObject serviceObj = new GameObject("QuestionDataService");
                    questionDataService = serviceObj.AddComponent<QuestionDataService>();
                    LogDebug("创建了新的 QuestionDataService 实例");
                }
                else
                {
                    LogDebug("找到现有的 QuestionDataService 实例");
                }
            }

            // 查找题目控制器引用
            yield return StartCoroutine(FindQuestionController());

            // 预加载常用的数据提供者
            if (questionDataService != null)
            {
                LogDebug("预加载题目数据提供者...");
                questionDataService.PreloadAllProviders();
            }

            LogDebug("服务依赖初始化完成");
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

        #endregion

        #region 双层架构支持

        /// <summary>
        /// 检查双层架构配置
        /// </summary>
        private void CheckDualLayerArchitecture()
        {
            if (!isDualLayerArchitecture)
            {
                LogDebug("使用传统单层架构");
                return;
            }

            var serverMarker = GetComponent<ServerLayerMarker>();
            var playerHostMarker = GetComponent<PlayerHostLayerMarker>();

            if (serverMarker != null)
            {
                isServerLayerOnly = true;
                LogDebug("当前HostGameManager运行在服务器层 (ID=0)");
            }
            else if (playerHostMarker != null)
            {
                isServerLayerOnly = false;
                LogDebug("当前HostGameManager运行在玩家Host层 (ID=1)");
            }
            else
            {
                var localNetworkManager = GetComponentInParent<NetworkManager>() ?? FindObjectOfType<NetworkManager>();
                if (localNetworkManager != null)
                {
                    isServerLayerOnly = (localNetworkManager.ClientId == 0);
                    LogDebug($"通过NetworkManager ClientId判断层级: {(isServerLayerOnly ? "服务器层" : "玩家层")}");
                }
            }
            FindDualLayerNetworkManagers();
        }

        /// <summary>
        /// 查找双层架构中的NetworkManager实例
        /// </summary>
        private void FindDualLayerNetworkManagers()
        {
            var allNetworkManagers = FindObjectsOfType<NetworkManager>();

            foreach (var nm in allNetworkManagers)
            {
                if (nm.ClientId == 0)
                {
                    serverNetworkManager = nm;
                    LogDebug($"找到服务器层NetworkManager: ClientId={nm.ClientId}");
                }
                else if (nm.ClientId == 1)
                {
                    playerHostNetworkManager = nm;
                    LogDebug($"找到玩家Host层NetworkManager: ClientId={nm.ClientId}");
                }
            }

            if (isDualLayerArchitecture)
            {
                LogDebug($"双层架构状态 - 服务器层: {serverNetworkManager != null}, 玩家Host层: {playerHostNetworkManager != null}");
            }
        }

        #endregion

        #region 玩家管理事件处理

        /// <summary>
        /// 从房间系统同步玩家数据
        /// </summary>
        private void SyncPlayersFromRoomSystem()
        {
            LogDebug("同步房间系统玩家数据");

            int initialHealth = hpManager?.GetEffectiveInitialHealth() ?? 100;
            int syncedCount = playerStateManager.SyncFromRoomSystem(initialHealth);

            LogDebug($"玩家同步完成，总计玩家数: {syncedCount}");
        }

        /// <summary>
        /// 玩家状态添加事件处理
        /// </summary>
        private void OnPlayerStateAdded(ushort playerId, string playerName, bool isHost)
        {
            LogDebug($"玩家状态已添加: {playerName} (ID: {playerId}, IsHost: {isHost})");

            // 在HP管理器中初始化玩家
            if (hpManager != null)
            {
                int initialHealth = hpManager.GetEffectiveInitialHealth();
                hpManager.InitializePlayer(playerId, initialHealth);
            }
        }

        /// <summary>
        /// 玩家状态移除事件处理
        /// </summary>
        private void OnPlayerStateRemoved(ushort playerId, string playerName)
        {
            LogDebug($"玩家状态已移除: {playerName} (ID: {playerId})");

            // 从HP管理器中移除玩家
            if (hpManager != null)
            {
                hpManager.RemovePlayer(playerId);
            }

            // 如果当前回合的玩家离开，切换到下一个玩家
            if (currentTurnPlayerId == playerId && gameInProgress)
            {
                NextPlayerTurn();
            }

            CheckGameEndConditions();
        }

        /// <summary>
        /// Host验证失败事件处理
        /// </summary>
        private void OnHostValidationFailed(List<ushort> duplicateHostIds)
        {
            Debug.LogError($"[HostGameManager] Host验证失败，发现重复Host: {string.Join(", ", duplicateHostIds)}");
        }

        /// <summary>
        /// Host验证通过事件处理
        /// </summary>
        private void OnHostValidationPassed(ushort hostId)
        {
            LogDebug($"Host验证通过: {hostId}");
        }

        #endregion

        #region 答案处理

        /// <summary>
        /// 处理玩家答案（优化延迟）
        /// </summary>
        public void HandlePlayerAnswer(ushort playerId, string answer)
        {
            playerAnswerCache[playerId] = answer;
            if (!isInitialized || !gameInProgress || currentQuestion == null)
            {
                LogDebug($"无效的答案提交状态: initialized={isInitialized}, gameInProgress={gameInProgress}, hasQuestion={currentQuestion != null}");
                return;
            }
            if (playerId != currentTurnPlayerId)
            {
                LogDebug($"不是当前玩家的回合: 提交者={playerId}, 当前回合={currentTurnPlayerId}");
                return;
            }
            LogDebug($"处理玩家 {playerId} 的答案: {answer}");

            var validationResult = answerValidationManager.ValidateAnswer(answer, currentQuestion);

            // 立即广播答题结果
            BroadcastPlayerAnswerResult(playerId, validationResult.isCorrect, answer);
            BroadcastAnswerResult(validationResult.isCorrect, currentQuestion.correctAnswer);

            // 更新玩家状态
            UpdatePlayerState(playerId, validationResult.isCorrect);

            // 检查游戏结束条件
            CheckGameEndConditions();

            // 优化：减少延迟时间，如果游戏仍在进行，快速切换到下一个玩家
            if (gameInProgress)
            {
                // 从1秒减少到0.8秒，给玩家短暂时间查看答题结果
                Invoke(nameof(NextPlayerTurn), 0.8f);
            }
        }
        private void BroadcastPlayerAnswerResult(ushort playerId, bool isCorrect, string answer)
        {
            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.PlayerAnswerResult);
            message.AddUShort(playerId);
            message.AddBool(isCorrect);
            message.AddString(answer);
            NetworkManager.Instance.BroadcastMessage(message);
        }

        /// 更新玩家状态
        private void UpdatePlayerState(ushort playerId, bool isCorrect)
        {
            if (!playerStateManager.ContainsPlayer(playerId))
                return;

            if (isCorrect)
            {
                LogDebug($"玩家 {playerId} 答对了");
                HandleCorrectAnswer(playerId);
            }
            else
            {
                LogDebug($"玩家 {playerId} 答错了");
                HandleWrongAnswer(playerId);
            }
        }

        /// 处理正确答案
        private void HandleCorrectAnswer(ushort playerId)
        {
            // 处理成语接龙逻辑
            if (isIdiomChainActive && currentQuestion?.questionType == QuestionType.IdiomChain)
            {
                string playerAnswer = GetLastPlayerAnswer(playerId);
                if (!string.IsNullOrEmpty(playerAnswer))
                {
                    currentIdiomChainWord = playerAnswer.Trim();
                    idiomChainCount++;
                    LogDebug($"成语接龙更新: {currentIdiomChainWord} (第{idiomChainCount}个)");

                    if (idiomChainCount >= MAX_IDIOM_CHAIN)
                    {
                        LogDebug("成语接龙达到最大长度，结束接龙模式");
                        ResetIdiomChainState();
                    }
                }
            }
        }

        /// 处理错误答案
        private void HandleWrongAnswer(ushort playerId)
        {
            if (hpManager != null && hpManager.IsPlayerAlive(playerId))
            {
                bool damageApplied = hpManager.ApplyDamage(playerId, out int newHealth, out bool isDead);

                if (damageApplied)
                {
                    LogDebug($"玩家 {playerId} 答错，HP管理器处理伤害 - 新血量: {newHealth}, 是否死亡: {isDead}");
                }
            }
            // 成语接龙答错时重置
            if (isIdiomChainActive && currentQuestion?.questionType == QuestionType.IdiomChain)
            {
                LogDebug("成语接龙答错，重置接龙状态");
                ResetIdiomChainState();
            }
        }
        /// 答案验证事件处理
        private void OnAnswerValidated(QuestionType questionType, string answer, bool isCorrect)
        {
            LogDebug($"答案验证完成: {questionType} - {answer} -> {(isCorrect ? "正确" : "错误")}");
        }
        /// 验证错误事件处理
        private void OnValidationError(string errorMessage)
        {
            Debug.LogError($"[HostGameManager] 答案验证错误: {errorMessage}");
        }
        #endregion

        #region 题目生成
        private int currentQuestionNumber = 0;
        /// <summary>
        /// 生成并发送题目（添加状态验证）
        /// </summary>
        private void GenerateAndSendQuestion()
        {
            if (!gameInProgress || !isInitialized)
            {
                LogDebug("游戏未进行或未初始化，跳过发题");
                return;
            }

            // 验证当前回合玩家是否有效
            if (currentTurnPlayerId == 0 || !playerStateManager.ContainsPlayer(currentTurnPlayerId))
            {
                Debug.LogError($"当前回合玩家ID无效: {currentTurnPlayerId}");
                return;
            }

            LogDebug($"开始生成新题目 - 当前回合玩家: {currentTurnPlayerId}");
            NetworkQuestionData question = null;

            if (isIdiomChainActive && !string.IsNullOrEmpty(currentIdiomChainWord))
            {
                question = GenerateIdiomChainContinuation(currentIdiomChainWord);
                LogDebug($"生成成语接龙题目，基于: {currentIdiomChainWord}");
            }
            else
            {
                var questionType = gameConfigManager.SelectRandomQuestionType();
                question = GetQuestionFromService(questionType);
                if (questionType == QuestionType.IdiomChain && enableIdiomChain)
                {
                    isIdiomChainActive = true;
                    idiomChainCount = 0;
                    currentIdiomChainWord = null;
                    LogDebug("激活成语接龙模式");
                }
            }

            if (question != null)
            {
                currentQuestionNumber++;
                currentQuestion = question;

                // 先广播游戏进度，再广播题目
                BroadcastGameProgress();

                // 稍微延迟确保进度信息先到达
                StartCoroutine(DelayedQuestionBroadcast(question));

                LogDebug($"题目已准备发送: 第{currentQuestionNumber}题 - {question.questionType} - {question.questionText}");
            }
            else
            {
                Debug.LogError($"题目生成失败，重新尝试");
                Invoke(nameof(GenerateAndSendQuestion), 1f);
            }
        }
        /// <summary>
        /// 延迟的题目广播：确保进度信息先到达
        /// </summary>
        private IEnumerator DelayedQuestionBroadcast(NetworkQuestionData question)
        {
            // 短暂延迟确保进度信息先到达客户端
            yield return new WaitForSeconds(0.1f);

            BroadcastQuestion(question);
            LogDebug($"题目已发送: 第{currentQuestionNumber}题 - {question.questionType}");
        }
        /// 广播游戏进度信息
        private void BroadcastGameProgress()
        {
            if (!isInitialized || NetworkManager.Instance == null)
                return;

            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.GameProgress);
            message.AddInt(currentQuestionNumber); // 当前题目编号
            message.AddInt(playerStateManager.GetAlivePlayerCount()); // 当前存活玩家数
            message.AddUShort(currentTurnPlayerId); // 当前回合玩家
            // 添加题目类型信息
            if (currentQuestion != null)
            {
                message.AddInt((int)currentQuestion.questionType); // 题目类型
                message.AddFloat(currentQuestion.timeLimit); // 时间限制
            }
            else
            {
                message.AddInt(-1); // 无题目
                message.AddFloat(0f);
            }
            NetworkManager.Instance.BroadcastMessage(message);
            LogDebug($"广播游戏进度: 第{currentQuestionNumber}题, 存活{playerStateManager.GetAlivePlayerCount()}人, 回合玩家{currentTurnPlayerId}");
        }
        /// 从服务获取题目数据
        private NetworkQuestionData GetQuestionFromService(QuestionType questionType)
        {
            if (questionDataService == null)
            {
                Debug.LogError("[HostGameManager] QuestionDataService 未初始化");
                return CreateFallbackQuestion(questionType);
            }
            try
            {
                LogDebug($"使用 QuestionDataService 获取题目: {questionType}");
                var questionData = questionDataService.GetQuestionData(questionType);

                if (questionData != null)
                {
                    float dynamicTimeLimit = gameConfigManager.GetTimeLimitForQuestionType(questionType);
                    questionData.timeLimit = dynamicTimeLimit;

                    LogDebug($"成功从服务获取题目: {questionData.questionText}, 时间限制: {dynamicTimeLimit}秒");
                    return questionData;
                }
                else
                {
                    LogDebug($"服务返回空题目，使用备用题目");
                    return CreateFallbackQuestion(questionType);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"从服务获取题目失败: {e.Message}");
                return CreateFallbackQuestion(questionType);
            }
        }
        /// 创建备用题目
        private NetworkQuestionData CreateFallbackQuestion(QuestionType questionType)
        {
            float dynamicTimeLimit = gameConfigManager.GetTimeLimitForQuestionType(questionType);

            return NetworkQuestionDataExtensions.CreateFromLocalData(
                questionType,
                $"这是一个{questionType}类型的备用题目",
                "备用答案",
                questionType == QuestionType.ExplanationChoice || questionType == QuestionType.SimularWordChoice
                    ? new string[] { "选项A", "备用答案", "选项C", "选项D" }
                    : null,
                dynamicTimeLimit,
                "{\"source\": \"host_fallback\", \"isDefault\": true}"
            );
        }
        #endregion

        #region 成语接龙逻辑

        /// <summary>
        /// 生成成语接龙延续题目
        /// </summary>
        private NetworkQuestionData GenerateIdiomChainContinuation(string baseIdiom)
        {
            LogDebug($"生成基于 '{baseIdiom}' 的接龙题目");

            var idiomManager = GetIdiomChainManager();
            if (idiomManager != null)
            {
                var question = idiomManager.CreateContinuationQuestion(baseIdiom, idiomChainCount, currentIdiomChainWord);
                if (question != null)
                {
                    float dynamicTimeLimit = gameConfigManager.GetTimeLimitForQuestionType(QuestionType.IdiomChain);
                    question.timeLimit = dynamicTimeLimit;
                    LogDebug($"生成接龙题成功，时间限制: {dynamicTimeLimit}秒");
                    return question;
                }
            }

            LogDebug("无法生成接龙题目，重置接龙状态");
            ResetIdiomChainState();
            return null;
        }

        private void ResetIdiomChainState()
        {
            isIdiomChainActive = false;
            currentIdiomChainWord = null;
            idiomChainCount = 0;
            LogDebug("成语接龙状态已重置");
        }
        private string GetLastPlayerAnswer(ushort playerId)
        {
            return playerAnswerCache.ContainsKey(playerId) ? playerAnswerCache[playerId] : null;
        }

        private IdiomChainQuestionManager GetIdiomChainManager()
        {
            try
            {
                if (questionDataService != null)
                {
                    var getProviderMethod = questionDataService.GetType()
                        .GetMethod("GetOrCreateProvider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (getProviderMethod != null)
                    {
                        var provider = getProviderMethod.Invoke(questionDataService, new object[] { QuestionType.IdiomChain });

                        if (provider is IdiomChainQuestionManager manager)
                        {
                            return manager;
                        }
                    }
                }

                return FindObjectOfType<IdiomChainQuestionManager>();
            }
            catch (System.Exception e)
            {
                LogDebug($"获取IdiomChainQuestionManager失败: {e.Message}");
                return null;
            }
        }

        #endregion

        #region 回合管理

        /// <summary>
        /// 切换到下一个玩家（优化延迟机制）
        /// </summary>
        private void NextPlayerTurn()
        {
            var alivePlayerIds = playerStateManager.GetAlivePlayerIds();

            if (alivePlayerIds.Count == 0)
            {
                LogDebug("没有存活的玩家，游戏结束");
                EndGame("没有存活的玩家");
                return;
            }

            if (alivePlayerIds.Count == 1)
            {
                var winnerState = playerStateManager.GetPlayerState(alivePlayerIds[0]);
                LogDebug($"游戏结束：{winnerState.playerName} 获胜");
                EndGame($"{winnerState.playerName} 获胜！");
                return;
            }

            // 找到下一个存活的玩家
            int currentIndex = alivePlayerIds.IndexOf(currentTurnPlayerId);
            if (currentIndex == -1)
            {
                currentTurnPlayerId = alivePlayerIds[0];
            }
            else
            {
                int nextIndex = (currentIndex + 1) % alivePlayerIds.Count;
                currentTurnPlayerId = alivePlayerIds[nextIndex];
            }

            LogDebug($"回合切换到玩家: {currentTurnPlayerId}");

            // 优化：使用协程控制时序，避免双重延迟
            StartCoroutine(DelayedTurnChangeSequence());
        }
        private IEnumerator DelayedTurnChangeSequence()
        {
            // 先广播回合变更
            BroadcastPlayerTurnChanged(currentTurnPlayerId);

            // 短暂延迟确保回合信息到达
            yield return new WaitForSeconds(0.3f);

            // 再发送新题目
            GenerateAndSendQuestion();
        }

        /// <summary>
        /// 检查游戏结束条件
        /// </summary>
        private void CheckGameEndConditions()
        {
            if (!gameInProgress)
                return;

            var alivePlayerIds = playerStateManager.GetAlivePlayerIds();

            if (alivePlayerIds.Count == 0)
            {
                LogDebug("游戏结束：没有存活的玩家");
                EndGame("游戏结束：所有玩家都被淘汰");
            }
            else if (alivePlayerIds.Count == 1)
            {
                var winnerState = playerStateManager.GetPlayerState(alivePlayerIds[0]);
                LogDebug($"游戏结束：玩家 {alivePlayerIds[0]} ({winnerState.playerName}) 获胜");
                EndGame($"游戏结束：{winnerState.playerName} 获胜！");
            }
            else if (playerStateManager.GetPlayerCount() == 0)
            {
                LogDebug("游戏结束：没有玩家");
                EndGame("游戏结束：所有玩家都离开了");
            }
        }

        #endregion

        #region 游戏流程控制

        /// <summary>
        /// 开始主机游戏（修复版：分离游戏开始和发题逻辑）
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

            LogDebug($"主机开始游戏，玩家数: {playerStateManager.GetPlayerCount()}");

            if (playerStateManager.GetPlayerCount() == 0)
            {
                Debug.LogError("没有玩家可以开始游戏 - 玩家列表为空");
                return;
            }

            gameInProgress = true;
            currentQuestionNumber = 0; // 重置题目编号

            // 启动NQMC多人游戏模式
            if (NetworkQuestionManagerController.Instance != null)
            {
                LogDebug("启动NQMC多人游戏模式");
                NetworkQuestionManagerController.Instance.StartGame(true);
            }
            else
            {
                Debug.LogError("NetworkQuestionManagerController.Instance为空，无法启动题目管理");
            }

            // 选择第一个存活的玩家开始
            var alivePlayerIds = playerStateManager.GetAlivePlayerIds();
            if (alivePlayerIds.Count > 0)
            {
                currentTurnPlayerId = alivePlayerIds[0];
                var playerState = playerStateManager.GetPlayerState(currentTurnPlayerId);
                LogDebug($"选择玩家 {currentTurnPlayerId} ({playerState.playerName}) 开始游戏");

                // 分步骤广播：先广播游戏开始，再广播状态，最后发题
                BroadcastGameStartedOnly();
                BroadcastAllPlayerStates();

                // 关键修改：延迟广播回合信息和发送第一题
                StartCoroutine(DelayedGameStartSequence());
            }
            else
            {
                Debug.LogError("没有存活的玩家可以开始游戏");
                gameInProgress = false;
            }
        }
        /// <summary>
        /// 仅广播游戏开始消息（不包含回合信息）
        /// </summary>
        private void BroadcastGameStartedOnly()
        {
            if (!isInitialized || NetworkManager.Instance == null)
                return;

            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.GameStart);
            message.AddInt(playerStateManager.GetPlayerCount()); // 初始玩家数
            message.AddInt(playerStateManager.GetAlivePlayerCount()); // 存活玩家数
            message.AddUShort(0); // 暂时不指定首回合玩家，后续单独发送
            NetworkManager.Instance.BroadcastMessage(message);

            LogDebug($"广播游戏开始（仅开始信息）: 总玩家{playerStateManager.GetPlayerCount()}, 存活{playerStateManager.GetAlivePlayerCount()}");
        }
        /// <summary>
        /// 延迟的游戏开始序列：确保状态同步后再发题
        /// </summary>
        private IEnumerator DelayedGameStartSequence()
        {
            LogDebug("开始延迟游戏启动序列");

            yield return new WaitForSeconds(1f);

            // 广播回合变更（第一次）
            LogDebug($"广播首个回合: 玩家 {currentTurnPlayerId}");
            BroadcastPlayerTurnChanged(currentTurnPlayerId);

            yield return new WaitForSeconds(0.5f);

            // 发送第一题
            LogDebug("发送第一题");
            GenerateAndSendQuestion();
        }
        /// <summary>
        /// 向所有客户端同步完整玩家状态
        /// </summary>
        private void BroadcastAllPlayerStates()
        {
            if (!isInitialized || NetworkManager.Instance == null)
                return;

            var allPlayerStates = playerStateManager.GetAllPlayerStates();

            foreach (var playerState in allPlayerStates.Values)
            {
                Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.PlayerStateSync);
                message.AddUShort(playerState.playerId);
                message.AddString(playerState.playerName);
                message.AddBool(IsHostPlayer(playerState.playerId)); // 是否为Host
                message.AddInt(playerState.health);
                message.AddInt(playerState.maxHealth);
                message.AddBool(playerState.isAlive);

                NetworkManager.Instance.BroadcastMessage(message);
            }

            LogDebug($"广播了 {allPlayerStates.Count} 个玩家的状态数据");
        }
        /// <summary>
        /// 广播游戏开始消息
        /// </summary>
        private void BroadcastGameStarted()
        {
            if (!isInitialized || NetworkManager.Instance == null)
                return;

            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.GameStart);
            message.AddInt(playerStateManager.GetPlayerCount()); // 初始玩家数
            message.AddInt(playerStateManager.GetAlivePlayerCount()); // 存活玩家数
            message.AddUShort(currentTurnPlayerId); // 首个回合玩家
            NetworkManager.Instance.BroadcastMessage(message);

            LogDebug($"广播游戏开始: 总玩家{playerStateManager.GetPlayerCount()}, 存活{playerStateManager.GetAlivePlayerCount()}, 首回合玩家{currentTurnPlayerId}");
        }

        /// <summary>
        /// 从房间系统启动游戏（外部调用接口）
        /// </summary>
        public void StartGameFromRoom()
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

            LogDebug("从房间系统启动游戏 - 准备游戏数据");
            PrepareGameData();
            LogDebug("HostGameManager游戏数据准备完成，等待场景切换");
        }

        /// <summary>
        /// 准备游戏数据
        /// </summary>
        private void PrepareGameData()
        {
            LogDebug("准备游戏数据...");

            // 同步房间系统的玩家数据
            SyncPlayersFromRoomSystem();

            // 预加载题目数据
            if (questionDataService != null)
            {
                LogDebug("预加载题目数据提供者...");
                questionDataService.PreloadAllProviders();
            }

            // 验证必要组件
            if (!ValidateGameComponents())
            {
                Debug.LogError("[HostGameManager] 游戏组件验证失败");
                return;
            }

            LogDebug("游戏数据准备完成");
        }

        /// <summary>
        /// 验证游戏组件
        /// </summary>
        private bool ValidateGameComponents()
        {
            bool isValid = true;

            if (questionDataService == null)
            {
                Debug.LogError("QuestionDataService 未初始化");
                isValid = false;
            }

            if (NetworkManager.Instance == null)
            {
                Debug.LogError("NetworkManager 实例不存在");
                isValid = false;
            }

            if (playerStateManager == null || playerStateManager.GetPlayerCount() == 0)
            {
                Debug.LogError("玩家数据为空");
                isValid = false;
            }

            return isValid;
        }

        /// <summary>
        /// 结束游戏
        /// </summary>
        public void EndGame(string reason = "游戏结束")
        {
            if (!gameInProgress)
                return;

            LogDebug($"结束游戏: {reason}");

            // 确定获胜者
            ushort winnerId = 0;
            var alivePlayerIds = playerStateManager.GetAlivePlayerIds();
            if (alivePlayerIds.Count == 1)
            {
                winnerId = alivePlayerIds[0];
                var winnerState = playerStateManager.GetPlayerState(winnerId);
                LogDebug($"获胜者: {winnerState.playerName} (ID: {winnerId})");
            }

            // 广播游戏结束
            BroadcastGameEnded(reason, winnerId);

            // 重置游戏状态
            gameInProgress = false;
            currentQuestion = null;
            currentQuestionNumber = 0;
            ResetIdiomChainState();
            playerAnswerCache.Clear();

            LogDebug("游戏状态已重置");
        }

        /// <summary>
        /// 广播游戏结束消息
        /// </summary>
        private void BroadcastGameEnded(string reason, ushort winnerId = 0)
        {
            if (!isInitialized || NetworkManager.Instance == null)
                return;

            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.EndGame);
            message.AddString(reason); // 结束原因
            message.AddUShort(winnerId); // 获胜者ID (0表示无获胜者)
            message.AddInt(currentQuestionNumber); // 总题目数
            message.AddInt(playerStateManager.GetAlivePlayerCount()); // 最终存活数
            NetworkManager.Instance.BroadcastMessage(message);

            LogDebug($"广播游戏结束: {reason}, 获胜者ID: {winnerId}, 总题数: {currentQuestionNumber}");
        }

        /// <summary>
        /// 场景加载完成后的初始化
        /// </summary>
        public void OnGameSceneLoaded()
        {
            LogDebug("游戏场景加载完成，启动游戏逻辑");

            if (isInitialized && !gameInProgress)
            {
                Invoke(nameof(StartHostGame), 1f);
            }
        }

        #endregion

        #region HP事件处理

        /// <summary>
        /// 玩家血量变更事件处理
        /// </summary>
        private void OnPlayerHealthChanged(ushort playerId, int newHealth, int maxHealth)
        {
            LogDebug($"玩家 {playerId} 血量变更: {newHealth}/{maxHealth}");

            // 更新玩家状态管理器
            playerStateManager.UpdatePlayerHealth(playerId, newHealth, maxHealth);

            // 广播血量更新
            BroadcastHealthUpdate(playerId, newHealth, maxHealth);
        }

        /// <summary>
        /// 玩家死亡事件处理
        /// </summary>
        private void OnPlayerDied(ushort playerId)
        {
            LogDebug($"玩家 {playerId} 已死亡");

            // 更新玩家状态管理器
            playerStateManager.SetPlayerAlive(playerId, false);

            // 广播血量更新（死亡状态）
            BroadcastHealthUpdate(playerId, 0);

            // 如果是当前回合玩家死亡，切换到下一个玩家
            if (currentTurnPlayerId == playerId && gameInProgress)
            {
                LogDebug($"当前回合玩家 {playerId} 死亡，切换到下一个玩家");
                Invoke(nameof(NextPlayerTurn), 1f);
            }

            CheckGameEndConditions();
        }

        #endregion

        #region 配置事件处理

        /// <summary>
        /// 配置变更事件处理
        /// </summary>
        private void OnConfigurationChanged()
        {
            LogDebug("游戏配置已变更");

            // 可以在这里添加配置变更后的处理逻辑
            // 比如通知客户端配置更新等
        }

        #endregion

        #region 网络事件处理

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
        /// 玩家加入事件处理
        /// </summary>
        private void OnPlayerJoined(ushort playerId)
        {
            LogDebug($"网络事件: 玩家 {playerId} 加入");

            if (isInitialized && playerStateManager != null)
            {
                int initialHealth = hpManager?.GetEffectiveInitialHealth() ?? 100;
                playerStateManager.AddPlayer(playerId, $"玩家{playerId}", initialHealth, initialHealth, false);
            }
        }

        /// <summary>
        /// 玩家离开事件处理
        /// </summary>
        private void OnPlayerLeft(ushort playerId)
        {
            LogDebug($"网络事件: 玩家 {playerId} 离开");

            if (isInitialized && playerStateManager != null)
            {
                playerStateManager.RemovePlayer(playerId);
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

        #endregion

        #region 网络消息广播

        /// <summary>
        /// 广播题目
        /// </summary>
        private void BroadcastQuestion(NetworkQuestionData question)
        {
            if (!isInitialized || NetworkManager.Instance == null)
            {
                Debug.LogError("[HostGameManager] 无法广播题目：未初始化或NetworkManager为空");
                return;
            }

            LogDebug($"准备广播题目: {question.questionType} - {question.questionText}");

            if (isDualLayerArchitecture && isServerLayerOnly)
            {
                BroadcastQuestionDualLayer(question);
            }
            else
            {
                BroadcastQuestionTraditional(question);
            }

            SyncAllPlayersHealth();
        }

        /// <summary>
        /// 双层架构的题目广播
        /// </summary>
        private void BroadcastQuestionDualLayer(NetworkQuestionData question)
        {
            LogDebug("双层架构：服务器层广播题目");

            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.SendQuestion);
            message.AddBytes(question.Serialize());

            if (serverNetworkManager != null)
            {
                serverNetworkManager.BroadcastMessage(message);
                LogDebug("题目已通过服务器层广播给外部客户端");
            }

            if (DualLayerArchitectureManager.Instance != null)
            {
                DualLayerArchitectureManager.Instance.ServerToPlayerHostQuestion(question);
                LogDebug("题目已发送给本地玩家Host层");
            }
        }

        /// 传统架构的题目广播
        private void BroadcastQuestionTraditional(NetworkQuestionData question)
        {
            LogDebug("传统架构：直接广播题目");

            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.SendQuestion);
            message.AddBytes(question.Serialize());
            NetworkManager.Instance.BroadcastMessage(message);

            LogDebug("题目已通过网络广播");
            if (NetworkManager.Instance.IsHost)
            {
                TriggerHostQuestionReceive(NetworkQuestionManagerController.Instance, question);
            }
        }
        /// 触发Host的题目接收
        private void TriggerHostQuestionReceive(NetworkQuestionManagerController nqmc, NetworkQuestionData question)
        {
            try
            {
                LogDebug("尝试触发Host的题目接收");
                var publicMethod = typeof(NetworkQuestionManagerController).GetMethod("ReceiveNetworkQuestion");
                if (publicMethod != null)
                {
                    LogDebug("通过公共方法 ReceiveNetworkQuestion 触发");
                    publicMethod.Invoke(nqmc, new object[] { question });
                    return;
                }
                var privateMethod = typeof(NetworkQuestionManagerController).GetMethod("OnNetworkQuestionReceived",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (privateMethod != null)
                {
                    LogDebug("通过反射调用 OnNetworkQuestionReceived");
                    privateMethod.Invoke(nqmc, new object[] { question });
                }
                else
                {
                    if (!nqmc.IsGameStarted)
                    {
                        LogDebug("NQMC未启动，尝试启动多人游戏");
                        nqmc.StartGame(true);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"触发Host题目接收失败: {e.Message}");
            }
        }

        /// 广播玩家回合变更
        private void BroadcastPlayerTurnChanged(ushort playerId)
        {
            if (!isInitialized || NetworkManager.Instance == null)
                return;

            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.PlayerTurnChanged);
            message.AddUShort(playerId);
            NetworkManager.Instance.BroadcastMessage(message);

            LogDebug($"广播回合变更: 轮到玩家 {playerId}");
        }

        /// 广播血量更新
        private void BroadcastHealthUpdate(ushort playerId, int newHealth, int maxHealth = -1)
        {
            if (!isInitialized || NetworkManager.Instance == null)
                return;
            if (maxHealth == -1)
            {
                var playerState = playerStateManager.GetPlayerState(playerId);
                maxHealth = playerState?.maxHealth ?? 100;
            }
            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.HealthUpdate);
            message.AddUShort(playerId);
            message.AddInt(newHealth);
            message.AddInt(maxHealth);
            NetworkManager.Instance.BroadcastMessage(message);

            LogDebug($"广播血量更新: 玩家{playerId}, 血量{newHealth}/{maxHealth}");
        }

        /// 广播答题结果
        private void BroadcastAnswerResult(bool isCorrect, string correctAnswer)
        {
            if (!isInitialized || NetworkManager.Instance == null)
                return;

            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.AnswerResult);
            message.AddBool(isCorrect);
            message.AddString(correctAnswer);
            NetworkManager.Instance.BroadcastMessage(message);
        }

        /// 同步所有玩家血量状态
        private void SyncAllPlayersHealth()
        {
            if (!isInitialized || NetworkManager.Instance == null)
                return;

            LogDebug("同步所有玩家血量状态...");
            var allPlayerStates = playerStateManager.GetAllPlayerStates();
            foreach (var playerState in allPlayerStates.Values)
            {
                SyncPlayerHealth(playerState.playerId);
            }
        }

        /// 同步单个玩家血量
        private void SyncPlayerHealth(ushort playerId)
        {
            var playerState = playerStateManager.GetPlayerState(playerId);
            if (playerState == null)
                return;

            int currentHealth = playerState.health;
            int maxHealth = playerState.maxHealth;
            if (hpManager != null)
            {
                var hpInfo = hpManager.GetPlayerHP(playerId);
                if (hpInfo.currentHealth > 0 || hpInfo.maxHealth > 0)
                {
                    currentHealth = hpInfo.currentHealth;
                    maxHealth = hpInfo.maxHealth;
                }
            }
            LogDebug($"同步玩家 {playerId} 血量: {currentHealth}/{maxHealth}");
            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.HealthUpdate);
            message.AddUShort(playerId);
            message.AddInt(currentHealth);
            message.AddInt(maxHealth);
            NetworkManager.Instance.BroadcastMessage(message);
        }

        #endregion

        #region 调试和状态信息
        /// 获取游戏统计信息
        public string GetGameStats()
        {
            var stats = "=== 重构后的游戏统计 ===\n";
            stats += $"游戏状态: {(gameInProgress ? "进行中" : "未开始")}\n";
            stats += $"初始化完成: {isInitialized}\n";
            stats += $"当前回合玩家: {currentTurnPlayerId}\n";
            stats += $"玩家总数: {playerStateManager.GetPlayerCount()}\n";
            stats += $"存活玩家数: {playerStateManager.GetAlivePlayerCount()}\n";
            stats += $"Host玩家ID: {playerStateManager.GetHostPlayerId()}\n";
            stats += $"HP配置源: {hpManager.GetHPConfigSource()}\n";
            stats += $"初始血量: {hpManager.GetEffectiveInitialHealth()}\n";
            stats += $"答错扣血: {hpManager.GetEffectiveDamageAmount()}\n";
            stats += $"最多答错: {hpManager.GetMaxWrongAnswers()}次\n";
            stats += $"配置摘要: {gameConfigManager.GetConfigSummary()}\n";
            stats += $"当前题目类型: {currentQuestion.questionType}\n";
            stats += $"当前题目时间限制: {currentQuestion.timeLimit}秒\n";
            stats += $"NetworkManager Host玩家ID: {NetworkManager.Instance.GetHostPlayerId()}\n";
            stats += $"NetworkManager Host客户端就绪: {NetworkManager.Instance.IsHostClientReady}\n";

            return stats;
        }
        /// 获取状态信息
        public string GetStatusInfo()
        {
            return $"Initialized: {isInitialized}, " +
                   $"GameInProgress: {gameInProgress}, " +
                   $"PlayerCount: {PlayerCount}, " +
                   $"CurrentTurn: {currentTurnPlayerId}, " +
                   $"NetworkManager: {NetworkManager.Instance != null}, " +
                   $"IsHost: {NetworkManager.Instance?.IsHost}, " +
                   $"PlayerStateManager: {playerStateManager != null}, " +
                   $"GameConfigManager: {gameConfigManager != null}, " +
                   $"AnswerValidationManager: {answerValidationManager != null}, " +
                   $"HPManager: {hpManager != null}";
        }

        /// 清理服务缓存
        public void ClearServiceCache()
        {
            if (questionDataService != null)
            {
                questionDataService.ClearCache();
                LogDebug("已清理题目数据服务缓存");
            }
        }
        #endregion

        #region 调试方法

        [ContextMenu("显示玩家列表")]
        public void ShowPlayerList()
        {
            if (Application.isPlaying && playerStateManager != null)
            {
                LogDebug("=== 当前玩家列表 ===");
                LogDebug(playerStateManager.GetStatusInfo());
            }
        }
        /// <summary>
        /// 显示管理器状态
        /// </summary>
        [ContextMenu("显示管理器状态")]
        public void ShowManagerStatus()
        {
            LogDebug("=== 管理器状态 ===");
            if (playerStateManager != null){LogDebug("PlayerStateManager: " + playerStateManager.GetStatusInfo());}
            if (gameConfigManager != null){LogDebug("GameConfigManager: " + gameConfigManager.GetStatusInfo());}
            if (answerValidationManager != null){LogDebug("AnswerValidationManager: " + answerValidationManager.GetValidationStats());}
            if (hpManager != null){LogDebug("HPManager: " + hpManager.GetStatusInfo());}
        }

        #endregion

        #region 辅助方法
        /// 调试日志
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[HostGameManager] {message}");
            }
        }
        private bool IsHostPlayer(ushort playerId)
        {
            return NetworkManager.Instance?.IsHostPlayer(playerId) ?? false;
        }
        #endregion
    }
}