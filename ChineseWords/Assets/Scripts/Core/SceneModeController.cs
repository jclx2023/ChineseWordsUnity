using UnityEngine;
using Core.Network;
using UI;

namespace Core
{
    /// <summary>
    /// 场景模式控制器
    /// 根据游戏模式自动激活/禁用相应组件
    /// </summary>
    public class SceneModeController : MonoBehaviour
    {
        [Header("组件引用")]
        [SerializeField] private GameObject networkManager;
        [SerializeField] private GameObject hostGameManager;
        [SerializeField] private GameObject networkCanvas;
        [SerializeField] private GameObject gameCanvas;
        [SerializeField] private NetworkUI networkUI;

        [Header("调试设置")]
        [SerializeField] private bool showDebugLogs = true;

        private void Start()
        {
            ConfigureSceneBasedOnGameMode();
        }

        /// <summary>
        /// 根据游戏模式配置场景
        /// </summary>
        private void ConfigureSceneBasedOnGameMode()
        {
            var gameMode = MainMenuManager.SelectedGameMode;
            LogDebug($"配置场景模式: {gameMode}");

            switch (gameMode)
            {
                case MainMenuManager.GameMode.SinglePlayer:
                    ConfigureSinglePlayerMode();
                    break;

                case MainMenuManager.GameMode.Host:
                    ConfigureHostMode();
                    break;

                case MainMenuManager.GameMode.Client:
                    ConfigureClientMode();
                    break;

                default:
                    Debug.LogWarning($"未知游戏模式: {gameMode}，使用单机模式配置");
                    ConfigureSinglePlayerMode();
                    break;
            }
        }

        /// <summary>
        /// 配置单机模式
        /// </summary>
        private void ConfigureSinglePlayerMode()
        {
            LogDebug("配置单机模式");

            // 网络组件
            SetGameObjectActive(networkManager, false);
            SetGameObjectActive(hostGameManager, false);

            // UI组件
            SetGameObjectActive(networkCanvas, false);
            SetGameObjectActive(gameCanvas, true);

            // 隐藏网络UI
            if (networkUI != null)
                networkUI.HideNetworkPanel();
        }

        /// <summary>
        /// 配置主机模式
        /// </summary>
        private void ConfigureHostMode()
        {
            LogDebug("配置主机模式");

            // 网络组件
            SetGameObjectActive(networkManager, true);
            SetGameObjectActive(hostGameManager, true);

            // UI组件
            SetGameObjectActive(networkCanvas, true);
            SetGameObjectActive(gameCanvas, true);

            // 显示网络UI
            if (networkUI != null)
                networkUI.ShowNetworkPanel();
        }

        /// <summary>
        /// 配置客户端模式
        /// </summary>
        private void ConfigureClientMode()
        {
            LogDebug("配置客户端模式");

            // 网络组件
            SetGameObjectActive(networkManager, true);
            SetGameObjectActive(hostGameManager, false);

            // UI组件
            SetGameObjectActive(networkCanvas, true);
            SetGameObjectActive(gameCanvas, true);

            // 显示网络UI
            if (networkUI != null)
                networkUI.ShowNetworkPanel();
        }

        /// <summary>
        /// 安全设置GameObject激活状态
        /// </summary>
        private void SetGameObjectActive(GameObject obj, bool active)
        {
            if (obj != null)
            {
                obj.SetActive(active);
                LogDebug($"设置 {obj.name} 激活状态: {active}");
            }
            else
            {
                Debug.LogWarning($"GameObject引用为空，无法设置激活状态");
            }
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[SceneModeController] {message}");
            }
        }

        /// <summary>
        /// 动态切换模式（用于运行时测试）
        /// </summary>
        [ContextMenu("重新配置场景")]
        public void ReconfigureScene()
        {
            ConfigureSceneBasedOnGameMode();
        }
    }
}