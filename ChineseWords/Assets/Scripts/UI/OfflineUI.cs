using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Core;
using Managers;

namespace UI
{
    /// <summary>
    /// 单机UI管理器
    /// 专门处理单机游戏的UI显示和交互
    /// </summary>
    public class OfflineUI : MonoBehaviour
    {
        [Header("游戏状态显示")]
        [SerializeField] private GameObject gameStatusPanel;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text questionCountText;

        [Header("控制按钮")]
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button backToMenuButton;
        [SerializeField] private Button restartButton;

        [Header("暂停菜单")]
        [SerializeField] private GameObject pauseMenuPanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseMenuRestartButton;
        [SerializeField] private Button pauseMenuExitButton;

        [Header("显示设置")]
        [SerializeField] private bool showGameStatus = true;
        [SerializeField] private bool showTimer = true;
        [SerializeField] private bool showScore = true;

        // 组件引用
        private PlayerHealthManager healthManager;
        private TimerManager timerManager;
        private OfflineSceneController sceneController;

        // 状态追踪
        private int currentScore = 0;
        private int questionsAnswered = 0;

        private void Start()
        {
            InitializeUI();
            RegisterEvents();
            UpdateUI();
        }

        private void OnDestroy()
        {
            UnregisterEvents();
        }

        /// <summary>
        /// 初始化UI组件
        /// </summary>
        private void InitializeUI()
        {
            // 获取组件引用
            healthManager = FindObjectOfType<PlayerHealthManager>();
            timerManager = FindObjectOfType<TimerManager>();
            sceneController = OfflineSceneController.Instance;

            // 绑定按钮事件
            if (pauseButton != null)
                pauseButton.onClick.AddListener(OnPauseClicked);
            if (backToMenuButton != null)
                backToMenuButton.onClick.AddListener(OnBackToMenuClicked);
            if (restartButton != null)
                restartButton.onClick.AddListener(OnRestartClicked);

            // 暂停菜单按钮
            if (resumeButton != null)
                resumeButton.onClick.AddListener(OnResumeClicked);
            if (pauseMenuRestartButton != null)
                pauseMenuRestartButton.onClick.AddListener(OnRestartClicked);
            if (pauseMenuExitButton != null)
                pauseMenuExitButton.onClick.AddListener(OnBackToMenuClicked);

            // 设置初始状态
            if (gameStatusPanel != null)
                gameStatusPanel.SetActive(showGameStatus);
            if (pauseMenuPanel != null)
                pauseMenuPanel.SetActive(false);

            // 根据设置显示/隐藏组件
            ConfigureUIVisibility();
        }

        /// <summary>
        /// 配置UI组件的可见性
        /// </summary>
        private void ConfigureUIVisibility()
        {
            if (timerText != null)
                timerText.gameObject.SetActive(showTimer);
            if (scoreText != null)
                scoreText.gameObject.SetActive(showScore);
        }

        /// <summary>
        /// 注册事件
        /// </summary>
        private void RegisterEvents()
        {
            // 监听游戏事件（如果有）
            // 例如：健康变化、分数变化等
        }

        /// <summary>
        /// 注销事件
        /// </summary>
        private void UnregisterEvents()
        {
            // 注销事件监听
        }

        private void Update()
        {
            UpdateGameStatus();
        }

        /// <summary>
        /// 更新游戏状态显示
        /// </summary>
        private void UpdateGameStatus()
        {
            // 更新血量显示
            if (healthText != null && healthManager != null)
            {
                healthText.text = $"血量: {healthManager.CurrentHealth}";
            }

            // 更新计时器显示
            if (timerText != null && timerManager != null && showTimer)
            {
                float remainingTime = timerManager.RemainingTime;
                timerText.text = $"时间: {remainingTime:F1}s";

                // 时间不足时变红
                if (remainingTime <= 10f)
                {
                    timerText.color = Color.red;
                }
                else if (remainingTime <= 20f)
                {
                    timerText.color = Color.yellow;
                }
                else
                {
                    timerText.color = Color.white;
                }
            }

            // 更新分数显示
            if (scoreText != null && showScore)
            {
                scoreText.text = $"分数: {currentScore}";
            }

            // 更新题目计数
            if (questionCountText != null)
            {
                questionCountText.text = $"题目: {questionsAnswered}";
            }
        }

        /// <summary>
        /// 更新UI状态
        /// </summary>
        public void UpdateUI()
        {
            bool gameStarted = sceneController?.IsGameStarted ?? false;
            bool gamePaused = sceneController?.IsGamePaused ?? false;

            // 更新按钮状态
            if (pauseButton != null)
            {
                pauseButton.interactable = gameStarted;
                var buttonText = pauseButton.GetComponentInChildren<TMP_Text>();
                if (buttonText != null)
                    buttonText.text = gamePaused ? "恢复" : "暂停";
            }

            // 显示/隐藏暂停菜单
            if (pauseMenuPanel != null)
                pauseMenuPanel.SetActive(gamePaused);
        }

        #region 按钮事件处理

        /// <summary>
        /// 暂停按钮点击
        /// </summary>
        private void OnPauseClicked()
        {
            if (sceneController == null)
                return;

            if (sceneController.IsGamePaused)
            {
                sceneController.ResumeGame();
            }
            else
            {
                sceneController.PauseGame();
            }

            UpdateUI();
        }

        /// <summary>
        /// 恢复游戏按钮点击
        /// </summary>
        private void OnResumeClicked()
        {
            if (sceneController != null)
            {
                sceneController.ResumeGame();
                UpdateUI();
            }
        }

        /// <summary>
        /// 重新开始按钮点击
        /// </summary>
        private void OnRestartClicked()
        {
            if (sceneController != null)
            {
                sceneController.RestartGame();
            }
        }

        /// <summary>
        /// 返回主菜单按钮点击
        /// </summary>
        private void OnBackToMenuClicked()
        {
            if (sceneController != null)
            {
                sceneController.BackToMainMenu();
            }
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 更新分数
        /// </summary>
        public void UpdateScore(int newScore)
        {
            currentScore = newScore;
        }

        /// <summary>
        /// 增加分数
        /// </summary>
        public void AddScore(int scoreToAdd)
        {
            currentScore += scoreToAdd;
        }

        /// <summary>
        /// 增加题目计数
        /// </summary>
        public void IncrementQuestionCount()
        {
            questionsAnswered++;
        }

        /// <summary>
        /// 重置统计数据
        /// </summary>
        public void ResetStats()
        {
            currentScore = 0;
            questionsAnswered = 0;
        }

        /// <summary>
        /// 显示游戏状态面板
        /// </summary>
        public void ShowGameStatus()
        {
            showGameStatus = true;
            if (gameStatusPanel != null)
                gameStatusPanel.SetActive(true);
        }

        /// <summary>
        /// 隐藏游戏状态面板
        /// </summary>
        public void HideGameStatus()
        {
            showGameStatus = false;
            if (gameStatusPanel != null)
                gameStatusPanel.SetActive(false);
        }

        /// <summary>
        /// 切换游戏状态面板显示
        /// </summary>
        public void ToggleGameStatus()
        {
            showGameStatus = !showGameStatus;
            if (gameStatusPanel != null)
                gameStatusPanel.SetActive(showGameStatus);
        }

        /// <summary>
        /// 显示游戏结束信息
        /// </summary>
        public void ShowGameOverInfo(string message)
        {
            // 这里可以显示游戏结束对话框或信息
            Debug.Log($"游戏结束: {message}");
        }

        #endregion
    }
}