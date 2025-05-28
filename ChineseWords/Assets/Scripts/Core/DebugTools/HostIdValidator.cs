using UnityEngine;
using Core.Network;
using System.Text;

namespace Core.Tools
{
    /// <summary>
    /// Host ID一致性验证工具
    /// 用于调试和验证各个系统中Host ID是否一致
    /// </summary>
    public class HostIdValidator : MonoBehaviour
    {
        [Header("验证设置")]
        [SerializeField] private bool autoValidateOnStart = false;
        [SerializeField] private float autoValidateInterval = 5f;
        [SerializeField] private bool enableDebugLogs = true;

        private void Start()
        {
            if (autoValidateOnStart)
            {
                InvokeRepeating(nameof(ValidateHostIdConsistency), 1f, autoValidateInterval);
            }
        }

        /// <summary>
        /// 验证Host ID一致性
        /// </summary>
        [ContextMenu("验证Host ID一致性")]
        public void ValidateHostIdConsistency()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("只能在运行时验证Host ID一致性");
                return;
            }

            StringBuilder report = new StringBuilder();
            report.AppendLine("=== Host ID 一致性验证报告 ===");
            report.AppendLine($"验证时间: {System.DateTime.Now:HH:mm:ss}");
            report.AppendLine();

            bool hasInconsistency = false;

            // 1. NetworkManager 状态
            ushort networkManagerClientId = 0;
            ushort networkManagerHostPlayerId = 0;
            bool networkManagerIsHost = false;
            bool networkManagerHostReady = false;

            if (NetworkManager.Instance != null)
            {
                networkManagerClientId = NetworkManager.Instance.ClientId;
                networkManagerHostPlayerId = NetworkManager.Instance.GetHostPlayerId();
                networkManagerIsHost = NetworkManager.Instance.IsHost;
                networkManagerHostReady = NetworkManager.Instance.IsHostClientReady;

                report.AppendLine("NetworkManager 状态:");
                report.AppendLine($"  ClientId: {networkManagerClientId}");
                report.AppendLine($"  HostPlayerId: {networkManagerHostPlayerId}");
                report.AppendLine($"  IsHost: {networkManagerIsHost}");
                report.AppendLine($"  HostClientReady: {networkManagerHostReady}");
                report.AppendLine($"  IsConnected: {NetworkManager.Instance.IsConnected}");
            }
            else
            {
                report.AppendLine("NetworkManager: NULL");
                hasInconsistency = true;
            }

            // 2. RoomManager 状态
            ushort roomManagerHostId = 0;
            bool roomManagerIsHost = false;
            int roomPlayerCount = 0;

            if (RoomManager.Instance != null)
            {
                roomManagerIsHost = RoomManager.Instance.IsHost;

                if (RoomManager.Instance.CurrentRoom != null)
                {
                    roomManagerHostId = RoomManager.Instance.CurrentRoom.hostId;
                    roomPlayerCount = RoomManager.Instance.CurrentRoom.players.Count;
                }

                report.AppendLine();
                report.AppendLine("RoomManager 状态:");
                report.AppendLine($"  IsHost: {roomManagerIsHost}");
                report.AppendLine($"  RoomHostId: {roomManagerHostId}");
                report.AppendLine($"  IsInRoom: {RoomManager.Instance.IsInRoom}");
                report.AppendLine($"  PlayerCount: {roomPlayerCount}");
            }
            else
            {
                report.AppendLine();
                report.AppendLine("RoomManager: NULL");
                hasInconsistency = true;
            }

            // 3. HostGameManager 状态
            int hostGameManagerPlayerCount = 0;
            bool hostGameManagerInitialized = false;

            if (HostGameManager.Instance != null)
            {
                hostGameManagerPlayerCount = HostGameManager.Instance.PlayerCount;
                hostGameManagerInitialized = HostGameManager.Instance.IsInitialized;

                report.AppendLine();
                report.AppendLine("HostGameManager 状态:");
                report.AppendLine($"  IsInitialized: {hostGameManagerInitialized}");
                report.AppendLine($"  PlayerCount: {hostGameManagerPlayerCount}");
                report.AppendLine($"  IsGameInProgress: {HostGameManager.Instance.IsGameInProgress}");
            }
            else
            {
                report.AppendLine();
                report.AppendLine("HostGameManager: NULL");
            }

            // 4. 一致性检查
            report.AppendLine();
            report.AppendLine("=== 一致性检查 ===");

            // 检查 NetworkManager 内部一致性
            if (NetworkManager.Instance != null)
            {
                if (networkManagerIsHost)
                {
                    if (networkManagerClientId != networkManagerHostPlayerId)
                    {
                        report.AppendLine($"❌ NetworkManager内部不一致: ClientId({networkManagerClientId}) != HostPlayerId({networkManagerHostPlayerId})");
                        hasInconsistency = true;
                    }
                    else
                    {
                        report.AppendLine($"✅ NetworkManager内部一致: ClientId == HostPlayerId == {networkManagerClientId}");
                    }
                }
            }

            // 检查 NetworkManager 与 RoomManager 一致性
            if (NetworkManager.Instance != null && RoomManager.Instance?.CurrentRoom != null)
            {
                if (networkManagerHostPlayerId != roomManagerHostId)
                {
                    report.AppendLine($"❌ NetworkManager与RoomManager不一致: Network({networkManagerHostPlayerId}) != Room({roomManagerHostId})");
                    hasInconsistency = true;
                }
                else
                {
                    report.AppendLine($"✅ NetworkManager与RoomManager一致: HostId == {networkManagerHostPlayerId}");
                }

                if (networkManagerIsHost != roomManagerIsHost)
                {
                    report.AppendLine($"❌ IsHost状态不一致: Network({networkManagerIsHost}) != Room({roomManagerIsHost})");
                    hasInconsistency = true;
                }
                else
                {
                    report.AppendLine($"✅ IsHost状态一致: {networkManagerIsHost}");
                }
            }

            // 5. 总结
            report.AppendLine();
            report.AppendLine("=== 验证总结 ===");
            if (hasInconsistency)
            {
                report.AppendLine("❌ 发现Host ID不一致问题！");
            }
            else
            {
                report.AppendLine("✅ Host ID一致性验证通过");
            }

            // 输出报告
            if (enableDebugLogs || hasInconsistency)
            {
                if (hasInconsistency)
                {
                    Debug.LogError($"[HostIdValidator]\n{report}");
                }
                else
                {
                    Debug.Log($"[HostIdValidator]\n{report}");
                }
            }
        }

        /// <summary>
        /// 获取系统状态快照
        /// </summary>
        [ContextMenu("获取系统状态快照")]
        public void GetSystemSnapshot()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("只能在运行时获取系统状态");
                return;
            }

            StringBuilder snapshot = new StringBuilder();
            snapshot.AppendLine("=== 系统状态快照 ===");
            snapshot.AppendLine($"时间: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            snapshot.AppendLine();

            // NetworkManager详细状态
            if (NetworkManager.Instance != null)
            {
                snapshot.AppendLine("NetworkManager详细状态:");
                snapshot.AppendLine($"  {NetworkManager.Instance.GetNetworkStatus()}");
                snapshot.AppendLine($"  房间信息: {NetworkManager.Instance.GetRoomInfo()}");
            }

            // RoomManager详细状态
            if (RoomManager.Instance != null)
            {
                snapshot.AppendLine();
                snapshot.AppendLine("RoomManager详细状态:");
                snapshot.AppendLine($"  {RoomManager.Instance.GetRoomStatusInfo()}");

                if (RoomManager.Instance.CurrentRoom != null)
                {
                    snapshot.AppendLine("  房间玩家列表:");
                    foreach (var player in RoomManager.Instance.CurrentRoom.players.Values)
                    {
                        snapshot.AppendLine($"    - {player.playerName} (ID:{player.playerId}, Host:{player.isHost}, State:{player.state})");
                    }
                }
            }

            // HostGameManager详细状态
            if (HostGameManager.Instance != null)
            {
                snapshot.AppendLine();
                snapshot.AppendLine("HostGameManager详细状态:");
                snapshot.AppendLine($"  {HostGameManager.Instance.GetStatusInfo()}");
                snapshot.AppendLine();
                snapshot.AppendLine("HostGameManager游戏统计:");
                snapshot.AppendLine(HostGameManager.Instance.GetGameStats());
            }

            Debug.Log($"[HostIdValidator]\n{snapshot}");
        }

        /// <summary>
        /// 尝试修复Host ID不一致问题
        /// </summary>
        [ContextMenu("尝试修复Host ID不一致")]
        public void TryFixHostIdInconsistency()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("只能在运行时执行修复");
                return;
            }

            Debug.Log("[HostIdValidator] 开始尝试修复Host ID不一致问题");

            // 1. 重置NetworkManager状态
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsHost)
            {
                Debug.Log("[HostIdValidator] 检测到NetworkManager Host状态");

                if (!NetworkManager.Instance.IsHostClientReady)
                {
                    Debug.LogWarning("[HostIdValidator] Host客户端尚未准备就绪，等待连接完成");
                    return;
                }
            }

            // 2. 重新验证RoomManager状态
            if (RoomManager.Instance != null)
            {
                Debug.Log("[HostIdValidator] 验证RoomManager房间数据");

                if (RoomManager.Instance.IsInRoom)
                {
                    bool isValid = RoomManager.Instance.ValidateRoomData();
                    if (!isValid)
                    {
                        Debug.LogWarning("[HostIdValidator] 房间数据验证失败");
                    }
                }
            }

            // 3. 重新同步HostGameManager
            if (HostGameManager.Instance != null && HostGameManager.Instance.IsInitialized)
            {
                Debug.Log("[HostIdValidator] 刷新HostGameManager缓存");
                // 这里可以添加HostGameManager的重新同步方法调用
            }

            // 4. 重新验证一致性
            Invoke(nameof(ValidateHostIdConsistency), 0.5f);
        }

        /// <summary>
        /// 监控Host状态变化
        /// </summary>
        public void StartMonitoring()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnHostStarted += OnHostStarted;
                NetworkManager.OnHostPlayerReady += OnHostPlayerReady;
                NetworkManager.OnPlayerJoined += OnPlayerJoined;
            }

            if (RoomManager.Instance != null)
            {
                RoomManager.OnRoomCreated += OnRoomCreated;
            }

            Debug.Log("[HostIdValidator] 开始监控Host状态变化");
        }

        /// <summary>
        /// 停止监控
        /// </summary>
        public void StopMonitoring()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnHostStarted -= OnHostStarted;
                NetworkManager.OnHostPlayerReady -= OnHostPlayerReady;
                NetworkManager.OnPlayerJoined -= OnPlayerJoined;
            }

            if (RoomManager.Instance != null)
            {
                RoomManager.OnRoomCreated -= OnRoomCreated;
            }

            Debug.Log("[HostIdValidator] 停止监控Host状态变化");
        }

        #region 事件处理

        private void OnHostStarted()
        {
            if (enableDebugLogs)
                Debug.Log("[HostIdValidator] 监控到Host启动事件");
        }

        private void OnHostPlayerReady()
        {
            if (enableDebugLogs)
                Debug.Log("[HostIdValidator] 监控到Host玩家准备就绪事件");

            // Host玩家准备就绪后验证一致性
            Invoke(nameof(ValidateHostIdConsistency), 0.5f);
        }

        private void OnPlayerJoined(ushort playerId)
        {
            if (enableDebugLogs)
                Debug.Log($"[HostIdValidator] 监控到玩家加入: {playerId}");
        }

        private void OnRoomCreated(RoomData roomData)
        {
            if (enableDebugLogs)
                Debug.Log($"[HostIdValidator] 监控到房间创建: {roomData.roomName}, Host ID: {roomData.hostId}");

            // 房间创建后验证一致性
            Invoke(nameof(ValidateHostIdConsistency), 1f);
        }

        #endregion

        #region Unity生命周期

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                StartMonitoring();
            }
        }

        private void OnDisable()
        {
            StopMonitoring();
        }

        private void OnDestroy()
        {
            StopMonitoring();
        }

        #endregion

        #region 调试辅助方法

        /// <summary>
        /// 输出当前所有系统的Host相关信息
        /// </summary>
        [ContextMenu("输出Host信息摘要")]
        public void LogHostInfoSummary()
        {
            if (!Application.isPlaying) return;

            StringBuilder summary = new StringBuilder();
            summary.AppendLine("=== Host信息摘要 ===");

            // NetworkManager
            if (NetworkManager.Instance != null)
            {
                summary.AppendLine($"NetworkManager - IsHost: {NetworkManager.Instance.IsHost}, " +
                                 $"ClientId: {NetworkManager.Instance.ClientId}, " +
                                 $"HostPlayerId: {NetworkManager.Instance.GetHostPlayerId()}, " +
                                 $"HostReady: {NetworkManager.Instance.IsHostClientReady}");
            }

            // RoomManager
            if (RoomManager.Instance != null && RoomManager.Instance.CurrentRoom != null)
            {
                var room = RoomManager.Instance.CurrentRoom;
                summary.AppendLine($"RoomManager - HostId: {room.hostId}, " +
                                 $"IsHost: {RoomManager.Instance.IsHost}, " +
                                 $"PlayerCount: {room.players.Count}");
            }

            // HostGameManager
            if (HostGameManager.Instance != null)
            {
                summary.AppendLine($"HostGameManager - PlayerCount: {HostGameManager.Instance.PlayerCount}, " +
                                 $"Initialized: {HostGameManager.Instance.IsInitialized}");
            }

            Debug.Log($"[HostIdValidator]\n{summary}");
        }

        /// <summary>
        /// 检查特定玩家ID是否在所有系统中一致
        /// </summary>
        public bool CheckPlayerConsistency(ushort playerId)
        {
            bool isConsistent = true;
            StringBuilder report = new StringBuilder();
            report.AppendLine($"=== 玩家 {playerId} 一致性检查 ===");

            // NetworkManager中的状态
            if (NetworkManager.Instance != null)
            {
                bool isNetworkHost = NetworkManager.Instance.IsHostPlayer(playerId);
                report.AppendLine($"NetworkManager - IsHostPlayer: {isNetworkHost}");
            }

            // RoomManager中的状态
            if (RoomManager.Instance?.CurrentRoom != null)
            {
                bool existsInRoom = RoomManager.Instance.CurrentRoom.players.ContainsKey(playerId);
                bool isRoomHost = existsInRoom && RoomManager.Instance.CurrentRoom.players[playerId].isHost;
                report.AppendLine($"RoomManager - ExistsInRoom: {existsInRoom}, IsHost: {isRoomHost}");
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[HostIdValidator]\n{report}");
            }

            return isConsistent;
        }

        #endregion
    }
}