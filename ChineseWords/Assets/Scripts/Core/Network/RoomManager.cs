using UnityEngine;
using System;
using UI;

namespace Core.Network
{
    /// <summary>
    /// ��������� - �������߼���״̬����
    /// �޸��汾��ʹ��ͳһ��Host ID�����ӳٷ��䴴��ֱ��Host�ͻ���׼������
    /// </summary>
    public class RoomManager : MonoBehaviour
    {
        [Header("��������")]
        [SerializeField] private int maxPlayersPerRoom = 4;
        [SerializeField] private bool enableDebugLogs = false;

        [Header("��Ϸ��ʼ����")]
        [SerializeField] private bool hasGameStarted = false; // ��ֹ�ظ���ʼ
        [SerializeField] private float gameStartDelay = 1f;

        public static RoomManager Instance { get; private set; }

        // ��������
        private RoomData currentRoom;
        private bool isHost;
        private bool isInitialized = false;

        // **���������䴴���ȴ�״̬**
        private bool isWaitingForHostReady = false;
        private string pendingRoomName = "";
        private string pendingPlayerName = "";

        // �¼�
        public static event Action<RoomData> OnRoomCreated;
        public static event Action<RoomData> OnRoomJoined;
        public static event Action<RoomPlayer> OnPlayerJoinedRoom;
        public static event Action<ushort> OnPlayerLeftRoom;
        public static event Action<ushort, bool> OnPlayerReadyChanged;
        public static event Action OnGameStarting; // Ψһ����Ϸ��ʼ�¼�
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

                // **�ؼ�����������Host���׼�������¼�**
                NetworkManager.OnHostPlayerReady += OnHostPlayerReady;
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
                NetworkManager.OnHostPlayerReady -= OnHostPlayerReady;
            }
        }

        #region �������

        /// <summary>
        /// �������䣨Host���ã�- �޸��汾���ӳٴ���ֱ��Host�ͻ���׼������
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

            LogDebug($"���󴴽�����: {roomName}, Host���: {playerName}");

            // **�ؼ��޸������Host�ͻ����Ƿ�׼������**
            if (!NetworkManager.Instance.IsHostClientReady)
            {
                LogDebug("Host�ͻ�����δ׼���������ȴ�������ɺ󴴽�����");

                // ����������ķ�����Ϣ
                isWaitingForHostReady = true;
                pendingRoomName = roomName;
                pendingPlayerName = playerName;

                return true; // ����true��ʾ�����ѽ��ܣ���ʵ�ʴ������ӳ�
            }

            // Host�ͻ�����׼��������������������
            return CreateRoomImmediate(roomName, playerName);
        }

        /// <summary>
        /// �����������䣨˽�з�����
        /// </summary>
        private bool CreateRoomImmediate(string roomName, string playerName)
        {
            // **ʹ��ͳһ��Host���ID�ӿ�**
            ushort hostPlayerId = NetworkManager.Instance.GetHostPlayerId();

            if (hostPlayerId == 0)
            {
                Debug.LogError("[RoomManager] Host���ID��Ч���޷���������");
                return false;
            }

            LogDebug($"��ʼ�������� - ������: {roomName}, Host���: {playerName}, Host ID: {hostPlayerId}");

            // ���ɷ������
            string roomCode = GenerateRoomCode();

            // **ʹ����ȷ��Host���ID��������**
            currentRoom = new RoomData(roomName, roomCode, hostPlayerId, maxPlayersPerRoom);
            isHost = true;

            // **ȷ��Host����ӵ�����б�**
            bool hostAdded = currentRoom.AddPlayer(hostPlayerId, playerName);

            if (!hostAdded)
            {
                Debug.LogError($"[RoomManager] �޷���Host��ӵ����䣺ID={hostPlayerId}, Name={playerName}");
                currentRoom = null;
                isHost = false;
                return false;
            }

            LogDebug($"���䴴���ɹ�: {roomName} (����: {roomCode})");
            LogDebug($"Host����ӵ�����б�: ID={hostPlayerId}, Name={playerName}");
            LogDebug($"��ǰ�����������: {currentRoom.players.Count}");

            // ������Ϸ��ʼ״̬
            hasGameStarted = false;

            // ����ȴ�״̬
            isWaitingForHostReady = false;
            pendingRoomName = "";
            pendingPlayerName = "";

            // �����¼�
            OnRoomCreated?.Invoke(currentRoom);

            // ��֤��������
            ValidateRoomDataAfterCreation();

            return true;
        }

        /// <summary>
        /// Host���׼�������¼�����
        /// </summary>
        private void OnHostPlayerReady()
        {
            LogDebug("Host��ҿͻ���׼������");

            // ������ڵȴ�Host׼����������������
            if (isWaitingForHostReady && !string.IsNullOrEmpty(pendingRoomName))
            {
                LogDebug($"Host׼�����������ڴ����ȴ��еķ���: {pendingRoomName}");
                CreateRoomImmediate(pendingRoomName, pendingPlayerName);
            }
        }

        /// <summary>
        /// ��֤���䴴��������ݣ������ã�
        /// </summary>
        private void ValidateRoomDataAfterCreation()
        {
            if (currentRoom == null)
            {
                Debug.LogError("[RoomManager] ��������Ϊ��");
                return;
            }

            LogDebug($"=== ���䴴������֤ ===");
            LogDebug($"������: {currentRoom.roomName}");
            LogDebug($"�������: {currentRoom.roomCode}");
            LogDebug($"Host ID: {currentRoom.hostId}");
            LogDebug($"�������: {currentRoom.players.Count}/{currentRoom.maxPlayers}");
            LogDebug($"��Host�����: {currentRoom.GetNonHostPlayerCount()}");

            // ���Host�Ƿ�������б���
            if (currentRoom.players.ContainsKey(currentRoom.hostId))
            {
                var hostPlayer = currentRoom.players[currentRoom.hostId];
                LogDebug($"Host�����Ϣ: {hostPlayer.playerName} (isHost: {hostPlayer.isHost})");
            }
            else
            {
                Debug.LogError("[RoomManager] Host��������б��У�");
            }

            // **��֤��NetworkManager��һ����**
            if (NetworkManager.Instance != null)
            {
                ushort networkHostId = NetworkManager.Instance.GetHostPlayerId();
                if (currentRoom.hostId != networkHostId)
                {
                    Debug.LogError($"[RoomManager] Host ID��һ�£�������: {currentRoom.hostId}, NetworkManager��: {networkHostId}");
                }
                else
                {
                    LogDebug($"Host IDһ������֤ͨ��: {currentRoom.hostId}");
                }
            }
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
            hasGameStarted = false;  // ������Ϸ��ʼ״̬

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

            // ����״̬
            currentRoom = null;
            isHost = false;
            hasGameStarted = false;
            isWaitingForHostReady = false;
            pendingRoomName = "";
            pendingPlayerName = "";

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
        /// ������Ϸ - ͳһ��ڵ㣨�޸��汾��
        /// **������Ϸ��ʼ��Ψһ��ڣ������������ö�Ӧ��ͨ������**
        /// </summary>
        public void StartGame()
        {
            // ��ֹ�ظ�����
            if (hasGameStarted)
            {
                LogDebug("��Ϸ�Ѿ���ʼ�������ظ�����");
                return;
            }

            if (!IsHost)
            {
                Debug.LogError("[RoomManager] ֻ�з�������������Ϸ");
                return;
            }

            if (!CanStartGame())
            {
                Debug.LogError("[RoomManager] ��Ϸ��������������");
                LogDebug($"��ϸ��� - ����״̬: {GetRoomStatusInfo()}");
                return;
            }

            LogDebug("����������Ϸ��ʼ");

            // ���ñ�־��ֹ�ظ�����
            hasGameStarted = true;

            // ���·���״̬
            if (currentRoom != null)
            {
                currentRoom.state = RoomState.Starting;
                LogDebug($"����״̬�Ѹ���Ϊ: {currentRoom.state}");
            }

            // �㲥��Ϸ��ʼ��Ϣ�����пͻ���
            if (NetworkManager.Instance != null)
            {
                LogDebug("�㲥��Ϸ��ʼ��Ϣ");
                NetworkManager.Instance.BroadcastGameStart();
            }

            // �ӳٴ�����Ϸ��ʼ�¼�����������Ϣ����ʱ�䣩
            Invoke(nameof(TriggerGameStartingEvent), gameStartDelay);
        }

        /// <summary>
        /// ������Ϸ��ʼ�¼� - ˽�з���
        /// </summary>
        private void TriggerGameStartingEvent()
        {
            LogDebug("���� OnGameStarting �¼� - �⽫��RoomSceneController����ִ�г����л�");

            // ������Ϸ��ʼ�¼� - ��ᱻRoomSceneController����ִ�г����л�
            OnGameStarting?.Invoke();
        }

        /// <summary>
        /// ������Ϸ��ʼ���� - ��NetworkManager����
        /// **�޸ģ�ͳһ·�ɵ�StartGame����������������㲥**
        /// </summary>
        public void OnNetworkGameStart()
        {
            LogDebug("�յ�������Ϸ��ʼ��Ϣ");

            // ��ֹ�ظ�����
            if (hasGameStarted)
            {
                LogDebug("��Ϸ�Ѿ���ʼ������������Ϣ");
                return;
            }

            // �ͻ����յ���Ϸ��ʼ��Ϣ
            if (!IsHost)
            {
                LogDebug("�ͻ����յ���Ϸ��ʼ֪ͨ");
                hasGameStarted = true;

                // ���·���״̬
                if (currentRoom != null)
                {
                    currentRoom.state = RoomState.Starting;
                }

                // ֱ�Ӵ�����Ϸ��ʼ�¼�
                TriggerGameStartingEvent();
            }
            else
            {
                // Host���յ��Լ��Ĺ㲥��Ϣ������
                LogDebug("Host�յ��Լ��Ĺ㲥��Ϣ�����Դ���");
            }
        }

        #endregion

        #region �����¼�����

        private void OnNetworkHostStarted()
        {
            LogDebug("����Host���������������㣩");
        }

        private void OnNetworkPlayerJoined(ushort playerId)
        {
            if (!IsInRoom || !isHost)
                return;

            // **ʹ��ͳһ�ӿڼ���Ƿ�ΪHost���**
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsHostPlayer(playerId))
            {
                LogDebug($"����Host�Լ����������: {playerId}");
                return;
            }

            // Hostģʽ�£�Ϊ�¼������Ҵ�����������
            string playerName = $"���{playerId}";
            bool success = currentRoom.AddPlayer(playerId, playerName);

            if (success)
            {
                LogDebug($"��� {playerId} ���뷿��");
                LogDebug($"��ǰ���������: {currentRoom.players.Count}");

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
                // **ʹ��ͳһ�ӿڼ���Ƿ�ΪHost���**
                if (NetworkManager.Instance != null && NetworkManager.Instance.IsHostPlayer(playerId))
                {
                    LogDebug($"Host��ҶϿ�����: {playerId}");
                    // Host��ҶϿ����ӷ����Ƴ���ֻ���״̬
                    return;
                }

                // Host������������뿪
                bool success = currentRoom.RemovePlayer(playerId);
                if (success)
                {
                    LogDebug($"��� {playerId} �뿪����");
                    LogDebug($"��ǰ���������: {currentRoom.players.Count}");

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

                if (currentRoom != null && currentRoom.players.ContainsKey(playerId))
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

            // ������Ϸ��ʼ״̬����Ϊ�յ����µķ������ݣ�
            if (currentRoom.state == RoomState.Waiting)
            {
                hasGameStarted = false;
            }

            if (wasFirstSync)
            {
                // �״�ͬ�����������뷿���¼�
                LogDebug($"�״�ͬ���������ݣ������: {currentRoom.players.Count}");
                OnRoomJoined?.Invoke(currentRoom);
            }
            else
            {
                // ����ͬ����������Ҫ����UI
                LogDebug($"���·������ݣ������: {currentRoom.players.Count}");
            }
        }

        /// <summary>
        /// ����������Ҽ��루�ͻ��˵��ã�
        /// </summary>
        public void OnNetworkPlayerJoined(RoomPlayer player)
        {
            if (isHost || !IsInRoom) return;

            LogDebug($"������Ҽ���: {player.playerName} (ID: {player.playerId})");

            if (!currentRoom.players.ContainsKey(player.playerId))
            {
                currentRoom.players[player.playerId] = player;
                LogDebug($"�ͻ��˷������������Ϊ: {currentRoom.players.Count}");
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
                LogDebug($"�ͻ��˷������������Ϊ: {currentRoom.players.Count}");
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

        #endregion

        #region ״̬��ѯ����

        /// <summary>
        /// ����Ƿ���Կ�ʼ��Ϸ���޸��汾��
        /// </summary>
        public bool CanStartGame()
        {
            if (!IsHost)
            {
                LogDebug("ֻ�з������Կ�ʼ��Ϸ");
                return false;
            }

            if (currentRoom == null)
            {
                LogDebug("��������Ϊ��");
                return false;
            }

            if (hasGameStarted)
            {
                LogDebug("��Ϸ�Ѿ���ʼ");
                return false;
            }

            if (currentRoom.state != RoomState.Waiting)
            {
                LogDebug($"����״̬���ǵȴ���: {currentRoom.state}");
                return false;
            }

            // **���Host�ͻ����Ƿ�׼������**
            if (NetworkManager.Instance != null && !NetworkManager.Instance.IsHostClientReady)
            {
                LogDebug("Host�ͻ�����δ׼������");
                return false;
            }

            // ����Ƿ��зǷ������
            int nonHostPlayerCount = currentRoom.GetNonHostPlayerCount();
            if (nonHostPlayerCount == 0)
            {
                LogDebug("û��������ң��޷���ʼ��Ϸ");
                return false;
            }

            // ������зǷ�������Ƿ���׼��
            if (!currentRoom.AreAllPlayersReady())
            {
                int readyCount = currentRoom.GetReadyPlayerCount();
                LogDebug($"�������δ׼��: {readyCount}/{nonHostPlayerCount}");
                return false;
            }

            return true;
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
        /// ��ȡ����״̬��Ϣ
        /// </summary>
        public string GetRoomStatusInfo()
        {
            if (!IsInRoom)
                return "δ�ڷ�����";

            try
            {
                string hostInfo = "";
                if (NetworkManager.Instance != null)
                {
                    hostInfo = $", Host���ID: {NetworkManager.Instance.GetHostPlayerId()}, Host�ͻ��˾���: {NetworkManager.Instance.IsHostClientReady}";
                }

                return $"����: {currentRoom.roomName}, " +
                       $"״̬: {currentRoom.state}, " +
                       $"���: {currentRoom.players.Count}/{currentRoom.maxPlayers}, " +
                       $"׼��: {currentRoom.GetReadyPlayerCount()}/{currentRoom.GetNonHostPlayerCount()}, " +
                       $"�Ƿ���: {isHost}, " +
                       $"��Ϸ�ѿ�ʼ: {hasGameStarted}" +
                       hostInfo;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"��ȡ����״̬��Ϣʧ��: {e.Message}");
                return "����״̬�쳣";
            }
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
        /// ������Ϸ��ʼ״̬�����ڵ��Ի����ָ���
        /// </summary>
        public void ResetGameStartState()
        {
            LogDebug("������Ϸ��ʼ״̬");
            hasGameStarted = false;

            if (currentRoom != null)
            {
                currentRoom.state = RoomState.Waiting;
            }
        }

        #endregion

        #region ���Ժ���֤����

        /// <summary>
        /// ��֤��������������
        /// </summary>
        public bool ValidateRoomData()
        {
            if (!IsInRoom)
            {
                LogDebug("δ�ڷ����У��޷���֤");
                return false;
            }

            try
            {
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
                    Debug.LogWarning($"����(ID: {currentRoom.hostId})��������б���");
                    return false;
                }

                // ��֤������־
                var hostPlayer = currentRoom.players[currentRoom.hostId];
                if (!hostPlayer.isHost)
                {
                    Debug.LogWarning($"������ҵ�isHost��־Ϊfalse");
                    return false;
                }

                // **��֤��NetworkManager��Host IDһ����**
                if (NetworkManager.Instance != null)
                {
                    ushort networkHostId = NetworkManager.Instance.GetHostPlayerId();
                    if (currentRoom.hostId != networkHostId)
                    {
                        Debug.LogWarning($"����Host ID��NetworkManager��һ��: ����={currentRoom.hostId}, ����={networkHostId}");
                        return false;
                    }
                }

                LogDebug("����������֤ͨ��");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"����������֤�쳣: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// ��ȡ��ϸ�ĵ�����Ϣ
        /// </summary>
        public string GetDetailedDebugInfo()
        {
            var info = "=== �����������ϸ��Ϣ ===\n";
            info += $"�Ƿ�Ϊ����: {IsHost}\n";
            info += $"�Ƿ��ڷ�����: {IsInRoom}\n";
            info += $"��Ϸ�Ƿ��ѿ�ʼ: {hasGameStarted}\n";
            info += $"�Ƿ��ѳ�ʼ��: {isInitialized}\n";
            info += $"�ȴ�Host׼������: {isWaitingForHostReady}\n";

            if (isWaitingForHostReady)
            {
                info += $"������������: {pendingRoomName}\n";
                info += $"�����������: {pendingPlayerName}\n";
            }

            if (currentRoom != null)
            {
                try
                {
                    info += $"������: {currentRoom.roomName}\n";
                    info += $"�������: {currentRoom.roomCode}\n";
                    info += $"����״̬: {currentRoom.state}\n";
                    info += $"����ID: {currentRoom.hostId}\n";
                    info += $"�������: {currentRoom.players.Count}/{currentRoom.maxPlayers}\n";
                    info += $"�Ƿ��������: {currentRoom.GetNonHostPlayerCount()}\n";
                    info += $"׼�������: {currentRoom.GetReadyPlayerCount()}\n";
                    info += $"��������Ƿ�׼��: {currentRoom.AreAllPlayersReady()}\n";
                    info += $"���Կ�ʼ��Ϸ: {CanStartGame()}\n";
                    info += $"����������֤: {(ValidateRoomData() ? "ͨ��" : "ʧ��")}\n";

                    info += "����б�:\n";
                    foreach (var player in currentRoom.players.Values)
                    {
                        info += $"  - {player.playerName} (ID:{player.playerId}, Host:{player.isHost}, State:{player.state})\n";
                    }
                }
                catch (System.Exception e)
                {
                    info += $"��ȡ������Ϣʱ�����쳣: {e.Message}\n";
                }
            }
            else
            {
                info += "��ǰ����: null\n";
            }

            if (NetworkManager.Instance != null)
            {
                info += $"NetworkManager״̬: ����={NetworkManager.Instance.IsConnected}, Host={NetworkManager.Instance.IsHost}\n";
                info += $"NetworkManager ClientID={NetworkManager.Instance.ClientId}, HostPlayerID={NetworkManager.Instance.GetHostPlayerId()}\n";
                info += $"Host�ͻ��˾���: {NetworkManager.Instance.IsHostClientReady}\n";
            }
            else
            {
                info += "NetworkManager: null\n";
            }

            return info;
        }

        /// <summary>
        /// ���Է�������ʾ����״̬
        /// </summary>
        [ContextMenu("��ʾ����״̬")]
        public void ShowRoomStatus()
        {
            if (Application.isPlaying)
            {
                Debug.Log(GetDetailedDebugInfo());
            }
        }

        /// <summary>
        /// ���Է�����ǿ�ƿ�ʼ��Ϸ
        /// </summary>
        [ContextMenu("ǿ�ƿ�ʼ��Ϸ")]
        public void ForceStartGame()
        {
            if (Application.isPlaying)
            {
                LogDebug("ǿ�ƿ�ʼ��Ϸ�������ã�");
                ResetGameStartState();
                StartGame();
            }
        }

        /// <summary>
        /// ���Է�������֤��������
        /// </summary>
        [ContextMenu("��֤��������")]
        public void DebugValidateRoomData()
        {
            if (Application.isPlaying)
            {
                bool isValid = ValidateRoomData();
                Debug.Log($"����������֤���: {(isValid ? "ͨ��" : "ʧ��")}");
                if (!isValid)
                {
                    Debug.Log(GetDetailedDebugInfo());
                }
            }
        }

        /// <summary>
        /// ���Է���������Host IDһ����
        /// </summary>
        [ContextMenu("����Host IDһ����")]
        public void TestHostIdConsistency()
        {
            if (Application.isPlaying && IsInRoom && IsHost)
            {
                ushort roomHostId = currentRoom.hostId;
                ushort networkHostId = NetworkManager.Instance?.GetHostPlayerId() ?? 0;
                ushort clientId = NetworkManager.Instance?.ClientId ?? 0;

                Debug.Log($"=== Host ID һ���Բ��� ===");
                Debug.Log($"������Host ID: {roomHostId}");
                Debug.Log($"NetworkManager Host���ID: {networkHostId}");
                Debug.Log($"NetworkManager �ͻ���ID: {clientId}");
                Debug.Log($"һ���Լ��: {(roomHostId == networkHostId && networkHostId == clientId ? "ͨ��" : "ʧ��")}");
            }
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

        /// <summary>
        /// ���Է�����ǿ�ƴ������䣨���Եȴ�״̬��
        /// </summary>
        [ContextMenu("ǿ�ƴ�������")]
        public void ForceCreateRoom()
        {
            if (Application.isPlaying && isWaitingForHostReady)
            {
                LogDebug("ǿ�ƴ����ȴ��еķ���");
                CreateRoomImmediate(pendingRoomName, pendingPlayerName);
            }
        }

        #endregion

        private void OnDestroy()
        {
            // ȡ�������¼�����
            UnsubscribeFromNetworkEvents();

            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}