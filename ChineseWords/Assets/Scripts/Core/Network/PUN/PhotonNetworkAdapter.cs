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
    /// Photon����������
    /// ��ΪNetworkManager�Ĳ��䣬����Photon�ض�����
    /// ����ʽǨ�Ʋ��ԣ��Ȳ��棬������
    /// </summary>
    public class PhotonNetworkAdapter : MonoBehaviourPun, IConnectionCallbacks, IMatchmakingCallbacks, ILobbyCallbacks
    {
        [Header("Photon����")]
        [SerializeField] private string gameVersion = "1.0";
        [SerializeField] private bool enableDebugLogs = true;

        public static PhotonNetworkAdapter Instance { get; private set; }

        #region ������ר���¼���������NetworkManager��ͻ��

        // ʹ�ò�ͬ���¼����Ʊ����ͻ
        public static event System.Action OnPhotonConnected;
        public static event System.Action OnPhotonDisconnected;
        public static event System.Action OnPhotonHostStarted;
        public static event System.Action OnPhotonHostStopped;
        public static event System.Action<ushort> OnPhotonPlayerJoined;
        public static event System.Action<ushort> OnPhotonPlayerLeft;
        public static event System.Action OnPhotonRoomJoined;
        public static event System.Action OnPhotonRoomLeft;
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

        private List<RoomInfo> cachedRoomList = new List<RoomInfo>();

        #endregion

        #region Unity��������

        private void Awake()
        {
            // ����ģʽ
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializePhoton();
                PhotonNetwork.AddCallbackTarget(this);
                LogDebug("PhotonNetworkAdapter �����Ѵ���");
            }
            else
            {
                LogDebug("�����ظ���PhotonNetworkAdapterʵ��");
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            CleanupPhoton();
            PhotonNetwork.RemoveCallbackTarget(this);
            if (Instance == this)
            {
                Instance = null;
            }
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
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Photon��������ʼ��ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// ����Photon��Դ
        /// </summary>
        private void CleanupPhoton()
        {
            if (PhotonNetwork.IsConnected)
            {
                PhotonNetwork.Disconnect();
            }
            LogDebug("Photon��������Դ������");
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

            if (!PhotonNetwork.IsConnected)
            {
                LogDebug("���ӵ�Photon������...");
                PhotonNetwork.ConnectUsingSettings();
            }
            else
            {
                LogDebug("�����ӣ�ֱ�Ӵ�������");
                CreateRoom();
            }
        }

        /// <summary>
        /// ���ӵ�Photon�����뷿�䣨Clientģʽ��
        /// </summary>
        public void JoinPhotonRoom()
        {
            LogDebug("����Photon����");

            isPendingClient = true;

            if (!PhotonNetwork.IsConnected)
            {
                LogDebug("���ӵ�Photon������...");
                PhotonNetwork.ConnectUsingSettings();
            }
            else
            {
                LogDebug("�����ӣ�ֱ�Ӽ��뷿��");
                JoinRandomRoom();
            }
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

            // ���ػ���ķ����б���
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
                // ������䱻�Ƴ���رգ��ӻ�����ɾ��
                if (roomUpdate.RemovedFromList || !roomUpdate.IsOpen)
                {
                    RemoveRoomFromCache(roomUpdate.Name);
                    LogDebug($"�ӻ������Ƴ�����: {roomUpdate.Name}");
                }
                else
                {
                    // ���»���ӷ��䵽����
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
            // �������з���
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

            // ���û�ҵ�������·���
            if (!found)
            {
                cachedRoomList.Add(roomUpdate);
            }
        }
        #endregion

        #region �����ѯ����

        /// <summary>
        /// ��ȡ�ɼ���ķ����б�
        /// </summary>
        public List<RoomInfo> GetJoinableRooms()
        {
            var joinableRooms = new List<RoomInfo>();

            foreach (var room in cachedRoomList)
            {
                if (room.IsOpen && room.IsVisible && room.PlayerCount < room.MaxPlayers)
                {
                    joinableRooms.Add(room);
                }
            }

            return joinableRooms;
        }

        /// <summary>
        /// ��ȡ������ķ�������
        /// </summary>
        public int GetPasswordProtectedRoomCount()
        {
            int count = 0;
            foreach (var room in cachedRoomList)
            {
                if (room.CustomProperties.TryGetValue("hasPassword", out object hasPassword) && (bool)hasPassword)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// ����Ϸģʽɸѡ����
        /// </summary>
        public List<RoomInfo> GetRoomsByGameMode(string gameMode)
        {
            var filteredRooms = new List<RoomInfo>();

            foreach (var room in cachedRoomList)
            {
                if (room.CustomProperties.TryGetValue("gameMode", out object roomGameMode) &&
                    roomGameMode.ToString() == gameMode)
                {
                    filteredRooms.Add(room);
                }
            }

            return filteredRooms;
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
                    { "version", gameVersion }
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
            PhotonNetwork.JoinLobby();
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
        }

        public void OnDisconnected(DisconnectCause cause)
        {
            LogDebug($"��Photon�������Ͽ�����: {cause}");
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
        }

        public void OnCreateRoomFailed(short returnCode, string message)
        {
            Debug.LogError($"Photon���䴴��ʧ��: {message} (����: {returnCode})");
        }

        public void OnJoinedRoom()
        {
            LogDebug($"�ɹ�����Photon����: {PhotonNetwork.CurrentRoom.Name}");
            LogDebug($"�ҵ�ActorNumber: {PhotonNetwork.LocalPlayer.ActorNumber}");
            LogDebug($"�Ƿ�ΪMaster Client: {PhotonNetwork.IsMasterClient}");

            OnPhotonRoomJoined?.Invoke();

            if (PhotonNetwork.IsMasterClient)
            {
                OnPhotonHostStarted?.Invoke();
            }

            OnPhotonConnected?.Invoke();
        }

        public void OnJoinRoomFailed(short returnCode, string message)
        {
            Debug.LogError($"����Photon����ʧ��: {message} (����: {returnCode})");
        }

        public void OnJoinRandomFailed(short returnCode, string message)
        {
            Debug.LogError($"�������Photon����ʧ��: {message} (����: {returnCode})");
        }

        public void OnLeftRoom()
        {
            LogDebug("�뿪Photon����");
            OnPhotonRoomLeft?.Invoke();
            OnPhotonDisconnected?.Invoke();
        }

        public void OnJoinedLobby()
        {
            LogDebug("�ɹ�����Photon����");
            OnPhotonJoinedLobby?.Invoke();
        }

        public void OnLeftLobby()
        {
            LogDebug("�뿪Photon����");

            cachedRoomList.Clear();
            OnPhotonLeftLobby?.Invoke();
            // �����Ϊ��ˢ�¶��뿪���Զ����¼���
            if (shouldRejoinLobby)
            {
                shouldRejoinLobby = false;
                Invoke(nameof(JoinPhotonLobby), 0.5f); // �ӳ�0.5������¼���
            }
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

            foreach (var lobby in lobbyStatistics)
            {
                LogDebug($"����: {lobby.Name} (����: {lobby.Type}) - ������: {lobby.RoomCount}, �����: {lobby.PlayerCount}");
            }

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
        }

        #endregion

        #endregion

        #region ���Է���

        /// <summary>
        /// ������־
        /// </summary>
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

        [ContextMenu("���Դ�������")]
        public void TestCreateRoom()
        {
            CreatePhotonRoom("TestRoom", 4);
        }

        [ContextMenu("���Լ��뷿��")]
        public void TestJoinRoom()
        {
            JoinPhotonRoom();
        }

        [ContextMenu("�뿪����")]
        public void TestLeaveRoom()
        {
            LeavePhotonRoom();
        }
        [ContextMenu("��ʾ�����б������Ϣ")]
        public void ShowRoomListDebugInfo()
        {
            Debug.Log(GetRoomListDebugInfo());
        }
        /// <summary>
        /// ��ȡ�����б����ϸ������Ϣ
        /// </summary>
        public string GetRoomListDebugInfo()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine($"=== Photon�����б������Ϣ ===");
            info.AppendLine($"����״̬: {(PhotonNetwork.InLobby ? "������" : "δ����")}");
            info.AppendLine($"���淿����: {cachedRoomList.Count}");
            info.AppendLine($"Photonͳ�Ʒ�����: {PhotonNetwork.CountOfRooms}");
            info.AppendLine($"�ɼ��뷿����: {GetJoinableRooms().Count}");

            if (cachedRoomList.Count > 0)
            {
                info.AppendLine("\n��������:");
                for (int i = 0; i < cachedRoomList.Count; i++)
                {
                    var room = cachedRoomList[i];
                    info.AppendLine($"  {i + 1}. {room.Name} ({room.PlayerCount}/{room.MaxPlayers}) " +
                                  $"����:{room.IsOpen} �ɼ�:{room.IsVisible}");
                }
            }

            return info.ToString();
        }

        #endregion

        #region Lobby֧�ַ���

        private bool shouldRejoinLobby = false;

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

            if (!PhotonNetwork.IsConnected)
            {
                LogDebug("δ���ӵ�Photon���޷����뷿��");
                return;
            }

            LogDebug($"�����Ƽ��뷿��: {roomName}");
            PhotonNetwork.JoinRoom(roomName);
        }

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
        public ExitGames.Client.Photon.Hashtable GetPlayerProperties(Photon.Realtime.Player player = null)
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

        /// <summary>
        /// ��ȡ���з����е������Ϣ
        /// </summary>
        public List<Photon.Realtime.Player> GetRoomPlayers()
        {
            var players = new List<Photon.Realtime.Player>();

            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
                return players;

            foreach (var player in PhotonNetwork.CurrentRoom.Players.Values)
            {
                players.Add(player);
            }

            return players;
        }

        /// <summary>
        /// �߳���ң���Master Client���ã�
        /// </summary>
        public void KickPlayer(Photon.Realtime.Player player)
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                LogDebug("����Master Client���޷��߳����");
                return;
            }

            if (player == null)
            {
                LogDebug("���Ϊ�գ��޷��߳�");
                return;
            }

            LogDebug($"�߳����: {player.NickName}");
            PhotonNetwork.CloseConnection(player);
        }

        /// <summary>
        /// ת��Master ClientȨ��
        /// </summary>
        public void TransferMasterClient(Photon.Realtime.Player newMaster)
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                LogDebug("����Master Client���޷�ת��Ȩ��");
                return;
            }

            if (newMaster == null)
            {
                LogDebug("��Master ClientΪ��");
                return;
            }

            LogDebug($"ת��Master ClientȨ�޸�: {newMaster.NickName}");
            PhotonNetwork.SetMasterClient(newMaster);
        }

        /// <summary>
        /// ��ȡ��ϸ��Photon״̬
        /// </summary>
        public string GetDetailedPhotonStatus()
        {
            string status = GetPhotonStatus() + "\n";

            status += "=== ��ϸ״̬ ===\n";
            status += $"����״̬: {PhotonNetwork.NetworkClientState}\n";
            status += $"������ʱ��: {PhotonNetwork.ServerTimestamp}\n";
            status += $"�ӳ�: {PhotonNetwork.GetPing()}ms\n";

            if (PhotonNetwork.InLobby)
            {
                status += $"�����з�����: {PhotonNetwork.CountOfRooms}\n";
                status += $"�����������: {PhotonNetwork.CountOfPlayers}\n";
            }

            if (PhotonNetwork.InRoom)
            {
                status += $"��������б�:\n";
                foreach (var player in PhotonNetwork.CurrentRoom.Players.Values)
                {
                    status += $"  - {player.NickName} (ID: {player.ActorNumber})\n";
                }
            }

            return status;
        }

        #endregion
    }
}