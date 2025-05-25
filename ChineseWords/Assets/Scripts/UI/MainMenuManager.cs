using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace UI
{
    /// <summary>
    /// 主菜单管理器
    /// 提供游戏模式选择：单机、创建房间（Host）、加入房间（Client）
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        [Header("主菜单面板")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private Button singlePlayerButton;
        [SerializeField] private Button createRoomButton;
        [SerializeField] private Button joinRoomButton;
        [SerializeField] private Button exitButton;

        [Header("创建房间面板")]
        [SerializeField] private GameObject createRoomPanel;
        [SerializeField] private TMP_InputField roomNameInput;
        [SerializeField] private TMP_InputField hostPortInput;
        [SerializeField] private TMP_InputField maxPlayersInput;
        [SerializeField] private Button startHostButton;
        [SerializeField] private Button backFromCreateButton;
        [SerializeField] private TMP_Text hostStatusText;

        [Header("加入房间面板")]
        [SerializeField] private GameObject joinRoomPanel;
        [SerializeField] private TMP_InputField hostIPInput;
        [SerializeField] private TMP_InputField clientPortInput;
        [SerializeField] private TMP_InputField playerNameInput;
        [SerializeField] private Button connectToHostButton;
        [SerializeField] private Button backFromJoinButton;
        [SerializeField] private TMP_Text connectionStatusText;

        [Header("场景名称")]
        [SerializeField] private string offlineGameScene = "OfflineGameScene";
        [SerializeField] private string networkGameScene = "NetworkGameScene";

        // 游戏模式
        public enum GameMode
        {
            SinglePlayer,
            Host,
            Client
        }

        public static GameMode SelectedGameMode { get; private set; }
        public static string PlayerName { get; private set; }
        public static string RoomName { get; private set; }
        public static string HostIP { get; private set; }
        public static ushort Port { get; private set; }
        public static int MaxPlayers { get; private set; }

        private void Start()
        {
            InitializeUI();
            ShowMainMenu();
        }

        /// <summary>
        /// 初始化UI组件和事件
        /// </summary>
        private void InitializeUI()
        {
            // 设置默认值
            if (roomNameInput != null)
                roomNameInput.text = "我的房间";
            if (hostPortInput != null)
                hostPortInput.text = "7777";
            if (maxPlayersInput != null)
                maxPlayersInput.text = "4";
            if (hostIPInput != null)
                hostIPInput.text = "127.0.0.1";
            if (clientPortInput != null)
                clientPortInput.text = "7777";
            if (playerNameInput != null)
                playerNameInput.text = "玩家" + Random.Range(1000, 9999);

            // 绑定主菜单按钮事件
            if (singlePlayerButton != null)
                singlePlayerButton.onClick.AddListener(OnSinglePlayerClicked);
            if (createRoomButton != null)
                createRoomButton.onClick.AddListener(OnCreateRoomClicked);
            if (joinRoomButton != null)
                joinRoomButton.onClick.AddListener(OnJoinRoomClicked);
            if (exitButton != null)
                exitButton.onClick.AddListener(OnExitClicked);

            // 绑定创建房间面板按钮事件
            if (startHostButton != null)
                startHostButton.onClick.AddListener(OnStartHostClicked);
            if (backFromCreateButton != null)
                backFromCreateButton.onClick.AddListener(OnBackFromCreateClicked);

            // 绑定加入房间面板按钮事件
            if (connectToHostButton != null)
                connectToHostButton.onClick.AddListener(OnConnectToHostClicked);
            if (backFromJoinButton != null)
                backFromJoinButton.onClick.AddListener(OnBackFromJoinClicked);

            // 清空状态文本
            if (hostStatusText != null)
                hostStatusText.text = "";
            if (connectionStatusText != null)
                connectionStatusText.text = "";
        }

        /// <summary>
        /// 显示主菜单
        /// </summary>
        private void ShowMainMenu()
        {
            SetPanelActive(mainMenuPanel, true);
            SetPanelActive(createRoomPanel, false);
            SetPanelActive(joinRoomPanel, false);
        }

        /// <summary>
        /// 显示创建房间面板
        /// </summary>
        private void ShowCreateRoomPanel()
        {
            SetPanelActive(mainMenuPanel, false);
            SetPanelActive(createRoomPanel, true);
            SetPanelActive(joinRoomPanel, false);
        }

        /// <summary>
        /// 显示加入房间面板
        /// </summary>
        private void ShowJoinRoomPanel()
        {
            SetPanelActive(mainMenuPanel, false);
            SetPanelActive(createRoomPanel, false);
            SetPanelActive(joinRoomPanel, true);
        }

        /// <summary>
        /// 安全设置面板激活状态
        /// </summary>
        private void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
                panel.SetActive(active);
        }

        #region 主菜单按钮事件

        /// <summary>
        /// 单机游戏按钮点击
        /// </summary>
        private void OnSinglePlayerClicked()
        {
            Debug.Log("选择单机游戏");
            SelectedGameMode = GameMode.SinglePlayer;

            // 加载单机游戏场景
            LoadScene(offlineGameScene);
        }

        /// <summary>
        /// 创建房间按钮点击
        /// </summary>
        private void OnCreateRoomClicked()
        {
            Debug.Log("选择创建房间");
            ShowCreateRoomPanel();
        }

        /// <summary>
        /// 加入房间按钮点击
        /// </summary>
        private void OnJoinRoomClicked()
        {
            Debug.Log("选择加入房间");
            ShowJoinRoomPanel();
        }

        /// <summary>
        /// 退出游戏按钮点击
        /// </summary>
        private void OnExitClicked()
        {
            Debug.Log("退出游戏");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
        }

        #endregion

        #region 创建房间面板事件

        /// <summary>
        /// 开始作为主机按钮点击
        /// </summary>
        private void OnStartHostClicked()
        {
            // 验证输入
            if (!ValidateHostInputs())
                return;

            // 设置Host模式参数
            SelectedGameMode = GameMode.Host;
            RoomName = roomNameInput.text.Trim();
            PlayerName = "房主"; // Host默认名称

            if (ushort.TryParse(hostPortInput.text, out ushort port))
                Port = port;
            else
                Port = 7777;

            if (int.TryParse(maxPlayersInput.text, out int maxPlayers))
                MaxPlayers = Mathf.Clamp(maxPlayers, 2, 8);
            else
                MaxPlayers = 4;

            Debug.Log($"创建房间: {RoomName}, 端口: {Port}, 最大玩家数: {MaxPlayers}");

            // 显示状态并加载网络场景
            UpdateHostStatusText("正在创建房间...");
            LoadScene(networkGameScene);
        }

        /// <summary>
        /// 从创建房间面板返回
        /// </summary>
        private void OnBackFromCreateClicked()
        {
            Debug.Log("从创建房间返回主菜单");
            ShowMainMenu();
        }

        /// <summary>
        /// 验证Host输入
        /// </summary>
        private bool ValidateHostInputs()
        {
            if (roomNameInput != null && string.IsNullOrWhiteSpace(roomNameInput.text))
            {
                UpdateHostStatusText("请输入房间名称");
                return false;
            }

            // 验证端口号
            ushort port = 7777; // 默认值
            if (hostPortInput != null && !ushort.TryParse(hostPortInput.text, out port))
            {
                UpdateHostStatusText("端口号格式错误");
                return false;
            }

            if (port < 1024 || port > 65535)
            {
                UpdateHostStatusText("端口号范围: 1024-65535");
                return false;
            }

            // 验证最大玩家数
            int maxPlayers = 4; // 默认值
            if (maxPlayersInput != null && !int.TryParse(maxPlayersInput.text, out maxPlayers))
            {
                UpdateHostStatusText("最大玩家数格式错误");
                return false;
            }

            if (maxPlayers < 2 || maxPlayers > 8)
            {
                UpdateHostStatusText("玩家数范围: 2-8");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 更新Host状态文本
        /// </summary>
        private void UpdateHostStatusText(string text)
        {
            if (hostStatusText != null)
                hostStatusText.text = text;
        }

        #endregion

        #region 加入房间面板事件

        /// <summary>
        /// 连接到主机按钮点击
        /// </summary>
        private void OnConnectToHostClicked()
        {
            // 验证输入
            if (!ValidateClientInputs())
                return;

            // 设置Client模式参数
            SelectedGameMode = GameMode.Client;
            HostIP = hostIPInput.text.Trim();
            PlayerName = playerNameInput.text.Trim();

            if (ushort.TryParse(clientPortInput.text, out ushort port))
                Port = port;
            else
                Port = 7777;

            Debug.Log($"连接到主机: {HostIP}:{Port}, 玩家名: {PlayerName}");

            // 显示状态并加载网络场景
            UpdateConnectionStatusText("正在连接到主机...");
            LoadScene(networkGameScene);
        }

        /// <summary>
        /// 从加入房间面板返回
        /// </summary>
        private void OnBackFromJoinClicked()
        {
            Debug.Log("从加入房间返回主菜单");
            ShowMainMenu();
        }

        /// <summary>
        /// 验证Client输入
        /// </summary>
        private bool ValidateClientInputs()
        {
            if (hostIPInput != null && string.IsNullOrWhiteSpace(hostIPInput.text))
            {
                UpdateConnectionStatusText("请输入主机IP地址");
                return false;
            }

            if (playerNameInput != null && string.IsNullOrWhiteSpace(playerNameInput.text))
            {
                UpdateConnectionStatusText("请输入玩家名称");
                return false;
            }

            // 验证端口号
            ushort port = 7777; // 默认值
            if (clientPortInput != null && !ushort.TryParse(clientPortInput.text, out port))
            {
                UpdateConnectionStatusText("端口号格式错误");
                return false;
            }

            if (port < 1 || port > 65535)
            {
                UpdateConnectionStatusText("端口号范围: 1-65535");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 更新连接状态文本
        /// </summary>
        private void UpdateConnectionStatusText(string text)
        {
            if (connectionStatusText != null)
                connectionStatusText.text = text;
        }

        #endregion

        /// <summary>
        /// 加载场景
        /// </summary>
        private void LoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("场景名称为空");
                return;
            }

            try
            {
                SceneManager.LoadScene(sceneName);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"加载场景失败: {sceneName}, 错误: {e.Message}");
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        private void OnDestroy()
        {
            // 清理所有按钮事件监听
            if (singlePlayerButton != null)
                singlePlayerButton.onClick.RemoveAllListeners();
            if (createRoomButton != null)
                createRoomButton.onClick.RemoveAllListeners();
            if (joinRoomButton != null)
                joinRoomButton.onClick.RemoveAllListeners();
            if (exitButton != null)
                exitButton.onClick.RemoveAllListeners();
            if (startHostButton != null)
                startHostButton.onClick.RemoveAllListeners();
            if (backFromCreateButton != null)
                backFromCreateButton.onClick.RemoveAllListeners();
            if (connectToHostButton != null)
                connectToHostButton.onClick.RemoveAllListeners();
            if (backFromJoinButton != null)
                backFromJoinButton.onClick.RemoveAllListeners();
        }
    }
}