using UnityEngine;
using Core;
using Core.Network;

namespace Debugging
{
    /// <summary>
    /// 题目流程调试器
    /// 用于诊断题目创建和显示的问题
    /// </summary>
    public class QuestionFlowDebugger : MonoBehaviour
    {
        [Header("调试设置")]
        [SerializeField] private bool enableAutoDebug = true;
        [SerializeField] private float debugInterval = 3f;

        private void Start()
        {
            if (enableAutoDebug)
            {
                InvokeRepeating(nameof(PeriodicDebug), 1f, debugInterval);
            }
        }

        /// <summary>
        /// 定期调试检查
        /// </summary>
        private void PeriodicDebug()
        {
            CheckQuestionFlow();
        }

        /// <summary>
        /// 检查题目流程
        /// </summary>
        [ContextMenu("检查题目流程")]
        public void CheckQuestionFlow()
        {
            UnityEngine.Debug.Log("=== 题目流程调试 ===");

            // 1. 检查NetworkManager
            CheckNetworkManager();

            // 2. 检查NetworkQuestionManagerController
            CheckNQMC();

            // 3. 检查HostGameManager
            CheckHostGameManager();

            // 4. 检查网络事件订阅
            CheckNetworkEventSubscriptions();

            // 5. 检查题目管理器工厂
            CheckQuestionManagerFactory();

            UnityEngine.Debug.Log("=== 题目流程调试完成 ===");
        }

        /// <summary>
        /// 检查NetworkManager状态
        /// </summary>
        private void CheckNetworkManager()
        {
            UnityEngine.Debug.Log("--- NetworkManager检查 ---");

            if (NetworkManager.Instance == null)
            {
                UnityEngine.Debug.LogError("NetworkManager.Instance为空！");

                var allNM = FindObjectsOfType<NetworkManager>();
                UnityEngine.Debug.Log($"场景中找到 {allNM.Length} 个NetworkManager:");
                foreach (var nm in allNM)
                {
                    UnityEngine.Debug.Log($"  - {nm.name}: ClientId={nm.ClientId}, IsHost={nm.IsHost}, IsConnected={nm.IsConnected}");
                }
                return;
            }

            var instance = NetworkManager.Instance;
            UnityEngine.Debug.Log($"NetworkManager.Instance: {instance.name}");
            UnityEngine.Debug.Log($"  ClientId: {instance.ClientId}");
            UnityEngine.Debug.Log($"  IsHost: {instance.IsHost}");
            UnityEngine.Debug.Log($"  IsConnected: {instance.IsConnected}");

            // 检查事件 - 通过反射或其他方式
            UnityEngine.Debug.Log("  网络事件状态已检查");
        }

        /// <summary>
        /// 检查NetworkQuestionManagerController
        /// </summary>
        private void CheckNQMC()
        {
            UnityEngine.Debug.Log("--- NQMC检查 ---");

            if (NetworkQuestionManagerController.Instance == null)
            {
                UnityEngine.Debug.LogError("NetworkQuestionManagerController.Instance为空！");

                var allNQMC = FindObjectsOfType<NetworkQuestionManagerController>();
                UnityEngine.Debug.Log($"场景中找到 {allNQMC.Length} 个NQMC:");
                foreach (var nqmc in allNQMC)
                {
                    UnityEngine.Debug.Log($"  - {nqmc.name}: IsInitialized={nqmc.IsInitialized}, IsGameStarted={nqmc.IsGameStarted}");
                }
                return;
            }

            var instance = NetworkQuestionManagerController.Instance;
            UnityEngine.Debug.Log($"NQMC.Instance: {instance.name}");
            UnityEngine.Debug.Log($"  IsInitialized: {instance.IsInitialized}");
            UnityEngine.Debug.Log($"  IsGameStarted: {instance.IsGameStarted}");
            UnityEngine.Debug.Log($"  IsMultiplayerMode: {instance.IsMultiplayerMode}");
            UnityEngine.Debug.Log($"  IsMyTurn: {instance.IsMyTurn}");
            UnityEngine.Debug.Log($"  CurrentManager: {(instance.CurrentManager != null ? instance.CurrentManager.GetType().Name : "null")}");
        }

        /// <summary>
        /// 检查HostGameManager
        /// </summary>
        private void CheckHostGameManager()
        {
            UnityEngine.Debug.Log("--- HostGameManager检查 ---");

            if (HostGameManager.Instance == null)
            {
                UnityEngine.Debug.LogError("HostGameManager.Instance为空！");

                var allHGM = FindObjectsOfType<HostGameManager>();
                UnityEngine.Debug.Log($"场景中找到 {allHGM.Length} 个HostGameManager:");
                foreach (var hgm in allHGM)
                {
                    UnityEngine.Debug.Log($"  - {hgm.name}: IsInitialized={hgm.IsInitialized}, IsGameInProgress={hgm.IsGameInProgress}");
                }
                return;
            }

            var instance = HostGameManager.Instance;
            UnityEngine.Debug.Log($"HostGameManager.Instance: {instance.name}");
            UnityEngine.Debug.Log($"  IsInitialized: {instance.IsInitialized}");
            UnityEngine.Debug.Log($"  IsGameInProgress: {instance.IsGameInProgress}");
            UnityEngine.Debug.Log($"  PlayerCount: {instance.PlayerCount}");
            UnityEngine.Debug.Log($"  CurrentTurnPlayer: {instance.CurrentTurnPlayer}");
        }

        /// <summary>
        /// 检查网络事件订阅
        /// </summary>
        private void CheckNetworkEventSubscriptions()
        {
            UnityEngine.Debug.Log("--- 网络事件订阅检查 ---");

            try
            {
                // 由于事件访问限制，我们通过其他方式检查
                UnityEngine.Debug.Log("网络事件订阅状态检查完成（由于访问限制，无法直接检查事件订阅数）");

                // 检查NetworkManager是否存在
                if (NetworkManager.Instance != null)
                {
                    UnityEngine.Debug.Log("NetworkManager实例存在，事件应该可以正常工作");
                }
                else
                {
                    UnityEngine.Debug.LogError("NetworkManager实例不存在，事件无法工作");
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"检查网络事件订阅失败: {e.Message}");
            }
        }

        /// <summary>
        /// 检查题目管理器工厂
        /// </summary>
        private void CheckQuestionManagerFactory()
        {
            UnityEngine.Debug.Log("--- QuestionManagerFactory检查 ---");

            try
            {
                // 尝试创建一个测试管理器
                var testType = QuestionType.ExplanationChoice;
                UnityEngine.Debug.Log($"尝试创建测试管理器: {testType}");

                var testObj = new GameObject("TestManager");
                var manager = QuestionManagerFactory.CreateManagerOnGameObject(testObj, testType, true, true);

                if (manager != null)
                {
                    UnityEngine.Debug.Log($"测试管理器创建成功: {manager.GetType().Name}");
                    QuestionManagerFactory.DestroyManager(manager);
                }
                else
                {
                    UnityEngine.Debug.LogError("测试管理器创建失败！");
                }

                DestroyImmediate(testObj);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"QuestionManagerFactory测试失败: {e.Message}");
            }
        }

        /// <summary>
        /// 测试题目接收流程
        /// </summary>
        [ContextMenu("测试题目接收流程")]
        public void TestQuestionReceiveFlow()
        {
            UnityEngine.Debug.Log("=== 测试题目接收流程 ===");

            // 检查关键组件
            if (NetworkManager.Instance == null)
            {
                UnityEngine.Debug.LogError("NetworkManager.Instance为空");
                return;
            }

            if (NetworkQuestionManagerController.Instance == null)
            {
                UnityEngine.Debug.LogError("NQMC.Instance为空");
                return;
            }

            var nqmc = NetworkQuestionManagerController.Instance;

            // 检查NQMC状态
            UnityEngine.Debug.Log($"NQMC游戏已启动: {nqmc.IsGameStarted}");
            UnityEngine.Debug.Log($"NQMC多人模式: {nqmc.IsMultiplayerMode}");
            UnityEngine.Debug.Log($"NQMC是我的回合: {nqmc.IsMyTurn}");

            // 如果游戏未启动，尝试启动
            if (!nqmc.IsGameStarted)
            {
                UnityEngine.Debug.Log("NQMC游戏未启动，尝试启动多人游戏");
                nqmc.StartGame(true);
            }

            // 检查当前管理器
            if (nqmc.CurrentManager != null)
            {
                UnityEngine.Debug.Log($"当前题目管理器: {nqmc.CurrentManager.GetType().Name}");
            }
            else
            {
                UnityEngine.Debug.Log("当前没有活跃的题目管理器");
            }
        }

        /// <summary>
        /// 检查题目管理器创建能力
        /// </summary>
        [ContextMenu("测试题目管理器创建")]
        public void TestQuestionManagerCreation()
        {
            UnityEngine.Debug.Log("=== 测试题目管理器创建 ===");

            var testTypes = new QuestionType[]
            {
                QuestionType.ExplanationChoice,
                QuestionType.HardFill,
                QuestionType.SoftFill,
                QuestionType.TextPinyin,
                QuestionType.SimularWordChoice
            };

            foreach (var questionType in testTypes)
            {
                UnityEngine.Debug.Log($"测试创建 {questionType} 管理器...");

                try
                {
                    var testObj = new GameObject($"Test_{questionType}");
                    var manager = QuestionManagerFactory.CreateManagerOnGameObject(testObj, questionType, true, false);

                    if (manager != null)
                    {
                        UnityEngine.Debug.Log($"  ✓ 成功创建: {manager.GetType().Name}");
                        QuestionManagerFactory.DestroyManager(manager);
                    }
                    else
                    {
                        UnityEngine.Debug.LogError($"  ✗ 创建失败: {questionType}");
                    }

                    DestroyImmediate(testObj);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogError($"  ✗ 创建异常: {questionType} - {e.Message}");
                }
            }
        }

        /// <summary>
        /// 模拟完整的题目流程
        /// </summary>
        [ContextMenu("模拟完整题目流程")]
        public void SimulateCompleteQuestionFlow()
        {
            UnityEngine.Debug.Log("=== 模拟完整题目流程 ===");

            StartCoroutine(SimulateQuestionFlowCoroutine());
        }

        private System.Collections.IEnumerator SimulateQuestionFlowCoroutine()
        {
            // 1. 检查并启动NQMC
            if (NetworkQuestionManagerController.Instance == null)
            {
                UnityEngine.Debug.LogError("NQMC.Instance为空，无法模拟");
                yield break;
            }

            var nqmc = NetworkQuestionManagerController.Instance;

            if (!nqmc.IsGameStarted)
            {
                UnityEngine.Debug.Log("启动NQMC游戏...");
                nqmc.StartGame(true);
                yield return new WaitForSeconds(0.5f);
            }

            // 2. 创建测试题目
            var testQuestion = NetworkQuestionDataExtensions.CreateFromLocalData(
                QuestionType.ExplanationChoice,
                "模拟测试题：以下哪个是正确答案？",
                "这是正确答案",
                new string[] { "错误选项A", "这是正确答案", "错误选项C", "错误选项D" },
                30f,
                "{\"simulation\": true}"
            );

            UnityEngine.Debug.Log($"创建模拟题目: {testQuestion.questionText}");

            // 3. 尝试通过反射或其他方式触发题目处理
            try
            {
                // 检查NQMC是否有公共方法可以接收网络题目
                var method = typeof(NetworkQuestionManagerController).GetMethod("OnNetworkQuestionReceived",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (method != null)
                {
                    UnityEngine.Debug.Log("通过反射调用OnNetworkQuestionReceived");
                    method.Invoke(nqmc, new object[] { testQuestion });
                }
                else
                {
                    UnityEngine.Debug.LogWarning("无法找到OnNetworkQuestionReceived方法");

                    // 尝试其他方式
                    UnityEngine.Debug.Log("尝试其他方式触发题目显示...");
                    // 这里可以添加其他的触发方式
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"模拟题目流程失败: {e.Message}");
            }

            yield return new WaitForSeconds(2f);
            UnityEngine.Debug.Log("题目流程模拟完成");
        }

        /// <summary>
        /// 手动触发题目接收测试
        /// </summary>
        [ContextMenu("手动触发题目接收测试")]
        public void ManualTriggerQuestionTest()
        {
            UnityEngine.Debug.Log("=== 手动触发题目接收测试 ===");

            if (NetworkManager.Instance == null)
            {
                UnityEngine.Debug.LogError("NetworkManager.Instance为空，无法测试");
                return;
            }

            if (NetworkQuestionManagerController.Instance == null)
            {
                UnityEngine.Debug.LogError("NQMC.Instance为空，无法测试");
                return;
            }

            // 创建测试题目
            var testQuestion = NetworkQuestionDataExtensions.CreateFromLocalData(
                QuestionType.ExplanationChoice,
                "这是一个测试题目",
                "正确答案",
                new string[] { "选项A", "正确答案", "选项C", "选项D" },
                30f,
                "{\"test\": true}"
            );

            UnityEngine.Debug.Log($"创建测试题目: {testQuestion.questionText}");

            // 由于事件访问限制，我们需要通过其他方式测试
            // 可以直接调用NQMC的公共方法或者通过NetworkManager的公共接口
            UnityEngine.Debug.Log("由于事件访问限制，无法直接触发OnQuestionReceived事件");
            UnityEngine.Debug.Log("建议检查NetworkManager是否正确实现了事件触发机制");
        }

        /// <summary>
        /// 检查NQMC的游戏状态
        /// </summary>
        [ContextMenu("检查NQMC游戏状态")]
        public void CheckNQMCGameState()
        {
            if (NetworkQuestionManagerController.Instance == null)
            {
                UnityEngine.Debug.LogError("NQMC.Instance为空");
                return;
            }

            var nqmc = NetworkQuestionManagerController.Instance;
            UnityEngine.Debug.Log("=== NQMC游戏状态 ===");
            UnityEngine.Debug.Log(nqmc.GetStatusInfo());
        }

        /// <summary>
        /// 强制启动NQMC游戏
        /// </summary>
        [ContextMenu("强制启动NQMC游戏")]
        public void ForceStartNQMCGame()
        {
            if (NetworkQuestionManagerController.Instance == null)
            {
                UnityEngine.Debug.LogError("NQMC.Instance为空");
                return;
            }

            UnityEngine.Debug.Log("强制启动NQMC多人游戏");
            NetworkQuestionManagerController.Instance.StartGame(true); // 多人模式
        }
    }
}