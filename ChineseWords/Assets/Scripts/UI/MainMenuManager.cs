using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace UI
{
    /// <summary>
    /// 简化的主菜单管理器 - 专为多人游戏设计
    /// 流程：MainMenu → LobbyScene → RoomScene → NetworkGameScene
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        [Header("主菜单UI")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private Button enterLobbyButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button exitButton;



        [Header("设置面板")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Button backFromSettingsButton;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Toggle fullscreenToggle;

        [Header("制作名单面板")]
        [SerializeField] private GameObject creditsPanel;
        [SerializeField] private Button backFromCreditsButton;
        [SerializeField] private TMP_Text creditsText;

        [Header("场景配置")]
        [SerializeField] private string lobbyScene = "LobbyScene";

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 游戏模式枚举（保留以防其他脚本引用）
        public enum GameMode
        {
            SinglePlayer,  // 保留但不使用
            Host,          // 保留但不使用
            Client,        // 保留但不使用
            Multiplayer    // 新的统一多人模式
        }

        // 静态属性 - 供其他场景使用
        public static GameMode SelectedGameMode { get; private set; } = GameMode.Multiplayer;
        public static string PlayerName { get; private set; }
        public static string RoomName { get; private set; }  // 保留以防其他脚本引用
        public static string HostIP { get; private set; }    // 保留以防其他脚本引用
        public static ushort Port { get; private set; }      // 保留以防其他脚本引用
        public static int MaxPlayers { get; private set; }   // 保留以防其他脚本引用

        private void Start()
        {
            Application.runInBackground = true;
            InitializeUI();
            ShowMainMenu();
        }

        /// <summary>
        /// 初始化UI组件和事件
        /// </summary>
        private void InitializeUI()
        {
            // 绑定主菜单按钮事件
            if (enterLobbyButton != null)
                enterLobbyButton.onClick.AddListener(OnEnterLobbyClicked);
            if (settingsButton != null)
                settingsButton.onClick.AddListener(OnSettingsClicked);
            if (creditsButton != null)
                creditsButton.onClick.AddListener(OnCreditsClicked);
            if (exitButton != null)
                exitButton.onClick.AddListener(OnExitClicked);

            // 绑定设置面板按钮事件
            if (backFromSettingsButton != null)
                backFromSettingsButton.onClick.AddListener(OnBackFromSettingsClicked);

            // 绑定制作名单面板按钮事件
            if (backFromCreditsButton != null)
                backFromCreditsButton.onClick.AddListener(OnBackFromCreditsClicked);

            // 初始化设置
            InitializeSettings();

            LogDebug("MainMenuManager 初始化完成");
        }

        /// <summary>
        /// 初始化游戏设置
        /// </summary>
        private void InitializeSettings()
        {
            // 音量设置
            if (volumeSlider != null)
            {
                float savedVolume = PlayerPrefs.GetFloat("MasterVolume", 0.8f);
                volumeSlider.value = savedVolume;
                AudioListener.volume = savedVolume;
                volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            }

            // 全屏设置
            if (fullscreenToggle != null)
            {
                bool isFullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
                fullscreenToggle.isOn = isFullscreen;
                Screen.fullScreen = isFullscreen;
                fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggleChanged);
            }
        }

        /// <summary>
        /// 显示主菜单
        /// </summary>
        private void ShowMainMenu()
        {
            SetPanelActive(mainMenuPanel, true);
            SetPanelActive(settingsPanel, false);
            SetPanelActive(creditsPanel, false);
        }

        /// <summary>
        /// 显示设置面板
        /// </summary>
        private void ShowSettingsPanel()
        {
            SetPanelActive(mainMenuPanel, false);
            SetPanelActive(settingsPanel, true);
            SetPanelActive(creditsPanel, false);
        }

        /// <summary>
        /// 显示制作名单面板
        /// </summary>
        private void ShowCreditsPanel()
        {
            SetPanelActive(mainMenuPanel, false);
            SetPanelActive(settingsPanel, false);
            SetPanelActive(creditsPanel, true);
        }

        /// <summary>
        /// 安全设置面板激活状态
        /// </summary>
        private void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
                panel.SetActive(active);
        }

        #region 按钮事件处理

        /// <summary>
        /// 进入大厅按钮点击
        /// </summary>
        private void OnEnterLobbyClicked()
        {
            // 设置游戏模式（保留以防其他脚本引用）
            SelectedGameMode = GameMode.Multiplayer;

            LogDebug("进入大厅");

            // 进入大厅场景
            LoadLobbyScene();
        }

        /// <summary>
        /// 设置按钮点击
        /// </summary>
        private void OnSettingsClicked()
        {
            LogDebug("打开设置面板");
            ShowSettingsPanel();
        }

        /// <summary>
        /// 制作名单按钮点击
        /// </summary>
        private void OnCreditsClicked()
        {
            LogDebug("打开制作名单");
            ShowCreditsPanel();
            LoadCreditsContent();
        }

        /// <summary>
        /// 退出游戏按钮点击
        /// </summary>
        private void OnExitClicked()
        {
            LogDebug("退出游戏");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// 从设置返回按钮点击
        /// </summary>
        private void OnBackFromSettingsClicked()
        {
            LogDebug("从设置返回主菜单");
            ShowMainMenu();
        }

        /// <summary>
        /// 从制作名单返回按钮点击
        /// </summary>
        private void OnBackFromCreditsClicked()
        {
            LogDebug("从制作名单返回主菜单");
            ShowMainMenu();
        }

        #endregion

        #region 输入事件处理

        /// <summary>
        /// 音量滑条变更
        /// </summary>
        private void OnVolumeChanged(float volume)
        {
            AudioListener.volume = volume;
            PlayerPrefs.SetFloat("MasterVolume", volume);
            LogDebug($"音量设置为: {volume:F2}");
        }

        /// <summary>
        /// 全屏切换
        /// </summary>
        private void OnFullscreenToggleChanged(bool isFullscreen)
        {
            Screen.fullScreen = isFullscreen;
            PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
            LogDebug($"全屏模式: {isFullscreen}");
        }

        #endregion

        #region 辅助方法



        /// <summary>
        /// 加载大厅场景
        /// </summary>
        private void LoadLobbyScene()
        {
            if (string.IsNullOrEmpty(lobbyScene))
            {
                Debug.LogError("大厅场景名称未设置");
                return;
            }

            try
            {
                LogDebug($"加载大厅场景: {lobbyScene}");
                SceneManager.LoadScene(lobbyScene);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"加载大厅场景失败: {lobbyScene}, 错误: {e.Message}");
            }
        }

        /// <summary>
        /// 加载制作名单内容
        /// </summary>
        private void LoadCreditsContent()
        {
            if (creditsText != null)
            {
                string credits = GetCreditsContent();
                creditsText.text = credits;
            }
        }

        /// <summary>
        /// 获取制作名单内容
        /// </summary>
        private string GetCreditsContent()
        {
            return @"《中文词汇量大挑战》

开发团队：
策划: Alexa
程序: [开发者名称]
美术: [美术师名称]
音效: [音效师名称]

特别感谢：
Unity Technologies
Photon Network
所有测试玩家

技术支持：
Unity 2022.3 LTS
Photon PUN2
TextMeshPro

版本信息：
版本号: " + Application.version + @"
构建日期: " + System.DateTime.Now.ToString("yyyy-MM-dd") + @"

© 2025 中文词汇量大挑战开发团队
保留所有权利";
        }



        #endregion

        #region 兼容性方法（保留以防其他脚本引用）

        /// <summary>
        /// 兼容方法：单机游戏（已废弃）
        /// </summary>
        [System.Obsolete("单机模式已移除，请使用进入大厅")]
        public void OnSinglePlayerClicked()
        {
            LogDebug("单机模式已移除，转向进入大厅");
            OnEnterLobbyClicked();
        }

        /// <summary>
        /// 兼容方法：开始游戏（已废弃）
        /// </summary>
        [System.Obsolete("请使用进入大厅")]
        public void OnStartGameClicked()
        {
            LogDebug("开始游戏已改为进入大厅");
            OnEnterLobbyClicked();
        }

        /// <summary>
        /// 兼容方法：创建房间（已废弃）
        /// </summary>
        [System.Obsolete("请在大厅场景中创建房间")]
        public void OnCreateRoomClicked()
        {
            LogDebug("请在大厅场景中创建房间");
            OnEnterLobbyClicked();
        }

        /// <summary>
        /// 兼容方法：加入房间（已废弃）
        /// </summary>
        [System.Obsolete("请在大厅场景中加入房间")]
        public void OnJoinRoomClicked()
        {
            LogDebug("请在大厅场景中加入房间");
            OnEnterLobbyClicked();
        }

        #endregion

        #region Unity生命周期

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                // 保存当前设置
                PlayerPrefs.Save();
            }
        }

        private void OnDestroy()
        {
            // 清理按钮事件监听
            if (enterLobbyButton != null)
                enterLobbyButton.onClick.RemoveAllListeners();
            if (settingsButton != null)
                settingsButton.onClick.RemoveAllListeners();
            if (creditsButton != null)
                creditsButton.onClick.RemoveAllListeners();
            if (exitButton != null)
                exitButton.onClick.RemoveAllListeners();
            if (backFromSettingsButton != null)
                backFromSettingsButton.onClick.RemoveAllListeners();
            if (backFromCreditsButton != null)
                backFromCreditsButton.onClick.RemoveAllListeners();

            // 清理输入事件监听
            if (volumeSlider != null)
                volumeSlider.onValueChanged.RemoveAllListeners();
            if (fullscreenToggle != null)
                fullscreenToggle.onValueChanged.RemoveAllListeners();

            LogDebug("MainMenuManager 资源已清理");
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
                Debug.Log($"[MainMenuManager] {message}");
            }
        }

        [ContextMenu("显示当前状态")]
        public void ShowCurrentStatus()
        {
            string status = "=== MainMenuManager 状态 ===\n";
            status += $"游戏模式: {SelectedGameMode}\n";
            status += $"大厅场景: {lobbyScene}\n";
            status += $"主音量: {AudioListener.volume:F2}\n";
            status += $"全屏模式: {Screen.fullScreen}\n";

            Debug.Log(status);
        }

        [ContextMenu("重置游戏设置")]
        public void ResetGameSettings()
        {
            PlayerPrefs.DeleteKey("MasterVolume");
            PlayerPrefs.DeleteKey("Fullscreen");
            PlayerPrefs.Save();

            LogDebug("游戏设置已重置");
        }

        [ContextMenu("测试进入大厅")]
        public void TestEnterLobby()
        {
            if (Application.isPlaying)
            {
                OnEnterLobbyClicked();
            }
        }

        #endregion
    }
}