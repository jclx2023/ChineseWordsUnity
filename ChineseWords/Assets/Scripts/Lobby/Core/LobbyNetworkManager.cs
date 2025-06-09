using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Photon.Realtime;
using Core.Network;
using Lobby.Data;
using Lobby.Network;

namespace Lobby.Core
{
    /// <summary>
    /// Lobby��������� - Photon��ʵʵ�ְ汾
    /// ר�Ŵ���Lobby��ص�Photon�������
    /// </summary>
    public class LobbyNetworkManager : MonoBehaviour
    {
        [Header("��������")]
        [SerializeField] private bool autoConnectOnStart = true;
        [SerializeField] private float roomListRefreshInterval = 5f;
        [SerializeField] private float connectionTimeout = 10f;

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        public static LobbyNetworkManager Instance { get; private set; }

        // ����״̬
        private bool isConnected = false;
        private bool isConnecting = false;
        private bool isInLobby = false;

        // ��������
        private List<LobbyRoomData> cachedRoomList = new List<LobbyRoomData>();
        private Coroutine connectionTimeoutCoroutine;

        // �¼�
        public System.Action<bool> OnConnectionStatusChanged;
        public System.Action<List<LobbyRoomData>> OnRoomListUpdated;
        public System.Action<string, bool> OnRoomCreated; // roomName, success
        public System.Action<string, bool> OnRoomJoined; // roomName, success
        public System.Action<bool> OnLobbyStatusChanged; // inLobby

        #region Unity��������

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                LogDebug("LobbyNetworkManager ʵ���Ѵ���");
            }
            else
            {
                LogDebug("�����ظ���LobbyNetworkManagerʵ��");
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            if (autoConnectOnStart)
            {
                StartCoroutine(ConnectToPhotonCoroutine());
            }
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
            UnsubscribeFromPhotonEvents();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region ���ӹ���

        /// <summary>
        /// ���ӵ�Photon��Э��
        /// </summary>
        private IEnumerator ConnectToPhotonCoroutine()
        {
            LogDebug("��ʼ���ӵ�Photon");

            if (PhotonNetworkAdapter.Instance == null)
            {
                Debug.LogError("[LobbyNetworkManager] PhotonNetworkAdapter.Instance Ϊ��");
                yield break;
            }

            isConnecting = true;
            OnConnectionStatusChanged?.Invoke(false);

            // ����Photon�¼�
            SubscribeToPhotonEvents();

            // ����Ƿ��Ѿ�����
            if (PhotonNetworkAdapter.Instance.IsPhotonConnected)
            {
                LogDebug("�����ӵ�Photon��ֱ�Ӽ������");
                JoinPhotonLobby();
                yield break;
            }

            // ��ʼ���ӳ�ʱ��ʱ
            connectionTimeoutCoroutine = StartCoroutine(ConnectionTimeoutCoroutine());

            // �ȴ��������
            while (isConnecting && !isConnected)
            {
                yield return new WaitForSeconds(0.1f);
            }

            // ֹͣ��ʱ��ʱ
            if (connectionTimeoutCoroutine != null)
            {
                StopCoroutine(connectionTimeoutCoroutine);
                connectionTimeoutCoroutine = null;
            }
        }

        /// <summary>
        /// ���ӳ�ʱЭ��
        /// </summary>
        private IEnumerator ConnectionTimeoutCoroutine()
        {
            yield return new WaitForSeconds(connectionTimeout);

            if (isConnecting)
            {
                LogDebug("���ӳ�ʱ");
                isConnecting = false;
                OnConnectionStatusChanged?.Invoke(false);
            }
        }

        /// <summary>
        /// ����Photon����
        /// </summary>
        private void JoinPhotonLobby()
        {
            if (PhotonNetworkAdapter.Instance != null)
            {
                PhotonNetworkAdapter.Instance.JoinPhotonLobby();
            }
        }

        /// <summary>
        /// ����Photon�¼�
        /// </summary>
        private void SubscribeToPhotonEvents()
        {
            if (PhotonNetworkAdapter.Instance != null)
            {
                PhotonNetworkAdapter.OnPhotonConnected += OnPhotonConnected;
                PhotonNetworkAdapter.OnPhotonDisconnected += OnPhotonDisconnected;
                PhotonNetworkAdapter.OnPhotonJoinedLobby += OnPhotonJoinedLobby;
                PhotonNetworkAdapter.OnPhotonLeftLobby += OnPhotonLeftLobby;
                PhotonNetworkAdapter.OnPhotonRoomListUpdate += OnPhotonRoomListUpdate;
                PhotonNetworkAdapter.OnPhotonRoomJoined += OnPhotonRoomJoined;
                PhotonNetworkAdapter.OnPhotonRoomLeft += OnPhotonRoomLeft;

                LogDebug("�Ѷ���Photon�¼�");
            }
        }

        /// <summary>
        /// ȡ������Photon�¼�
        /// </summary>
        private void UnsubscribeFromPhotonEvents()
        {
            if (PhotonNetworkAdapter.Instance != null)
            {
                PhotonNetworkAdapter.OnPhotonConnected -= OnPhotonConnected;
                PhotonNetworkAdapter.OnPhotonDisconnected -= OnPhotonDisconnected;
                PhotonNetworkAdapter.OnPhotonJoinedLobby -= OnPhotonJoinedLobby;
                PhotonNetworkAdapter.OnPhotonLeftLobby -= OnPhotonLeftLobby;
                PhotonNetworkAdapter.OnPhotonRoomListUpdate -= OnPhotonRoomListUpdate;
                PhotonNetworkAdapter.OnPhotonRoomJoined -= OnPhotonRoomJoined;
                PhotonNetworkAdapter.OnPhotonRoomLeft -= OnPhotonRoomLeft;

                LogDebug("��ȡ������Photon�¼�");
            }
        }

        #endregion

        #region �������

        public void CreateRoom(string roomName, int maxPlayers, string password = "")
        {
            if (!isConnected || !isInLobby)
            {
                LogDebug("δ���ӵ������δ�ڴ����У��޷���������");
                OnRoomCreated?.Invoke(roomName, false);
                return;
            }

            if (string.IsNullOrEmpty(roomName) || maxPlayers < 2)
            {
                LogDebug("��Ч�ķ������");
                OnRoomCreated?.Invoke(roomName, false);
                return;
            }

            // ����������
            roomName = PhotonLobbyDataConverter.CleanRoomNameForPhoton(roomName);

            LogDebug($"��������: {roomName}, �������: {maxPlayers}");

            var lobbyRoomData = LobbyRoomData.CreateNew(roomName, maxPlayers, GetCurrentPlayerName());
            lobbyRoomData.hasPassword = !string.IsNullOrEmpty(password);
            lobbyRoomData.password = password;

            var roomOptions = lobbyRoomData.ToPhotonRoomOptions();

            if (roomOptions == null)
            {
                LogDebug("����ѡ��ת��ʧ��");
                OnRoomCreated?.Invoke(roomName, false);
                return;
            }

            // �����������
            SetPlayerProperties();

            // ����PhotonNetworkAdapter��������
            if (PhotonNetworkAdapter.Instance != null)
            {
                PhotonNetworkAdapter.Instance.CreatePhotonRoom(roomName, maxPlayers);
            }
        }

        /// <summary>
        /// ���뷿��
        /// </summary>
        public void JoinRoom(LobbyRoomData roomData)
        {
            if (!isConnected || !isInLobby)
            {
                LogDebug("δ���ӵ������δ�ڴ����У��޷����뷿��");
                OnRoomJoined?.Invoke(roomData.roomName, false);
                return;
            }

            if (roomData == null || !roomData.CanJoin())
            {
                LogDebug("����������Ч���޷�����");
                OnRoomJoined?.Invoke(roomData?.roomName ?? "Unknown", false);
                return;
            }

            LogDebug($"���뷿��: {roomData.roomName}");

            // �����������
            SetPlayerProperties();

            // TODO: ������������룬������Ҫ����������֤
            if (roomData.hasPassword)
            {
                LogDebug("������Ҫ���룬����δʵ��������֤");
                // �ڽ׶�3������չ�������빦��
            }

            // ����PhotonNetworkAdapter���뷿��
            if (PhotonNetworkAdapter.Instance != null)
            {
                // ע�⣺������Ҫ�޸�PhotonNetworkAdapter��Ӱ����Ƽ��뷿��ķ���
                PhotonNetworkAdapter.Instance.JoinPhotonRoomByName(roomData.roomName);
            }
        }

        /// <summary>
        /// �����������
        /// </summary>
        public void JoinRandomRoom()
        {
            if (!isConnected || !isInLobby)
            {
                LogDebug("δ���ӵ������δ�ڴ����У��޷������������");
                return;
            }

            if (cachedRoomList.Count == 0)
            {
                LogDebug("û�п��õķ���");
                return;
            }

            // �ҵ���һ���ɼ���ķ���
            foreach (var room in cachedRoomList)
            {
                if (room.CanJoin())
                {
                    JoinRoom(room);
                    return;
                }
            }

            LogDebug("û�пɼ���ķ���");

            // ʹ��PhotonNetwork��������빦��
            if (PhotonNetworkAdapter.Instance != null)
            {
                PhotonNetworkAdapter.Instance.JoinPhotonRoom(); // ��᳢���������
            }
        }

        /// <summary>
        /// ˢ�·����б�
        /// </summary>
        public void RefreshRoomList()
        {
            if (!isConnected || !isInLobby)
            {
                LogDebug("δ���ӵ������δ�ڴ����У��޷�ˢ�·����б�");
                return;
            }

            LogDebug("ˢ�·����б�");

            // Photon���Զ�ά�������б�������Ҫ�Ǵ�������
            if (PhotonNetworkAdapter.Instance != null)
            {
                var photonRooms = PhotonNetworkAdapter.Instance.GetPhotonRoomList();
                OnPhotonRoomListUpdate(photonRooms);
            }
        }

        /// <summary>
        /// �Ͽ�����
        /// </summary>
        public void Disconnect()
        {
            LogDebug("�Ͽ���������");

            if (PhotonNetworkAdapter.Instance != null)
            {
                if (isInLobby)
                {
                    PhotonNetworkAdapter.Instance.LeavePhotonLobby();
                }
                PhotonNetworkAdapter.Instance.DisconnectPhoton();
            }
        }

        #endregion

        #region Photon�¼�����

        private void OnPhotonConnected()
        {
            LogDebug("Photon���ӳɹ���׼���������");
            isConnecting = false;
            isConnected = true;
            OnConnectionStatusChanged?.Invoke(true);

            // ���ӳɹ����Զ��������
            JoinPhotonLobby();
        }

        private void OnPhotonDisconnected()
        {
            LogDebug("Photon���ӶϿ�");
            isConnecting = false;
            isConnected = false;
            isInLobby = false;

            OnConnectionStatusChanged?.Invoke(false);
            OnLobbyStatusChanged?.Invoke(false);

            // ��շ����б�
            cachedRoomList.Clear();
            OnRoomListUpdated?.Invoke(new List<LobbyRoomData>());
        }

        private void OnPhotonJoinedLobby()
        {
            LogDebug("�ɹ�����Photon����");
            isInLobby = true;
            OnLobbyStatusChanged?.Invoke(true);

            // ��������ɹ����Զ�ˢ�·����б�
            RefreshRoomList();
        }

        private void OnPhotonLeftLobby()
        {
            LogDebug("�뿪Photon����");
            isInLobby = false;
            OnLobbyStatusChanged?.Invoke(false);

            // ��շ����б�
            cachedRoomList.Clear();
            OnRoomListUpdated?.Invoke(new List<LobbyRoomData>());
        }

        private void OnPhotonRoomListUpdate(List<RoomInfo> photonRooms)
        {
            LogDebug($"Photon�����б���£��� {photonRooms.Count} ������");

            // ת��Photon��������ΪLobby��������
            cachedRoomList = PhotonLobbyDataConverter.FromPhotonRoomList(photonRooms);

            OnRoomListUpdated?.Invoke(new List<LobbyRoomData>(cachedRoomList));
            LogDebug($"ת����ķ����б�{cachedRoomList.Count} �����÷���");
        }

        private void OnPhotonRoomJoined()
        {
            LogDebug("�ɹ�����Photon����");

            // ��ȡ��ǰ������Ϣ
            if (PhotonNetworkAdapter.Instance.IsInPhotonRoom)
            {
                string roomName = PhotonNetworkAdapter.Instance.CurrentRoomName;
                OnRoomJoined?.Invoke(roomName, true);
                LogDebug($"�������ɹ�: {roomName}");
            }
        }

        private void OnPhotonRoomLeft()
        {
            LogDebug("�뿪Photon����");
            // ���¼�������Լ����������
            if (isConnected && !isInLobby)
            {
                JoinPhotonLobby();
            }
        }

        #endregion

        #region ��������

        /// <summary>
        /// �����������
        /// </summary>
        private void SetPlayerProperties()
        {
            if (LobbySceneManager.Instance == null)
                return;

            var playerData = LobbySceneManager.Instance.GetCurrentPlayerData();
            if (playerData == null)
                return;

            // ��������Զ�������
            var playerProps = PhotonLobbyDataConverter.CreatePlayerProperties(playerData);

            // ���õ�Photon
            if (PhotonNetworkAdapter.Instance != null)
            {
                // ������Ҫ��PhotonNetworkAdapter���������������Եķ���
                PhotonNetworkAdapter.Instance.SetPlayerProperties(playerProps);
            }
        }

        /// <summary>
        /// ��ȡ��ǰ�������
        /// </summary>
        private string GetCurrentPlayerName()
        {
            if (LobbySceneManager.Instance != null)
            {
                var playerData = LobbySceneManager.Instance.GetCurrentPlayerData();
                if (playerData != null && !string.IsNullOrEmpty(playerData.playerName))
                {
                    return playerData.playerName;
                }
            }
            return "Unknown Player";
        }

        /// <summary>
        /// �������״̬
        /// </summary>
        private bool CheckNetworkStatus(string operation)
        {
            if (!isConnected)
            {
                LogDebug($"�޷�ִ�� {operation}��δ���ӵ�����");
                return false;
            }

            if (!isInLobby)
            {
                LogDebug($"�޷�ִ�� {operation}��δ�ڴ�����");
                return false;
            }

            return true;
        }

        #endregion

        #region �����ӿ�

        /// <summary>
        /// ��ȡ����״̬
        /// </summary>
        public bool IsConnected()
        {
            return isConnected;
        }

        /// <summary>
        /// ��ȡ������״̬
        /// </summary>
        public bool IsConnecting()
        {
            return isConnecting;
        }

        /// <summary>
        /// ��ȡ����״̬
        /// </summary>
        public bool IsInLobby()
        {
            return isInLobby;
        }

        /// <summary>
        /// ��ȡ����ķ����б�
        /// </summary>
        public List<LobbyRoomData> GetCachedRoomList()
        {
            return new List<LobbyRoomData>(cachedRoomList);
        }

        /// <summary>
        /// ǿ����������
        /// </summary>
        public void ForceReconnect()
        {
            LogDebug("ǿ����������");

            // �ȶϿ�����
            Disconnect();

            // �ȴ�һ֡����������
            StartCoroutine(ReconnectCoroutine());
        }

        /// <summary>
        /// ����Э��
        /// </summary>
        private System.Collections.IEnumerator ReconnectCoroutine()
        {
            yield return new WaitForSeconds(1f);
            yield return StartCoroutine(ConnectToPhotonCoroutine());
        }

        /// <summary>
        /// ��ȡ����ͳ����Ϣ
        /// </summary>
        public string GetNetworkStats()
        {
            if (PhotonNetworkAdapter.Instance == null)
                return "PhotonNetworkAdapter ������";

            string stats = "=== ����ͳ�� ===\n";
            stats += $"����״̬: {isConnected}\n";
            stats += $"����״̬: {isInLobby}\n";
            stats += $"��������: {cachedRoomList.Count}\n";

            if (PhotonNetworkAdapter.Instance.IsPhotonConnected)
            {
                stats += $"Photon��������: {PhotonNetworkAdapter.Instance.PhotonRoomCount}\n";
                stats += $"Photon�������: {PhotonNetworkAdapter.Instance.PhotonPlayerCount}";
            }

            return stats;
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
                Debug.Log($"[LobbyNetworkManager] {message}");
            }
        }

        [ContextMenu("��ʾ����״̬")]
        public void ShowNetworkStatus()
        {
            LogDebug(GetNetworkStats());
        }

        [ContextMenu("ǿ��ˢ�·����б�")]
        public void ForceRefreshRoomList()
        {
            RefreshRoomList();
        }

        [ContextMenu("ǿ����������")]
        public void ForceReconnectDebug()
        {
            ForceReconnect();
        }

        [ContextMenu("���Դ�������")]
        public void TestCreateRoom()
        {
            CreateRoom("���Է���_" + Random.Range(1000, 9999), 4);
        }

        [ContextMenu("��ʾ���淿���б�")]
        public void ShowCachedRoomList()
        {
            LogDebug($"=== ���淿���б� ({cachedRoomList.Count}) ===");
            for (int i = 0; i < cachedRoomList.Count; i++)
            {
                var room = cachedRoomList[i];
                LogDebug($"{i + 1}. {room.roomName} ({room.currentPlayers}/{room.maxPlayers}) - {room.status}");
            }
        }

        #endregion
    }
}