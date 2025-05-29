using UnityEngine;
using UnityEngine.SceneManagement;
using Core.Network;

namespace Core.Network
{
    /// <summary>
    /// 房间输入处理器
    /// 专门处理房间场景中的快捷键输入
    /// </summary>
    public class RoomInputHandler : MonoBehaviour
    {
        [Header("快捷键设置")]
        [SerializeField] private KeyCode configUIKey = KeyCode.F1;
        [SerializeField] private KeyCode debugInfoKey = KeyCode.F2;
        [SerializeField] private KeyCode playerListKey = KeyCode.F3;

        [Header("场景限制")]
        [SerializeField] private string[] validScenes = { "RoomScene", "LobbyScene" };
        [SerializeField] private bool onlyInValidScenes = true;

        [Header("权限设置")]
        [SerializeField] private bool requireHostForConfig = true;
        [SerializeField] private bool showInputHints = true;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 组件引用
        private RoomConfigManager configManager;
        private bool isInitialized = false;

        // 输入状态
        private bool inputEnabled = true;
        private float lastInputTime = 0f;
        private const float INPUT_COOLDOWN = 0.2f; // 防止重复输入

        private void Start()
        {
            InitializeInputHandler();
        }

        private void Update()
        {
            if (isInitialized && inputEnabled && CanProcessInput())
            {
                HandleKeyboardInput();
            }
        }

        private void OnEnable()
        {
            // 订阅场景切换事件
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            // 取消订阅
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>
        /// 初始化输入处理器
        /// </summary>
        private void InitializeInputHandler()
        {
            LogDebug("初始化房间输入处理器");

            // 查找配置管理器
            FindConfigManager();

            // 验证当前场景
            if (onlyInValidScenes && !IsInValidScene())
            {
                LogDebug("当前场景不在有效场景列表中，禁用输入处理");
                inputEnabled = false;
                return;
            }

            isInitialized = true;
            LogDebug("房间输入处理器初始化完成");

            // 显示输入提示
            if (showInputHints)
            {
                ShowInputHints();
            }
        }

        /// <summary>
        /// 查找配置管理器
        /// </summary>
        private void FindConfigManager()
        {
            configManager = RoomConfigManager.Instance;

            if (configManager == null)
            {
                configManager = FindObjectOfType<RoomConfigManager>();
            }

            if (configManager == null)
            {
                LogDebug("未找到RoomConfigManager，配置UI功能将不可用");
            }
            else
            {
                LogDebug("找到RoomConfigManager");
            }
        }

        /// <summary>
        /// 检查是否可以处理输入
        /// </summary>
        private bool CanProcessInput()
        {
            // 检查输入冷却
            if (Time.time - lastInputTime < INPUT_COOLDOWN)
                return false;

            // 检查UI状态（如果有其他UI打开，可能需要禁用输入）
            if (configManager != null && configManager.IsUIOpen)
                return false;

            return true;
        }

        /// <summary>
        /// 处理键盘输入
        /// </summary>
        private void HandleKeyboardInput()
        {
            // F1 - 配置UI
            if (Input.GetKeyDown(configUIKey))
            {
                HandleConfigUIToggle();
                lastInputTime = Time.time;
            }

            // F2 - 调试信息
            if (Input.GetKeyDown(debugInfoKey))
            {
                HandleDebugInfoToggle();
                lastInputTime = Time.time;
            }

            // F3 - 玩家列表
            if (Input.GetKeyDown(playerListKey))
            {
                HandlePlayerListToggle();
                lastInputTime = Time.time;
            }
        }

        /// <summary>
        /// 处理配置UI切换
        /// </summary>
        private void HandleConfigUIToggle()
        {
            if (configManager == null)
            {
                configManager = FindObjectOfType<RoomConfigManager>();
            }

            if (configManager != null)
            {
                configManager.HandleConfigUIToggle();
            }
            else
            {
                LogDebug("配置管理器不存在，无法打开配置UI");
                ShowMessage("配置功能不可用");
            }
        }

        /// <summary>
        /// 处理调试信息切换
        /// </summary>
        private void HandleDebugInfoToggle()
        {
            LogDebug($"检测到 {debugInfoKey} 按键");

            // 显示当前游戏状态信息
            ShowGameStatusInfo();
        }

        /// <summary>
        /// 处理玩家列表切换
        /// </summary>
        private void HandlePlayerListToggle()
        {
            LogDebug($"检测到 {playerListKey} 按键");

            // 显示当前房间玩家列表
            ShowPlayerListInfo();
        }

        /// <summary>
        /// 检查当前玩家是否为房主
        /// </summary>
        private bool IsCurrentPlayerHost()
        {
            // 检查NetworkManager
            if (NetworkManager.Instance != null)
            {
                bool isHost = NetworkManager.Instance.IsHost;
                LogDebug($"NetworkManager检查 - IsHost: {isHost}");
                return isHost;
            }

            // 检查RoomManager
            if (RoomManager.Instance?.CurrentRoom != null)
            {
                var room = RoomManager.Instance.CurrentRoom;

                // 获取当前玩家ID - 需要从NetworkManager或其他地方获取
                ushort currentPlayerId = GetCurrentPlayerId();

                bool isRoomHost = (currentPlayerId != 0 && currentPlayerId == room.hostId);
                LogDebug($"RoomManager检查 - CurrentPlayerId: {currentPlayerId}, HostId: {room.hostId}, IsHost: {isRoomHost}");

                return isRoomHost;
            }

            // 开发模式允许
            if (Application.isEditor)
            {
                LogDebug("编辑器模式，跳过房主检查");
                return true;
            }

            LogDebug("无法确定房主身份，默认拒绝");
            return false;
        }

        /// <summary>
        /// 获取当前玩家ID
        /// </summary>
        private ushort GetCurrentPlayerId()
        {
            // 优先从NetworkManager获取
            if (NetworkManager.Instance != null)
            {
                return NetworkManager.Instance.ClientId;
            }

            // 如果NetworkManager不可用，尝试其他方式
            // 这里可能需要根据你的具体实现调整

            LogDebug("无法获取当前玩家ID");
            return 0;
        }

        /// <summary>
        /// 检查是否在有效场景中
        /// </summary>
        private bool IsInValidScene()
        {
            string currentScene = SceneManager.GetActiveScene().name;

            foreach (string validScene in validScenes)
            {
                if (currentScene.Contains(validScene))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 场景加载回调
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            LogDebug($"场景切换: {scene.name}");

            // 重新验证场景和初始化
            if (onlyInValidScenes)
            {
                inputEnabled = IsInValidScene();
                LogDebug($"输入处理状态: {(inputEnabled ? "启用" : "禁用")}");
            }

            // 重新查找配置管理器（可能在新场景中）
            if (inputEnabled)
            {
                FindConfigManager();
            }
        }

        /// <summary>
        /// 显示输入提示
        /// </summary>
        private void ShowInputHints()
        {
            var hints = $"=== 房间快捷键 ===\n";
            hints += $"{configUIKey}: 游戏配置 (仅房主)\n";
            hints += $"{debugInfoKey}: 调试信息\n";
            hints += $"{playerListKey}: 玩家列表\n";

            LogDebug(hints);

            // 可以在这里显示UI提示
            // UIMessageManager.ShowMessage(hints, 5f);
        }

        /// <summary>
        /// 显示游戏状态信息
        /// </summary>
        private void ShowGameStatusInfo()
        {
            var info = "=== 游戏状态信息 ===\n";

            // 当前场景
            info += $"当前场景: {SceneManager.GetActiveScene().name}\n";

            // 网络状态
            if (NetworkManager.Instance != null)
            {
                info += $"网络状态: {(NetworkManager.Instance.IsConnected ? "已连接" : "未连接")}\n";
                info += $"是否为主机: {NetworkManager.Instance.IsHost}\n";
                info += $"客户端ID: {NetworkManager.Instance.ClientId}\n";
                info += $"连接玩家数: {NetworkManager.Instance.ConnectedPlayerCount}\n";
            }
            else
            {
                info += "NetworkManager: 未找到\n";
            }

            // 房间状态
            if (RoomManager.Instance?.CurrentRoom != null)
            {
                var room = RoomManager.Instance.CurrentRoom;
                info += $"房间名: {room.roomName}\n";
                info += $"房间玩家数: {room.players.Count}\n";
                info += $"房主ID: {room.hostId}\n";
            }
            else
            {
                info += "房间状态: 未加入房间\n";
            }

            // 配置管理器状态
            if (configManager != null)
            {
                info += $"配置管理器: 已初始化\n";
                info += $"配置UI状态: {(configManager.IsUIOpen ? "已打开" : "已关闭")}\n";
            }
            else
            {
                info += "配置管理器: 未找到\n";
            }

            LogDebug(info);
            ShowMessage("调试信息已输出到控制台");
        }

        /// <summary>
        /// 显示玩家列表信息
        /// </summary>
        private void ShowPlayerListInfo()
        {
            var info = "=== 当前玩家列表 ===\n";

            if (RoomManager.Instance?.CurrentRoom != null)
            {
                var room = RoomManager.Instance.CurrentRoom;

                if (room.players.Count == 0)
                {
                    info += "房间内没有玩家\n";
                }
                else
                {
                    foreach (var player in room.players.Values)
                    {
                        string hostFlag = (player.playerId == room.hostId) ? " [房主]" : "";
                        info += $"- {player.playerName} (ID: {player.playerId}){hostFlag}\n";
                    }
                }
            }
            else
            {
                info += "未加入任何房间\n";
            }

            LogDebug(info);
            ShowMessage("玩家列表已输出到控制台");
        }

        /// <summary>
        /// 显示消息给用户
        /// </summary>
        private void ShowMessage(string message)
        {
            LogDebug($"用户消息: {message}");

            // 这里可以集成实际的UI消息系统
            // 例如：UIMessageManager.ShowMessage(message, 3f);
            // 或者：ToastManager.Show(message);

            // 临时在控制台显示
            Debug.Log($"[用户提示] {message}");
        }

        /// <summary>
        /// 启用输入处理
        /// </summary>
        public void EnableInput()
        {
            inputEnabled = true;
            LogDebug("输入处理已启用");
        }

        /// <summary>
        /// 禁用输入处理
        /// </summary>
        public void DisableInput()
        {
            inputEnabled = false;
            LogDebug("输入处理已禁用");
        }

        /// <summary>
        /// 切换输入处理状态
        /// </summary>
        public void ToggleInput()
        {
            inputEnabled = !inputEnabled;
            LogDebug($"输入处理已{(inputEnabled ? "启用" : "禁用")}");
        }

        /// <summary>
        /// 设置配置UI快捷键
        /// </summary>
        public void SetConfigUIKey(KeyCode keyCode)
        {
            configUIKey = keyCode;
            LogDebug($"配置UI快捷键已设置为: {keyCode}");
        }

        /// <summary>
        /// 获取当前输入状态
        /// </summary>
        public bool IsInputEnabled()
        {
            return inputEnabled && isInitialized;
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[RoomInputHandler] {message}");
            }
        }

        #region 公共接口

        /// <summary>
        /// 手动触发配置UI
        /// </summary>
        public void TriggerConfigUI()
        {
            HandleConfigUIToggle();
        }

        /// <summary>
        /// 手动显示调试信息
        /// </summary>
        public void TriggerDebugInfo()
        {
            HandleDebugInfoToggle();
        }

        /// <summary>
        /// 手动显示玩家列表
        /// </summary>
        public void TriggerPlayerList()
        {
            HandlePlayerListToggle();
        }

        /// <summary>
        /// 获取当前快捷键设置
        /// </summary>
        public KeyCode GetConfigUIKey()
        {
            return configUIKey;
        }

        #endregion

        #region 调试方法

#if UNITY_EDITOR
        [ContextMenu("测试配置UI")]
        public void TestConfigUI()
        {
            TriggerConfigUI();
        }

        [ContextMenu("显示调试信息")]
        public void TestDebugInfo()
        {
            TriggerDebugInfo();
        }

        [ContextMenu("显示玩家列表")]
        public void TestPlayerList()
        {
            TriggerPlayerList();
        }

        [ContextMenu("显示输入提示")]
        public void TestInputHints()
        {
            ShowInputHints();
        }
#endif

        #endregion
    }
}