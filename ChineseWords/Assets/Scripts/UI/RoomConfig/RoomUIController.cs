using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using Core.Network;
using Photon.Realtime;

namespace UI
{
    /// <summary>
    /// ������������ - �򻯽����
    /// ְ��UI��ʾ���¡��û���������
    /// ͨ��RoomManager��NetworkManager��ȡ���ݣ���ȫ�¼�����
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
        [SerializeField] private Button actionButton; // �ϲ����׼��/��ʼ��Ϸ��ť
        [SerializeField] private Image actionButtonIcon; // ��ť�������ͼ��Image
        [SerializeField] private Button leaveRoomButton;

        [Header("��ťͼ������")]
        [SerializeField] private Sprite readySprite;
        [SerializeField] private Sprite cancelReadySprite;
        [SerializeField] private Sprite startGameSprite;

        [Header("UIˢ������")]
        [SerializeField] private float autoRefreshInterval = 3f;
        [SerializeField] private bool enableAutoRefresh = true;

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        // ���UI����
        private Dictionary<int, GameObject> playerUIItems = new Dictionary<int, GameObject>();

        // ״̬����
        private bool isInitialized = false;
        private Coroutine autoRefreshCoroutine;

        private void Start()
        {
            StartCoroutine(InitializeUIController());
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            StopAutoRefresh();
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

            // �ȴ�NetworkManager׼������
            while (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected)
            {
                LogDebug("�ȴ�NetworkManager����...");
                yield return new WaitForSeconds(0.1f);
            }

            // ��ʼ��UI���
            InitializeUIComponents();

            // �����¼�
            SubscribeToEvents();

            // ����ˢ��һ��UI
            RefreshAllUI();

            // �����Զ�ˢ��
            StartAutoRefresh();

            isInitialized = true;
            LogDebug("RoomUIController��ʼ�����");
        }

        /// <summary>
        /// ��ʼ��UI���
        /// </summary>
        private void InitializeUIComponents()
        {
            // �󶨶�����ť���ϲ���׼��/��ʼ��Ϸ��ť��
            if (actionButton != null)
            {
                actionButton.onClick.RemoveAllListeners();
                actionButton.onClick.AddListener(OnActionButtonClicked);
            }

            // ���뿪���䰴ť
            if (leaveRoomButton != null)
            {
                leaveRoomButton.onClick.RemoveAllListeners();
                leaveRoomButton.onClick.AddListener(OnLeaveRoomButtonClicked);
            }

            LogDebug("UI�����ʼ�����");
        }

        /// <summary>
        /// ����RoomManager�¼�
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

            // ����NetworkManager�¼�
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnRoomStateReset += OnRoomStateReset;
                NetworkManager.OnPlayerReadyChanged += OnNetworkPlayerReadyChanged;
                LogDebug("�Ѷ���NetworkManager�¼�");
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
            }

            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnRoomStateReset -= OnRoomStateReset;
                NetworkManager.OnPlayerReadyChanged -= OnNetworkPlayerReadyChanged;
            }

            LogDebug("��ȡ�������¼�");
        }

        #endregion

        #region �Զ�ˢ�»���

        /// <summary>
        /// �����Զ�ˢ��
        /// </summary>
        private void StartAutoRefresh()
        {
            if (enableAutoRefresh && autoRefreshCoroutine == null)
            {
                autoRefreshCoroutine = StartCoroutine(AutoRefreshCoroutine());
                LogDebug($"�����Զ�ˢ�£����: {autoRefreshInterval}��");
            }
        }

        /// <summary>
        /// ֹͣ�Զ�ˢ��
        /// </summary>
        private void StopAutoRefresh()
        {
            if (autoRefreshCoroutine != null)
            {
                StopCoroutine(autoRefreshCoroutine);
                autoRefreshCoroutine = null;
                LogDebug("ֹͣ�Զ�ˢ��");
            }
        }

        /// <summary>
        /// �Զ�ˢ��Э��
        /// </summary>
        private IEnumerator AutoRefreshCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(autoRefreshInterval);

                if (isInitialized && NetworkManager.Instance?.IsConnected == true)
                {
                    RefreshAllUI();
                }
            }
        }

        #endregion

        #region �¼�����

        /// <summary>
        /// ��������¼�����
        /// </summary>
        private void OnRoomEntered()
        {
            LogDebug("�յ���������¼�");
            RefreshAllUI();
        }

        /// <summary>
        /// ��Ҽ��뷿���¼�����
        /// </summary>
        private void OnPlayerJoinedRoom(Player player)
        {
            if (player == null) return;
            LogDebug($"��Ҽ���: {player.NickName} (ID: {player.ActorNumber})");
            CreateOrUpdatePlayerUI(player);
            RefreshRoomInfo();
            RefreshActionButton();
        }

        /// <summary>
        /// ����뿪�����¼�����
        /// </summary>
        private void OnPlayerLeftRoom(Player player)
        {
            if (player != null)
            {
                LogDebug($"����뿪: {player.NickName} (ID: {player.ActorNumber})");
                RemovePlayerUI(player.ActorNumber);
            }
            RefreshRoomInfo();
            RefreshActionButton();
        }

        /// <summary>
        /// ���׼��״̬�仯�¼�����
        /// </summary>
        private void OnPlayerReadyChanged(Player player, bool isReady)
        {
            if (player == null) return;
            LogDebug($"���׼��״̬�仯: {player.NickName} -> {isReady}");
            UpdatePlayerReadyState(player);
            RefreshActionButton();
        }

        /// <summary>
        /// NetworkManager���׼��״̬�仯�¼�����
        /// </summary>
        private void OnNetworkPlayerReadyChanged(ushort playerId, bool isReady)
        {
            LogDebug($"�������׼��״̬�仯: ID {playerId} -> {isReady}");
            RefreshPlayerList();
            RefreshActionButton();
        }

        /// <summary>
        /// �������׼�������¼�����
        /// </summary>
        private void OnAllPlayersReady()
        {
            LogDebug("������Ҷ���׼������");
            RefreshActionButton();
            ShowAllReadyNotification();
        }

        /// <summary>
        /// ��Ϸ��ʼ�¼�����
        /// </summary>
        private void OnGameStarting()
        {
            LogDebug("��Ϸ������ʼ");
            RefreshActionButton();
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

        /// <summary>
        /// ����״̬�����¼�����
        /// </summary>
        private void OnRoomStateReset()
        {
            LogDebug("�յ�����״̬�����¼�");
            RefreshAllUI();
        }

        #endregion

        #region UIˢ�·���

        /// <summary>
        /// ˢ������UI
        /// </summary>
        public void RefreshAllUI()
        {
            if (NetworkManager.Instance?.IsConnected != true)
            {
                LogDebug("δ���ӷ��䣬���UI");
                ClearAllUI();
                return;
            }

            LogDebug("ˢ������UI");
            RefreshRoomInfo();
            RefreshPlayerList();
            RefreshActionButton();
        }

        /// <summary>
        /// ˢ�·�����Ϣ
        /// </summary>
        private void RefreshRoomInfo()
        {
            if (NetworkManager.Instance?.IsConnected != true) return;

            // ���·�������
            if (roomNameText != null)
            {
                roomNameText.text = $"����: {NetworkManager.Instance.RoomName}";
            }

            // ���·������
            if (roomCodeText != null)
            {
                string roomCode = NetworkManager.Instance.GetRoomProperty<string>("roomCode", "");
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
                int totalPlayers = NetworkManager.Instance.PlayerCount;
                int maxPlayers = NetworkManager.Instance.MaxPlayers;
                int readyCount = NetworkManager.Instance.GetReadyPlayerCount();
                int nonHostCount = NetworkManager.Instance.GetNonHostPlayerCount();

                playerCountText.text = $"���: {totalPlayers}/{maxPlayers} (׼��: {readyCount}/{nonHostCount})";
            }

            LogDebug($"������Ϣ�Ѹ���: {NetworkManager.Instance.RoomName}, �����: {NetworkManager.Instance.PlayerCount}");
        }

        /// <summary>
        /// ˢ������б�
        /// </summary>
        private void RefreshPlayerList()
        {
            if (NetworkManager.Instance?.IsConnected != true || playerListParent == null) return;

            LogDebug("ˢ������б�");

            // �������UI
            ClearPlayerUIItems();

            // ���´����������UI
            var playerIds = NetworkManager.Instance.GetAllOnlinePlayerIds();
            foreach (var playerId in playerIds)
            {
                CreatePlayerUIFromId(playerId);
            }

            LogDebug($"����б�ˢ����ɣ���ǰ�����: {playerIds.Count}");
        }

        /// <summary>
        /// �����ID����UI
        /// </summary>
        private void CreatePlayerUIFromId(ushort playerId)
        {
            if (playerItemPrefab == null || playerListParent == null) return;

            GameObject playerItem = Instantiate(playerItemPrefab, playerListParent);
            playerItem.name = $"Player_{playerId}";
            playerUIItems[playerId] = playerItem;

            // ����UI����
            UpdatePlayerUIFromId(playerItem, playerId);

            LogDebug($"�������UI: ID {playerId}");
        }

        /// <summary>
        /// �����ID����UI����
        /// </summary>
        private void UpdatePlayerUIFromId(GameObject playerItem, ushort playerId)
        {
            if (playerItem == null) return;

            string playerName = NetworkManager.Instance.GetPlayerName(playerId);
            bool isHost = NetworkManager.Instance.IsHostPlayer(playerId);
            bool isReady = NetworkManager.Instance.GetPlayerReady(playerId);

            // �����������
            var nameText = playerItem.GetComponentInChildren<TMP_Text>();
            if (nameText != null)
            {
                string displayName = playerName;
                if (isHost)
                {
                    displayName += " (����)";
                }
                nameText.text = displayName;
            }

            // ����׼��״̬��ʾ
            var statusTexts = playerItem.GetComponentsInChildren<TMP_Text>();
            TMP_Text statusText = null;

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
                if (isHost)
                {
                    backgroundImage.color = new Color(1f, 0.8f, 0.2f, 0.3f); // ������ɫ����
                }
                else
                {
                    backgroundImage.color = isReady ?
                        new Color(0.2f, 1f, 0.2f, 0.2f) : // ׼��������ɫ
                        new Color(1f, 0.2f, 0.2f, 0.2f);  // δ׼��������ɫ
                }
            }
        }

        /// <summary>
        /// ������������UI������Player����
        /// </summary>
        private void CreateOrUpdatePlayerUI(Player player)
        {
            if (player == null) return;
            CreatePlayerUIFromId((ushort)player.ActorNumber);
        }

        /// <summary>
        /// �����ض���ҵ�׼��״̬UI
        /// </summary>
        private void UpdatePlayerReadyState(Player player)
        {
            if (player == null) return;

            if (playerUIItems.ContainsKey(player.ActorNumber))
            {
                var playerItem = playerUIItems[player.ActorNumber];
                if (playerItem != null)
                {
                    UpdatePlayerUIFromId(playerItem, (ushort)player.ActorNumber);
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
        /// ˢ�¶�����ť���ϲ���׼��/��ʼ��Ϸ��ť��
        /// </summary>
        private void RefreshActionButton()
        {
            if (NetworkManager.Instance?.IsConnected != true || actionButton == null) return;

            bool isHost = NetworkManager.Instance.IsHost;
            bool canStartGame = RoomManager.Instance?.CanStartGame() ?? false;
            bool myReadyState = NetworkManager.Instance.GetMyReadyState();
            bool gameStarted = GetGameStarted();

            if (isHost)
            {
                // ������ʾ��ʼ��Ϸ��ť
                actionButton.gameObject.SetActive(true);
                actionButton.interactable = canStartGame && !gameStarted;

                // ���ð�ťͼ��
                SetButtonIcon(startGameSprite);
            }
            else
            {
                // �����ʾ׼����ť
                actionButton.gameObject.SetActive(true);
                actionButton.interactable = !gameStarted;

                // ����׼��״̬����ͼ��
                SetButtonIcon(myReadyState ? cancelReadySprite : readySprite);
            }

            // �����뿪���䰴ť
            if (leaveRoomButton != null)
            {
                leaveRoomButton.interactable = !gameStarted;
            }
        }

        /// <summary>
        /// ���ð�ťͼ�꣨�л�������Image��Sprite��
        /// </summary>
        private void SetButtonIcon(Sprite sprite)
        {
            if (actionButtonIcon != null && sprite != null)
            {
                actionButtonIcon.sprite = sprite;
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
            if (actionButton != null) actionButton.gameObject.SetActive(false);
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
        /// ������ť������ϲ���׼��/��ʼ��Ϸ��ť��
        /// </summary>
        private void OnActionButtonClicked()
        {
            if (NetworkManager.Instance?.IsConnected != true) return;

            bool isHost = NetworkManager.Instance.IsHost;

            if (isHost)
            {
                // ���������ʼ��Ϸ
                if (RoomManager.Instance != null)
                {
                    if (RoomManager.Instance.CanStartGame())
                    {
                        RoomManager.Instance.StartGame();
                        LogDebug("���������ʼ��Ϸ");
                    }
                    else
                    {
                        LogDebug($"�޷���ʼ��Ϸ: {RoomManager.Instance.GetGameStartConditions()}");
                    }
                }
            }
            else
            {
                // ��ҵ��׼��/ȡ��׼��
                bool currentState = NetworkManager.Instance.GetMyReadyState();
                bool newState = !currentState;

                if (RoomManager.Instance != null)
                {
                    RoomManager.Instance.SetPlayerReady(newState);
                    LogDebug($"����׼��״̬: {currentState} -> {newState}");
                }
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
        }

        #endregion

        #region ���ݻ�ȡ����

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

        #endregion

        #region UIЧ����֪ͨ

        /// <summary>
        /// ��ʾ�������׼������֪ͨ
        /// </summary>
        private void ShowAllReadyNotification()
        {
            LogDebug("�������׼������ - ���������Ч");
            // ���������������Ч����Ч��UI��ʾ
        }

        /// <summary>
        /// ��ʾ��Ϸ��ʼ֪ͨ
        /// </summary>
        private void ShowGameStartingNotification()
        {
            LogDebug("��Ϸ������ʼ - ������ӵ���ʱ");
            // ������������ӵ���ʱUI����Ч
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

    }
}