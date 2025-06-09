using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Core;
using Photon.Pun;
using Photon.Realtime;

namespace Core.Network
{
    /// <summary>
    /// 玩家游戏状态数据结构
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
            health = 100;      // 默认值，实际会在初始化时设置
            maxHealth = 100;   // 默认值，实际会在初始化时设置
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
    /// 玩家状态管理器 - Photon优化版
    /// </summary>
    public class PlayerStateManager
    {
        [Header("调试设置")]
        private bool enableDebugLogs = true;

        private Dictionary<ushort, PlayerGameState> playerStates;

        public System.Action<ushort, string, bool> OnPlayerAdded;    // playerId, playerName, isHost
        public System.Action<ushort, string> OnPlayerRemoved;        // playerId, playerName
        public System.Action<List<ushort>> OnHostValidationFailed;   // duplicateHostIds
        public System.Action<ushort> OnHostValidationPassed;         // validHostId
        public System.Action OnPlayersCleared;

        private NetworkManager networkManager;

        /// <summary>
        /// 构造函数
        /// </summary>
        public PlayerStateManager()
        {
            playerStates = new Dictionary<ushort, PlayerGameState>();
            LogDebug("PlayerStateManager 实例已创建");
        }

        #region 初始化

        /// <summary>
        /// 初始化玩家状态管理器 - Photon适配版
        /// </summary>
        public void Initialize(NetworkManager networkMgr = null)
        {
            LogDebug("初始化PlayerStateManager...");

            networkManager = networkMgr ?? NetworkManager.Instance;

            if (networkManager == null)
            {
                Debug.LogWarning("[PlayerStateManager] NetworkManager引用为空，Host验证功能可能受限");
            }

            // 验证Photon连接状态
            if (!PhotonNetwork.InRoom)
            {
                Debug.LogWarning("[PlayerStateManager] 未在Photon房间中，某些功能可能受限");
            }
            else
            {
                LogDebug($"Photon房间状态: {PhotonNetwork.CurrentRoom.Name}, 玩家数: {PhotonNetwork.PlayerList.Length}");
            }

            LogDebug("PlayerStateManager初始化完成");
        }

        #endregion

        #region 玩家管理

        /// <summary>
        /// 添加玩家 - Photon适配版
        /// </summary>
        public bool AddPlayer(ushort playerId, string playerName, int initialHealth = 100, int maxHealth = 100, bool isReady = true)
        {
            if (playerStates.ContainsKey(playerId))
            {
                LogDebug($"玩家 {playerId} 已存在，跳过重复添加");
                return false;
            }

            // 使用Photon判断是否为Host玩家
            bool isHostPlayer = IsHostPlayerPhoton(playerId);

            // 创建玩家状态
            var playerState = new PlayerGameState
            {
                playerId = playerId,
                playerName = isHostPlayer ? $"房主{playerName}" : playerName, // 标记房主
                health = initialHealth,
                maxHealth = maxHealth,
                isAlive = true,
                isReady = isReady,
                lastActiveTime = Time.time
            };

            playerStates[playerId] = playerState;

            LogDebug($"添加玩家: {playerState.playerName} (ID: {playerId}, IsHost: {isHostPlayer}, HP: {initialHealth}/{maxHealth})");

            // 触发添加事件
            OnPlayerAdded?.Invoke(playerId, playerState.playerName, isHostPlayer);

            return true;
        }

        /// <summary>
        /// 从Photon房间添加玩家
        /// </summary>
        public bool AddPlayerFromPhoton(Player photonPlayer, int initialHealth = 100, int maxHealth = 100)
        {
            if (photonPlayer == null) return false;

            ushort playerId = (ushort)photonPlayer.ActorNumber;
            string playerName = photonPlayer.NickName ?? $"玩家{playerId}";

            return AddPlayer(playerId, playerName, initialHealth, maxHealth, true);
        }

        /// <summary>
        /// 从房间系统添加玩家（向后兼容）
        /// </summary>
        public bool AddPlayerFromRoom(ushort playerId, string playerName, int initialHealth = 100, int maxHealth = 100)
        {
            return AddPlayer(playerId, playerName, initialHealth, maxHealth, true);
        }

        /// <summary>
        /// 移除玩家
        /// </summary>
        public bool RemovePlayer(ushort playerId)
        {
            if (!playerStates.ContainsKey(playerId))
            {
                LogDebug($"玩家 {playerId} 不存在，无法移除");
                return false;
            }

            string playerName = playerStates[playerId].playerName;
            playerStates.Remove(playerId);

            LogDebug($"移除玩家: {playerId} ({playerName}), 剩余玩家数: {playerStates.Count}");

            // 触发移除事件
            OnPlayerRemoved?.Invoke(playerId, playerName);

            return true;
        }

        /// <summary>
        /// 清空所有玩家
        /// </summary>
        public void ClearAllPlayers()
        {
            int playerCount = playerStates.Count;
            playerStates.Clear();

            LogDebug($"已清空所有玩家状态，共移除 {playerCount} 个玩家");

            // 触发清空事件
            OnPlayersCleared?.Invoke();
        }

        #endregion

        #region 玩家状态更新

        /// <summary>
        /// 更新玩家血量
        /// </summary>
        public bool UpdatePlayerHealth(ushort playerId, int newHealth, int? maxHealth = null)
        {
            if (!playerStates.ContainsKey(playerId))
            {
                LogDebug($"玩家 {playerId} 不存在，无法更新血量");
                return false;
            }

            var playerState = playerStates[playerId];
            int oldHealth = playerState.health;

            playerState.health = newHealth;
            if (maxHealth.HasValue)
            {
                playerState.maxHealth = maxHealth.Value;
            }

            // 更新存活状态
            playerState.isAlive = newHealth > 0;
            playerState.lastActiveTime = Time.time;

            LogDebug($"更新玩家 {playerId} 血量: {oldHealth} -> {newHealth} (存活: {playerState.isAlive})");

            return true;
        }

        /// <summary>
        /// 设置玩家存活状态
        /// </summary>
        public bool SetPlayerAlive(ushort playerId, bool isAlive)
        {
            if (!playerStates.ContainsKey(playerId))
            {
                LogDebug($"玩家 {playerId} 不存在，无法设置存活状态");
                return false;
            }

            var playerState = playerStates[playerId];
            bool oldState = playerState.isAlive;

            playerState.isAlive = isAlive;
            playerState.lastActiveTime = Time.time;

            // 如果设为死亡，血量归零
            if (!isAlive)
            {
                playerState.health = 0;
            }

            LogDebug($"设置玩家 {playerId} 存活状态: {oldState} -> {isAlive}");

            return true;
        }

        /// <summary>
        /// 设置玩家准备状态
        /// </summary>
        public bool SetPlayerReady(ushort playerId, bool isReady)
        {
            if (!playerStates.ContainsKey(playerId))
            {
                LogDebug($"玩家 {playerId} 不存在，无法设置准备状态");
                return false;
            }

            var playerState = playerStates[playerId];
            playerState.isReady = isReady;
            playerState.lastActiveTime = Time.time;

            LogDebug($"设置玩家 {playerId} 准备状态: {isReady}");

            return true;
        }

        #endregion

        #region Host验证 - Photon适配版

        /// <summary>
        /// 验证Host数量并修复重复问题 - Photon适配版
        /// </summary>
        public bool ValidateAndFixHostCount()
        {
            LogDebug("开始验证Host数量...");

            var hostPlayers = GetHostPlayers();

            if (hostPlayers.Count == 0)
            {
                Debug.LogWarning("[PlayerStateManager] 没有找到房主");
                return false;
            }
            else if (hostPlayers.Count == 1)
            {
                var hostPlayer = hostPlayers[0];
                LogDebug($"Host验证通过: ID={hostPlayer.playerId}, Name={hostPlayer.playerName}");

                // 验证与Photon的一致性
                ushort photonMasterClientId = GetPhotonMasterClientId();
                if (hostPlayer.playerId != photonMasterClientId)
                {
                    Debug.LogError($"[PlayerStateManager] Host ID与Photon不一致！游戏中: {hostPlayer.playerId}, Photon MasterClient: {photonMasterClientId}");
                    return false;
                }

                OnHostValidationPassed?.Invoke(hostPlayer.playerId);
                return true;
            }
            else
            {
                Debug.LogError($"[PlayerStateManager] 检测到多个房主: {hostPlayers.Count} 个");

                // 收集重复Host的ID
                var duplicateHostIds = hostPlayers.Select(h => h.playerId).ToList();
                OnHostValidationFailed?.Invoke(duplicateHostIds);

                // 尝试修复
                return FixDuplicateHostsPhoton();
            }
        }

        /// <summary>
        /// 修复重复Host问题 - Photon适配版
        /// </summary>
        public bool FixDuplicateHostsPhoton()
        {
            ushort correctHostId = GetPhotonMasterClientId();
            if (correctHostId == 0)
            {
                Debug.LogError("[PlayerStateManager] 无法获取Photon MasterClient ID，无法修复重复Host");
                return false;
            }

            LogDebug($"修复重复房主，正确的Host ID: {correctHostId}");

            var hostPlayers = GetHostPlayers();
            if (hostPlayers.Count <= 1)
            {
                LogDebug("房主数量正常，无需修复");
                return true;
            }

            LogDebug($"发现 {hostPlayers.Count} 个房主，开始修复");

            // 保留正确ID的房主，修改其他的
            var correctHost = hostPlayers.FirstOrDefault(h => h.playerId == correctHostId);

            if (correctHost != null)
            {
                LogDebug($"保留正确房主: ID={correctHost.playerId}, Name={correctHost.playerName}");

                // 修改其他房主为普通玩家
                var duplicateHosts = hostPlayers.Where(h => h.playerId != correctHostId).ToList();
                foreach (var duplicateHost in duplicateHosts)
                {
                    // 移除"房主"标记
                    duplicateHost.playerName = duplicateHost.playerName.Replace("房主", "").Trim();
                    if (string.IsNullOrEmpty(duplicateHost.playerName))
                    {
                        duplicateHost.playerName = $"玩家{duplicateHost.playerId}";
                    }
                    LogDebug($"修正重复房主为普通玩家: ID={duplicateHost.playerId}, 新名称={duplicateHost.playerName}");
                }
            }
            else
            {
                Debug.LogWarning($"[PlayerStateManager] 找不到正确ID({correctHostId})的房主，保留第一个房主");

                var primaryHost = hostPlayers.OrderBy(h => h.playerId).First();
                var duplicateHosts = hostPlayers.Skip(1).ToList();

                foreach (var duplicateHost in duplicateHosts)
                {
                    duplicateHost.playerName = duplicateHost.playerName.Replace("房主", "").Trim();
                    if (string.IsNullOrEmpty(duplicateHost.playerName))
                    {
                        duplicateHost.playerName = $"玩家{duplicateHost.playerId}";
                    }
                    LogDebug($"修正重复房主为普通玩家: ID={duplicateHost.playerId}");
                }
            }

            // 重新验证
            LogDebug($"修复完成，当前玩家数: {playerStates.Count}");
            return ValidateHostCount();
        }

        /// <summary>
        /// 简单验证Host数量（不修复）
        /// </summary>
        public bool ValidateHostCount()
        {
            var hostPlayers = GetHostPlayers();
            return hostPlayers.Count == 1;
        }

        /// <summary>
        /// 获取所有Host玩家
        /// </summary>
        private List<PlayerGameState> GetHostPlayers()
        {
            return playerStates.Values.Where(p => p.playerName.Contains("房主")).ToList();
        }

        /// <summary>
        /// 判断玩家是否为Host - Photon版本
        /// </summary>
        private bool IsHostPlayerPhoton(ushort playerId)
        {
            if (!PhotonNetwork.InRoom) return false;

            return PhotonNetwork.MasterClient != null &&
                   PhotonNetwork.MasterClient.ActorNumber == playerId;
        }

        /// <summary>
        /// 获取Photon MasterClient ID
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

        #region 查询方法

        /// <summary>
        /// 获取玩家状态
        /// </summary>
        public PlayerGameState GetPlayerState(ushort playerId)
        {
            return playerStates.ContainsKey(playerId) ? playerStates[playerId] : null;
        }

        /// <summary>
        /// 检查玩家是否存在
        /// </summary>
        public bool ContainsPlayer(ushort playerId)
        {
            return playerStates.ContainsKey(playerId);
        }

        /// <summary>
        /// 检查玩家是否存活
        /// </summary>
        public bool IsPlayerAlive(ushort playerId)
        {
            return playerStates.ContainsKey(playerId) && playerStates[playerId].isAlive;
        }

        /// <summary>
        /// 获取所有玩家状态
        /// </summary>
        public Dictionary<ushort, PlayerGameState> GetAllPlayerStates()
        {
            return new Dictionary<ushort, PlayerGameState>(playerStates);
        }

        /// <summary>
        /// 获取存活玩家列表
        /// </summary>
        public List<ushort> GetAlivePlayerIds()
        {
            return playerStates.Where(p => p.Value.isAlive).Select(p => p.Key).ToList();
        }

        /// <summary>
        /// 获取存活玩家状态列表
        /// </summary>
        public List<PlayerGameState> GetAlivePlayers()
        {
            return playerStates.Values.Where(p => p.isAlive).ToList();
        }

        /// <summary>
        /// 获取玩家总数
        /// </summary>
        public int GetPlayerCount()
        {
            return playerStates.Count;
        }

        /// <summary>
        /// 获取存活玩家数量
        /// </summary>
        public int GetAlivePlayerCount()
        {
            return playerStates.Values.Count(p => p.isAlive);
        }

        /// <summary>
        /// 获取准备就绪的玩家数量
        /// </summary>
        public int GetReadyPlayerCount()
        {
            return playerStates.Values.Count(p => p.isReady);
        }

        /// <summary>
        /// 检查是否所有玩家都已准备
        /// </summary>
        public bool AreAllPlayersReady()
        {
            return playerStates.Count > 0 && playerStates.Values.All(p => p.isReady);
        }

        /// <summary>
        /// 获取Host玩家ID - Photon适配版
        /// </summary>
        public ushort GetHostPlayerId()
        {
            // 优先使用Photon的MasterClient
            ushort photonMasterClientId = GetPhotonMasterClientId();
            if (photonMasterClientId != 0)
            {
                return photonMasterClientId;
            }

            // 备用：从玩家状态中查找
            var hostPlayer = playerStates.Values.FirstOrDefault(p => p.playerName.Contains("房主"));
            return hostPlayer?.playerId ?? 0;
        }

        #endregion

        #region 房间数据同步 - Photon优化版

        /// <summary>
        /// 从Photon房间同步玩家数据
        /// </summary>
        public int SyncFromPhotonRoom(int initialHealth = 100)
        {
            LogDebug("从Photon房间同步玩家数据");

            if (!PhotonNetwork.InRoom)
            {
                Debug.LogWarning("[PlayerStateManager] 未在Photon房间中，无法同步玩家数据");
                return 0;
            }

            // 清空现有玩家状态
            ClearAllPlayers();

            int syncedCount = 0;

            // 从Photon房间同步所有玩家
            foreach (var photonPlayer in PhotonNetwork.PlayerList)
            {
                if (AddPlayerFromPhoton(photonPlayer, initialHealth, initialHealth))
                {
                    syncedCount++;
                }
            }

            LogDebug($"Photon房间玩家同步完成，总计玩家数: {syncedCount}");
            return syncedCount;
        }

        /// <summary>
        /// 从房间系统同步玩家数据（向后兼容，优先使用Photon）
        /// </summary>
        public int SyncFromRoomSystem(int initialHealth = 100)
        {
            LogDebug("从房间系统同步玩家数据");

            // 优先使用Photon房间数据
            if (PhotonNetwork.InRoom)
            {
                return SyncFromPhotonRoom(initialHealth);
            }

            // 向后兼容：从RoomManager获取
            if (RoomManager.Instance != null && RoomManager.Instance.IsInRoom)
            {
                LogDebug($"从RoomManager同步，使用Photon房间数据，玩家数: {RoomManager.Instance.PlayerCount}");

                ClearAllPlayers();
                int syncedCount = 0;

                // 直接使用PhotonNetwork.PlayerList
                foreach (var photonPlayer in PhotonNetwork.PlayerList)
                {
                    ushort playerId = (ushort)photonPlayer.ActorNumber;
                    string playerName = photonPlayer.NickName ?? $"玩家{playerId}";

                    if (AddPlayerFromRoom(playerId, playerName, initialHealth, initialHealth))
                    {
                        syncedCount++;
                    }
                }

                LogDebug($"房间玩家同步完成，总计玩家数: {syncedCount}");
                return syncedCount;
            }

            // 最后备用：从NetworkManager获取（如果有的话）
            if (networkManager != null)
            {
                LogDebug("从NetworkManager获取基本信息...");

                ushort hostPlayerId = GetPhotonMasterClientId();
                if (hostPlayerId != 0)
                {
                    ClearAllPlayers();
                    if (AddPlayer(hostPlayerId, "房主", initialHealth, initialHealth))
                    {
                        LogDebug($"添加Host玩家: ID={hostPlayerId}");
                        return 1;
                    }
                }
            }

            LogDebug("无法从任何来源同步玩家数据");
            return 0;
        }

        #endregion

        #region 状态信息

        /// <summary>
        /// 获取玩家状态管理器状态信息 - Photon增强版
        /// </summary>
        public string GetStatusInfo()
        {
            var status = "=== PlayerStateManager状态 (Photon版) ===\n";
            status += $"玩家总数: {GetPlayerCount()}\n";
            status += $"存活玩家数: {GetAlivePlayerCount()}\n";
            status += $"准备玩家数: {GetReadyPlayerCount()}\n";
            status += $"所有玩家已准备: {(AreAllPlayersReady() ? "是" : "否")}\n";

            var hostId = GetHostPlayerId();
            status += $"Host玩家ID: {(hostId != 0 ? hostId.ToString() : "未找到")}\n";

            // Photon房间信息
            if (PhotonNetwork.InRoom)
            {
                status += $"Photon房间: {PhotonNetwork.CurrentRoom.Name}\n";
                status += $"Photon玩家数: {PhotonNetwork.PlayerList.Length}\n";
                status += $"Photon MasterClient: {PhotonNetwork.MasterClient?.ActorNumber ?? 0}\n";
            }
            else
            {
                status += "Photon房间: 未连接\n";
            }

            if (playerStates.Count > 0)
            {
                status += "玩家详情:\n";
                foreach (var player in playerStates.Values)
                {
                    status += $"  - {player.playerName} (ID: {player.playerId}) ";
                    status += $"HP: {player.health}/{player.maxHealth} ";
                    status += $"存活: {(player.isAlive ? "是" : "否")} ";
                    status += $"准备: {(player.isReady ? "是" : "否")}\n";
                }
            }

            return status;
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 调试日志输出
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerStateManager] {message}");
            }
        }

        #endregion

        #region 销毁和清理

        /// <summary>
        /// 销毁玩家状态管理器
        /// </summary>
        public void Dispose()
        {
            // 清理事件
            OnPlayerAdded = null;
            OnPlayerRemoved = null;
            OnHostValidationFailed = null;
            OnHostValidationPassed = null;
            OnPlayersCleared = null;

            // 清理玩家状态
            ClearAllPlayers();

            // 清理引用
            networkManager = null;

            LogDebug("PlayerStateManager已销毁");
        }

        #endregion
    }
}