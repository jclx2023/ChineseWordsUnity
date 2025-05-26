using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Core.Network;

namespace UI
{
    /// <summary>
    /// ���˵�������
    /// �ṩ��Ϸģʽѡ�񣺵������������䣨Host�������뷿�䣨Client��
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        [Header("���˵����")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private Button singlePlayerButton;
        [SerializeField] private Button createRoomButton;
        [SerializeField] private Button joinRoomButton;
        [SerializeField] private Button exitButton;

        [Header("�����������")]
        [SerializeField] private GameObject createRoomPanel;
        [SerializeField] private TMP_InputField roomNameInput;
        [SerializeField] private TMP_InputField hostPortInput;
        [SerializeField] private TMP_InputField maxPlayersInput;
        [SerializeField] private TMP_InputField hostPlayerNameInput; // ���������������
        [SerializeField] private Button startHostButton;
        [SerializeField] private Button backFromCreateButton;
        [SerializeField] private TMP_Text hostStatusText;

        [Header("���뷿�����")]
        [SerializeField] private GameObject joinRoomPanel;
        [SerializeField] private TMP_InputField hostIPInput;
        [SerializeField] private TMP_InputField clientPortInput;
        [SerializeField] private TMP_InputField playerNameInput;
        [SerializeField] private Button connectToHostButton;
        [SerializeField] private Button backFromJoinButton;
        [SerializeField] private TMP_Text connectionStatusText;

        [Header("��������")]
        [SerializeField] private string offlineGameScene = "OfflineGameScene";
        [SerializeField] private string networkGameScene = "NetworkGameScene";
        [SerializeField] private string roomScene = "RoomScene"; // ���������䳡��

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        // ��Ϸģʽ
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

        // ����״̬����
        private bool isConnecting = false;

        private void Start()
        {
            Application.runInBackground = true;
            InitializeUI();
            ShowMainMenu();
        }

        /// <summary>
        /// ��ʼ��UI������¼�
        /// </summary>
        private void InitializeUI()
        {
            // ����Ĭ��ֵ
            if (roomNameInput != null)
                roomNameInput.text = "�ҵķ���";
            if (hostPortInput != null)
                hostPortInput.text = "7777";
            if (maxPlayersInput != null)
                maxPlayersInput.text = "4";
            if (hostPlayerNameInput != null)
                hostPlayerNameInput.text = "����" + Random.Range(100, 999);
            if (hostIPInput != null)
                hostIPInput.text = "127.0.0.1";
            if (clientPortInput != null)
                clientPortInput.text = "7777";
            if (playerNameInput != null)
                playerNameInput.text = "���" + Random.Range(1000, 9999);

            // �����˵���ť�¼�
            if (singlePlayerButton != null)
                singlePlayerButton.onClick.AddListener(OnSinglePlayerClicked);
            if (createRoomButton != null)
                createRoomButton.onClick.AddListener(OnCreateRoomClicked);
            if (joinRoomButton != null)
                joinRoomButton.onClick.AddListener(OnJoinRoomClicked);
            if (exitButton != null)
                exitButton.onClick.AddListener(OnExitClicked);

            // �󶨴���������尴ť�¼�
            if (startHostButton != null)
                startHostButton.onClick.AddListener(OnStartHostClicked);
            if (backFromCreateButton != null)
                backFromCreateButton.onClick.AddListener(OnBackFromCreateClicked);

            // �󶨼��뷿����尴ť�¼�
            if (connectToHostButton != null)
                connectToHostButton.onClick.AddListener(OnConnectToHostClicked);
            if (backFromJoinButton != null)
                backFromJoinButton.onClick.AddListener(OnBackFromJoinClicked);

            // ���״̬�ı�
            if (hostStatusText != null)
                hostStatusText.text = "";
            if (connectionStatusText != null)
                connectionStatusText.text = "";
        }

        /// <summary>
        /// ��ʾ���˵�
        /// </summary>
        private void ShowMainMenu()
        {
            SetPanelActive(mainMenuPanel, true);
            SetPanelActive(createRoomPanel, false);
            SetPanelActive(joinRoomPanel, false);
        }

        /// <summary>
        /// ��ʾ�����������
        /// </summary>
        private void ShowCreateRoomPanel()
        {
            SetPanelActive(mainMenuPanel, false);
            SetPanelActive(createRoomPanel, true);
            SetPanelActive(joinRoomPanel, false);
        }

        /// <summary>
        /// ��ʾ���뷿�����
        /// </summary>
        private void ShowJoinRoomPanel()
        {
            SetPanelActive(mainMenuPanel, false);
            SetPanelActive(createRoomPanel, false);
            SetPanelActive(joinRoomPanel, true);
        }

        /// <summary>
        /// ��ȫ������弤��״̬
        /// </summary>
        private void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
                panel.SetActive(active);
        }

        #region ���˵���ť�¼�

        /// <summary>
        /// ������Ϸ��ť���
        /// </summary>
        private void OnSinglePlayerClicked()
        {
            LogDebug("ѡ�񵥻���Ϸ");
            SelectedGameMode = GameMode.SinglePlayer;

            // ���ص�����Ϸ����
            LoadScene(offlineGameScene);
        }

        /// <summary>
        /// �������䰴ť���
        /// </summary>
        private void OnCreateRoomClicked()
        {
            LogDebug("ѡ�񴴽�����");
            ShowCreateRoomPanel();
        }

        /// <summary>
        /// ���뷿�䰴ť���
        /// </summary>
        private void OnJoinRoomClicked()
        {
            LogDebug("ѡ����뷿��");
            ShowJoinRoomPanel();
        }

        /// <summary>
        /// �˳���Ϸ��ť���
        /// </summary>
        private void OnExitClicked()
        {
            LogDebug("�˳���Ϸ");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
        }

        #region �����¼�����

        #region ������������¼�

        /// <summary>
        /// ��ʼ��Ϊ������ť���
        /// </summary>
        private void OnStartHostClicked()
        {
            if (isConnecting)
            {
                LogDebug("���������У����Ժ�");
                return;
            }

            // ��֤����
            if (!ValidateHostInputs())
                return;

            // ����Hostģʽ����
            SelectedGameMode = GameMode.Host;
            RoomName = roomNameInput.text.Trim();
            PlayerName = hostPlayerNameInput?.text.Trim() ?? "����";

            if (ushort.TryParse(hostPortInput.text, out ushort port))
                Port = port;
            else
                Port = 7777;

            if (int.TryParse(maxPlayersInput.text, out int maxPlayers))
                MaxPlayers = Mathf.Clamp(maxPlayers, 2, 8);
            else
                MaxPlayers = 4;

            LogDebug($"��������: {RoomName}, ���: {PlayerName}, �˿�: {Port}, ��������: {MaxPlayers}");

            // ��ʾ״̬
            UpdateHostStatusText("������������...");
            isConnecting = true;

            // ȷ��NetworkManager����
            if (EnsureNetworkManager())
            {
                // ����Host�����¼�
                NetworkManager.OnHostStarted += OnHostStartedForRoom;

                // ����Host
                NetworkManager.Instance.StartAsHost(Port, RoomName, MaxPlayers);
            }
            else
            {
                LogDebug("�޷��������ҵ�NetworkManager");
                UpdateHostStatusText("�����ʼ��ʧ��");
                isConnecting = false;
            }
        }

        /// <summary>
        /// �Ӵ���������巵��
        /// </summary>
        private void OnBackFromCreateClicked()
        {
            LogDebug("�Ӵ������䷵�����˵�");

            // ȡ������״̬
            isConnecting = false;

            // ���������¼�����
            CleanupNetworkEvents();

            ShowMainMenu();
        }

        /// <summary>
        /// ��֤Host����
        /// </summary>
        private bool ValidateHostInputs()
        {
            if (roomNameInput != null && string.IsNullOrWhiteSpace(roomNameInput.text))
            {
                UpdateHostStatusText("�����뷿������");
                return false;
            }

            if (hostPlayerNameInput != null && string.IsNullOrWhiteSpace(hostPlayerNameInput.text))
            {
                UpdateHostStatusText("�������������");
                return false;
            }

            // ��֤�˿ں�
            ushort port = 7777; // Ĭ��ֵ
            if (hostPortInput != null && !ushort.TryParse(hostPortInput.text, out port))
            {
                UpdateHostStatusText("�˿ںŸ�ʽ����");
                return false;
            }

            if (port < 1024 || port > 65535)
            {
                UpdateHostStatusText("�˿ںŷ�Χ: 1024-65535");
                return false;
            }

            // ��֤��������
            int maxPlayers = 4; // Ĭ��ֵ
            if (maxPlayersInput != null && !int.TryParse(maxPlayersInput.text, out maxPlayers))
            {
                UpdateHostStatusText("����������ʽ����");
                return false;
            }

            if (maxPlayers < 2 || maxPlayers > 8)
            {
                UpdateHostStatusText("�������Χ: 2-8");
                return false;
            }

            return true;
        }

        /// <summary>
        /// ����Host״̬�ı�
        /// </summary>
        private void UpdateHostStatusText(string text)
        {
            if (hostStatusText != null)
                hostStatusText.text = text;
        }

        #endregion

        #region ���뷿������¼�

        /// <summary>
        /// ���ӵ�������ť���
        /// </summary>
        private void OnConnectToHostClicked()
        {
            if (isConnecting)
            {
                LogDebug("���������У����Ժ�");
                return;
            }

            // ��֤����
            if (!ValidateClientInputs())
                return;

            // ����Clientģʽ����
            SelectedGameMode = GameMode.Client;
            HostIP = hostIPInput.text.Trim();
            PlayerName = playerNameInput.text.Trim();

            if (ushort.TryParse(clientPortInput.text, out ushort port))
                Port = port;
            else
                Port = 7777;

            LogDebug($"���ӵ�����: {HostIP}:{Port}, �����: {PlayerName}");

            // ��ʾ״̬
            UpdateConnectionStatusText("�������ӵ�����...");
            isConnecting = true;

            // ȷ��NetworkManager����
            if (EnsureNetworkManager())
            {
                // ���������¼�
                NetworkManager.OnConnected += OnConnectedForRoom;

                // ���ӵ�Host
                NetworkManager.Instance.ConnectAsClient(HostIP, Port);
            }
            else
            {
                LogDebug("�޷��������ҵ�NetworkManager");
                UpdateConnectionStatusText("�����ʼ��ʧ��");
                isConnecting = false;
            }
        }

        /// <summary>
        /// �Ӽ��뷿����巵��
        /// </summary>
        private void OnBackFromJoinClicked()
        {
            LogDebug("�Ӽ��뷿�䷵�����˵�");

            // ȡ������״̬
            isConnecting = false;

            // ���������¼�����
            CleanupNetworkEvents();

            ShowMainMenu();
        }

        /// <summary>
        /// ��֤Client����
        /// </summary>
        private bool ValidateClientInputs()
        {
            if (hostIPInput != null && string.IsNullOrWhiteSpace(hostIPInput.text))
            {
                UpdateConnectionStatusText("����������IP��ַ");
                return false;
            }

            if (playerNameInput != null && string.IsNullOrWhiteSpace(playerNameInput.text))
            {
                UpdateConnectionStatusText("�������������");
                return false;
            }

            // ��֤�˿ں�
            ushort port = 7777; // Ĭ��ֵ
            if (clientPortInput != null && !ushort.TryParse(clientPortInput.text, out port))
            {
                UpdateConnectionStatusText("�˿ںŸ�ʽ����");
                return false;
            }

            if (port < 1 || port > 65535)
            {
                UpdateConnectionStatusText("�˿ںŷ�Χ: 1-65535");
                return false;
            }

            return true;
        }

        /// <summary>
        /// ��������״̬�ı�
        /// </summary>
        private void UpdateConnectionStatusText(string text)
        {
            if (connectionStatusText != null)
                connectionStatusText.text = text;
        }

        #endregion

        #region ���������ȷ��

        /// <summary>
        /// ȷ��NetworkManager��RoomManager���ڲ�����ȷ��ʼ��
        /// </summary>
        private bool EnsureNetworkManager()
        {
            // 1. ���NetworkManager�Ƿ����
            if (NetworkManager.Instance == null)
            {
                LogDebug("NetworkManager�����ڣ����Բ��һ򴴽�");

                // �����ڳ����в���
                NetworkManager existingNetworkManager = FindObjectOfType<NetworkManager>();
                if (existingNetworkManager == null)
                {
                    // ���Դ���NetworkManager
                    if (!CreateNetworkManager())
                    {
                        LogDebug("����NetworkManagerʧ��");
                        return false;
                    }
                }
                else
                {
                    LogDebug("�ڳ������ҵ���NetworkManager");
                }
            }

            // 2. ���RoomManager�Ƿ����
            if (RoomManager.Instance == null)
            {
                LogDebug("RoomManager�����ڣ����Բ��һ򴴽�");

                // �����ڳ����в���
                RoomManager existingRoomManager = FindObjectOfType<RoomManager>();
                if (existingRoomManager == null)
                {
                    // ���Դ���RoomManager
                    if (!CreateRoomManager())
                    {
                        LogDebug("����RoomManagerʧ��");
                        return false;
                    }
                }
                else
                {
                    LogDebug("�ڳ������ҵ���RoomManager");
                }
            }

            // 3. �ֶ���ʼ��NetworkManager����Ҫ�����������������˵��Զ�������
            if (NetworkManager.Instance != null)
            {
                // �����ֶ���ʼ������������NetworkManager֪������Ӧ������������
                NetworkManager.Instance.ManualInitializeNetwork();
            }

            return NetworkManager.Instance != null && RoomManager.Instance != null;
        }

        /// <summary>
        /// ����NetworkManager
        /// </summary>
        private bool CreateNetworkManager()
        {
            try
            {
                // ����NetworkManager GameObject
                GameObject networkManagerObj = new GameObject("NetworkManager");
                DontDestroyOnLoad(networkManagerObj);

                // ���NetworkManager���
                NetworkManager networkManager = networkManagerObj.AddComponent<NetworkManager>();

                LogDebug("NetworkManager�����ɹ�");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"����NetworkManagerʧ��: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// ����RoomManager
        /// </summary>
        private bool CreateRoomManager()
        {
            try
            {
                // ����RoomManager GameObject
                GameObject roomManagerObj = new GameObject("RoomManager");
                DontDestroyOnLoad(roomManagerObj);

                // ���RoomManager���
                RoomManager roomManager = roomManagerObj.AddComponent<RoomManager>();

                LogDebug("RoomManager�����ɹ�");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"����RoomManagerʧ��: {e.Message}");
                return false;
            }
        }

        #endregion

        /// <summary>
        /// Host�����ɹ���Ĵ���
        /// </summary>
        private void OnHostStartedForRoom()
        {
            LogDebug("Host�����ɹ����������䲢�л�����");

            // ȡ������
            NetworkManager.OnHostStarted -= OnHostStartedForRoom;
            isConnecting = false;

            // ��������
            if (RoomManager.Instance != null)
            {
                bool success = RoomManager.Instance.CreateRoom(RoomName, PlayerName);
                if (success)
                {
                    LogDebug("���䴴���ɹ����л������䳡��");
                    // �л������䳡��
                    LoadScene(roomScene);
                }
                else
                {
                    LogDebug("��������ʧ��");
                    UpdateHostStatusText("��������ʧ��");
                }
            }
            else
            {
                LogDebug("RoomManager ʵ��������");
                UpdateHostStatusText("���������δ�ҵ�");
            }
        }

        /// <summary>
        /// �ͻ������ӳɹ���Ĵ���
        /// </summary>
        private void OnConnectedForRoom()
        {
            LogDebug("���ӳɹ������뷿�䲢�л�����");

            // ȡ������
            NetworkManager.OnConnected -= OnConnectedForRoom;
            isConnecting = false;

            // ���뷿��
            if (RoomManager.Instance != null)
            {
                bool success = RoomManager.Instance.JoinRoom(PlayerName);
                if (success)
                {
                    LogDebug("���뷿��ɹ����л������䳡��");
                    // �л������䳡��
                    LoadScene(roomScene);
                }
                else
                {
                    LogDebug("���뷿��ʧ��");
                    UpdateConnectionStatusText("���뷿��ʧ��");
                }
            }
            else
            {
                LogDebug("RoomManager ʵ��������");
                UpdateConnectionStatusText("���������δ�ҵ�");
            }
        }

        /// <summary>
        /// ���������¼�����
        /// </summary>
        private void CleanupNetworkEvents()
        {
            NetworkManager.OnHostStarted -= OnHostStartedForRoom;
            NetworkManager.OnConnected -= OnConnectedForRoom;
        }

        #endregion

        /// <summary>
        /// ���س���
        /// </summary>
        private void LoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("��������Ϊ��");
                return;
            }

            try
            {
                LogDebug($"���س���: {sceneName}");
                SceneManager.LoadScene(sceneName);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"���س���ʧ��: {sceneName}, ����: {e.Message}");
            }
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[MainMenuManager] {message}");
            }
        }

        /// <summary>
        /// ������Դ
        /// </summary>
        private void OnDestroy()
        {
            // ���������¼�����
            CleanupNetworkEvents();

            // �������а�ť�¼�����
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
#endregion