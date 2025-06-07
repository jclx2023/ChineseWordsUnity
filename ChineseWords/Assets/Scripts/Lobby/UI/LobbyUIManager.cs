using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lobby.Core;
using Lobby.Data;

namespace Lobby.UI
{
    /// <summary>
    /// Lobby UI总管理器
    /// 负责协调各个UI组件的工作
    /// </summary>
    public class LobbyUIManager : MonoBehaviour
    {
        [Header("UI组件引用")]
        [SerializeField] private TMP_InputField playerNameInputField;
        [SerializeField] private Button backToMenuButton;
        [SerializeField] private Button joinRandomButton;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        public static LobbyUIManager Instance { get; private set; }

        // UI状态
        private bool isInitialized = false;

        #region Unity生命周期

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                LogDebug("LobbyUIManager 实例已创建");
            }
            else
            {
                LogDebug("销毁重复的LobbyUIManager实例");
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            InitializeUI();
        }

        private void OnDestroy()
        {
            CleanupUI();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region UI初始化

        /// <summary>
        /// 初始化UI
        /// </summary>
        private void InitializeUI()
        {
            LogDebug("初始化UI组件");

            // 自动查找组件（如果未手动分配）
            FindUIComponents();

            // 绑定事件
            BindUIEvents();

            isInitialized = true;
            LogDebug("UI初始化完成");
        }

        /// <summary>
        /// 查找UI组件
        /// </summary>
        private void FindUIComponents()
        {
            // 查找玩家昵称输入框
            if (playerNameInputField == null)
            {
                playerNameInputField = GameObject.Find("PlayerNameInputField")?.GetComponent<TMP_InputField>();
                if (playerNameInputField == null)
                {
                    Debug.LogWarning("[LobbyUIManager] 未找到PlayerNameInputField");
                }
            }

            // 查找返回主菜单按钮
            if (backToMenuButton == null)
            {
                backToMenuButton = GameObject.Find("BackToMenuButton")?.GetComponent<Button>();
                if (backToMenuButton == null)
                {
                    Debug.LogWarning("[LobbyUIManager] 未找到BackToMenuButton");
                }
            }

            // 查找随机加入按钮
            if (joinRandomButton == null)
            {
                joinRandomButton = GameObject.Find("JoinRandomButton")?.GetComponent<Button>();
                if (joinRandomButton == null)
                {
                    Debug.LogWarning("[LobbyUIManager] 未找到JoinRandomButton");
                }
            }
        }

        /// <summary>
        /// 绑定UI事件
        /// </summary>
        private void BindUIEvents()
        {
            // 玩家昵称输入事件
            if (playerNameInputField != null)
            {
                playerNameInputField.onEndEdit.AddListener(OnPlayerNameChanged);
                LogDebug("已绑定玩家昵称输入事件");
            }

            // 返回主菜单按钮事件
            if (backToMenuButton != null)
            {
                backToMenuButton.onClick.AddListener(OnBackToMenuClicked);
                LogDebug("已绑定返回主菜单按钮事件");
            }

            // 随机加入按钮事件
            if (joinRandomButton != null)
            {
                joinRandomButton.onClick.AddListener(OnJoinRandomClicked);
                LogDebug("已绑定随机加入按钮事件");
            }
        }

        /// <summary>
        /// 清理UI
        /// </summary>
        private void CleanupUI()
        {
            // 移除事件监听
            if (playerNameInputField != null)
            {
                playerNameInputField.onEndEdit.RemoveListener(OnPlayerNameChanged);
            }

            if (backToMenuButton != null)
            {
                backToMenuButton.onClick.RemoveListener(OnBackToMenuClicked);
            }

            if (joinRandomButton != null)
            {
                joinRandomButton.onClick.RemoveListener(OnJoinRandomClicked);
            }

            LogDebug("UI清理完成");
        }

        #endregion

        #region UI事件处理

        /// <summary>
        /// 玩家昵称改变事件
        /// </summary>
        private void OnPlayerNameChanged(string newName)
        {
            LogDebug($"玩家昵称改变: {newName}");

            if (LobbySceneManager.Instance != null)
            {
                LobbySceneManager.Instance.UpdatePlayerName(newName);
            }
        }

        /// <summary>
        /// 返回主菜单按钮点击事件
        /// </summary>
        private void OnBackToMenuClicked()
        {
            LogDebug("点击返回主菜单按钮");

            if (LobbySceneManager.Instance != null)
            {
                LobbySceneManager.Instance.BackToMainMenu();
            }
        }

        /// <summary>
        /// 随机加入按钮点击事件
        /// </summary>
        private void OnJoinRandomClicked()
        {
            LogDebug("点击随机加入按钮");

            if (LobbyNetworkManager.Instance != null)
            {
                LobbyNetworkManager.Instance.JoinRandomRoom();
            }
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 更新玩家信息显示
        /// </summary>
        public void UpdatePlayerInfo(LobbyPlayerData playerData)
        {
            if (!isInitialized || playerData == null)
                return;

            LogDebug($"更新玩家信息: {playerData.playerName}");

            // 更新玩家昵称输入框
            if (playerNameInputField != null)
            {
                playerNameInputField.text = playerData.playerName;
            }
        }

        /// <summary>
        /// 设置随机加入按钮状态
        /// </summary>
        public void SetJoinRandomButtonInteractable(bool interactable)
        {
            if (joinRandomButton != null)
            {
                joinRandomButton.interactable = interactable;
            }
        }

        /// <summary>
        /// 设置返回按钮状态
        /// </summary>
        public void SetBackButtonInteractable(bool interactable)
        {
            if (backToMenuButton != null)
            {
                backToMenuButton.interactable = interactable;
            }
        }

        /// <summary>
        /// 显示提示消息（简单实现）
        /// </summary>
        public void ShowMessage(string message)
        {
            LogDebug($"显示消息: {message}");
            // 这里可以扩展为弹窗或者Toast提示
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
                Debug.Log($"[LobbyUIManager] {message}");
            }
        }

        [ContextMenu("显示UI状态")]
        public void ShowUIStatus()
        {
            string status = "=== Lobby UI状态 ===\n";
            status += $"UI已初始化: {isInitialized}\n";
            status += $"玩家昵称输入框: {(playerNameInputField != null ? "✓" : "✗")}\n";
            status += $"返回按钮: {(backToMenuButton != null ? "✓" : "✗")}\n";
            status += $"随机加入按钮: {(joinRandomButton != null ? "✓" : "✗")}";

            LogDebug(status);
        }

        #endregion
    }
}