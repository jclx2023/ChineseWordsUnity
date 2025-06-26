using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Classroom.Scene;
using Core.Network;

namespace Classroom.Player
{
    /// <summary>
    /// 座位-玩家绑定器 - 管理玩家与座位的映射关系
    /// 提供座位查询、玩家定位等功能，与CircularSeatingSystem协同工作
    /// </summary>
    public class SeatingPlayerBinder : MonoBehaviour
    {
        [Header("组件引用")]
        [SerializeField] private CircularSeatingSystem seatingSystem;
        [SerializeField] private NetworkPlayerSpawner playerSpawner;

        [Header("绑定配置")]
        [SerializeField] private bool autoBindOnStart = true;
        [SerializeField] private bool maintainBindingOnPlayerLeave = true; // 玩家离开时保持座位绑定

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool showBindingGizmos = true;

        // 绑定数据
        private Dictionary<ushort, SeatBinding> playerBindings = new Dictionary<ushort, SeatBinding>();
        private Dictionary<int, SeatBinding> seatBindings = new Dictionary<int, SeatBinding>();
        private bool isInitialized = false;

        // 座位绑定数据结构
        [System.Serializable]
        public class SeatBinding
        {
            public ushort playerId;
            public int seatIndex;
            public SeatIdentifier seatIdentifier;
            public GameObject playerCharacter;
            public string playerName;
            public bool isOccupied;
            public bool isActive; // 玩家是否在线

            public SeatBinding(ushort id, int seat, SeatIdentifier identifier)
            {
                playerId = id;
                seatIndex = seat;
                seatIdentifier = identifier;
                isOccupied = true;
                isActive = true;
            }
        }

        // 公共属性
        public bool IsInitialized => isInitialized;
        public int TotalSeats => seatBindings.Count;
        public int OccupiedSeats => seatBindings.Values.Count(b => b.isOccupied);
        public int ActivePlayers => playerBindings.Values.Count(b => b.isActive);

        // 事件
        public static event System.Action<SeatBinding> OnPlayerBoundToSeat;
        public static event System.Action<ushort, int> OnPlayerUnboundFromSeat;
        public static event System.Action<SeatBinding> OnPlayerActivated;
        public static event System.Action<SeatBinding> OnPlayerDeactivated;

        #region Unity生命周期

        private void Awake()
        {
            // 自动查找组件
            if (seatingSystem == null)
                seatingSystem = FindObjectOfType<CircularSeatingSystem>();

            if (playerSpawner == null)
                playerSpawner = FindObjectOfType<NetworkPlayerSpawner>();

            if (seatingSystem == null)
            {
                Debug.LogError("[SeatingPlayerBinder] 未找到CircularSeatingSystem组件");
                return;
            }
        }

        private void Start()
        {
            if (autoBindOnStart && seatingSystem != null)
            {
                Initialize();
            }
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region 事件订阅

        private void SubscribeToEvents()
        {
            if (playerSpawner != null)
            {
                NetworkPlayerSpawner.OnCharacterSpawned += OnCharacterSpawned;
                NetworkPlayerSpawner.OnSeatsGenerated += OnSeatsGenerated;
            }

            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerLeft += OnPlayerLeft;
                NetworkManager.OnPlayerJoined += OnPlayerJoined;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (playerSpawner != null)
            {
                NetworkPlayerSpawner.OnCharacterSpawned -= OnCharacterSpawned;
                NetworkPlayerSpawner.OnSeatsGenerated -= OnSeatsGenerated;
            }

            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerLeft -= OnPlayerLeft;
                NetworkManager.OnPlayerJoined -= OnPlayerJoined;
            }
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化座位绑定器
        /// </summary>
        public void Initialize()
        {
            if (isInitialized)
            {
                LogDebug("已初始化，跳过重复初始化");
                return;
            }

            LogDebug("开始初始化座位绑定器");

            // 清理现有数据
            ClearAllBindings();

            // 等待座位系统准备就绪
            if (!seatingSystem.IsInitialized)
            {
                LogDebug("等待座位系统初始化...");
                StartCoroutine(WaitForSeatingSystemReady());
                return;
            }

            // 建立初始绑定
            BuildInitialBindings();

            isInitialized = true;
            LogDebug("座位绑定器初始化完成");
        }

        /// <summary>
        /// 等待座位系统准备就绪
        /// </summary>
        private System.Collections.IEnumerator WaitForSeatingSystemReady()
        {
            yield return new WaitUntil(() => seatingSystem.IsInitialized);
            BuildInitialBindings();
            isInitialized = true;
            LogDebug("座位绑定器延迟初始化完成");
        }

        /// <summary>
        /// 建立初始绑定关系
        /// </summary>
        private void BuildInitialBindings()
        {
            var generatedSeats = seatingSystem.GeneratedSeats;
            LogDebug($"建立初始绑定关系，座位数量: {generatedSeats.Count}");

            for (int i = 0; i < generatedSeats.Count; i++)
            {
                var seatData = generatedSeats[i];
                if (seatData.seatInstance != null)
                {
                    var seatIdentifier = seatData.seatInstance.GetComponent<SeatIdentifier>();
                    if (seatIdentifier != null)
                    {
                        // 创建空座位绑定
                        CreateEmptySeatBinding(i, seatIdentifier);
                    }
                }
            }
        }

        /// <summary>
        /// 创建空座位绑定
        /// </summary>
        private void CreateEmptySeatBinding(int seatIndex, SeatIdentifier seatIdentifier)
        {
            var binding = new SeatBinding(0, seatIndex, seatIdentifier)
            {
                isOccupied = false,
                isActive = false,
                playerName = ""
            };

            seatBindings[seatIndex] = binding;
            LogDebug($"创建空座位绑定: 座位 {seatIndex}");
        }

        #endregion

        #region 绑定管理

        /// <summary>
        /// 绑定玩家到座位
        /// </summary>
        public bool BindPlayerToSeat(ushort playerId, int seatIndex, string playerName = "")
        {
            if (!IsValidSeatIndex(seatIndex))
            {
                LogDebug($"无效的座位索引: {seatIndex}");
                return false;
            }

            if (IsPlayerBound(playerId))
            {
                LogDebug($"玩家 {playerId} 已绑定座位，先解绑");
                UnbindPlayer(playerId);
            }

            var seatBinding = seatBindings[seatIndex];
            var seatIdentifier = seatBinding.seatIdentifier;

            // 更新绑定信息
            seatBinding.playerId = playerId;
            seatBinding.isOccupied = true;
            seatBinding.isActive = true;
            seatBinding.playerName = !string.IsNullOrEmpty(playerName) ? playerName : $"Player_{playerId}";

            // 占用座位
            seatIdentifier.OccupySeat(seatBinding.playerName);

            // 建立双向映射
            playerBindings[playerId] = seatBinding;

            OnPlayerBoundToSeat?.Invoke(seatBinding);
            LogDebug($"玩家 {playerId} ({seatBinding.playerName}) 绑定到座位 {seatIndex}");

            return true;
        }

        /// <summary>
        /// 解绑玩家
        /// </summary>
        public bool UnbindPlayer(ushort playerId)
        {
            if (!IsPlayerBound(playerId))
            {
                LogDebug($"玩家 {playerId} 未绑定任何座位");
                return false;
            }

            var binding = playerBindings[playerId];
            int seatIndex = binding.seatIndex;

            // 移除玩家绑定
            playerBindings.Remove(playerId);

            if (maintainBindingOnPlayerLeave)
            {
                // 保持座位，但标记为非活跃
                binding.isActive = false;
                binding.playerCharacter = null;
                LogDebug($"玩家 {playerId} 从座位 {seatIndex} 解绑，座位保留");
            }
            else
            {
                // 完全释放座位
                binding.playerId = 0;
                binding.isOccupied = false;
                binding.isActive = false;
                binding.playerName = "";
                binding.playerCharacter = null;
                binding.seatIdentifier.ReleaseSeat();
                LogDebug($"玩家 {playerId} 从座位 {seatIndex} 解绑，座位释放");
            }

            OnPlayerUnboundFromSeat?.Invoke(playerId, seatIndex);
            return true;
        }

        /// <summary>
        /// 重新激活玩家（用于断线重连）
        /// </summary>
        public bool ReactivatePlayer(ushort playerId, GameObject playerCharacter = null)
        {
            var binding = GetPlayerBinding(playerId);
            if (binding == null)
            {
                LogDebug($"玩家 {playerId} 没有保留的座位绑定");
                return false;
            }

            binding.isActive = true;
            binding.playerCharacter = playerCharacter;

            // 重新占用座位
            binding.seatIdentifier.OccupySeat(binding.playerName);

            OnPlayerActivated?.Invoke(binding);
            LogDebug($"玩家 {playerId} 重新激活，座位 {binding.seatIndex}");

            return true;
        }

        /// <summary>
        /// 停用玩家（但保留座位）
        /// </summary>
        public bool DeactivatePlayer(ushort playerId)
        {
            if (!IsPlayerBound(playerId))
            {
                return false;
            }

            var binding = playerBindings[playerId];
            binding.isActive = false;
            binding.playerCharacter = null;

            OnPlayerDeactivated?.Invoke(binding);
            LogDebug($"玩家 {playerId} 已停用，座位 {binding.seatIndex} 保留");

            return true;
        }

        #endregion

        #region 查询接口

        /// <summary>
        /// 获取玩家的座位绑定
        /// </summary>
        public SeatBinding GetPlayerBinding(ushort playerId)
        {
            return playerBindings.TryGetValue(playerId, out SeatBinding binding) ? binding : null;
        }

        /// <summary>
        /// 获取座位的绑定信息
        /// </summary>
        public SeatBinding GetSeatBinding(int seatIndex)
        {
            return seatBindings.TryGetValue(seatIndex, out SeatBinding binding) ? binding : null;
        }

        /// <summary>
        /// 获取玩家的座位索引
        /// </summary>
        public int GetPlayerSeatIndex(ushort playerId)
        {
            var binding = GetPlayerBinding(playerId);
            return binding?.seatIndex ?? -1;
        }

        /// <summary>
        /// 获取座位的玩家ID
        /// </summary>
        public ushort GetSeatPlayerId(int seatIndex)
        {
            var binding = GetSeatBinding(seatIndex);
            return binding?.playerId ?? 0;
        }

        /// <summary>
        /// 获取玩家的座位标识符
        /// </summary>
        public SeatIdentifier GetPlayerSeatIdentifier(ushort playerId)
        {
            var binding = GetPlayerBinding(playerId);
            return binding?.seatIdentifier;
        }

        /// <summary>
        /// 获取玩家的角色对象
        /// </summary>
        public GameObject GetPlayerCharacter(ushort playerId)
        {
            var binding = GetPlayerBinding(playerId);
            return binding?.playerCharacter;
        }

        /// <summary>
        /// 检查玩家是否已绑定座位
        /// </summary>
        public bool IsPlayerBound(ushort playerId)
        {
            return playerBindings.ContainsKey(playerId);
        }

        /// <summary>
        /// 检查玩家是否活跃
        /// </summary>
        public bool IsPlayerActive(ushort playerId)
        {
            var binding = GetPlayerBinding(playerId);
            return binding?.isActive ?? false;
        }

        /// <summary>
        /// 检查座位是否被占用
        /// </summary>
        public bool IsSeatOccupied(int seatIndex)
        {
            var binding = GetSeatBinding(seatIndex);
            return binding?.isOccupied ?? false;
        }

        /// <summary>
        /// 获取所有活跃玩家的绑定
        /// </summary>
        public List<SeatBinding> GetActivePlayerBindings()
        {
            return playerBindings.Values.Where(b => b.isActive).ToList();
        }

        /// <summary>
        /// 获取所有空闲座位的索引
        /// </summary>
        public List<int> GetAvailableSeatIndices()
        {
            return seatBindings.Values.Where(b => !b.isOccupied).Select(b => b.seatIndex).ToList();
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 座位生成完成事件
        /// </summary>
        private void OnSeatsGenerated(int seatCount)
        {
            LogDebug($"收到座位生成事件，座位数: {seatCount}");

            if (!isInitialized)
            {
                Initialize();
            }
            else
            {
                // 重新建立绑定
                BuildInitialBindings();
            }
        }

        /// <summary>
        /// 角色生成完成事件
        /// </summary>
        private void OnCharacterSpawned(ushort playerId, GameObject character)
        {
            LogDebug($"收到角色生成事件: 玩家 {playerId}");

            // 更新绑定中的角色引用
            var binding = GetPlayerBinding(playerId);
            if (binding != null)
            {
                binding.playerCharacter = character;
                LogDebug($"更新玩家 {playerId} 的角色引用");
            }
            else
            {
                LogDebug($"警告：玩家 {playerId} 没有对应的座位绑定");
            }
        }

        /// <summary>
        /// 玩家离开事件
        /// </summary>
        private void OnPlayerLeft(ushort playerId)
        {
            LogDebug($"玩家 {playerId} 离开房间");

            if (maintainBindingOnPlayerLeave)
            {
                DeactivatePlayer(playerId);
            }
            else
            {
                UnbindPlayer(playerId);
            }
        }

        /// <summary>
        /// 玩家加入事件
        /// </summary>
        private void OnPlayerJoined(ushort playerId)
        {
            LogDebug($"玩家 {playerId} 加入房间");

            // 检查是否有保留的座位绑定
            var existingBinding = GetPlayerBinding(playerId);
            if (existingBinding != null && !existingBinding.isActive)
            {
                LogDebug($"玩家 {playerId} 有保留座位 {existingBinding.seatIndex}，准备重新激活");
                // 等待角色生成后再激活
            }
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 清理所有绑定
        /// </summary>
        public void ClearAllBindings()
        {
            LogDebug("清理所有座位绑定");

            // 释放所有座位
            foreach (var binding in seatBindings.Values)
            {
                binding.seatIdentifier?.ReleaseSeat();
            }

            playerBindings.Clear();
            seatBindings.Clear();
        }

        /// <summary>
        /// 强制重建绑定关系
        /// </summary>
        [ContextMenu("重建绑定关系")]
        public void RebuildBindings()
        {
            LogDebug("强制重建绑定关系");

            ClearAllBindings();
            BuildInitialBindings();
        }

        /// <summary>
        /// 获取绑定状态信息
        /// </summary>
        public string GetBindingStatus()
        {
            var activePlayers = GetActivePlayerBindings();
            var availableSeats = GetAvailableSeatIndices();

            string status = $"=== 座位绑定状态 ===\n";
            status += $"总座位数: {TotalSeats}\n";
            status += $"已占用: {OccupiedSeats}\n";
            status += $"活跃玩家: {ActivePlayers}\n";
            status += $"可用座位: {availableSeats.Count}\n\n";

            status += "活跃玩家列表:\n";
            foreach (var binding in activePlayers)
            {
                status += $"  座位 {binding.seatIndex}: 玩家 {binding.playerId} ({binding.playerName})\n";
            }

            return status;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 检查座位索引是否有效
        /// </summary>
        private bool IsValidSeatIndex(int seatIndex)
        {
            return seatIndex >= 0 && seatBindings.ContainsKey(seatIndex);
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[SeatingPlayerBinder] {message}");
            }
        }

        #endregion

        #region 调试方法

        [ContextMenu("显示所有绑定详情")]
        public void ShowAllBindingDetails()
        {
            string details = "=== 详细绑定信息 ===\n";

            foreach (var kvp in seatBindings)
            {
                var binding = kvp.Value;
                details += $"座位 {binding.seatIndex}: ";

                if (binding.isOccupied)
                {
                    details += $"玩家 {binding.playerId} ({binding.playerName}) ";
                    details += $"- {(binding.isActive ? "活跃" : "离线")}";
                }
                else
                {
                    details += "空闲";
                }

                details += "\n";
            }

            Debug.Log(details);
        }

        #endregion

    }
}