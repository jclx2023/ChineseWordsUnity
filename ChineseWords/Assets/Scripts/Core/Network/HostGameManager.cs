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
    /// 修复网络同步问题的Host游戏管理器
    /// 主要修复：1. 从房间系统正确同步玩家数据 2. 简化游戏开始逻辑
    /// </summary>
    public class HostGameManager : MonoBehaviour
    {
        [Header("游戏配置")]
        [SerializeField] private float questionTimeLimit = 30f;
        [SerializeField] private int initialPlayerHealth = 100;
        [SerializeField] private int damagePerWrongAnswer = 20;
        [SerializeField] private bool autoStartGame = true;

        [Header("双层架构设置")]
        [SerializeField] private bool isDualLayerArchitecture = true;
        [SerializeField] private bool isServerLayerOnly = false; // 是否只作为服务器层运行

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        [Header("题目配置")]
        [SerializeField] private QuestionWeightConfig questionWeightConfig;

        [Header("Timer配置")]
        [SerializeField] private TimerConfig timerConfig;

        [Header("HP配置")]
        [SerializeField] private HPConfig hpConfig;
        [SerializeField] private bool useCustomHPConfig = false;

        // HP管理器
        private PlayerHPManager hpManager;

        [Header("成语接龙设置")]
        [SerializeField] private bool enableIdiomChain = true;

        private bool isIdiomChainActive = false;
        private string currentIdiomChainWord = null;
        private int idiomChainCount = 0;
        private const int MAX_IDIOM_CHAIN = 10;

        // 双层架构相关
        private NetworkManager serverNetworkManager;    // ID=0, 纯服务器
        private NetworkManager playerHostNetworkManager; // ID=1, 玩家Host

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

        // 题目相关 - 现在使用服务而不是直接管理
        private QuestionDataService questionDataService;
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
                LogDebug("HostGameManager 单例已创建");
            }
            else
            {
                LogDebug("销毁重复的HostGameManager实例");
                Destroy(gameObject);
                return;
            }

            // 检查是否为双层架构
            CheckDualLayerArchitecture();
        }

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

            // 检查当前是否为服务器层
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
                // 尝试通过NetworkManager判断
                var localNetworkManager = GetComponentInParent<NetworkManager>() ?? FindObjectOfType<NetworkManager>();
                if (localNetworkManager != null)
                {
                    isServerLayerOnly = (localNetworkManager.ClientId == 0);
                    LogDebug($"通过NetworkManager ClientId判断层级: {(isServerLayerOnly ? "服务器层" : "玩家层")}");
                }
            }

            // 查找双层架构中的NetworkManager实例
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

        private void Start()
        {
            LogDebug($"[HostGameManager]Start 执行时间: {Time.time}");

            // 检查是否应该激活（只在确定的游戏模式下）
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
        /// 初始化主机游戏的协程（修复重复房主问题）
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

            // 初始化服务依赖
            yield return StartCoroutine(InitializeServices());

            if (questionDataService == null)
            {
                Debug.LogError("[HostGameManager] QuestionDataService 初始化失败");
                this.enabled = false;
                yield break;
            }

            // 新增：初始化HP管理器
            yield return StartCoroutine(InitializeHPManager());

            // 记录NetworkManager的当前状态
            if (NetworkManager.Instance != null)
            {
                LogDebug($"NetworkManager状态 - ClientId: {NetworkManager.Instance.ClientId}, IsHost: {NetworkManager.Instance.IsHost}, IsConnected: {NetworkManager.Instance.IsConnected}");
            }

            // 关键修复：从房间系统同步玩家数据
            SyncPlayersFromRoomSystem();

            // 标记为已初始化（在同步玩家后立即标记，避免网络事件重复添加）
            isInitialized = true;
            LogDebug($"主机游戏初始化完成，当前玩家数: {playerStates.Count}");

            LogDebug("跳过Host自动添加，完全依赖房间数据");

            ValidateHostCount();

            if (autoStartGame && playerStates.Count > 0)
            {
                LogDebug("从房间系统进入，直接开始游戏");
                Invoke(nameof(StartHostGame), gameStartDelay);
            }
        }


        /// <summary>
        /// 从房间系统同步玩家数据（修复重复房主问题）
        /// </summary>
        private void SyncPlayersFromRoomSystem()
        {
            LogDebug("同步房间系统玩家数据");

            // 清空现有玩家状态，避免重复
            playerStates.Clear();

            // 方法1：从RoomManager获取
            if (RoomManager.Instance?.CurrentRoom != null)
            {
                var room = RoomManager.Instance.CurrentRoom;
                LogDebug($"从RoomManager同步，房间玩家数: {room.players.Count}");

                foreach (var roomPlayer in room.players.Values)
                {
                    AddPlayerFromRoom(roomPlayer.playerId, roomPlayer.playerName);
                }

                LogDebug($"房间玩家同步完成，总计玩家数: {playerStates.Count}");

                // 验证Host数量
                ValidateHostCount();
                return;
            }

            // 方法2：从NetworkManager的连接状态获取
            if (NetworkManager.Instance != null)
            {
                LogDebug($"从NetworkManager同步，连接玩家数: {NetworkManager.Instance.ConnectedPlayerCount}");

                // **关键修复：使用统一的Host玩家ID接口**
                ushort hostPlayerId = NetworkManager.Instance.GetHostPlayerId();
                if (hostPlayerId != 0 && NetworkManager.Instance.IsHostClientReady)
                {
                    AddPlayerFromRoom(hostPlayerId, "房主");
                    LogDebug($"添加Host玩家: ID={hostPlayerId}");
                }
                else
                {
                    LogDebug($"Host玩家尚未准备就绪: ID={hostPlayerId}, Ready={NetworkManager.Instance.IsHostClientReady}");
                }
            }

            LogDebug($"玩家同步完成，总计玩家数: {playerStates.Count}");
            ValidateHostCount();
        }

        /// <summary>
        /// 验证房主数量（调试用）
        /// </summary>
        private void ValidateHostCount()
        {
            var hostPlayers = playerStates.Values.Where(p => p.playerName.Contains("房主")).ToList();

            if (hostPlayers.Count > 1)
            {
                Debug.LogError($"[HostGameManager] 检测到多个房主: {hostPlayers.Count} 个");

                foreach (var host in hostPlayers)
                {
                    LogDebug($"重复房主: ID={host.playerId}, Name={host.playerName}");
                }

                // 修复重复Host
                FixDuplicateHosts();
            }
            else if (hostPlayers.Count == 1)
            {
                LogDebug($"Host验证通过: ID={hostPlayers[0].playerId}");

                // **验证与NetworkManager的一致性**
                if (NetworkManager.Instance != null)
                {
                    ushort networkHostId = NetworkManager.Instance.GetHostPlayerId();
                    if (hostPlayers[0].playerId != networkHostId)
                    {
                        Debug.LogError($"[HostGameManager] Host ID不一致！游戏中: {hostPlayers[0].playerId}, NetworkManager: {networkHostId}");
                    }
                }
            }
            else
            {
                Debug.LogWarning("[HostGameManager] 没有找到房主");
            }
        }


        /// <summary>
        /// 修复重复房主问题（增强版）
        /// </summary>
        private void FixDuplicateHosts()
        {
            if (NetworkManager.Instance == null) return;

            ushort correctHostId = NetworkManager.Instance.GetHostPlayerId();
            LogDebug($"修复重复房主，正确的Host ID: {correctHostId}");

            // 获取所有房主玩家
            var hostPlayers = playerStates.Where(p => p.Value.playerName.Contains("房主")).ToList();

            if (hostPlayers.Count <= 1)
            {
                LogDebug("房主数量正常，无需修复");
                return;
            }

            LogDebug($"发现 {hostPlayers.Count} 个房主，开始修复");

            // 保留正确ID的房主，移除其他的
            var correctHost = hostPlayers.FirstOrDefault(h => h.Key == correctHostId);

            if (correctHost.Value != null)
            {
                LogDebug($"保留正确房主: ID={correctHost.Key}, Name={correctHost.Value.playerName}");

                // 移除其他房主
                var duplicateHosts = hostPlayers.Where(h => h.Key != correctHostId).ToList();
                foreach (var duplicateHost in duplicateHosts)
                {
                    playerStates.Remove(duplicateHost.Key);
                    LogDebug($"移除重复房主: ID={duplicateHost.Key}, Name={duplicateHost.Value.playerName}");
                }
            }
            else
            {
                // 如果没有找到正确ID的房主，保留最小ID的房主
                var primaryHost = hostPlayers.OrderBy(h => h.Key).First();
                var duplicateHosts = hostPlayers.Skip(1).ToList();

                LogDebug($"保留主房主: ID={primaryHost.Key}, 移除重复房主: {string.Join(", ", duplicateHosts.Select(h => h.Key))}");

                foreach (var duplicateHost in duplicateHosts)
                {
                    playerStates.Remove(duplicateHost.Key);
                    LogDebug($"移除重复房主: ID={duplicateHost.Key}, Name={duplicateHost.Value.playerName}");
                }
            }

            // 重新验证
            LogDebug($"修复完成，当前玩家数: {playerStates.Count}");
            ValidateHostCount();
        }

        private void AddPlayerFromRoom(ushort playerId, string playerName)
        {
            LogDebug($"=== AddPlayerFromRoom开始 === 玩家ID: {playerId}, 名称: {playerName}");

            if (playerStates.ContainsKey(playerId))
            {
                LogDebug($"玩家 {playerId} 已存在，跳过添加");
                return;
            }

            bool isHostPlayer = NetworkManager.Instance?.IsHostPlayer(playerId) ?? false;
            int initialHealth = hpManager?.GetEffectiveInitialHealth() ?? initialPlayerHealth;

            playerStates[playerId] = new PlayerGameState
            {
                playerId = playerId,
                playerName = isHostPlayer ? "房主" : playerName,
                health = initialHealth,
                maxHealth = initialHealth,
                isAlive = true,
                isReady = true
            };

            // 在HP管理器中初始化玩家
            if (hpManager != null)
            {
                hpManager.InitializePlayer(playerId, initialHealth);
                LogDebug($"在HP管理器中初始化玩家 {playerId}");
            }
            else
            {
                LogDebug("hpManager为空，无法初始化玩家HP");
            }

            LogDebug($"=== AddPlayerFromRoom完成 === 玩家: {playerName}, HP: {initialHealth}/{initialHealth}");
        }

        /// <summary>
        /// 初始化服务依赖
        /// </summary>
        private IEnumerator InitializeServices()
        {
            LogDebug("初始化服务依赖...");

            // 1. 获取或创建 QuestionDataService
            questionDataService = QuestionDataService.Instance;
            if (questionDataService == null)
            {
                // 在场景中查找
                questionDataService = FindObjectOfType<QuestionDataService>();

                if (questionDataService == null)
                {
                    // 创建新的服务实例
                    GameObject serviceObj = new GameObject("QuestionDataService");
                    questionDataService = serviceObj.AddComponent<QuestionDataService>();
                    LogDebug("创建了新的 QuestionDataService 实例");
                }
                else
                {
                    LogDebug("找到现有的 QuestionDataService 实例");
                }
            }

            // 2. 查找题目控制器引用
            yield return StartCoroutine(FindQuestionController());

            // 3. 预加载常用的数据提供者（可选优化）
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

        /// <summary>
        /// 添加玩家（防止重复添加）
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
                LogDebug($"玩家 {playerId} 已存在，跳过重复添加");
                return;
            }

            // 获取HP管理器的初始血量
            int initialHealth = hpManager?.GetEffectiveInitialHealth() ?? initialPlayerHealth;

            playerStates[playerId] = new PlayerGameState
            {
                playerId = playerId,
                playerName = playerName ?? $"玩家{playerId}",
                health = initialHealth,
                isAlive = true,
                isReady = false
            };

            // 在HP管理器中初始化玩家
            if (hpManager != null)
            {
                hpManager.InitializePlayer(playerId, initialHealth);
                LogDebug($"在HP管理器中初始化玩家 {playerId}");
            }

            LogDebug($"添加玩家: {playerId} ({playerName}), 当前玩家数: {playerStates.Count}, HP: {initialHealth}");
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

            // 从HP管理器中移除玩家
            if (hpManager != null)
            {
                hpManager.RemovePlayer(playerId);
                LogDebug($"从HP管理器中移除玩家 {playerId}");
            }

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
        /// 准备游戏数据（新增方法）
        /// </summary>
        private void PrepareGameData()
        {
            LogDebug("准备游戏数据...");

            // 同步房间系统的玩家数据
            SyncPlayersFromRoomSystem();

            // 预加载题目数据
            if (questionDataService != null)
            {
                LogDebug("1预加载题目数据提供者...");
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
        /// 验证游戏组件（新增方法）
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

            if (playerStates == null || playerStates.Count == 0)
            {
                Debug.LogError("玩家数据为空");
                isValid = false;
            }

            return isValid;
        }

        /// <summary>
        /// 生成并发送题目（修复：使用正确的NQMC接口）
        /// </summary>
        private void GenerateAndSendQuestion()
        {
            if (!gameInProgress || !isInitialized)
                return;

            LogDebug("开始生成新题目");
            NetworkQuestionData question = null;
            if (isIdiomChainActive && !string.IsNullOrEmpty(currentIdiomChainWord))
            {
                // 生成接龙题目，传递状态信息
                question = GenerateIdiomChainContinuation(currentIdiomChainWord);
                LogDebug($"生成成语接龙题目，基于: {currentIdiomChainWord}");
            }
            else
            {
                // 普通题目生成
                var questionType = SelectRandomQuestionType();
                question = GetQuestionFromService(questionType);

                // 如果是成语接龙题，激活接龙模式
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
                currentQuestion = question;
                BroadcastQuestion(question);
                LogDebug($"题目已发送: {question.questionType} - {question.questionText}");
            }
            else
            {
                Debug.LogError($"题目生成失败，重新尝试");
                Invoke(nameof(GenerateAndSendQuestion), 1f);
            }
        }

        /// <summary>
        /// 从服务获取题目数据
        /// </summary>
        /// <summary>
        /// 从服务获取题目数据
        /// </summary>
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
                    // 关键修改：设置动态时间限制
                    float dynamicTimeLimit = GetTimeLimitForQuestionType(questionType);
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

        /// <summary>
        /// 创建备用题目（当服务失败时）
        /// </summary>
        private NetworkQuestionData CreateFallbackQuestion(QuestionType questionType)
        {
            // 关键修改：使用动态时间限制而不是固定值
            float dynamicTimeLimit = GetTimeLimitForQuestionType(questionType);

            return NetworkQuestionDataExtensions.CreateFromLocalData(
                questionType,
                $"这是一个{questionType}类型的备用题目",
                "备用答案",
                questionType == QuestionType.ExplanationChoice || questionType == QuestionType.SimularWordChoice
                    ? new string[] { "选项A", "备用答案", "选项C", "选项D" }
                    : null,
                dynamicTimeLimit, // 使用动态时间限制
                "{\"source\": \"host_fallback\", \"isDefault\": true}"
            );
        }


        /// <summary>
        /// 选择随机题目类型
        /// </summary>
        private QuestionType SelectRandomQuestionType()
        {
            // 使用统一的权重管理器
            if (questionWeightConfig != null)
            {
                return questionWeightConfig.SelectRandomType();
            }

            // 回退到权重管理器的全局配置
            return QuestionWeightManager.SelectRandomQuestionType();
        }

        private Dictionary<QuestionType, float> GetDefaultTypeWeights()
        {
            return QuestionWeightManager.GetWeights();
        }
        /// <summary>
        /// 获取指定题型的时间限制
        /// </summary>
        private float GetTimeLimitForQuestionType(QuestionType questionType)
        {
            // 优先使用本地配置
            if (timerConfig != null)
            {
                float timeLimit = timerConfig.GetTimeLimitForQuestionType(questionType);
                LogDebug($"使用本地Timer配置: {questionType} -> {timeLimit}秒");
                return timeLimit;
            }

            // 回退到全局配置管理器
            if (TimerConfigManager.Config != null)
            {
                float timeLimit = TimerConfigManager.Config.GetTimeLimitForQuestionType(questionType);
                LogDebug($"使用TimerConfigManager配置: {questionType} -> {timeLimit}秒");
                return timeLimit;
            }

            // 最终回退到默认值
            float defaultTime = GetDefaultTimeLimitForQuestionType(questionType);
            LogDebug($"使用默认时间配置: {questionType} -> {defaultTime}秒");
            return defaultTime;
        }
        /// <summary>
        /// 获取题型的默认时间限制
        /// </summary>
        private float GetDefaultTimeLimitForQuestionType(QuestionType questionType)
        {
            switch (questionType)
            {
                case QuestionType.ExplanationChoice:
                case QuestionType.SimularWordChoice:
                    return 20f;
                case QuestionType.HardFill:
                    return 30f;
                case QuestionType.SoftFill:
                case QuestionType.TextPinyin:
                    return 25f;
                case QuestionType.IdiomChain:
                    return 20f;
                case QuestionType.SentimentTorF:
                case QuestionType.UsageTorF:
                    return 15f;
                case QuestionType.HandWriting:
                    return 60f;
                default:
                    return questionTimeLimit; // 使用原有的默认值
            }
        }

        /// <summary>
        /// 检查游戏结束条件（使用HP管理器）
        /// </summary>
        private void CheckGameEndConditions()
        {
            if (!gameInProgress)
                return;

            List<ushort> alivePlayers;

            // 优先使用HP管理器获取存活玩家
            if (hpManager != null)
            {
                alivePlayers = hpManager.GetAlivePlayerIds();
                LogDebug($"HP管理器报告存活玩家数: {alivePlayers.Count}");
            }
            else
            {
                // 回退到原有逻辑
                alivePlayers = playerStates.Where(p => p.Value.isAlive).Select(p => p.Key).ToList();
                LogDebug($"原有逻辑检测存活玩家数: {alivePlayers.Count}");
            }

            if (alivePlayers.Count == 0)
            {
                LogDebug("游戏结束：没有存活的玩家");
                EndGame("游戏结束：所有玩家都被淘汰");
            }
            else if (alivePlayers.Count == 1)
            {
                var winnerId = alivePlayers[0];
                var winnerName = playerStates.ContainsKey(winnerId) ?
                    playerStates[winnerId].playerName : $"玩家{winnerId}";

                LogDebug($"游戏结束：玩家 {winnerId} ({winnerName}) 获胜");
                EndGame($"游戏结束：{winnerName} 获胜！");
            }
            else if (playerStates.Count == 0)
            {
                LogDebug("游戏结束：没有玩家");
                EndGame("游戏结束：所有玩家都离开了");
            }
        }

        /// <summary>
        /// 开始主机游戏（修复：确保NQMC也启动）
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

            LogDebug($"主机开始游戏，玩家数: {playerStates.Count}");

            // 检查是否有玩家
            if (playerStates.Count == 0)
            {
                Debug.LogError("没有玩家可以开始游戏 - 玩家列表为空");
                return;
            }

            gameInProgress = true;

            // **关键修改：确保NQMC也启动多人游戏模式**
            if (NetworkQuestionManagerController.Instance != null)
            {
                LogDebug("启动NQMC多人游戏模式");
                NetworkQuestionManagerController.Instance.StartGame(true); // 多人模式
            }
            else
            {
                Debug.LogError("NetworkQuestionManagerController.Instance为空，无法启动题目管理");
            }

            // 选择第一个存活的玩家开始（修复ID=0的问题）
            var alivePlayers = playerStates.Where(p => p.Value.isAlive).ToList();
            if (alivePlayers.Count > 0)
            {
                currentTurnPlayerId = alivePlayers.First().Key;
                LogDebug($"选择玩家 {currentTurnPlayerId} ({playerStates[currentTurnPlayerId].playerName}) 开始游戏");

                BroadcastPlayerTurnChanged(currentTurnPlayerId);

                // 生成第一题
                Invoke(nameof(GenerateAndSendQuestion), 1f);
            }
            else
            {
                Debug.LogError("没有存活的玩家可以开始游戏");
                gameInProgress = false;
            }
        }
        /// <summary>
        /// 添加新的公共方法：场景切换完成后的初始化
        /// </summary>
        public void OnGameSceneLoaded()
        {
            LogDebug("游戏场景加载完成，启动游戏逻辑");

            if (isInitialized && !gameInProgress)
            {
                // 延迟启动，确保所有组件都已就绪
                Invoke(nameof(StartHostGame), 1f);
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
            ResetIdiomChainState();
            playerAnswerCache.Clear();

            // 这里可以添加发送游戏结束消息给所有客户端
            // BroadcastGameEnd(reason);
        }

        /// <summary>
        /// 处理玩家答案
        /// </summary>
        public void HandlePlayerAnswer(ushort playerId, string answer)
        {
            playerAnswerCache[playerId] = answer;

            if (!isInitialized || !gameInProgress || currentQuestion == null)
            {
                LogDebug($"无效的答案提交状态: initialized={isInitialized}, gameInProgress={gameInProgress}, hasQuestion={currentQuestion != null}");
                return;
            }

            if (!isInitialized || !gameInProgress || currentQuestion == null)
            {
                LogDebug($"无效的答案提交状态");
                return;
            }

            // **使用统一接口验证玩家身份**
            if (!playerStates.ContainsKey(playerId))
            {
                LogDebug($"未知玩家提交答案: {playerId}");
                return;
            }

            // 检查是否轮到当前玩家
            if (playerId != currentTurnPlayerId)
            {
                LogDebug($"不是当前玩家的回合: 提交者={playerId}, 当前回合={currentTurnPlayerId}");
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
        /// 更新玩家状态（使用HP管理器）
        /// </summary>
        private void UpdatePlayerState(ushort playerId, bool isCorrect)
        {
            if (!playerStates.ContainsKey(playerId))
                return;

            var playerState = playerStates[playerId];

            if (isCorrect)
            {
                LogDebug($"玩家 {playerId} 答对了");

                // 处理成语接龙逻辑
                if (isIdiomChainActive && currentQuestion?.questionType == QuestionType.IdiomChain)
                {
                    string playerAnswer = GetLastPlayerAnswer(playerId);
                    if (!string.IsNullOrEmpty(playerAnswer))
                    {
                        currentIdiomChainWord = playerAnswer.Trim();
                        idiomChainCount++;
                        LogDebug($"成语接龙更新: {currentIdiomChainWord} (第{idiomChainCount}个)");

                        // 检查是否达到最大接龙数
                        if (idiomChainCount >= MAX_IDIOM_CHAIN)
                        {
                            LogDebug("成语接龙达到最大长度，结束接龙模式");
                            ResetIdiomChainState();
                        }
                    }
                }
            }
            else
            {
                // 使用HP管理器处理伤害
                if (hpManager != null && hpManager.IsPlayerAlive(playerId))
                {
                    bool damageApplied = hpManager.ApplyDamage(playerId, out int newHealth, out bool isDead);

                    if (damageApplied)
                    {
                        LogDebug($"玩家 {playerId} 答错，HP管理器处理伤害 - 新血量: {newHealth}, 是否死亡: {isDead}");

                        // HP管理器会通过事件自动更新playerState和发送网络消息
                        // 这里不需要手动处理
                    }
                    else
                    {
                        LogDebug($"玩家 {playerId} 伤害处理失败，可能已经死亡");
                    }
                }
                else
                {
                    // 回退到原有逻辑（向后兼容）
                    LogDebug($"HP管理器不可用，使用原有扣血逻辑");

                    int damageAmount = damagePerWrongAnswer;
                    playerState.health -= damageAmount;

                    if (playerState.health <= 0)
                    {
                        playerState.health = 0;
                        playerState.isAlive = false;
                        LogDebug($"玩家 {playerId} 被淘汰");
                    }
                    else
                    {
                        LogDebug($"玩家 {playerId} 答错，生命值: {playerState.health}");
                    }

                    BroadcastHealthUpdate(playerId, playerState.health);
                }

                // 成语接龙答错时重置（可选）
                if (isIdiomChainActive && currentQuestion?.questionType == QuestionType.IdiomChain)
                {
                    LogDebug("成语接龙答错，重置接龙状态");
                    ResetIdiomChainState();
                }
            }
        }
        private NetworkQuestionData GenerateIdiomChainContinuation(string baseIdiom)
        {
            LogDebug($"生成基于 '{baseIdiom}' 的接龙题目");

            // 获取成语接龙管理器
            var idiomManager = GetIdiomChainManager();

            if (idiomManager != null)
            {
                var question = idiomManager.CreateContinuationQuestion(baseIdiom, idiomChainCount, currentIdiomChainWord);

                if (question != null)
                {
                    // 关键修改：为接龙题目设置正确的时间限制
                    float dynamicTimeLimit = GetTimeLimitForQuestionType(QuestionType.IdiomChain);
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

        private Dictionary<ushort, string> playerAnswerCache = new Dictionary<ushort, string>();

        private string GetLastPlayerAnswer(ushort playerId)
        {
            return playerAnswerCache.ContainsKey(playerId) ? playerAnswerCache[playerId] : null;
        }

        /// <summary>
        /// 验证答案
        /// </summary>
        private bool ValidateAnswer(string answer, NetworkQuestionData question)
        {
            if (question.questionType == QuestionType.IdiomChain)
            {
                // 成语接龙特殊验证
                return ValidateIdiomChainAnswer(answer, question);
            }
            else if (question.questionType == QuestionType.HardFill)
            {
                // 硬性填空题验证
                return ValidateHardFillAnswer(answer, question);
            }

            // 其他题型的普通验证
            return answer.Trim().Equals(question.correctAnswer.Trim(), System.StringComparison.OrdinalIgnoreCase);
        }
        /// <summary>
        /// 验证硬性填空题答案 - 委托给HardFill管理器
        /// </summary>
        private bool ValidateHardFillAnswer(string answer, NetworkQuestionData question)
        {
            LogDebug($"委托验证硬填空答案: {answer}");

            try
            {
                // 调用HardFill管理器的静态验证方法
                bool isValid = HardFillQuestionManager.ValidateAnswerStatic(answer, question);

                LogDebug($"硬填空验证结果: {isValid}");
                return isValid;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HostGameManager] 委托验证硬填空答案时发生错误: {e.Message}");
                return false;
            }
        }
        /// <summary>
        /// 验证成语接龙答案
        /// </summary>
        private bool ValidateIdiomChainAnswer(string answer, NetworkQuestionData question)
        {
            // 从题目数据中获取题干成语
            string baseIdiom = GetBaseIdiomFromQuestion(question);

            if (string.IsNullOrEmpty(baseIdiom))
            {
                LogDebug("无法获取题干成语，验证失败");
                return false;
            }

            var idiomManager = GetIdiomChainManager();

            if (idiomManager != null)
            {
                // 调用验证方法
                bool result = idiomManager.ValidateIdiomChain(answer, baseIdiom);
                LogDebug($"成语接龙验证结果: {answer} (基于: {baseIdiom}) -> {result}");
                return result;
            }
            else return false;
        }

        /// <summary>
        /// 获取IdiomChainQuestionManager实例
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

                // 方法2：直接查找场景中的实例
                return FindObjectOfType<IdiomChainQuestionManager>();
            }
            catch (System.Exception e)
            {
                LogDebug($"获取IdiomChainQuestionManager失败: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从题目数据中获取题干成语
        /// </summary>
        private string GetBaseIdiomFromQuestion(NetworkQuestionData question)
        {
            try
            {
                if (!string.IsNullOrEmpty(question.additionalData))
                {
                    var additionalInfo = JsonUtility.FromJson<GameLogic.FillBlank.IdiomChainAdditionalData>(question.additionalData);
                    return additionalInfo.currentIdiom;
                }
            }
            catch (System.Exception e)
            {
                LogDebug($"获取题干成语失败: {e.Message}");
            }

            return "";
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
        /// 广播题目（双层架构适配）
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
                // 双层架构：服务器层广播题目
                BroadcastQuestionDualLayer(question);
            }
            else
            {
                // 传统架构：直接广播
                BroadcastQuestionTraditional(question);
            }
            SyncAllPlayersHealth();
        }
        private void SyncAllPlayersHealth()
        {
            if (!isInitialized || NetworkManager.Instance == null)
                return;

            LogDebug("同步所有玩家血量状态...");

            foreach (var playerState in playerStates.Values)
            {
                SyncPlayerHealth(playerState.playerId);
            }
        }
        private void SyncPlayerHealth(ushort playerId)
        {
            if (!playerStates.ContainsKey(playerId))
                return;

            var playerState = playerStates[playerId];
            int currentHealth = playerState.health;
            int maxHealth = playerState.maxHealth;

            // 从HP管理器获取更准确的数据
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

            // 发送血量同步消息
            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.HealthUpdate);
            message.AddUShort(playerId);
            message.AddInt(currentHealth);
            message.AddInt(maxHealth);
            NetworkManager.Instance.BroadcastMessage(message);
        }

        /// <summary>
        /// 双层架构的题目广播
        /// </summary>
        private void BroadcastQuestionDualLayer(NetworkQuestionData question)
        {
            LogDebug("双层架构：服务器层广播题目");

            // 1. 通过网络广播给所有外部客户端
            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.SendQuestion);
            message.AddBytes(question.Serialize());

            // 使用服务器层的NetworkManager广播
            if (serverNetworkManager != null)
            {
                serverNetworkManager.BroadcastMessage(message);
                LogDebug("题目已通过服务器层广播给外部客户端");
            }

            // 2. 通过双层架构管理器发送给本地玩家Host层
            if (DualLayerArchitectureManager.Instance != null)
            {
                DualLayerArchitectureManager.Instance.ServerToPlayerHostQuestion(question);
                LogDebug("题目已发送给本地玩家Host层");
            }
            else
            {
                Debug.LogError("双层架构管理器不存在，无法发送题目给玩家Host层");
            }
        }

        /// <summary>
        /// 传统架构的题目广播
        /// </summary>
        private void BroadcastQuestionTraditional(NetworkQuestionData question)
        {
            LogDebug("传统架构：直接广播题目");

            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.SendQuestion);
            message.AddBytes(question.Serialize());
            NetworkManager.Instance.BroadcastMessage(message);

            LogDebug("题目已通过网络广播");

            // 确保Host也接收到题目
            if (NetworkManager.Instance.IsHost)
            {
                TriggerHostQuestionReceive(NetworkQuestionManagerController.Instance, question);
            }
        }

        /// <summary>
        /// 触发Host的题目接收（通过反射或公共接口）
        /// </summary>
        private void TriggerHostQuestionReceive(NetworkQuestionManagerController nqmc, NetworkQuestionData question)
        {
            try
            {
                LogDebug("尝试触发Host的题目接收");

                // 方案1：检查是否有公共方法可以直接调用
                var publicMethod = typeof(NetworkQuestionManagerController).GetMethod("ReceiveNetworkQuestion");
                if (publicMethod != null)
                {
                    LogDebug("通过公共方法 ReceiveNetworkQuestion 触发");
                    publicMethod.Invoke(nqmc, new object[] { question });
                    return;
                }

                // 方案2：通过反射调用私有方法
                var privateMethod = typeof(NetworkQuestionManagerController).GetMethod("OnNetworkQuestionReceived",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (privateMethod != null)
                {
                    LogDebug("通过反射调用 OnNetworkQuestionReceived");
                    privateMethod.Invoke(nqmc, new object[] { question });
                }
                else
                {
                    Debug.LogError("无法找到合适的方法来触发Host题目接收");

                    // 方案3：检查NQMC是否已经启动游戏，如果没有则启动
                    if (!nqmc.IsGameStarted)
                    {
                        LogDebug("NQMC未启动，尝试启动多人游戏");
                        nqmc.StartGame(true);
                    }

                    LogDebug("建议检查NetworkManager的BroadcastMessage实现是否包含Host自己");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"触发Host题目接收失败: {e.Message}");
            }
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
        /// 广播血量更新（包含最大血量信息）
        /// </summary>
        private void BroadcastHealthUpdate(ushort playerId, int newHealth)
        {
            if (!isInitialized || NetworkManager.Instance == null)
                return;

            // 获取最大血量信息
            int maxHealth = 100; // 默认值
            if (playerStates.ContainsKey(playerId))
            {
                maxHealth = playerStates[playerId].maxHealth;
            }
            else if (hpManager != null)
            {
                var hpInfo = hpManager.GetPlayerHP(playerId);
                maxHealth = hpInfo.maxHealth;
            }

            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.HealthUpdate);
            message.AddUShort(playerId);
            message.AddInt(newHealth);
            message.AddInt(maxHealth); // 新增：发送最大血量
            NetworkManager.Instance.BroadcastMessage(message);

            LogDebug($"广播血量更新: 玩家{playerId}, 血量{newHealth}/{maxHealth}");
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
        #region HP事件处理

        private void OnPlayerHealthChanged(ushort playerId, int newHealth, int maxHealth)
        {
            LogDebug($"玩家 {playerId} 血量变更: {newHealth}/{maxHealth}");

            // 更新playerStates中的血量信息
            if (playerStates.ContainsKey(playerId))
            {
                var playerState = playerStates[playerId];
                playerState.health = newHealth;
                playerState.maxHealth = maxHealth;
                playerState.lastActiveTime = Time.time;
            }
            else
            {
                Debug.LogWarning($"[HostGameManager] 玩家 {playerId} 不在playerStates中，无法更新血量状态");
            }

            // 验证血量值的合理性
            if (newHealth < 0 || maxHealth <= 0 || newHealth > maxHealth)
            {
                Debug.LogWarning($"[HostGameManager] 玩家 {playerId} 血量数据异常: {newHealth}/{maxHealth}");
            }

            // 广播血量更新给客户端
            BroadcastHealthUpdate(playerId, newHealth);
        }

        /// <summary>
        /// 处理玩家死亡事件
        /// </summary>
        /// <param name="playerId">死亡的玩家ID</param>
        private void OnPlayerDied(ushort playerId)
        {
            LogDebug($"玩家 {playerId} 已死亡");

            // 更新playerStates中的存活状态
            if (playerStates.ContainsKey(playerId))
            {
                var playerState = playerStates[playerId];
                playerState.isAlive = false;
                playerState.health = 0;
            }

            // 广播玩家死亡信息
            BroadcastHealthUpdate(playerId, 0);

            // 如果是当前回合玩家死亡，切换到下一个玩家
            if (currentTurnPlayerId == playerId && gameInProgress)
            {
                LogDebug($"当前回合玩家 {playerId} 死亡，切换到下一个玩家");
                Invoke(nameof(NextPlayerTurn), 1f);
            }

            // 检查游戏结束条件
            CheckGameEndConditions();
        }

        /// <summary>
        /// 检查PlayerGameState是否有maxHealth字段（向后兼容）
        /// </summary>
        private bool HasMaxHealthField(PlayerGameState playerState)
        {
            try
            {
                var field = typeof(PlayerGameState).GetField("maxHealth");
                return field != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 设置PlayerGameState的maxHealth字段（向后兼容）
        /// </summary>
        private void SetMaxHealth(PlayerGameState playerState, int maxHealth)
        {
            try
            {
                var field = typeof(PlayerGameState).GetField("maxHealth");
                if (field != null)
                {
                    field.SetValue(playerState, maxHealth);
                }
            }
            catch (System.Exception e)
            {
                LogDebug($"设置maxHealth失败: {e.Message}");
            }
        }

        #endregion

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

        #region 公共接口和调试方法

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
        public string GetGameStats()
        {
            var stats = "=== 游戏统计 ===\n";
            stats += $"游戏状态: {(gameInProgress ? "进行中" : "未开始")}\n";
            stats += $"初始化完成: {isInitialized}\n";
            stats += $"当前回合玩家: {currentTurnPlayerId}\n";
            stats += $"玩家数量: {playerStates?.Count ?? 0}\n";

            if (NetworkManager.Instance != null)
            {
                stats += $"Host玩家ID: {NetworkManager.Instance.GetHostPlayerId()}\n";
                stats += $"Host客户端就绪: {NetworkManager.Instance.IsHostClientReady}\n";
            }

            // HP管理器信息
            if (hpManager != null)
            {
                stats += $"HP配置源: {hpManager.GetHPConfigSource()}\n";
                stats += $"初始血量: {hpManager.GetEffectiveInitialHealth()}\n";
                stats += $"答错扣血: {hpManager.GetEffectiveDamageAmount()}\n";
                stats += $"最多答错: {hpManager.GetMaxWrongAnswers()}次\n";
                stats += $"HP管理器存活玩家: {hpManager.GetAlivePlayerCount()}\n";
            }
            else
            {
                stats += "HP管理器: 未初始化\n";
            }

            // Timer配置信息
            stats += $"Timer配置源: {GetTimerConfigSource()}\n";
            if (currentQuestion != null)
            {
                stats += $"当前题目类型: {currentQuestion.questionType}\n";
                stats += $"当前题目时间限制: {currentQuestion.timeLimit}秒\n";
            }

            if (playerStates != null)
            {
                stats += "玩家状态:\n";
                foreach (var player in playerStates.Values)
                {
                    stats += $"  - {player.playerName} (ID: {player.playerId}, 血量: {player.health}, 存活: {player.isAlive})\n";
                }
            }

            if (RoomManager.Instance?.CurrentRoom != null)
            {
                var room = RoomManager.Instance.CurrentRoom;
                stats += $"房间玩家数: {room.players.Count}\n";
                stats += $"房间Host ID: {room.hostId}\n";
            }

            return stats;
        }
        /// <summary>
        /// 手动同步房间数据（调试用）
        /// </summary>
        [ContextMenu("手动同步房间数据")]
        public void ManualSyncFromRoom()
        {
            if (Application.isPlaying)
            {
                LogDebug("手动同步房间数据");
                SyncPlayersFromRoomSystem();
            }
        }
        /// <summary>
        /// 获取Timer配置源信息（用于调试）
        /// </summary>
        private string GetTimerConfigSource()
        {
            if (timerConfig != null)
            {
                return $"本地配置({timerConfig.ConfigName})";
            }
            else if (TimerConfigManager.Config != null)
            {
                return $"全局管理器({TimerConfigManager.Config.ConfigName})";
            }
            else
            {
                return "默认值";
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
                   $"IsHost: {NetworkManager.Instance?.IsHost}, " +
                   $"HostPlayerId: {NetworkManager.Instance?.GetHostPlayerId()}, " +
                   $"HostClientReady: {NetworkManager.Instance?.IsHostClientReady}, " +
                   $"QuestionDataService: {questionDataService != null}, " +
                   $"RoomManager: {RoomManager.Instance != null}, " +
                   $"CurrentRoom: {RoomManager.Instance?.CurrentRoom != null}";
        }

        /// <summary>
        /// 显示当前玩家列表（调试用）
        /// </summary>
        [ContextMenu("显示玩家列表")]
        public void ShowPlayerList()
        {
            if (Application.isPlaying)
            {
                LogDebug("=== 当前玩家列表 ===");
                if (playerStates == null || playerStates.Count == 0)
                {
                    LogDebug("没有玩家");
                    return;
                }

                foreach (var player in playerStates.Values)
                {
                    LogDebug($"玩家: {player.playerName} (ID: {player.playerId}, 血量: {player.health}, 存活: {player.isAlive})");
                }
            }
        }

        /// <summary>
        /// 清理服务缓存
        /// </summary>
        public void ClearServiceCache()
        {
            if (questionDataService != null)
            {
                questionDataService.ClearCache();
                LogDebug("已清理题目数据服务缓存");
            }
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
        /// <summary>
        /// 初始化HP管理器
        /// </summary>
        private IEnumerator InitializeHPManager()
        {
            LogDebug("初始化HP管理器...");

            // 执行初始化逻辑
            bool success = InitializeHPManagerInternal();

            // 等待一帧确保初始化完成
            yield return null;

            if (success)
            {
                LogDebug($"HP管理器初始化完成 - 配置源: {hpManager.GetHPConfigSource()}");
                LogDebug($"HP设置 - 初始血量: {hpManager.GetEffectiveInitialHealth()}, 扣血量: {hpManager.GetEffectiveDamageAmount()}");
            }
            else
            {
                LogDebug("HP管理器初始化失败，将使用备用方案");
            }
        }

        /// <summary>
        /// HP管理器内部初始化逻辑（非协程方法）
        /// </summary>
        private bool InitializeHPManagerInternal()
        {
            try
            {
                // 创建HP管理器实例
                hpManager = new PlayerHPManager();

                // 绑定事件
                hpManager.OnHealthChanged += OnPlayerHealthChanged;
                hpManager.OnPlayerDied += OnPlayerDied;

                // 初始化HP管理器
                hpManager.Initialize(hpConfig, useCustomHPConfig);

                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HostGameManager] HP管理器初始化失败: {e.Message}");

                try
                {
                    // 失败时创建基础HP管理器
                    hpManager = new PlayerHPManager();
                    hpManager.Initialize();

                    // 仍然绑定事件
                    hpManager.OnHealthChanged += OnPlayerHealthChanged;
                    hpManager.OnPlayerDied += OnPlayerDied;

                    LogDebug("HP管理器使用默认配置初始化完成");
                    return true;
                }
                catch (System.Exception fallbackException)
                {
                    Debug.LogError($"[HostGameManager] HP管理器默认初始化也失败: {fallbackException.Message}");
                    hpManager = null;
                    return false;
                }
            }
        }

        #endregion

        #region Unity 生命周期

        private void OnDestroy()
        {
            LogDebug("HostGameManager 被销毁");
            UnsubscribeFromNetworkEvents();

            // 清理服务缓存
            ClearServiceCache();

            // 销毁HP管理器
            if (hpManager != null)
            {
                hpManager.Dispose();
                hpManager = null;
                LogDebug("HP管理器已销毁");
            }

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
    /// 玩家游戏状态（扩展HP支持）
    /// </summary>
    [System.Serializable]
    public class PlayerGameState
    {
        public ushort playerId;
        public string playerName;
        public int health;
        public int maxHealth;
        public bool isAlive;
        public bool isReady;
        public float lastActiveTime;

        public PlayerGameState()
        {
            lastActiveTime = Time.time;
            health = 100;      // 默认值，实际会在初始化时设置
            maxHealth = 100;   // 默认值，实际会在初始化时设置
        }

        /// <summary>
        /// 获取血量百分比
        /// </summary>
        public float GetHealthPercentage()
        {
            if (maxHealth <= 0) return 0f;
            return (float)health / maxHealth;
        }

        /// <summary>
        /// 检查是否为满血状态
        /// </summary>
        public bool IsFullHealth()
        {
            return health >= maxHealth;
        }

        /// <summary>
        /// 检查是否为低血量（低于30%）
        /// </summary>
        public bool IsLowHealth()
        {
            return GetHealthPercentage() < 0.3f;
        }

        /// <summary>
        /// 检查是否为危险血量（低于10%）
        /// </summary>
        public bool IsCriticalHealth()
        {
            return GetHealthPercentage() < 0.1f;
        }
    }
}
