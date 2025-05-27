using UnityEngine;
using Core.Network;

namespace Core
{
    /// <summary>
    /// 双层架构管理器
    /// 协调服务器层(ID=0)和玩家Host层(ID=1)之间的通信
    /// </summary>
    public class DualLayerArchitectureManager : MonoBehaviour
    {
        [Header("双层架构配置")]
        [SerializeField] private bool enableDebugLogs = true;

        public static DualLayerArchitectureManager Instance { get; private set; }

        // 层级引用
        private NetworkManager serverNetworkManager;    // ID=0
        private NetworkManager playerHostNetworkManager; // ID=1
        private HostGameManager serverHostGameManager;
        private NetworkQuestionManagerController playerNQMC;

        // 状态
        private bool isInitialized = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                LogDebug("双层架构管理器初始化");
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            InitializeDualLayerArchitecture();
        }

        /// <summary>
        /// 初始化双层架构
        /// </summary>
        private void InitializeDualLayerArchitecture()
        {
            LogDebug("开始初始化双层架构");

            // 查找两层的NetworkManager
            FindNetworkManagers();

            // 查找关键组件
            FindKeyComponents();

            // 建立层间通信
            EstablishLayerCommunication();

            isInitialized = true;
            LogDebug("双层架构初始化完成");
        }

        /// <summary>
        /// 查找NetworkManager实例
        /// </summary>
        private void FindNetworkManagers()
        {
            var allNetworkManagers = FindObjectsOfType<NetworkManager>();

            foreach (var nm in allNetworkManagers)
            {
                if (nm.ClientId == 0)
                {
                    serverNetworkManager = nm;
                    LogDebug($"找到服务器层NetworkManager: {nm.name}");
                }
                else if (nm.ClientId == 1)
                {
                    playerHostNetworkManager = nm;
                    LogDebug($"找到玩家Host层NetworkManager: {nm.name}");
                }
            }

            if (serverNetworkManager == null)
                Debug.LogError("未找到服务器层NetworkManager (ID=0)");
            if (playerHostNetworkManager == null)
                Debug.LogError("未找到玩家Host层NetworkManager (ID=1)");
        }

        /// <summary>
        /// 查找关键组件
        /// </summary>
        private void FindKeyComponents()
        {
            // 查找服务器层的HostGameManager
            if (serverNetworkManager != null)
            {
                serverHostGameManager = serverNetworkManager.GetComponent<HostGameManager>();
                if (serverHostGameManager == null)
                {
                    serverHostGameManager = serverNetworkManager.GetComponentInChildren<HostGameManager>();
                }

                if (serverHostGameManager != null)
                    LogDebug("找到服务器层HostGameManager");
                else
                    Debug.LogError("未找到服务器层HostGameManager");
            }

            // 查找玩家Host层的NQMC
            playerNQMC = NetworkQuestionManagerController.Instance;
            if (playerNQMC != null)
                LogDebug("找到玩家层NQMC");
            else
                Debug.LogError("未找到玩家层NQMC");
        }

        /// <summary>
        /// 建立层间通信
        /// </summary>
        private void EstablishLayerCommunication()
        {
            LogDebug("建立层间通信");

            // 如果服务器层HostGameManager存在，重写其题目广播逻辑
            if (serverHostGameManager != null)
            {
                // 这里可以通过事件或直接调用来建立通信
                LogDebug("服务器层通信已建立");
            }

            // 确保玩家层NQMC正确启动
            if (playerNQMC != null)
            {
                if (!playerNQMC.IsGameStarted)
                {
                    LogDebug("启动玩家层NQMC");
                    playerNQMC.StartGame(true); // 多人模式
                }
            }
        }

        /// <summary>
        /// 服务器层请求向玩家层发送题目
        /// </summary>
        public void ServerToPlayerHostQuestion(NetworkQuestionData questionData)
        {
            if (!isInitialized)
            {
                Debug.LogError("双层架构未初始化");
                return;
            }

            LogDebug($"服务器层向玩家层发送题目: {questionData.questionType}");

            // 直接调用玩家层的NQMC来处理题目
            if (playerNQMC != null)
            {
                // 通过反射调用私有方法，或者添加公共接口
                TriggerPlayerHostQuestionReceive(questionData);
            }
            else
            {
                Debug.LogError("玩家层NQMC不存在，无法发送题目");
            }
        }

        /// <summary>
        /// 触发玩家Host接收题目
        /// </summary>
        private void TriggerPlayerHostQuestionReceive(NetworkQuestionData questionData)
        {
            try
            {
                // 方案1：如果NQMC有公共方法
                var method = typeof(NetworkQuestionManagerController).GetMethod("ReceiveNetworkQuestion");
                if (method != null)
                {
                    LogDebug("通过公共方法发送题目到玩家层");
                    method.Invoke(playerNQMC, new object[] { questionData });
                    return;
                }

                // 方案2：通过反射调用私有方法
                var privateMethod = typeof(NetworkQuestionManagerController).GetMethod("OnNetworkQuestionReceived",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (privateMethod != null)
                {
                    LogDebug("通过反射发送题目到玩家层");
                    privateMethod.Invoke(playerNQMC, new object[] { questionData });
                }
                else
                {
                    Debug.LogError("无法找到题目接收方法");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"发送题目到玩家层失败: {e.Message}");
            }
        }

        /// <summary>
        /// 玩家层请求向服务器层提交答案
        /// </summary>
        public void PlayerHostToServerAnswer(ushort playerId, string answer)
        {
            if (!isInitialized)
            {
                Debug.LogError("双层架构未初始化");
                return;
            }

            LogDebug($"玩家层向服务器层提交答案: 玩家{playerId} -> {answer}");

            // 直接调用服务器层的HostGameManager处理答案
            if (serverHostGameManager != null)
            {
                serverHostGameManager.HandlePlayerAnswer(playerId, answer);
            }
            else
            {
                Debug.LogError("服务器层HostGameManager不存在，无法处理答案");
            }
        }

        /// <summary>
        /// 获取当前架构状态
        /// </summary>
        public string GetArchitectureStatus()
        {
            var status = "=== 双层架构状态 ===\n";
            status += $"初始化完成: {isInitialized}\n";
            status += $"服务器层NetworkManager: {(serverNetworkManager != null ? $"✓ (ID={serverNetworkManager.ClientId})" : "✗")}\n";
            status += $"玩家Host层NetworkManager: {(playerHostNetworkManager != null ? $"✓ (ID={playerHostNetworkManager.ClientId})" : "✗")}\n";
            status += $"服务器层HostGameManager: {(serverHostGameManager != null ? "✓" : "✗")}\n";
            status += $"玩家层NQMC: {(playerNQMC != null ? "✓" : "✗")}\n";
            return status;
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[DualLayerManager] {message}");
            }
        }

        /// <summary>
        /// 获取服务器层NetworkManager
        /// </summary>
        public NetworkManager GetServerNetworkManager()
        {
            return serverNetworkManager;
        }

        /// <summary>
        /// 获取玩家Host层NetworkManager
        /// </summary>
        public NetworkManager GetPlayerHostNetworkManager()
        {
            return playerHostNetworkManager;
        }

        /// <summary>
        /// 强制重新初始化
        /// </summary>
        [ContextMenu("重新初始化双层架构")]
        public void ForceReinitialize()
        {
            if (Application.isPlaying)
            {
                isInitialized = false;
                InitializeDualLayerArchitecture();
            }
        }

        /// <summary>
        /// 显示架构状态
        /// </summary>
        [ContextMenu("显示架构状态")]
        public void ShowArchitectureStatus()
        {
            LogDebug(GetArchitectureStatus());
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}