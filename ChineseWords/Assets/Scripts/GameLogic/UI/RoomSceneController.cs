using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
using Core.Network;

namespace UI
{
    /// <summary>
    /// ���䳡��������
    /// ������UI��ʾ���û�����
    /// </summary>
    public class RoomSceneController : MonoBehaviour
    {
        [Header("������ϢUI")]
        [SerializeField] private TMP_Text roomNameText;
        [SerializeField] private TMP_Text roomCodeText;
        [SerializeField] private TMP_Text playerCountText;
        [SerializeField] private TMP_Text roomStatusText;

        [Header("����б�")]
        [SerializeField] private Transform playerListParent;
        [SerializeField] private GameObject playerItemPrefab;
        [SerializeField] private ScrollRect playerListScrollRect;

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
        [SerializeField] private float uiUpdateInterval = 0.5f;

        // ����б�UI��
        private Dictionary<ushort, GameObject> playerListItems = new Dictionary<ushort, GameObject>();

        // ״̬����
        private bool isInitialized = false;
        private float lastUIUpdateTime = 0f;

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

            // ��ʼ��UI״̬
            InitializeUIState();

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
            RoomManager.OnRoomCreated += OnRoomCreated;
            RoomManager.OnRoomJoined += OnRoomJoined;
            RoomManager.OnPlayerJoinedRoom += OnPlayerJoinedRoom;
            RoomManager.OnPlayerLeftRoom += OnPlayerLeftRoom;
            RoomManager.OnPlayerReadyChanged += OnPlayerReadyChanged;
            RoomManager.OnGameStarting += OnGameStarting;
            RoomManager.OnRoomLeft += OnRoomLeft;

            // ���������¼�
            NetworkManager.OnDisconnected += OnNetworkDisconnected;
        }

        /// <summary>
        /// ȡ�����ķ����¼�
        /// </summary>
        private void UnsubscribeFromRoomEvents()
        {
            RoomManager.OnRoomCreated -= OnRoomCreated;
            RoomManager.OnRoomJoined -= OnRoomJoined;
            RoomManager.OnPlayerJoinedRoom -= OnPlayerJoinedRoom;
            RoomManager.OnPlayerLeftRoom -= OnPlayerLeftRoom;
            RoomManager.OnPlayerReadyChanged -= OnPlayerReadyChanged;
            RoomManager.OnGameStarting -= OnGameStarting;
            RoomManager.OnRoomLeft -= OnRoomLeft;

            NetworkManager.OnDisconnected -= OnNetworkDisconnected;
        }

        private void Update()
        {
            // ���ڸ���UI״̬
            if (isInitialized && Time.time - lastUIUpdateTime > uiUpdateInterval)
            {
                UpdateUIState();
                lastUIUpdateTime = Time.time;
            }
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

        #region UI״̬����

        /// <summary>
        /// ��ʼ��UI״̬
        /// </summary>
        private void InitializeUIState()
        {
            if (RoomManager.Instance?.CurrentRoom != null)
            {
                UpdateRoomInfo(RoomManager.Instance.CurrentRoom);
                UpdatePlayerList();
            }

            UpdateButtonStates();
        }

        /// <summary>
        /// ����UI״̬
        /// </summary>
        private void UpdateUIState()
        {
            if (!isInitialized || RoomManager.Instance?.CurrentRoom == null)
                return;

            UpdateRoomInfo(RoomManager.Instance.CurrentRoom);
            UpdateButtonStates();
        }

        /// <summary>
        /// ���·�����Ϣ��ʾ
        /// </summary>
        private void UpdateRoomInfo(RoomData roomData)
        {
            if (roomNameText != null)
                roomNameText.text = $"����: {roomData.roomName}";

            if (roomCodeText != null)
                roomCodeText.text = $"�������: {roomData.roomCode}";

            if (playerCountText != null)
                playerCountText.text = $"���: {roomData.players.Count}/{roomData.maxPlayers}";

            if (roomStatusText != null)
            {
                string statusText = GetRoomStatusText(roomData);
                roomStatusText.text = statusText;
            }
        }

        /// <summary>
        /// ��ȡ����״̬�ı�
        /// </summary>
        private string GetRoomStatusText(RoomData roomData)
        {
            switch (roomData.state)
            {
                case RoomState.Waiting:
                    // ��ʾ׼��״̬�����ų�����
                    int readyCount = roomData.GetReadyPlayerCount();
                    int nonHostCount = roomData.GetNonHostPlayerCount();
                    if (nonHostCount == 0)
                        return "�ȴ���Ҽ���";
                    else
                        return $"׼��״̬: {readyCount}/{nonHostCount}";
                case RoomState.Ready:
                    return "׼����ʼ";
                case RoomState.Starting:
                    return "��Ϸ������...";
                case RoomState.InGame:
                    return "��Ϸ������";
                case RoomState.Ended:
                    return "��Ϸ�ѽ���";
                default:
                    return "δ֪״̬";
            }
        }

        /// <summary>
        /// ���°�ť״̬
        /// </summary>
        private void UpdateButtonStates()
        {
            bool isHost = RoomManager.Instance?.IsHost ?? false;
            bool canStartGame = RoomManager.Instance?.CanStartGame() ?? false;
            bool isReady = RoomManager.Instance?.GetMyReadyState() ?? false;

            // ׼����ť��ֻ�пͻ�����ʾ��
            if (readyButton != null)
            {
                readyButton.gameObject.SetActive(!isHost);
                if (!isHost)
                {
                    var buttonText = readyButton.GetComponentInChildren<TMP_Text>();
                    if (buttonText != null)
                        buttonText.text = isReady ? "ȡ��׼��" : "׼��";
                }
            }

            // ��ʼ��Ϸ��ť��ֻ�з�����ʾ��
            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(isHost);
                if (isHost)
                {
                    startGameButton.interactable = canStartGame;
                    var buttonText = startGameButton.GetComponentInChildren<TMP_Text>();
                    if (buttonText != null)
                    {
                        var room = RoomManager.Instance?.CurrentRoom;
                        if (room == null)
                        {
                            buttonText.text = "�ȴ���������";
                        }
                        else if (room.GetNonHostPlayerCount() == 0)
                        {
                            buttonText.text = "�ȴ���Ҽ���";
                        }
                        else if (!room.AreAllPlayersReady())
                        {
                            int readyCount = room.GetReadyPlayerCount();
                            int totalNonHost = room.GetNonHostPlayerCount();
                            buttonText.text = $"�ȴ�׼�� ({readyCount}/{totalNonHost})";
                        }
                        else
                        {
                            buttonText.text = "��ʼ��Ϸ";
                        }
                    }
                }
            }
        }

        /// <summary>
        /// ��������б�
        /// </summary>
        private void UpdatePlayerList()
        {
            if (RoomManager.Instance?.CurrentRoom == null)
                return;

            // ��������б�
            ClearPlayerList();

            // ����������
            foreach (var player in RoomManager.Instance.CurrentRoom.players.Values)
            {
                AddPlayerToList(player);
            }
        }

        /// <summary>
        /// �����ҵ��б�
        /// </summary>
        private void AddPlayerToList(RoomPlayer player)
        {
            if (playerItemPrefab == null || playerListParent == null)
                return;

            if (playerListItems.ContainsKey(player.playerId))
                return;

            GameObject playerItem = Instantiate(playerItemPrefab, playerListParent);
            playerListItems[player.playerId] = playerItem;

            UpdatePlayerItemInfo(playerItem, player);
        }

        /// <summary>
        /// �Ƴ���Ҵ��б�
        /// </summary>
        private void RemovePlayerFromList(ushort playerId)
        {
            if (playerListItems.ContainsKey(playerId))
            {
                Destroy(playerListItems[playerId]);
                playerListItems.Remove(playerId);
            }
        }

        /// <summary>
        /// �������б�
        /// </summary>
        private void ClearPlayerList()
        {
            foreach (var item in playerListItems.Values)
            {
                if (item != null)
                    Destroy(item);
            }
            playerListItems.Clear();
        }

        /// <summary>
        /// �����������Ϣ
        /// </summary>
        private void UpdatePlayerItemInfo(GameObject playerItem, RoomPlayer player)
        {
            var nameText = playerItem.GetComponentInChildren<TMP_Text>();
            if (nameText != null)
            {
                string statusText = "";
                if (player.isHost)
                    statusText = " (����)";
                else if (player.state == PlayerRoomState.Ready)
                    statusText = " (��׼��)";
                else
                    statusText = " (δ׼��)";

                nameText.text = player.playerName + statusText;

                // ������ɫ
                if (player.isHost)
                    nameText.color = Color.yellow;
                else if (player.state == PlayerRoomState.Ready)
                    nameText.color = Color.green;
                else
                    nameText.color = Color.white;
            }
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

        private void OnRoomCreated(RoomData roomData)
        {
            LogDebug("���䴴���¼�");
            UpdateRoomInfo(roomData);
            UpdatePlayerList();
        }

        private void OnRoomJoined(RoomData roomData)
        {
            LogDebug("��������¼�");
            UpdateRoomInfo(roomData);
            UpdatePlayerList();
        }

        private void OnPlayerJoinedRoom(RoomPlayer player)
        {
            LogDebug($"��Ҽ���: {player.playerName}");
            AddPlayerToList(player);
        }

        private void OnPlayerLeftRoom(ushort playerId)
        {
            LogDebug($"����뿪: {playerId}");
            RemovePlayerFromList(playerId);
        }

        private void OnPlayerReadyChanged(ushort playerId, bool isReady)
        {
            LogDebug($"��� {playerId} ׼��״̬: {isReady}");

            if (playerListItems.ContainsKey(playerId) &&
                RoomManager.Instance?.CurrentRoom?.players.ContainsKey(playerId) == true)
            {
                var player = RoomManager.Instance.CurrentRoom.players[playerId];
                UpdatePlayerItemInfo(playerListItems[playerId], player);
            }
        }

        private void OnGameStarting()
        {
            LogDebug("��Ϸ��ʼ");
            ShowLoadingPanel("��Ϸ�����У����Ժ�...");

            // ����HostGameManager��ʼ��Ϸ����Host���ã�
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

            // �ӳ��л������������һЩ��Ӧʱ��
            Invoke(nameof(SwitchToGameScene), 2f);
        }

        private void OnRoomLeft()
        {
            LogDebug("�뿪����");
            ReturnToMainMenu();
        }

        private void OnNetworkDisconnected()
        {
            LogDebug("����Ͽ�����");
            ShowError("�������ӶϿ������������˵�");
            Invoke(nameof(ReturnToMainMenu), 3f);
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

            ReturnToMainMenu();
        }

        /// <summary>
        /// ˢ�°�ť���
        /// </summary>
        private void OnRefreshButtonClicked()
        {
            LogDebug("ˢ�·���״̬");

            if (RoomManager.Instance?.CurrentRoom != null)
            {
                UpdateRoomInfo(RoomManager.Instance.CurrentRoom);
                UpdatePlayerList();
                UpdateButtonStates();
            }
        }

        #endregion

        #region ��������

        /// <summary>
        /// �л�����Ϸ����
        /// </summary>
        private void SwitchToGameScene()
        {
            LogDebug("�л�����Ϸ����");
            SceneManager.LoadScene("NetworkGameScene");
        }

        /// <summary>
        /// �������˵�
        /// </summary>
        private void ReturnToMainMenu()
        {
            LogDebug("�������˵�");

            // ������������
            if (NetworkManager.Instance != null)
                NetworkManager.Instance.Disconnect();

            SceneManager.LoadScene("MainMenuScene");
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
                // ������������ͣʱ�Ĵ����߼�
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && isInitialized)
            {
                LogDebug("Ӧ��ʧȥ����");
                // ����������ʧȥ����ʱ�Ĵ����߼�
            }
        }

        #endregion
    }
}