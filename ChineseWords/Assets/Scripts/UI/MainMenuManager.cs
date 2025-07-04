using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace UI
{
    /// <summary>
    /// 简化的主菜单管理器 - 专为多人游戏设计，支持自定义按钮
    /// 流程：MainMenu → LobbyScene → RoomScene → NetworkGameScene
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        [Header("主菜单UI - 自定义按钮")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private CustomButton enterLobbyButton;
        [SerializeField] private CustomButton settingsButton;
        [SerializeField] private CustomButton creditsButton;
        [SerializeField] private CustomButton exitButton;

        [Header("设置面板")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Toggle fullscreenToggle;

        [Header("制作名单面板")]
        [SerializeField] private GameObject creditsPanel;
        [SerializeField] private TMP_Text creditsText;

        [Header("场景配置")]
        [SerializeField] private string lobbyScene = "LobbyScene";

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

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
            // 绑定主菜单自定义按钮事件
            enterLobbyButton.AddClickListener(OnEnterLobbyClicked);
            settingsButton.AddClickListener(OnSettingsClicked);
            creditsButton.AddClickListener(OnCreditsClicked);
            exitButton.AddClickListener(OnExitClicked);

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
        /// 显示主菜单（只显示主菜单，隐藏其他面板）
        /// </summary>
        private void ShowMainMenu()
        {
            SetPanelActive(mainMenuPanel, true);
            SetPanelActive(settingsPanel, false);
            SetPanelActive(creditsPanel, false);
        }

        /// <summary>
        /// 显示设置面板（保持主菜单显示）
        /// </summary>
        private void ShowSettingsPanel()
        {
            // 主菜单保持显示，只显示设置面板，隐藏制作名单面板
            SetPanelActive(mainMenuPanel, true);
            SetPanelActive(settingsPanel, true);
            SetPanelActive(creditsPanel, false);
        }

        /// <summary>
        /// 显示制作名单面板（保持主菜单显示）
        /// </summary>
        private void ShowCreditsPanel()
        {
            // 主菜单保持显示，只显示制作名单面板，隐藏设置面板
            SetPanelActive(mainMenuPanel, true);
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
            return @"《语文课堂》

<color=#4A90E2><b>开发团队：</b></color></size>
策划: Alexa
程序: Alexa
美术: Alexa
音效: [音效师名称]

<color=#4A90E2><b>特别感谢：</b></color></size>
所有测试玩家

<color=#4A90E2><b>技术支持：</b></color></size>
Unity 2022.3 LTS
Photon PUN2
Riptide

<color=#4A90E2><b>版本信息：</b></color></size>
版本号: " + Application.version + @"

© 2025 语文课堂开发团队
保留所有权利";
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
            // 清理自定义按钮事件监听
            if (enterLobbyButton != null)
                enterLobbyButton.RemoveClickListener(OnEnterLobbyClicked);
            if (settingsButton != null)
                settingsButton.RemoveClickListener(OnSettingsClicked);
            if (creditsButton != null)
                creditsButton.RemoveClickListener(OnCreditsClicked);
            if (exitButton != null)
                exitButton.RemoveClickListener(OnExitClicked);

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

        #endregion
    }
}