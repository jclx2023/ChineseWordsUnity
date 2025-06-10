using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

namespace Core
{
    /// <summary>
    /// 场景切换管理器 - 防止重复切换的单例管理器
    /// </summary>
    public class SceneTransitionManager : MonoBehaviour
    {
        public static SceneTransitionManager Instance { get; private set; }

        [Header("场景切换设置")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private float transitionDelay = 2f;

        // 状态管理
        private static bool isTransitioning = false;
        private static string targetScene = "";

        // 事件
        public static System.Action<string> OnSceneTransitionStarted;
        public static System.Action<string> OnSceneTransitionCompleted;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LogDebug("场景切换管理器初始化完成");
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 安全的场景切换方法 - 防止重复切换
        /// </summary>
        public bool TransitionToScene(string sceneName, float delay = 0f, string caller = "Unknown")
        {
            // 检查是否已在切换中
            if (isTransitioning)
            {
                LogDebug($"场景切换被拒绝：已在切换中。调用者：{caller}，目标场景：{sceneName}，当前目标：{targetScene}");
                return false;
            }

            // 检查目标场景是否与当前场景相同
            string currentScene = SceneManager.GetActiveScene().name;
            if (currentScene.Equals(sceneName, System.StringComparison.OrdinalIgnoreCase))
            {
                LogDebug($"场景切换被拒绝：已在目标场景中。调用者：{caller}，场景：{sceneName}");
                return false;
            }

            LogDebug($"开始场景切换：{currentScene} → {sceneName}，调用者：{caller}，延迟：{delay}秒");

            // 设置切换状态
            isTransitioning = true;
            targetScene = sceneName;

            // 触发切换开始事件
            OnSceneTransitionStarted?.Invoke(sceneName);

            // 开始切换协程
            StartCoroutine(TransitionCoroutine(sceneName, delay));

            return true;
        }

        /// <summary>
        /// 场景切换协程
        /// </summary>
        private IEnumerator TransitionCoroutine(string sceneName, float delay)
        {
            // 等待指定延迟
            if (delay > 0f)
            {
                LogDebug($"等待 {delay} 秒后切换到 {sceneName}");
                yield return new WaitForSeconds(delay);
            }

            try
            {
                LogDebug($"执行场景切换到：{sceneName}");

                // 执行场景切换
                SceneManager.LoadScene(sceneName);

                // 触发切换完成事件
                OnSceneTransitionCompleted?.Invoke(sceneName);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"场景切换失败：{e.Message}");

                // 重置状态以允许重试
                ResetTransitionState();
            }
        }

        /// <summary>
        /// 重置切换状态（用于错误恢复）
        /// </summary>
        public static void ResetTransitionState()
        {
            isTransitioning = false;
            targetScene = "";
            Debug.Log("[SceneTransitionManager] 切换状态已重置");
        }

        /// <summary>
        /// 检查是否正在切换场景
        /// </summary>
        public static bool IsTransitioning => isTransitioning;

        /// <summary>
        /// 获取目标场景名称
        /// </summary>
        public static string GetTargetScene() => targetScene;

        /// <summary>
        /// 强制切换场景（忽略状态检查）
        /// </summary>
        public void ForceTransitionToScene(string sceneName, string caller = "Force")
        {
            LogDebug($"强制场景切换：{sceneName}，调用者：{caller}");

            // 重置状态
            ResetTransitionState();

            // 直接切换
            SceneManager.LoadScene(sceneName);
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[SceneTransitionManager] {message}");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #region 静态便捷方法

        /// <summary>
        /// 静态方法：切换到游戏场景
        /// </summary>
        public static bool SwitchToGameScene(string caller = "Unknown")
        {
            if (Instance != null)
                return Instance.TransitionToScene("NetworkGameScene", 2f, caller);

            Debug.LogError("SceneTransitionManager实例不存在");
            return false;
        }

        /// <summary>
        /// 静态方法：返回主菜单
        /// </summary>
        public static bool ReturnToMainMenu(string caller = "Unknown")
        {
            if (Instance != null)
                return Instance.TransitionToScene("MainMenuScene", 0f, caller);

            Debug.LogError("SceneTransitionManager实例不存在");
            return false;
        }

        #endregion

        #region 调试方法

        [ContextMenu("显示切换状态")]
        public void ShowTransitionStatus()
        {
            Debug.Log($"=== 场景切换状态 ===");
            Debug.Log($"正在切换：{isTransitioning}");
            Debug.Log($"目标场景：{targetScene}");
            Debug.Log($"当前场景：{SceneManager.GetActiveScene().name}");
        }

        [ContextMenu("重置切换状态")]
        public void DebugResetState()
        {
            ResetTransitionState();
        }

        #endregion
    }
}