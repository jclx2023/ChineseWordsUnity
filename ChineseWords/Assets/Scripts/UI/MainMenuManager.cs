using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

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

        [Header("标题图片")]
        [SerializeField] private GameObject titleImage;  // 新增：标题图片引用

        [Header("设置面板")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Slider masterVolumeSlider;      // 主音量
        [SerializeField] private Slider musicVolumeSlider;       // 音乐音量
        [SerializeField] private Slider sfxVolumeSlider;         // 音效音量
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private TMP_Dropdown resolutionDropdown; // 分辨率下拉框

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
            InitializeVolumeSettings();
            InitializeDisplaySettings();
        }

        /// <summary>
        /// 初始化音量设置
        /// </summary>
        private void InitializeVolumeSettings()
        {
            // 主音量设置
            if (masterVolumeSlider != null)
            {
                float savedMasterVolume = PlayerPrefs.GetFloat("MasterVolume", 0.8f);
                masterVolumeSlider.value = savedMasterVolume;
                AudioListener.volume = savedMasterVolume;
                masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
                LogDebug($"主音量初始化: {savedMasterVolume:F2}");
            }

            // 音乐音量设置
            if (musicVolumeSlider != null)
            {
                float savedMusicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.7f);
                musicVolumeSlider.value = savedMusicVolume;
                musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
                LogDebug($"音乐音量初始化: {savedMusicVolume:F2}");
            }

            // 音效音量设置
            if (sfxVolumeSlider != null)
            {
                float savedSFXVolume = PlayerPrefs.GetFloat("SFXVolume", 0.8f);
                sfxVolumeSlider.value = savedSFXVolume;
                sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
                LogDebug($"音效音量初始化: {savedSFXVolume:F2}");
            }
        }

        /// <summary>
        /// 初始化显示设置
        /// </summary>
        private void InitializeDisplaySettings()
        {
            // 全屏设置
            if (fullscreenToggle != null)
            {
                bool isFullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
                fullscreenToggle.isOn = isFullscreen;
                Screen.fullScreen = isFullscreen;
                fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggleChanged);
                LogDebug($"全屏模式初始化: {isFullscreen}");
            }

            // 分辨率设置
            InitializeResolutionDropdown();
        }

        /// <summary>
        /// 初始化分辨率下拉框
        /// </summary>
        private void InitializeResolutionDropdown()
        {
            if (resolutionDropdown != null)
            {
                // 清空现有选项
                resolutionDropdown.ClearOptions();

                // 添加分辨率选项
                var resolutionOptions = new List<TMP_Dropdown.OptionData>
                {
                    new TMP_Dropdown.OptionData("1920x1080 (Full HD)"),
                    new TMP_Dropdown.OptionData("2560x1440 (2K)"),
                    new TMP_Dropdown.OptionData("3840x2160 (4K)")
                };

                resolutionDropdown.AddOptions(resolutionOptions);

                // 设置当前分辨率
                int savedResolution = PlayerPrefs.GetInt("Resolution", 0);
                resolutionDropdown.value = savedResolution;
                ApplyResolution(savedResolution);

                // 绑定事件
                resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);

                LogDebug($"分辨率下拉框初始化完成，当前选择: {savedResolution}");
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
            SetPanelActive(titleImage, true);  // 显示标题图片
        }

        /// <summary>
        /// 显示设置面板（保持主菜单显示，隐藏标题图片）
        /// </summary>
        private void ShowSettingsPanel()
        {
            // 主菜单保持显示，只显示设置面板，隐藏制作名单面板和标题图片
            SetPanelActive(mainMenuPanel, true);
            SetPanelActive(settingsPanel, true);
            SetPanelActive(creditsPanel, false);
            SetPanelActive(titleImage, false);  // 隐藏标题图片
        }

        /// <summary>
        /// 显示制作名单面板（保持主菜单显示，隐藏标题图片）
        /// </summary>
        private void ShowCreditsPanel()
        {
            // 主菜单保持显示，只显示制作名单面板，隐藏设置面板和标题图片
            SetPanelActive(mainMenuPanel, true);
            SetPanelActive(settingsPanel, false);
            SetPanelActive(creditsPanel, true);
            SetPanelActive(titleImage, false);  // 隐藏标题图片
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
        /// 主音量滑条变更
        /// </summary>
        private void OnMasterVolumeChanged(float volume)
        {
            AudioListener.volume = volume;
            PlayerPrefs.SetFloat("MasterVolume", volume);
            LogDebug($"主音量设置为: {volume:F2}");
        }

        /// <summary>
        /// 音乐音量滑条变更
        /// </summary>
        private void OnMusicVolumeChanged(float volume)
        {
            PlayerPrefs.SetFloat("MusicVolume", volume);

            // 通知音乐管理器更新音量
            GameObject musicManager = GameObject.FindWithTag("MusicManager");
            if (musicManager != null)
            {
                var audioSource = musicManager.GetComponent<AudioSource>();
                if (audioSource != null)
                {
                    audioSource.volume = volume;
                }
            }

            LogDebug($"音乐音量设置为: {volume:F2}");
        }

        /// <summary>
        /// 音效音量滑条变更
        /// </summary>
        private void OnSFXVolumeChanged(float volume)
        {
            PlayerPrefs.SetFloat("SFXVolume", volume);

            // 通知音效管理器更新音量
            GameObject sfxManager = GameObject.FindWithTag("SFXManager");
            if (sfxManager != null)
            {
                var audioSources = sfxManager.GetComponents<AudioSource>();
                foreach (var audioSource in audioSources)
                {
                    audioSource.volume = volume;
                }
            }

            LogDebug($"音效音量设置为: {volume:F2}");
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

        /// <summary>
        /// 分辨率下拉框变更
        /// </summary>
        private void OnResolutionChanged(int resolutionIndex)
        {
            PlayerPrefs.SetInt("Resolution", resolutionIndex);
            ApplyResolution(resolutionIndex);
            LogDebug($"分辨率变更为索引: {resolutionIndex}");
        }

        /// <summary>
        /// 应用分辨率设置
        /// </summary>
        private void ApplyResolution(int resolutionIndex)
        {
            switch (resolutionIndex)
            {
                case 0: // 1920x1080
                    Screen.SetResolution(1920, 1080, Screen.fullScreen);
                    LogDebug("应用分辨率: 1920x1080");
                    break;
                case 1: // 2560x1440
                    Screen.SetResolution(2560, 1440, Screen.fullScreen);
                    LogDebug("应用分辨率: 2560x1440");
                    break;
                case 2: // 3840x2160
                    Screen.SetResolution(3840, 2160, Screen.fullScreen);
                    LogDebug("应用分辨率: 3840x2160");
                    break;
                default:
                    LogDebug($"未知的分辨率索引: {resolutionIndex}，使用默认分辨率");
                    Screen.SetResolution(1920, 1080, Screen.fullScreen);
                    break;
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 加载大厅场景
        /// </summary>
        private void LoadLobbyScene()
        {
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
            return @"

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

<color=#4A90E2><b>版本信息：</b></color></size>
版本号: " + Application.version + @"

© 2025 语文课堂开发团队
保留所有权利";
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 返回主菜单（显示标题图片）
        /// </summary>
        public void ReturnToMainMenu()
        {
            LogDebug("返回主菜单");
            ShowMainMenu();
        }

        /// <summary>
        /// 关闭当前面板并返回主菜单
        /// </summary>
        public void CloseCurrentPanel()
        {
            LogDebug("关闭当前面板");
            ShowMainMenu();
        }

        /// <summary>
        /// 获取当前音量设置
        /// </summary>
        public (float master, float music, float sfx) GetVolumeSettings()
        {
            float master = PlayerPrefs.GetFloat("MasterVolume", 0.8f);
            float music = PlayerPrefs.GetFloat("MusicVolume", 0.7f);
            float sfx = PlayerPrefs.GetFloat("SFXVolume", 0.8f);
            return (master, music, sfx);
        }

        /// <summary>
        /// 获取当前显示设置
        /// </summary>
        public (bool fullscreen, int resolution) GetDisplaySettings()
        {
            bool fullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
            int resolution = PlayerPrefs.GetInt("Resolution", 0);
            return (fullscreen, resolution);
        }

        /// <summary>
        /// 重置所有设置为默认值
        /// </summary>
        public void ResetToDefaults()
        {
            LogDebug("重置所有设置为默认值");

            // 重置音量设置
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.value = 0.8f;
                OnMasterVolumeChanged(0.8f);
            }

            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.value = 0.7f;
                OnMusicVolumeChanged(0.7f);
            }

            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.value = 0.8f;
                OnSFXVolumeChanged(0.8f);
            }

            // 重置显示设置
            if (fullscreenToggle != null)
            {
                fullscreenToggle.isOn = true;
                OnFullscreenToggleChanged(true);
            }

            if (resolutionDropdown != null)
            {
                resolutionDropdown.value = 0;
                OnResolutionChanged(0);
            }

            LogDebug("设置重置完成");
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
            if (masterVolumeSlider != null)
                masterVolumeSlider.onValueChanged.RemoveAllListeners();
            if (musicVolumeSlider != null)
                musicVolumeSlider.onValueChanged.RemoveAllListeners();
            if (sfxVolumeSlider != null)
                sfxVolumeSlider.onValueChanged.RemoveAllListeners();
            if (fullscreenToggle != null)
                fullscreenToggle.onValueChanged.RemoveAllListeners();
            if (resolutionDropdown != null)
                resolutionDropdown.onValueChanged.RemoveAllListeners();

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