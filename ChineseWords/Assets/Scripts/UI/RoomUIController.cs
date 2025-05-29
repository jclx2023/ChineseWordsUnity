using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using Core.Network;

namespace UI
{
    /// <summary>
    /// ������������ - ���𷿼�UI���Զ�ˢ�º�ͬ��
    /// ���Host��Client֮��Ľ�����Ϣ��ͬ������
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
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button readyButton;
        [SerializeField] private TMP_Text readyButtonText;

        [Header("ˢ������")]
        [SerializeField] private bool enableAutoRefresh = true;
        [SerializeField] private float autoRefreshInterval = 1f;
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool enableManualRefresh = true;
        [SerializeField] private Button manualRefreshButton;

        // ����б�UI����
        private Dictionary<ushort, GameObject> playerUIItems = new Dictionary<ushort, GameObject>();

        // ״̬���棨���ڼ��仯��
        private string lastRoomStatus = "";
        private int lastPlayerCount = 0;
        private Dictionary<ushort, PlayerUIState> lastPlayerStates = new Dictionary<ushort, PlayerUIState>();

        // �Զ�ˢ��Э��
        private Coroutine autoRefreshCoroutine;

        private struct PlayerUIState
        {
            public string playerName;
            public PlayerRoomState state;
            public bool isHost;

            public PlayerUIState(string name, PlayerRoomState roomState, bool host)
            {
                playerName = name;
                state = roomState;
                isHost = host;
            }

            public bool Equals(PlayerUIState other)
            {
                return playerName == other.playerName &&
                       state == other.state &&
                       isHost == other.isHost;
            }
        }

        private void Start()
        {
            InitializeUI();
            SubscribeToEvents();

            if (enableAutoRefresh)
            {
                StartAutoRefresh();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            StopAutoRefresh();
        }

        #region ��ʼ��

        /// <summary>
        /// ��ʼ��UI���
        /// </summary>
        private void InitializeUI()
        {
            // ���ֶ�ˢ�°�ť
            if (manualRefreshButton != null && enableManualRefresh)
            {
                manualRefreshButton.onClick.RemoveAllListeners();
                manualRefreshButton.onClick.AddListener(ForceRefreshUI);
                manualRefreshButton.gameObject.SetActive(true);
            }
            else if (manualRefreshButton != null)
            {
                manualRefreshButton.gameObject.SetActive(false);
            }

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

            LogDebug("RoomUIController ��ʼ�����");
        }

        /// <summary>
        /// ���ķ���������¼�
        /// </summary>
        private void SubscribeToEvents()
        {
            if (RoomManager.Instance != null)
            {
                RoomManager.OnRoomCreated += OnRoomUpdated;
                RoomManager.OnRoomJoined += OnRoomUpdated;
                RoomManager.OnPlayerJoinedRoom += OnPlayerJoinedRoom;
                RoomManager.OnPlayerLeftRoom += OnPlayerLeftRoom;
                RoomManager.OnPlayerReadyChanged += OnPlayerReadyChanged;
                RoomManager.OnGameStarting += OnGameStarting;
                RoomManager.OnRoomLeft += OnRoomLeft;

                LogDebug("�Ѷ��� RoomManager �¼�");
            }
            else
            {
                LogDebug("RoomManager ʵ�������ڣ������Ժ����Զ���");
                StartCoroutine(DelayedSubscribe());
            }

            // ���������¼�����Ϊ����ͬ�����ƣ�
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerJoined += OnNetworkPlayerJoined;
                NetworkManager.OnPlayerLeft += OnNetworkPlayerLeft;
                LogDebug("�Ѷ��� NetworkManager �¼�");
            }
        }

        /// <summary>
        /// �ӳٶ��ģ��ȴ�RoomManager��ʼ����
        /// </summary>
        private IEnumerator DelayedSubscribe()
        {
            int retryCount = 0;
            while (RoomManager.Instance == null && retryCount < 10)
            {
                yield return new WaitForSeconds(0.5f);
                retryCount++;
            }

            if (RoomManager.Instance != null)
            {
                SubscribeToEvents();
                // ����ˢ��һ��UI
                RefreshUI();
            }
            else
            {
                Debug.LogError("[RoomUIController] RoomManager ��ʼ����ʱ");
            }
        }

        /// <summary>
        /// ȡ�������¼�
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (RoomManager.Instance != null)
            {
                RoomManager.OnRoomCreated -= OnRoomUpdated;
                RoomManager.OnRoomJoined -= OnRoomUpdated;
                RoomManager.OnPlayerJoinedRoom -= OnPlayerJoinedRoom;
                RoomManager.OnPlayerLeftRoom -= OnPlayerLeftRoom;
                RoomManager.OnPlayerReadyChanged -= OnPlayerReadyChanged;
                RoomManager.OnGameStarting -= OnGameStarting;
                RoomManager.OnRoomLeft -= OnRoomLeft;
            }

            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerJoined -= OnNetworkPlayerJoined;
                NetworkManager.OnPlayerLeft -= OnNetworkPlayerLeft;
            }
        }

        #endregion

        #region �¼�����

        /// <summary>
        /// ���䴴��/�����¼�����
        /// </summary>
        private void OnRoomUpdated(RoomData roomData)
        {
            LogDebug($"��������¼�: {roomData.roomName}");
            RefreshUI();
        }

        /// <summary>
        /// ��Ҽ��뷿���¼�����
        /// </summary>
        private void OnPlayerJoinedRoom(RoomPlayer player)
        {
            LogDebug($"��Ҽ����¼�: {player.playerName} (ID: {player.playerId})");
            RefreshPlayerList();
            RefreshRoomInfo();
        }

        /// <summary>
        /// ����뿪�����¼�����
        /// </summary>
        private void OnPlayerLeftRoom(ushort playerId)
        {
            LogDebug($"����뿪�¼�: {playerId}");
            RemovePlayerFromUI(playerId);
            RefreshRoomInfo();
        }

        /// <summary>
        /// ���׼��״̬�仯�¼�����
        /// </summary>
        private void OnPlayerReadyChanged(ushort playerId, bool isReady)
        {
            LogDebug($"���׼��״̬�仯: {playerId} -> {isReady}");
            UpdatePlayerReadyState(playerId, isReady);
            RefreshGameControls();
        }

        /// <summary>
        /// ��Ϸ��ʼ�¼�����
        /// </summary>
        private void OnGameStarting()
        {
            LogDebug("��Ϸ��ʼ�¼�");
            RefreshGameControls();
        }

        /// <summary>
        /// �뿪�����¼�����
        /// </summary>
        private void OnRoomLeft()
        {
            LogDebug("�뿪�����¼�");
            ClearUI();
        }

        /// <summary>
        /// ������Ҽ����¼���������ͬ����
        /// </summary>
        private void OnNetworkPlayerJoined(ushort playerId)
        {
            LogDebug($"������Ҽ����¼�: {playerId}");
            // �ӳ�ˢ�£�ȷ��RoomManager�Ѵ������
            StartCoroutine(DelayedRefresh(0.1f));
        }

        /// <summary>
        /// ��������뿪�¼���������ͬ����
        /// </summary>
        private void OnNetworkPlayerLeft(ushort playerId)
        {
            LogDebug($"��������뿪�¼�: {playerId}");
            // �ӳ�ˢ�£�ȷ��RoomManager�Ѵ������
            StartCoroutine(DelayedRefresh(0.1f));
        }

        #endregion

        #region UIˢ�º����߼�

        /// <summary>
        /// ��ȫˢ��UI
        /// </summary>
        public void RefreshUI()
        {
            if (!RoomManager.Instance?.IsInRoom == true)
            {
                ClearUI();
                return;
            }

            RefreshRoomInfo();
            RefreshPlayerList();
            RefreshGameControls();

            LogDebug("UI ��ȫˢ�����");
        }

        /// <summary>
        /// ǿ��ˢ��UI�����Ի��棩
        /// </summary>
        public void ForceRefreshUI()
        {
            LogDebug("ִ��ǿ��ˢ��");

            // �������
            lastRoomStatus = "";
            lastPlayerCount = 0;
            lastPlayerStates.Clear();

            // ǿ��ˢ��
            RefreshUI();
        }

        /// <summary>
        /// ˢ�·�����Ϣ
        /// </summary>
        private void RefreshRoomInfo()
        {
            if (!RoomManager.Instance?.IsInRoom == true)
                return;

            var roomData = RoomManager.Instance.CurrentRoom;
            if (roomData == null)
                return;

            // ��鷿��״̬�Ƿ��б仯
            string currentStatus = RoomManager.Instance.GetRoomStatusInfo();
            if (currentStatus == lastRoomStatus && roomData.players.Count == lastPlayerCount)
                return;

            // ���·�������
            if (roomNameText != null)
                roomNameText.text = $"����: {roomData.roomName}";

            // ���·������
            if (roomCodeText != null)
                roomCodeText.text = $"�������: {roomData.roomCode}";

            // ���·���״̬
            if (roomStatusText != null)
            {
                string statusText = GetRoomStateDisplayText(roomData.state);
                roomStatusText.text = $"״̬: {statusText}";
            }

            // �����������
            if (playerCountText != null)
            {
                int readyCount = roomData.GetReadyPlayerCount();
                int totalNonHost = roomData.GetNonHostPlayerCount();
                playerCountText.text = $"���: {roomData.players.Count}/{roomData.maxPlayers} (׼��: {readyCount}/{totalNonHost})";
            }

            // ���»���
            lastRoomStatus = currentStatus;
            lastPlayerCount = roomData.players.Count;

            LogDebug($"������Ϣ�Ѹ���: {roomData.roomName}, �����: {roomData.players.Count}");
        }

        /// <summary>
        /// ˢ������б�
        /// </summary>
        private void RefreshPlayerList()
        {
            if (!RoomManager.Instance?.IsInRoom == true || playerListParent == null)
                return;

            var roomData = RoomManager.Instance.CurrentRoom;
            if (roomData == null)
                return;

            // �������б��Ƿ��б仯
            bool hasChanges = false;
            foreach (var player in roomData.players.Values)
            {
                var newState = new PlayerUIState(player.playerName, player.state, player.isHost);
                if (!lastPlayerStates.ContainsKey(player.playerId) ||
                    !lastPlayerStates[player.playerId].Equals(newState))
                {
                    hasChanges = true;
                    lastPlayerStates[player.playerId] = newState;
                }
            }

            // ����Ƿ�������뿪
            var playersToRemove = new List<ushort>();
            foreach (var playerId in lastPlayerStates.Keys)
            {
                if (!roomData.players.ContainsKey(playerId))
                {
                    playersToRemove.Add(playerId);
                    hasChanges = true;
                }
            }

            foreach (var playerId in playersToRemove)
            {
                lastPlayerStates.Remove(playerId);
            }

            if (!hasChanges)
                return;

            // �Ƴ����뿪��ҵ�UI
            foreach (var playerId in playersToRemove)
            {
                RemovePlayerFromUI(playerId);
            }

            // ���»򴴽����UI��
            foreach (var player in roomData.players.Values)
            {
                UpdateOrCreatePlayerUI(player);
            }

            LogDebug($"����б��Ѹ��£���ǰ�����: {roomData.players.Count}");
        }

        /// <summary>
        /// ���»򴴽����UI��
        /// </summary>
        private void UpdateOrCreatePlayerUI(RoomPlayer player)
        {
            GameObject playerItem;

            // ����Ƿ��Ѵ���UI��
            if (playerUIItems.ContainsKey(player.playerId))
            {
                playerItem = playerUIItems[player.playerId];
                if (playerItem == null)
                {
                    playerUIItems.Remove(player.playerId);
                    playerItem = CreatePlayerUIItem(player);
                }
            }
            else
            {
                playerItem = CreatePlayerUIItem(player);
            }

            // ����UI������
            UpdatePlayerUIContent(playerItem, player);
        }

        /// <summary>
        /// �������UI��
        /// </summary>
        private GameObject CreatePlayerUIItem(RoomPlayer player)
        {
            if (playerItemPrefab == null)
            {
                LogDebug("���UIԤ����δ����");
                return null;
            }

            GameObject item = Instantiate(playerItemPrefab, playerListParent);
            playerUIItems[player.playerId] = item;

            LogDebug($"�������UI��: {player.playerName}");
            return item;
        }

        /// <summary>
        /// �������UI������
        /// </summary>
        private void UpdatePlayerUIContent(GameObject playerItem, RoomPlayer player)
        {
            if (playerItem == null)
                return;

            // �����������
            var nameText = playerItem.GetComponentInChildren<TMP_Text>();
            if (nameText != null)
            {
                string displayName = player.playerName;
                if (player.isHost)
                    displayName += " (����)";

                nameText.text = displayName;
            }

            // ����׼��״̬��ʾ
            var statusText = playerItem.transform.Find("StatusText")?.GetComponent<TMP_Text>();
            if (statusText != null)
            {
                statusText.text = GetPlayerStateDisplayText(player.state);
                statusText.color = GetPlayerStateColor(player.state);
            }

            // ���±�����ɫ��ͼ��
            var backgroundImage = playerItem.GetComponent<Image>();
            if (backgroundImage != null)
            {
                backgroundImage.color = player.isHost ?
                    new Color(1f, 0.8f, 0.2f, 0.3f) : // ������ɫ����
                    Color.white;
            }
        }

        /// <summary>
        /// ��UI���Ƴ����
        /// </summary>
        private void RemovePlayerFromUI(ushort playerId)
        {
            if (playerUIItems.ContainsKey(playerId))
            {
                var item = playerUIItems[playerId];
                if (item != null)
                {
                    Destroy(item);
                }
                playerUIItems.Remove(playerId);

                LogDebug($"�Ƴ����UI: {playerId}");
            }
        }

        /// <summary>
        /// �����ض���ҵ�׼��״̬
        /// </summary>
        private void UpdatePlayerReadyState(ushort playerId, bool isReady)
        {
            if (!RoomManager.Instance?.IsInRoom == true)
                return;

            var roomData = RoomManager.Instance.CurrentRoom;
            if (roomData?.players.ContainsKey(playerId) == true)
            {
                var player = roomData.players[playerId];
                UpdateOrCreatePlayerUI(player);
            }
        }

        /// <summary>
        /// ˢ����Ϸ���ư�ť
        /// </summary>
        private void RefreshGameControls()
        {
            if (!RoomManager.Instance?.IsInRoom == true)
                return;

            bool isHost = RoomManager.Instance.IsHost;
            bool canStartGame = RoomManager.Instance.CanStartGame();
            bool myReadyState = RoomManager.Instance.GetMyReadyState();

            // ���¿�ʼ��Ϸ��ť
            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(isHost);
                startGameButton.interactable = canStartGame;

                var buttonText = startGameButton.GetComponentInChildren<TMP_Text>();
                if (buttonText != null)
                {
                    buttonText.text = canStartGame ? "��ʼ��Ϸ" : "�ȴ����׼��";
                }
            }

            // ����׼����ť
            if (readyButton != null)
            {
                readyButton.gameObject.SetActive(!isHost);

                if (readyButtonText != null)
                {
                    readyButtonText.text = myReadyState ? "ȡ��׼��" : "׼��";
                }
            }
        }

        /// <summary>
        /// ���UI
        /// </summary>
        private void ClearUI()
        {
            LogDebug("��շ���UI");

            // ��շ�����Ϣ
            if (roomNameText != null) roomNameText.text = "";
            if (roomCodeText != null) roomCodeText.text = "";
            if (roomStatusText != null) roomStatusText.text = "";
            if (playerCountText != null) playerCountText.text = "";

            // �������б�
            foreach (var item in playerUIItems.Values)
            {
                if (item != null)
                    Destroy(item);
            }
            playerUIItems.Clear();

            // ���ؿ��ư�ť
            if (startGameButton != null) startGameButton.gameObject.SetActive(false);
            if (readyButton != null) readyButton.gameObject.SetActive(false);

            // ��ջ���
            lastRoomStatus = "";
            lastPlayerCount = 0;
            lastPlayerStates.Clear();
        }

        #endregion

        #region �Զ�ˢ�»���

        /// <summary>
        /// ��ʼ�Զ�ˢ��
        /// </summary>
        private void StartAutoRefresh()
        {
            if (autoRefreshCoroutine != null)
                StopCoroutine(autoRefreshCoroutine);

            autoRefreshCoroutine = StartCoroutine(AutoRefreshCoroutine());
            LogDebug($"�Զ�ˢ�������������: {autoRefreshInterval}��");
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
                LogDebug("�Զ�ˢ����ֹͣ");
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

                if (RoomManager.Instance?.IsInRoom == true)
                {
                    RefreshUI();
                }
            }
        }

        /// <summary>
        /// �ӳ�ˢ��
        /// </summary>
        private IEnumerator DelayedRefresh(float delay)
        {
            yield return new WaitForSeconds(delay);
            RefreshUI();
        }

        #endregion

        #region ��ť�¼�����

        /// <summary>
        /// ׼����ť���
        /// </summary>
        private void OnReadyButtonClicked()
        {
            if (RoomManager.Instance?.IsInRoom == true)
            {
                bool currentState = RoomManager.Instance.GetMyReadyState();
                RoomManager.Instance.SetPlayerReady(!currentState);

                LogDebug($"�л�׼��״̬: {!currentState}");
            }
        }

        /// <summary>
        /// ��ʼ��Ϸ��ť���
        /// </summary>
        private void OnStartGameButtonClicked()
        {
            if (RoomManager.Instance?.IsHost == true)
            {
                RoomManager.Instance.StartGame();
                LogDebug("���������ʼ��Ϸ");
            }
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
                default: return "δ֪";
            }
        }

        /// <summary>
        /// ��ȡ���״̬��ʾ�ı�
        /// </summary>
        private string GetPlayerStateDisplayText(PlayerRoomState state)
        {
            switch (state)
            {
                case PlayerRoomState.Connected: return "δ׼��";
                case PlayerRoomState.Ready: return "��׼��";
                case PlayerRoomState.InGame: return "��Ϸ��";
                case PlayerRoomState.Disconnected: return "�Ѷ���";
                default: return "δ֪";
            }
        }

        /// <summary>
        /// ��ȡ���״̬��Ӧ����ɫ
        /// </summary>
        private Color GetPlayerStateColor(PlayerRoomState state)
        {
            switch (state)
            {
                case PlayerRoomState.Connected: return Color.red;      // δ׼�� - ��ɫ
                case PlayerRoomState.Ready: return Color.green;       // ��׼�� - ��ɫ
                case PlayerRoomState.InGame: return Color.blue;       // ��Ϸ�� - ��ɫ
                case PlayerRoomState.Disconnected: return Color.gray; // �Ѷ��� - ��ɫ
                default: return Color.yellow;                         // δ֪״̬ - ��ɫ
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

        #region �����ӿں͵��Է���

        /// <summary>
        /// �����Զ�ˢ��״̬
        /// </summary>
        public void SetAutoRefresh(bool enable)
        {
            enableAutoRefresh = enable;

            if (enable && autoRefreshCoroutine == null)
            {
                StartAutoRefresh();
            }
            else if (!enable && autoRefreshCoroutine != null)
            {
                StopAutoRefresh();
            }
        }

        /// <summary>
        /// �����Զ�ˢ�¼��
        /// </summary>
        public void SetAutoRefreshInterval(float interval)
        {
            autoRefreshInterval = Mathf.Max(0.1f, interval);

            if (autoRefreshCoroutine != null)
            {
                StopAutoRefresh();
                StartAutoRefresh();
            }
        }

        /// <summary>
        /// ��ȡ��ǰUI״̬��Ϣ
        /// </summary>
        public string GetUIStatusInfo()
        {
            return $"�Զ�ˢ��: {(autoRefreshCoroutine != null ? "����" : "�ر�")}, " +
                   $"ˢ�¼��: {autoRefreshInterval}s, " +
                   $"���UI����: {playerUIItems.Count}, " +
                   $"��������: {RoomManager.Instance?.IsInRoom}";
        }

        #endregion

        #region ���Է���

        [ContextMenu("ǿ��ˢ��UI")]
        public void DebugForceRefresh()
        {
            if (Application.isPlaying)
            {
                ForceRefreshUI();
            }
        }

        [ContextMenu("��ʾUI״̬")]
        public void DebugShowUIStatus()
        {
            if (Application.isPlaying)
            {
                Debug.Log($"=== RoomUIController ״̬ ===\n{GetUIStatusInfo()}");
            }
        }

        [ContextMenu("�����Զ�ˢ��")]
        public void DebugRestartAutoRefresh()
        {
            if (Application.isPlaying)
            {
                StopAutoRefresh();
                StartAutoRefresh();
            }
        }

        #endregion
    }
}