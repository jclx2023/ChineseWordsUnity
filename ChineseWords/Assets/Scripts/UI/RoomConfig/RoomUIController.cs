using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using Core.Network;
using Photon.Realtime;
using RoomScene.Manager;
using RoomScene.Data;
using Photon.Pun;
using System.Linq;

namespace UI
{
    /// <summary>
    /// ������������ - ��ȫ�ع��汾��֧��3Dģ��ѡ��
    /// ÿ�������ʾ��3Dģ��Ԥ�� + ģ���л���ť + ����׼����ť + �����Ϣ
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
        [SerializeField] private GameObject playerModelItemPrefab;    // ����3Dģ��Ԥ���������Ԥ����

        [Header("��������UI")]
        [SerializeField] private Button leaveRoomButton;             // ֻ�����뿪���䰴ť����ʼ��Ϸ�ϲ������Ե�actionButton

        [Header("UIˢ������")]
        [SerializeField] private float autoRefreshInterval = 3f;
        [SerializeField] private bool enableAutoRefresh = true;

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        // ���UI����
        private Dictionary<ushort, PlayerModelItemUI> playerUIItems = new Dictionary<ushort, PlayerModelItemUI>();
        private Dictionary<ushort, RoomPlayerData> playerDataCache = new Dictionary<ushort, RoomPlayerData>();

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
            ClearAllPlayerUI();
        }

        #region ��ʼ��

        /// <summary>
        /// ��ʼ��UI������
        /// </summary>
        private IEnumerator InitializeUIController()
        {
            LogDebug("��ʼ��ʼ��RoomUIController (ģ��ѡ��汾)");

            // �ȴ��������
            while (RoomManager.Instance == null || NetworkManager.Instance == null ||
                   PlayerModelManager.Instance == null)
            {
                yield return new WaitForSeconds(0.1f);
            }

            while (!NetworkManager.Instance.IsConnected)
            {
                LogDebug("�ȴ���������...");
                yield return new WaitForSeconds(0.1f);
            }

            // ��֤��Ҫ���
            if (playerModelItemPrefab == null)
            {
                Debug.LogError("[RoomUIController] δ����playerModelItemPrefab��");
                yield break;
            }

            // ��ʼ��UI���
            InitializeUIComponents();

            // �����¼�
            SubscribeToEvents();

            // ��ʼ���������
            InitializePlayerData();

            // ����ˢ��UI
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
            // ���뿪���䰴ť
            if (leaveRoomButton != null)
            {
                leaveRoomButton.onClick.RemoveAllListeners();
                leaveRoomButton.onClick.AddListener(OnLeaveRoomButtonClicked);
            }

            LogDebug("UI�����ʼ�����");
        }

        /// <summary>
        /// ��ʼ���������
        /// </summary>
        private void InitializePlayerData()
        {
            foreach (var player in PhotonNetwork.PlayerList)
            {
                ushort playerId = (ushort)player.ActorNumber;
                var playerData = new RoomPlayerData
                {
                    playerId = playerId,
                    playerName = player.NickName ?? $"Player_{playerId}",
                    isHost = player.IsMasterClient,
                    isReady = NetworkManager.Instance.GetPlayerReady(playerId),
                    selectedModelId = PlayerModelManager.Instance.GetDefaultModelId()
                };

                playerDataCache[playerId] = playerData;
            }

            LogDebug($"��ʼ���� {playerDataCache.Count} ���������");
        }

        #endregion

        #region �¼�����

        /// <summary>
        /// �����¼�
        /// </summary>
        private void SubscribeToEvents()
        {
            // RoomManager�¼�
            if (RoomManager.Instance != null)
            {
                RoomManager.OnRoomEntered += OnRoomEntered;
                RoomManager.OnPlayerJoinedRoom += OnPlayerJoinedRoom;
                RoomManager.OnPlayerLeftRoom += OnPlayerLeftRoom;
                RoomManager.OnPlayerReadyChanged += OnPlayerReadyChanged;
                RoomManager.OnGameStarting += OnGameStarting;
                RoomManager.OnReturnToLobby += OnReturnToLobby;
            }

            // NetworkManager�¼�
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerReadyChanged += OnNetworkPlayerReadyChanged;
                NetworkManager.OnPlayerModelChanged += OnPlayerModelChanged;
                NetworkManager.OnModelSyncRequested += OnModelSyncRequested;
            }

            LogDebug("�Ѷ��������¼�");
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
                RoomManager.OnGameStarting -= OnGameStarting;
                RoomManager.OnReturnToLobby -= OnReturnToLobby;
            }

            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerReadyChanged -= OnNetworkPlayerReadyChanged;
                NetworkManager.OnPlayerModelChanged -= OnPlayerModelChanged;
                NetworkManager.OnModelSyncRequested -= OnModelSyncRequested;
            }

            LogDebug("��ȡ�������¼�");
        }

        #endregion

        #region �¼�����

        private void OnRoomEntered()
        {
            LogDebug("��������¼�");
            RefreshAllUI();
        }

        private void OnPlayerJoinedRoom(Player player)
        {
            if (player == null) return;

            ushort playerId = (ushort)player.ActorNumber;
            LogDebug($"��Ҽ���: {player.NickName} (ID: {playerId})");

            // �����������
            var playerData = new RoomPlayerData
            {
                playerId = playerId,
                playerName = player.NickName ?? $"Player_{playerId}",
                isHost = player.IsMasterClient,
                isReady = false,
                selectedModelId = PlayerModelManager.Instance.GetDefaultModelId()
            };
            playerDataCache[playerId] = playerData;

            // ����UI
            CreatePlayerUI(playerId);
            RefreshRoomInfo();
            RefreshHostControls();
        }

        private void OnPlayerLeftRoom(Player player)
        {
            if (player == null) return;

            ushort playerId = (ushort)player.ActorNumber;
            LogDebug($"����뿪: {player.NickName} (ID: {playerId})");

            RemovePlayerUI(playerId);
            playerDataCache.Remove(playerId);

            RefreshRoomInfo();
            RefreshHostControls();
        }

        private void OnPlayerReadyChanged(Player player, bool isReady)
        {
            if (player == null) return;

            ushort playerId = (ushort)player.ActorNumber;
            LogDebug($"���׼��״̬�仯: {player.NickName} -> {isReady}");

            if (playerDataCache.ContainsKey(playerId))
            {
                playerDataCache[playerId].SetReady(isReady);
            }

            UpdatePlayerUI(playerId);
            RefreshHostControls();
        }

        private void OnNetworkPlayerReadyChanged(ushort playerId, bool isReady)
        {
            LogDebug($"�������׼��״̬�仯: ID {playerId} -> {isReady}");

            if (playerDataCache.ContainsKey(playerId))
            {
                playerDataCache[playerId].SetReady(isReady);
            }

            UpdatePlayerUI(playerId);
            RefreshHostControls();
        }

        private void OnPlayerModelChanged(ushort playerId, int modelId, string modelName)
        {
            LogDebug($"���ģ�ͱ仯: ID {playerId} -> ģ�� {modelId} ({modelName})");

            if (playerDataCache.ContainsKey(playerId))
            {
                playerDataCache[playerId].SetSelectedModel(modelId, modelName);
            }

            UpdatePlayerUI(playerId);
        }

        private void OnModelSyncRequested(ushort requestingPlayerId)
        {
            if (!NetworkManager.Instance.IsHost) return;

            LogDebug($"�յ���� {requestingPlayerId} ��ģ��ͬ������");

            // ���͵�ǰ������ҵ�ģ������
            var playerIds = new List<ushort>();
            var modelIds = new List<int>();

            foreach (var kvp in playerDataCache)
            {
                playerIds.Add(kvp.Key);
                modelIds.Add(kvp.Value.selectedModelId);
            }

            if (playerIds.Count > 0)
            {
                NetworkManager.Instance.BroadcastAllPlayerModels(playerIds.ToArray(), modelIds.ToArray());
            }
        }

        private void OnGameStarting()
        {
            LogDebug("��Ϸ������ʼ������ģ��ѡ������");
            SavePlayerModelSelections();
            RefreshHostControls();
        }

        private void OnReturnToLobby()
        {
            LogDebug("���ش���");
            ClearAllUI();
        }

        #endregion

        #region UIˢ��

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
            RefreshHostControls();
        }

        /// <summary>
        /// ˢ�·�����Ϣ
        /// </summary>
        private void RefreshRoomInfo()
        {
            if (NetworkManager.Instance?.IsConnected != true) return;

            if (roomNameText != null)
                roomNameText.text = $"����: {NetworkManager.Instance.RoomName}";

            if (roomCodeText != null)
            {
                string roomCode = NetworkManager.Instance.GetRoomProperty<string>("roomCode", "");
                roomCodeText.text = string.IsNullOrEmpty(roomCode) ? "�������: ��" : $"�������: {roomCode}";
            }

            if (roomStatusText != null)
            {
                var roomState = GetRoomState();
                roomStatusText.text = $"״̬: {GetRoomStateDisplayText(roomState)}";
            }

            if (playerCountText != null)
            {
                int totalPlayers = NetworkManager.Instance.PlayerCount;
                int maxPlayers = NetworkManager.Instance.MaxPlayers;
                int readyCount = GetReadyPlayerCount();
                int nonHostCount = GetNonHostPlayerCount();

                playerCountText.text = $"���: {totalPlayers}/{maxPlayers} (׼��: {readyCount}/{nonHostCount})";
            }
        }

        /// <summary>
        /// ˢ������б�
        /// </summary>
        private void RefreshPlayerList()
        {
            if (NetworkManager.Instance?.IsConnected != true) return;

            LogDebug("ˢ������б�");

            // ��ȡ��ǰ�����������
            var currentPlayerIds = new HashSet<ushort>();
            foreach (var player in PhotonNetwork.PlayerList)
            {
                ushort playerId = (ushort)player.ActorNumber;
                currentPlayerIds.Add(playerId);

                // ȷ��������ݴ���
                if (!playerDataCache.ContainsKey(playerId))
                {
                    var playerData = new RoomPlayerData
                    {
                        playerId = playerId,
                        playerName = player.NickName ?? $"Player_{playerId}",
                        isHost = player.IsMasterClient,
                        isReady = NetworkManager.Instance.GetPlayerReady(playerId),
                        selectedModelId = PlayerModelManager.Instance.GetDefaultModelId()
                    };
                    playerDataCache[playerId] = playerData;
                }

                // ���������UI
                if (!playerUIItems.ContainsKey(playerId))
                {
                    CreatePlayerUI(playerId);
                }
                else
                {
                    UpdatePlayerUI(playerId);
                }
            }

            // �Ƴ����뿪��ҵ�UI
            var playersToRemove = new List<ushort>();
            foreach (var playerId in playerUIItems.Keys)
            {
                if (!currentPlayerIds.Contains(playerId))
                {
                    playersToRemove.Add(playerId);
                }
            }

            foreach (var playerId in playersToRemove)
            {
                RemovePlayerUI(playerId);
                playerDataCache.Remove(playerId);
            }
        }

        /// <summary>
        /// ˢ�·�������
        /// </summary>
        private void RefreshHostControls()
        {
            bool gameStarted = GetGameStarted();

            // �뿪���䰴ť
            if (leaveRoomButton != null)
            {
                leaveRoomButton.interactable = !gameStarted;
            }
        }

        #endregion

        #region ���UI����

        /// <summary>
        /// �������UI
        /// </summary>
        private void CreatePlayerUI(ushort playerId)
        {
            if (playerModelItemPrefab == null || playerListParent == null)
            {
                Debug.LogError("[RoomUIController] ȱ�ٱ�Ҫ��UIԤ����򸸼�");
                return;
            }

            if (playerUIItems.ContainsKey(playerId))
            {
                LogDebug($"��� {playerId} ��UI�Ѵ��ڣ����Ƴ�");
                RemovePlayerUI(playerId);
            }

            // ʵ����UI��
            GameObject uiItem = Instantiate(playerModelItemPrefab, playerListParent);
            uiItem.name = $"PlayerModelItem_{playerId}";

            // ��ȡPlayerModelItemUI���
            var itemUI = uiItem.GetComponent<PlayerModelItemUI>();
            if (itemUI == null)
            {
                itemUI = uiItem.AddComponent<PlayerModelItemUI>();
            }

            // ��ʼ��UI��
            var playerData = playerDataCache[playerId];
            itemUI.Initialize(playerId, playerData, this);

            // ����UI��
            playerUIItems[playerId] = itemUI;

            LogDebug($"�������UI: {playerData.playerName} (ID: {playerId})");
        }

        /// <summary>
        /// �������UI
        /// </summary>
        private void UpdatePlayerUI(ushort playerId)
        {
            if (!playerUIItems.ContainsKey(playerId) || !playerDataCache.ContainsKey(playerId))
                return;

            var itemUI = playerUIItems[playerId];
            var playerData = playerDataCache[playerId];

            itemUI.UpdateDisplay(playerData);
        }

        /// <summary>
        /// �Ƴ����UI
        /// </summary>
        private void RemovePlayerUI(ushort playerId)
        {
            if (playerUIItems.ContainsKey(playerId))
            {
                var itemUI = playerUIItems[playerId];
                if (itemUI != null && itemUI.gameObject != null)
                {
                    Destroy(itemUI.gameObject);
                }
                playerUIItems.Remove(playerId);
                LogDebug($"�Ƴ����UI: ID {playerId}");
            }
        }

        /// <summary>
        /// ����������UI
        /// </summary>
        private void ClearAllPlayerUI()
        {
            foreach (var itemUI in playerUIItems.Values)
            {
                if (itemUI != null && itemUI.gameObject != null)
                {
                    Destroy(itemUI.gameObject);
                }
            }
            playerUIItems.Clear();
            playerDataCache.Clear();
        }

        /// <summary>
        /// �������UI
        /// </summary>
        private void ClearAllUI()
        {
            // ��շ�����Ϣ
            if (roomNameText != null) roomNameText.text = "����: δ����";
            if (roomCodeText != null) roomCodeText.text = "�������: ��";
            if (roomStatusText != null) roomStatusText.text = "״̬: ����";
            if (playerCountText != null) playerCountText.text = "���: 0/0";

            // �������б�
            ClearAllPlayerUI();

            // ���ؿ��ư�ť
            // �Ƴ���startGameButton��ش��룬��Ϊ��ʼ��Ϸ�����Ѻϲ������Ե�actionButton
        }

        #endregion

        #region �����ӿ� - ��PlayerModelItemUI����

        /// <summary>
        /// ���ģ��ѡ��仯
        /// </summary>
        public void OnPlayerModelSelectionChanged(ushort playerId, int newModelId)
        {
            if (!playerDataCache.ContainsKey(playerId)) return;

            string modelName = PlayerModelManager.Instance.GetModelName(newModelId);
            playerDataCache[playerId].SetSelectedModel(newModelId, modelName);

            // ͨ������ͬ�����������
            NetworkManager.Instance.SendPlayerModelChangeRPC(playerId, newModelId, modelName);

            LogDebug($"��� {playerId} ѡ��ģ��: {modelName} (ID: {newModelId})");
        }

        /// <summary>
        /// ���׼��״̬�仯��֧�ַ�����ʼ��Ϸ��
        /// </summary>
        public void OnPlayerReadyStateChanged(ushort playerId, bool isReady)
        {
            if (playerId != NetworkManager.Instance.ClientId)
            {
                LogDebug("ֻ�ܸı��Լ���״̬");
                return;
            }

            var playerData = GetPlayerData(playerId);
            if (playerData == null) return;

            if (playerData.isHost)
            {
                // ���������ʼ��Ϸ
                if (CanStartGame())
                {
                    RoomManager.Instance.StartGame();
                    LogDebug("���������ʼ��Ϸ");
                }
                else
                {
                    LogDebug("�޷���ʼ��Ϸ - ����������");
                }
            }
            else
            {
                // ��ͨ��Ҹı�׼��״̬
                RoomManager.Instance.SetPlayerReady(isReady);
                LogDebug($"���ñ������׼��״̬: {isReady}");
            }
        }

        /// <summary>
        /// ��ȡ�������
        /// </summary>
        public RoomPlayerData GetPlayerData(ushort playerId)
        {
            return playerDataCache.TryGetValue(playerId, out RoomPlayerData data) ? data : null;
        }

        /// <summary>
        /// ����Ƿ���Կ�ʼ��Ϸ����PlayerModelItemUI���ã�
        /// </summary>
        public bool CanStartGame()
        {
            return RoomManager.Instance?.CanStartGame() ?? false;
        }

        #endregion

        #region ���ݱ���

        /// <summary>
        /// �������ģ��ѡ������
        /// </summary>
        private void SavePlayerModelSelections()
        {
            var modelSelections = new Dictionary<ushort, int>();
            foreach (var kvp in playerDataCache)
            {
                modelSelections[kvp.Key] = kvp.Value.selectedModelId;
            }

            PlayerModelSelectionData.SaveSelections(modelSelections);
            LogDebug($"������ {modelSelections.Count} ����ҵ�ģ��ѡ������");
        }

        #endregion

        #region ��ť�¼�

        private void OnLeaveRoomButtonClicked()
        {
            LogDebug("����뿪����");
            RoomManager.Instance.LeaveRoomAndReturnToLobby();
        }

        #endregion

        #region �Զ�ˢ��

        private void StartAutoRefresh()
        {
            if (enableAutoRefresh && autoRefreshCoroutine == null)
            {
                autoRefreshCoroutine = StartCoroutine(AutoRefreshCoroutine());
                LogDebug($"�����Զ�ˢ�£����: {autoRefreshInterval}��");
            }
        }

        private void StopAutoRefresh()
        {
            if (autoRefreshCoroutine != null)
            {
                StopCoroutine(autoRefreshCoroutine);
                autoRefreshCoroutine = null;
            }
        }

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

        #region ��������

        private RoomState GetRoomState()
        {
            return RoomManager.Instance?.GetRoomState() ?? RoomState.Waiting;
        }

        private bool GetGameStarted()
        {
            return RoomManager.Instance?.GetGameStarted() ?? false;
        }

        private int GetReadyPlayerCount()
        {
            return playerDataCache.Values.Count(p => p.isReady && !p.isHost);
        }

        private int GetNonHostPlayerCount()
        {
            return playerDataCache.Values.Count(p => !p.isHost);
        }

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