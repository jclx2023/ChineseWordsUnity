using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

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

        private void Start()
        {
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
            Debug.Log("ѡ�񵥻���Ϸ");
            SelectedGameMode = GameMode.SinglePlayer;

            // ���ص�����Ϸ����
            LoadScene(offlineGameScene);
        }

        /// <summary>
        /// �������䰴ť���
        /// </summary>
        private void OnCreateRoomClicked()
        {
            Debug.Log("ѡ�񴴽�����");
            ShowCreateRoomPanel();
        }

        /// <summary>
        /// ���뷿�䰴ť���
        /// </summary>
        private void OnJoinRoomClicked()
        {
            Debug.Log("ѡ����뷿��");
            ShowJoinRoomPanel();
        }

        /// <summary>
        /// �˳���Ϸ��ť���
        /// </summary>
        private void OnExitClicked()
        {
            Debug.Log("�˳���Ϸ");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
        }

        #endregion

        #region ������������¼�

        /// <summary>
        /// ��ʼ��Ϊ������ť���
        /// </summary>
        private void OnStartHostClicked()
        {
            // ��֤����
            if (!ValidateHostInputs())
                return;

            // ����Hostģʽ����
            SelectedGameMode = GameMode.Host;
            RoomName = roomNameInput.text.Trim();
            PlayerName = "����"; // HostĬ������

            if (ushort.TryParse(hostPortInput.text, out ushort port))
                Port = port;
            else
                Port = 7777;

            if (int.TryParse(maxPlayersInput.text, out int maxPlayers))
                MaxPlayers = Mathf.Clamp(maxPlayers, 2, 8);
            else
                MaxPlayers = 4;

            Debug.Log($"��������: {RoomName}, �˿�: {Port}, ��������: {MaxPlayers}");

            // ��ʾ״̬���������糡��
            UpdateHostStatusText("���ڴ�������...");
            LoadScene(networkGameScene);
        }

        /// <summary>
        /// �Ӵ���������巵��
        /// </summary>
        private void OnBackFromCreateClicked()
        {
            Debug.Log("�Ӵ������䷵�����˵�");
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

            Debug.Log($"���ӵ�����: {HostIP}:{Port}, �����: {PlayerName}");

            // ��ʾ״̬���������糡��
            UpdateConnectionStatusText("�������ӵ�����...");
            LoadScene(networkGameScene);
        }

        /// <summary>
        /// �Ӽ��뷿����巵��
        /// </summary>
        private void OnBackFromJoinClicked()
        {
            Debug.Log("�Ӽ��뷿�䷵�����˵�");
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
                SceneManager.LoadScene(sceneName);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"���س���ʧ��: {sceneName}, ����: {e.Message}");
            }
        }

        /// <summary>
        /// ������Դ
        /// </summary>
        private void OnDestroy()
        {
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