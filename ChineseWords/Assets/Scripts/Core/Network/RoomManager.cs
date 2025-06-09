using UnityEngine;
using System;
using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Core.Network
{
    /// <summary>
    /// ��������� - RoomSceneר�ð�
    /// ְ�𣺹����Ѽ��뷿������״̬��׼�����ơ���Ϸ��ʼ
    /// �����𷿼䴴��/���루��LobbyScene����
    /// </summary>
    public class RoomManager : MonoBehaviourPun, IInRoomCallbacks
    {
        [Header("��Ϸ��ʼ����")]
        [SerializeField] private float gameStartDelay = 2f;
        [SerializeField] private int minPlayersToStart = 2;

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        public static RoomManager Instance { get; private set; }

        // Photon�������Լ�������
        private const string ROOM_STATE_KEY = "roomState";
        private const string GAME_STARTED_KEY = "gameStarted";

        // ������Լ�������
        private const string PLAYER_READY_KEY = "isReady";

        // RoomSceneר���¼�
        public static event Action OnRoomEntered;           // ����RoomSceneʱ����
        public static event Action<Player> OnPlayerJoinedRoom;
        public static event Action<Player> OnPlayerLeftRoom;
        public static event Action<Player, bool> OnPlayerReadyChanged;
        public static event Action OnAllPlayersReady;       // �������׼������
        public static event Action OnGameStarting;          // ��Ϸ������ʼ
        public static event Action OnReturnToLobby;         // ���ش���

        // ״̬���� - ֱ��ʹ��Photon API
        public bool IsHost => PhotonNetwork.IsMasterClient;
        public bool IsInRoom => PhotonNetwork.InRoom;
        public string RoomName => PhotonNetwork.CurrentRoom?.Name ?? "";
        public int PlayerCount => PhotonNetwork.CurrentRoom?.PlayerCount ?? 0;
        public int MaxPlayers => PhotonNetwork.CurrentRoom?.MaxPlayers ?? 0;
        public bool IsInitialized => isInitialized;

        // RoomScene״̬
        private bool isInitialized = false;
        private bool hasGameStarted = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                LogDebug("RoomManager (RoomScene��) ��ʼ��");
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

        /// <summary>
        /// ��ʼ�����������
        /// </summary>
        private IEnumerator InitializeRoomManager()
        {
            // �ȴ�ȷ���ڷ�����
            while (!PhotonNetwork.InRoom)
            {
                LogDebug("�ȴ�����Photon����...");
                yield return new WaitForSeconds(0.1f);
            }

            // ע��Photon�ص�
            PhotonNetwork.AddCallbackTarget(this);

            // ��֤����״̬
            if (!ValidateRoomState())
            {
                Debug.LogError("[RoomManager] ����״̬��֤ʧ��");
                yield break;
            }

            // ������Ϸ��ʼ��־
            hasGameStarted = GetGameStarted();

            isInitialized = true;
            LogDebug($"RoomManager��ʼ����� - ����: {RoomName}, �����: {PlayerCount}, �Ƿ���: {IsHost}");

            // ������������¼�
            OnRoomEntered?.Invoke();

            // ����Ƿ�����ȷ�����䴦�ڵȴ�״̬
            if (IsHost && !hasGameStarted)
            {
                SetRoomWaitingState();
            }
        }

        private void OnDestroy()
        {
            // �Ƴ�Photon�ص�
            if (PhotonNetwork.NetworkingClient != null)
            {
                PhotonNetwork.RemoveCallbackTarget(this);
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

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

            // ʹ��Photon�������
            var props = new Hashtable { { PLAYER_READY_KEY, ready } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);

            return true;
        }

        /// <summary>
        /// ��ȡָ����ҵ�׼��״̬
        /// </summary>
        public bool GetPlayerReady(Player player)
        {
            if (player?.CustomProperties?.TryGetValue(PLAYER_READY_KEY, out object isReady) == true)
            {
                return (bool)isReady;
            }
            return false;
        }

        /// <summary>
        /// ��ȡ������ҵ�׼��״̬
        /// </summary>
        public bool GetMyReadyState()
        {
            return GetPlayerReady(PhotonNetwork.LocalPlayer);
        }

        /// <summary>
        /// ��ȡ׼�����������������������������
        /// </summary>
        public int GetReadyPlayerCount()
        {
            if (!IsInRoom) return 0;

            int readyCount = 0;
            foreach (var player in PhotonNetwork.PlayerList)
            {
                if (!player.IsMasterClient && GetPlayerReady(player))
                {
                    readyCount++;
                }
            }
            return readyCount;
        }

        /// <summary>
        /// ��ȡ�Ƿ����������
        /// </summary>
        public int GetNonHostPlayerCount()
        {
            if (!IsInRoom) return 0;

            int nonHostCount = 0;
            foreach (var player in PhotonNetwork.PlayerList)
            {
                if (!player.IsMasterClient)
                {
                    nonHostCount++;
                }
            }
            return nonHostCount;
        }

        /// <summary>
        /// ������зǷ�������Ƿ���׼��
        /// </summary>
        public bool AreAllPlayersReady()
        {
            if (!IsInRoom) return false;

            int nonHostPlayers = GetNonHostPlayerCount();
            if (nonHostPlayers == 0) return false;

            int readyPlayers = GetReadyPlayerCount();
            bool allReady = readyPlayers == nonHostPlayers;

            return allReady;
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

            // ʹ�÷������Թ㲥��Ϸ��ʼ
            var roomProps = new Hashtable
            {
                { ROOM_STATE_KEY, (int)RoomState.Starting },
                { GAME_STARTED_KEY, true }
            };

            PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);

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
            if (!IsHost) return false;
            if (!IsInRoom || !isInitialized) return false;
            if (hasGameStarted) return false;

            // �����С�����
            if (PlayerCount < minPlayersToStart)
            {
                return false;
            }

            // ��鷿��״̬
            if (GetRoomState() != RoomState.Waiting)
            {
                return false;
            }

            // ������зǷ�������Ƿ���׼��
            return AreAllPlayersReady();
        }

        /// <summary>
        /// ��ȡ��Ϸ��ʼ�����������
        /// </summary>
        public string GetGameStartConditions()
        {
            if (!IsHost) return "���Ƿ���";
            if (!IsInRoom || !isInitialized) return "����δ��ʼ��";
            if (hasGameStarted) return "��Ϸ�ѿ�ʼ";

            if (PlayerCount < minPlayersToStart)
                return $"��������� ({PlayerCount}/{minPlayersToStart})";

            if (GetRoomState() != RoomState.Waiting)
                return $"����״̬���� ({GetRoomState()})";

            int readyCount = GetReadyPlayerCount();
            int nonHostCount = GetNonHostPlayerCount();

            if (!AreAllPlayersReady())
                return $"���δȫ��׼�� ({readyCount}/{nonHostCount})";

            return "���㿪ʼ����";
        }

        #endregion

        #region ����״̬����

        /// <summary>
        /// ���÷���Ϊ�ȴ�״̬
        /// </summary>
        private void SetRoomWaitingState()
        {
            if (!IsHost) return;

            var roomProps = new Hashtable
            {
                { ROOM_STATE_KEY, (int)RoomState.Waiting },
                { GAME_STARTED_KEY, false }
            };

            PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);
            LogDebug("����״̬����Ϊ�ȴ���");
        }

        /// <summary>
        /// ��ȡ����״̬
        /// </summary>
        public RoomState GetRoomState()
        {
            if (!IsInRoom) return RoomState.Waiting;

            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ROOM_STATE_KEY, out object state))
            {
                return (RoomState)state;
            }
            return RoomState.Waiting;
        }

        /// <summary>
        /// ��ȡ��Ϸ�Ƿ��ѿ�ʼ
        /// </summary>
        public bool GetGameStarted()
        {
            if (!IsInRoom) return false;

            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(GAME_STARTED_KEY, out object started))
            {
                return (bool)started;
            }
            return false;
        }

        /// <summary>
        /// ��֤����״̬
        /// </summary>
        private bool ValidateRoomState()
        {
            if (!PhotonNetwork.InRoom)
            {
                Debug.LogError("[RoomManager] ����Photon������");
                return false;
            }

            if (PhotonNetwork.CurrentRoom == null)
            {
                Debug.LogError("[RoomManager] ��ǰ����Ϊ��");
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

            // Photon���߳������Ҫͨ���رշ����������ʽ
            // �������ʹ���Զ���RPC֪ͨ���߳������
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

        #region Photon�ص�ʵ��

        void IInRoomCallbacks.OnPlayerEnteredRoom(Player newPlayer)
        {
            LogDebug($"��Ҽ��뷿��: {newPlayer.NickName} (ID: {newPlayer.ActorNumber})");
            OnPlayerJoinedRoom?.Invoke(newPlayer);

            // ������������Է��ͻ�ӭ��Ϣ�򷿼�״̬
            if (IsHost)
            {
                LogDebug($"��ǰ���������: {PlayerCount}/{MaxPlayers}");
            }
        }

        void IInRoomCallbacks.OnPlayerLeftRoom(Player otherPlayer)
        {
            LogDebug($"����뿪����: {otherPlayer.NickName} (ID: {otherPlayer.ActorNumber})");
            OnPlayerLeftRoom?.Invoke(otherPlayer);

            // ����Ƿ��ܼ�����Ϸ
            if (hasGameStarted && PlayerCount < minPlayersToStart)
            {
                LogDebug("����������㣬��Ϸ������Ҫ��ͣ�����");
            }
        }

        void IInRoomCallbacks.OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            // �������׼��״̬�仯
            if (changedProps.TryGetValue(PLAYER_READY_KEY, out object isReadyObj))
            {
                bool isReady = (bool)isReadyObj;
                LogDebug($"���׼��״̬����: {targetPlayer.NickName} -> {isReady}");
                OnPlayerReadyChanged?.Invoke(targetPlayer, isReady);

                // ����Ƿ�������Ҷ���׼��
                if (AreAllPlayersReady() && GetNonHostPlayerCount() > 0)
                {
                    LogDebug("������Ҷ���׼������");
                    OnAllPlayersReady?.Invoke();
                }
            }
        }

        void IInRoomCallbacks.OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
            // ������Ϸ��ʼ
            if (propertiesThatChanged.TryGetValue(GAME_STARTED_KEY, out object gameStartedObj))
            {
                bool gameStarted = (bool)gameStartedObj;
                if (gameStarted && !IsHost) // �ͻ����յ���Ϸ��ʼ֪ͨ
                {
                    LogDebug("�ͻ����յ���Ϸ��ʼ֪ͨ");
                    hasGameStarted = true;
                    StartCoroutine(DelayedGameStart());
                }
            }

            // ������״̬�仯
            if (propertiesThatChanged.TryGetValue(ROOM_STATE_KEY, out object roomStateObj))
            {
                RoomState newState = (RoomState)roomStateObj;
                LogDebug($"����״̬����: {newState}");
            }
        }

        void IInRoomCallbacks.OnMasterClientSwitched(Player newMasterClient)
        {
            LogDebug($"�����л���: {newMasterClient.NickName} (ID: {newMasterClient.ActorNumber})");

            // ���������ҳ�Ϊ�·���
            if (newMasterClient.IsLocal)
            {
                LogDebug("������ҳ�Ϊ�·��������÷���״̬");

                // ���÷���״̬
                if (!hasGameStarted)
                {
                    SetRoomWaitingState();
                }
            }
        }

        #endregion

        #region ״̬��ѯ

        /// <summary>
        /// ��ȡ����״̬��Ϣ
        /// </summary>
        public string GetRoomStatusInfo()
        {
            if (!IsInRoom)
                return "δ�ڷ�����";

            return $"����: {RoomName}, " +
                   $"״̬: {GetRoomState()}, " +
                   $"���: {PlayerCount}/{MaxPlayers}, " +
                   $"׼��: {GetReadyPlayerCount()}/{GetNonHostPlayerCount()}, " +
                   $"�Ƿ���: {IsHost}, " +
                   $"��Ϸ�ѿ�ʼ: {hasGameStarted}, " +
                   $"���Կ�ʼ: {CanStartGame()}";
        }

        /// <summary>
        /// ��ȡ����б���Ϣ
        /// </summary>
        public string GetPlayerListInfo()
        {
            if (!IsInRoom) return "δ�ڷ�����";

            string info = "����б�:\n";
            foreach (var player in PhotonNetwork.PlayerList)
            {
                bool isReady = GetPlayerReady(player);
                info += $"  - {player.NickName} (ID: {player.ActorNumber}) ";
                info += $"[{(player.IsMasterClient ? "����" : "���")}] ";

                if (!player.IsMasterClient)
                {
                    info += $"[{(isReady ? "��׼��" : "δ׼��")}]";
                }

                info += "\n";
            }
            return info;
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
                Debug.Log($"[RoomManager] {message}");
            }
        }

        #endregion

        #region ���Է���

        [ContextMenu("��ʾ����״̬")]
        public void ShowRoomStatus()
        {
            if (Application.isPlaying)
            {
                Debug.Log("=== RoomScene����״̬ ===");
                Debug.Log(GetRoomStatusInfo());
                Debug.Log(GetPlayerListInfo());
                Debug.Log($"��Ϸ��ʼ����: {GetGameStartConditions()}");
            }
        }

        [ContextMenu("ǿ�ƿ�ʼ��Ϸ")]
        public void ForceStartGame()
        {
            if (Application.isPlaying && IsHost)
            {
                LogDebug("ǿ�ƿ�ʼ��Ϸ�������ã�");
                hasGameStarted = false; // ����״̬
                StartGame();
            }
        }

        [ContextMenu("�л�׼��״̬")]
        public void ToggleReadyState()
        {
            if (Application.isPlaying && IsInRoom && !IsHost)
            {
                bool currentReady = GetMyReadyState();
                SetPlayerReady(!currentReady);
                LogDebug($"�л�׼��״̬: {currentReady} -> {!currentReady}");
            }
        }

        [ContextMenu("���÷���״̬")]
        public void ResetRoomState()
        {
            if (Application.isPlaying && IsHost)
            {
                hasGameStarted = false;
                SetRoomWaitingState();
                LogDebug("����״̬������");
            }
        }

        [ContextMenu("���ش���")]
        public void ReturnToLobby()
        {
            if (Application.isPlaying)
            {
                LeaveRoomAndReturnToLobby();
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