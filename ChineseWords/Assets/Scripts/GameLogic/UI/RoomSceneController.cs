using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Core.Network;
using Core;
using Photon.Pun;
using Photon.Realtime;

namespace UI
{
    /// <summary>
    /// ���䳡�������� - ��ȫ����Photon�汾
    /// רע�ڳ������ƺ�ҵ���߼���UIˢ��ί�и�RoomUIController
    /// ��ȫ���������е�RoomManager��SceneTransitionManager�ܹ�
    /// </summary>
    public class RoomSceneController : MonoBehaviourPun, IInRoomCallbacks
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
        private bool isLeavingRoom = false;

        private void Start()
        {
            InitializeRoomScene();
        }

        private void OnEnable()
        {
            // ע��Photon�ص�
            PhotonNetwork.AddCallbackTarget(this);
        }

        private void OnDisable()
        {
            // ȡ��ע��Photon�ص�
            PhotonNetwork.RemoveCallbackTarget(this);
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

            // ��֤״̬
            if (!ValidateRoomStatus())
            {
                ShowError("����״̬�쳣���뷵�ش�������");
                return;
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
        /// ���ķ����¼� - �������RoomManager�ܹ�
        /// </summary>
        private void SubscribeToRoomEvents()
        {
            // ����RoomManager�¼�������������أ�
            RoomManager.OnGameStarting += OnGameStarting;
            RoomManager.OnReturnToLobby += OnReturnToLobby;

            // ����SceneTransitionManager�¼�
            SceneTransitionManager.OnSceneTransitionStarted += OnSceneTransitionStarted;

            LogDebug("�Ѷ��ķ���ͳ����л��¼�");
        }

        /// <summary>
        /// ȡ�����ķ����¼�
        /// </summary>
        private void UnsubscribeFromRoomEvents()
        {
            RoomManager.OnGameStarting -= OnGameStarting;
            RoomManager.OnReturnToLobby -= OnReturnToLobby;
            SceneTransitionManager.OnSceneTransitionStarted -= OnSceneTransitionStarted;
        }

        #region ��֤����

        /// <summary>
        /// ��֤����״̬
        /// </summary>
        private bool ValidateRoomStatus()
        {
            if (!PhotonNetwork.InRoom)
            {
                LogDebug("δ��Photon������");
                return false;
            }

            if (RoomManager.Instance == null)
            {
                LogDebug("RoomManagerʵ��������");
                return false;
            }

            if (!RoomManager.Instance.IsInitialized)
            {
                LogDebug("RoomManagerδ��ʼ��");
                return false;
            }

            LogDebug($"����״̬��֤ͨ�� - ����: {RoomManager.Instance.RoomName}, �����: {RoomManager.Instance.PlayerCount}");
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

        #region �����¼����� - ������ļܹ�

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

            // ʹ�����SceneTransitionManagerִ�г����л�
            bool switchSuccess = SceneTransitionManager.SwitchToGameScene("RoomSceneController");

            if (!switchSuccess)
            {
                LogDebug("�����л�����ʧ�ܣ����������л���");
                // ����л�ʧ�ܣ����ô����־
                hasHandledGameStart = false;
                HideLoadingPanel();
            }
        }

        /// <summary>
        /// ���ش����¼�����
        /// </summary>
        private void OnReturnToLobby()
        {
            LogDebug("�յ����ش����¼�");
            SceneTransitionManager.ReturnToMainMenu("RoomSceneController");
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

        #region ��ť�¼����� - ί�и�RoomManager

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
                string conditions = RoomManager.Instance.GetGameStartConditions();
                ShowError($"�޷���ʼ��Ϸ: {conditions}");
                Invoke(nameof(HideErrorPanel), 3f);
                return;
            }

            LogDebug("����������Ϸ");

            // ֱ�ӵ���RoomManager��StartGame����
            RoomManager.Instance.StartGame();
        }

        /// <summary>
        /// �뿪���䰴ť���
        /// </summary>
        private void OnLeaveRoomButtonClicked()
        {
            LogDebug("�û�����뿪����");

            // ���Ϊ�����뿪
            isLeavingRoom = true;

            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.LeaveRoomAndReturnToLobby();
            }
            else
            {
                // ���÷�����ֱ��ʹ��SceneTransitionManager
                SceneTransitionManager.ReturnToMainMenu("RoomSceneController");
            }
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
                roomUIController.RefreshAllUI(); // ʹ�����RefreshAllUI����
                LogDebug("������RoomUIControllerǿ��ˢ��");
            }
            else
            {
                LogDebug("RoomUIController�����ڣ��޷�ִ��ˢ��");
            }
        }

        #endregion

        #region IInRoomCallbacksʵ�� - ��С������

        void IInRoomCallbacks.OnPlayerEnteredRoom(Player newPlayer)
        {
            LogDebug($"Photon: ��Ҽ��뷿�� - {newPlayer.NickName} (ID: {newPlayer.ActorNumber})");
            // UI���½���RoomUIController����
        }

        void IInRoomCallbacks.OnPlayerLeftRoom(Player otherPlayer)
        {
            LogDebug($"Photon: ����뿪���� - {otherPlayer.NickName} (ID: {otherPlayer.ActorNumber})");
            // UI���½���RoomUIController����
        }

        void IInRoomCallbacks.OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
        {
            // ׼��״̬�������RoomManager��RoomUIController����
        }

        void IInRoomCallbacks.OnMasterClientSwitched(Player newMasterClient)
        {
            LogDebug($"Photon: �����л��� {newMasterClient.NickName} (ID: {newMasterClient.ActorNumber})");

            // ǿ��ˢ��UI�Է�ӳ�µķ���״̬
            if (roomUIController != null)
            {
                roomUIController.RefreshAllUI();
            }
        }

        void IInRoomCallbacks.OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
        {
            // �������Ա����RoomManager����
        }

        #endregion

        #region Photon����״̬���

        /// <summary>
        /// ���Photon����״̬��ͨ��Update��飩
        /// </summary>
        private void Update()
        {
            if (!isInitialized) return;

            // ����Ƿ�����Ͽ�����
            if (!PhotonNetwork.IsConnected)
            {
                HandleDisconnection("���Ӷ�ʧ");
            }
            // ����Ƿ������뿪����
            else if (!PhotonNetwork.InRoom && !isLeavingRoom)
            {
                HandleRoomLeft("�����뿪����");
            }
        }

        /// <summary>
        /// �������ӶϿ�
        /// </summary>
        private void HandleDisconnection(string reason)
        {
            if (isLeavingRoom) return;

            LogDebug($"��⵽���ӶϿ�: {reason}");
            ShowError($"�������ӶϿ�: {reason}");
            Invoke(nameof(ReturnToLobbyDelayed), 3f);
        }

        /// <summary>
        /// �����뿪����
        /// </summary>
        private void HandleRoomLeft(string reason)
        {
            LogDebug($"��⵽�뿪����: {reason}");

            // �������ش����¼�
            OnReturnToLobby();
        }

        #endregion

        #region ��������

        /// <summary>
        /// �ӳٷ��ش���
        /// </summary>
        private void ReturnToLobbyDelayed()
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
                roomUIController.RefreshAllUI(); // ʹ����ȷ�ķ�����
            }
            else
            {
                LogDebug("�޷�ˢ��UI��RoomUIControllerδ����");
            }
        }

        /// <summary>
        /// ��ȡUI״̬��Ϣ - ������ļܹ�
        /// </summary>
        public string GetUIStatusInfo()
        {
            if (roomUIController != null)
            {
                return roomUIController.GetUIStatusInfo(); // ʹ����ķ���
            }
            return "RoomUIController: δ����";
        }

        /// <summary>
        /// ��ȡ������ϸ��Ϣ - ʹ�����RoomManager
        /// </summary>
        public string GetDetailedDebugInfo()
        {
            if (RoomManager.Instance != null)
            {
                return RoomManager.Instance.GetRoomStatusInfo() + "\n" +
                       RoomManager.Instance.GetPlayerListInfo();
            }
            return "RoomManager: δ��ʼ��";
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

        #endregion

        #region ���Է��� - ������ļܹ�

        /// <summary>
        /// ��ʾ������ϸ��Ϣ
        /// </summary>
        [ContextMenu("��ʾ������ϸ��Ϣ")]
        public void ShowRoomDetailedInfo()
        {
            Debug.Log("=== ������ϸ��Ϣ ===\n" + GetDetailedDebugInfo());
        }

        /// <summary>
        /// ��ʾUI������״̬
        /// </summary>
        [ContextMenu("��ʾUI������״̬")]
        public void ShowUIControllerStatus()
        {
            Debug.Log($"=== UI������״̬ ===\n{GetUIStatusInfo()}");
        }

        /// <summary>
        /// ���Կ�ʼ��Ϸ
        /// </summary>
        [ContextMenu("���Կ�ʼ��Ϸ")]
        public void TestStartGame()
        {
            if (Application.isPlaying && RoomManager.Instance?.IsHost == true)
            {
                OnStartGameButtonClicked();
            }
            else
            {
                Debug.Log("��Ҫ����Ϸ����ʱ��Ϊ�������ܲ���");
            }
        }

        /// <summary>
        /// ����׼��״̬�л�
        /// </summary>
        [ContextMenu("����׼��״̬�л�")]
        public void TestReadyToggle()
        {
            if (Application.isPlaying && RoomManager.Instance?.IsHost == false)
            {
                OnReadyButtonClicked();
            }
            else
            {
                Debug.Log("��Ҫ����Ϸ����ʱ��Ϊ�Ƿ������ܲ���");
            }
        }

        /// <summary>
        /// ����ǿ��ˢ��UI
        /// </summary>
        [ContextMenu("����ǿ��ˢ��UI")]
        public void TestForceRefreshUI()
        {
            if (Application.isPlaying)
            {
                ForceRefreshUI();
            }
        }

        /// <summary>
        /// ��ʾSceneTransitionManager״̬
        /// </summary>
        [ContextMenu("��ʾ�����л�״̬")]
        public void ShowSceneTransitionStatus()
        {
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.ShowTransitionStatus();
            }
            else
            {
                Debug.Log("SceneTransitionManagerʵ��������");
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