using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using Core.Network;
using Photon.Pun;
using Photon.Realtime;

namespace UI
{
    /// <summary>
    /// ������������ - Photon�Ż���
    /// רע��RoomScene��UI����ʹ�ô��¼���������
    /// �Ƴ��Զ�ˢ�»��ƣ���ȫ����Photon�Ŀɿ��¼�ͬ��
    /// </summary>
    public class RoomUIController : MonoBehaviour
    {
        [Header("������ϢUI")]
        [SerializeField] private TMP_Text roomNameText;
        [SerializeField] private TMP_Text roomCodeText;
        [SerializeField] private TMP_Text roomStatusText;
        [SerializeField] private TMP_Text playerCountText;

        [Header("����б�UI")]
        [SerializeField] private Transform playerListParent;
        [SerializeField] private GameObject playerItemPrefab;

        [Header("��Ϸ����UI")]
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button readyButton;
        [SerializeField] private TMP_Text readyButtonText;
        [SerializeField] private Button leaveRoomButton;

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private Button debugRefreshButton;

        // ���UI���� - ʹ��Photon��ActorNumber��Ϊ��
        private Dictionary<int, GameObject> playerUIItems = new Dictionary<int, GameObject>();

        // ��ʼ��״̬
        private bool isInitialized = false;

        private void Start()
        {
            StartCoroutine(InitializeUIController());
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            ClearPlayerUIItems();
        }

        #region ��ʼ��

        /// <summary>
        /// ��ʼ��UI������
        /// </summary>
        private IEnumerator InitializeUIController()
        {
            LogDebug("��ʼ��ʼ��RoomUIController");

            // �ȴ�RoomManager��ʼ��
            while (RoomManager.Instance == null || !RoomManager.Instance.IsInitialized)
            {
                yield return new WaitForSeconds(0.1f);
            }

            // �ȴ�ȷ���ڷ�����
            while (!PhotonNetwork.InRoom)
            {
                LogDebug("�ȴ�����Photon����...");
                yield return new WaitForSeconds(0.1f);
            }

            // ��ʼ��UI���
            InitializeUIComponents();

            // �����¼�
            SubscribeToEvents();

            // ����ˢ��һ��UI
            RefreshAllUI();

            isInitialized = true;
            LogDebug("RoomUIController��ʼ�����");
        }

        /// <summary>
        /// ��ʼ��UI���
        /// </summary>
        private void InitializeUIComponents()
        {
            // ��׼����ť
            if (readyButton != null)
            {
                readyButton.onClick.RemoveAllListeners();
                readyButton.onClick.AddListener(OnReadyButtonClicked);
            }

            // �󶨿�ʼ��Ϸ��ť
            if (startGameButton != null)
            {
                startGameButton.onClick.RemoveAllListeners();
                startGameButton.onClick.AddListener(OnStartGameButtonClicked);
            }

            // ���뿪���䰴ť
            if (leaveRoomButton != null)
            {
                leaveRoomButton.onClick.RemoveAllListeners();
                leaveRoomButton.onClick.AddListener(OnLeaveRoomButtonClicked);
            }

            // �󶨵���ˢ�°�ť
            if (debugRefreshButton != null)
            {
                debugRefreshButton.onClick.RemoveAllListeners();
                debugRefreshButton.onClick.AddListener(RefreshAllUI);
                debugRefreshButton.gameObject.SetActive(enableDebugLogs);
            }

            LogDebug("UI�����ʼ�����");
        }

        /// <summary>
        /// ����RoomManager�¼� - Photon�汾
        /// </summary>
        private void SubscribeToEvents()
        {
            if (RoomManager.Instance != null)
            {
                RoomManager.OnRoomEntered += OnRoomEntered;
                RoomManager.OnPlayerJoinedRoom += OnPlayerJoinedRoom;
                RoomManager.OnPlayerLeftRoom += OnPlayerLeftRoom;
                RoomManager.OnPlayerReadyChanged += OnPlayerReadyChanged;
                RoomManager.OnAllPlayersReady += OnAllPlayersReady;
                RoomManager.OnGameStarting += OnGameStarting;
                RoomManager.OnReturnToLobby += OnReturnToLobby;

                LogDebug("�Ѷ���RoomManager�¼�");
            }
            else
            {
                Debug.LogError("[RoomUIController] RoomManagerʵ��������");
            }
        }

        /// <summary>
        /// ȡ�������¼�
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (RoomManager.Instance != null)
            {
                RoomManager.OnRoomEntered -= OnRoomEntered;
                RoomManager.OnPlayerJoinedRoom -= OnPlayerJoinedRoom;
                RoomManager.OnPlayerLeftRoom -= OnPlayerLeftRoom;
                RoomManager.OnPlayerReadyChanged -= OnPlayerReadyChanged;
                RoomManager.OnAllPlayersReady -= OnAllPlayersReady;
                RoomManager.OnGameStarting -= OnGameStarting;
                RoomManager.OnReturnToLobby -= OnReturnToLobby;

                LogDebug("��ȡ������RoomManager�¼�");
            }
        }

        #endregion

        #region �¼����� - Photon�����

        /// <summary>
        /// ��������¼�����
        /// </summary>
        private void OnRoomEntered()
        {
            LogDebug("�յ���������¼�");
            RefreshAllUI();
        }

        /// <summary>
        /// ��Ҽ��뷿���¼����� - ʹ��Photon Player����
        /// </summary>
        private void OnPlayerJoinedRoom(Player player)
        {
            LogDebug($"��Ҽ���: {player.NickName} (ID: {player.ActorNumber})");
            CreateOrUpdatePlayerUI(player);
            RefreshRoomInfo();
            RefreshGameControls();
        }

        /// <summary>
        /// ����뿪�����¼����� - ʹ��Photon Player����
        /// </summary>
        private void OnPlayerLeftRoom(Player player)
        {
            LogDebug($"����뿪: {player.NickName} (ID: {player.ActorNumber})");
            RemovePlayerUI(player.ActorNumber);
            RefreshRoomInfo();
            RefreshGameControls();
        }

        /// <summary>
        /// ���׼��״̬�仯�¼����� - ʹ��Photon Player����
        /// </summary>
        private void OnPlayerReadyChanged(Player player, bool isReady)
        {
            LogDebug($"���׼��״̬�仯: {player.NickName} -> {isReady}");
            UpdatePlayerReadyState(player);
            RefreshGameControls();
        }

        /// <summary>
        /// �������׼�������¼�����
        /// </summary>
        private void OnAllPlayersReady()
        {
            LogDebug("������Ҷ���׼������");
            RefreshGameControls();

            // ���������������Ч����ʾ
            ShowAllReadyNotification();
        }

        /// <summary>
        /// ��Ϸ��ʼ�¼�����
        /// </summary>
        private void OnGameStarting()
        {
            LogDebug("��Ϸ������ʼ");
            RefreshGameControls();

            // ���������������Ϸ��ʼ����ʱUI
            ShowGameStartingNotification();
        }

        /// <summary>
        /// ���ش����¼�����
        /// </summary>
        private void OnReturnToLobby()
        {
            LogDebug("���ش���");
            ClearAllUI();
        }

        #endregion

        #region UIˢ�·��� - �򻯰�

        /// <summary>
        /// ˢ������UI - �򻯵��¼������汾
        /// </summary>
        public void RefreshAllUI()
        {
            if (!PhotonNetwork.InRoom)
            {
                LogDebug("���ڷ����У����UI");
                ClearAllUI();
                return;
            }

            LogDebug("ˢ������UI");
            RefreshRoomInfo();
            RefreshPlayerList();
            RefreshGameControls();
        }

        /// <summary>
        /// ˢ�·�����Ϣ - ֱ��ʹ��Photon����
        /// </summary>
        private void RefreshRoomInfo()
        {
            if (!PhotonNetwork.InRoom) return;

            var room = PhotonNetwork.CurrentRoom;

            // ���·�������
            if (roomNameText != null)
            {
                roomNameText.text = $"����: {room.Name}";
            }

            // ���·�����루�ӷ������Ի�ȡ��
            if (roomCodeText != null)
            {
                string roomCode = GetRoomCode();
                roomCodeText.text = string.IsNullOrEmpty(roomCode) ? "�������: ��" : $"�������: {roomCode}";
            }

            // ���·���״̬
            if (roomStatusText != null)
            {
                var roomState = GetRoomState();
                roomStatusText.text = $"״̬: {GetRoomStateDisplayText(roomState)}";
            }

            // �������������׼��״̬
            if (playerCountText != null)
            {
                int totalPlayers = PhotonNetwork.PlayerList.Length;
                int maxPlayers = room.MaxPlayers;
                int readyCount = GetReadyPlayerCount();
                int nonHostCount = GetNonHostPlayerCount();

                playerCountText.text = $"���: {totalPlayers}/{maxPlayers} (׼��: {readyCount}/{nonHostCount})";
            }

            LogDebug($"������Ϣ�Ѹ���: {room.Name}, �����: {PhotonNetwork.PlayerList.Length}");
        }

        /// <summary>
        /// ˢ������б� - �ؽ��������UI
        /// </summary>
        private void RefreshPlayerList()
        {
            if (!PhotonNetwork.InRoom || playerListParent == null) return;

            LogDebug("ˢ������б�");

            // �������UI���򻯷�����
            ClearPlayerUIItems();

            // ���´����������UI
            foreach (var player in PhotonNetwork.PlayerList)
            {
                CreateOrUpdatePlayerUI(player);
            }

            LogDebug($"����б�ˢ����ɣ���ǰ�����: {PhotonNetwork.PlayerList.Length}");
        }

        /// <summary>
        /// ������������UI
        /// </summary>
        private void CreateOrUpdatePlayerUI(Player player)
        {
            if (playerItemPrefab == null || playerListParent == null) return;

            GameObject playerItem;

            // ����Ƿ��Ѵ���
            if (playerUIItems.ContainsKey(player.ActorNumber))
            {
                playerItem = playerUIItems[player.ActorNumber];
                if (playerItem == null)
                {
                    // UI�����������٣����´���
                    playerUIItems.Remove(player.ActorNumber);
                    playerItem = CreatePlayerUIItem(player);
                }
            }
            else
            {
                playerItem = CreatePlayerUIItem(player);
            }

            // ����UI����
            if (playerItem != null)
            {
                UpdatePlayerUIContent(playerItem, player);
            }
        }

        /// <summary>
        /// �������UI��
        /// </summary>
        private GameObject CreatePlayerUIItem(Player player)
        {
            GameObject item = Instantiate(playerItemPrefab, playerListParent);
            item.name = $"Player_{player.ActorNumber}_{player.NickName}";
            playerUIItems[player.ActorNumber] = item;

            LogDebug($"�������UI: {player.NickName} (ID: {player.ActorNumber})");
            return item;
        }

        /// <summary>
        /// �������UI����
        /// </summary>
        private void UpdatePlayerUIContent(GameObject playerItem, Player player)
        {
            if (playerItem == null) return;

            // �����������
            var nameText = playerItem.GetComponentInChildren<TMP_Text>();
            if (nameText != null)
            {
                string displayName = player.NickName;
                if (player.IsMasterClient)
                {
                    displayName += " (����)";
                }
                nameText.text = displayName;
            }

            // ����׼��״̬��ʾ
            var statusTexts = playerItem.GetComponentsInChildren<TMP_Text>();
            TMP_Text statusText = null;

            // ����״̬�ı���ͨ���ǵڶ���Text�������ΪStatusText�������
            foreach (var text in statusTexts)
            {
                if (text != nameText && (text.name.Contains("Status") || statusTexts.Length > 1))
                {
                    statusText = text;
                    break;
                }
            }

            if (statusText != null)
            {
                bool isReady = GetPlayerReady(player);
                bool isHost = player.IsMasterClient;

                if (isHost)
                {
                    statusText.text = "����";
                    statusText.color = Color.yellow;
                }
                else
                {
                    statusText.text = isReady ? "��׼��" : "δ׼��";
                    statusText.color = isReady ? Color.green : Color.red;
                }
            }

            // ���±�����ɫ
            var backgroundImage = playerItem.GetComponent<Image>();
            if (backgroundImage != null)
            {
                if (player.IsMasterClient)
                {
                    backgroundImage.color = new Color(1f, 0.8f, 0.2f, 0.3f); // ������ɫ����
                }
                else
                {
                    bool isReady = GetPlayerReady(player);
                    backgroundImage.color = isReady ?
                        new Color(0.2f, 1f, 0.2f, 0.2f) : // ׼��������ɫ
                        new Color(1f, 0.2f, 0.2f, 0.2f);  // δ׼��������ɫ
                }
            }
        }

        /// <summary>
        /// �����ض���ҵ�׼��״̬UI
        /// </summary>
        private void UpdatePlayerReadyState(Player player)
        {
            if (playerUIItems.ContainsKey(player.ActorNumber))
            {
                var playerItem = playerUIItems[player.ActorNumber];
                if (playerItem != null)
                {
                    UpdatePlayerUIContent(playerItem, player);
                }
            }
        }

        /// <summary>
        /// �Ƴ����UI
        /// </summary>
        private void RemovePlayerUI(int actorNumber)
        {
            if (playerUIItems.ContainsKey(actorNumber))
            {
                var item = playerUIItems[actorNumber];
                if (item != null)
                {
                    Destroy(item);
                }
                playerUIItems.Remove(actorNumber);

                LogDebug($"�Ƴ����UI: ActorNumber {actorNumber}");
            }
        }

        /// <summary>
        /// ˢ����Ϸ���ư�ť
        /// </summary>
        private void RefreshGameControls()
        {
            if (!PhotonNetwork.InRoom) return;

            bool isHost = PhotonNetwork.IsMasterClient;
            bool canStartGame = RoomManager.Instance?.CanStartGame() ?? false;
            bool myReadyState = GetMyReadyState();

            // ���¿�ʼ��Ϸ��ť
            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(isHost);
                startGameButton.interactable = canStartGame;

                var buttonText = startGameButton.GetComponentInChildren<TMP_Text>();
                if (buttonText != null)
                {
                    if (canStartGame)
                    {
                        buttonText.text = "��ʼ��Ϸ";
                    }
                    else
                    {
                        string condition = RoomManager.Instance?.GetGameStartConditions() ?? "�����...";
                        buttonText.text = $"�޷���ʼ: {condition}";
                    }
                }
            }

            // ����׼����ť
            if (readyButton != null)
            {
                readyButton.gameObject.SetActive(!isHost);
                readyButton.interactable = !GetGameStarted(); // ��Ϸ��ʼ�����

                if (readyButtonText != null)
                {
                    readyButtonText.text = myReadyState ? "ȡ��׼��" : "׼��";
                }
            }

            // �����뿪���䰴ť
            if (leaveRoomButton != null)
            {
                leaveRoomButton.interactable = !GetGameStarted(); // ��Ϸ��ʼ����ܽ���
            }
        }

        /// <summary>
        /// �������UI
        /// </summary>
        private void ClearAllUI()
        {
            LogDebug("�������UI");

            // ��շ�����Ϣ
            if (roomNameText != null) roomNameText.text = "����: δ����";
            if (roomCodeText != null) roomCodeText.text = "�������: ��";
            if (roomStatusText != null) roomStatusText.text = "״̬: ����";
            if (playerCountText != null) playerCountText.text = "���: 0/0";

            // �������б�
            ClearPlayerUIItems();

            // ���ؿ��ư�ť
            if (startGameButton != null) startGameButton.gameObject.SetActive(false);
            if (readyButton != null) readyButton.gameObject.SetActive(false);
        }

        /// <summary>
        /// ������UI��
        /// </summary>
        private void ClearPlayerUIItems()
        {
            foreach (var item in playerUIItems.Values)
            {
                if (item != null)
                {
                    Destroy(item);
                }
            }
            playerUIItems.Clear();
        }

        #endregion

        #region ��ť�¼�����

        /// <summary>
        /// ׼����ť���
        /// </summary>
        private void OnReadyButtonClicked()
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient)
            {
                LogDebug("��������Ҫ׼����δ�ڷ�����");
                return;
            }

            bool currentState = GetMyReadyState();
            bool newState = !currentState;

            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.SetPlayerReady(newState);
                LogDebug($"����׼��״̬: {currentState} -> {newState}");
            }
        }

        /// <summary>
        /// ��ʼ��Ϸ��ť���
        /// </summary>
        private void OnStartGameButtonClicked()
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                LogDebug("ֻ�з������Կ�ʼ��Ϸ");
                return;
            }

            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.StartGame();
                LogDebug("���������ʼ��Ϸ");
            }
        }

        /// <summary>
        /// �뿪���䰴ť���
        /// </summary>
        private void OnLeaveRoomButtonClicked()
        {
            LogDebug("����뿪����");

            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.LeaveRoomAndReturnToLobby();
            }
            else
            {
                // ���÷�����ֱ���뿪Photon����
                PhotonNetwork.LeaveRoom();
            }
        }

        #endregion

        #region Photon���ݻ�ȡ����

        /// <summary>
        /// ��ȡ�������
        /// </summary>
        private string GetRoomCode()
        {
            if (!PhotonNetwork.InRoom) return "";

            var room = PhotonNetwork.CurrentRoom;
            if (room.CustomProperties.TryGetValue("roomCode", out object code))
            {
                return (string)code;
            }
            return "";
        }

        /// <summary>
        /// ��ȡ����״̬
        /// </summary>
        private RoomState GetRoomState()
        {
            if (RoomManager.Instance != null)
            {
                return RoomManager.Instance.GetRoomState();
            }
            return RoomState.Waiting;
        }

        /// <summary>
        /// ��ȡ��Ϸ�Ƿ��ѿ�ʼ
        /// </summary>
        private bool GetGameStarted()
        {
            if (RoomManager.Instance != null)
            {
                return RoomManager.Instance.GetGameStarted();
            }
            return false;
        }

        /// <summary>
        /// ��ȡ���׼��״̬
        /// </summary>
        private bool GetPlayerReady(Player player)
        {
            if (RoomManager.Instance != null)
            {
                return RoomManager.Instance.GetPlayerReady(player);
            }
            return false;
        }

        /// <summary>
        /// ��ȡ�������׼��״̬
        /// </summary>
        private bool GetMyReadyState()
        {
            if (RoomManager.Instance != null)
            {
                return RoomManager.Instance.GetMyReadyState();
            }
            return false;
        }

        /// <summary>
        /// ��ȡ׼���������
        /// </summary>
        private int GetReadyPlayerCount()
        {
            if (RoomManager.Instance != null)
            {
                return RoomManager.Instance.GetReadyPlayerCount();
            }
            return 0;
        }

        /// <summary>
        /// ��ȡ�Ƿ����������
        /// </summary>
        private int GetNonHostPlayerCount()
        {
            if (RoomManager.Instance != null)
            {
                return RoomManager.Instance.GetNonHostPlayerCount();
            }
            return 0;
        }

        #endregion

        #region UIЧ����֪ͨ

        /// <summary>
        /// ��ʾ�������׼������֪ͨ
        /// </summary>
        private void ShowAllReadyNotification()
        {
            // ���������������Ч����Ч��UI��ʾ
            LogDebug("�������׼������ - ���������Ч");
        }

        /// <summary>
        /// ��ʾ��Ϸ��ʼ֪ͨ
        /// </summary>
        private void ShowGameStartingNotification()
        {
            // ������������ӵ���ʱUI����Ч
            LogDebug("��Ϸ������ʼ - ������ӵ���ʱ");
        }

        #endregion

        #region ��������

        /// <summary>
        /// ��ȡ����״̬��ʾ�ı�
        /// </summary>
        private string GetRoomStateDisplayText(RoomState state)
        {
            switch (state)
            {
                case RoomState.Waiting: return "�ȴ���";
                case RoomState.Starting: return "��ʼ��";
                case RoomState.InGame: return "��Ϸ��";
                case RoomState.Ended: return "�ѽ���";
                default: return "δ֪";
            }
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[RoomUIController] {message}");
            }
        }

        #endregion

        #region �����ӿں͵���

        /// <summary>
        /// ��ȡUI״̬��Ϣ
        /// </summary>
        public string GetUIStatusInfo()
        {
            return $"��ʼ��: {isInitialized}, " +
                   $"�ڷ�����: {PhotonNetwork.InRoom}, " +
                   $"���UI����: {playerUIItems.Count}, " +
                   $"������: {PhotonNetwork.CurrentRoom?.Name ?? "��"}";
        }

        /// <summary>
        /// �ֶ�����UIˢ�£������ã�
        /// </summary>
        public void ManualRefresh()
        {
            LogDebug("�ֶ�����UIˢ��");
            RefreshAllUI();
        }

        #endregion

        #region ���Է���

        [ContextMenu("ˢ��UI")]
        public void DebugRefreshUI()
        {
            if (Application.isPlaying)
            {
                RefreshAllUI();
            }
        }

        [ContextMenu("��ʾUI״̬")]
        public void DebugShowUIStatus()
        {
            if (Application.isPlaying)
            {
                Debug.Log($"=== RoomUIController״̬ ===\n{GetUIStatusInfo()}");

                if (RoomManager.Instance != null)
                {
                    Debug.Log($"RoomManager״̬: {RoomManager.Instance.GetRoomStatusInfo()}");
                }
            }
        }

        [ContextMenu("��ʾ����б�")]
        public void DebugShowPlayerList()
        {
            if (Application.isPlaying && PhotonNetwork.InRoom)
            {
                Debug.Log("=== ��ǰ����б� ===");
                foreach (var player in PhotonNetwork.PlayerList)
                {
                    bool isReady = GetPlayerReady(player);
                    Debug.Log($"- {player.NickName} (ID: {player.ActorNumber}) " +
                             $"[{(player.IsMasterClient ? "����" : "���")}] " +
                             $"[{(isReady ? "��׼��" : "δ׼��")}]");
                }
            }
        }

        [ContextMenu("�л�׼��״̬")]
        public void DebugToggleReady()
        {
            if (Application.isPlaying)
            {
                OnReadyButtonClicked();
            }
        }

        [ContextMenu("���´�������б�")]
        public void DebugRecreatePlayerList()
        {
            if (Application.isPlaying)
            {
                LogDebug("���´�������б�");
                RefreshPlayerList();
            }
        }

        #endregion
    }
}