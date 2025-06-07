using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Core.Network;
using Lobby.Data;

namespace Lobby.Core
{
    /// <summary>
    /// Lobby���������
    /// ר�Ŵ���Lobby��ص�Photon�������
    /// </summary>
    public class LobbyNetworkManager : MonoBehaviour
    {
        [Header("��������")]
        [SerializeField] private bool autoConnectOnStart = true;
        [SerializeField] private float roomListRefreshInterval = 5f;

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        public static LobbyNetworkManager Instance { get; private set; }

        // ����״̬
        private bool isConnected = false;
        private bool isConnecting = false;

        // ��������
        private List<LobbyRoomData> cachedRoomList = new List<LobbyRoomData>();

        // �¼�
        public System.Action<bool> OnConnectionStatusChanged;
        public System.Action<List<LobbyRoomData>> OnRoomListUpdated;
        public System.Action<string, bool> OnRoomCreated; // roomName, success
        public System.Action<string, bool> OnRoomJoined; // roomName, success

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

            // ����Ѿ����ӣ�ֱ�ӳɹ�
            if (PhotonNetworkAdapter.Instance.IsPhotonConnected)
            {
                LogDebug("�����ӵ�Photon");
                OnPhotonConnected();
                yield break;
            }

            // �������ӣ�������ʱʹ��ģ�����ӣ�ʵ����Ŀ�е���PhotonNetworkAdapter��
            LogDebug("ģ�����ӵ�Photon������...");
            yield return new WaitForSeconds(2f); // ģ�������ӳ�

            // ģ�����ӳɹ�
            OnPhotonConnected();
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
                PhotonNetworkAdapter.OnPhotonRoomJoined -= OnPhotonRoomJoined;
                PhotonNetworkAdapter.OnPhotonRoomLeft -= OnPhotonRoomLeft;

                LogDebug("��ȡ������Photon�¼�");
            }
        }

        #endregion

        #region �������

        /// <summary>
        /// ��������
        /// </summary>
        public void CreateRoom(string roomName, int maxPlayers, string password = "")
        {
            if (!isConnected)
            {
                LogDebug("δ���ӵ����磬�޷���������");
                OnRoomCreated?.Invoke(roomName, false);
                return;
            }

            if (string.IsNullOrEmpty(roomName) || maxPlayers < 2)
            {
                LogDebug("��Ч�ķ������");
                OnRoomCreated?.Invoke(roomName, false);
                return;
            }

            LogDebug($"��������: {roomName}, �������: {maxPlayers}");

            // �׶�1����ʱģ�ⷿ�䴴��
            StartCoroutine(SimulateCreateRoom(roomName, maxPlayers, password));
        }

        /// <summary>
        /// ���뷿��
        /// </summary>
        public void JoinRoom(LobbyRoomData roomData)
        {
            if (!isConnected)
            {
                LogDebug("δ���ӵ����磬�޷����뷿��");
                OnRoomJoined?.Invoke(roomData.roomName, false);
                return;
            }

            LogDebug($"���뷿��: {roomData.roomName}");

            // �׶�1����ʱģ�ⷿ�����
            StartCoroutine(SimulateJoinRoom(roomData));
        }

        /// <summary>
        /// �����������
        /// </summary>
        public void JoinRandomRoom()
        {
            if (!isConnected)
            {
                LogDebug("δ���ӵ����磬�޷������������");
                return;
            }

            if (cachedRoomList.Count == 0)
            {
                LogDebug("û�п��õķ���");
                return;
            }

            // ѡ��һ���п�λ�ķ���
            foreach (var room in cachedRoomList)
            {
                if (room.currentPlayers < room.maxPlayers)
                {
                    JoinRoom(room);
                    return;
                }
            }

            LogDebug("û�пɼ���ķ���");
        }

        /// <summary>
        /// ˢ�·����б�
        /// </summary>
        public void RefreshRoomList()
        {
            if (!isConnected)
            {
                LogDebug("δ���ӵ����磬�޷�ˢ�·����б�");
                return;
            }

            LogDebug("ˢ�·����б�");
            StartCoroutine(SimulateGetRoomList());
        }

        /// <summary>
        /// �Ͽ�����
        /// </summary>
        public void Disconnect()
        {
            LogDebug("�Ͽ���������");

            if (PhotonNetworkAdapter.Instance != null)
            {
                PhotonNetworkAdapter.Instance.DisconnectPhoton();
            }

            OnPhotonDisconnected();
        }

        #endregion

        #region ģ������������׶�1ʹ�ã�

        /// <summary>
        /// ģ�ⴴ������
        /// </summary>
        private IEnumerator SimulateCreateRoom(string roomName, int maxPlayers, string password)
        {
            LogDebug("ģ�ⴴ������...");
            yield return new WaitForSeconds(1f);

            // �����·�������
            var newRoom = new LobbyRoomData
            {
                roomName = roomName,
                maxPlayers = maxPlayers,
                currentPlayers = 1,
                hasPassword = !string.IsNullOrEmpty(password),
                hostPlayerName = LobbySceneManager.Instance.GetCurrentPlayerData().playerName
            };

            cachedRoomList.Add(newRoom);
            OnRoomCreated?.Invoke(roomName, true);
            OnRoomListUpdated?.Invoke(new List<LobbyRoomData>(cachedRoomList));

            LogDebug($"���䴴���ɹ�: {roomName}");
        }

        /// <summary>
        /// ģ����뷿��
        /// </summary>
        private IEnumerator SimulateJoinRoom(LobbyRoomData roomData)
        {
            LogDebug("ģ����뷿��...");
            yield return new WaitForSeconds(1f);

            OnRoomJoined?.Invoke(roomData.roomName, true);
            LogDebug($"�������ɹ�: {roomData.roomName}");
        }

        /// <summary>
        /// ģ���ȡ�����б�
        /// </summary>
        private IEnumerator SimulateGetRoomList()
        {
            LogDebug("ģ���ȡ�����б�...");
            yield return new WaitForSeconds(1f);

            // ����һЩģ�ⷿ������
            if (cachedRoomList.Count == 0)
            {
                cachedRoomList.Add(new LobbyRoomData
                {
                    roomName = "���Է���1",
                    maxPlayers = 4,
                    currentPlayers = 2,
                    hasPassword = false,
                    hostPlayerName = "�������1"
                });

                cachedRoomList.Add(new LobbyRoomData
                {
                    roomName = "˽�˷���",
                    maxPlayers = 2,
                    currentPlayers = 1,
                    hasPassword = true,
                    hostPlayerName = "�������2"
                });
            }

            OnRoomListUpdated?.Invoke(new List<LobbyRoomData>(cachedRoomList));
            LogDebug($"�����б������ɣ��� {cachedRoomList.Count} ������");
        }

        #endregion

        #region Photon�¼�����

        private void OnPhotonConnected()
        {
            LogDebug("Photon���ӳɹ�");
            isConnecting = false;
            isConnected = true;
            OnConnectionStatusChanged?.Invoke(true);

            // ���ӳɹ����Զ�ˢ�·����б�
            RefreshRoomList();
        }

        private void OnPhotonDisconnected()
        {
            LogDebug("Photon���ӶϿ�");
            isConnecting = false;
            isConnected = false;
            OnConnectionStatusChanged?.Invoke(false);

            // ��շ����б�
            cachedRoomList.Clear();
            OnRoomListUpdated?.Invoke(new List<LobbyRoomData>());
        }

        private void OnPhotonRoomJoined()
        {
            LogDebug("�ɹ�����Photon����");
        }

        private void OnPhotonRoomLeft()
        {
            LogDebug("�뿪Photon����");
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
        /// ��ȡ����ķ����б�
        /// </summary>
        public List<LobbyRoomData> GetCachedRoomList()
        {
            return new List<LobbyRoomData>(cachedRoomList);
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
            string status = "=== Lobby����״̬ ===\n";
            status += $"������: {isConnected}\n";
            status += $"������: {isConnecting}\n";
            status += $"��������: {cachedRoomList.Count}";

            LogDebug(status);
        }

        [ContextMenu("ǿ��ˢ�·����б�")]
        public void ForceRefreshRoomList()
        {
            RefreshRoomList();
        }

        #endregion
    }
}