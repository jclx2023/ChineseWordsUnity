using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Classroom.Scene;
using Core.Network;

namespace Classroom.Player
{
    /// <summary>
    /// 网络玩家生成器 - 负责根据房间玩家数动态生成座位和角色
    /// 主机端：生成座位+分配玩家+生成角色，然后同步给所有客户端
    /// 客户端：接收同步数据，在本地重建座位和角色布局
    /// 使用统一的网络管理器进行RPC通信
    /// </summary>
    public class NetworkPlayerSpawner : MonoBehaviour
    {
        [Header("核心配置")]
        [SerializeField] private GameObject playerCharacterPrefab; // 玩家角色预制体（带Humanoid骨骼）
        [SerializeField] private CircularSeatingSystem seatingSystem; // 座位系统引用
        [SerializeField] private float spawnDelay = 1f; // 生成延迟（等待场景稳定）

        [Header("角色配置")]
        [SerializeField] private float characterHeight = 1.8f; // 角色高度
        [SerializeField] private Vector3 characterScale = Vector3.one; // 角色缩放

        [Header("同步配置")]
        [SerializeField] private float sceneLoadTimeout = 10f; // 场景加载超时时间
        [SerializeField] private bool waitForAllPlayersReady = true; // 是否等待所有玩家准备就绪

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 玩家-座位绑定数据
        private Dictionary<ushort, int> playerToSeatMap = new Dictionary<ushort, int>(); // PlayerID -> SeatIndex
        private Dictionary<int, ushort> seatToPlayerMap = new Dictionary<int, ushort>(); // SeatIndex -> PlayerID
        private Dictionary<ushort, GameObject> playerCharacters = new Dictionary<ushort, GameObject>(); // PlayerID -> Character GameObject

        // 生成状态
        private bool isInitialized = false;
        private bool hasGeneratedSeats = false;
        private bool hasSpawnedCharacters = false;
        private List<ushort> readyPlayerIds = new List<ushort>(); // 已准备好的玩家ID列表

        // 公共属性
        public bool IsInitialized => isInitialized;
        public bool HasGeneratedSeats => hasGeneratedSeats;
        public bool HasSpawnedCharacters => hasSpawnedCharacters;
        public int SpawnedCharacterCount => playerCharacters.Count;

        // 事件
        public static event System.Action<int> OnSeatsGenerated; // 座位生成完成
        public static event System.Action<ushort, GameObject> OnCharacterSpawned; // 角色生成完成
        public static event System.Action OnAllCharactersSpawned; // 所有角色生成完成

        #region Unity生命周期

        private void Awake()
        {
            // 自动查找座位系统
            if (seatingSystem == null)
            {
                seatingSystem = FindObjectOfType<CircularSeatingSystem>();
            }

            if (seatingSystem == null)
            {
                Debug.LogError("[NetworkPlayerSpawner] 未找到CircularSeatingSystem组件");
                return;
            }

            isInitialized = true;
            LogDebug("NetworkPlayerSpawner已初始化");
        }

        private void Start()
        {
            if (!isInitialized) return;

            // 订阅网络事件
            SubscribeToNetworkEvents();

            // 如果是主机且在房间中，开始生成流程
            if (PhotonNetwork.IsMasterClient && PhotonNetwork.InRoom)
            {
                StartCoroutine(DelayedSpawnInitialization());
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromNetworkEvents();
        }

        #endregion

        #region 网络事件订阅

        private void SubscribeToNetworkEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerJoined += OnPlayerJoined;
                NetworkManager.OnPlayerLeft += OnPlayerLeft;
            }
        }

        private void UnsubscribeFromNetworkEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerJoined -= OnPlayerJoined;
                NetworkManager.OnPlayerLeft -= OnPlayerLeft;
            }
        }

        #endregion

        #region 主机端：生成座位和角色

        /// <summary>
        /// 延迟初始化生成流程
        /// </summary>
        private IEnumerator DelayedSpawnInitialization()
        {
            LogDebug("开始延迟生成初始化");

            // 等待场景稳定
            yield return new WaitForSeconds(spawnDelay);

            // 等待所有玩家准备就绪（如果启用）
            if (waitForAllPlayersReady)
            {
                yield return StartCoroutine(WaitForAllPlayersReady());
            }

            // 执行完整的生成流程
            GenerateSeatsAndSpawnCharacters();
        }

        /// <summary>
        /// 等待所有玩家准备就绪
        /// </summary>
        private IEnumerator WaitForAllPlayersReady()
        {
            LogDebug("等待所有玩家准备就绪...");

            float timeout = sceneLoadTimeout;
            while (timeout > 0)
            {
                // 检查是否所有玩家都已发送"准备就绪"信号
                if (AreAllPlayersReady())
                {
                    LogDebug("所有玩家已准备就绪");
                    break;
                }

                timeout -= Time.deltaTime;
                yield return null;
            }

            if (timeout <= 0)
            {
                LogDebug("等待玩家准备超时，强制开始生成");
            }
        }

        /// <summary>
        /// 检查所有玩家是否准备就绪
        /// </summary>
        private bool AreAllPlayersReady()
        {
            // 暂时简化：认为所有在房间中的玩家都已准备就绪
            // 后续可以添加更复杂的准备就绪检查逻辑
            return PhotonNetwork.PlayerList.Length > 0;
        }

        /// <summary>
        /// 生成座位并分配玩家角色
        /// </summary>
        public void GenerateSeatsAndSpawnCharacters()
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                LogDebug("非主机端，跳过生成逻辑");
                return;
            }

            LogDebug("开始生成座位和角色...");

            // 获取当前房间所有玩家
            var allPlayers = PhotonNetwork.PlayerList.OrderBy(p => p.ActorNumber).ToList();
            int playerCount = allPlayers.Count;

            LogDebug($"当前房间玩家数: {playerCount}");

            // 清理现有数据
            ClearAllData();

            // 1. 生成座位
            seatingSystem.GenerateSeats(playerCount);
            hasGeneratedSeats = true;

            // 2. 分配玩家到座位
            AssignPlayersToSeats(allPlayers);

            // 3. 生成角色
            SpawnAllCharacters();

            // 4. 同步给所有客户端
            SyncToAllClients();

            OnSeatsGenerated?.Invoke(playerCount);
            LogDebug("座位和角色生成完成");
        }

        /// <summary>
        /// 分配玩家到座位
        /// </summary>
        private void AssignPlayersToSeats(List<Photon.Realtime.Player> players)
        {
            LogDebug("开始分配玩家到座位");

            for (int i = 0; i < players.Count; i++)
            {
                ushort playerId = (ushort)players[i].ActorNumber;
                int seatIndex = i;

                // 建立双向映射
                playerToSeatMap[playerId] = seatIndex;
                seatToPlayerMap[seatIndex] = playerId;

                // 占用座位
                var seatData = seatingSystem.GetSeatData(seatIndex);
                if (seatData?.seatInstance != null)
                {
                    var seatIdentifier = seatData.seatInstance.GetComponent<SeatIdentifier>();
                    if (seatIdentifier != null)
                    {
                        seatIdentifier.OccupySeat(players[i].NickName ?? $"Player_{playerId}");
                    }
                }

                LogDebug($"玩家 {players[i].NickName} (ID: {playerId}) 分配到座位 {seatIndex}");
            }

            LogDebug($"玩家分配完成，共分配 {players.Count} 名玩家");
        }

        /// <summary>
        /// 生成所有角色
        /// </summary>
        private void SpawnAllCharacters()
        {
            if (playerCharacterPrefab == null)
            {
                Debug.LogError("[NetworkPlayerSpawner] 角色预制体未设置");
                return;
            }

            LogDebug("开始生成所有角色");

            foreach (var kvp in playerToSeatMap)
            {
                ushort playerId = kvp.Key;
                int seatIndex = kvp.Value;

                SpawnCharacterForPlayer(playerId, seatIndex);
            }

            hasSpawnedCharacters = true;
            OnAllCharactersSpawned?.Invoke();
            LogDebug("所有角色生成完成");
        }

        /// <summary>
        /// 为指定玩家生成角色
        /// </summary>
        private void SpawnCharacterForPlayer(ushort playerId, int seatIndex)
        {
            var seatData = seatingSystem.GetSeatData(seatIndex);
            if (seatData?.seatInstance == null)
            {
                Debug.LogError($"[NetworkPlayerSpawner] 座位 {seatIndex} 数据无效");
                return;
            }

            var seatIdentifier = seatData.seatInstance.GetComponent<SeatIdentifier>();
            if (seatIdentifier == null)
            {
                Debug.LogError($"[NetworkPlayerSpawner] 座位 {seatIndex} 缺少SeatIdentifier组件");
                return;
            }

            // 获取生成位置
            Vector3 spawnPosition = seatIdentifier.GetPlayerSpawnPosition();
            Quaternion spawnRotation = seatIdentifier.GetSeatRotation();

            // 生成角色
            GameObject character = Instantiate(playerCharacterPrefab, spawnPosition, spawnRotation);
            character.name = $"Player_{playerId:D2}_Character";
            character.transform.localScale = characterScale;

            // 设置角色属性
            SetupCharacterForPlayer(character, playerId);

            // 记录角色
            playerCharacters[playerId] = character;

            OnCharacterSpawned?.Invoke(playerId, character);
            LogDebug($"为玩家 {playerId} 在座位 {seatIndex} 生成角色");
        }

        /// <summary>
        /// 设置角色属性
        /// </summary>
        private void SetupCharacterForPlayer(GameObject character, ushort playerId)
        {
            // 添加PlayerCameraController（仅本地玩家）
            if (playerId == NetworkManager.Instance.ClientId)
            {
                var cameraController = character.GetComponent<PlayerCameraController>();
                if (cameraController == null)
                {
                    cameraController = character.AddComponent<PlayerCameraController>();
                }

                // 获取对应的座位信息设置摄像机
                int seatIndex = playerToSeatMap[playerId];
                var seatData = seatingSystem.GetSeatData(seatIndex);
                var seatIdentifier = seatData.seatInstance.GetComponent<SeatIdentifier>();

                // 传递座位朝向给摄像机控制器
                // 注意：这里不直接设置CameraMount，让PlayerCameraController自己查找角色的头部挂载点
                cameraController.SetCameraMount(null, seatIdentifier.GetSeatRotation());
                LogDebug($"为本地玩家 {playerId} 设置摄像机控制器");
            }

            // 添加其他角色组件（如动画、网络同步等）
            var networkSync = character.GetComponent<PlayerNetworkSync>();
            if (networkSync == null)
            {
                networkSync = character.AddComponent<PlayerNetworkSync>();
            }
            networkSync.Initialize(playerId);
        }

        #endregion

        #region 网络同步

        /// <summary>
        /// 同步给所有客户端
        /// </summary>
        private void SyncToAllClients()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            // 准备同步数据
            ushort[] playerIds = playerToSeatMap.Keys.ToArray();
            int[] seatIndices = playerToSeatMap.Values.ToArray();

            // 通过NetworkManager发送RPC
            NetworkManager.Instance.BroadcastSeatsAndCharacters(playerIds, seatIndices);
            LogDebug($"向所有客户端同步数据：{playerIds.Length} 名玩家");
        }

        /// <summary>
        /// 接收主机端同步的数据（由NetworkManager调用）
        /// </summary>
        public void ReceiveSeatsAndCharactersData(ushort[] playerIds, int[] seatIndices)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                LogDebug("主机端忽略同步数据");
                return;
            }

            LogDebug($"客户端接收同步数据：{playerIds.Length} 名玩家");

            StartCoroutine(ProcessSyncData(playerIds, seatIndices));
        }

        /// <summary>
        /// 处理同步数据
        /// </summary>
        private IEnumerator ProcessSyncData(ushort[] playerIds, int[] seatIndices)
        {
            // 等待本地座位系统准备就绪
            yield return new WaitUntil(() => seatingSystem.IsInitialized);

            LogDebug("开始处理同步数据");

            // 清理现有数据
            ClearAllData();

            // 重新生成座位（使用相同的玩家数量）
            seatingSystem.GenerateSeats(playerIds.Length);
            hasGeneratedSeats = true;

            // 重建映射关系
            for (int i = 0; i < playerIds.Length; i++)
            {
                ushort playerId = playerIds[i];
                int seatIndex = seatIndices[i];

                playerToSeatMap[playerId] = seatIndex;
                seatToPlayerMap[seatIndex] = playerId;

                // 占用座位
                var seatData = seatingSystem.GetSeatData(seatIndex);
                if (seatData?.seatInstance != null)
                {
                    var seatIdentifier = seatData.seatInstance.GetComponent<SeatIdentifier>();
                    if (seatIdentifier != null)
                    {
                        var playerName = GetPlayerNameById(playerId);
                        seatIdentifier.OccupySeat(playerName);
                    }
                }
            }

            // 生成角色
            SpawnAllCharacters();

            OnSeatsGenerated?.Invoke(playerIds.Length);
            LogDebug("客户端数据同步完成");
        }

        #endregion

        #region 网络事件处理

        /// <summary>
        /// 玩家加入房间
        /// </summary>
        private void OnPlayerJoined(ushort playerId)
        {
            LogDebug($"玩家 {playerId} 加入房间");

            if (PhotonNetwork.IsMasterClient)
            {
                // 重新生成座位和角色
                StartCoroutine(DelayedRegenerateSeatsAndCharacters());
            }
        }

        /// <summary>
        /// 玩家离开房间
        /// </summary>
        private void OnPlayerLeft(ushort playerId)
        {
            LogDebug($"玩家 {playerId} 离开房间");

            // 移除玩家角色，但保留座位
            if (playerCharacters.ContainsKey(playerId))
            {
                Destroy(playerCharacters[playerId]);
                playerCharacters.Remove(playerId);
                LogDebug($"已移除玩家 {playerId} 的角色");
            }

            // 如果是主机，重新生成（可选，根据需求决定）
            // 注释掉以保持座位不变，支持断线重连
            /*
            if (PhotonNetwork.IsMasterClient)
            {
                StartCoroutine(DelayedRegenerateSeatsAndCharacters());
            }
            */
        }

        /// <summary>
        /// 延迟重新生成座位和角色
        /// </summary>
        private IEnumerator DelayedRegenerateSeatsAndCharacters()
        {
            yield return new WaitForSeconds(1f); // 等待网络状态稳定
            GenerateSeatsAndSpawnCharacters();
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 获取玩家的座位索引
        /// </summary>
        public int GetPlayerSeatIndex(ushort playerId)
        {
            return playerToSeatMap.TryGetValue(playerId, out int seatIndex) ? seatIndex : -1;
        }

        /// <summary>
        /// 获取玩家角色对象
        /// </summary>
        public GameObject GetPlayerCharacter(ushort playerId)
        {
            return playerCharacters.TryGetValue(playerId, out GameObject character) ? character : null;
        }

        /// <summary>
        /// 获取玩家的座位标识符
        /// </summary>
        public SeatIdentifier GetPlayerSeatIdentifier(ushort playerId)
        {
            int seatIndex = GetPlayerSeatIndex(playerId);
            if (seatIndex >= 0)
            {
                var seatData = seatingSystem.GetSeatData(seatIndex);
                return seatData?.seatInstance?.GetComponent<SeatIdentifier>();
            }
            return null;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 清理所有数据
        /// </summary>
        private void ClearAllData()
        {
            // 销毁现有角色
            foreach (var character in playerCharacters.Values)
            {
                if (character != null)
                {
                    Destroy(character);
                }
            }

            // 清理映射
            playerToSeatMap.Clear();
            seatToPlayerMap.Clear();
            playerCharacters.Clear();

            hasGeneratedSeats = false;
            hasSpawnedCharacters = false;

            LogDebug("已清理所有数据");
        }

        /// <summary>
        /// 根据ID获取玩家名称
        /// </summary>
        private string GetPlayerNameById(ushort playerId)
        {
            var player = PhotonNetwork.PlayerList.FirstOrDefault(p => p.ActorNumber == playerId);
            return player?.NickName ?? $"Player_{playerId}";
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[NetworkPlayerSpawner] {message}");
            }
        }

        #endregion
    }
}