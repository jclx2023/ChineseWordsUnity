using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core
{
    /// <summary>
    /// 单机场景控制器
    /// 专门管理单机游戏的初始化和控制
    /// </summary>
    public class OfflineSceneController : MonoBehaviour
    {
        [Header("游戏控制器")]
        [SerializeField] private QuestionManagerController questionController;
        [SerializeField] private TimerManager timerManager;
        [SerializeField] private PlayerHealthManager healthManager;

        [Header("UI组件")]
        [SerializeField] private GameObject gameCanvas;
        [SerializeField] private GameObject offlineUICanvas;

        [Header("游戏设置")]
        [SerializeField] private bool autoStartGame = true;
        [SerializeField] private float gameStartDelay = 1f;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        public static OfflineSceneController Instance { get; private set; }

        // 游戏状态
        private bool gameStarted = false;
        private bool gamePaused = false;

        // 属性
        public bool IsGameStarted => gameStarted;
        public bool IsGamePaused => gamePaused;
        public QuestionManagerController QuestionController => questionController;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            InitializeOfflineGame();
        }

        /// <summary>
        /// 初始化单机游戏
        /// </summary>
        private void InitializeOfflineGame()
        {
            LogDebug("初始化单机游戏");

            // 验证组件引用
            if (!ValidateComponents())
            {
                Debug.LogError("组件验证失败，无法启动游戏");
                return;
            }

            // 配置UI
            ConfigureUI();

            // 自动开始游戏
            if (autoStartGame)
            {
                Invoke(nameof(StartGame), gameStartDelay);
            }

            LogDebug("单机游戏初始化完成");
        }

        /// <summary>
        /// 验证必要组件
        /// </summary>
        private bool ValidateComponents()
        {
            bool isValid = true;

            if (questionController == null)
            {
                Debug.LogError("QuestionManagerController 未设置");
                isValid = false;
            }

            if (timerManager == null)
            {
                Debug.LogError("TimerManager 未设置");
                isValid = false;
            }

            if (healthManager == null)
            {
                Debug.LogError("PlayerHealthManager 未设置");
                isValid = false;
            }

            return isValid;
        }

        /// <summary>
        /// 配置UI组件
        /// </summary>
        private void ConfigureUI()
        {
            // 确保游戏UI可见
            if (gameCanvas != null)
                gameCanvas.SetActive(true);

            if (offlineUICanvas != null)
                offlineUICanvas.SetActive(true);

            LogDebug("UI配置完成");
        }

        /// <summary>
        /// 开始游戏
        /// </summary>
        public void StartGame()
        {
            if (gameStarted)
            {
                LogDebug("游戏已经开始");
                return;
            }

            LogDebug("开始单机游戏");
            gameStarted = true;
            gamePaused = false;

            // 这里可以添加游戏开始的特效或音效

            // QuestionManagerController 会自动开始加载第一题
        }

        /// <summary>
        /// 暂停游戏
        /// </summary>
        public void PauseGame()
        {
            if (!gameStarted || gamePaused)
                return;

            LogDebug("暂停游戏");
            gamePaused = true;

            // 暂停计时器
            if (timerManager != null)
                timerManager.PauseTimer();

            // 设置时间缩放
            Time.timeScale = 0f;
        }

        /// <summary>
        /// 恢复游戏
        /// </summary>
        public void ResumeGame()
        {
            if (!gameStarted || !gamePaused)
                return;

            LogDebug("恢复游戏");
            gamePaused = false;

            // 恢复计时器
            if (timerManager != null)
                timerManager.ResumeTimer();

            // 恢复时间缩放
            Time.timeScale = 1f;
        }

        /// <summary>
        /// 重新开始游戏
        /// </summary>
        public void RestartGame()
        {
            LogDebug("重新开始游戏");

            // 恢复时间缩放
            Time.timeScale = 1f;

            // 重新加载当前场景
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>
        /// 结束游戏
        /// </summary>
        public void EndGame()
        {
            if (!gameStarted)
                return;

            LogDebug("结束游戏");
            gameStarted = false;
            gamePaused = false;

            // 恢复时间缩放
            Time.timeScale = 1f;

            // 停止计时器
            if (timerManager != null)
                timerManager.StopTimer();

            // 这里可以显示游戏结束界面或统计信息
        }

        /// <summary>
        /// 返回主菜单
        /// </summary>
        public void BackToMainMenu()
        {
            LogDebug("返回主菜单");

            // 恢复时间缩放
            Time.timeScale = 1f;

            // 加载主菜单场景
            SceneManager.LoadScene("MainMenuScene");
        }

        /// <summary>
        /// 获取游戏统计信息
        /// </summary>
        public string GetGameStats()
        {
            var stats = "=== 游戏统计 ===\n";
            stats += $"游戏状态: {(gameStarted ? (gamePaused ? "已暂停" : "进行中") : "未开始")}\n";

            if (healthManager != null)
            {
                // 假设HealthManager有获取当前血量的方法
                stats += $"当前血量: {healthManager.CurrentHealth}\n";
            }

            if (timerManager != null)
            {
                // 假设TimerManager有获取剩余时间的方法
                stats += $"剩余时间: {timerManager.RemainingTime:F1}秒\n";
            }

            return stats;
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[OfflineSceneController] {message}");
            }
        }

        /// <summary>
        /// 应用程序焦点变化时的处理
        /// </summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && gameStarted && !gamePaused)
            {
                // 失去焦点时自动暂停游戏
                PauseGame();
                LogDebug("应用失去焦点，自动暂停游戏");
            }
        }

        /// <summary>
        /// 应用程序暂停时的处理
        /// </summary>
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && gameStarted && !gamePaused)
            {
                // 应用暂停时自动暂停游戏
                PauseGame();
                LogDebug("应用暂停，自动暂停游戏");
            }
        }

        private void OnDestroy()
        {
            // 确保恢复时间缩放
            Time.timeScale = 1f;
        }
    }
}