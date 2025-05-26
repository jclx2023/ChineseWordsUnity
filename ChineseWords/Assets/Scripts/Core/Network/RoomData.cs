using System.Collections.Generic;
using UnityEngine;

namespace Core.Network
{
    /// <summary>
    /// 房间状态枚举
    /// </summary>
    public enum RoomState
    {
        Waiting,    // 等待玩家加入
        Ready,      // 准备开始（所有玩家已准备）
        Starting,   // 正在启动游戏
        InGame,     // 游戏进行中
        Ended       // 游戏结束
    }

    /// <summary>
    /// 玩家房间状态
    /// </summary>
    public enum PlayerRoomState
    {
        Connected,  // 已连接，未准备
        Ready,      // 已准备
        InGame,     // 游戏中
        Disconnected // 已断线
    }

    /// <summary>
    /// 房间内玩家信息
    /// </summary>
    [System.Serializable]
    public class RoomPlayer
    {
        public ushort playerId;
        public string playerName;
        public PlayerRoomState state;
        public bool isHost;
        public float joinTime;

        public RoomPlayer(ushort id, string name, bool host = false)
        {
            playerId = id;
            playerName = name;
            isHost = host;
            state = PlayerRoomState.Connected;
            joinTime = Time.time;
        }
    }

    /// <summary>
    /// 房间数据
    /// </summary>
    [System.Serializable]
    public class RoomData
    {
        public string roomName;
        public string roomCode;
        public RoomState state;
        public ushort hostId;
        public int maxPlayers;
        public Dictionary<ushort, RoomPlayer> players;
        public float createTime;

        public RoomData(string name, string code, ushort hostPlayerId, int maxPlayerCount = 4)
        {
            roomName = name;
            roomCode = code;
            hostId = hostPlayerId;
            maxPlayers = maxPlayerCount;
            state = RoomState.Waiting;
            players = new Dictionary<ushort, RoomPlayer>();
            createTime = Time.time;
        }

        /// <summary>
        /// 添加玩家到房间
        /// </summary>
        public bool AddPlayer(ushort playerId, string playerName)
        {
            if (players.Count >= maxPlayers || players.ContainsKey(playerId))
                return false;

            bool isHost = playerId == hostId;
            players[playerId] = new RoomPlayer(playerId, playerName, isHost);
            return true;
        }

        /// <summary>
        /// 移除玩家
        /// </summary>
        public bool RemovePlayer(ushort playerId)
        {
            return players.Remove(playerId);
        }

        /// <summary>
        /// 设置玩家准备状态
        /// </summary>
        public bool SetPlayerReady(ushort playerId, bool ready)
        {
            if (!players.ContainsKey(playerId))
                return false;

            players[playerId].state = ready ? PlayerRoomState.Ready : PlayerRoomState.Connected;
            return true;
        }

        /// <summary>
        /// 检查是否所有玩家都已准备（房主不需要准备）
        /// </summary>
        public bool AreAllPlayersReady()
        {
            if (players.Count < 2) // 至少需要2个玩家
                return false;

            foreach (var player in players.Values)
            {
                // 房主不需要准备，跳过检查
                if (player.isHost)
                    continue;

                // 其他玩家必须准备
                if (player.state != PlayerRoomState.Ready)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 获取准备的玩家数量（不包括房主）
        /// </summary>
        public int GetReadyPlayerCount()
        {
            int count = 0;
            foreach (var player in players.Values)
            {
                // 房主不算在准备数量中
                if (player.isHost)
                    continue;

                if (player.state == PlayerRoomState.Ready)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 获取需要准备的玩家总数（不包括房主）
        /// </summary>
        public int GetNonHostPlayerCount()
        {
            int count = 0;
            foreach (var player in players.Values)
            {
                if (!player.isHost)
                    count++;
            }
            return count;
        }
    }
}