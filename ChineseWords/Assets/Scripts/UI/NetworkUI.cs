using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Core.Network;
using System.Collections.Generic;

namespace UI
{
    /// <summary>
    /// 网络UI管理器
    /// 专注于网络状态显示和房间管理，不处理游戏玩法UI
    /// </summary>
    public class NetworkUI : MonoBehaviour
    {
        [Header("网络状态面板")]
        [SerializeField] private GameObject networkStatusPanel;
        [SerializeField] private TMP_Text connectionStatusText;
        [SerializeField] private TMP_Text roomInfoText;
        [SerializeField] private TMP_Text playerListText;
        [SerializeField] private Button disconnectButton;
        [SerializeField] private Button backToMenuButton;

        [Header("调试信息（可选）")]
        [SerializeField] private bool showDebugInfo = false;
        [SerializeField] private TMP_Text debugInfoText;

        private Dictionary<ushort, string> connectedPlayers;

        private void Start()
        {
            InitializeUI();
            RegisterNetworkEvents();
            UpdateUIBasedOnGameMode();
        }

        private void OnDestroy()
        {
            UnregisterNetworkEvents();
        }

        /// <summary>
        /// 初始化UI组件
        /// </summary>
        private void InitializeUI()
        {
            connectedPlayers = new Dictionary<ushort, string>();

            // 绑定按钮事件
            if (disconnectButton != null)
                disconnectButton.onClick.AddListener(OnDisconnectClicked);
            if (backToMenuButton != null)
                backToMenuButton.onClick.AddListener(OnBackToMenuClicked);

            // 设置调试信息显示
            if (debugInfoText != null)
                debugInfoText.gameObject.SetActive(showDebugInfo);
        }

        /// <summary>
        /// 注册网络事件
        /// </summary>
        private void RegisterNetworkEvents()
        {
            NetworkManager.OnConnected += OnNetworkConnected;
            NetworkManager.OnDisconnected += OnNetworkDisconnected;
            NetworkManager.OnHostStarted += OnHostStarted;
            NetworkManager.OnHostStopped += OnHostStopped;
            NetworkManager.OnPlayerJoined += OnPlayerJoined;
            NetworkManager.OnPlayerLeft += OnPlayerLeft;
        }

        /// <summary>
        /// 注销网络事件
        /// </summary>
        private void UnregisterNetworkEvents()
        {
            NetworkManager.OnConnected -= OnNetworkConnected;
            NetworkManager.OnDisconnected -= OnNetworkDisconnected;
            NetworkManager.OnHostStarted -= OnHostStarted;
            NetworkManager.OnHostStopped -= OnHostStopped;
            NetworkManager.OnPlayerJoined -= OnPlayerJoined;
            NetworkManager.OnPlayerLeft -= OnPlayerLeft;
        }

        /// <summary>
        /// 根据游戏模式更新UI
        /// </summary>
        private void UpdateUIBasedOnGameMode()
        {
            var gameMode = MainMenuManager.SelectedGameMode;

            switch (gameMode)
            {
                case MainMenuManager.GameMode.SinglePlayer:
                    // 单机模式：隐藏网络UI
                    SetNetworkPanelActive(false);
                    break;

                case MainMenuManager.GameMode.Host:
                case MainMenuManager.GameMode.Client:
                    // 网络模式：显示网络UI
                    SetNetworkPanelActive(true);
                    break;

                default:
                    Debug.LogWarning($"未知游戏模式: {gameMode}");
                    break;
            }
        }

        /// <summary>
        /// 设置网络面板激活状态
        /// </summary>
        private void SetNetworkPanelActive(bool active)
        {
            if (networkStatusPanel != null)
                networkStatusPanel.SetActive(active);
        }

        #region 网络事件处理

        private void OnNetworkConnected()
        {
            UpdateConnectionStatus("已连接到服务器");

            if (NetworkManager.Instance.IsHost)
            {
                UpdateRoomInfo($"房间: {NetworkManager.Instance.RoomName}");
                AddPlayer(NetworkManager.Instance.ClientId, "房主");
            }
            else
            {
                UpdateRoomInfo("已连接到房间");
                AddPlayer(NetworkManager.Instance.ClientId, MainMenuManager.PlayerName);
            }

            UpdateUI();
        }

        private void OnNetworkDisconnected()
        {
            UpdateConnectionStatus("连接已断开");
            UpdateRoomInfo("");
            ClearPlayerList();
            UpdateUI();
        }

        private void OnHostStarted()
        {
            UpdateConnectionStatus("主机已启动");
            UpdateRoomInfo($"房间: {NetworkManager.Instance.RoomName} | 端口: {NetworkManager.Instance.Port}");

            if (disconnectButton != null)
            {
                var buttonText = disconnectButton.GetComponentInChildren<TMP_Text>();
                if (buttonText != null)
                    buttonText.text = "停止主机";
            }
        }

        private void OnHostStopped()
        {
            UpdateConnectionStatus("主机已停止");
            UpdateRoomInfo("");
            ClearPlayerList();

            if (disconnectButton != null)
            {
                var buttonText = disconnectButton.GetComponentInChildren<TMP_Text>();
                if (buttonText != null)
                    buttonText.text = "断开连接";
            }
        }

        private void OnPlayerJoined(ushort playerId)
        {
            AddPlayer(playerId, $"玩家{playerId}");
            Debug.Log($"玩家加入: {playerId}");
        }

        private void OnPlayerLeft(ushort playerId)
        {
            RemovePlayer(playerId);
            Debug.Log($"玩家离开: {playerId}");
        }

        #endregion

        #region 玩家列表管理

        /// <summary>
        /// 添加玩家到列表
        /// </summary>
        private void AddPlayer(ushort playerId, string playerName)
        {
            connectedPlayers[playerId] = playerName;
            UpdatePlayerList();
        }

        /// <summary>
        /// 从列表移除玩家
        /// </summary>
        private void RemovePlayer(ushort playerId)
        {
            connectedPlayers.Remove(playerId);
            UpdatePlayerList();
        }

        /// <summary>
        /// 清空玩家列表
        /// </summary>
        private void ClearPlayerList()
        {
            connectedPlayers.Clear();
            UpdatePlayerList();
        }

        /// <summary>
        /// 更新玩家列表显示
        /// </summary>
        private void UpdatePlayerList()
        {
            if (playerListText == null)
                return;

            if (connectedPlayers.Count == 0)
            {
                playerListText.text = "玩家列表：无";
                return;
            }

            var playerListStr = "玩家列表：\n";
            foreach (var player in connectedPlayers)
            {
                playerListStr += $"• {player.Value} (ID: {player.Key})\n";
            }

            playerListText.text = playerListStr.TrimEnd('\n');
        }

        #endregion

        #region UI更新方法

        /// <summary>
        /// 更新连接状态文本
        /// </summary>
        private void UpdateConnectionStatus(string status)
        {
            if (connectionStatusText != null)
                connectionStatusText.text = $"状态: {status}";
        }

        /// <summary>
        /// 更新房间信息文本
        /// </summary>
        private void UpdateRoomInfo(string info)
        {
            if (roomInfoText != null)
                roomInfoText.text = info;
        }

        /// <summary>
        /// 更新UI状态
        /// </summary>
        private void UpdateUI()
        {
            bool isConnected = NetworkManager.Instance?.IsConnected ?? false;
            bool isHost = NetworkManager.Instance?.IsHost ?? false;

            // 更新按钮状态
            if (disconnectButton != null)
                disconnectButton.interactable = isConnected || isHost;

            // 更新调试信息
            if (showDebugInfo && debugInfoText != null)
            {
                UpdateDebugInfo();
            }
        }

        /// <summary>
        /// 更新调试信息
        /// </summary>
        private void UpdateDebugInfo()
        {
            if (debugInfoText == null)
                return;

            var info = "=== 网络调试信息 ===\n";
            info += $"游戏模式: {MainMenuManager.SelectedGameMode}\n";

            if (NetworkManager.Instance != null)
            {
                info += $"是否为主机: {NetworkManager.Instance.IsHost}\n";
                info += $"是否连接: {NetworkManager.Instance.IsConnected}\n";
                info += $"客户端ID: {NetworkManager.Instance.ClientId}\n";
                info += $"端口: {NetworkManager.Instance.Port}\n";

                if (NetworkManager.Instance.IsHost)
                {
                    info += $"房间名: {NetworkManager.Instance.RoomName}\n";
                    info += $"最大玩家: {NetworkManager.Instance.MaxPlayers}\n";
                    info += $"当前玩家数: {NetworkManager.Instance.ConnectedPlayerCount}\n";
                }
            }

            debugInfoText.text = info;
        }

        #endregion

        #region 按钮事件处理

        /// <summary>
        /// 断开连接按钮点击
        /// </summary>
        private void OnDisconnectClicked()
        {
            if (NetworkManager.Instance != null)
            {
                if (NetworkManager.Instance.IsHost)
                {
                    Debug.Log("停止主机");
                    NetworkManager.Instance.StopHost();
                }
                else
                {
                    Debug.Log("断开连接");
                    NetworkManager.Instance.Disconnect();
                }
            }
        }

        /// <summary>
        /// 返回主菜单按钮点击
        /// </summary>
        private void OnBackToMenuClicked()
        {
            // 先断开网络连接
            OnDisconnectClicked();

            // 返回主菜单场景
            SceneManager.LoadScene("MainMenuScene");
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 显示网络状态面板
        /// </summary>
        public void ShowNetworkPanel()
        {
            SetNetworkPanelActive(true);
        }

        /// <summary>
        /// 隐藏网络状态面板
        /// </summary>
        public void HideNetworkPanel()
        {
            SetNetworkPanelActive(false);
        }

        /// <summary>
        /// 切换网络面板显示状态
        /// </summary>
        public void ToggleNetworkPanel()
        {
            if (networkStatusPanel != null)
                SetNetworkPanelActive(!networkStatusPanel.activeSelf);
        }

        #endregion
    }
}