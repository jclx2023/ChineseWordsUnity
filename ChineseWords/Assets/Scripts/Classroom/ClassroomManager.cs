using UnityEngine;
using System.Collections;
using Classroom.Scene;
using Classroom.Player;
using Core.Network;
using Photon.Pun;

namespace Classroom
{
    /// <summary>
    /// 教室管理器 - NetworkGameScene的总控制器
    /// 统一管理座位系统、玩家生成和摄像机控制的初始化流程
    /// 优化了座位绑定器的初始化时机，确保在生成完成后再进行绑定
    /// </summary>
    public class ClassroomManager : MonoBehaviour
    {
        [Header("系统组件")]
        [SerializeField] private CircularSeatingSystem seatingSystem;
        [SerializeField] private NetworkPlayerSpawner playerSpawner;
        [SerializeField] private SeatingPlayerBinder seatBinder;

        [Header("初始化配置")]
        [SerializeField] private float initializationDelay = 0.3f; // 初始化延迟
        [SerializeField] private float sceneLoadTimeout = 15f; // 场景加载超时
        [SerializeField] private bool waitForNetworkReady = true; // 等待网络准备就绪

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool showInitializationSteps = true;

        // 初始化状态
        private bool isInitialized = false;
        private bool initializationInProgress = false;
        private int currentStep = 0;
        private string[] initializationSteps = {
            "检查网络状态",
            "初始化座位系统",
            "初始化玩家生成器",
            "生成座位和角色",
            "同步网络数据",
            "初始化座位绑定器",
            "设置摄像机控制",
            "完成初始化"
        };

        // 公共属性
        public bool IsInitialized => isInitialized;
        public bool InitializationInProgress => initializationInProgress;
        public CircularSeatingSystem SeatingSystem => seatingSystem;
        public NetworkPlayerSpawner PlayerSpawner => playerSpawner;
        public SeatingPlayerBinder SeatBinder => seatBinder;

        // 事件
        public static event System.Action OnClassroomInitialized;
        public static event System.Action<string> OnInitializationStep;
        public static event System.Action<string> OnClassroomError;
        public static event System.Action OnCameraSetupCompleted;
        public static event System.Action OnSeatsAndCharactersReady;

        #region Unity生命周期

        private void Awake()
        {
            // 自动查找组件
            FindRequiredComponents();
        }

        private void Start()
        {
            // 开始初始化流程
            StartCoroutine(InitializeClassroomAsync());
        }

        private void OnEnable()
        {
            SubscribeToPlayerSpawnerEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromPlayerSpawnerEvents();
        }

        #endregion

        #region 事件订阅管理

        /// <summary>
        /// 订阅NetworkPlayerSpawner的事件
        /// </summary>
        private void SubscribeToPlayerSpawnerEvents()
        {
            NetworkPlayerSpawner.OnPlayerJoinedEvent += OnPlayerJoinedFromSpawner;
            NetworkPlayerSpawner.OnPlayerLeftEvent += OnPlayerLeftFromSpawner;
        }

        /// <summary>
        /// 取消订阅NetworkPlayerSpawner的事件
        /// </summary>
        private void UnsubscribeFromPlayerSpawnerEvents()
        {
            NetworkPlayerSpawner.OnPlayerJoinedEvent -= OnPlayerJoinedFromSpawner;
            NetworkPlayerSpawner.OnPlayerLeftEvent -= OnPlayerLeftFromSpawner;
        }

        #endregion

        #region 网络事件处理

        /// <summary>
        /// 处理玩家加入事件（从NetworkPlayerSpawner转发）
        /// </summary>
        private void OnPlayerJoinedFromSpawner(ushort playerId)
        {
            LogDebug($"收到玩家 {playerId} 加入事件");

            if (PhotonNetwork.IsMasterClient && isInitialized)
            {
                LogDebug("主机端：玩家加入，重新生成座位和角色");
                StartCoroutine(DelayedRegenerateSeatsAndCharacters());
            }
        }

        /// <summary>
        /// 处理玩家离开事件（从NetworkPlayerSpawner转发）
        /// </summary>
        private void OnPlayerLeftFromSpawner(ushort playerId)
        {
            LogDebug($"收到玩家 {playerId} 离开事件");
        }

        /// <summary>
        /// 延迟重新生成座位和角色
        /// </summary>
        private IEnumerator DelayedRegenerateSeatsAndCharacters()
        {
            yield return new WaitForSeconds(1f); // 等待网络状态稳定

            LogDebug("开始重新生成座位和角色");

            yield return StartCoroutine(ExecuteGenerateBindAndSync());
        }

        #endregion

        #region 组件查找

        /// <summary>
        /// 查找必需的组件
        /// </summary>
        private void FindRequiredComponents()
        {
            if (seatingSystem == null)
                seatingSystem = FindObjectOfType<CircularSeatingSystem>();

            if (playerSpawner == null)
                playerSpawner = FindObjectOfType<NetworkPlayerSpawner>();

            if (seatBinder == null)
                seatBinder = FindObjectOfType<SeatingPlayerBinder>();

            LogDebug("组件查找完成");
        }

        #endregion

        #region 异步初始化流程

        /// <summary>
        /// 异步初始化教室
        /// </summary>
        private IEnumerator InitializeClassroomAsync()
        {
            if (initializationInProgress)
            {
                LogDebug("初始化已在进行中，跳过重复初始化");
                yield break;
            }

            initializationInProgress = true;
            LogDebug("开始教室初始化流程");

            // 使用单独的协程来处理可能的异常
            yield return StartCoroutine(ExecuteInitializationSteps());
        }

        /// <summary>
        /// 执行初始化步骤（优化了座位绑定器的初始化时机）
        /// </summary>
        private IEnumerator ExecuteInitializationSteps()
        {
            // 等待初始延迟
            yield return new WaitForSeconds(initializationDelay);

            // 步骤1: 检查网络状态
            var checkNetworkCoroutine = StartCoroutine(CheckNetworkStatus());
            yield return checkNetworkCoroutine;
            if (!ValidateCoroutineResult("检查网络状态")) yield break;

            // 步骤2: 初始化座位系统
            var initSeatingCoroutine = StartCoroutine(InitializeSeatingSystem());
            yield return initSeatingCoroutine;
            if (!ValidateCoroutineResult("初始化座位系统")) yield break;

            // 步骤3: 初始化玩家生成器
            var initSpawnerCoroutine = StartCoroutine(InitializePlayerSpawner());
            yield return initSpawnerCoroutine;
            if (!ValidateCoroutineResult("初始化玩家生成器")) yield break;

            // 步骤4: 生成座位和角色（仅主机）
            var generateCoroutine = StartCoroutine(GenerateSeatsAndCharacters());
            yield return generateCoroutine;
            if (!ValidateCoroutineResult("生成座位和角色")) yield break;

            // 步骤5: 同步网络数据（仅主机）
            var syncCoroutine = StartCoroutine(SyncNetworkData());
            yield return syncCoroutine;
            if (!ValidateCoroutineResult("同步网络数据")) yield break;

            // 步骤6: 初始化座位绑定器
            var initBinderCoroutine = StartCoroutine(InitializeSeatBinder());
            yield return initBinderCoroutine;
            if (!ValidateCoroutineResult("初始化座位绑定器")) yield break;

            // 步骤7: 设置摄像机控制
            var setupCameraCoroutine = StartCoroutine(SetupCameraControl());
            yield return setupCameraCoroutine;
            if (!ValidateCoroutineResult("设置摄像机控制")) yield break;

            // 步骤8: 完成初始化
            CompleteInitialization();
        }

        /// <summary>
        /// 验证协程结果（简单的错误检查）
        /// </summary>
        private bool ValidateCoroutineResult(string stepName)
        {

            bool isValid = true;

            switch (stepName)
            {
                case "初始化座位系统":
                    isValid = seatingSystem != null && seatingSystem.IsInitialized;
                    break;
                case "初始化玩家生成器":
                    isValid = playerSpawner != null && playerSpawner.IsInitialized;
                    break;
                case "初始化座位绑定器":
                    isValid = seatBinder != null && seatBinder.IsInitialized;
                    break;
                case "生成座位和角色":
                    isValid = playerSpawner.HasGeneratedSeats &&
                             (playerSpawner.HasSpawnedCharacters);
                    break;
            }

            if (!isValid)
            {
                HandleInitializationError($"{stepName} 验证失败");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 检查网络状态
        /// </summary>
        private IEnumerator CheckNetworkStatus()
        {
            ReportStep("检查网络状态");

            if (!waitForNetworkReady)
            {
                LogDebug("跳过网络状态检查");
                yield break;
            }

            float timeout = sceneLoadTimeout;
            bool networkReady = false;

            while (timeout > 0 && !networkReady)
            {
                if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
                {
                    networkReady = true;
                    LogDebug("网络状态检查通过");
                }
                else
                {
                    timeout -= Time.deltaTime;
                    yield return null;
                }
            }

        }

        /// <summary>
        /// 初始化座位系统
        /// </summary>
        private IEnumerator InitializeSeatingSystem()
        {
            ReportStep("初始化座位系统");

            if (seatingSystem == null)
            {
                HandleInitializationError("座位系统组件缺失");
                yield break;
            }
            // 等待座位系统准备就绪
            yield return new WaitUntil(() => seatingSystem.IsInitialized);
            LogDebug("座位系统初始化完成");
        }

        /// <summary>
        /// 初始化玩家生成器
        /// </summary>
        private IEnumerator InitializePlayerSpawner()
        {
            ReportStep("初始化玩家生成器");

            if (playerSpawner == null)
            {
                HandleInitializationError("玩家生成器组件缺失");
                yield break;
            }

            // 等待玩家生成器准备就绪
            yield return new WaitUntil(() => playerSpawner.IsInitialized);
            LogDebug("玩家生成器初始化完成");
        }

        /// <summary>
        /// 生成座位和角色
        /// </summary>
        private IEnumerator GenerateSeatsAndCharacters()
        {
            ReportStep("生成座位和角色");

            if (PhotonNetwork.IsMasterClient)
            {
                LogDebug("主机端：开始生成座位和角色");

                // 先执行延迟初始化
                yield return StartCoroutine(playerSpawner.DelayedSpawnInitializationCoroutine());

                // 然后生成座位和角色
                playerSpawner.GenerateSeatsAndSpawnCharacters();

                // 等待生成完成
                yield return new WaitUntil(() => playerSpawner.HasSpawnedCharacters);
                LogDebug("主机端：座位和角色生成完成");

                OnSeatsAndCharactersReady?.Invoke(); // 通知座位和角色准备完成
            }
            else
            {
                LogDebug("客户端：等待主机同步数据");

                // 客户端等待同步
                float timeout = sceneLoadTimeout;
                while (timeout > 0 && !playerSpawner.HasGeneratedSeats)
                {
                    timeout -= Time.deltaTime;
                    yield return null;
                }

                if (timeout <= 0)
                {
                    HandleInitializationError("等待主机同步数据超时");
                    yield break;
                }

                LogDebug("客户端：数据同步完成");
                OnSeatsAndCharactersReady?.Invoke(); // 通知座位和角色准备完成
            }
        }

        /// <summary>
        /// 同步网络数据
        /// </summary>
        private IEnumerator SyncNetworkData()
        {
            ReportStep("同步网络数据");

            if (PhotonNetwork.IsMasterClient)
            {
                LogDebug("主机端：开始同步数据给所有客户端");
                playerSpawner.SyncToAllClients();

                // 等待一帧确保RPC发送
                yield return null;
                LogDebug("主机端：网络数据同步完成");
            }
            else
            {
                LogDebug("客户端：跳过网络数据同步");
            }
        }

        /// <summary>
        /// 初始化座位绑定器（优化后：在座位和角色生成完成后执行）
        /// </summary>
        private IEnumerator InitializeSeatBinder()
        {
            ReportStep("初始化座位绑定器");

            if (seatBinder == null)
            {
                HandleInitializationError("座位绑定器组件缺失");
                yield break;
            }

            LogDebug("座位和角色已生成完成，开始初始化座位绑定器");
            // 禁用座位绑定器的自动初始化，由ClassroomManager控制
            seatBinder.Initialize();

            // 等待绑定器准备就绪
            yield return new WaitUntil(() => seatBinder.IsInitialized);
            LogDebug("座位绑定器初始化完成");

            // 等待一帧确保绑定关系建立完成
            yield return null;

            // 自动绑定当前在线的所有玩家
            yield return StartCoroutine(AutoBindExistingPlayers());
        }

        /// <summary>
        /// 自动绑定已存在的玩家
        /// </summary>
        private IEnumerator AutoBindExistingPlayers()
        {
            LogDebug("开始自动绑定已存在的玩家");

            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected)
            {
                LogDebug("网络未连接，跳过自动绑定");
                yield break;
            }
            var onlinePlayerIds = NetworkManager.Instance.GetAllOnlinePlayerIds();
            LogDebug($"通过NetworkManager获取到 {onlinePlayerIds.Count} 个在线玩家");

            int bindingIndex = 0;
            foreach (var playerId in onlinePlayerIds)
            {
                // 通过NetworkManager获取玩家名称
                string playerName = NetworkManager.Instance.GetPlayerName(playerId);

                // 为每个玩家分配座位
                if (bindingIndex < seatBinder.TotalSeats)
                {
                    bool bindSuccess = seatBinder.BindPlayerToSeat(playerId, bindingIndex, playerName);

                    if (bindSuccess)
                    {
                        LogDebug($"成功绑定玩家 {playerId} ({playerName}) 到座位 {bindingIndex}");

                        // 更新角色引用
                        var playerCharacter = playerSpawner.GetPlayerCharacter(playerId);
                        if (playerCharacter != null)
                        {
                            seatBinder.UpdatePlayerCharacter(playerId, playerCharacter);
                            LogDebug($"更新玩家 {playerId} 的角色引用");
                        }

                        bindingIndex++;
                    }
                    else
                    {
                        LogDebug($"绑定玩家 {playerId} 到座位 {bindingIndex} 失败");
                    }
                }

                // 每绑定一个玩家后等待一帧，避免卡顿
                yield return null;
            }

            LogDebug($"自动绑定完成，成功绑定 {bindingIndex} 个玩家");
        }

        /// <summary>
        /// 设置摄像机控制
        /// </summary>
        private IEnumerator SetupCameraControl()
        {
            ReportStep("设置摄像机控制");
            // 等待一帧确保所有组件准备就绪
            yield return null;

            // 检查本地玩家的摄像机控制器
            ushort localPlayerId = NetworkManager.Instance.ClientId;
            var localCharacter = playerSpawner.GetPlayerCharacter(localPlayerId);

            if (localCharacter != null)
            {
                var cameraController = localCharacter.GetComponent<PlayerCameraController>();
                if (cameraController != null && cameraController.IsInitialized)
                {
                    LogDebug("本地玩家摄像机控制器设置完成");
                    OnCameraSetupCompleted?.Invoke();
                }
            }
            else
            {
                LogDebug("警告：未找到本地玩家角色");
            }
        }

        /// <summary>
        /// 执行生成、绑定和同步流程（供重新生成时调用）
        /// </summary>
        private IEnumerator ExecuteGenerateBindAndSync()
        {
            // 清理现有绑定
            if (seatBinder != null && seatBinder.IsInitialized)
            {
                seatBinder.ClearAllBindings();
                LogDebug("清理现有座位绑定");
            }

            yield return StartCoroutine(playerSpawner.DelayedSpawnInitializationCoroutine());

            playerSpawner.GenerateSeatsAndSpawnCharacters();

            yield return new WaitUntil(() => playerSpawner.HasSpawnedCharacters);

            if (seatBinder != null)
            {
                seatBinder.Initialize();
                yield return new WaitUntil(() => seatBinder.IsInitialized);

                // 自动绑定当前玩家
                yield return StartCoroutine(AutoBindExistingPlayers());
            }

            // 同步给所有客户端
            playerSpawner.SyncToAllClients();

        }

        /// <summary>
        /// 完成初始化
        /// </summary>
        private void CompleteInitialization()
        {
            ReportStep("完成初始化");

            isInitialized = true;
            initializationInProgress = false;
            OnClassroomInitialized?.Invoke();

            // 打印绑定状态信息
            if (seatBinder != null && seatBinder.IsInitialized)
            {
                LogDebug(seatBinder.GetBindingStatus());
            }
        }

        #endregion

        #region 错误处理

        /// <summary>
        /// 处理初始化错误
        /// </summary>
        private void HandleInitializationError(string errorMessage)
        {
            Debug.LogError($"[ClassroomManager] {errorMessage}");

            initializationInProgress = false;
            OnClassroomError?.Invoke(errorMessage);
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 获取本地玩家的摄像机控制器（供其他组件调用）
        /// </summary>
        public PlayerCameraController GetLocalPlayerCameraController()
        {
            if (!isInitialized) return null;

            ushort localPlayerId =NetworkManager.Instance.ClientId;
            var localCharacter = playerSpawner.GetPlayerCharacter(localPlayerId);

            return localCharacter?.GetComponent<PlayerCameraController>();
        }
        #endregion

        #region 事件处理

        /// <summary>
        /// 报告初始化步骤
        /// </summary>
        private void ReportStep(string stepName)
        {
            LogDebug($"初始化步骤 {currentStep + 1}/{initializationSteps.Length}: {stepName}");

            if (showInitializationSteps)
            {
                OnInitializationStep?.Invoke(stepName);
            }

            currentStep++;
        }

        #endregion

        #region 调试方法

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[ClassroomManager] {message}");
            }
        }

        #endregion

        private void OnDestroy()
        {
            // 清理资源
            StopAllCoroutines();
            UnsubscribeFromPlayerSpawnerEvents();

            LogDebug("ClassroomManager已销毁");
        }

    }
}