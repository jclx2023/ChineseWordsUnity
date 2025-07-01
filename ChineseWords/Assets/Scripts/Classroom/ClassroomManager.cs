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
    /// 修改为统一处理网络事件和同步控制
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
        [SerializeField] private bool enableTestMode = false; // 测试模式（不需要网络）

        // 初始化状态
        private bool isInitialized = false;
        private bool initializationInProgress = false;
        private int currentStep = 0;
        private string[] initializationSteps = {
            "检查网络状态",
            "初始化座位系统",
            "初始化玩家生成器",
            "初始化座位绑定器",
            "生成座位和角色",
            "同步网络数据",
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
        public static event System.Action OnCameraSetupCompleted; // 新增：摄像机设置完成事件

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
            // 订阅NetworkPlayerSpawner的事件
            SubscribeToPlayerSpawnerEvents();
        }

        private void OnDisable()
        {
            // 取消订阅NetworkPlayerSpawner的事件
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
                // 重新生成座位和角色
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

            // 可以根据需求决定是否重新生成
            // 目前保持座位不变，支持断线重连
            LogDebug("玩家离开，保持当前座位布局不变");
        }

        /// <summary>
        /// 延迟重新生成座位和角色
        /// </summary>
        private IEnumerator DelayedRegenerateSeatsAndCharacters()
        {
            yield return new WaitForSeconds(1f); // 等待网络状态稳定

            LogDebug("开始重新生成座位和角色");

            // 执行完整的生成和同步流程
            yield return StartCoroutine(ExecuteGenerateAndSync());
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

            // 检查关键组件
            if (seatingSystem == null)
            {
                Debug.LogError("[ClassroomManager] 未找到CircularSeatingSystem组件");
            }

            if (playerSpawner == null)
            {
                Debug.LogError("[ClassroomManager] 未找到NetworkPlayerSpawner组件");
            }

            if (seatBinder == null)
            {
                Debug.LogError("[ClassroomManager] 未找到SeatingPlayerBinder组件");
            }

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
        /// 执行初始化步骤（不包含try-catch以避免编译错误）
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

            // 步骤4: 初始化座位绑定器
            var initBinderCoroutine = StartCoroutine(InitializeSeatBinder());
            yield return initBinderCoroutine;
            if (!ValidateCoroutineResult("初始化座位绑定器")) yield break;

            // 步骤5: 生成座位和角色（仅主机）
            var generateCoroutine = StartCoroutine(GenerateSeatsAndCharacters());
            yield return generateCoroutine;
            if (!ValidateCoroutineResult("生成座位和角色")) yield break;

            // 步骤6: 同步网络数据（仅主机）
            var syncCoroutine = StartCoroutine(SyncNetworkData());
            yield return syncCoroutine;
            if (!ValidateCoroutineResult("同步网络数据")) yield break;

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
            // 这里可以添加更复杂的验证逻辑
            // 目前简单检查组件状态
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

            if (enableTestMode)
            {
                LogDebug("测试模式：跳过网络检查");
                yield break;
            }

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

            if (!networkReady)
            {
                HandleInitializationError("网络连接超时");
                yield break;
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
        /// 初始化座位绑定器
        /// </summary>
        private IEnumerator InitializeSeatBinder()
        {
            ReportStep("初始化座位绑定器");

            if (seatBinder == null)
            {
                HandleInitializationError("座位绑定器组件缺失");
                yield break;
            }

            // 手动初始化座位绑定器
            seatBinder.Initialize();

            // 等待绑定器准备就绪
            yield return new WaitUntil(() => seatBinder.IsInitialized);
            LogDebug("座位绑定器初始化完成");
        }

        /// <summary>
        /// 生成座位和角色
        /// </summary>
        private IEnumerator GenerateSeatsAndCharacters()
        {
            ReportStep("生成座位和角色");

            if (enableTestMode)
            {
                // 测试模式：生成固定数量的座位
                seatingSystem.GenerateSeats(4);
                LogDebug("测试模式：生成了4个座位");
                yield break;
            }

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
        /// 执行生成和同步流程（供重新生成时调用）
        /// </summary>
        private IEnumerator ExecuteGenerateAndSync()
        {
            // 先执行延迟初始化
            yield return StartCoroutine(playerSpawner.DelayedSpawnInitializationCoroutine());

            // 然后生成座位和角色
            playerSpawner.GenerateSeatsAndSpawnCharacters();

            // 等待生成完成
            yield return new WaitUntil(() => playerSpawner.HasSpawnedCharacters);

            // 同步给所有客户端
            playerSpawner.SyncToAllClients();

            LogDebug("重新生成和同步完成");
        }

        /// <summary>
        /// 设置摄像机控制
        /// </summary>
        private IEnumerator SetupCameraControl()
        {
            ReportStep("设置摄像机控制");
            LogDebug($"当前ClientId: {NetworkManager.Instance?.ClientId}, 测试模式: {enableTestMode}");

            // 等待一帧确保所有组件准备就绪
            yield return null;

            // 检查本地玩家的摄像机控制器
            ushort localPlayerId = enableTestMode ? (ushort)1 : NetworkManager.Instance.ClientId;
            var localCharacter = playerSpawner.GetPlayerCharacter(localPlayerId);

            if (localCharacter != null)
            {
                var cameraController = localCharacter.GetComponent<PlayerCameraController>();
                if (cameraController != null && cameraController.IsInitialized)
                {
                    LogDebug("本地玩家摄像机控制器设置完成");

                    // 触发摄像机设置完成事件
                    OnCameraSetupCompleted?.Invoke();
                }
                else
                {
                    LogDebug("警告：本地玩家摄像机控制器未正确初始化");
                }
            }
            else
            {
                LogDebug("警告：未找到本地玩家角色");
            }
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
            LogDebug("教室初始化流程完成");
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

            // 可以在这里添加错误恢复逻辑或返回上级场景
            StartCoroutine(HandleErrorRecovery(errorMessage));
        }

        /// <summary>
        /// 错误恢复处理
        /// </summary>
        private IEnumerator HandleErrorRecovery(string errorMessage)
        {
            LogDebug("开始错误恢复流程");

            // 等待一段时间让用户看到错误信息
            yield return new WaitForSeconds(3f);

            // 尝试重新初始化或返回房间
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
            {
                LogDebug("尝试重新初始化");
                StartCoroutine(InitializeClassroomAsync());
            }
            else
            {
                LogDebug("网络连接丢失，准备返回上级场景");
                // 这里可以触发返回房间或主菜单的逻辑
            }
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 获取本地玩家的摄像机控制器（供其他组件调用）
        /// </summary>
        public PlayerCameraController GetLocalPlayerCameraController()
        {
            if (!isInitialized) return null;

            ushort localPlayerId = enableTestMode ? (ushort)1 : NetworkManager.Instance.ClientId;
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

        #region 生命周期管理

        private void OnDestroy()
        {
            // 清理资源
            StopAllCoroutines();
            UnsubscribeFromPlayerSpawnerEvents();

            LogDebug("ClassroomManager已销毁");
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && initializationInProgress)
            {
                LogDebug("应用暂停，暂停初始化流程");
                // 可以在这里保存状态
            }
        }

        #endregion
    }
}