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
    /// </summary>
    public class ClassroomManager : MonoBehaviour
    {
        [Header("系统组件")]
        [SerializeField] private CircularSeatingSystem seatingSystem;
        [SerializeField] private NetworkPlayerSpawner playerSpawner;
        [SerializeField] private SeatingPlayerBinder seatBinder;

        [Header("初始化配置")]
        [SerializeField] private float initializationDelay = 1f; // 初始化延迟
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

            // 等待初始延迟
            yield return new WaitForSeconds(initializationDelay);

            // 步骤1: 检查网络状态
            var checkNetworkCoroutine = StartCoroutine(CheckNetworkStatus());
            yield return checkNetworkCoroutine;

            // 步骤2: 初始化座位系统
            var initSeatingCoroutine = StartCoroutine(InitializeSeatingSystem());
            yield return initSeatingCoroutine;

            // 步骤3: 初始化玩家生成器
            var initSpawnerCoroutine = StartCoroutine(InitializePlayerSpawner());
            yield return initSpawnerCoroutine;

            // 步骤4: 初始化座位绑定器
            var initBinderCoroutine = StartCoroutine(InitializeSeatBinder());
            yield return initBinderCoroutine;

            // 步骤5: 生成座位和角色（仅主机）
            var generateCoroutine = StartCoroutine(GenerateSeatsAndCharacters());
            yield return generateCoroutine;

            // 步骤6: 设置摄像机控制
            var setupCameraCoroutine = StartCoroutine(SetupCameraControl());
            yield return setupCameraCoroutine;

            // 步骤7: 完成初始化
            CompleteInitialization();
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
                throw new System.Exception("网络连接超时");
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
                throw new System.Exception("座位系统组件缺失");
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
                throw new System.Exception("玩家生成器组件缺失");
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
                throw new System.Exception("座位绑定器组件缺失");
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

                // 主机负责生成
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
                    throw new System.Exception("等待主机同步数据超时");
                }

                LogDebug("客户端：数据同步完成");
            }
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
        /// 手动重新初始化
        /// </summary>
        [ContextMenu("重新初始化教室")]
        public void ReinitializeClassroom()
        {
            if (initializationInProgress)
            {
                LogDebug("初始化正在进行中，取消重新初始化");
                return;
            }

            LogDebug("手动重新初始化教室");

            isInitialized = false;
            currentStep = 0;

            StartCoroutine(InitializeClassroomAsync());
        }

        /// <summary>
        /// 强制停止初始化
        /// </summary>
        public void StopInitialization()
        {
            StopAllCoroutines();
            initializationInProgress = false;
            LogDebug("强制停止初始化流程");
        }

        /// <summary>
        /// 获取初始化进度
        /// </summary>
        public float GetInitializationProgress()
        {
            if (isInitialized) return 1f;
            if (!initializationInProgress) return 0f;

            return (float)currentStep / initializationSteps.Length;
        }

        /// <summary>
        /// 获取当前初始化步骤
        /// </summary>
        public string GetCurrentInitializationStep()
        {
            if (isInitialized) return "初始化完成";
            if (!initializationInProgress) return "未开始";

            if (currentStep < initializationSteps.Length)
                return initializationSteps[currentStep];

            return "未知步骤";
        }

        /// <summary>
        /// 获取教室状态信息
        /// </summary>
        public string GetClassroomStatus()
        {
            string status = "=== 教室状态 ===\n";
            status += $"已初始化: {isInitialized}\n";
            status += $"初始化进行中: {initializationInProgress}\n";
            status += $"当前步骤: {GetCurrentInitializationStep()}\n";
            status += $"进度: {GetInitializationProgress() * 100:F1}%\n\n";

            if (seatingSystem != null)
            {
                status += $"座位系统: 已生成 {seatingSystem.SeatCount} 个座位\n";
            }

            if (playerSpawner != null)
            {
                status += $"玩家生成器: 已生成 {playerSpawner.SpawnedCharacterCount} 个角色\n";
            }

            if (seatBinder != null)
            {
                status += $"座位绑定器: {seatBinder.ActivePlayers} 名活跃玩家\n";
            }

            return status;
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

        [ContextMenu("显示教室状态")]
        public void ShowClassroomStatus()
        {
            Debug.Log(GetClassroomStatus());
        }

        [ContextMenu("显示组件状态")]
        public void ShowComponentStatus()
        {
            string status = "=== 组件状态 ===\n";

            status += $"CircularSeatingSystem: {(seatingSystem != null ? "✓" : "✗")}\n";
            if (seatingSystem != null)
                status += $"  - 已初始化: {seatingSystem.IsInitialized}\n";

            status += $"NetworkPlayerSpawner: {(playerSpawner != null ? "✓" : "✗")}\n";
            if (playerSpawner != null)
                status += $"  - 已初始化: {playerSpawner.IsInitialized}\n";

            status += $"SeatingPlayerBinder: {(seatBinder != null ? "✓" : "✗")}\n";
            if (seatBinder != null)
                status += $"  - 已初始化: {seatBinder.IsInitialized}\n";

            status += $"NetworkManager: {(NetworkManager.Instance != null ? "✓" : "✗")}\n";
            if (NetworkManager.Instance != null)
                status += $"  - 已连接: {NetworkManager.Instance.IsConnected}\n";

            Debug.Log(status);
        }

        [ContextMenu("测试模式切换")]
        public void ToggleTestMode()
        {
            enableTestMode = !enableTestMode;
            LogDebug($"测试模式: {enableTestMode}");
        }

        [ContextMenu("模拟网络玩家加入")]
        public void SimulatePlayerJoin()
        {
            if (enableTestMode && isInitialized)
            {
                // 模拟玩家加入（仅测试用）
                LogDebug("模拟玩家加入（测试模式）");
            }
        }

        #endregion

        #region 生命周期管理

        private void OnDestroy()
        {
            // 清理资源
            StopAllCoroutines();

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

        #region 配置验证

        /// <summary>
        /// 验证配置
        /// </summary>
        [ContextMenu("验证配置")]
        public void ValidateConfiguration()
        {
            bool isValid = true;
            string issues = "=== 配置验证结果 ===\n";

            // 检查必需组件
            if (seatingSystem == null)
            {
                issues += "✗ 缺少CircularSeatingSystem组件\n";
                isValid = false;
            }
            else
            {
                issues += "✓ CircularSeatingSystem组件存在\n";
            }

            if (playerSpawner == null)
            {
                issues += "✗ 缺少NetworkPlayerSpawner组件\n";
                isValid = false;
            }
            else
            {
                issues += "✓ NetworkPlayerSpawner组件存在\n";
            }

            if (seatBinder == null)
            {
                issues += "✗ 缺少SeatingPlayerBinder组件\n";
                isValid = false;
            }
            else
            {
                issues += "✓ SeatingPlayerBinder组件存在\n";
            }

            // 检查配置参数
            if (initializationDelay < 0)
            {
                issues += "✗ 初始化延迟不能为负数\n";
                isValid = false;
            }

            if (sceneLoadTimeout <= 0)
            {
                issues += "✗ 场景加载超时时间必须大于0\n";
                isValid = false;
            }

            issues += $"\n配置验证: {(isValid ? "通过" : "失败")}";
            Debug.Log(issues);
        }

        #endregion
    }
}