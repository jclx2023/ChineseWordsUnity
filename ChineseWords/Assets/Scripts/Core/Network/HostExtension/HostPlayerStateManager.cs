using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Core;
using Photon.Pun;
using Photon.Realtime;

namespace Core.Network
{
    /// <summary>
    /// �����Ϸ״̬���ݽṹ
    /// </summary>
    [System.Serializable]
    public class PlayerGameState
    {
        public ushort playerId;
        public string playerName;
        public int health;
        public int maxHealth;
        public bool isAlive;
        public bool isReady;
        public float lastActiveTime;

        public PlayerGameState()
        {
            lastActiveTime = Time.time;
            health = 100;      // Ĭ��ֵ��ʵ�ʻ��ڳ�ʼ��ʱ����
            maxHealth = 100;   // Ĭ��ֵ��ʵ�ʻ��ڳ�ʼ��ʱ����
        }

        public float GetHealthPercentage()
        {
            if (maxHealth <= 0) return 0f;
            return (float)health / maxHealth;
        }

        public bool IsFullHealth()
        {
            return health >= maxHealth;
        }

        public bool IsLowHealth()
        {
            return GetHealthPercentage() < 0.3f;
        }

        public bool IsCriticalHealth()
        {
            return GetHealthPercentage() < 0.1f;
        }
    }

    /// <summary>
    /// ���״̬������ - Photon�Ż���
    /// </summary>
    public class PlayerStateManager
    {
        [Header("��������")]
        private bool enableDebugLogs = true;

        private Dictionary<ushort, PlayerGameState> playerStates;

        public System.Action<ushort, string, bool> OnPlayerAdded;    // playerId, playerName, isHost
        public System.Action<ushort, string> OnPlayerRemoved;        // playerId, playerName
        public System.Action<List<ushort>> OnHostValidationFailed;   // duplicateHostIds
        public System.Action<ushort> OnHostValidationPassed;         // validHostId
        public System.Action OnPlayersCleared;

        private NetworkManager networkManager;

        /// <summary>
        /// ���캯��
        /// </summary>
        public PlayerStateManager()
        {
            playerStates = new Dictionary<ushort, PlayerGameState>();
            LogDebug("PlayerStateManager ʵ���Ѵ���");
        }

        #region ��ʼ��

        /// <summary>
        /// ��ʼ�����״̬������ - Photon�����
        /// </summary>
        public void Initialize(NetworkManager networkMgr = null)
        {
            LogDebug("��ʼ��PlayerStateManager...");

            networkManager = networkMgr ?? NetworkManager.Instance;

            if (networkManager == null)
            {
                Debug.LogWarning("[PlayerStateManager] NetworkManager����Ϊ�գ�Host��֤���ܿ�������");
            }

            // ��֤Photon����״̬
            if (!PhotonNetwork.InRoom)
            {
                Debug.LogWarning("[PlayerStateManager] δ��Photon�����У�ĳЩ���ܿ�������");
            }
            else
            {
                LogDebug($"Photon����״̬: {PhotonNetwork.CurrentRoom.Name}, �����: {PhotonNetwork.PlayerList.Length}");
            }

            LogDebug("PlayerStateManager��ʼ�����");
        }

        #endregion

        #region ��ҹ���

        /// <summary>
        /// ������ - Photon�����
        /// </summary>
        public bool AddPlayer(ushort playerId, string playerName, int initialHealth = 100, int maxHealth = 100, bool isReady = true)
        {
            if (playerStates.ContainsKey(playerId))
            {
                LogDebug($"��� {playerId} �Ѵ��ڣ������ظ����");
                return false;
            }

            // ʹ��Photon�ж��Ƿ�ΪHost���
            bool isHostPlayer = IsHostPlayerPhoton(playerId);

            // �������״̬
            var playerState = new PlayerGameState
            {
                playerId = playerId,
                playerName = isHostPlayer ? $"����{playerName}" : playerName, // ��Ƿ���
                health = initialHealth,
                maxHealth = maxHealth,
                isAlive = true,
                isReady = isReady,
                lastActiveTime = Time.time
            };

            playerStates[playerId] = playerState;

            LogDebug($"������: {playerState.playerName} (ID: {playerId}, IsHost: {isHostPlayer}, HP: {initialHealth}/{maxHealth})");

            // ��������¼�
            OnPlayerAdded?.Invoke(playerId, playerState.playerName, isHostPlayer);

            return true;
        }

        /// <summary>
        /// ��Photon����������
        /// </summary>
        public bool AddPlayerFromPhoton(Player photonPlayer, int initialHealth = 100, int maxHealth = 100)
        {
            if (photonPlayer == null) return false;

            ushort playerId = (ushort)photonPlayer.ActorNumber;
            string playerName = photonPlayer.NickName ?? $"���{playerId}";

            return AddPlayer(playerId, playerName, initialHealth, maxHealth, true);
        }

        /// <summary>
        /// �ӷ���ϵͳ�����ң������ݣ�
        /// </summary>
        public bool AddPlayerFromRoom(ushort playerId, string playerName, int initialHealth = 100, int maxHealth = 100)
        {
            return AddPlayer(playerId, playerName, initialHealth, maxHealth, true);
        }

        /// <summary>
        /// �Ƴ����
        /// </summary>
        public bool RemovePlayer(ushort playerId)
        {
            if (!playerStates.ContainsKey(playerId))
            {
                LogDebug($"��� {playerId} �����ڣ��޷��Ƴ�");
                return false;
            }

            string playerName = playerStates[playerId].playerName;
            playerStates.Remove(playerId);

            LogDebug($"�Ƴ����: {playerId} ({playerName}), ʣ�������: {playerStates.Count}");

            // �����Ƴ��¼�
            OnPlayerRemoved?.Invoke(playerId, playerName);

            return true;
        }

        /// <summary>
        /// ����������
        /// </summary>
        public void ClearAllPlayers()
        {
            int playerCount = playerStates.Count;
            playerStates.Clear();

            LogDebug($"������������״̬�����Ƴ� {playerCount} �����");

            // ��������¼�
            OnPlayersCleared?.Invoke();
        }

        #endregion

        #region ���״̬����

        /// <summary>
        /// �������Ѫ��
        /// </summary>
        public bool UpdatePlayerHealth(ushort playerId, int newHealth, int? maxHealth = null)
        {
            if (!playerStates.ContainsKey(playerId))
            {
                LogDebug($"��� {playerId} �����ڣ��޷�����Ѫ��");
                return false;
            }

            var playerState = playerStates[playerId];
            int oldHealth = playerState.health;

            playerState.health = newHealth;
            if (maxHealth.HasValue)
            {
                playerState.maxHealth = maxHealth.Value;
            }

            // ���´��״̬
            playerState.isAlive = newHealth > 0;
            playerState.lastActiveTime = Time.time;

            LogDebug($"������� {playerId} Ѫ��: {oldHealth} -> {newHealth} (���: {playerState.isAlive})");

            return true;
        }

        /// <summary>
        /// ������Ҵ��״̬
        /// </summary>
        public bool SetPlayerAlive(ushort playerId, bool isAlive)
        {
            if (!playerStates.ContainsKey(playerId))
            {
                LogDebug($"��� {playerId} �����ڣ��޷����ô��״̬");
                return false;
            }

            var playerState = playerStates[playerId];
            bool oldState = playerState.isAlive;

            playerState.isAlive = isAlive;
            playerState.lastActiveTime = Time.time;

            // �����Ϊ������Ѫ������
            if (!isAlive)
            {
                playerState.health = 0;
            }

            LogDebug($"������� {playerId} ���״̬: {oldState} -> {isAlive}");

            return true;
        }

        #endregion

        #region Host��֤ - Photon�����

        /// <summary>
        /// �ж�����Ƿ�ΪHost - Photon�汾
        /// </summary>
        private bool IsHostPlayerPhoton(ushort playerId)
        {
            if (!PhotonNetwork.InRoom) return false;

            return PhotonNetwork.MasterClient != null &&
                   PhotonNetwork.MasterClient.ActorNumber == playerId;
        }

        /// <summary>
        /// ��ȡPhoton MasterClient ID
        /// </summary>
        private ushort GetPhotonMasterClientId()
        {
            if (PhotonNetwork.InRoom && PhotonNetwork.MasterClient != null)
            {
                return (ushort)PhotonNetwork.MasterClient.ActorNumber;
            }
            return 0;
        }

        #endregion

        #region ��ѯ����

        public PlayerGameState GetPlayerState(ushort playerId)
        {
            return playerStates.ContainsKey(playerId) ? playerStates[playerId] : null;
        }

        public bool ContainsPlayer(ushort playerId)
        {
            return playerStates.ContainsKey(playerId);
        }

        public bool IsPlayerAlive(ushort playerId)
        {
            return playerStates.ContainsKey(playerId) && playerStates[playerId].isAlive;
        }

        public Dictionary<ushort, PlayerGameState> GetAllPlayerStates()
        {
            return new Dictionary<ushort, PlayerGameState>(playerStates);
        }

        public List<ushort> GetAlivePlayerIds()
        {
            return playerStates.Where(p => p.Value.isAlive).Select(p => p.Key).ToList();
        }

        public List<PlayerGameState> GetAlivePlayers()
        {
            return playerStates.Values.Where(p => p.isAlive).ToList();
        }

        public int GetPlayerCount()
        {
            return playerStates.Count;
        }

        public int GetAlivePlayerCount()
        {
            return playerStates.Values.Count(p => p.isAlive);
        }

        public int GetReadyPlayerCount()
        {
            return playerStates.Values.Count(p => p.isReady);
        }

        public bool AreAllPlayersReady()
        {
            return playerStates.Count > 0 && playerStates.Values.All(p => p.isReady);
        }

        public ushort GetHostPlayerId()
        {
            // ����ʹ��Photon��MasterClient
            ushort photonMasterClientId = GetPhotonMasterClientId();
            if (photonMasterClientId != 0)
            {
                return photonMasterClientId;
            }

            var hostPlayer = playerStates.Values.FirstOrDefault(p => p.playerName.Contains("����"));
            return hostPlayer?.playerId ?? 0;
        }

        #endregion

        #region ��������ͬ�� - Photon�Ż���

        /// <summary>
        /// ��Photon����ͬ���������
        /// </summary>
        public int SyncFromPhotonRoom(int initialHealth = 100)
        {
            LogDebug("��Photon����ͬ���������");

            if (!PhotonNetwork.InRoom)
            {
                Debug.LogWarning("[PlayerStateManager] δ��Photon�����У��޷�ͬ���������");
                return 0;
            }

            // ����������״̬
            ClearAllPlayers();

            int syncedCount = 0;

            // ��Photon����ͬ���������
            foreach (var photonPlayer in PhotonNetwork.PlayerList)
            {
                if (AddPlayerFromPhoton(photonPlayer, initialHealth, initialHealth))
                {
                    syncedCount++;
                }
            }

            LogDebug($"Photon�������ͬ����ɣ��ܼ������: {syncedCount}");
            return syncedCount;
        }

        /// <summary>
        /// �ӷ���ϵͳͬ��������ݣ������ݣ�����ʹ��Photon��
        /// </summary>
        public int SyncFromRoomSystem(int initialHealth = 100)
        {
            LogDebug("�ӷ���ϵͳͬ���������");

            // ����ʹ��Photon��������
            if (PhotonNetwork.InRoom)
            {
                return SyncFromPhotonRoom(initialHealth);
            }

            // �����ݣ���RoomManager��ȡ
            if (RoomManager.Instance != null && RoomManager.Instance.IsInRoom)
            {
                LogDebug($"��RoomManagerͬ����ʹ��Photon�������ݣ������: {RoomManager.Instance.PlayerCount}");

                ClearAllPlayers();
                int syncedCount = 0;

                // ֱ��ʹ��PhotonNetwork.PlayerList
                foreach (var photonPlayer in PhotonNetwork.PlayerList)
                {
                    ushort playerId = (ushort)photonPlayer.ActorNumber;
                    string playerName = photonPlayer.NickName ?? $"���{playerId}";

                    if (AddPlayerFromRoom(playerId, playerName, initialHealth, initialHealth))
                    {
                        syncedCount++;
                    }
                }

                LogDebug($"�������ͬ����ɣ��ܼ������: {syncedCount}");
                return syncedCount;
            }

            // ����ã���NetworkManager��ȡ������еĻ���
            if (networkManager != null)
            {
                LogDebug("��NetworkManager��ȡ������Ϣ...");

                ushort hostPlayerId = GetPhotonMasterClientId();
                if (hostPlayerId != 0)
                {
                    ClearAllPlayers();
                    if (AddPlayer(hostPlayerId, "����", initialHealth, initialHealth))
                    {
                        LogDebug($"���Host���: ID={hostPlayerId}");
                        return 1;
                    }
                }
            }

            LogDebug("�޷����κ���Դͬ���������");
            return 0;
        }

        #endregion

        #region ���߷���

        /// <summary>
        /// ������־���
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                //Debug.Log($"[PlayerStateManager] {message}");
            }
        }

        #endregion

        #region ���ٺ�����

        /// <summary>
        /// �������״̬������
        /// </summary>
        public void Dispose()
        {
            // �����¼�
            OnPlayerAdded = null;
            OnPlayerRemoved = null;
            OnHostValidationFailed = null;
            OnHostValidationPassed = null;
            OnPlayersCleared = null;

            // �������״̬
            ClearAllPlayers();

            // ��������
            networkManager = null;

            LogDebug("PlayerStateManager������");
        }

        #endregion
    }
}