using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Core;

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
    /// ���״̬������
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
        public PlayerStateManager()
        {
            playerStates = new Dictionary<ushort, PlayerGameState>();
            LogDebug("PlayerStateManager ʵ���Ѵ���");
        }

        #region ��ʼ��

        /// <summary>
        /// ��ʼ�����״̬������
        /// </summary>
        public void Initialize(NetworkManager networkMgr = null)
        {
            LogDebug("��ʼ��PlayerStateManager...");

            networkManager = networkMgr ?? NetworkManager.Instance;

            if (networkManager == null)
            {
                Debug.LogWarning("[PlayerStateManager] NetworkManager����Ϊ�գ�Host��֤���ܿ�������");
            }

            LogDebug("PlayerStateManager��ʼ�����");
        }

        #endregion

        #region ��ҹ���

        /// <summary>
        /// ������
        /// </summary>
        public bool AddPlayer(ushort playerId, string playerName, int initialHealth = 100, int maxHealth = 100, bool isReady = true)
        {
            if (playerStates.ContainsKey(playerId))
            {
                LogDebug($"��� {playerId} �Ѵ��ڣ������ظ����");
                return false;
            }

            // �ж��Ƿ�ΪHost���
            bool isHostPlayer = IsHostPlayer(playerId);

            // �������״̬
            var playerState = new PlayerGameState
            {
                playerId = playerId,
                playerName = isHostPlayer ? "����" : playerName,
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

        public bool AddPlayerFromRoom(ushort playerId, string playerName, int initialHealth = 100, int maxHealth = 100)
        {
            return AddPlayer(playerId, playerName, initialHealth, maxHealth, true); // �ӷ������Ķ���׼���õ�
        }

        /// <summary>
        /// �Ƴ����
        /// </summary>
        /// <param name="playerId">���ID</param>
        /// <returns>�Ƿ�ɹ��Ƴ�</returns>
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
        /// <param name="playerId">���ID</param>
        /// <param name="newHealth">��Ѫ��</param>
        /// <param name="maxHealth">���Ѫ������ѡ��</param>
        /// <returns>�Ƿ�ɹ�����</returns>
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
        /// <param name="playerId">���ID</param>
        /// <param name="isAlive">�Ƿ���</param>
        /// <returns>�Ƿ�ɹ�����</returns>
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

        /// <summary>
        /// �������׼��״̬
        /// </summary>
        /// <param name="playerId">���ID</param>
        /// <param name="isReady">�Ƿ�׼������</param>
        /// <returns>�Ƿ�ɹ�����</returns>
        public bool SetPlayerReady(ushort playerId, bool isReady)
        {
            if (!playerStates.ContainsKey(playerId))
            {
                LogDebug($"��� {playerId} �����ڣ��޷�����׼��״̬");
                return false;
            }

            var playerState = playerStates[playerId];
            playerState.isReady = isReady;
            playerState.lastActiveTime = Time.time;

            LogDebug($"������� {playerId} ׼��״̬: {isReady}");

            return true;
        }

        #endregion

        #region Host��֤

        /// <summary>
        /// ��֤Host�������޸��ظ�����
        /// </summary>
        /// <returns>��֤�Ƿ�ͨ��</returns>
        public bool ValidateAndFixHostCount()
        {
            LogDebug("��ʼ��֤Host����...");

            var hostPlayers = GetHostPlayers();

            if (hostPlayers.Count == 0)
            {
                Debug.LogWarning("[PlayerStateManager] û���ҵ�����");
                return false;
            }
            else if (hostPlayers.Count == 1)
            {
                var hostPlayer = hostPlayers[0];
                LogDebug($"Host��֤ͨ��: ID={hostPlayer.playerId}, Name={hostPlayer.playerName}");

                // ��֤��NetworkManager��һ����
                if (networkManager != null)
                {
                    ushort networkHostId = networkManager.GetHostPlayerId();
                    if (hostPlayer.playerId != networkHostId)
                    {
                        Debug.LogError($"[PlayerStateManager] Host ID��һ�£���Ϸ��: {hostPlayer.playerId}, NetworkManager: {networkHostId}");
                        return false;
                    }
                }

                OnHostValidationPassed?.Invoke(hostPlayer.playerId);
                return true;
            }
            else
            {
                Debug.LogError($"[PlayerStateManager] ��⵽�������: {hostPlayers.Count} ��");

                // �ռ��ظ�Host��ID
                var duplicateHostIds = hostPlayers.Select(h => h.playerId).ToList();
                OnHostValidationFailed?.Invoke(duplicateHostIds);

                // �����޸�
                return FixDuplicateHosts();
            }
        }

        /// <summary>
        /// �޸��ظ�Host����
        /// </summary>
        /// <returns>�Ƿ��޸��ɹ�</returns>
        public bool FixDuplicateHosts()
        {
            if (networkManager == null)
            {
                Debug.LogError("[PlayerStateManager] NetworkManagerΪ�գ��޷��޸��ظ�Host");
                return false;
            }

            ushort correctHostId = networkManager.GetHostPlayerId();
            LogDebug($"�޸��ظ���������ȷ��Host ID: {correctHostId}");

            var hostPlayers = GetHostPlayers();
            if (hostPlayers.Count <= 1)
            {
                LogDebug("�������������������޸�");
                return true;
            }

            LogDebug($"���� {hostPlayers.Count} ����������ʼ�޸�");

            // ������ȷID�ķ������Ƴ�������
            var correctHost = hostPlayers.FirstOrDefault(h => h.playerId == correctHostId);

            if (correctHost != null)
            {
                LogDebug($"������ȷ����: ID={correctHost.playerId}, Name={correctHost.playerName}");

                // �Ƴ�����������������������ݣ�ֻ�޸����ƣ�
                var duplicateHosts = hostPlayers.Where(h => h.playerId != correctHostId).ToList();
                foreach (var duplicateHost in duplicateHosts)
                {
                    // �޸�����Ϊ��ͨ���
                    duplicateHost.playerName = $"���{duplicateHost.playerId}";
                    LogDebug($"�����ظ�����Ϊ��ͨ���: ID={duplicateHost.playerId}, ������={duplicateHost.playerName}");
                }
            }
            else
            {
                // ���û���ҵ���ȷID�ķ�����������СID�ķ���
                var primaryHost = hostPlayers.OrderBy(h => h.playerId).First();
                var duplicateHosts = hostPlayers.Skip(1).ToList();

                LogDebug($"����������: ID={primaryHost.playerId}, ���������ظ�����");

                foreach (var duplicateHost in duplicateHosts)
                {
                    duplicateHost.playerName = $"���{duplicateHost.playerId}";
                    LogDebug($"�����ظ�����Ϊ��ͨ���: ID={duplicateHost.playerId}");
                }
            }

            // ������֤
            LogDebug($"�޸���ɣ���ǰ�����: {playerStates.Count}");
            return ValidateHostCount();
        }

        /// <summary>
        /// ����֤Host���������޸���
        /// </summary>
        /// <returns>��֤�Ƿ�ͨ��</returns>
        public bool ValidateHostCount()
        {
            var hostPlayers = GetHostPlayers();
            return hostPlayers.Count == 1;
        }

        /// <summary>
        /// ��ȡ����Host���
        /// </summary>
        /// <returns>Host����б�</returns>
        private List<PlayerGameState> GetHostPlayers()
        {
            return playerStates.Values.Where(p => p.playerName.Contains("����")).ToList();
        }

        /// <summary>
        /// �ж�����Ƿ�ΪHost
        /// </summary>
        /// <param name="playerId">���ID</param>
        /// <returns>�Ƿ�ΪHost</returns>
        private bool IsHostPlayer(ushort playerId)
        {
            return networkManager?.IsHostPlayer(playerId) ?? false;
        }

        #endregion

        #region ��ѯ����

        /// <summary>
        /// ��ȡ���״̬
        /// </summary>
        /// <param name="playerId">���ID</param>
        /// <returns>���״̬���������򷵻�null</returns>
        public PlayerGameState GetPlayerState(ushort playerId)
        {
            return playerStates.ContainsKey(playerId) ? playerStates[playerId] : null;
        }

        /// <summary>
        /// �������Ƿ����
        /// </summary>
        /// <param name="playerId">���ID</param>
        /// <returns>�Ƿ����</returns>
        public bool ContainsPlayer(ushort playerId)
        {
            return playerStates.ContainsKey(playerId);
        }

        /// <summary>
        /// �������Ƿ���
        /// </summary>
        /// <param name="playerId">���ID</param>
        /// <returns>�Ƿ���</returns>
        public bool IsPlayerAlive(ushort playerId)
        {
            return playerStates.ContainsKey(playerId) && playerStates[playerId].isAlive;
        }

        /// <summary>
        /// ��ȡ�������״̬
        /// </summary>
        /// <returns>���״̬�ֵ�ĸ���</returns>
        public Dictionary<ushort, PlayerGameState> GetAllPlayerStates()
        {
            return new Dictionary<ushort, PlayerGameState>(playerStates);
        }

        /// <summary>
        /// ��ȡ�������б�
        /// </summary>
        /// <returns>������ID�б�</returns>
        public List<ushort> GetAlivePlayerIds()
        {
            return playerStates.Where(p => p.Value.isAlive).Select(p => p.Key).ToList();
        }

        /// <summary>
        /// ��ȡ������״̬�б�
        /// </summary>
        /// <returns>������״̬�б�</returns>
        public List<PlayerGameState> GetAlivePlayers()
        {
            return playerStates.Values.Where(p => p.isAlive).ToList();
        }

        /// <summary>
        /// ��ȡ�������
        /// </summary>
        /// <returns>�������</returns>
        public int GetPlayerCount()
        {
            return playerStates.Count;
        }

        /// <summary>
        /// ��ȡ����������
        /// </summary>
        /// <returns>����������</returns>
        public int GetAlivePlayerCount()
        {
            return playerStates.Values.Count(p => p.isAlive);
        }

        /// <summary>
        /// ��ȡ׼���������������
        /// </summary>
        /// <returns>׼���������������</returns>
        public int GetReadyPlayerCount()
        {
            return playerStates.Values.Count(p => p.isReady);
        }

        /// <summary>
        /// ����Ƿ�������Ҷ���׼��
        /// </summary>
        /// <returns>�Ƿ�������Ҷ���׼��</returns>
        public bool AreAllPlayersReady()
        {
            return playerStates.Count > 0 && playerStates.Values.All(p => p.isReady);
        }

        /// <summary>
        /// ��ȡHost���ID
        /// </summary>
        /// <returns>Host���ID��δ�ҵ�����0</returns>
        public ushort GetHostPlayerId()
        {
            var hostPlayer = playerStates.Values.FirstOrDefault(p => p.playerName.Contains("����"));
            return hostPlayer?.playerId ?? 0;
        }

        #endregion

        #region ״̬��Ϣ

        /// <summary>
        /// ��ȡ���״̬������״̬��Ϣ
        /// </summary>
        /// <returns>״̬��Ϣ�ַ���</returns>
        public string GetStatusInfo()
        {
            var status = "=== PlayerStateManager״̬ ===\n";
            status += $"�������: {GetPlayerCount()}\n";
            status += $"��������: {GetAlivePlayerCount()}\n";
            status += $"׼�������: {GetReadyPlayerCount()}\n";
            status += $"���������׼��: {(AreAllPlayersReady() ? "��" : "��")}\n";

            var hostId = GetHostPlayerId();
            status += $"Host���ID: {(hostId != 0 ? hostId.ToString() : "δ�ҵ�")}\n";

            if (playerStates.Count > 0)
            {
                status += "�������:\n";
                foreach (var player in playerStates.Values)
                {
                    status += $"  - {player.playerName} (ID: {player.playerId}) ";
                    status += $"HP: {player.health}/{player.maxHealth} ";
                    status += $"���: {(player.isAlive ? "��" : "��")} ";
                    status += $"׼��: {(player.isReady ? "��" : "��")}\n";
                }
            }

            return status;
        }

        #endregion

        #region ��������ͬ��
        public int SyncFromRoomSystem(int initialHealth = 100)
        {
            LogDebug("�ӷ���ϵͳͬ���������");

            // ����������״̬�������ظ�
            ClearAllPlayers();

            int syncedCount = 0;

            // ����1����RoomManager��ȡ
            if (RoomManager.Instance?.CurrentRoom != null)
            {
                var room = RoomManager.Instance.CurrentRoom;
                LogDebug($"��RoomManagerͬ�������������: {room.players.Count}");

                foreach (var roomPlayer in room.players.Values)
                {
                    if (AddPlayerFromRoom(roomPlayer.playerId, roomPlayer.playerName, initialHealth, initialHealth))
                    {
                        syncedCount++;
                    }
                }

                LogDebug($"�������ͬ����ɣ��ܼ������: {syncedCount}");
                return syncedCount;
            }

            // ����2����NetworkManager������״̬��ȡ
            if (networkManager != null)
            {
                LogDebug($"��NetworkManagerͬ�������������: {networkManager.ConnectedPlayerCount}");

                ushort hostPlayerId = networkManager.GetHostPlayerId();
                if (hostPlayerId != 0 && networkManager.IsHostClientReady)
                {
                    if (AddPlayerFromRoom(hostPlayerId, "����", initialHealth, initialHealth))
                    {
                        syncedCount++;
                        LogDebug($"���Host���: ID={hostPlayerId}");
                    }
                }
                else
                {
                    LogDebug($"Host�����δ׼������: ID={hostPlayerId}, Ready={networkManager.IsHostClientReady}");
                }
            }

            LogDebug($"���ͬ����ɣ��ܼ������: {syncedCount}");
            return syncedCount;
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
                Debug.Log($"[PlayerStateManager] {message}");
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