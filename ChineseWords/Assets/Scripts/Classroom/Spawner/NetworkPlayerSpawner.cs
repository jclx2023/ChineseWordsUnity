using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Classroom.Scene;
using Core.Network;
using RoomScene.PlayerModel; // 引用PlayerModelData

namespace Classroom.Player
{
    /// <summary>
    /// 网络玩家生成器 - 支持根据玩家模型选择生成个性化角色
    /// 使用静态资源加载，不依赖PlayerModelManager实例
    /// </summary>
    public class NetworkPlayerSpawner : MonoBehaviour
    {
        [Header("核心配置")]
        [SerializeField] private GameObject playerCharacterPrefab; // 默认角色预制体（回退使用）
        [SerializeField] private CircularSeatingSystem seatingSystem; // 座位系统引用
        [SerializeField] private float spawnDelay = 1f; // 生成延迟（等待场景稳定）

        [Header("角色配置")]
        [SerializeField] private float characterHeight = 1.8f; // 角色高度
        [SerializeField] private Vector3 characterScale = Vector3.one; // 角色缩放

        [Header("模型资源配置")]
        [SerializeField] private string modelDataResourcesPath = "PlayerModels"; // Resources文件夹中模型数据的路径
        [SerializeField] private int defaultModelId = 0; // 默认模型ID

        [Header("同步配置")]
        [SerializeField] private float sceneLoadTimeout = 10f; // 场景加载超时时间
        [SerializeField] private bool waitForAllPlayersReady = true; // 是否等待所有玩家准备就绪

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 静态模型数据缓存
        private static Dictionary<int, PlayerModelData> modelDataCache = new Dictionary<int, PlayerModelData>();
        private static bool modelDataLoaded = false;

        // 玩家-座位绑定数据
        private Dictionary<ushort, int> playerToSeatMap = new Dictionary<ushort, int>(); // PlayerID -> SeatIndex
        private Dictionary<int, ushort> seatToPlayerMap = new Dictionary<int, ushort>(); // SeatIndex -> PlayerID
        private Dictionary<ushort, GameObject> playerCharacters = new Dictionary<ushort, GameObject>(); // PlayerID -> Character GameObject

        // 生成状态
        private bool isInitialized = false;
        private bool hasGeneratedSeats = false;
        private bool hasSpawnedCharacters = false;
        private List<ushort> readyPlayerIds = new List<ushort>(); // 已准备好的玩家ID列表

        // 网络事件监听状态
        private bool networkEventsSubscribed = false;

        // 公共属性
        public bool IsInitialized => isInitialized;
        public bool HasGeneratedSeats => hasGeneratedSeats;
        public bool HasSpawnedCharacters => hasSpawnedCharacters;
        public int SpawnedCharacterCount => playerCharacters.Count;

        // 事件
        public static event System.Action<int> OnSeatsGenerated; // 座位生成完成
        public static event System.Action<ushort, GameObject> OnCharacterSpawned; // 角色生成完成
        public static event System.Action OnAllCharactersSpawned; // 所有角色生成完成
        public static event System.Action<ushort> OnPlayerJoinedEvent; // 玩家加入事件（转发给ClassroomManager）
        public static event System.Action<ushort> OnPlayerLeftEvent; // 玩家离开事件（转发给ClassroomManager）

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

            // 预加载模型数据
            LoadModelDataIfNeeded();

            isInitialized = true;
            LogDebug("NetworkPlayerSpawner已初始化");
        }

        private void Start()
        {
            if (!isInitialized) return;

            // 只订阅网络事件，不自动开始生成流程
            SubscribeToNetworkEvents();
            LogDebug("NetworkPlayerSpawner已订阅网络事件，等待ClassroomManager调用");
        }

        private void OnDestroy()
        {
            UnsubscribeFromNetworkEvents();
        }

        #endregion

        #region 静态模型数据加载

        /// <summary>
        /// 加载模型数据（如果尚未加载）
        /// </summary>
        private void LoadModelDataIfNeeded()
        {
            if (modelDataLoaded) return;

            try
            {
                LogDebug($"开始从Resources加载模型数据: {modelDataResourcesPath}");

                // 加载所有PlayerModelData资产
                PlayerModelData[] allModelData = Resources.LoadAll<PlayerModelData>(modelDataResourcesPath);

                if (allModelData == null || allModelData.Length == 0)
                {
                    Debug.LogWarning($"[NetworkPlayerSpawner] 未在 Resources/{modelDataResourcesPath} 中找到PlayerModelData资产");
                    return;
                }

                // 建立ID映射
                modelDataCache.Clear();
                foreach (var modelData in allModelData)
                {
                    if (modelData != null && modelData.IsValid())
                    {
                        modelDataCache[modelData.modelId] = modelData;
                        LogDebug($"加载模型数据: ID={modelData.modelId}, Name={modelData.modelName}");
                    }
                }

                modelDataLoaded = true;
                LogDebug($"模型数据加载完成，共加载 {modelDataCache.Count} 个模型");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkPlayerSpawner] 加载模型数据时发生错误: {e.Message}");
            }
        }

        /// <summary>
        /// 获取模型数据
        /// </summary>
        private static PlayerModelData GetModelData(int modelId)
        {
            return modelDataCache.TryGetValue(modelId, out PlayerModelData data) ? data : null;
        }

        /// <summary>
        /// 获取模型的游戏预制体
        /// </summary>
        private static GameObject GetModelGamePrefab(int modelId)
        {
            var modelData = GetModelData(modelId);
            return modelData?.GetGamePrefab();
        }

        #endregion

        #region 网络事件订阅

        private void SubscribeToNetworkEvents()
        {
            if (networkEventsSubscribed) return;

            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerJoined += OnPlayerJoinedInternal;
                NetworkManager.OnPlayerLeft += OnPlayerLeftInternal;
                networkEventsSubscribed = true;
                LogDebug("已订阅网络事件");
            }
        }

        private void UnsubscribeFromNetworkEvents()
        {
            if (!networkEventsSubscribed) return;

            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerJoined -= OnPlayerJoinedInternal;
                NetworkManager.OnPlayerLeft -= OnPlayerLeftInternal;
                networkEventsSubscribed = false;
                LogDebug("已取消订阅网络事件");
            }
        }

        #endregion

        #region 公共接口 - 供ClassroomManager调用

        /// <summary>
        /// 生成座位并分配玩家角色（由ClassroomManager调用）
        /// </summary>
        public void GenerateSeatsAndSpawnCharacters()
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                LogDebug("非主机端，跳过生成逻辑");
                return;
            }

            LogDebug("开始生成座位和角色...");

            // 确保模型数据已加载
            LoadModelDataIfNeeded();

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

            OnSeatsGenerated?.Invoke(playerCount);
            LogDebug("座位和角色生成完成");
        }

        /// <summary>
        /// 同步给所有客户端（由ClassroomManager调用）
        /// </summary>
        public void SyncToAllClients()
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
        /// 延迟初始化生成流程（由ClassroomManager调用）
        /// </summary>
        public IEnumerator DelayedSpawnInitializationCoroutine()
        {
            LogDebug("开始延迟生成初始化");

            // 等待场景稳定
            yield return new WaitForSeconds(spawnDelay);

            // 确保模型数据已加载
            LoadModelDataIfNeeded();

            // 等待所有玩家准备就绪（如果启用）
            if (waitForAllPlayersReady)
            {
                yield return StartCoroutine(WaitForAllPlayersReady());
            }

            LogDebug("延迟初始化完成，准备生成座位和角色");
        }

        #endregion

        #region 原有的生成逻辑（保持不变）

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
        /// 为指定玩家生成角色 - 修改版：支持个性化模型
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
            spawnRotation *= Quaternion.Euler(0, 180, 0);

            // 生成角色（使用个性化模型）
            GameObject character = CreatePlayerCharacter(playerId, spawnPosition, spawnRotation);
            if (character == null)
            {
                Debug.LogError($"[NetworkPlayerSpawner] 为玩家 {playerId} 创建角色失败");
                return;
            }

            character.name = $"Player_{playerId:D2}_Character";
            character.transform.localScale = characterScale;

            // 设置角色属性
            SetupCharacterForPlayer(character, playerId);

            // 记录角色
            playerCharacters[playerId] = character;

            OnCharacterSpawned?.Invoke(playerId, character);
            LogDebug($"为玩家 {playerId} 在座位 {seatIndex} 生成角色（模型ID: {GetPlayerModelId(playerId)}）");
        }

        /// <summary>
        /// 创建玩家角色 - 修改版：使用静态资源加载
        /// </summary>
        private GameObject CreatePlayerCharacter(ushort playerId, Vector3 position, Quaternion rotation)
        {
            // 获取玩家选择的模型ID
            int modelId = GetPlayerModelId(playerId);

            // 尝试通过静态资源加载获取模型预制体
            GameObject modelPrefab = GetModelGamePrefab(modelId);
            if (modelPrefab != null)
            {
                GameObject modelCharacter = Instantiate(modelPrefab, position, rotation);
                LogDebug($"通过静态资源为玩家 {playerId} 创建模型 {modelId} 成功");
                return modelCharacter;
            }
            else
            {
                LogDebug($"静态资源中未找到模型 {modelId}，使用默认预制体");
            }

            // 回退：使用默认预制体
            if (playerCharacterPrefab != null)
            {
                GameObject defaultCharacter = Instantiate(playerCharacterPrefab, position, rotation);
                LogDebug($"为玩家 {playerId} 使用默认预制体创建角色");
                return defaultCharacter;
            }

            Debug.LogError($"[NetworkPlayerSpawner] 无法为玩家 {playerId} 创建角色：缺少默认预制体");
            return null;
        }

        /// <summary>
        /// 获取玩家模型ID
        /// </summary>
        private int GetPlayerModelId(ushort playerId)
        {
            // 从NetworkManager获取玩家模型ID
            if (NetworkManager.Instance != null)
            {
                int modelId = NetworkManager.Instance.GetPlayerModelId(playerId);
                LogDebug($"从NetworkManager获取玩家 {playerId} 模型ID: {modelId}");
                return modelId;
            }

            // 回退：使用默认模型ID
            LogDebug($"NetworkManager不可用，为玩家 {playerId} 使用默认模型ID: {defaultModelId}");
            return defaultModelId;
        }

        /// <summary>
        /// 设置角色属性
        /// </summary>
        private void SetupCharacterForPlayer(GameObject character, ushort playerId)
        {
            LogDebug($"设置角色 {playerId}, 本地ClientId: {NetworkManager.Instance.ClientId}, 是否匹配: {playerId == NetworkManager.Instance.ClientId}");

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

                // 获取座位原始旋转并绕Y轴旋转180°
                Quaternion originalRotation = seatIdentifier.GetSeatRotation();
                Quaternion rotatedRotation = originalRotation * Quaternion.Euler(0, 180, 0);

                // 传递旋转后的朝向给摄像机控制器
                cameraController.SetCameraMount(null, rotatedRotation);
                LogDebug($"为本地玩家 {playerId} 设置摄像机控制器，已应用180°Y轴旋转");
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

        #region 网络同步（保持不变）

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

            // 确保模型数据已加载
            LoadModelDataIfNeeded();

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

        #region 网络事件处理（修改为转发事件）

        /// <summary>
        /// 玩家加入房间（内部处理，转发给ClassroomManager）
        /// </summary>
        private void OnPlayerJoinedInternal(ushort playerId)
        {
            LogDebug($"玩家 {playerId} 加入房间，转发事件给ClassroomManager");

            // 移除玩家角色，但不自动重新生成
            // 转发事件给ClassroomManager，由其决定是否重新生成
            OnPlayerJoinedEvent?.Invoke(playerId);
        }

        /// <summary>
        /// 玩家离开房间（内部处理，转发给ClassroomManager）
        /// </summary>
        private void OnPlayerLeftInternal(ushort playerId)
        {
            LogDebug($"玩家 {playerId} 离开房间");

            // 移除玩家角色，但保留座位
            if (playerCharacters.ContainsKey(playerId))
            {
                Destroy(playerCharacters[playerId]);
                playerCharacters.Remove(playerId);
                LogDebug($"已移除玩家 {playerId} 的角色");
            }

            // 转发事件给ClassroomManager，由其决定是否重新生成
            OnPlayerLeftEvent?.Invoke(playerId);
        }

        #endregion

        #region 公共查询接口（保持不变）

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

        #region 辅助方法（保持不变）

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