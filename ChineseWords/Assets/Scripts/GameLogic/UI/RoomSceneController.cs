using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Core.Network;
using Core;

namespace UI
{
    /// <summary>
    /// ���䳡�������� - �����
    /// רע�ڳ������ƺ�ҵ���߼���UIˢ��ί�и�RoomUIController
    /// </summary>
    public class RoomSceneController : MonoBehaviour
    {
        [Header("UI����������")]
        [SerializeField] private RoomUIController roomUIController;

        [Header("���ư�ť")]
        [SerializeField] private Button readyButton;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button leaveRoomButton;
        [SerializeField] private Button refreshButton;

        [Header("״̬��ʾ")]
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private TMP_Text loadingText;
        [SerializeField] private GameObject errorPanel;
        [SerializeField] private TMP_Text errorText;

        [Header("����")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool autoFindUIController = true;

        // ״̬����
        private bool isInitialized = false;
        private bool hasHandledGameStart = false;

        private void Start()
        {
            InitializeRoomScene();
        }

        /// <summary>
        /// ��ʼ�����䳡��
        /// </summary>
        private void InitializeRoomScene()
        {
            LogDebug("��ʼ�����䳡��");

            // ��ʾ���ؽ���
            ShowLoadingPanel("���ڼ��ط�����Ϣ...");

            // ����UI������
            if (autoFindUIController && roomUIController == null)
            {
                roomUIController = FindObjectOfType<RoomUIController>();
                if (roomUIController != null)
                {
                    LogDebug("�Զ��ҵ�RoomUIController");
                }
                else
                {
                    LogDebug("����: δ�ҵ�RoomUIController������UI���ܿ��ܲ�����");
                }
            }

            // ��UI�¼�
            BindUIEvents();

            // ���ķ����¼�
            SubscribeToRoomEvents();

            // ��֤����״̬
            if (!ValidateNetworkStatus())
            {
                ShowError("���������쳣���뷵�����˵�����");
                return;
            }

            // ��֤����״̬
            if (!ValidateRoomStatus())
            {
                ShowError("����״̬�쳣���뷵�����˵�����");
                return;
            }

            // ֪ͨUI���������г�ʼ��
            if (roomUIController != null)
            {
                // UI���������Զ�����UIˢ�£����ǲ���Ҫ�ֶ�����
                LogDebug("RoomUIController������UIˢ��");
            }

            // ���Ϊ�ѳ�ʼ��
            isInitialized = true;
            HideLoadingPanel();

            LogDebug("���䳡����ʼ�����");
        }

        /// <summary>
        /// ��UI�¼�
        /// </summary>
        private void BindUIEvents()
        {
            if (readyButton != null)
                readyButton.onClick.AddListener(OnReadyButtonClicked);

            if (startGameButton != null)
                startGameButton.onClick.AddListener(OnStartGameButtonClicked);

            if (leaveRoomButton != null)
                leaveRoomButton.onClick.AddListener(OnLeaveRoomButtonClicked);

            if (refreshButton != null)
                refreshButton.onClick.AddListener(OnRefreshButtonClicked);
        }

        /// <summary>
        /// ���ķ����¼�
        /// </summary>
        private void SubscribeToRoomEvents()
        {
            // ֻ���ĳ���������ص��¼���UI�����¼�����RoomUIController����
            RoomManager.OnGameStarting += OnGameStarting;
            RoomManager.OnRoomLeft += OnRoomLeft;

            // ���������¼�
            NetworkManager.OnDisconnected += OnNetworkDisconnected;

            // ���ĳ����л��¼�
            SceneTransitionManager.OnSceneTransitionStarted += OnSceneTransitionStarted;

            LogDebug("�Ѷ��ĳ�����������¼�");
        }

        /// <summary>
        /// ȡ�����ķ����¼�
        /// </summary>
        private void UnsubscribeFromRoomEvents()
        {
            RoomManager.OnGameStarting -= OnGameStarting;
            RoomManager.OnRoomLeft -= OnRoomLeft;
            NetworkManager.OnDisconnected -= OnNetworkDisconnected;
            SceneTransitionManager.OnSceneTransitionStarted -= OnSceneTransitionStarted;
        }

        #region ��֤����

        /// <summary>
        /// ��֤����״̬
        /// </summary>
        private bool ValidateNetworkStatus()
        {
            if (NetworkManager.Instance == null)
            {
                LogDebug("NetworkManager ʵ��������");
                return false;
            }

            if (!NetworkManager.Instance.IsConnected && !NetworkManager.Instance.IsHost)
            {
                LogDebug("����δ�����Ҳ���Host");
                return false;
            }

            return true;
        }

        /// <summary>
        /// ��֤����״̬
        /// </summary>
        private bool ValidateRoomStatus()
        {
            if (RoomManager.Instance == null)
            {
                LogDebug("RoomManager ʵ��������");
                return false;
            }

            if (!RoomManager.Instance.IsInRoom)
            {
                LogDebug("δ�ڷ�����");
                return false;
            }

            return true;
        }

        #endregion

        #region UI��ʾ����

        /// <summary>
        /// ��ʾ�������
        /// </summary>
        private void ShowLoadingPanel(string message)
        {
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(true);
                if (loadingText != null)
                    loadingText.text = message;
            }
        }

        /// <summary>
        /// ���ؼ������
        /// </summary>
        private void HideLoadingPanel()
        {
            if (loadingPanel != null)
                loadingPanel.SetActive(false);
        }

        /// <summary>
        /// ��ʾ������Ϣ
        /// </summary>
        private void ShowError(string message)
        {
            LogDebug($"��ʾ����: {message}");

            HideLoadingPanel();

            if (errorPanel != null)
            {
                errorPanel.SetActive(true);
                if (errorText != null)
                    errorText.text = message;
            }
        }

        /// <summary>
        /// ���ش������
        /// </summary>
        private void HideErrorPanel()
        {
            if (errorPanel != null)
                errorPanel.SetActive(false);
        }

        #endregion

        #region �����¼�����

        /// <summary>
        /// ��Ϸ��ʼ�¼����� - Ψһ�ĳ����л�ִ�е�
        /// </summary>
        private void OnGameStarting()
        {
            // ��ֹ�ظ�����
            if (hasHandledGameStart)
            {
                LogDebug("��Ϸ��ʼ�¼��Ѵ��������ظ�����");
                return;
            }

            hasHandledGameStart = true;
            LogDebug("�յ���Ϸ��ʼ�¼� - ��ʼ�����л�����");

            ShowLoadingPanel("��Ϸ�����У����Ժ�...");

            // ����HostGameManager��ʼ��Ϸ����Host�ˣ�
            if (RoomManager.Instance?.IsHost == true)
            {
                if (HostGameManager.Instance != null)
                {
                    LogDebug("����HostGameManager��ʼ��Ϸ");
                    HostGameManager.Instance.StartGameFromRoom();
                }
                else
                {
                    LogDebug("HostGameManagerʵ��������");
                }
            }

            // ִ�г����л� - ͳһ���л���
            bool switchSuccess = SceneTransitionManager.SwitchToGameScene("RoomSceneController");

            if (!switchSuccess)
            {
                LogDebug("�����л�����ʧ�ܣ����������л���");
                // ����л�ʧ�ܣ����ô����־
                hasHandledGameStart = false;
                HideLoadingPanel();
            }
        }

        private void OnRoomLeft()
        {
            LogDebug("�뿪����");
            SceneTransitionManager.ReturnToMainMenu("RoomSceneController");
        }

        private void OnNetworkDisconnected()
        {
            LogDebug("����Ͽ�����");
            ShowError("�������ӶϿ������������˵�");
            Invoke(nameof(ReturnToMainMenuDelayed), 3f);
        }

        /// <summary>
        /// �����л���ʼ�¼�
        /// </summary>
        private void OnSceneTransitionStarted(string sceneName)
        {
            LogDebug($"�����л���ʼ: {sceneName}");
            ShowLoadingPanel($"�����л��� {sceneName}...");
        }

        #endregion

        #region ��ť�¼�����

        /// <summary>
        /// ׼����ť���
        /// </summary>
        private void OnReadyButtonClicked()
        {
            if (RoomManager.Instance == null || RoomManager.Instance.IsHost)
                return;

            bool currentReady = RoomManager.Instance.GetMyReadyState();
            bool newReady = !currentReady;

            LogDebug($"�л�׼��״̬: {currentReady} -> {newReady}");
            RoomManager.Instance.SetPlayerReady(newReady);
        }

        /// <summary>
        /// ��ʼ��Ϸ��ť���
        /// </summary>
        private void OnStartGameButtonClicked()
        {
            if (RoomManager.Instance == null || !RoomManager.Instance.IsHost)
                return;

            if (!RoomManager.Instance.CanStartGame())
            {
                ShowError("�������δ׼������������");
                Invoke(nameof(HideErrorPanel), 2f);
                return;
            }

            LogDebug("����������Ϸ");

            // ͳһͨ��RoomManager������Ϸ
            RoomManager.Instance.StartGame();
        }

        /// <summary>
        /// �뿪���䰴ť���
        /// </summary>
        private void OnLeaveRoomButtonClicked()
        {
            LogDebug("�û�����뿪����");

            if (RoomManager.Instance != null)
                RoomManager.Instance.LeaveRoom();

            SceneTransitionManager.ReturnToMainMenu("RoomSceneController");
        }

        /// <summary>
        /// ˢ�°�ť���
        /// </summary>
        private void OnRefreshButtonClicked()
        {
            LogDebug("�ֶ�ˢ������");

            // ί�и�RoomUIController����UIˢ��
            if (roomUIController != null)
            {
                roomUIController.ForceRefreshUI();
                LogDebug("������RoomUIControllerǿ��ˢ��");
            }
            else
            {
                LogDebug("RoomUIController�����ڣ��޷�ִ��ˢ��");
            }
        }

        #endregion

        #region ��������

        /// <summary>
        /// �ӳٷ������˵�
        /// </summary>
        private void ReturnToMainMenuDelayed()
        {
            SceneTransitionManager.ReturnToMainMenu("RoomSceneController");
        }

        #endregion

        #region �����ӿ�

        /// <summary>
        /// ��ȡUI����������
        /// </summary>
        public RoomUIController GetUIController()
        {
            return roomUIController;
        }

        /// <summary>
        /// ����UI����������
        /// </summary>
        public void SetUIController(RoomUIController controller)
        {
            roomUIController = controller;
            LogDebug("UI����������������");
        }

        /// <summary>
        /// ǿ��ˢ��UI��ͨ��UI��������
        /// </summary>
        public void ForceRefreshUI()
        {
            if (roomUIController != null)
            {
                roomUIController.ForceRefreshUI();
            }
            else
            {
                LogDebug("�޷�ˢ��UI��RoomUIControllerδ����");
            }
        }

        #endregion

        #region ��������

        /// <summary>
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[RoomSceneController] {message}");
            }
        }

        /// <summary>
        /// ��ȡ������ϸ��Ϣ�������ã�
        /// </summary>
        [ContextMenu("��ʾ������ϸ��Ϣ")]
        public void ShowRoomDetailedInfo()
        {
            if (RoomManager.Instance != null)
            {
                string info = RoomManager.Instance.GetDetailedDebugInfo();
                Debug.Log(info);
            }
        }

        /// <summary>
        /// ��ʾUI������״̬
        /// </summary>
        [ContextMenu("��ʾUI������״̬")]
        public void ShowUIControllerStatus()
        {
            if (roomUIController != null)
            {
                Debug.Log($"=== UI������״̬ ===\n{roomUIController.GetUIStatusInfo()}");
            }
            else
            {
                Debug.Log("UI������: δ����");
            }
        }

        #endregion

        #region Unity��������

        private void OnDestroy()
        {
            // ȡ�������ӳٵ���
            CancelInvoke();

            // ȡ���¼�����
            UnsubscribeFromRoomEvents();

            LogDebug("���䳡������������");
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && isInitialized)
            {
                LogDebug("Ӧ����ͣ");
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && isInitialized)
            {
                LogDebug("Ӧ��ʧȥ����");
            }
        }

        #endregion
    }
}