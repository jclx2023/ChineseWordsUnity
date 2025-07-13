using UnityEngine;
using System;
using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using UnityEngine.SceneManagement;
using Core.Network;

namespace Core.Network
{
    /// <summary>
    /// ��������� - �򻯽����
    /// ְ�𣺹�����״̬�����׼�����ơ���Ϸ��ʼ����
    /// ͨ��NetworkManager��Photon��������ֱ������Photon API
    /// </summary>
    public class RoomManager : MonoBehaviourPun
    {
        [Header("��Ϸ��ʼ����")]
        [SerializeField] private float gameStartDelay = 2f;
        [SerializeField] private int minPlayersToStart = 2;

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        public static RoomManager Instance { get; private set; }

        // �������Լ�������
        private const string ROOM_STATE_KEY = "roomState";
        private const string GAME_STARTED_KEY = "gameStarted";

        // �¼�ϵͳ
        public static event Action OnRoomEntered;
        public static event Action<Player> OnPlayerJoinedRoom;
        public static event Action<Player> OnPlayerLeftRoom;
        public static event Action<Player, bool> OnPlayerReadyChanged;
        public static event Action OnAllPlayersReady;
        public static event Action OnGameStarting;
        public static event Action OnReturnToLobby;

        // ͨ��NetworkManager���ʵ�����
        public bool IsHost => NetworkManager.Instance?.IsHost ?? false;
        public bool IsInRoom => NetworkManager.Instance?.IsConnected ?? false;
        public string RoomName => NetworkManager.Instance?.RoomName ?? "";
        public int PlayerCount => NetworkManager.Instance?.PlayerCount ?? 0;
        public int MaxPlayers => NetworkManager.Instance?.MaxPlayers ?? 0;
        public bool IsInitialized => isInitialized;

        // �ڲ�״̬
        private bool isInitialized = false;
        private bool hasGameStarted = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                LogDebug("RoomManager ��ʼ��������棩");
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            StartCoroutine(InitializeRoomManager());
        }

        private void OnDestroy()
        {
            UnsubscribeFromNetworkEvents();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #region ��ʼ��

        /// <summary>
        /// ��ʼ�����������
        /// </summary>
        private IEnumerator InitializeRoomManager()
        {
            // �ȴ�NetworkManager׼������
            while (NetworkManager.Instance == null)
            {
                LogDebug("�ȴ�NetworkManager��ʼ��...");
                yield return new WaitForSeconds(0.1f);
            }

            // �ȴ�ȷ���ڷ�����
            while (!IsInRoom)
            {
                LogDebug("�ȴ����뷿��...");
                yield return new WaitForSeconds(0.1f);
            }

            // ����NetworkManager�¼�
            SubscribeToNetworkEvents();

            // ��֤����״̬
            if (!ValidateRoomState())
            {
                Debug.LogError("[RoomManager] ����״̬��֤ʧ��");
                yield break;
            }

            // ��鲢������Ϸ�������״̬
            HandlePostGameState();

            // ����Ƿ�����ȷ�����䴦�ڵȴ�״̬
            if (IsHost && !hasGameStarted)
            {
                SetRoomWaitingState();
            }

            isInitialized = true;
            LogDebug($"RoomManager��ʼ����� - ����: {RoomName}, �����: {PlayerCount}, �Ƿ���: {IsHost}");

            OnRoomEntered?.Invoke();
        }

        /// <summary>
        /// ������Ϸ�������״̬
        /// </summary>
        private void HandlePostGameState()
        {
            bool gameEnded = NetworkManager.Instance.GetRoomProperty("gameEnded", false);
            hasGameStarted = NetworkManager.Instance.GetRoomProperty(GAME_STARTED_KEY, false);

            if (gameEnded && IsHost)
            {
                LogDebug("��⵽��Ϸ�ѽ��������������÷���״̬");
                // ͨ��NetworkManager����״̬����ᴥ����Ӧ���¼�
                NetworkManager.Instance.ResetRoomStateAfterGame();
                hasGameStarted = false;
            }
        }

        /// <summary>
        /// ��֤����״̬
        /// </summary>
        private bool ValidateRoomState()
        {
            if (!IsInRoom)
            {
                Debug.LogError("[RoomManager] ���ڷ�����");
                return false;
            }

            if (PlayerCount == 0)
            {
                Debug.LogError("[RoomManager] ������û�����");
                return false;
            }

            LogDebug("����״̬��֤ͨ��");
            return true;
        }

        #endregion

        #region �¼�����

        /// <summary>
        /// ����NetworkManager�¼�
        /// </summary>
        private void SubscribeToNetworkEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerJoined += OnNetworkPlayerJoined;
                NetworkManager.OnPlayerLeft += OnNetworkPlayerLeft;
                NetworkManager.OnPlayerReadyChanged += OnNetworkPlayerReadyChanged;
                NetworkManager.OnAllPlayersReady += OnNetworkAllPlayersReady;
                NetworkManager.OnRoomStateReset += OnNetworkRoomStateReset;
                LogDebug("�Ѷ���NetworkManager�¼�");
            }
        }

        /// <summary>
        /// ȡ������NetworkManager�¼�
        /// </summary>
        private void UnsubscribeFromNetworkEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerJoined -= OnNetworkPlayerJoined;
                NetworkManager.OnPlayerLeft -= OnNetworkPlayerLeft;
                NetworkManager.OnPlayerReadyChanged -= OnNetworkPlayerReadyChanged;
                NetworkManager.OnAllPlayersReady -= OnNetworkAllPlayersReady;
                NetworkManager.OnRoomStateReset -= OnNetworkRoomStateReset;
                LogDebug("��ȡ������NetworkManager�¼�");
            }
        }

        // NetworkManager�¼�����
        private void OnNetworkPlayerJoined(ushort playerId)
        {
            var player = GetPhotonPlayerById(playerId);
            if (player != null)
            {
                LogDebug($"��Ҽ��뷿��: {player.NickName} (ID: {playerId})");
                OnPlayerJoinedRoom?.Invoke(player);
            }
        }

        private void OnNetworkPlayerLeft(ushort playerId)
        {
            LogDebug($"����뿪����: ID {playerId}");
            // ����һ����ʱ��Player���������¼�֪ͨ
            // ע�⣺��ʱ����Ѿ��뿪���޷���ȡ������Ϣ
            OnPlayerLeftRoom?.Invoke(null);
        }

        private void OnNetworkPlayerReadyChanged(ushort playerId, bool isReady)
        {
            var player = GetPhotonPlayerById(playerId);
            if (player != null)
            {
                LogDebug($"���׼��״̬�仯: {player.NickName} -> {isReady}");
                OnPlayerReadyChanged?.Invoke(player, isReady);
            }
        }

        private void OnNetworkAllPlayersReady()
        {
            LogDebug("������Ҷ���׼������");
            OnAllPlayersReady?.Invoke();
        }

        private void OnNetworkRoomStateReset()
        {
            LogDebug("�յ�����״̬����֪ͨ");
            hasGameStarted = false;
        }

        #endregion

        #region ���׼��״̬����

        /// <summary>
        /// ���ñ������׼��״̬
        /// </summary>
        public bool SetPlayerReady(bool ready)
        {
            if (!IsInRoom || !isInitialized)
            {
                LogDebug("����δ��ʼ�����޷�����׼��״̬");
                return false;
            }

            if (IsHost)
            {
                LogDebug("������������׼��״̬");
                return false;
            }

            if (hasGameStarted)
            {
                LogDebug("��Ϸ�ѿ�ʼ���޷�����׼��״̬");
                return false;
            }

            LogDebug($"����׼��״̬: {ready}");
            NetworkManager.Instance?.SetPlayerReady(ready);
            return true;
        }

        /// <summary>
        /// ��ȡָ����ҵ�׼��״̬
        /// </summary>
        public bool GetPlayerReady(Player player)
        {
            if (player == null || NetworkManager.Instance == null) return false;
            return NetworkManager.Instance.GetPlayerReady((ushort)player.ActorNumber);
        }

        /// <summary>
        /// ��ȡ������ҵ�׼��״̬
        /// </summary>
        public bool GetMyReadyState()
        {
            return NetworkManager.Instance?.GetMyReadyState() ?? false;
        }

        /// <summary>
        /// ��ȡ׼�����������������������������
        /// </summary>
        public int GetReadyPlayerCount()
        {
            return NetworkManager.Instance?.GetReadyPlayerCount() ?? 0;
        }

        /// <summary>
        /// ��ȡ�Ƿ����������
        /// </summary>
        public int GetNonHostPlayerCount()
        {
            return NetworkManager.Instance?.GetNonHostPlayerCount() ?? 0;
        }

        /// <summary>
        /// ������зǷ�������Ƿ���׼��
        /// </summary>
        public bool AreAllPlayersReady()
        {
            return NetworkManager.Instance?.AreAllPlayersReady() ?? false;
        }

        #endregion

        #region ��Ϸ��ʼ����

        /// <summary>
        /// ������Ϸ - ����ר��
        /// </summary>
        public void StartGame()
        {
            if (!IsHost)
            {
                Debug.LogError("[RoomManager] ֻ�з�������������Ϸ");
                return;
            }

            if (!CanStartGame())
            {
                Debug.LogError("[RoomManager] ��Ϸ��������������");
                LogDebug($"�����������: {GetGameStartConditions()}");
                return;
            }

            LogDebug("����������Ϸ");

            // ��ֹ�ظ�����
            hasGameStarted = true;

            // ���÷�������
            NetworkManager.Instance?.SetRoomProperty(ROOM_STATE_KEY, (int)RoomState.Starting);
            NetworkManager.Instance?.SetRoomProperty(GAME_STARTED_KEY, true);

            // �ӳٴ�����Ϸ��ʼ�¼�
            StartCoroutine(DelayedGameStart());
        }

        /// <summary>
        /// �ӳٴ�����Ϸ��ʼ�¼�
        /// </summary>
        private IEnumerator DelayedGameStart()
        {
            LogDebug($"��Ϸ���� {gameStartDelay} ���ʼ");
            yield return new WaitForSeconds(gameStartDelay);

            LogDebug("������Ϸ��ʼ�¼� - ׼���л�����Ϸ����");
            OnGameStarting?.Invoke();
        }

        /// <summary>
        /// ����Ƿ���Կ�ʼ��Ϸ
        /// </summary>
        public bool CanStartGame()
        {
            return NetworkManager.Instance?.CanStartGame(minPlayersToStart) ?? false;
        }

        /// <summary>
        /// ��ȡ��Ϸ��ʼ�����������
        /// </summary>
        public string GetGameStartConditions()
        {
            return NetworkManager.Instance?.GetGameStartConditions(minPlayersToStart) ?? "NetworkManagerδ��ʼ��";
        }

        #endregion

        #region ����״̬����

        /// <summary>
        /// ���÷���Ϊ�ȴ�״̬
        /// </summary>
        private void SetRoomWaitingState()
        {
            if (!IsHost || NetworkManager.Instance == null) return;

            NetworkManager.Instance.SetRoomProperty(ROOM_STATE_KEY, (int)RoomState.Waiting);
            NetworkManager.Instance.SetRoomProperty(GAME_STARTED_KEY, false);
            LogDebug("����״̬����Ϊ�ȴ���");
        }

        /// <summary>
        /// ��ȡ����״̬
        /// </summary>
        public RoomState GetRoomState()
        {
            if (NetworkManager.Instance == null) return RoomState.Waiting;
            return (RoomState)NetworkManager.Instance.GetRoomProperty(ROOM_STATE_KEY, 0);
        }

        /// <summary>
        /// ��ȡ��Ϸ�Ƿ��ѿ�ʼ
        /// </summary>
        public bool GetGameStarted()
        {
            return NetworkManager.Instance?.GetRoomProperty(GAME_STARTED_KEY, false) ?? false;
        }

        #endregion

        #region �������

        /// <summary>
        /// �뿪���䲢���ش���
        /// </summary>
        public void LeaveRoomAndReturnToLobby()
        {
            if (!IsInRoom)
            {
                LogDebug("���ڷ����У�ֱ�ӷ��ش���");
                OnReturnToLobby?.Invoke();
                return;
            }

            LogDebug("�뿪���䲢���ش���");
            PhotonNetwork.LeaveRoom();
            ReturnToLobbyDelayed();
        }

        private void ReturnToLobbyDelayed()
        {
            LogDebug("ִ�з��ش���");

            bool switchSuccess = SceneTransitionManager.ReturnToMainMenu("RoomSceneController");

            if (!switchSuccess)
            {
                Debug.LogWarning("[RoomManager] SceneTransitionManager����ʧ�ܣ�ʹ�ñ��÷���");
                SceneManager.LoadScene("LobbyScene");
            }
        }

        /// <summary>
        /// �߳���ң�����ר�ã�
        /// </summary>
        public bool KickPlayer(Player player)
        {
            if (!IsHost)
            {
                Debug.LogError("[RoomManager] ֻ�з��������߳����");
                return false;
            }

            if (player.IsMasterClient)
            {
                Debug.LogError("[RoomManager] �����߳�����");
                return false;
            }

            LogDebug($"�����߳����: {player.NickName}");
            photonView.RPC("OnKickedFromRoom", player);
            return true;
        }

        [PunRPC]
        void OnKickedFromRoom()
        {
            LogDebug("�������߳�����");
            PhotonNetwork.LeaveRoom();
        }

        #endregion

        #region ��������

        /// <summary>
        /// ͨ�����ID��ȡPhoton Player����
        /// </summary>
        private Player GetPhotonPlayerById(ushort playerId)
        {
            if (!PhotonNetwork.InRoom) return null;
            foreach (var player in PhotonNetwork.PlayerList)
            {
                if (player.ActorNumber == playerId)
                    return player;
            }
            return null;
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[RoomManager] {message}");
            }
        }

        #endregion
    }

    #region ���ݽṹ����

    /// <summary>
    /// ����״̬ö��
    /// </summary>
    public enum RoomState
    {
        Waiting = 0,    // �ȴ����׼��
        Starting = 1,   // ׼����ʼ��Ϸ
        InGame = 2,     // ��Ϸ������
        Ended = 3       // ��Ϸ����
    }

    #endregion
}