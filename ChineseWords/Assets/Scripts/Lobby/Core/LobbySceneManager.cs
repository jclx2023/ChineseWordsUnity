using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using Core.Network;
using Lobby.Data;
using Lobby.UI;

namespace Lobby.Core
{
    /// <summary>
    /// Lobby场景总管理器
    /// 负责场景的初始化、数据管理和场景切换
    /// </summary>
    public class LobbySceneManager : MonoBehaviour
    {
        [Header("场景配置")]
        [SerializeField] private string mainMenuSceneName = "MainMenuScene";
        [SerializeField] private string roomSceneName = "RoomScene";

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        public static LobbySceneManager Instance { get; private set; }

        // 玩家数据
        private LobbyPlayerData currentPlayerData;

        // 场景状态
        private bool isInitialized = false;

        #region Unity生命周期

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                LogDebug("LobbySceneManager 实例已创建");
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
            if (Instance == this)
            {
                Instance = null;
            }
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

            // 初始化UI系统
            InitializeUISystem();

            isInitialized = true;
            LogDebug("Lobby场景初始化完成");
        }

        /// <summary>
        /// 初始化玩家数据
        /// </summary>
        private void InitializePlayerData()
        {
            currentPlayerData = new LobbyPlayerData();

            // 从PlayerPrefs加载玩家数据
            string savedPlayerName = PlayerPrefs.GetString("PlayerName", "");
            if (string.IsNullOrEmpty(savedPlayerName))
            {
                currentPlayerData.playerName = $"玩家{Random.Range(1000, 9999)}";
            }
            else
            {
                currentPlayerData.playerName = savedPlayerName;
            }

            LogDebug($"玩家数据初始化完成: {currentPlayerData.playerName}");
        }

        /// <summary>
        /// 等待网络管理器准备就绪
        /// </summary>
        private IEnumerator WaitForNetworkManager()
        {
            LogDebug("等待网络管理器准备就绪...");

            int waitFrames = 0;
            const int maxWaitFrames = 300; // 5秒超时

            while (LobbyNetworkManager.Instance == null && waitFrames < maxWaitFrames)
            {
                yield return null;
                waitFrames++;
            }

            if (LobbyNetworkManager.Instance == null)
            {
                Debug.LogError("[LobbySceneManager] 网络管理器准备超时");
            }
            else
            {
                LogDebug($"网络管理器准备就绪，等待了 {waitFrames} 帧");
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
            }
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
        /// 更新玩家昵称
        /// </summary>
        public void UpdatePlayerName(string newName)
        {
            if (string.IsNullOrEmpty(newName) || newName.Length < 2)
            {
                LogDebug("无效的玩家昵称");
                return;
            }

            currentPlayerData.playerName = newName;
            PlayerPrefs.SetString("PlayerName", newName);
            PlayerPrefs.Save();

            LogDebug($"玩家昵称已更新: {newName}");
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

            // 切换场景
            SceneManager.LoadScene(mainMenuSceneName);
        }

        /// <summary>
        /// 进入房间场景（为后续阶段预留）
        /// </summary>
        public void EnterRoomScene(LobbyRoomData roomData)
        {
            LogDebug($"进入房间场景: {roomData.roomName}");

            // TODO: 阶段3实现 - 传递房间数据到RoomScene
            // 目前暂时只输出日志
            LogDebug("进入房间功能将在后续阶段实现");
        }

        /// <summary>
        /// 检查场景是否已初始化
        /// </summary>
        public bool IsSceneInitialized()
        {
            return isInitialized;
        }

        #endregion

        #region 调试方法

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

        [ContextMenu("显示场景状态")]
        public void ShowSceneStatus()
        {
            string status = "=== Lobby场景状态 ===\n";
            status += $"场景已初始化: {isInitialized}\n";
            status += $"当前玩家: {currentPlayerData?.playerName ?? "未设置"}\n";
            status += $"网络管理器: {(LobbyNetworkManager.Instance != null ? "就绪" : "未就绪")}\n";
            status += $"UI管理器: {(LobbyUIManager.Instance != null ? "就绪" : "未就绪")}";

            LogDebug(status);
        }

        #endregion
    }
}