using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lobby.Core;
using Lobby.Data;

namespace Lobby.UI
{
    /// <summary>
    /// 房间创建UI控制器
    /// 负责房间创建表单的处理
    /// </summary>
    public class LobbyRoomCreationUI : MonoBehaviour
    {
        [Header("UI组件引用")]
        [SerializeField] private TMP_InputField roomNameInput;
        [SerializeField] private TMP_InputField maxPlayersInput;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private Button createRoomButton;

        [Header("输入验证配置")]
        [SerializeField] private int minRoomNameLength = 2;
        [SerializeField] private int maxRoomNameLength = 30;
        [SerializeField] private int minMaxPlayers = 2;
        [SerializeField] private int maxMaxPlayers = 8;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // UI状态
        private bool isInitialized = false;
        private bool isCreatingRoom = false;
        private static readonly string[] GradeOptions =
        {
            // 小学
            "一年级","二年级","三年级","四年级","五年级","六年级",
            // 初中
            "初一","初二","初三",
            // 高中
            "高一","高二","高三"
        };

        #region Unity生命周期

        private void Start()
        {
            InitializeUI();
            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            CleanupUI();
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化UI
        /// </summary>
        private void InitializeUI()
        {
            LogDebug("初始化房间创建UI");

            // 自动查找组件
            FindUIComponents();

            // 设置默认值
            SetDefaultValues();

            // 绑定事件
            BindInputEvents();

            // 验证表单
            ValidateForm();

            isInitialized = true;
            LogDebug("房间创建UI初始化完成");
        }

        /// <summary>
        /// 查找UI组件
        /// </summary>
        private void FindUIComponents()
        {
            // 查找房间名称输入框
            if (roomNameInput == null)
            {
                roomNameInput = GameObject.Find("RoomNameInput")?.GetComponent<TMP_InputField>();
                if (roomNameInput == null)
                {
                    Debug.LogWarning("[LobbyRoomCreationUI] 未找到RoomNameInput");
                }
            }

            // 查找最大玩家数输入框
            if (maxPlayersInput == null)
            {
                maxPlayersInput = GameObject.Find("MaxPlayersInput")?.GetComponent<TMP_InputField>();
                if (maxPlayersInput == null)
                {
                    Debug.LogWarning("[LobbyRoomCreationUI] 未找到MaxPlayersInput");
                }
            }

            // 查找密码输入框
            if (passwordInput == null)
            {
                passwordInput = GameObject.Find("PasswordInput")?.GetComponent<TMP_InputField>();
                if (passwordInput == null)
                {
                    Debug.LogWarning("[LobbyRoomCreationUI] 未找到PasswordInput");
                }
            }

            // 查找创建房间按钮
            if (createRoomButton == null)
            {
                createRoomButton = GameObject.Find("CreateRoomButton")?.GetComponent<Button>();
                if (createRoomButton == null)
                {
                    Debug.LogWarning("[LobbyRoomCreationUI] 未找到CreateRoomButton");
                }
            }
        }

        /// <summary>
        /// 设置默认值
        /// </summary>
        private void SetDefaultValues()
        {
            // 生成默认房间名
            if (roomNameInput != null)
            {
                roomNameInput.text = GenerateRoomName();
            }

            // 设置默认最大玩家数
            if (maxPlayersInput != null)
            {
                maxPlayersInput.text = "4";
                maxPlayersInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            }

            // 密码输入框设置
            if (passwordInput != null)
            {
                passwordInput.text = "";
                passwordInput.contentType = TMP_InputField.ContentType.Password;
            }
        }
        /// <summary>
        /// 随机生成 “年级 + 班级” 格式的房间名，例如 “高二17班”“三年级4班”
        /// </summary>
        private string GenerateRoomName()
        {
            // 随机年级
            string grade = GradeOptions[UnityEngine.Random.Range(0, GradeOptions.Length)];

            // 随机班级号（1~999）
            int classNumber = UnityEngine.Random.Range(1, 1000);   // 上限999

            return $"{grade}{classNumber}班";
        }

        /// <summary>
        /// 绑定输入事件
        /// </summary>
        private void BindInputEvents()
        {
            // 房间名称输入验证
            if (roomNameInput != null)
            {
                roomNameInput.onValueChanged.AddListener(OnRoomNameChanged);
                roomNameInput.characterLimit = maxRoomNameLength;
            }

            // 最大玩家数输入验证
            if (maxPlayersInput != null)
            {
                maxPlayersInput.onValueChanged.AddListener(OnMaxPlayersChanged);
                maxPlayersInput.characterLimit = 1;
            }

            // 创建房间按钮事件
            if (createRoomButton != null)
            {
                createRoomButton.onClick.AddListener(OnCreateRoomClicked);
            }

            LogDebug("已绑定输入事件");
        }

        /// <summary>
        /// 订阅网络事件
        /// </summary>
        private void SubscribeToEvents()
        {
            if (LobbyNetworkManager.Instance != null)
            {
                LobbyNetworkManager.Instance.OnRoomCreated += OnRoomCreated;
                LobbyNetworkManager.Instance.OnConnectionStatusChanged += OnConnectionStatusChanged;
                LogDebug("已订阅网络事件");
            }
        }

        /// <summary>
        /// 取消订阅网络事件
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (LobbyNetworkManager.Instance != null)
            {
                LobbyNetworkManager.Instance.OnRoomCreated -= OnRoomCreated;
                LobbyNetworkManager.Instance.OnConnectionStatusChanged -= OnConnectionStatusChanged;
                LogDebug("已取消订阅网络事件");
            }
        }

        /// <summary>
        /// 清理UI
        /// </summary>
        private void CleanupUI()
        {
            // 移除输入事件监听
            if (roomNameInput != null)
            {
                roomNameInput.onValueChanged.RemoveListener(OnRoomNameChanged);
            }

            if (maxPlayersInput != null)
            {
                maxPlayersInput.onValueChanged.RemoveListener(OnMaxPlayersChanged);
            }

            if (createRoomButton != null)
            {
                createRoomButton.onClick.RemoveListener(OnCreateRoomClicked);
            }

            LogDebug("UI清理完成");
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 房间名称改变事件
        /// </summary>
        private void OnRoomNameChanged(string value)
        {
            ValidateForm();
        }

        /// <summary>
        /// 最大玩家数改变事件
        /// </summary>
        private void OnMaxPlayersChanged(string value)
        {
            // 限制输入范围
            if (int.TryParse(value, out int playerCount))
            {
                if (playerCount < minMaxPlayers)
                {
                    maxPlayersInput.text = minMaxPlayers.ToString();
                }
                else if (playerCount > maxMaxPlayers)
                {
                    maxPlayersInput.text = maxMaxPlayers.ToString();
                }
            }
            else if (!string.IsNullOrEmpty(value))
            {
                // 如果输入无效，设为默认值
                maxPlayersInput.text = "4";
            }

            ValidateForm();
        }

        /// <summary>
        /// 创建房间按钮点击事件
        /// </summary>
        private void OnCreateRoomClicked()
        {
            LogDebug("=== 创建房间按钮点击 ===");
            LogDebug($"表单验证: {IsFormValid()}");
            LogDebug($"正在创建中: {isCreatingRoom}");
            LogDebug($"LobbyNetworkManager存在: {LobbyNetworkManager.Instance != null}");

            if (isCreatingRoom || !IsFormValid())
            {
                LogDebug("❌ 前置条件检查失败");
                return;
            }

            LogDebug("✓ 前置条件检查通过");

            // 获取表单数据
            string roomName = roomNameInput.text.Trim();
            int maxPlayers = int.Parse(maxPlayersInput.text);
            string password = "";

            LogDebug($"调用LobbyNetworkManager.CreateRoom...");

            // 开始创建房间
            StartCreatingRoom(roomName, maxPlayers, password);
        }

        /// <summary>
        /// 房间创建结果事件
        /// </summary>
        private void OnRoomCreated(string roomName, bool success)
        {
            LogDebug($"房间创建结果: {roomName}, 成功: {success}");

            isCreatingRoom = false;
            UpdateCreateButtonState();

            if (success)
            {
                // 创建成功，显示提示
                if (LobbyUIManager.Instance != null)
                {
                    LobbyUIManager.Instance.ShowMessage($"房间 '{roomName}' 创建成功！");
                }

                // 清空表单（可选）
                // ClearForm();
            }
            else
            {
                // 创建失败，显示错误提示
                if (LobbyUIManager.Instance != null)
                {
                    LobbyUIManager.Instance.ShowMessage($"房间 '{roomName}' 创建失败，请重试");
                }
            }
        }

        /// <summary>
        /// 连接状态改变事件
        /// </summary>
        private void OnConnectionStatusChanged(bool isConnected)
        {
            LogDebug($"连接状态改变: {isConnected}");
            UpdateCreateButtonState();
        }

        #endregion

        #region 表单验证

        /// <summary>
        /// 验证表单
        /// </summary>
        private void ValidateForm()
        {
            UpdateCreateButtonState();
        }

        /// <summary>
        /// 检查表单是否有效
        /// </summary>
        private bool IsFormValid()
        {
            // 检查房间名称
            if (roomNameInput == null || string.IsNullOrEmpty(roomNameInput.text.Trim()))
                return false;

            string roomName = roomNameInput.text.Trim();
            if (roomName.Length < minRoomNameLength || roomName.Length > maxRoomNameLength)
                return false;

            // 检查最大玩家数
            if (maxPlayersInput == null || !int.TryParse(maxPlayersInput.text, out int maxPlayers))
                return false;

            if (maxPlayers < minMaxPlayers || maxPlayers > maxMaxPlayers)
                return false;

            return true;
        }

        /// <summary>
        /// 更新创建按钮状态
        /// </summary>
        private void UpdateCreateButtonState()
        {
            if (createRoomButton == null)
                return;

            bool isConnected = LobbyNetworkManager.Instance?.IsConnected() ?? false;
            bool isFormValid = IsFormValid();
            bool canCreate = isConnected && isFormValid && !isCreatingRoom;

            createRoomButton.interactable = canCreate;

            // 更新按钮文本
            var buttonText = createRoomButton.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                if (isCreatingRoom)
                {
                    buttonText.text = "创建中...";
                }
                else
                {
                    buttonText.text = "创建房间";
                }
            }
        }

        #endregion

        #region 房间创建逻辑

        /// <summary>
        /// 开始创建房间
        /// </summary>
        private void StartCreatingRoom(string roomName, int maxPlayers, string password)
        {
            LogDebug($"开始创建房间: {roomName}, 最大玩家: {maxPlayers}");

            isCreatingRoom = true;
            UpdateCreateButtonState();

            if (LobbyNetworkManager.Instance != null)
            {
                LobbyNetworkManager.Instance.CreateRoom(roomName, maxPlayers, password);
            }
        }

        /// <summary>
        /// 清空表单
        /// </summary>
        private void ClearForm()
        {
            if (roomNameInput != null)
            {
                roomNameInput.text = "";
            }

            if (maxPlayersInput != null)
            {
                maxPlayersInput.text = "4";
            }

            if (passwordInput != null)
            {
                passwordInput.text = "";
            }

            ValidateForm();
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 设置房间名称
        /// </summary>
        public void SetRoomName(string roomName)
        {
            if (roomNameInput != null)
            {
                roomNameInput.text = roomName;
                ValidateForm();
            }
        }

        /// <summary>
        /// 设置最大玩家数
        /// </summary>
        public void SetMaxPlayers(int maxPlayers)
        {
            if (maxPlayersInput != null)
            {
                maxPlayers = Mathf.Clamp(maxPlayers, minMaxPlayers, maxMaxPlayers);
                maxPlayersInput.text = maxPlayers.ToString();
                ValidateForm();
            }
        }

        /// <summary>
        /// 获取当前房间设置
        /// </summary>
        public LobbyRoomData GetCurrentRoomSettings()
        {
            if (!IsFormValid())
                return null;

            var roomData = new LobbyRoomData();
            roomData.roomName = roomNameInput.text.Trim();
            roomData.maxPlayers = int.Parse(maxPlayersInput.text);
            roomData.hasPassword = !string.IsNullOrEmpty(passwordInput.text.Trim());
            roomData.password = passwordInput.text.Trim();

            if (LobbySceneManager.Instance != null)
            {
                var playerData = LobbySceneManager.Instance.GetCurrentPlayerData();
                if (playerData != null)
                {
                    roomData.hostPlayerName = playerData.playerName;
                }
            }

            return roomData;
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
                Debug.Log($"[LobbyRoomCreationUI] {message}");
            }
        }

        [ContextMenu("显示房间创建UI状态")]
        public void ShowRoomCreationStatus()
        {
            string status = "=== 房间创建UI状态 ===\n";
            status += $"UI已初始化: {isInitialized}\n";
            status += $"正在创建房间: {isCreatingRoom}\n";
            status += $"表单有效: {IsFormValid()}\n";
            status += $"房间名称输入框: {(roomNameInput != null ? "✓" : "✗")}\n";
            status += $"最大玩家数输入框: {(maxPlayersInput != null ? "✓" : "✗")}\n";
            status += $"密码输入框: {(passwordInput != null ? "✓" : "✗")}\n";
            status += $"创建按钮: {(createRoomButton != null ? "✓" : "✗")}";

            LogDebug(status);
        }

        [ContextMenu("测试创建房间")]
        public void TestCreateRoom()
        {
            OnCreateRoomClicked();
        }
    }
}

        #endregion