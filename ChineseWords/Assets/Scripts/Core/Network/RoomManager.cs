using UnityEngine;
using System;
using UI;

namespace Core.Network
{
    /// <summary>
    /// ��������� - �������߼���״̬����
    /// </summary>
    public class RoomManager : MonoBehaviour
    {
        [Header("��������")]
        [SerializeField] private int maxPlayersPerRoom = 4;
        [SerializeField] private bool enableDebugLogs = true;

        public static RoomManager Instance { get; private set; }

        // ��������
        private RoomData currentRoom;
        private bool isHost;
        private bool isInitialized = false;

        // �¼�
        public static event Action<RoomData> OnRoomCreated;
        public static event Action<RoomData> OnRoomJoined;
        public static event Action<RoomPlayer> OnPlayerJoinedRoom;
        public static event Action<ushort> OnPlayerLeftRoom;
        public static event Action<ushort, bool> OnPlayerReadyChanged;
        public static event Action OnGameStarting;
        public static event Action OnRoomLeft;

        // ����
        public RoomData CurrentRoom => currentRoom;
        public bool IsHost => isHost;
        public bool IsInRoom => currentRoom != null;
        public bool IsInitialized => isInitialized;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LogDebug("RoomManager ��ʼ��");
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            // ���������¼�
            SubscribeToNetworkEvents();
            isInitialized = true;
        }

        private void SubscribeToNetworkEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnHostStarted += OnNetworkHostStarted;
                NetworkManager.OnPlayerJoined += OnNetworkPlayerJoined;
                NetworkManager.OnPlayerLeft += OnNetworkPlayerLeft;
                NetworkManager.OnDisconnected += OnNetworkDisconnected;
            }
        }

        private void UnsubscribeFromNetworkEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnHostStarted -= OnNetworkHostStarted;
                NetworkManager.OnPlayerJoined -= OnNetworkPlayerJoined;
                NetworkManager.OnPlayerLeft -= OnNetworkPlayerLeft;
                NetworkManager.OnDisconnected -= OnNetworkDisconnected;
            }
        }

        #region �������

        /// <summary>
        /// �������䣨Host���ã�
        /// </summary>
        public bool CreateRoom(string roomName, string playerName)
        {
            if (IsInRoom)
            {
                LogDebug("���ڷ����У��޷������·���");
                return false;
            }

            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsHost)
            {
                LogDebug("����Hostģʽ���޷���������");
                return false;
            }

            // ���ɷ������
            string roomCode = GenerateRoomCode();
            ushort hostId = NetworkManager.Instance.ClientId;

            // ������������
            currentRoom = new RoomData(roomName, roomCode, hostId, maxPlayersPerRoom);
            isHost = true;

            // ��ӷ���������
            currentRoom.AddPlayer(hostId, playerName);

            LogDebug($"���䴴���ɹ�: {roomName} (����: {roomCode})");

            // �����¼�
            OnRoomCreated?.Invoke(currentRoom);

            return true;
        }

        /// <summary>
        /// ���뷿�䣨Client���ã�
        /// </summary>
        public bool JoinRoom(string playerName)
        {
            if (IsInRoom)
            {
                LogDebug("���ڷ�����");
                return false;
            }

            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsConnected)
            {
                LogDebug("δ���ӵ����磬�޷����뷿��");
                return false;
            }

            isHost = false;

            LogDebug($"������뷿��: ��� {playerName}");

            // �ͻ������Ӻ��Զ����󷿼���Ϣ
            NetworkManager.Instance.RequestRoomInfo();

            return true;
        }

        /// <summary>
        /// �뿪����
        /// </summary>
        public void LeaveRoom()
        {
            if (!IsInRoom)
                return;

            LogDebug("�뿪����");

            currentRoom = null;
            isHost = false;

            // �����¼�
            OnRoomLeft?.Invoke();
        }

        /// <summary>
        /// �������׼��״̬
        /// </summary>
        public bool SetPlayerReady(bool ready)
        {
            if (!IsInRoom || NetworkManager.Instance == null)
                return false;

            ushort playerId = NetworkManager.Instance.ClientId;

            // ��������Ҫ׼��״̬
            if (isHost)
            {
                LogDebug("��������Ҫ����׼��״̬");
                return false;
            }

            // �����������������ֱ���޸ı���״̬
            NetworkManager.Instance.RequestReadyStateChange(ready);
            return true;
        }

        /// <summary>
        /// ��ʼ��Ϸ���������ɵ��ã�
        /// </summary>
        public bool StartGame()
        {
            if (!IsInRoom || !isHost)
            {
                LogDebug("ֻ�з������Կ�ʼ��Ϸ");
                return false;
            }

            if (currentRoom.state != RoomState.Waiting)
            {
                LogDebug($"����״̬����ȷ: {currentRoom.state}");
                return false;
            }

            if (currentRoom.players.Count < 2)
            {
                LogDebug("������Ҫ2����Ҳ��ܿ�ʼ��Ϸ");
                return false;
            }

            if (!currentRoom.AreAllPlayersReady())
            {
                LogDebug("�������δ׼��");
                return false;
            }

            // ���÷���״̬Ϊ��ʼ��
            currentRoom.state = RoomState.Starting;

            LogDebug("��ʼ��Ϸ");

            // ͨ������㲥��Ϸ��ʼ
            NetworkManager.Instance.BroadcastGameStart();

            // ���������¼�
            OnGameStarting?.Invoke();

            return true;
        }

        #endregion

        #region �����¼�����

        private void OnNetworkHostStarted()
        {
            LogDebug("����Host������");
        }

        private void OnNetworkPlayerJoined(ushort playerId)
        {
            if (!IsInRoom || !isHost)
                return;

            // ����Ƿ���Host�Լ��Ŀͻ������ӣ�����������
            if (NetworkManager.Instance != null && playerId == NetworkManager.Instance.ClientId)
            {
                LogDebug($"����Host�Լ��Ŀͻ�������: {playerId}");
                return;
            }

            // Hostģʽ�£�Ϊ�¼������Ҵ�����������
            string playerName = $"���{playerId}";
            bool success = currentRoom.AddPlayer(playerId, playerName);

            if (success)
            {
                LogDebug($"��� {playerId} ���뷿��");

                // �㲥�����пͻ���
                NetworkManager.Instance.BroadcastPlayerJoinRoom(currentRoom.players[playerId]);

                // ���������������ݸ������
                NetworkManager.Instance.SendRoomDataToClient(playerId, currentRoom);

                // ���������¼�
                OnPlayerJoinedRoom?.Invoke(currentRoom.players[playerId]);
            }
            else
            {
                LogDebug($"��� {playerId} ����ʧ�ܣ������������ظ���");
            }
        }

        private void OnNetworkPlayerLeft(ushort playerId)
        {
            if (!IsInRoom)
                return;

            if (isHost)
            {
                // Host��������뿪
                bool success = currentRoom.RemovePlayer(playerId);
                if (success)
                {
                    LogDebug($"��� {playerId} �뿪����");

                    // �㲥�������ͻ���
                    NetworkManager.Instance.BroadcastPlayerLeaveRoom(playerId);

                    // ���������¼�
                    OnPlayerLeftRoom?.Invoke(playerId);
                }
            }
            else
            {
                // Client��������뿪֪ͨ
                LogDebug($"��������뿪: {playerId}");

                if (currentRoom.players.ContainsKey(playerId))
                {
                    currentRoom.players.Remove(playerId);
                    OnPlayerLeftRoom?.Invoke(playerId);
                }
            }
        }

        private void OnNetworkDisconnected()
        {
            LogDebug("����Ͽ����뿪����");
            LeaveRoom();
        }

        #endregion

        #region ����ͬ����������NetworkManager���ã�

        /// <summary>
        /// ��������·������ݣ��ͻ��˵��ã�
        /// </summary>
        public void UpdateRoomFromNetwork(RoomData networkRoomData)
        {
            if (isHost) return; // Host���������緿������

            LogDebug($"��������·�������: {networkRoomData.roomName}");

            bool wasFirstSync = currentRoom == null;
            currentRoom = networkRoomData;

            if (wasFirstSync)
            {
                // �״�ͬ�����������뷿���¼�
                OnRoomJoined?.Invoke(currentRoom);
            }
            else
            {
                // ����ͬ����������Ҫ����UI
                // �������������������߼�
            }
        }

        /// <summary>
        /// ����������Ҽ��루�ͻ��˵��ã�
        /// </summary>
        public void OnNetworkPlayerJoined(RoomPlayer player)
        {
            if (isHost || !IsInRoom) return;

            LogDebug($"������Ҽ���: {player.playerName}");

            if (!currentRoom.players.ContainsKey(player.playerId))
            {
                currentRoom.players[player.playerId] = player;
                OnPlayerJoinedRoom?.Invoke(player);
            }
        }

        /// <summary>
        /// ������������뿪����NetworkManager���ã�����������Ϣ��
        /// </summary>
        public void OnNetworkPlayerLeftMessage(ushort playerId)
        {
            if (isHost || !IsInRoom) return;

            LogDebug($"�յ���������뿪��Ϣ: {playerId}");

            if (currentRoom.players.ContainsKey(playerId))
            {
                currentRoom.players.Remove(playerId);
                OnPlayerLeftRoom?.Invoke(playerId);
            }
        }

        /// <summary>
        /// �����������׼��״̬�仯
        /// </summary>
        public void OnNetworkPlayerReadyChanged(ushort playerId, bool isReady)
        {
            if (!IsInRoom) return;

            LogDebug($"�������׼��״̬�仯: {playerId} -> {isReady}");

            if (currentRoom.players.ContainsKey(playerId))
            {
                currentRoom.SetPlayerReady(playerId, isReady);
                OnPlayerReadyChanged?.Invoke(playerId, isReady);
            }
        }

        /// <summary>
        /// ����������Ϸ��ʼ����
        /// </summary>
        public void OnNetworkGameStart()
        {
            if (!IsInRoom) return;

            LogDebug("�յ�������Ϸ��ʼ����");

            currentRoom.state = RoomState.Starting;
            OnGameStarting?.Invoke();
        }

        #endregion

        #region ��������

        /// <summary>
        /// ���ɷ������
        /// </summary>
        private string GenerateRoomCode()
        {
            return UnityEngine.Random.Range(100000, 999999).ToString();
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

        /// <summary>
        /// ��ȡ����״̬��Ϣ
        /// </summary>
        public string GetRoomStatusInfo()
        {
            if (!IsInRoom)
                return "δ�ڷ�����";

            return $"����: {currentRoom.roomName}, " +
                   $"״̬: {currentRoom.state}, " +
                   $"���: {currentRoom.players.Count}/{currentRoom.maxPlayers}, " +
                   $"׼��: {currentRoom.GetReadyPlayerCount()}/{currentRoom.GetNonHostPlayerCount()}, " +
                   $"�Ƿ���: {isHost}";
        }

        /// <summary>
        /// ��ȡ��ǰ��ҵ�׼��״̬
        /// </summary>
        public bool GetMyReadyState()
        {
            if (!IsInRoom || NetworkManager.Instance == null)
                return false;

            ushort myId = NetworkManager.Instance.ClientId;
            if (currentRoom.players.ContainsKey(myId))
            {
                return currentRoom.players[myId].state == PlayerRoomState.Ready;
            }

            return false;
        }

        /// <summary>
        /// ����Ƿ���Կ�ʼ��Ϸ
        /// </summary>
        public bool CanStartGame()
        {
            if (!IsInRoom || !isHost)
                return false;

            return currentRoom.players.Count >= 2 &&
                   currentRoom.AreAllPlayersReady() &&
                   currentRoom.state == RoomState.Waiting;
        }

        /// <summary>
        /// ��ȡ����������б�
        /// </summary>
        public RoomPlayer[] GetPlayerList()
        {
            if (!IsInRoom)
                return new RoomPlayer[0];

            RoomPlayer[] players = new RoomPlayer[currentRoom.players.Count];
            int index = 0;
            foreach (var player in currentRoom.players.Values)
            {
                players[index++] = player;
            }

            return players;
        }

        /// <summary>
        /// ǿ��ˢ�·���״̬�������ã�
        /// </summary>
        [ContextMenu("ˢ�·���״̬")]
        public void RefreshRoomState()
        {
            if (IsInRoom)
            {
                LogDebug($"��ǰ����״̬: {GetRoomStatusInfo()}");
            }
            else
            {
                LogDebug("δ�ڷ�����");
            }
        }

        #endregion

        private void OnDestroy()
        {
            UnsubscribeFromNetworkEvents();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #region ���Ժ���֤����

        /// <summary>
        /// ��֤��������������
        /// </summary>
        public bool ValidateRoomData()
        {
            if (!IsInRoom)
                return false;

            // ��鷿�������Ϣ
            if (string.IsNullOrEmpty(currentRoom.roomName) ||
                string.IsNullOrEmpty(currentRoom.roomCode))
            {
                Debug.LogWarning("���������Ϣ������");
                return false;
            }

            // ����������
            if (currentRoom.players.Count == 0)
            {
                Debug.LogWarning("����û�����");
                return false;
            }

            // ��鷿���Ƿ����
            if (!currentRoom.players.ContainsKey(currentRoom.hostId))
            {
                Debug.LogWarning("������������б���");
                return false;
            }

            return true;
        }

        /// <summary>
        /// ��ȡ��ϸ�ĵ�����Ϣ
        /// </summary>
        public string GetDetailedDebugInfo()
        {
            if (!IsInRoom)
                return "δ�ڷ�����";

            string info = $"=== ������ϸ��Ϣ ===\n";
            info += $"������: {currentRoom.roomName}\n";
            info += $"�������: {currentRoom.roomCode}\n";
            info += $"����״̬: {currentRoom.state}\n";
            info += $"����ID: {currentRoom.hostId}\n";
            info += $"��������: {currentRoom.maxPlayers}\n";
            info += $"��ǰ�����: {currentRoom.players.Count}\n";
            info += $"׼�������: {currentRoom.GetReadyPlayerCount()}\n";
            info += $"�Ƿ���: {isHost}\n";
            info += $"����״̬: {(NetworkManager.Instance?.IsConnected ?? false)}\n";
            info += $"����б�:\n";

            foreach (var player in currentRoom.players.Values)
            {
                info += $"  - {player.playerName} (ID:{player.playerId}) ";
                info += $"[{(player.isHost ? "����" : "���")}] ";
                info += $"[{player.state}]\n";
            }

            return info;
        }

        #endregion
    }
}