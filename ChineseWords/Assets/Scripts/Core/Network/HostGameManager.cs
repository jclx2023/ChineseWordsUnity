using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using GameLogic.FillBlank;
using Photon.Realtime;
using Cards.Player;
using Cards.Core;

namespace Core.Network
{
    /// <summary>
    /// 优化后的Host游戏管理器 - 专注于游戏逻辑控制
    /// 适用场景：NetworkGameScene
    /// 职责：游戏流程控制、题目生成、答案验证、玩家状态管理
    /// </summary>
    public class HostGameManager : MonoBehaviourPun, IInRoomCallbacks
    {
        [Header("游戏配置")]
        [SerializeField] private QuestionWeightConfig questionWeightConfig;
        [SerializeField] private TimerConfig timerConfig;
        [SerializeField] private HPConfig hpConfig;
        [SerializeField] private CardConfig cardConfig;
        [SerializeField] private bool useCustomHPConfig = false;

        [Header("成语接龙设置")]
        [SerializeField] private bool enableIdiomChain = true;
        [SerializeField] private int maxIdiomChain = 10;

        [Header("游戏流程设置")]
        [SerializeField] private float gameStartDelay = 2f;
        [SerializeField] private float turnChangeDelay = 0.8f;
        [SerializeField] private float answerDisplayDelay = 1.0f;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        public static HostGameManager Instance { get; private set; }

        #region 管理器模块

        private PlayerStateManager playerStateManager;
        private GameConfigManager gameConfigManager;
        private AnswerValidationManager answerValidationManager;
        private PlayerHPManager hpManager;
        private QuestionDataService questionDataService;

        #endregion

        #region 游戏状态

        private bool isInitialized = false;
        private bool gameInProgress = false;
        private ushort currentTurnPlayerId = 0;
        private int currentQuestionNumber = 0;
        private NetworkQuestionData currentQuestion = null;

        // 成语接龙状态
        private bool isIdiomChainActive = false;
        private string currentIdiomChainWord = null;
        private int idiomChainCount = 0;

        // 玩家答案缓存
        private Dictionary<ushort, string> playerAnswerCache = new Dictionary<ushort, string>();

        #endregion

        #region 公共属性

        public bool IsGameInProgress => gameInProgress;
        public int PlayerCount => playerStateManager?.GetPlayerCount() ?? 0;
        public ushort CurrentTurnPlayer => currentTurnPlayerId;
        public bool IsInitialized => isInitialized;
        public int CurrentQuestionNumber => currentQuestionNumber;

        #endregion

        #region Unity生命周期

        private void Awake()
        {
            // 简化的单例模式
            if (Instance == null)
            {
                Instance = this;
                LogDebug("HostGameManager 实例已创建");
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
            // 只有在NetworkGameScene且为MasterClient时才启动
            if (!PhotonNetwork.InRoom)
            {
                Debug.LogError("[HostGameManager] 未在Photon房间中，禁用组件");
                this.enabled = false;
                return;
            }

            if (!PhotonNetwork.IsMasterClient)
            {
                LogDebug("非MasterClient，禁用HostGameManager");
                this.enabled = false;
                return;
            }

            // 注册Photon回调
            PhotonNetwork.AddCallbackTarget(this);

            LogDebug($"作为MasterClient启动 - 房间: {PhotonNetwork.CurrentRoom.Name}");
            StartCoroutine(InitializeHostGameManager());
        }

        private void OnDestroy()
        {
            // 移除Photon回调注册
            if (PhotonNetwork.NetworkingClient != null)
            {
                PhotonNetwork.RemoveCallbackTarget(this);
            }

            // 清理管理器
            playerStateManager?.Dispose();
            gameConfigManager?.Dispose();
            answerValidationManager?.Dispose();
            hpManager?.Dispose();

            if (Instance == this)
            {
                Instance = null;
            }

            LogDebug("HostGameManager已销毁");
        }

        #endregion

        #region 初始化流程

        /// <summary>
        /// 初始化Host游戏管理器
        /// </summary>
        private IEnumerator InitializeHostGameManager()
        {
            LogDebug("开始初始化HostGameManager");

            // 等待NetworkManager准备就绪
            yield return StartCoroutine(WaitForNetworkManager());

            // 初始化管理器模块
            InitializeManagers();

            // 初始化服务依赖
            InitializeServices();

            // 同步玩家数据
            SyncPlayersFromPhoton();

            isInitialized = true;
            LogDebug($"HostGameManager初始化完成 - 玩家数: {PlayerCount}");

            // 如果有玩家，延迟启动游戏
            if (PlayerCount > 0)
            {
                LogDebug($"延迟{gameStartDelay}秒后启动游戏");
                Invoke(nameof(StartGame), gameStartDelay);
            }
        }

        /// <summary>
        /// 等待NetworkManager准备就绪
        /// </summary>
        private IEnumerator WaitForNetworkManager()
        {
            float timeout = 5f;
            float elapsed = 0f;

            while (NetworkManager.Instance == null && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (NetworkManager.Instance == null)
            {
                Debug.LogError("[HostGameManager] NetworkManager初始化超时");
                this.enabled = false;
            }
            else
            {
                LogDebug("NetworkManager已准备就绪");
            }
        }

        /// <summary>
        /// 初始化管理器模块
        /// </summary>
        private void InitializeManagers()
        {
            try
            {
                // 初始化玩家状态管理器
                playerStateManager = new PlayerStateManager();
                playerStateManager.Initialize(NetworkManager.Instance);
                playerStateManager.OnPlayerAdded += OnPlayerAdded;
                playerStateManager.OnPlayerRemoved += OnPlayerRemoved;

                // 初始化配置管理器
                gameConfigManager = new GameConfigManager();
                gameConfigManager.Initialize(timerConfig, questionWeightConfig);

                // 初始化答案验证管理器
                answerValidationManager = new AnswerValidationManager();
                answerValidationManager.Initialize();

                // 初始化HP管理器
                hpManager = new PlayerHPManager();
                hpManager.Initialize(hpConfig, useCustomHPConfig);
                hpManager.OnHealthChanged += OnPlayerHealthChanged;
                hpManager.OnPlayerDied += OnPlayerDied;

                //初始化卡牌管理器
                //InitializeCardGameBridge();

                LogDebug("所有管理器初始化完成");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HostGameManager] 管理器初始化失败: {e.Message}");
                this.enabled = false;
            }
        }

        /// <summary>
        /// 初始化服务依赖
        /// </summary>
        private void InitializeServices()
        {
            // 获取QuestionDataService
            questionDataService = QuestionDataService.Instance;
            if (questionDataService == null)
            {
                questionDataService = FindObjectOfType<QuestionDataService>();
                if (questionDataService == null)
                {
                    GameObject serviceObj = new GameObject("QuestionDataService");
                    questionDataService = serviceObj.AddComponent<QuestionDataService>();
                    LogDebug("创建新的QuestionDataService");
                }
            }

            // 预加载题目数据
            questionDataService?.PreloadAllProviders();
            LogDebug("服务依赖初始化完成");
        }

        /// <summary>
        /// 从Photon同步玩家数据
        /// </summary>
        private void SyncPlayersFromPhoton()
        {
            LogDebug("从Photon同步玩家数据");

            int initialHealth = hpManager?.GetEffectiveInitialHealth() ?? 100;

            // 清空现有玩家数据
            playerStateManager.ClearAllPlayers();

            // 添加房间中的所有玩家
            foreach (var player in PhotonNetwork.PlayerList)
            {
                ushort playerId = (ushort)player.ActorNumber;
                string playerName = player.NickName ?? $"玩家{playerId}";
                bool isHost = player.IsMasterClient;

                playerStateManager.AddPlayer(playerId, playerName, initialHealth, initialHealth, isHost);
                hpManager?.InitializePlayer(playerId, initialHealth);

                LogDebug($"同步玩家: {playerName} (ID: {playerId}, Host: {isHost})");
            }

            LogDebug($"玩家同步完成，总计: {PlayerCount}");
        }

        #endregion

        #region 游戏流程控制

        /// <summary>
        /// 开始游戏
        /// </summary>
        public void StartGame()
        {
            if (!isInitialized)
            {
                Debug.LogError("[HostGameManager] 未初始化，无法开始游戏");
                return;
            }

            if (gameInProgress)
            {
                LogDebug("游戏已在进行中");
                return;
            }

            if (PlayerCount == 0)
            {
                Debug.LogError("[HostGameManager] 没有玩家，无法开始游戏");
                return;
            }

            LogDebug($"开始游戏 - 玩家数: {PlayerCount}");

            gameInProgress = true;
            currentQuestionNumber = 0;

            // 重置状态
            ResetGameState();

            // 选择第一个存活玩家
            var alivePlayerIds = playerStateManager.GetAlivePlayerIds();
            if (alivePlayerIds.Count > 0)
            {
                currentTurnPlayerId = alivePlayerIds[0];
                LogDebug($"选择玩家 {currentTurnPlayerId} 开始游戏");
                int cardsToGive = cardConfig.SystemSettings.startingCardCount;
                // 分发手牌
                DistributeCardsToAllAlivePlayers(cardsToGive);

                // 启动NQMC
                StartNetworkQuestionController();

                // 分步广播游戏开始
                StartCoroutine(GameStartSequence());
            }
            else
            {
                Debug.LogError("[HostGameManager] 没有存活玩家，无法开始游戏");
                gameInProgress = false;
            }
        }

        /// <summary>
        /// 游戏开始序列
        /// </summary>
        private IEnumerator GameStartSequence()
        {
            // 1. 广播游戏开始
            int totalPlayers = PlayerCount;
            int alivePlayers = playerStateManager.GetAlivePlayerCount();
            NetworkManager.Instance.BroadcastGameStart(totalPlayers, alivePlayers, currentTurnPlayerId);

            // 2. 同步所有玩家状态
            BroadcastAllPlayerStates();

            yield return new WaitForSeconds(1f);

            // 3. 广播首个回合
            NetworkManager.Instance.BroadcastPlayerTurnChanged(currentTurnPlayerId);

            yield return new WaitForSeconds(0.5f);

            // 4. 发送第一题
            GenerateAndSendQuestion();
        }

        /// <summary>
        /// 启动网络题目控制器
        /// </summary>
        private void StartNetworkQuestionController()
        {
            if (NetworkQuestionManagerController.Instance != null)
            {
                NetworkQuestionManagerController.Instance.StartGame(true);
                LogDebug("NQMC多人游戏模式已启动");
            }
            else
            {
                Debug.LogError("[HostGameManager] 找不到NetworkQuestionManagerController");
            }
        }

        /// <summary>
        /// 结束游戏 - 添加卡牌效果清理
        /// </summary>
        public void EndGame(string reason = "游戏结束")
        {
            if (!gameInProgress) return;

            LogDebug($"结束游戏: {reason}");

            // 确定获胜者信息
            ushort winnerId = 0;
            var alivePlayerIds = playerStateManager?.GetAlivePlayerIds();
            if (alivePlayerIds != null && alivePlayerIds.Count == 1)
            {
                winnerId = alivePlayerIds[0];
                var winnerState = playerStateManager.GetPlayerState(winnerId);
                LogDebug($"获胜者: {winnerState?.playerName} (ID: {winnerId})");
            }

            // 清理卡牌效果状态
            CleanupCardEffects();

            // 广播游戏结束
            BroadcastGameEnd(reason, winnerId);

            // 重置游戏状态
            ResetGameState();
            gameInProgress = false;

            LogDebug("游戏已结束");
        }
        private void CleanupCardEffects()
        {
            try
            {
                if (Cards.Integration.CardGameBridge.Instance != null)
                {
                    Cards.Integration.CardGameBridge.Instance.ClearAllEffectStates();
                    LogDebug("已清理所有卡牌效果状态");
                }
            }
            catch (System.Exception e)
            {
                LogDebug($"清理卡牌效果失败: {e.Message}");
            }
        }

        /// <summary>
        /// 重置游戏状态
        /// </summary>
        private void ResetGameState()
        {
            currentQuestion = null;
            currentQuestionNumber = 0;
            currentTurnPlayerId = 0;

            // 重置成语接龙
            isIdiomChainActive = false;
            currentIdiomChainWord = null;
            idiomChainCount = 0;

            // 清空答案缓存
            playerAnswerCache.Clear();

            LogDebug("游戏状态已重置");
        }

        #endregion

        #region 题目生成与管理

        /// <summary>
        /// 生成并发送题目
        /// </summary>
        private void GenerateAndSendQuestion()
        {
            if (!gameInProgress || !isInitialized)
            {
                LogDebug("游戏未进行或未初始化，跳过发题");
                return;
            }

            if (currentTurnPlayerId == 0 || !playerStateManager.ContainsPlayer(currentTurnPlayerId))
            {
                Debug.LogError($"[HostGameManager] 当前回合玩家ID无效: {currentTurnPlayerId}");
                return;
            }

            // 检查卡牌效果 - 跳过和代理
            if (!CheckCardEffectsBeforeQuestion())
            {
                return; // 如果被跳过，直接切换到下一个玩家
            }

            LogDebug($"生成题目 - 当前回合: 玩家{currentTurnPlayerId}");

            NetworkQuestionData question = null;

            // 检查卡牌指定的题目类型
            var specifiedQuestionType = GetCardSpecifiedQuestionType();

            if (!string.IsNullOrEmpty(specifiedQuestionType))
            {
                // 使用卡牌指定的题目类型
                question = GenerateSpecifiedTypeQuestion(specifiedQuestionType);
            }
            else if (isIdiomChainActive && !string.IsNullOrEmpty(currentIdiomChainWord))
            {
                // 检查是否为成语接龙延续
                question = GenerateIdiomChainContinuation();
            }
            else
            {
                // 生成普通题目
                question = GenerateNormalQuestion();
            }

            if (question != null)
            {
                // 应用时间调整效果
                ApplyTimeAdjustmentToQuestion(question);

                currentQuestionNumber++;
                currentQuestion = question;

                // 先广播游戏进度，再发送题目
                BroadcastGameProgress();
                StartCoroutine(DelayedQuestionBroadcast(question));

                LogDebug($"题目已准备 - 第{currentQuestionNumber}题: {question.questionType}");
            }
            else
            {
                Debug.LogError("[HostGameManager] 题目生成失败，重试");
                Invoke(nameof(GenerateAndSendQuestion), 1f);
            }
        }
        private bool CheckCardEffectsBeforeQuestion()
        {
            try
            {
                if (Cards.Integration.CardGameBridge.Instance != null)
                {
                    int actualAnswerPlayerId;
                    bool shouldProceed = Cards.Integration.CardGameBridge.Instance.OnQuestionStarting(
                        currentTurnPlayerId, out actualAnswerPlayerId);

                    if (!shouldProceed)
                    {
                        LogDebug($"玩家{currentTurnPlayerId}被跳过，切换到下一个玩家");
                        Invoke(nameof(NextPlayerTurn), turnChangeDelay);
                        return false;
                    }
                }
            }
            catch (System.Exception e)
            {
                LogDebug($"检查卡牌效果失败: {e.Message}，继续正常流程");
            }

            return true;
        }
        private string GetCardSpecifiedQuestionType()
        {
            try
            {
                if (Cards.Integration.CardGameBridge.Instance != null)
                {
                    return Cards.Integration.CardGameBridge.Instance.OnQuestionTypeSelecting(currentTurnPlayerId);
                }
            }
            catch (System.Exception e)
            {
                LogDebug($"获取卡牌指定题目类型失败: {e.Message}");
            }

            return null;
        }
        /// <summary>
        /// 生成指定类型的题目
        /// </summary>
        private NetworkQuestionData GenerateSpecifiedTypeQuestion(string questionTypeString)
        {
            try
            {
                // 将字符串转换为QuestionType枚举
                QuestionType questionType;
                switch (questionTypeString)
                {
                    case "IdiomChain":
                        questionType = QuestionType.IdiomChain;
                        break;
                    case "TrueFalse":
                    case "SentimentTorF":
                        questionType = QuestionType.SentimentTorF;
                        break;
                    case "UsageTorF":
                        questionType = QuestionType.UsageTorF;
                        break;
                    default:
                        LogDebug($"未知的题目类型字符串: {questionTypeString}，使用默认生成");
                        return GenerateNormalQuestion();
                }

                LogDebug($"生成卡牌指定的题目类型: {questionType}");

                var question = GetQuestionFromService(questionType);

                // 如果是成语接龙，激活成语接龙模式
                if (questionType == QuestionType.IdiomChain && enableIdiomChain)
                {
                    isIdiomChainActive = true;
                    idiomChainCount = 0;
                    currentIdiomChainWord = null;
                    LogDebug("通过卡牌效果激活成语接龙模式");
                }

                return question;
            }
            catch (System.Exception e)
            {
                LogDebug($"生成指定类型题目失败: {e.Message}，使用默认生成");
                return GenerateNormalQuestion();
            }
        }

        /// <summary>
        /// 应用时间调整效果到题目
        /// </summary>
        private void ApplyTimeAdjustmentToQuestion(NetworkQuestionData question)
        {
            try
            {
                if (Cards.Integration.CardGameBridge.Instance != null && question != null)
                {
                    float originalTimeLimit = question.timeLimit;
                    Cards.Integration.CardGameBridge.Instance.OnTimerStarting(currentTurnPlayerId, ref question.timeLimit);
                }
            }
            catch (System.Exception e)
            {
                LogDebug($"应用时间调整失败: {e.Message}");
            }
        }
        /// <summary>
        /// 生成普通题目
        /// </summary>
        private NetworkQuestionData GenerateNormalQuestion()
        {
            var questionType = gameConfigManager.SelectRandomQuestionType();
            var question = GetQuestionFromService(questionType);

            // 检查是否激活成语接龙
            if (questionType == QuestionType.IdiomChain && enableIdiomChain)
            {
                isIdiomChainActive = true;
                idiomChainCount = 0;
                currentIdiomChainWord = null;
                LogDebug("激活成语接龙模式");
            }

            return question;
        }

        /// <summary>
        /// 生成成语接龙延续题目
        /// </summary>
        private NetworkQuestionData GenerateIdiomChainContinuation()
        {
            LogDebug($"生成接龙题目 - 基于: {currentIdiomChainWord}");

            var idiomManager = GetIdiomChainManager();
            if (idiomManager != null)
            {
                var question = idiomManager.CreateContinuationQuestion(currentIdiomChainWord, idiomChainCount, currentIdiomChainWord);
                if (question != null)
                {
                    float timeLimit = gameConfigManager.GetTimeLimitForQuestionType(QuestionType.IdiomChain);
                    question.timeLimit = timeLimit;
                    return question;
                }
            }

            LogDebug("成语接龙题目生成失败，重置接龙状态");
            ResetIdiomChainState();
            return null;
        }

        /// <summary>
        /// 从服务获取题目
        /// </summary>
        private NetworkQuestionData GetQuestionFromService(QuestionType questionType)
        {
            if (questionDataService == null)
            {
                Debug.LogError("[HostGameManager] QuestionDataService未初始化");
                return null;
            }

            try
            {
                var questionData = questionDataService.GetQuestionData(questionType);
                if (questionData != null)
                {
                    float timeLimit = gameConfigManager.GetTimeLimitForQuestionType(questionType);
                    questionData.timeLimit = timeLimit;
                    return questionData;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HostGameManager] 获取题目失败: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// 延迟题目广播
        /// </summary>
        private IEnumerator DelayedQuestionBroadcast(NetworkQuestionData question)
        {
            yield return new WaitForSeconds(0.1f);
            NetworkManager.Instance.BroadcastQuestion(question);
            LogDebug($"题目已发送 - 第{currentQuestionNumber}题");
        }

        #endregion

        #region 手牌处理
        /// <summary>
        /// 为所有存活玩家分发卡牌
        /// </summary>
        private void DistributeCardsToAllAlivePlayers(int cardCount)
        {
            var alivePlayerIds = playerStateManager?.GetAlivePlayerIds();
            if (alivePlayerIds == null || alivePlayerIds.Count == 0)
            {
                LogDebug("没有存活玩家，无需分发卡牌");
                return;
            }
            // 为每个存活玩家生成并发送卡牌
            foreach (var alivePlayerId in alivePlayerIds)
            {
                var newCards = new List<int>();

                // 生成指定数量的随机卡牌
                for (int i = 0; i < cardCount; i++)
                {
                    var randomCard = DrawRandomCardForPlayer(alivePlayerId);
                    newCards.Add(randomCard.cardId);
                    LogDebug($"为存活玩家 {alivePlayerId} 生成卡牌: {randomCard.cardName} (ID: {randomCard.cardId})");
                }

                NetworkManager.Instance.SendPlayerHandCards(alivePlayerId, newCards);
                LogDebug($"已发送 {newCards.Count} 张新卡牌给存活玩家 {alivePlayerId}");
            }

            LogDebug($"卡牌分发完成 - 为 {alivePlayerIds.Count} 名存活玩家每人分发了 {cardCount} 张卡牌");
        }

        /// <summary>
        /// 为指定玩家生成初始手牌
        /// </summary>
        private List<int> GeneratePlayerHandCards(ushort playerId)
        {
            var handCards = new List<int>();

            if (cardConfig == null)
            {
                Debug.LogError("[HostGameManager] CardConfig未配置");
                return handCards;
            }

            int initialCardCount = cardConfig.SystemSettings.startingCardCount;

            LogDebug($"为玩家 {playerId} 生成 {initialCardCount} 张初始手牌");

            // 生成指定数量的随机卡牌
            for (int i = 0; i < initialCardCount; i++)
            {
                var randomCard = DrawRandomCardForPlayer(playerId);
                if (randomCard != null)
                {
                    handCards.Add(randomCard.cardId);
                    LogDebug($"为玩家 {playerId} 生成卡牌: {randomCard.cardName} (ID: {randomCard.cardId})");
                }
                else
                {
                    Debug.LogWarning($"[HostGameManager] 为玩家 {playerId} 生成第 {i + 1} 张卡牌失败");
                }
            }

            LogDebug($"玩家 {playerId} 手牌生成完成: {handCards.Count}/{initialCardCount} 张");
            return handCards;
        }

        /// <summary>
        /// 为玩家抽取随机卡牌（Host端权威抽卡）
        /// </summary>
        private CardData DrawRandomCardForPlayer(ushort playerId)
        {
            if (cardConfig?.AllCards == null || cardConfig.AllCards.Count == 0)
            {
                Debug.LogError("[HostGameManager] 卡牌配置为空");
                return null;
            }

            // 获取可用卡牌列表（排除禁用卡牌）
            var availableCards = cardConfig.AllCards.Where(card =>
                !cardConfig.DrawSettings.bannedCardIds.Contains(card.cardId)).ToList();

            if (availableCards.Count == 0)
            {
                Debug.LogError("[HostGameManager] 没有可用的卡牌");
                return null;
            }

            // 使用CardConfig中配置的权重进行抽取
            var selectedCard = CardUtilities.DrawRandomCard(availableCards);

            return selectedCard;
        }

        /// <summary>
        /// 为游戏进行中新加入的玩家分发手牌
        /// </summary>
        private IEnumerator DistributeHandCardsToNewPlayer(ushort playerId)
        {
            yield return new WaitForSeconds(0.5f); // 等待玩家完全加入

            var handCards = GeneratePlayerHandCards(playerId);
            if (handCards != null && handCards.Count > 0)
            {
                NetworkManager.Instance.SendPlayerHandCards(playerId, handCards);
                LogDebug($"已为新玩家 {playerId} 分发手牌: {handCards.Count} 张");
            }
            else
            {
                Debug.LogError($"[HostGameManager] 为新玩家 {playerId} 生成手牌失败");
            }
        }
        #endregion

        #region 答案处理

        /// <summary>
        /// 处理玩家答案 - 添加卡牌系统通知
        /// </summary>
        public void HandlePlayerAnswer(ushort playerId, string answer)
        {
            if (!ValidateAnswerSubmission(playerId, answer))
                return;

            LogDebug($"处理答案 - 玩家{playerId}: {answer}");

            // 缓存答案
            playerAnswerCache[playerId] = answer;

            // 验证答案
            var validationResult = answerValidationManager.ValidateAnswer(answer, currentQuestion);

            // 立即广播结果
            NetworkManager.Instance.BroadcastPlayerAnswerResult(playerId, validationResult.isCorrect, answer);
            NetworkManager.Instance.BroadcastAnswerResult(validationResult.isCorrect, currentQuestion.correctAnswer);

            // 根据答题结果处理
            if (validationResult.isCorrect)
            {
                // 答对了，立即处理
                UpdatePlayerState(playerId, true);
            }
            else
            {
                // 答错了，延迟扣血（给老师扔粉笔的时间）
                StartCoroutine(DelayedDamageApplication(playerId));
            }

            // 检查游戏结束条件
            CheckGameEndConditions();

            // 如果游戏继续，切换到下一个玩家
            if (gameInProgress)
            {
                Invoke(nameof(NextPlayerTurn), turnChangeDelay);
            }
        }

        /// <summary>
        /// 延迟扣血协程
        /// </summary>
        private IEnumerator DelayedDamageApplication(ushort playerId)
        {
            // 等待老师扔粉笔的时间（可配置）
            float chalkThrowDelay = 3f; // 对应TeacherManager的完整动画时长
            yield return new WaitForSeconds(chalkThrowDelay);

            // 执行扣血
            UpdatePlayerState(playerId, false);
            LogDebug($"延迟扣血已执行 - 玩家{playerId}");
        }

        /// <summary>
        /// 验证答案提交
        /// </summary>
        private bool ValidateAnswerSubmission(ushort playerId, string answer)
        {
            if (!isInitialized || !gameInProgress || currentQuestion == null)
            {
                LogDebug($"无效的游戏状态 - initialized:{isInitialized}, inProgress:{gameInProgress}, hasQuestion:{currentQuestion != null}");
                return false;
            }

            if (playerId != currentTurnPlayerId)
            {
                LogDebug($"不是当前玩家回合 - 提交者:{playerId}, 当前回合:{currentTurnPlayerId}");
                return false;
            }

            if (string.IsNullOrWhiteSpace(answer))
            {
                LogDebug($"玩家{playerId}提交了空答案");
                return true;
            }

            return true;
        }

        /// <summary>
        /// 更新玩家状态
        /// </summary>
        private void UpdatePlayerState(ushort playerId, bool isCorrect)
        {
            if (!playerStateManager.ContainsPlayer(playerId))
                return;

            if (isCorrect)
            {
                HandleCorrectAnswer(playerId);
            }
            else
            {
                HandleWrongAnswer(playerId);
            }
        }

        /// <summary>
        /// 处理正确答案
        /// </summary>
        private void HandleCorrectAnswer(ushort playerId)
        {
            LogDebug($"玩家{playerId}答对了");

            // 处理成语接龙
            if (isIdiomChainActive && currentQuestion?.questionType == QuestionType.IdiomChain)
            {
                string playerAnswer = GetLastPlayerAnswer(playerId);
                if (!string.IsNullOrEmpty(playerAnswer))
                {
                    currentIdiomChainWord = playerAnswer.Trim();
                    idiomChainCount++;
                    LogDebug($"成语接龙更新: {currentIdiomChainWord} (第{idiomChainCount}个)");

                    if (idiomChainCount >= maxIdiomChain)
                    {
                        LogDebug("成语接龙达到最大长度，结束接龙");
                        ResetIdiomChainState();
                    }
                }
            }
        }

        /// <summary>
        /// 处理错误答案
        /// </summary>
        private void HandleWrongAnswer(ushort playerId)
        {
            LogDebug($"玩家{playerId}答错了");

            // 应用卡牌伤害倍数效果
            if (hpManager != null && hpManager.IsPlayerAlive(playerId))
            {
                // 获取基础伤害值
                int baseDamage = hpManager.GetEffectiveDamageAmount();

                // 应用卡牌倍数效果
                int finalDamage = ApplyCardDamageMultiplier(baseDamage);

                // 应用最终伤害
                bool success = hpManager.ApplyDamage(playerId, out int newHealth, out bool isDead, finalDamage);

                LogDebug($"玩家{playerId}扣血 - 基础伤害:{baseDamage}, 最终伤害:{finalDamage}, 新血量:{newHealth}, 是否死亡:{isDead}");
            }

            // 成语接龙答错时重置
            if (isIdiomChainActive && currentQuestion?.questionType == QuestionType.IdiomChain)
            {
                LogDebug("成语接龙答错，重置接龙状态");
                ResetIdiomChainState();
            }
        }
        /// <summary>
        /// 应用卡牌伤害倍数效果
        /// </summary>
        private int ApplyCardDamageMultiplier(int baseDamage)
        {
            try
            {
                if (Cards.Integration.CardGameBridge.Instance != null)
                {
                    int finalDamage = Cards.Integration.CardGameBridge.Instance.OnDamageCalculating(baseDamage);

                    if (finalDamage != baseDamage)
                    {
                        LogDebug($"伤害倍数已应用: {baseDamage} → {finalDamage}");
                    }

                    return finalDamage;
                }
            }
            catch (System.Exception e)
            {
                LogDebug($"应用伤害倍数失败: {e.Message}，使用基础伤害");
            }

            return baseDamage;
        }

        #endregion

        #region 回合管理

        /// <summary>
        /// 切换到下一个玩家
        /// </summary>
        private void NextPlayerTurn()
        {
            var alivePlayerIds = playerStateManager.GetAlivePlayerIds();

            if (alivePlayerIds.Count == 0)
            {
                EndGame("没有存活的玩家");
                return;
            }

            if (alivePlayerIds.Count == 1)
            {
                var winnerState = playerStateManager.GetPlayerState(alivePlayerIds[0]);
                EndGame($"{winnerState.playerName} 获胜！");
                return;
            }

            // 找到下一个存活玩家
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

            // 广播回合变更并发送新题目
            StartCoroutine(TurnChangeSequence());
        }

        /// <summary>
        /// 回合切换序列
        /// </summary>
        private IEnumerator TurnChangeSequence()
        {
            // 广播回合变更
            NetworkManager.Instance.BroadcastPlayerTurnChanged(currentTurnPlayerId);

            yield return new WaitForSeconds(0.3f);

            // 发送新题目
            GenerateAndSendQuestion();
        }

        /// <summary>
        /// 检查游戏结束条件
        /// </summary
        private void CheckGameEndConditions()
        {
            if (!gameInProgress || playerStateManager == null) return;

            var alivePlayerIds = playerStateManager.GetAlivePlayerIds();
            LogDebug($"存活玩家检查: {alivePlayerIds.Count}/{PlayerCount}");

            // 只剩一名玩家存活 - 游戏胜利
            if (alivePlayerIds.Count == 1)
            {
                var winnerState = playerStateManager.GetPlayerState(alivePlayerIds[0]);
                LogDebug($"游戏胜利: {winnerState.playerName}");

                EndGameWithWinner(winnerState.playerId, winnerState.playerName, "最后的幸存者！");
            }
            // 没有存活玩家
            else if (alivePlayerIds.Count == 0)
            {
                LogDebug("所有玩家都死亡");
                EndGameWithoutWinner("所有玩家都被淘汰了！");
            }
            // 所有玩家都离开了
            else if (PlayerCount == 0)
            {
                LogDebug("所有玩家都离开了房间");
                EndGameWithoutWinner("所有玩家都离开了");
            }
        }

        private void EndGameWithWinner(ushort winnerId, string winnerName, string reason)
        {
            if (!gameInProgress) return;

            LogDebug($"游戏胜利结束: 获胜者={winnerName} (ID: {winnerId}), 原因={reason}");

            // 广播胜利消息
            NetworkManager.Instance.BroadcastGameVictory(winnerId, winnerName, reason);

            // 结束游戏
            EndGame($"{winnerName} 获胜！");
        }

        private void EndGameWithoutWinner(string reason)
        {
            if (!gameInProgress) return;

            LogDebug($"游戏无胜利者结束: {reason}");

            // 广播无胜利者消息
            NetworkManager.Instance.BroadcastGameEndWithoutWinner(reason);

            // 结束游戏
            EndGame(reason);
        }
        #endregion

        #region 事件处理

        /// <summary>
        /// 玩家添加事件
        /// </summary>
        private void OnPlayerAdded(ushort playerId, string playerName, bool isHost)
        {
            LogDebug($"玩家加入: {playerName} (ID: {playerId}, Host: {isHost})");

            if (hpManager != null)
            {
                int initialHealth = hpManager.GetEffectiveInitialHealth();
                hpManager.InitializePlayer(playerId, initialHealth);
            }

            if (gameInProgress && PlayerCardManager.Instance != null)
            {
                LogDebug($"游戏进行中，为新加入的玩家 {playerId} 分发手牌");
                StartCoroutine(DistributeHandCardsToNewPlayer(playerId));
            }
        }

        /// <summary>
        /// 玩家移除事件
        /// </summary>
        private void OnPlayerRemoved(ushort playerId, string playerName)
        {
            LogDebug($"玩家离开: {playerName} (ID: {playerId})");

            // 移除HP管理
            hpManager?.RemovePlayer(playerId);

            // 如果是当前回合玩家离开，切换回合
            if (currentTurnPlayerId == playerId && gameInProgress)
            {
                NextPlayerTurn();
            }

            CheckGameEndConditions();
        }

        /// <summary>
        /// 玩家血量变更事件
        /// </summary>
        private void OnPlayerHealthChanged(ushort playerId, int newHealth, int maxHealth)
        {
            LogDebug($"玩家{playerId}血量变更: {newHealth}/{maxHealth}");

            // 更新玩家状态
            playerStateManager.UpdatePlayerHealth(playerId, newHealth, maxHealth);

            // 广播血量更新
            NetworkManager.Instance.BroadcastHealthUpdate(playerId, newHealth, maxHealth);
        }

        /// <summary>
        /// 玩家死亡事件
        /// </summary>
        private void OnPlayerDied(ushort playerId)
        {
            LogDebug($"玩家{playerId}死亡");

            // 更新存活状态
            playerStateManager.SetPlayerAlive(playerId, false);

            // 获取玩家状态以获取maxHealth
            var playerState = playerStateManager.GetPlayerState(playerId);
            int maxHealth = playerState?.maxHealth ?? 100;
            string playerName = playerState?.playerName ?? $"玩家{playerId}";

            // 广播血量更新（死亡状态）
            NetworkManager.Instance.BroadcastHealthUpdate(playerId, 0, maxHealth);
            NetworkManager.Instance.BroadcastPlayerDeath(playerId, playerName);
            int cardsToGive = cardConfig.SystemSettings.cardsReceivedOnElimination;
            // 为所有存活玩家发放2张卡牌
            DistributeCardsToAllAlivePlayers(cardsToGive);

            // 如果是当前回合玩家死亡，切换回合
            if (currentTurnPlayerId == playerId && gameInProgress)
            {
                LogDebug($"当前回合玩家{playerId}死亡，切换回合");
                Invoke(nameof(NextPlayerTurn), 1f);
            }

            CheckGameEndConditions();
        }


        #endregion

        #region 网络广播

        /// <summary>
        /// 广播游戏进度
        /// </summary>
        private void BroadcastGameProgress()
        {
            if (!isInitialized || NetworkManager.Instance == null) return;

            int aliveCount = playerStateManager.GetAlivePlayerCount();
            int questionType = currentQuestion != null ? (int)currentQuestion.questionType : -1;
            float timeLimit = currentQuestion?.timeLimit ?? 0f;

            NetworkManager.Instance.BroadcastGameProgress(
                currentQuestionNumber,
                aliveCount,
                currentTurnPlayerId,
                questionType,
                timeLimit
            );

            LogDebug($"广播游戏进度: 第{currentQuestionNumber}题, 存活{aliveCount}人");
        }

        /// <summary>
        /// 广播所有玩家状态
        /// </summary>
        private void BroadcastAllPlayerStates()
        {
            if (!isInitialized || NetworkManager.Instance == null) return;

            var allPlayerStates = playerStateManager.GetAllPlayerStates();
            foreach (var playerState in allPlayerStates.Values)
            {
                // 查找对应的Photon玩家来确定是否为Host
                bool isHost = false;
                foreach (var photonPlayer in PhotonNetwork.PlayerList)
                {
                    if (photonPlayer.ActorNumber == playerState.playerId)
                    {
                        isHost = photonPlayer.IsMasterClient;
                        break;
                    }
                }

                NetworkManager.Instance.BroadcastPlayerStateSync(
                    playerState.playerId,
                    playerState.playerName,
                    isHost,
                    playerState.health,
                    playerState.maxHealth,
                    playerState.isAlive
                );
            }

            LogDebug($"广播了{allPlayerStates.Count}个玩家的状态");
        }

        /// <summary>
        /// 广播游戏结束信息
        /// </summary>
        private void BroadcastGameEnd(string reason, ushort winnerId = 0)
        {
            if (NetworkManager.Instance == null) return;

            try
            {
                // 设置房间属性，标记游戏已结束
                var roomProps = new ExitGames.Client.Photon.Hashtable();
                roomProps["gameEnded"] = true;
                roomProps["gameEndReason"] = reason;
                roomProps["gameEndTime"] = PhotonNetwork.Time;

                if (winnerId > 0)
                {
                    roomProps["winnerId"] = (int)winnerId;
                    var winnerState = playerStateManager.GetPlayerState(winnerId);
                    if (winnerState != null)
                    {
                        roomProps["winnerName"] = winnerState.playerName;
                    }
                }

                PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);
                LogDebug($"房间属性已更新 - 游戏结束: {reason}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HostGameManager] 广播游戏结束失败: {e.Message}");
            }
        }

        #endregion

        #region IInRoomCallbacks实现

        void IInRoomCallbacks.OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            ushort playerId = (ushort)newPlayer.ActorNumber;
            string playerName = newPlayer.NickName ?? $"玩家{playerId}";

            LogDebug($"新玩家加入房间: {playerName} (ID: {playerId})");

            // 如果游戏未开始且管理器已初始化，添加玩家
            if (!gameInProgress && isInitialized && playerStateManager != null)
            {
                int initialHealth = hpManager?.GetEffectiveInitialHealth() ?? 100;
                playerStateManager.AddPlayer(playerId, playerName, initialHealth, initialHealth, false);
                hpManager?.InitializePlayer(playerId, initialHealth);
            }
        }

        void IInRoomCallbacks.OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            ushort playerId = (ushort)otherPlayer.ActorNumber;
            string playerName = otherPlayer.NickName ?? $"玩家{playerId}";

            LogDebug($"玩家离开房间: {playerName} (ID: {playerId})");

            if (isInitialized && playerStateManager != null)
            {
                playerStateManager.RemovePlayer(playerId);
                hpManager?.RemovePlayer(playerId);

                // 如果是当前回合玩家离开且游戏进行中
                if (currentTurnPlayerId == playerId && gameInProgress)
                {
                    NextPlayerTurn();
                }

                CheckGameEndConditions();
            }
        }

        void IInRoomCallbacks.OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
        {
            LogDebug($"MasterClient切换: {newMasterClient.NickName} (ID: {newMasterClient.ActorNumber})");

            if (PhotonNetwork.IsMasterClient)
            {
                LogDebug("本地玩家成为新的MasterClient");
                this.enabled = true;

                // **简单方案：如果游戏进行中，就结束游戏**
                if (gameInProgress)
                {
                    LogDebug("检测到游戏进行中，原Host离开，结束游戏");
                    EndGameWithoutWinner("原房主离开，游戏结束");
                }
                else if (!isInitialized)
                {
                    StartCoroutine(InitializeHostGameManager());
                }
            }
            else
            {
                LogDebug("其他玩家成为MasterClient，禁用HostGameManager");
                this.enabled = false;
            }
        }

        void IInRoomCallbacks.OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
        {
            // 可以在这里处理玩家属性更新，比如准备状态等
        }

        void IInRoomCallbacks.OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
        {
            // 处理房间属性更新，比如游戏结束通知
            if (propertiesThatChanged.ContainsKey("gameEnded"))
            {
                bool gameEnded = (bool)propertiesThatChanged["gameEnded"];
                if (gameEnded)
                {
                    string reason = propertiesThatChanged.ContainsKey("gameEndReason") ? (string)propertiesThatChanged["gameEndReason"] : "游戏结束";
                    LogDebug($"收到游戏结束通知: {reason}");
                }
            }
        }

        #endregion

        #region 返回房间
        /// <summary>
        /// 请求返回房间（供其他脚本调用
        /// </summary>
        public void RequestReturnToRoom(ushort requestPlayerId, string reason = "玩家请求")
        {
            LogDebug($"玩家{requestPlayerId}请求返回房间: {reason}");

            // 验证请求者权限（可以是死亡玩家或游戏结束后的任何玩家）
            bool canReturn = false;

            // 游戏结束后任何人都可以返回
            if (!gameInProgress)
            {
                canReturn = true;
                LogDebug($"游戏已结束，允许玩家{requestPlayerId}返回");
            }
            // 游戏进行中只允许死亡玩家返回
            else if (playerStateManager != null)
            {
                var playerState = playerStateManager.GetPlayerState(requestPlayerId);
                canReturn = playerState != null && !playerState.isAlive;
                LogDebug($"游戏进行中，玩家{requestPlayerId}存活状态: {playerState?.isAlive}, 允许返回: {canReturn}");
            }

            if (canReturn)
            {
                LogDebug($"准备广播返回房间请求 - NetworkManager可用: {NetworkManager.Instance != null}");

                if (NetworkManager.Instance != null)
                {
                    NetworkManager.Instance.BroadcastForceReturnToRoom($"玩家{requestPlayerId}请求返回: {reason}");
                    LogDebug($"✓ 已调用BroadcastForceReturnToRoom，将执行场景切换");
                }
                else
                {
                    LogDebug($"❌ NetworkManager.Instance为null，无法广播返回请求");
                }

                LogDebug($"已批准玩家{requestPlayerId}的返回房间请求");
            }
            else
            {
                LogDebug($"拒绝玩家{requestPlayerId}的返回房间请求 - 不满足条件");
            }
        }

        /// <summary>
        /// 强制所有玩家返回房间（游戏结束时使用）
        /// </summary>
        public void ForceReturnAllToRoom(float delay = 5f)
        {
            LogDebug($"将在{delay}秒后强制所有玩家返回房间");

            if (delay > 0)
            {
                Invoke(nameof(ExecuteForceReturn), delay);
            }
            else
            {
                ExecuteForceReturn();
            }
        }

        /// <summary>
        /// 执行强制返回
        /// </summary>
        private void ExecuteForceReturn()
        {
            LogDebug("执行强制返回房间");
            NetworkManager.Instance.BroadcastForceReturnToRoom("游戏已结束");
        }
        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取玩家最后一次答案
        /// </summary>
        private string GetLastPlayerAnswer(ushort playerId)
        {
            return playerAnswerCache.ContainsKey(playerId) ? playerAnswerCache[playerId] : null;
        }

        /// <summary>
        /// 获取成语接龙管理器
        /// </summary>
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

        /// <summary>
        /// 重置成语接龙状态
        /// </summary>
        private void ResetIdiomChainState()
        {
            isIdiomChainActive = false;
            currentIdiomChainWord = null;
            idiomChainCount = 0;
            LogDebug("成语接龙状态已重置");
        }

        /// <summary>
        /// 获取游戏统计信息
        /// </summary>
        public string GetGameStats()
        {
            var stats = "=== 优化后的游戏统计 ===\n";
            stats += $"游戏状态: {(gameInProgress ? "进行中" : "未开始")}\n";
            stats += $"初始化状态: {isInitialized}\n";
            stats += $"当前回合玩家: {currentTurnPlayerId}\n";
            stats += $"当前题目编号: {currentQuestionNumber}\n";
            stats += $"玩家总数: {PlayerCount}\n";
            stats += $"存活玩家数: {playerStateManager?.GetAlivePlayerCount() ?? 0}\n";
            stats += $"成语接龙活跃: {isIdiomChainActive}\n";
            stats += $"成语接龙计数: {idiomChainCount}\n";
            stats += $"当前成语: {currentIdiomChainWord ?? "无"}\n";

            if (currentQuestion != null)
            {
                stats += $"当前题目类型: {currentQuestion.questionType}\n";
                stats += $"当前题目时限: {currentQuestion.timeLimit}秒\n";
            }

            return stats;
        }

        /// <summary>
        /// 获取简化状态信息
        /// </summary>
        public string GetStatusInfo()
        {
            return $"Initialized: {isInitialized}, " +
                   $"GameInProgress: {gameInProgress}, " +
                   $"PlayerCount: {PlayerCount}, " +
                   $"CurrentTurn: {currentTurnPlayerId}, " +
                   $"QuestionNumber: {currentQuestionNumber}, " +
                   $"IsMasterClient: {PhotonNetwork.IsMasterClient}";
        }

        #endregion

        #region 公共接口方法

        /// <summary>
        /// 外部启动游戏接口（从RoomScene调用）
        /// </summary>
        public void StartGameFromRoom()
        {
            LogDebug("从房间场景启动游戏");

            if (!isInitialized)
            {
                LogDebug("HostGameManager未初始化，等待初始化完成");
                StartCoroutine(WaitForInitializationAndStart());
                return;
            }

            StartGame();
        }

        /// <summary>
        /// 等待初始化完成并启动游戏
        /// </summary>
        private IEnumerator WaitForInitializationAndStart()
        {
            float timeout = 10f;
            float elapsed = 0f;

            while (!isInitialized && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (isInitialized)
            {
                StartGame();
            }
            else
            {
                Debug.LogError("[HostGameManager] 初始化超时，无法启动游戏");
            }
        }


        /// <summary>
        /// 强制结束游戏
        /// </summary>
        public void ForceEndGame(string reason = "游戏被强制结束")
        {
            LogDebug($"强制结束游戏: {reason}");
            EndGame(reason);
        }

        #endregion

        #region 调试方法

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[HostGameManager] {message}");
            }
        }
        #endregion
    }
}