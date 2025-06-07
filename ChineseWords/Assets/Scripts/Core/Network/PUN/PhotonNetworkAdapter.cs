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
    public class PhotonNetworkAdapter : MonoBehaviourPun, IConnectionCallbacks, IMatchmakingCallbacks, IInRoomCallbacks
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

        // ������״̬
        private bool isInitialized = false;
        private string pendingRoomName = "";
        private int pendingMaxPlayers = 4;
        private bool isPendingClient = false;

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

        #region NetworkManager�Žӷ���

        /// <summary>
        /// �Žӵ�NetworkManager�¼�����ѡ��
        /// �����Ҫ��PhotonNetworkAdapter���¼�����NetworkManager���¼�
        /// </summary>
        public void EnableNetworkManagerBridge()
        {
            LogDebug("����NetworkManager�¼��Ž�");

            OnPhotonConnected += () => {
                LogDebug("�Ž�: Photon���� -> NetworkManager.OnConnected");
                // ������Դ���NetworkManager���¼�
                // NetworkManager.OnConnected?.Invoke();
            };

            OnPhotonDisconnected += () => {
                LogDebug("�Ž�: Photon�Ͽ� -> NetworkManager.OnDisconnected");
                // ������Դ���NetworkManager���¼�
                // NetworkManager.OnDisconnected?.Invoke();
            };

            OnPhotonPlayerJoined += (playerId) => {
                LogDebug($"�Ž�: Photon��Ҽ��� -> NetworkManager.OnPlayerJoined ({playerId})");
                // ������Դ���NetworkManager���¼�
                // NetworkManager.OnPlayerJoined?.Invoke(playerId);
            };

            OnPhotonPlayerLeft += (playerId) => {
                LogDebug($"�Ž�: Photon����뿪 -> NetworkManager.OnPlayerLeft ({playerId})");
                // ������Դ���NetworkManager���¼�
                // NetworkManager.OnPlayerLeft?.Invoke(playerId);
            };
        }

        /// <summary>
        /// ����NetworkManager�¼��Ž�
        /// </summary>
        public void DisableNetworkManagerBridge()
        {
            LogDebug("����NetworkManager�¼��Ž�");
            // �����������Ƴ��¼�����
        }

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

        #endregion
    }
}