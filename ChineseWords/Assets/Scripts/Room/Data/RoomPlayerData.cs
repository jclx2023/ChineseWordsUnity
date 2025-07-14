using UnityEngine;
using Lobby.Data;

namespace RoomScene.Data
{
    /// <summary>
    /// 房间玩家数据 - 扩展LobbyPlayerData，添加模型选择功能
    /// </summary>
    [System.Serializable]
    public class RoomPlayerData
    {
        [Header("基础信息")]
        public ushort playerId;
        public string playerName;
        public bool isHost;
        public bool isReady;

        [Header("模型选择")]
        public int selectedModelId = 0;          // 选择的模型ID
        public string selectedModelName = "";    // 模型名称（用于显示）

        [Header("网络状态")]
        public bool isOnline = true;
        public float lastSyncTime;

        /// <summary>
        /// 从LobbyPlayerData构造
        /// </summary>
        public RoomPlayerData(LobbyPlayerData lobbyData, ushort id)
        {
            playerId = id;
            playerName = lobbyData.GetDisplayName();
            selectedModelId = lobbyData.avatarIndex; // 将Lobby的avatarIndex映射为modelId
            isHost = false;
            isReady = false;
            isOnline = true;
            lastSyncTime = 0f; // 不在构造函数中调用Time.time
        }

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public RoomPlayerData()
        {
            playerId = 0;
            playerName = "未知玩家";
            selectedModelId = 0;
            selectedModelName = "";
            isHost = false;
            isReady = false;
            isOnline = true;
            lastSyncTime = 0f; // 不在构造函数中调用Time.time
        }

        /// <summary>
        /// 设置模型选择
        /// </summary>
        public void SetSelectedModel(int modelId, string modelName)
        {
            selectedModelId = modelId;
            selectedModelName = modelName;
            UpdateSyncTime(); // 使用方法来更新时间
        }

        /// <summary>
        /// 设置准备状态
        /// </summary>
        public void SetReady(bool ready)
        {
            isReady = ready;
            UpdateSyncTime(); // 使用方法来更新时间
        }

        /// <summary>
        /// 更新同步时间（安全的时间更新方法）
        /// </summary>
        public void UpdateSyncTime()
        {
            if (Application.isPlaying) // 只在运行时更新时间
            {
                lastSyncTime = Time.time;
            }
        }

        /// <summary>
        /// 初始化同步时间（在Awake或Start中调用）
        /// </summary>
        public void InitializeSyncTime()
        {
            lastSyncTime = Time.time;
        }

        /// <summary>
        /// 获取调试信息
        /// </summary>
        public override string ToString()
        {
            return $"RoomPlayer[ID:{playerId}, Name:{playerName}, Model:{selectedModelId}, Ready:{isReady}]";
        }

        /// <summary>
        /// 转换为网络传输格式
        /// </summary>
        public RoomPlayerNetworkData ToNetworkData()
        {
            return new RoomPlayerNetworkData
            {
                playerId = this.playerId,
                playerName = this.playerName,
                isHost = this.isHost,
                isReady = this.isReady,
                selectedModelId = this.selectedModelId,
                selectedModelName = this.selectedModelName
            };
        }

        /// <summary>
        /// 从网络数据构造
        /// </summary>
        public static RoomPlayerData FromNetworkData(RoomPlayerNetworkData networkData)
        {
            var data = new RoomPlayerData
            {
                playerId = networkData.playerId,
                playerName = networkData.playerName,
                isHost = networkData.isHost,
                isReady = networkData.isReady,
                selectedModelId = networkData.selectedModelId,
                selectedModelName = networkData.selectedModelName,
                isOnline = true,
                lastSyncTime = 0f // 不在构造时调用Time.time
            };

            // 创建后初始化时间
            data.InitializeSyncTime();
            return data;
        }
    }

    /// <summary>
    /// 网络传输用的玩家数据结构
    /// </summary>
    [System.Serializable]
    public class RoomPlayerNetworkData
    {
        public ushort playerId;
        public string playerName;
        public bool isHost;
        public bool isReady;
        public int selectedModelId;
        public string selectedModelName;
    }
}