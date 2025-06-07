using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lobby.Core;

namespace Lobby.UI
{
    /// <summary>
    /// Lobby状态显示UI
    /// 负责显示网络连接状态和相关信息
    /// </summary>
    public class LobbyStatusUI : MonoBehaviour
    {
        [Header("UI组件引用")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Image statusIcon;

        [Header("状态颜色配置")]
        [SerializeField] private Color connectedColor = Color.green;
        [SerializeField] private Color connectingColor = Color.yellow;
        [SerializeField] private Color disconnectedColor = Color.red;

        [Header("状态图标配置")]
        [SerializeField] private Sprite connectedIcon;
        [SerializeField] private Sprite connectingIcon;
        [SerializeField] private Sprite disconnectedIcon;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 状态枚举
        public enum ConnectionStatus
        {
            Disconnected,
            Connecting,
            Connected
        }

        // 当前状态
        private ConnectionStatus currentStatus = ConnectionStatus.Disconnected;
        private bool isInitialized = false;

        #region Unity生命周期

        private void Start()
        {
            InitializeUI();
            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化UI
        /// </summary>
        private void InitializeUI()
        {
            LogDebug("初始化状态UI");

            // 自动查找组件
            FindUIComponents();

            // 设置初始状态
            UpdateStatusDisplay(ConnectionStatus.Disconnected);

            isInitialized = true;
            LogDebug("状态UI初始化完成");
        }

        /// <summary>
        /// 查找UI组件
        /// </summary>
        private void FindUIComponents()
        {
            // 查找状态文本
            if (statusText == null)
            {
                statusText = GetComponentInChildren<TMP_Text>();
                if (statusText == null)
                {
                    statusText = GameObject.Find("StatusText")?.GetComponent<TMP_Text>();
                }

                if (statusText == null)
                {
                    Debug.LogWarning("[LobbyStatusUI] 未找到StatusText");
                }
            }

            // 查找状态图标
            if (statusIcon == null)
            {
                statusIcon = GetComponentInChildren<Image>();
                if (statusIcon == null)
                {
                    statusIcon = GameObject.Find("StatusIcon")?.GetComponent<Image>();
                }

                if (statusIcon == null)
                {
                    Debug.LogWarning("[LobbyStatusUI] 未找到StatusIcon");
                }
            }
        }

        /// <summary>
        /// 订阅网络事件
        /// </summary>
        private void SubscribeToEvents()
        {
            if (LobbyNetworkManager.Instance != null)
            {
                LobbyNetworkManager.Instance.OnConnectionStatusChanged += OnConnectionStatusChanged;
                LogDebug("已订阅网络状态事件");

                // 获取当前连接状态
                bool isConnected = LobbyNetworkManager.Instance.IsConnected();
                bool isConnecting = LobbyNetworkManager.Instance.IsConnecting();

                if (isConnected)
                {
                    UpdateStatusDisplay(ConnectionStatus.Connected);
                }
                else if (isConnecting)
                {
                    UpdateStatusDisplay(ConnectionStatus.Connecting);
                }
                else
                {
                    UpdateStatusDisplay(ConnectionStatus.Disconnected);
                }
            }
        }

        /// <summary>
        /// 取消订阅网络事件
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (LobbyNetworkManager.Instance != null)
            {
                LobbyNetworkManager.Instance.OnConnectionStatusChanged -= OnConnectionStatusChanged;
                LogDebug("已取消订阅网络状态事件");
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 连接状态改变事件
        /// </summary>
        private void OnConnectionStatusChanged(bool isConnected)
        {
            LogDebug($"连接状态改变: {isConnected}");

            ConnectionStatus newStatus = isConnected ? ConnectionStatus.Connected : ConnectionStatus.Disconnected;
            UpdateStatusDisplay(newStatus);
        }

        #endregion

        #region 状态显示

        /// <summary>
        /// 更新状态显示
        /// </summary>
        private void UpdateStatusDisplay(ConnectionStatus status)
        {
            if (!isInitialized)
                return;

            currentStatus = status;

            // 更新状态文本
            UpdateStatusText();

            // 更新状态图标和颜色
            UpdateStatusIcon();

            LogDebug($"状态显示已更新: {status}");
        }

        /// <summary>
        /// 更新状态文本
        /// </summary>
        private void UpdateStatusText()
        {
            if (statusText == null)
                return;

            string statusMessage = GetStatusMessage();
            statusText.text = statusMessage;

            // 设置文本颜色
            Color textColor = GetStatusColor();
            statusText.color = textColor;
        }

        /// <summary>
        /// 更新状态图标
        /// </summary>
        private void UpdateStatusIcon()
        {
            if (statusIcon == null)
                return;

            // 设置图标精灵
            Sprite iconSprite = GetStatusIcon();
            if (iconSprite != null)
            {
                statusIcon.sprite = iconSprite;
            }

            // 设置图标颜色
            Color iconColor = GetStatusColor();
            statusIcon.color = iconColor;
        }

        /// <summary>
        /// 获取状态消息文本
        /// </summary>
        private string GetStatusMessage()
        {
            switch (currentStatus)
            {
                case ConnectionStatus.Connected:
                    return "已连接";
                case ConnectionStatus.Connecting:
                    return "连接中...";
                case ConnectionStatus.Disconnected:
                    return "未连接";
                default:
                    return "状态未知";
            }
        }

        /// <summary>
        /// 获取状态颜色
        /// </summary>
        private Color GetStatusColor()
        {
            switch (currentStatus)
            {
                case ConnectionStatus.Connected:
                    return connectedColor;
                case ConnectionStatus.Connecting:
                    return connectingColor;
                case ConnectionStatus.Disconnected:
                    return disconnectedColor;
                default:
                    return Color.gray;
            }
        }

        /// <summary>
        /// 获取状态图标
        /// </summary>
        private Sprite GetStatusIcon()
        {
            switch (currentStatus)
            {
                case ConnectionStatus.Connected:
                    return connectedIcon;
                case ConnectionStatus.Connecting:
                    return connectingIcon;
                case ConnectionStatus.Disconnected:
                    return disconnectedIcon;
                default:
                    return null;
            }
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 手动设置连接状态
        /// </summary>
        public void SetConnectionStatus(bool isConnected)
        {
            ConnectionStatus status = isConnected ? ConnectionStatus.Connected : ConnectionStatus.Disconnected;
            UpdateStatusDisplay(status);
        }

        /// <summary>
        /// 设置连接中状态
        /// </summary>
        public void SetConnectingStatus()
        {
            UpdateStatusDisplay(ConnectionStatus.Connecting);
        }

        /// <summary>
        /// 获取当前状态
        /// </summary>
        public ConnectionStatus GetCurrentStatus()
        {
            return currentStatus;
        }

        /// <summary>
        /// 显示自定义消息
        /// </summary>
        public void ShowCustomMessage(string message, Color color)
        {
            if (statusText != null)
            {
                statusText.text = message;
                statusText.color = color;
            }
        }

        /// <summary>
        /// 恢复状态显示
        /// </summary>
        public void RestoreStatusDisplay()
        {
            UpdateStatusDisplay(currentStatus);
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
                Debug.Log($"[LobbyStatusUI] {message}");
            }
        }

        [ContextMenu("显示状态UI信息")]
        public void ShowStatusUIInfo()
        {
            string info = "=== 状态UI信息 ===\n";
            info += $"UI已初始化: {isInitialized}\n";
            info += $"当前状态: {currentStatus}\n";
            info += $"状态文本组件: {(statusText != null ? "✓" : "✗")}\n";
            info += $"状态图标组件: {(statusIcon != null ? "✓" : "✗")}\n";
            info += $"当前显示文本: {(statusText != null ? statusText.text : "N/A")}\n";
            info += $"当前颜色: {GetStatusColor()}";

            LogDebug(info);
        }

        [ContextMenu("测试连接状态")]
        public void TestConnectionStatus()
        {
            // 循环测试所有状态
            StartCoroutine(TestStatusCoroutine());
        }

        private System.Collections.IEnumerator TestStatusCoroutine()
        {
            LogDebug("开始测试状态显示");

            UpdateStatusDisplay(ConnectionStatus.Disconnected);
            yield return new WaitForSeconds(1f);

            UpdateStatusDisplay(ConnectionStatus.Connecting);
            yield return new WaitForSeconds(1f);

            UpdateStatusDisplay(ConnectionStatus.Connected);
            yield return new WaitForSeconds(1f);

            // 恢复实际状态
            if (LobbyNetworkManager.Instance != null)
            {
                bool isConnected = LobbyNetworkManager.Instance.IsConnected();
                UpdateStatusDisplay(isConnected ? ConnectionStatus.Connected : ConnectionStatus.Disconnected);
            }

            LogDebug("状态测试完成");
        }

        #endregion
    }
}