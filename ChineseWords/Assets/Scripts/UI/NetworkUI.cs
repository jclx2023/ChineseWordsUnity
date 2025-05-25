using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Core.Network;

namespace UI
{
    /// <summary>
    /// ��������UI������
    /// ֻ�����������ӣ���Ϸ�߼���NetworkQuestionManagerController����
    /// </summary>
    public class NetworkUI : MonoBehaviour
    {
        [Header("�����")]
        [SerializeField] private GameObject mainPanel;

        [Header("��Ϸģʽѡ�����")]
        [SerializeField] private GameObject gameModePanel;
        [SerializeField] private Button singlePlayerButton;
        [SerializeField] private Button multiPlayerButton;

        [Header("���ӽ���")]
        [SerializeField] private GameObject connectPanel;
        [SerializeField] private TMP_InputField serverIPInput;
        [SerializeField] private TMP_InputField portInput;
        [SerializeField] private Button connectButton;
        [SerializeField] private Button backToModeButton; // ����ģʽѡ��ť
        [SerializeField] private TMP_Text statusText;

        [Header("�ȴ�����")]
        [SerializeField] private GameObject waitingPanel;
        [SerializeField] private TMP_Text waitingText;
        [SerializeField] private Button disconnectButton; // �Ͽ����Ӱ�ť���ڵȴ�����
        [SerializeField] private TMP_Text clientInfoText;

        private NetworkQuestionManagerController networkQMC;

        private void Start()
        {
            networkQMC = FindObjectOfType<NetworkQuestionManagerController>();
            if (networkQMC == null)
            {
                Debug.LogError("δ�ҵ� NetworkQuestionManagerController");
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
            // ����Ĭ��ֵ
            if (serverIPInput != null)
                serverIPInput.text = "127.0.0.1";
            if (portInput != null)
                portInput.text = "7777";

            // �󶨰�ť�¼�
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

            // ��ʼ״̬����ʾ��Ϸģʽѡ����壨����ѡ�񵥻�����ˣ�
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

        #region UI�¼�����

        private void OnConnectButtonClicked()
        {
            string ip = serverIPInput?.text ?? "127.0.0.1";
            if (ushort.TryParse(portInput?.text ?? "7777", out ushort port))
            {
                NetworkManager.Instance?.Connect(ip, port);
                UpdateStatusText("�������ӷ�����...");
                connectButton.interactable = false;
            }
            else
            {
                UpdateStatusText("�˿ںŸ�ʽ����");
            }
        }

        private void OnDisconnectButtonClicked()
        {
            NetworkManager.Instance?.Disconnect();
        }

        private void OnSinglePlayerClicked()
        {
            Debug.Log("��ʼ������Ϸ");
            // ��������UI����������ģʽ
            HideNetworkUI();
            networkQMC?.StartGame(false); // ����ģʽ
        }

        private void OnMultiPlayerClicked()
        {
            Debug.Log("ѡ�������Ϸ����Ҫ�����ӷ�����");
            // ��ʾ����������û����ӷ�����
            ShowPanel(PanelType.Connect);
        }

        private void OnBackToModeClicked()
        {
            Debug.Log("����ģʽѡ��");
            // ��������巵��ģʽѡ��
            ShowPanel(PanelType.GameMode);
        }

        #endregion

        #region �����¼�����

        private void OnNetworkConnected()
        {
            UpdateStatusText("���ӳɹ�");
            UpdateClientInfoText($"�ͻ���ID: {NetworkManager.Instance.ClientId}");

            // ���ӳɹ�����ʾ������Ϸ�ȴ�����
            ShowPanel(PanelType.Waiting);
            UpdateWaitingText("���ӳɹ����ȴ�������������Ϸ...");

            // �Զ���ʼ������Ϸ
            networkQMC?.StartGame(true); // ����ģʽ

            UpdateUI();
        }

        private void OnNetworkDisconnected()
        {
            UpdateStatusText("���ӶϿ�");
            ShowPanel(PanelType.GameMode); // �ص�ģʽѡ��
            UpdateUI();

            // ������ڶ�����Ϸ�жϿ�����ʾ�û�
            if (networkQMC?.IsMultiplayerMode == true)
            {
                Debug.Log("����Ͽ����ص�ģʽѡ�����");
                ShowNetworkUI(); // ȷ��UI�ɼ�
            }
        }

        private void OnPlayerTurnChanged(ushort playerId)
        {
            if (networkQMC?.IsMultiplayerMode == true)
            {
                bool isMyTurn = playerId == NetworkManager.Instance?.ClientId;
                if (isMyTurn)
                {
                    UpdateWaitingText("�ֵ�������ˣ�");
                    // ���صȴ����棬��ʾ��Ϸ����
                    HideNetworkUI();
                }
                else
                {
                    UpdateWaitingText($"�ֵ���� {playerId} ���⣬��ȴ�...");
                    ShowPanel(PanelType.Waiting);
                }
            }
        }

        #endregion

        #region UI״̬����

        private enum PanelType
        {
            Connect,
            GameMode,
            Waiting
        }

        private void ShowPanel(PanelType panelType)
        {
            // �����������
            if (connectPanel != null) connectPanel.SetActive(false);
            if (gameModePanel != null) gameModePanel.SetActive(false);
            if (waitingPanel != null) waitingPanel.SetActive(false);

            // ��ʾָ�����
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

            // ȷ�������ɼ�
            if (mainPanel != null) mainPanel.SetActive(true);
        }

        private void HideNetworkUI()
        {
            // ������������UI������Ϸ������ʾ
            if (mainPanel != null) mainPanel.SetActive(false);
        }

        private void ShowNetworkUI()
        {
            // ������ʾ����UI
            if (mainPanel != null) mainPanel.SetActive(true);
        }

        private void UpdateUI()
        {
            bool isConnected = NetworkManager.Instance?.IsConnected ?? false;

            if (connectButton != null)
                connectButton.interactable = !isConnected;
            if (disconnectButton != null)
                disconnectButton.interactable = isConnected;

            // ����ģʽʼ�տ��ã�����ģʽ��GameMode�����ʼ�տ���
            if (singlePlayerButton != null)
                singlePlayerButton.interactable = true;
            if (multiPlayerButton != null)
                multiPlayerButton.interactable = true;
        }

        #endregion

        #region UI�ı�����

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

        #region ��������

        /// <summary>
        /// ��ʾ�������
        /// </summary>
        public void ShowNetworkError(string error)
        {
            UpdateStatusText($"����: {error}");
            ShowNetworkUI();
            ShowPanel(PanelType.Connect);
        }

        /// <summary>
        /// ��Ϸ������������ʾģʽѡ��
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