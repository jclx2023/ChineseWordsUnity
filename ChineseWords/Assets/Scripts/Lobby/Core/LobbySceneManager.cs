using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using Core;
using Core.Network;
using Lobby.Data;
using Lobby.UI;
using Photon.Pun;

namespace Lobby.Core
{
    /// <summary>
    /// Lobby场景总管理器 - 完整实现版
    /// 负责场景的初始化、数据管理、网络事件响应和场景切换
    /// </summary>
    public class LobbySceneManager : MonoBehaviourPun
    {
        [Header("场景配置")]
        [SerializeField] private string mainMenuSceneName = "MainMenuScene";
        [SerializeField] private string roomSceneName = "RoomScene";
        [SerializeField] private float roomJoinTransitionDelay = 1f;

        [Header("数据持久化")]
        [SerializeField] private bool persistPlayerData = true;
        [SerializeField] private bool autoSavePlayerPrefs = true;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        public static LobbySceneManager Instance { get; private set; }

        // 玩家数据
        private LobbyPlayerData currentPlayerData;
        private LobbyRoomData lastJoinedRoomData; // 缓存最后加入的房间数据

        // 场景状态
        private bool isInitialized = false;
        private bool isTransitioningToRoom = false;

        // 网络事件状态
        private bool hasSubscribedToNetworkEvents = false;

        #region Unity生命周期

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                LogDebug("LobbySceneManager 实例已创建");

                // 设置为持久化对象，以便在场景切换时保持数据
                if (persistPlayerData)
                {
                    DontDestroyOnLoad(gameObject);
                    LogDebug("LobbySceneManager 设置为跨场景持久化");
                }
            }
            else
            {
                LogDebug("销毁重复的LobbySceneManager实例");
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            StartCoroutine(InitializeSceneCoroutine());
        }

        private void OnDestroy()
        {
            // 取消订阅网络事件
            UnsubscribeFromNetworkEvents();

            if (Instance == this)
            {
                Instance = null;
            }

            LogDebug("LobbySceneManager 已销毁");
        }

        #endregion

        #region 场景初始化

        /// <summary>
        /// 场景初始化协程
        /// </summary>
        private IEnumerator InitializeSceneCoroutine()
        {
            LogDebug("开始初始化Lobby场景");

            // 初始化玩家数据
            InitializePlayerData();

            // 等待网络管理器准备就绪
            yield return StartCoroutine(WaitForNetworkManager());

            // 订阅网络事件
            SubscribeToNetworkEvents();

            // 初始化UI系统
            InitializeUISystem();

            // 确保SceneTransitionManager存在
            EnsureSceneTransitionManager();

            isInitialized = true;
            LogDebug("Lobby场景初始化完成");
        }

        /// <summary>
        /// 初始化玩家数据
        /// </summary>
        private void InitializePlayerData()
        {
            LogDebug("初始化玩家数据");

            // 创建新的玩家数据或从持久化数据恢复
            if (currentPlayerData == null)
            {
                currentPlayerData = new LobbyPlayerData();

                // 从PlayerPrefs加载保存的玩家数据
                string savedPlayerName = PlayerPrefs.GetString("PlayerName", "");
                if (string.IsNullOrEmpty(savedPlayerName))
                {
                    currentPlayerData.playerName = GenerateRandomPlayerName();
                    LogDebug($"生成随机玩家名: {currentPlayerData.playerName}");
                }
                else
                {
                    currentPlayerData.playerName = savedPlayerName;
                    LogDebug($"从PlayerPrefs恢复玩家名: {currentPlayerData.playerName}");
                }

                // 设置玩家的唯一ID
                currentPlayerData.playerId = GetOrCreatePlayerId();

                // 自动保存
                if (autoSavePlayerPrefs)
                {
                    SavePlayerDataToPrefs();
                }
            }
            else
            {
                LogDebug($"使用现有玩家数据: {currentPlayerData.playerName}");
            }

            LogDebug($"玩家数据初始化完成 - 名称: {currentPlayerData.playerName}, ID: {currentPlayerData.playerId}");
        }

        /// <summary>
        /// 等待网络管理器准备就绪
        /// </summary>
        private IEnumerator WaitForNetworkManager()
        {
            LogDebug("等待LobbyNetworkManager准备就绪...");

            int waitFrames = 0;
            const int maxWaitFrames = 300; // 5秒超时

            while (LobbyNetworkManager.Instance == null && waitFrames < maxWaitFrames)
            {
                yield return null;
                waitFrames++;
            }

            if (LobbyNetworkManager.Instance == null)
            {
                Debug.LogError("[LobbySceneManager] LobbyNetworkManager准备超时！请检查LobbyNetworkManager是否存在于场景中");
            }
            else
            {
                LogDebug($"LobbyNetworkManager准备就绪，等待了 {waitFrames} 帧");
            }
        }

        /// <summary>
        /// 初始化UI系统
        /// </summary>
        private void InitializeUISystem()
        {
            LogDebug("初始化UI系统");

            // 更新玩家信息UI
            if (LobbyUIManager.Instance != null)
            {
                LobbyUIManager.Instance.UpdatePlayerInfo(currentPlayerData);
                LogDebug("已更新LobbyUIManager的玩家信息");
            }
            else
            {
                Debug.LogWarning("[LobbySceneManager] LobbyUIManager实例不存在");
            }
        }

        /// <summary>
        /// 确保SceneTransitionManager存在
        /// </summary>
        private void EnsureSceneTransitionManager()
        {
            if (SceneTransitionManager.Instance == null)
            {
                LogDebug("SceneTransitionManager不存在，尝试创建");

                // 尝试从Resources加载或创建
                GameObject stmPrefab = Resources.Load<GameObject>("SceneTransitionManager");
                if (stmPrefab != null)
                {
                    Instantiate(stmPrefab);
                    LogDebug("从Resources创建了SceneTransitionManager");
                }
                else
                {
                    // 创建空的GameObject并添加组件
                    GameObject stmObject = new GameObject("SceneTransitionManager");
                    stmObject.AddComponent<SceneTransitionManager>();
                    LogDebug("手动创建了SceneTransitionManager");
                }
            }
            else
            {
                LogDebug("SceneTransitionManager已存在");
            }
        }

        #endregion

        #region 网络事件管理

        /// <summary>
        /// 订阅网络事件
        /// </summary>
        private void SubscribeToNetworkEvents()
        {
            if (hasSubscribedToNetworkEvents)
            {
                LogDebug("已经订阅了网络事件，跳过重复订阅");
                return;
            }

            if (LobbyNetworkManager.Instance != null)
            {
                // 订阅房间相关事件
                LobbyNetworkManager.Instance.OnRoomJoined += OnRoomJoinedHandler;
                LobbyNetworkManager.Instance.OnRoomCreated += OnRoomCreatedHandler;
                LobbyNetworkManager.Instance.OnConnectionStatusChanged += OnConnectionStatusChangedHandler;

                hasSubscribedToNetworkEvents = true;
                LogDebug("已订阅LobbyNetworkManager事件");
            }
            else
            {
                Debug.LogError("[LobbySceneManager] 无法订阅事件：LobbyNetworkManager实例不存在");
            }
        }

        /// <summary>
        /// 取消订阅网络事件
        /// </summary>
        private void UnsubscribeFromNetworkEvents()
        {
            if (!hasSubscribedToNetworkEvents)
                return;

            if (LobbyNetworkManager.Instance != null)
            {
                LobbyNetworkManager.Instance.OnRoomJoined -= OnRoomJoinedHandler;
                LobbyNetworkManager.Instance.OnRoomCreated -= OnRoomCreatedHandler;
                LobbyNetworkManager.Instance.OnConnectionStatusChanged -= OnConnectionStatusChangedHandler;

                LogDebug("已取消订阅LobbyNetworkManager事件");
            }

            hasSubscribedToNetworkEvents = false;
        }

        #endregion

        #region 网络事件处理器

        /// <summary>
        /// 房间加入成功事件处理器 - 核心实现
        /// </summary>
        private void OnRoomJoinedHandler(string roomName, bool success)
        {
            LogDebug($"收到房间加入事件 - 房间: {roomName}, 成功: {success}");

            if (success)
            {
                LogDebug($"成功加入房间: {roomName}，准备切换到RoomScene");

                // 缓存房间信息（从Photon获取最新数据）
                CacheCurrentRoomData();

                // 设置Photon玩家属性
                SetPhotonPlayerProperties();

                // 开始场景切换流程
                StartRoomSceneTransition();
            }
            else
            {
                Debug.LogError($"[LobbySceneManager] 加入房间失败: {roomName}");

                // 显示错误提示
                if (LobbyUIManager.Instance != null)
                {
                    LobbyUIManager.Instance.ShowMessage($"加入房间 '{roomName}' 失败，请重试");
                }
            }
        }

        /// <summary>
        /// 房间创建成功事件处理器
        /// </summary>
        private void OnRoomCreatedHandler(string roomName, bool success)
        {
            LogDebug($"收到房间创建事件 - 房间: {roomName}, 成功: {success}");

            if (success)
            {
                LogDebug($"成功创建房间: {roomName}，等待自动加入");
                // 房间创建成功后会自动加入，由OnRoomJoinedHandler处理后续流程
            }
            else
            {
                Debug.LogError($"[LobbySceneManager] 创建房间失败: {roomName}");

                // 显示错误提示
                if (LobbyUIManager.Instance != null)
                {
                    LobbyUIManager.Instance.ShowMessage($"创建房间 '{roomName}' 失败，请重试");
                }
            }
        }

        /// <summary>
        /// 连接状态变化事件处理器
        /// </summary>
        private void OnConnectionStatusChangedHandler(bool isConnected)
        {
            LogDebug($"网络连接状态变化: {isConnected}");

            if (!isConnected)
            {
                // 连接断开时重置状态
                isTransitioningToRoom = false;
                lastJoinedRoomData = null;

                LogDebug("网络连接断开，重置房间相关状态");
            }
        }

        #endregion

        #region 房间场景切换流程

        /// <summary>
        /// 开始房间场景切换流程
        /// </summary>
        private void StartRoomSceneTransition()
        {
            if (isTransitioningToRoom)
            {
                LogDebug("已在切换到房间场景的流程中，跳过重复切换");
                return;
            }

            if (SceneTransitionManager.IsTransitioning)
            {
                LogDebug("SceneTransitionManager正在切换场景，等待完成");
                return;
            }

            LogDebug("开始房间场景切换流程");
            isTransitioningToRoom = true;

            // 使用SceneTransitionManager进行安全的场景切换
            bool transitionStarted = SceneTransitionManager.Instance?.TransitionToScene(
                roomSceneName,
                roomJoinTransitionDelay,
                "LobbySceneManager.OnRoomJoined"
            ) ?? false;

            if (transitionStarted)
            {
                LogDebug($"场景切换已启动，将在 {roomJoinTransitionDelay} 秒后切换到 {roomSceneName}");

                // 显示切换提示
                if (LobbyUIManager.Instance != null)
                {
                    LobbyUIManager.Instance.ShowMessage($"正在进入房间...");
                }
            }
            else
            {
                Debug.LogError("[LobbySceneManager] 场景切换启动失败");
                isTransitioningToRoom = false;

                // 显示错误提示
                if (LobbyUIManager.Instance != null)
                {
                    LobbyUIManager.Instance.ShowMessage("进入房间失败，请重试");
                }
            }
        }

        /// <summary>
        /// 缓存当前房间数据
        /// </summary>
        private void CacheCurrentRoomData()
        {
            if (!PhotonNetwork.InRoom)
            {
                LogDebug("不在Photon房间中，无法缓存房间数据");
                return;
            }

            var currentRoom = PhotonNetwork.CurrentRoom;
            LogDebug($"缓存房间数据: {currentRoom.Name}");

            // 从Photon房间创建LobbyRoomData
            lastJoinedRoomData = LobbyRoomData.FromPhotonRoom(currentRoom);

            if (lastJoinedRoomData != null)
            {
                LogDebug($"房间数据缓存成功 - 名称: {lastJoinedRoomData.roomName}, 玩家: {lastJoinedRoomData.currentPlayers}/{lastJoinedRoomData.maxPlayers}");
            }
            else
            {
                Debug.LogError("[LobbySceneManager] 从Photon房间创建LobbyRoomData失败");
            }
        }

        /// <summary>
        /// 设置Photon玩家属性
        /// </summary>
        private void SetPhotonPlayerProperties()
        {
            if (!PhotonNetwork.InRoom || currentPlayerData == null)
            {
                LogDebug("无法设置Photon玩家属性：不在房间中或玩家数据为空");
                return;
            }

            LogDebug($"设置Photon玩家属性: {currentPlayerData.playerName}");

            // 设置玩家昵称
            PhotonNetwork.LocalPlayer.NickName = currentPlayerData.playerName;

            // 设置自定义玩家属性
            var playerProps = new ExitGames.Client.Photon.Hashtable();
            playerProps["playerName"] = currentPlayerData.playerName;
            playerProps["playerId"] = currentPlayerData.playerId;
            playerProps["joinTime"] = System.DateTime.Now.ToBinary();

            PhotonNetwork.LocalPlayer.SetCustomProperties(playerProps);

            LogDebug("Photon玩家属性设置完成");
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 获取当前玩家数据
        /// </summary>
        public LobbyPlayerData GetCurrentPlayerData()
        {
            return currentPlayerData;
        }

        /// <summary>
        /// 获取最后加入的房间数据
        /// </summary>
        public LobbyRoomData GetLastJoinedRoomData()
        {
            return lastJoinedRoomData;
        }

        /// <summary>
        /// 更新玩家昵称
        /// </summary>
        public void UpdatePlayerName(string newName)
        {
            if (string.IsNullOrEmpty(newName) || newName.Length < 2)
            {
                LogDebug($"无效的玩家昵称: '{newName}'");
                return;
            }

            // 清理玩家昵称
            newName = newName.Trim();
            if (newName.Length > 20) // 限制昵称长度
            {
                newName = newName.Substring(0, 20);
            }

            string oldName = currentPlayerData.playerName;
            currentPlayerData.playerName = newName;

            // 保存到PlayerPrefs
            if (autoSavePlayerPrefs)
            {
                SavePlayerDataToPrefs();
            }

            LogDebug($"玩家昵称已更新: '{oldName}' -> '{newName}'");

            // 更新UI
            if (LobbyUIManager.Instance != null)
            {
                LobbyUIManager.Instance.UpdatePlayerInfo(currentPlayerData);
            }
        }

        /// <summary>
        /// 返回主菜单
        /// </summary>
        public void BackToMainMenu()
        {
            LogDebug("返回主菜单");

            // 断开网络连接
            if (LobbyNetworkManager.Instance != null)
            {
                LobbyNetworkManager.Instance.Disconnect();
            }

            // 重置状态
            isTransitioningToRoom = false;
            lastJoinedRoomData = null;

            // 使用SceneTransitionManager切换场景
            bool transitionStarted = SceneTransitionManager.ReturnToMainMenu("LobbySceneManager.BackToMainMenu");

            if (!transitionStarted)
            {
                // 备用方案：直接切换
                Debug.LogWarning("[LobbySceneManager] SceneTransitionManager切换失败，使用备用方案");
                SceneManager.LoadScene(mainMenuSceneName);
            }
        }

        /// <summary>
        /// 进入房间场景（手动调用版本）
        /// </summary>
        public void EnterRoomScene(LobbyRoomData roomData = null)
        {
            if (roomData != null)
            {
                LogDebug($"手动进入房间场景: {roomData.roomName}");
                lastJoinedRoomData = roomData;
            }
            else
            {
                LogDebug("手动进入房间场景（使用缓存的房间数据）");
            }

            // 检查是否在Photon房间中
            if (!PhotonNetwork.InRoom)
            {
                Debug.LogWarning("[LobbySceneManager] 不在Photon房间中，但仍尝试切换场景");
            }

            StartRoomSceneTransition();
        }

        /// <summary>
        /// 检查场景是否已初始化
        /// </summary>
        public bool IsSceneInitialized()
        {
            return isInitialized;
        }

        /// <summary>
        /// 检查是否正在切换到房间场景
        /// </summary>
        public bool IsTransitioningToRoom()
        {
            return isTransitioningToRoom;
        }

        /// <summary>
        /// 重置房间切换状态（用于错误恢复）
        /// </summary>
        public void ResetRoomTransitionState()
        {
            LogDebug("重置房间切换状态");
            isTransitioningToRoom = false;
        }

        #endregion

        #region 数据持久化

        /// <summary>
        /// 保存玩家数据到PlayerPrefs
        /// </summary>
        private void SavePlayerDataToPrefs()
        {
            if (currentPlayerData == null) return;

            PlayerPrefs.SetString("PlayerName", currentPlayerData.playerName);
            PlayerPrefs.SetString("PlayerId", currentPlayerData.playerId);
            PlayerPrefs.Save();

            LogDebug($"玩家数据已保存到PlayerPrefs: {currentPlayerData.playerName}");
        }

        /// <summary>
        /// 生成随机玩家名
        /// </summary>
        private string GenerateRandomPlayerName()
        {
            string[] prefixes = { "玩家", "用户", "访客", "新手" };
            string prefix = prefixes[Random.Range(0, prefixes.Length)];
            int number = Random.Range(1000, 9999);
            return $"{prefix}{number}";
        }

        /// <summary>
        /// 获取或创建玩家ID
        /// </summary>
        private string GetOrCreatePlayerId()
        {
            string savedId = PlayerPrefs.GetString("PlayerId", "");
            if (string.IsNullOrEmpty(savedId))
            {
                savedId = System.Guid.NewGuid().ToString();
                PlayerPrefs.SetString("PlayerId", savedId);
                PlayerPrefs.Save();
                LogDebug($"创建新的玩家ID: {savedId}");
            }
            else
            {
                LogDebug($"使用已保存的玩家ID: {savedId}");
            }
            return savedId;
        }

        #endregion

        #region 状态查询和调试

        /// <summary>
        /// 获取场景状态信息
        /// </summary>
        public string GetSceneStatusInfo()
        {
            return $"初始化: {isInitialized}, " +
                   $"切换中: {isTransitioningToRoom}, " +
                   $"网络订阅: {hasSubscribedToNetworkEvents}, " +
                   $"玩家: {currentPlayerData?.playerName ?? "未设置"}, " +
                   $"在房间中: {PhotonNetwork.InRoom}, " +
                   $"网络管理器: {(LobbyNetworkManager.Instance != null ? "存在" : "不存在")}";
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[LobbySceneManager] {message}");
            }
        }

        #endregion

        #region 调试方法

        [ContextMenu("显示场景状态")]
        public void ShowSceneStatus()
        {
            string status = "=== Lobby场景状态 ===\n";
            status += GetSceneStatusInfo() + "\n";

            if (lastJoinedRoomData != null)
            {
                status += $"最后房间: {lastJoinedRoomData.roomName}\n";
            }

            if (LobbyNetworkManager.Instance != null)
            {
                status += $"网络状态: {LobbyNetworkManager.Instance.GetNetworkStats()}";
            }

            LogDebug(status);
        }

        [ContextMenu("强制重置状态")]
        public void ForceResetState()
        {
            isTransitioningToRoom = false;
            lastJoinedRoomData = null;
            SceneTransitionManager.ResetTransitionState();
            LogDebug("已强制重置所有状态");
        }

        [ContextMenu("测试场景切换")]
        public void TestSceneTransition()
        {
            if (Application.isPlaying)
            {
                LogDebug("测试场景切换到RoomScene");
                EnterRoomScene();
            }
        }

        [ContextMenu("保存玩家数据")]
        public void SavePlayerData()
        {
            if (Application.isPlaying)
            {
                SavePlayerDataToPrefs();
            }
        }

        #endregion
    }
}