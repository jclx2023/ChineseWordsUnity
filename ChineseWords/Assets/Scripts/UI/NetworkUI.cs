using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Core.Network;

namespace UI
{
    /// <summary>
    /// 网络连接UI管理器
    /// 只负责网络连接，游戏逻辑由NetworkQuestionManagerController处理
    /// </summary>
    public class NetworkUI : MonoBehaviour
    {
        [Header("主面板")]
        [SerializeField] private GameObject mainPanel;

        [Header("游戏模式选择面板")]
        [SerializeField] private GameObject gameModePanel;
        [SerializeField] private Button singlePlayerButton;
        [SerializeField] private Button multiPlayerButton;

        [Header("连接界面")]
        [SerializeField] private GameObject connectPanel;
        [SerializeField] private TMP_InputField serverIPInput;
        [SerializeField] private TMP_InputField portInput;
        [SerializeField] private Button connectButton;
        [SerializeField] private Button backToModeButton; // 返回模式选择按钮
        [SerializeField] private TMP_Text statusText;

        [Header("等待界面")]
        [SerializeField] private GameObject waitingPanel;
        [SerializeField] private TMP_Text waitingText;
        [SerializeField] private Button disconnectButton; // 断开连接按钮放在等待界面
        [SerializeField] private TMP_Text clientInfoText;

        private NetworkQuestionManagerController networkQMC;

        private void Start()
        {
            networkQMC = FindObjectOfType<NetworkQuestionManagerController>();
            if (networkQMC == null)
            {
                Debug.LogError("未找到 NetworkQuestionManagerController");
            }

            InitializeUI();
            RegisterNetworkEvents();
        }

        private void OnDestroy()
        {
            UnregisterNetworkEvents();
        }

        private void InitializeUI()
        {
            // 设置默认值
            if (serverIPInput != null)
                serverIPInput.text = "127.0.0.1";
            if (portInput != null)
                portInput.text = "7777";

            // 绑定按钮事件
            if (connectButton != null)
                connectButton.onClick.AddListener(OnConnectButtonClicked);
            if (backToModeButton != null)
                backToModeButton.onClick.AddListener(OnBackToModeClicked);
            if (disconnectButton != null)
                disconnectButton.onClick.AddListener(OnDisconnectButtonClicked);
            if (singlePlayerButton != null)
                singlePlayerButton.onClick.AddListener(OnSinglePlayerClicked);
            if (multiPlayerButton != null)
                multiPlayerButton.onClick.AddListener(OnMultiPlayerClicked);

            // 初始状态：显示游戏模式选择面板（允许选择单机或多人）
            ShowPanel(PanelType.GameMode);
            UpdateUI();
        }

        private void RegisterNetworkEvents()
        {
            NetworkManager.OnConnected += OnNetworkConnected;
            NetworkManager.OnDisconnected += OnNetworkDisconnected;
            NetworkManager.OnPlayerTurnChanged += OnPlayerTurnChanged;
        }

        private void UnregisterNetworkEvents()
        {
            NetworkManager.OnConnected -= OnNetworkConnected;
            NetworkManager.OnDisconnected -= OnNetworkDisconnected;
            NetworkManager.OnPlayerTurnChanged -= OnPlayerTurnChanged;
        }

        #region UI事件处理

        private void OnConnectButtonClicked()
        {
            string ip = serverIPInput?.text ?? "127.0.0.1";
            if (ushort.TryParse(portInput?.text ?? "7777", out ushort port))
            {
                NetworkManager.Instance?.Connect(ip, port);
                UpdateStatusText("正在连接服务器...");
                connectButton.interactable = false;
            }
            else
            {
                UpdateStatusText("端口号格式错误");
            }
        }

        private void OnDisconnectButtonClicked()
        {
            NetworkManager.Instance?.Disconnect();
        }

        private void OnSinglePlayerClicked()
        {
            Debug.Log("开始单机游戏");
            // 隐藏网络UI，启动单机模式
            HideNetworkUI();
            networkQMC?.StartGame(false); // 单机模式
        }

        private void OnMultiPlayerClicked()
        {
            Debug.Log("选择多人游戏，需要先连接服务器");
            // 显示连接面板让用户连接服务器
            ShowPanel(PanelType.Connect);
        }

        private void OnBackToModeClicked()
        {
            Debug.Log("返回模式选择");
            // 从连接面板返回模式选择
            ShowPanel(PanelType.GameMode);
        }

        #endregion

        #region 网络事件处理

        private void OnNetworkConnected()
        {
            UpdateStatusText("连接成功");
            UpdateClientInfoText($"客户端ID: {NetworkManager.Instance.ClientId}");

            // 连接成功后显示多人游戏等待界面
            ShowPanel(PanelType.Waiting);
            UpdateWaitingText("连接成功！等待服务器分配游戏...");

            // 自动开始多人游戏
            networkQMC?.StartGame(true); // 多人模式

            UpdateUI();
        }

        private void OnNetworkDisconnected()
        {
            UpdateStatusText("连接断开");
            ShowPanel(PanelType.GameMode); // 回到模式选择
            UpdateUI();

            // 如果正在多人游戏中断开，提示用户
            if (networkQMC?.IsMultiplayerMode == true)
            {
                Debug.Log("网络断开，回到模式选择界面");
                ShowNetworkUI(); // 确保UI可见
            }
        }

        private void OnPlayerTurnChanged(ushort playerId)
        {
            if (networkQMC?.IsMultiplayerMode == true)
            {
                bool isMyTurn = playerId == NetworkManager.Instance?.ClientId;
                if (isMyTurn)
                {
                    UpdateWaitingText("轮到你答题了！");
                    // 隐藏等待界面，显示游戏内容
                    HideNetworkUI();
                }
                else
                {
                    UpdateWaitingText($"轮到玩家 {playerId} 答题，请等待...");
                    ShowPanel(PanelType.Waiting);
                }
            }
        }

        #endregion

        #region UI状态管理

        private enum PanelType
        {
            Connect,
            GameMode,
            Waiting
        }

        private void ShowPanel(PanelType panelType)
        {
            // 隐藏所有面板
            if (connectPanel != null) connectPanel.SetActive(false);
            if (gameModePanel != null) gameModePanel.SetActive(false);
            if (waitingPanel != null) waitingPanel.SetActive(false);

            // 显示指定面板
            switch (panelType)
            {
                case PanelType.Connect:
                    if (connectPanel != null) connectPanel.SetActive(true);
                    break;
                case PanelType.GameMode:
                    if (gameModePanel != null) gameModePanel.SetActive(true);
                    break;
                case PanelType.Waiting:
                    if (waitingPanel != null) waitingPanel.SetActive(true);
                    break;
            }

            // 确保主面板可见
            if (mainPanel != null) mainPanel.SetActive(true);
        }

        private void HideNetworkUI()
        {
            // 隐藏整个网络UI，让游戏内容显示
            if (mainPanel != null) mainPanel.SetActive(false);
        }

        private void ShowNetworkUI()
        {
            // 重新显示网络UI
            if (mainPanel != null) mainPanel.SetActive(true);
        }

        private void UpdateUI()
        {
            bool isConnected = NetworkManager.Instance?.IsConnected ?? false;

            if (connectButton != null)
                connectButton.interactable = !isConnected;
            if (disconnectButton != null)
                disconnectButton.interactable = isConnected;

            // 单机模式始终可用，多人模式在GameMode面板中始终可用
            if (singlePlayerButton != null)
                singlePlayerButton.interactable = true;
            if (multiPlayerButton != null)
                multiPlayerButton.interactable = true;
        }

        #endregion

        #region UI文本更新

        private void UpdateStatusText(string text)
        {
            if (statusText != null)
                statusText.text = text;
        }

        private void UpdateClientInfoText(string text)
        {
            if (clientInfoText != null)
                clientInfoText.text = text;
        }

        private void UpdateWaitingText(string text)
        {
            if (waitingText != null)
                waitingText.text = text;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 显示网络错误
        /// </summary>
        public void ShowNetworkError(string error)
        {
            UpdateStatusText($"错误: {error}");
            ShowNetworkUI();
            ShowPanel(PanelType.Connect);
        }

        /// <summary>
        /// 游戏结束后重新显示模式选择
        /// </summary>
        public void OnGameEnded()
        {
            ShowNetworkUI();
            if (NetworkManager.Instance?.IsConnected == true)
            {
                ShowPanel(PanelType.GameMode);
            }
            else
            {
                ShowPanel(PanelType.Connect);
            }
        }

        #endregion
    }
}