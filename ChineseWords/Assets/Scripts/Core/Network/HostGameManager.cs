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

            // 修复：完全移除自动添加Host的逻辑
            // 房间系统已经处理了所有玩家数据，不需要额外添加
            LogDebug("跳过Host自动添加，完全依赖房间数据");

            // 验证房主数量
            ValidateHostCount();

            // 简化开始逻辑：从房间来的就直接开始
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

        /// <summary>
        /// 从房间数据添加玩家（修复版本）
        /// </summary>
        private void AddPlayerFromRoom(ushort playerId, string playerName)
        {
            if (playerStates.ContainsKey(playerId))
            {
                LogDebug($"玩家 {playerId} 已存在，跳过添加");
                return;
            }

            // **使用NetworkManager的统一接口判断是否为Host**
            bool isHostPlayer = NetworkManager.Instance?.IsHostPlayer(playerId) ?? false;

            playerStates[playerId] = new PlayerGameState
            {
                playerId = playerId,
                playerName = isHostPlayer ? "房主" : playerName,
                health = initialPlayerHealth,
                isAlive = true,
                isReady = true // 从房间来的都是准备好的
            };

            LogDebug($"从房间添加玩家: {playerName} (ID: {playerId}, IsHost: {isHostPlayer})");
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

            playerStates[playerId] = new PlayerGameState
            {
                playerId = playerId,
                playerName = playerName ?? $"玩家{playerId}",
                health = initialPlayerHealth,
                isAlive = true,
                isReady = false
            };

            LogDebug($"添加玩家: {playerId} ({playerName}), 当前玩家数: {playerStates.Count}");
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
                    LogDebug($"成功从服务获取题目: {questionData.questionText}");
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
            return NetworkQuestionDataExtensions.CreateFromLocalData(
                questionType,
                $"这是一个{questionType}类型的备用题目",
                "备用答案",
                questionType == QuestionType.ExplanationChoice || questionType == QuestionType.SimularWordChoice
                    ? new string[] { "选项A", "备用答案", "选项C", "选项D" }
                    : null,
                questionTimeLimit,
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
        /// 检查游戏结束条件（简化版本）
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
        /// 更新玩家状态（简化版本）
        /// </summary>
        private void UpdatePlayerState(ushort playerId, bool isCorrect)
        {
            if (!playerStates.ContainsKey(playerId))
                return;

            var playerState = playerStates[playerId];

            if (isCorrect)
            {
                LogDebug($"玩家 {playerId} 答对了");
                if (isIdiomChainActive && currentQuestion?.questionType == QuestionType.IdiomChain)
                {
                    string playerAnswer = GetLastPlayerAnswer(playerId); // 需要缓存玩家答案
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
                playerState.health -= damagePerWrongAnswer;
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

                // 成语接龙答错时重置（可选）
                if (isIdiomChainActive && currentQuestion?.questionType == QuestionType.IdiomChain)
                {
                    LogDebug("成语接龙答错，重置接龙状态");
                    ResetIdiomChainState();
                }

                BroadcastHealthUpdate(playerId, playerState.health);
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
                    LogDebug("生成接龙题成功");
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

            // 其他题型的普通验证
            return answer.Trim().Equals(question.correctAnswer.Trim(), System.StringComparison.OrdinalIgnoreCase);
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

        #endregion

        #region Unity 生命周期

        private void OnDestroy()
        {
            LogDebug("HostGameManager 被销毁");
            UnsubscribeFromNetworkEvents();

            // 清理服务缓存
            ClearServiceCache();

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
    /// 玩家游戏状态（简化版本，移除分数）
    /// </summary>
    [System.Serializable]
    public class PlayerGameState
    {
        public ushort playerId;
        public string playerName;
        public int health;
        public bool isAlive;
        public bool isReady;
        public float lastActiveTime;

        public PlayerGameState()
        {
            lastActiveTime = Time.time;
        }
    }
}
