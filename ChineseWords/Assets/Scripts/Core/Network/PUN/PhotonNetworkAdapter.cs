using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Core.Network;

namespace Core.Network
{
    /// <summary>
    /// �־û�Photon����������
    /// רע��Photon���ӹ�������ʹ��DontDestroyOnLoad����PersistentNetworkManager����
    /// </summary>
    public class PhotonNetworkAdapter : MonoBehaviourPun, IConnectionCallbacks, IMatchmakingCallbacks, ILobbyCallbacks
    {
        [Header("Photon����")]
        [SerializeField] private string gameVersion = "0.4.4";
        [SerializeField] private bool enableDebugLogs = true;

        // �Ƴ�����ģʽ����Ϊͨ��PersistentNetworkManager����
        public static PhotonNetworkAdapter Instance => PersistentNetworkManager.Instance?.PhotonAdapter;

        #region �¼�ϵͳ

        // ��������¼�
        public static event System.Action OnPhotonConnected;
        public static event System.Action OnPhotonDisconnected;

        // ��������¼�
        public static event System.Action OnPhotonRoomCreated;
        public static event System.Action OnPhotonRoomJoined;
        public static event System.Action OnPhotonRoomLeft;

        // Host����¼�
        public static event System.Action OnPhotonHostStarted;
        public static event System.Action OnPhotonHostStopped;

        // �������¼�
        public static event System.Action<ushort> OnPhotonPlayerJoined;
        public static event System.Action<ushort> OnPhotonPlayerLeft;

        // Lobby����¼�
        public static event System.Action OnPhotonJoinedLobby;
        public static event System.Action OnPhotonLeftLobby;
        public static event System.Action<List<RoomInfo>> OnPhotonRoomListUpdate;

        #endregion

        #region ״̬����

        // Photonר��״̬
        public bool IsPhotonConnected => PhotonNetwork.IsConnected;
        public bool IsInPhotonRoom => PhotonNetwork.InRoom;
        public bool IsPhotonMasterClient => PhotonNetwork.IsMasterClient;
        public ushort PhotonClientId => PhotonNetwork.LocalPlayer?.ActorNumber != null ? (ushort)PhotonNetwork.LocalPlayer.ActorNumber : (ushort)0;

        // ������Ϣ
        public string CurrentRoomName => PhotonNetwork.CurrentRoom?.Name ?? "";
        public int CurrentRoomPlayerCount => PhotonNetwork.CurrentRoom?.PlayerCount ?? 0;
        public int CurrentRoomMaxPlayers => PhotonNetwork.CurrentRoom?.MaxPlayers ?? 0;
        public ushort PhotonMasterClientId => PhotonNetwork.InRoom ? (ushort)PhotonNetwork.CurrentRoom.MasterClientId : (ushort)0;
        public bool IsInPhotonLobby => PhotonNetwork.InLobby;
        public int PhotonRoomCount => PhotonNetwork.CountOfRooms;
        public int PhotonPlayerCount => PhotonNetwork.CountOfPlayers;

        // ������״̬
        private bool isInitialized = false;
        private string pendingRoomName = "";
        private int pendingMaxPlayers = 4;
        private bool isPendingClient = false;

        // ����״̬����
        private bool hasTriggeredConnectedEvent = false;
        private bool isWaitingForRoomOperation = false;

        private List<RoomInfo> cachedRoomList = new List<RoomInfo>();

        #endregion

        #region Unity��������

        private void Awake()
        {
            // �Ƴ������߼�����Ϊ��PersistentNetworkManager����
            InitializePhoton();
            PhotonNetwork.AddCallbackTarget(this);
            LogDebug("PhotonNetworkAdapter �ѳ�ʼ�����־û��汾��");
        }

        private void Start()
        {
            // ��鵱ǰ����״̬
            CheckAndSyncConnectionStatus();
        }

        private void OnDestroy()
        {
            CleanupPhoton();
            PhotonNetwork.RemoveCallbackTarget(this);
            LogDebug("PhotonNetworkAdapter ������");
        }

        #endregion

        #region ��ʼ��

        /// <summary>
        /// ��ʼ��Photon
        /// </summary>
        private void InitializePhoton()
        {
            if (isInitialized)
            {
                LogDebug("PhotonNetworkAdapter �Ѿ���ʼ��");
                return;
            }

            try
            {
                PhotonNetwork.AutomaticallySyncScene = false;
                PhotonNetwork.GameVersion = gameVersion;

                isInitialized = true;
                LogDebug("Photon��������ʼ�����");

                // ��鵱ǰ����״̬
                CheckAndSyncConnectionStatus();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Photon��������ʼ��ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// ��鲢ͬ������״̬
        /// </summary>
        private void CheckAndSyncConnectionStatus()
        {
            if (PhotonNetwork.IsConnected && !hasTriggeredConnectedEvent)
            {
                LogDebug("��⵽�����ӵ�Photon��ͬ������״̬");
                hasTriggeredConnectedEvent = true;
                OnPhotonConnected?.Invoke();

                if (PhotonNetwork.InRoom)
                {
                    LogDebug("��⵽���ڷ����У�ͬ������״̬");
                    OnPhotonRoomJoined?.Invoke();

                    if (PhotonNetwork.IsMasterClient)
                    {
                        OnPhotonHostStarted?.Invoke();
                    }
                }

                if (PhotonNetwork.InLobby)
                {
                    LogDebug("��⵽���ڴ����У�ͬ������״̬");
                    OnPhotonJoinedLobby?.Invoke();
                }
            }
        }

        /// <summary>
        /// ����Photon��Դ
        /// </summary>
        private void CleanupPhoton()
        {
            // �־û��汾�������Ͽ����ӣ���PersistentNetworkManager����
            LogDebug("Photon��������Դ�������������ӣ�");
        }

        #endregion

        #region �����ӿڷ���

        /// <summary>
        /// ���ӵ�Photon���������䣨Hostģʽ��
        /// </summary>
        public void CreatePhotonRoom(string roomName, int maxPlayers)
        {
            LogDebug($"����Photon����: {roomName}, ������: {maxPlayers}");

            pendingRoomName = roomName;
            pendingMaxPlayers = maxPlayers;
            isPendingClient = false;
            isWaitingForRoomOperation = true;

            if (!PhotonNetwork.IsConnected)
            {
                LogDebug("���ӵ�Photon������...");
                PhotonNetwork.ConnectUsingSettings();
            }
            else if (!PhotonNetwork.InLobby)
            {
                LogDebug("�����ӵ�δ�ڴ������������...");
                PhotonNetwork.JoinLobby();
            }
            else
            {
                LogDebug("���������ڴ�����ֱ�Ӵ�������");
                CreateRoom();
            }
        }

        /// <summary>
        /// ���ӵ�Photon�����뷿�䣨Clientģʽ��
        /// </summary>
        public void JoinPhotonRoom()
        {
            LogDebug("�������Photon����");

            isPendingClient = true;
            isWaitingForRoomOperation = true;

            if (!PhotonNetwork.IsConnected)
            {
                LogDebug("���ӵ�Photon������...");
                PhotonNetwork.ConnectUsingSettings();
            }
            else if (!PhotonNetwork.InLobby)
            {
                LogDebug("�����ӵ�δ�ڴ������������...");
                PhotonNetwork.JoinLobby();
            }
            else
            {
                LogDebug("���������ڴ�����ֱ�Ӽ����������");
                JoinRandomRoom();
            }
        }

        /// <summary>
        /// ���������Ƽ��뷿��
        /// </summary>
        public void JoinPhotonRoomByName(string roomName)
        {
            if (string.IsNullOrEmpty(roomName))
            {
                LogDebug("��������Ϊ�գ��޷�����");
                return;
            }

            LogDebug($"�����Ƽ��뷿��: {roomName}");

            isWaitingForRoomOperation = true;

            if (!PhotonNetwork.IsConnected)
            {
                LogDebug("δ���ӵ�Photon���޷����뷿��");
                InvokeRoomJoinFailed(roomName);
                return;
            }

            if (PhotonNetwork.InRoom)
            {
                LogDebug("�������������У����뿪��ǰ����");
                PhotonNetwork.LeaveRoom();
                StartCoroutine(DelayedJoinRoom(roomName, 1f));
                return;
            }

            PhotonNetwork.JoinRoom(roomName);
        }

        /// <summary>
        /// �ӳټ��뷿��Э��
        /// </summary>
        private IEnumerator DelayedJoinRoom(string roomName, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (!PhotonNetwork.InRoom)
            {
                LogDebug($"�ӳټ��뷿��: {roomName}");
                PhotonNetwork.JoinRoom(roomName);
            }
        }

        /// <summary>
        /// �����������ʧ���¼�
        /// </summary>
        private void InvokeRoomJoinFailed(string roomName)
        {
            isWaitingForRoomOperation = false;
            LogDebug($"�������ʧ��: {roomName}");
        }

        /// <summary>
        /// �뿪��ǰPhoton����
        /// </summary>
        public void LeavePhotonRoom()
        {
            if (PhotonNetwork.InRoom)
            {
                LogDebug("�뿪Photon����");
                PhotonNetwork.LeaveRoom();
            }
            else
            {
                LogDebug("�����κη�����");
            }
        }

        /// <summary>
        /// �Ͽ�Photon����
        /// </summary>
        public void DisconnectPhoton()
        {
            if (PhotonNetwork.IsConnected)
            {
                LogDebug("�Ͽ�Photon����");
                hasTriggeredConnectedEvent = false;
                PhotonNetwork.Disconnect();
            }
            else
            {
                LogDebug("Photonδ����");
            }
        }

        /// <summary>
        /// ��ȡPhoton״̬��Ϣ
        /// </summary>
        public string GetPhotonStatus()
        {
            return $"Connected: {IsPhotonConnected}, InRoom: {IsInPhotonRoom}, " +
                   $"IsMasterClient: {IsPhotonMasterClient}, ClientId: {PhotonClientId}, " +
                   $"RoomName: {CurrentRoomName}, Players: {CurrentRoomPlayerCount}/{CurrentRoomMaxPlayers}";
        }

        #endregion

        #region Lobby����

        /// <summary>
        /// ����Photon����
        /// </summary>
        public void JoinPhotonLobby()
        {
            if (!PhotonNetwork.IsConnected)
            {
                LogDebug("δ���ӵ�Photon���޷��������");
                return;
            }

            if (PhotonNetwork.InLobby)
            {
                LogDebug("���ڴ�����");
                OnPhotonJoinedLobby?.Invoke();
                return;
            }

            LogDebug("����Photon����");
            PhotonNetwork.JoinLobby();
        }

        /// <summary>
        /// �뿪Photon����
        /// </summary>
        public void LeavePhotonLobby()
        {
            if (!PhotonNetwork.InLobby)
            {
                LogDebug("���ڴ�����");
                return;
            }

            LogDebug("�뿪Photon����");
            PhotonNetwork.LeaveLobby();
        }

        /// <summary>
        /// ��ȡ����ķ����б�
        /// </summary>
        public List<RoomInfo> GetPhotonRoomList()
        {
            if (!PhotonNetwork.InLobby)
            {
                LogDebug("���ڴ����У����ؿշ����б�");
                return new List<RoomInfo>();
            }

            return new List<RoomInfo>(cachedRoomList);
        }

        public int GetPhotonRoomCount()
        {
            return cachedRoomList.Count;
        }

        public RoomInfo FindRoomByName(string roomName)
        {
            if (string.IsNullOrEmpty(roomName))
                return null;

            foreach (var room in cachedRoomList)
            {
                if (room.Name == roomName)
                    return room;
            }

            return null;
        }

        public void ClearRoomListCache()
        {
            cachedRoomList.Clear();
            LogDebug("�����б��������");
        }

        /// <summary>
        /// ���»���ķ����б�
        /// </summary>
        private void UpdateCachedRoomList(List<RoomInfo> roomUpdates)
        {
            foreach (var roomUpdate in roomUpdates)
            {
                if (roomUpdate.RemovedFromList || !roomUpdate.IsOpen)
                {
                    RemoveRoomFromCache(roomUpdate.Name);
                    LogDebug($"�ӻ������Ƴ�����: {roomUpdate.Name}");
                }
                else
                {
                    UpdateRoomInCache(roomUpdate);
                    LogDebug($"���»����еķ���: {roomUpdate.Name} ({roomUpdate.PlayerCount}/{roomUpdate.MaxPlayers})");
                }
            }

            LogDebug($"�����б��������ɣ���ǰ�� {cachedRoomList.Count} ������");
        }

        /// <summary>
        /// �ӻ������Ƴ�����
        /// </summary>
        private void RemoveRoomFromCache(string roomName)
        {
            for (int i = cachedRoomList.Count - 1; i >= 0; i--)
            {
                if (cachedRoomList[i].Name == roomName)
                {
                    cachedRoomList.RemoveAt(i);
                    break;
                }
            }
        }

        /// <summary>
        /// ���»����еķ���
        /// </summary>
        private void UpdateRoomInCache(RoomInfo roomUpdate)
        {
            bool found = false;
            for (int i = 0; i < cachedRoomList.Count; i++)
            {
                if (cachedRoomList[i].Name == roomUpdate.Name)
                {
                    cachedRoomList[i] = roomUpdate;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                cachedRoomList.Add(roomUpdate);
            }
        }

        #endregion

        #region �ڲ�����

        /// <summary>
        /// ��������
        /// </summary>
        private void CreateRoom()
        {
            RoomOptions roomOptions = new RoomOptions()
            {
                MaxPlayers = (byte)pendingMaxPlayers,
                IsVisible = true,
                IsOpen = true,
                CustomRoomProperties = new ExitGames.Client.Photon.Hashtable()
                {
                    { "gameMode", "ChineseWords" },
                    { "version", gameVersion },
                    { "hostName", PhotonNetwork.LocalPlayer.NickName ?? "Host" },
                    { "createTime", Time.time }
                }
            };

            LogDebug($"��������: {pendingRoomName}");
            PhotonNetwork.CreateRoom(pendingRoomName, roomOptions);
        }

        /// <summary>
        /// �����������
        /// </summary>
        private void JoinRandomRoom()
        {
            ExitGames.Client.Photon.Hashtable expectedProperties = new ExitGames.Client.Photon.Hashtable()
            {
                { "gameMode", "ChineseWords" }
            };

            LogDebug("�����������");
            PhotonNetwork.JoinRandomRoom(expectedProperties, 0);
        }

        #endregion

        #region Photon�ص�ʵ��

        #region IConnectionCallbacks

        public void OnConnected()
        {
            LogDebug("�����ӵ�Photon����");
        }

        public void OnConnectedToMaster()
        {
            LogDebug("�����ӵ�Photon��������");

            if (!hasTriggeredConnectedEvent)
            {
                hasTriggeredConnectedEvent = true;
                OnPhotonConnected?.Invoke();
                LogDebug("����Photon���ӳɹ��¼�");
            }

            if (isWaitingForRoomOperation)
            {
                if (!PhotonNetwork.InLobby)
                {
                    LogDebug("���ӳɹ�����������Խ��з������");
                    PhotonNetwork.JoinLobby();
                }
                else
                {
                    ProcessPendingRoomOperation();
                }
            }
            else
            {
                if (!PhotonNetwork.InLobby)
                {
                    PhotonNetwork.JoinLobby();
                }
            }
        }

        /// <summary>
        /// ���������ķ������
        /// </summary>
        private void ProcessPendingRoomOperation()
        {
            if (isPendingClient)
            {
                JoinRandomRoom();
                isPendingClient = false;
            }
            else if (!string.IsNullOrEmpty(pendingRoomName))
            {
                CreateRoom();
                pendingRoomName = "";
            }

            isWaitingForRoomOperation = false;
        }

        public void OnDisconnected(DisconnectCause cause)
        {
            LogDebug($"��Photon�������Ͽ�����: {cause}");

            hasTriggeredConnectedEvent = false;
            isWaitingForRoomOperation = false;
            isPendingClient = false;
            pendingRoomName = "";

            OnPhotonDisconnected?.Invoke();
        }

        public void OnRegionListReceived(RegionHandler regionHandler)
        {
            LogDebug("�յ�Photon�����б�");
        }

        public void OnCustomAuthenticationResponse(Dictionary<string, object> data)
        {
            LogDebug("�յ�Photon�Զ�����֤��Ӧ");
        }

        public void OnCustomAuthenticationFailed(string debugMessage)
        {
            Debug.LogError($"Photon�Զ�����֤ʧ��: {debugMessage}");
        }

        #endregion

        #region IMatchmakingCallbacks

        public void OnFriendListUpdate(List<FriendInfo> friendList)
        {
            LogDebug("Photon�����б����");
        }

        public void OnCreatedRoom()
        {
            LogDebug($"Photon���䴴���ɹ�: {PhotonNetwork.CurrentRoom.Name}");

            OnPhotonRoomCreated?.Invoke();
            pendingRoomName = "";
            isWaitingForRoomOperation = false;
        }

        public void OnCreateRoomFailed(short returnCode, string message)
        {
            Debug.LogError($"Photon���䴴��ʧ��: {message} (����: {returnCode})");

            pendingRoomName = "";
            isWaitingForRoomOperation = false;
        }

        public void OnJoinedRoom()
        {
            LogDebug($"�ɹ�����Photon����: {PhotonNetwork.CurrentRoom.Name}");
            LogDebug($"�ҵ�ActorNumber: {PhotonNetwork.LocalPlayer.ActorNumber}");
            LogDebug($"�Ƿ�ΪMaster Client: {PhotonNetwork.IsMasterClient}");

            OnPhotonRoomJoined?.Invoke();
            LogDebug("����Photon�������ɹ��¼�");

            if (PhotonNetwork.IsMasterClient)
            {
                OnPhotonHostStarted?.Invoke();
                LogDebug("����Host��ʼ�¼�");
            }

            isPendingClient = false;
            isWaitingForRoomOperation = false;
        }

        public void OnJoinRoomFailed(short returnCode, string message)
        {
            Debug.LogError($"����Photon����ʧ��: {message} (����: {returnCode})");

            isPendingClient = false;
            isWaitingForRoomOperation = false;
        }

        public void OnJoinRandomFailed(short returnCode, string message)
        {
            Debug.LogError($"�������Photon����ʧ��: {message} (����: {returnCode})");

            isPendingClient = false;
            isWaitingForRoomOperation = false;
        }

        public void OnLeftRoom()
        {
            LogDebug("�뿪Photon����");
            OnPhotonRoomLeft?.Invoke();
            OnPhotonHostStopped?.Invoke();
        }

        public void OnJoinedLobby()
        {
            LogDebug("�ɹ�����Photon����");
            OnPhotonJoinedLobby?.Invoke();

            if (isWaitingForRoomOperation)
            {
                ProcessPendingRoomOperation();
            }
        }

        public void OnLeftLobby()
        {
            LogDebug("�뿪Photon����");
            cachedRoomList.Clear();
            OnPhotonLeftLobby?.Invoke();
        }

        public void OnRoomListUpdate(List<RoomInfo> roomList)
        {
            LogDebug($"�����б���£��� {roomList.Count} ������");
            UpdateCachedRoomList(roomList);
            OnPhotonRoomListUpdate?.Invoke(roomList);
        }

        public void OnLobbyStatisticsUpdate(List<TypedLobbyInfo> lobbyStatistics)
        {
            LogDebug($"����ͳ�Ƹ��£��յ� {lobbyStatistics.Count} ��������Ϣ");
        }

        #endregion

        #region IInRoomCallbacks

        public void OnPlayerEnteredRoom(Player newPlayer)
        {
            LogDebug($"��Ҽ���Photon����: {newPlayer.NickName} (ActorNumber: {newPlayer.ActorNumber})");
            OnPhotonPlayerJoined?.Invoke((ushort)newPlayer.ActorNumber);
        }

        public void OnPlayerLeftRoom(Player otherPlayer)
        {
            LogDebug($"����뿪Photon����: {otherPlayer.NickName} (ActorNumber: {otherPlayer.ActorNumber})");
            OnPhotonPlayerLeft?.Invoke((ushort)otherPlayer.ActorNumber);
        }

        public void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
        {
            LogDebug("Photon�������Ը���");
        }

        public void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
        {
            LogDebug($"Photon������Ը���: {targetPlayer.NickName}");
        }

        public void OnMasterClientSwitched(Player newMasterClient)
        {
            LogDebug($"Photon Master Client �л���: {newMasterClient.NickName} (ActorNumber: {newMasterClient.ActorNumber})");

            if (PhotonNetwork.IsMasterClient)
            {
                OnPhotonHostStarted?.Invoke();
            }
            else
            {
                OnPhotonHostStopped?.Invoke();
            }
        }

        #endregion

        #endregion

        #region ��������

        /// <summary>
        /// ��������Զ�������
        /// </summary>
        public void SetPlayerProperties(ExitGames.Client.Photon.Hashtable properties)
        {
            if (!PhotonNetwork.IsConnected)
            {
                LogDebug("δ���ӵ�Photon���޷������������");
                return;
            }

            if (properties == null || properties.Count == 0)
            {
                LogDebug("�������Ϊ��");
                return;
            }

            LogDebug($"����������ԣ��� {properties.Count} ��");
            PhotonNetwork.LocalPlayer.SetCustomProperties(properties);
        }

        /// <summary>
        /// ��ȡ����Զ�������
        /// </summary>
        public ExitGames.Client.Photon.Hashtable GetPlayerProperties(Player player = null)
        {
            if (player == null)
                player = PhotonNetwork.LocalPlayer;

            if (player == null)
                return new ExitGames.Client.Photon.Hashtable();

            return player.CustomProperties;
        }

        /// <summary>
        /// ���÷����Զ������ԣ���Master Client���ã�
        /// </summary>
        public void SetRoomProperties(ExitGames.Client.Photon.Hashtable properties)
        {
            if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom)
            {
                LogDebug("δ���ӻ�δ�ڷ����У��޷����÷�������");
                return;
            }

            if (!PhotonNetwork.IsMasterClient)
            {
                LogDebug("����Master Client���޷����÷�������");
                return;
            }

            if (properties == null || properties.Count == 0)
            {
                LogDebug("��������Ϊ��");
                return;
            }

            LogDebug($"���÷������ԣ��� {properties.Count} ��");
            PhotonNetwork.CurrentRoom.SetCustomProperties(properties);
        }

        /// <summary>
        /// ��ȡ�����Զ�������
        /// </summary>
        public ExitGames.Client.Photon.Hashtable GetRoomProperties()
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
                return new ExitGames.Client.Photon.Hashtable();

            return PhotonNetwork.CurrentRoom.CustomProperties;
        }

        #endregion

        #region ���Է���

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[PhotonNetworkAdapter] {message}");
            }
        }

        [ContextMenu("��ʾPhoton״̬")]
        public void ShowPhotonStatus()
        {
            Debug.Log($"=== Photon״̬ ===\n{GetPhotonStatus()}");
        }

        [ContextMenu("ǿ�ƴ��������¼�")]
        public void ForceTriggeredConnectedEvent()
        {
            if (PhotonNetwork.IsConnected && !hasTriggeredConnectedEvent)
            {
                hasTriggeredConnectedEvent = true;
                OnPhotonConnected?.Invoke();
                LogDebug("�ֶ����������¼�");
            }
        }

        [ContextMenu("��������״̬")]
        public void ResetConnectionState()
        {
            hasTriggeredConnectedEvent = false;
            isWaitingForRoomOperation = false;
            isPendingClient = false;
            pendingRoomName = "";
            LogDebug("����״̬������");
        }

        #endregion
    }
}