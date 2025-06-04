using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Core;

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
    /// 玩家状态管理器
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
        public PlayerStateManager()
        {
            playerStates = new Dictionary<ushort, PlayerGameState>();
            LogDebug("PlayerStateManager 实例已创建");
        }

        #region 初始化

        /// <summary>
        /// 初始化玩家状态管理器
        /// </summary>
        public void Initialize(NetworkManager networkMgr = null)
        {
            LogDebug("初始化PlayerStateManager...");

            networkManager = networkMgr ?? NetworkManager.Instance;

            if (networkManager == null)
            {
                Debug.LogWarning("[PlayerStateManager] NetworkManager引用为空，Host验证功能可能受限");
            }

            LogDebug("PlayerStateManager初始化完成");
        }

        #endregion

        #region 玩家管理

        /// <summary>
        /// 添加玩家
        /// </summary>
        public bool AddPlayer(ushort playerId, string playerName, int initialHealth = 100, int maxHealth = 100, bool isReady = true)
        {
            if (playerStates.ContainsKey(playerId))
            {
                LogDebug($"玩家 {playerId} 已存在，跳过重复添加");
                return false;
            }

            // 判断是否为Host玩家
            bool isHostPlayer = IsHostPlayer(playerId);

            // 创建玩家状态
            var playerState = new PlayerGameState
            {
                playerId = playerId,
                playerName = isHostPlayer ? "房主" : playerName,
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

        public bool AddPlayerFromRoom(ushort playerId, string playerName, int initialHealth = 100, int maxHealth = 100)
        {
            return AddPlayer(playerId, playerName, initialHealth, maxHealth, true); // 从房间来的都是准备好的
        }

        /// <summary>
        /// 移除玩家
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <returns>是否成功移除</returns>
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
        /// <param name="playerId">玩家ID</param>
        /// <param name="newHealth">新血量</param>
        /// <param name="maxHealth">最大血量（可选）</param>
        /// <returns>是否成功更新</returns>
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
        /// <param name="playerId">玩家ID</param>
        /// <param name="isAlive">是否存活</param>
        /// <returns>是否成功设置</returns>
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
        /// <param name="playerId">玩家ID</param>
        /// <param name="isReady">是否准备就绪</param>
        /// <returns>是否成功设置</returns>
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

        #region Host验证

        /// <summary>
        /// 验证Host数量并修复重复问题
        /// </summary>
        /// <returns>验证是否通过</returns>
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

                // 验证与NetworkManager的一致性
                if (networkManager != null)
                {
                    ushort networkHostId = networkManager.GetHostPlayerId();
                    if (hostPlayer.playerId != networkHostId)
                    {
                        Debug.LogError($"[PlayerStateManager] Host ID不一致！游戏中: {hostPlayer.playerId}, NetworkManager: {networkHostId}");
                        return false;
                    }
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
                return FixDuplicateHosts();
            }
        }

        /// <summary>
        /// 修复重复Host问题
        /// </summary>
        /// <returns>是否修复成功</returns>
        public bool FixDuplicateHosts()
        {
            if (networkManager == null)
            {
                Debug.LogError("[PlayerStateManager] NetworkManager为空，无法修复重复Host");
                return false;
            }

            ushort correctHostId = networkManager.GetHostPlayerId();
            LogDebug($"修复重复房主，正确的Host ID: {correctHostId}");

            var hostPlayers = GetHostPlayers();
            if (hostPlayers.Count <= 1)
            {
                LogDebug("房主数量正常，无需修复");
                return true;
            }

            LogDebug($"发现 {hostPlayers.Count} 个房主，开始修复");

            // 保留正确ID的房主，移除其他的
            var correctHost = hostPlayers.FirstOrDefault(h => h.playerId == correctHostId);

            if (correctHost != null)
            {
                LogDebug($"保留正确房主: ID={correctHost.playerId}, Name={correctHost.playerName}");

                // 移除其他房主，但保留玩家数据（只修改名称）
                var duplicateHosts = hostPlayers.Where(h => h.playerId != correctHostId).ToList();
                foreach (var duplicateHost in duplicateHosts)
                {
                    // 修改名称为普通玩家
                    duplicateHost.playerName = $"玩家{duplicateHost.playerId}";
                    LogDebug($"修正重复房主为普通玩家: ID={duplicateHost.playerId}, 新名称={duplicateHost.playerName}");
                }
            }
            else
            {
                // 如果没有找到正确ID的房主，保留最小ID的房主
                var primaryHost = hostPlayers.OrderBy(h => h.playerId).First();
                var duplicateHosts = hostPlayers.Skip(1).ToList();

                LogDebug($"保留主房主: ID={primaryHost.playerId}, 修正其他重复房主");

                foreach (var duplicateHost in duplicateHosts)
                {
                    duplicateHost.playerName = $"玩家{duplicateHost.playerId}";
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
        /// <returns>验证是否通过</returns>
        public bool ValidateHostCount()
        {
            var hostPlayers = GetHostPlayers();
            return hostPlayers.Count == 1;
        }

        /// <summary>
        /// 获取所有Host玩家
        /// </summary>
        /// <returns>Host玩家列表</returns>
        private List<PlayerGameState> GetHostPlayers()
        {
            return playerStates.Values.Where(p => p.playerName.Contains("房主")).ToList();
        }

        /// <summary>
        /// 判断玩家是否为Host
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <returns>是否为Host</returns>
        private bool IsHostPlayer(ushort playerId)
        {
            return networkManager?.IsHostPlayer(playerId) ?? false;
        }

        #endregion

        #region 查询方法

        /// <summary>
        /// 获取玩家状态
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <returns>玩家状态，不存在则返回null</returns>
        public PlayerGameState GetPlayerState(ushort playerId)
        {
            return playerStates.ContainsKey(playerId) ? playerStates[playerId] : null;
        }

        /// <summary>
        /// 检查玩家是否存在
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <returns>是否存在</returns>
        public bool ContainsPlayer(ushort playerId)
        {
            return playerStates.ContainsKey(playerId);
        }

        /// <summary>
        /// 检查玩家是否存活
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <returns>是否存活</returns>
        public bool IsPlayerAlive(ushort playerId)
        {
            return playerStates.ContainsKey(playerId) && playerStates[playerId].isAlive;
        }

        /// <summary>
        /// 获取所有玩家状态
        /// </summary>
        /// <returns>玩家状态字典的副本</returns>
        public Dictionary<ushort, PlayerGameState> GetAllPlayerStates()
        {
            return new Dictionary<ushort, PlayerGameState>(playerStates);
        }

        /// <summary>
        /// 获取存活玩家列表
        /// </summary>
        /// <returns>存活玩家ID列表</returns>
        public List<ushort> GetAlivePlayerIds()
        {
            return playerStates.Where(p => p.Value.isAlive).Select(p => p.Key).ToList();
        }

        /// <summary>
        /// 获取存活玩家状态列表
        /// </summary>
        /// <returns>存活玩家状态列表</returns>
        public List<PlayerGameState> GetAlivePlayers()
        {
            return playerStates.Values.Where(p => p.isAlive).ToList();
        }

        /// <summary>
        /// 获取玩家总数
        /// </summary>
        /// <returns>玩家总数</returns>
        public int GetPlayerCount()
        {
            return playerStates.Count;
        }

        /// <summary>
        /// 获取存活玩家数量
        /// </summary>
        /// <returns>存活玩家数量</returns>
        public int GetAlivePlayerCount()
        {
            return playerStates.Values.Count(p => p.isAlive);
        }

        /// <summary>
        /// 获取准备就绪的玩家数量
        /// </summary>
        /// <returns>准备就绪的玩家数量</returns>
        public int GetReadyPlayerCount()
        {
            return playerStates.Values.Count(p => p.isReady);
        }

        /// <summary>
        /// 检查是否所有玩家都已准备
        /// </summary>
        /// <returns>是否所有玩家都已准备</returns>
        public bool AreAllPlayersReady()
        {
            return playerStates.Count > 0 && playerStates.Values.All(p => p.isReady);
        }

        /// <summary>
        /// 获取Host玩家ID
        /// </summary>
        /// <returns>Host玩家ID，未找到返回0</returns>
        public ushort GetHostPlayerId()
        {
            var hostPlayer = playerStates.Values.FirstOrDefault(p => p.playerName.Contains("房主"));
            return hostPlayer?.playerId ?? 0;
        }

        #endregion

        #region 状态信息

        /// <summary>
        /// 获取玩家状态管理器状态信息
        /// </summary>
        /// <returns>状态信息字符串</returns>
        public string GetStatusInfo()
        {
            var status = "=== PlayerStateManager状态 ===\n";
            status += $"玩家总数: {GetPlayerCount()}\n";
            status += $"存活玩家数: {GetAlivePlayerCount()}\n";
            status += $"准备玩家数: {GetReadyPlayerCount()}\n";
            status += $"所有玩家已准备: {(AreAllPlayersReady() ? "是" : "否")}\n";

            var hostId = GetHostPlayerId();
            status += $"Host玩家ID: {(hostId != 0 ? hostId.ToString() : "未找到")}\n";

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

        #region 房间数据同步
        public int SyncFromRoomSystem(int initialHealth = 100)
        {
            LogDebug("从房间系统同步玩家数据");

            // 清空现有玩家状态，避免重复
            ClearAllPlayers();

            int syncedCount = 0;

            // 方法1：从RoomManager获取
            if (RoomManager.Instance?.CurrentRoom != null)
            {
                var room = RoomManager.Instance.CurrentRoom;
                LogDebug($"从RoomManager同步，房间玩家数: {room.players.Count}");

                foreach (var roomPlayer in room.players.Values)
                {
                    if (AddPlayerFromRoom(roomPlayer.playerId, roomPlayer.playerName, initialHealth, initialHealth))
                    {
                        syncedCount++;
                    }
                }

                LogDebug($"房间玩家同步完成，总计玩家数: {syncedCount}");
                return syncedCount;
            }

            // 方法2：从NetworkManager的连接状态获取
            if (networkManager != null)
            {
                LogDebug($"从NetworkManager同步，连接玩家数: {networkManager.ConnectedPlayerCount}");

                ushort hostPlayerId = networkManager.GetHostPlayerId();
                if (hostPlayerId != 0 && networkManager.IsHostClientReady)
                {
                    if (AddPlayerFromRoom(hostPlayerId, "房主", initialHealth, initialHealth))
                    {
                        syncedCount++;
                        LogDebug($"添加Host玩家: ID={hostPlayerId}");
                    }
                }
                else
                {
                    LogDebug($"Host玩家尚未准备就绪: ID={hostPlayerId}, Ready={networkManager.IsHostClientReady}");
                }
            }

            LogDebug($"玩家同步完成，总计玩家数: {syncedCount}");
            return syncedCount;
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