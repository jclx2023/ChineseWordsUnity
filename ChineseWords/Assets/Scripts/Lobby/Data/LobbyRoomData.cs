using UnityEngine;

namespace Lobby.Data
{
    /// <summary>
    /// Lobby房间数据模型
    /// 存储房间在大厅列表中显示的信息
    /// </summary>
    [System.Serializable]
    public class LobbyRoomData
    {
        [Header("基础信息")]
        public string roomName = "";
        public string roomId = "";
        public string hostPlayerName = "";

        [Header("房间设置")]
        public int maxPlayers = 4;
        public int currentPlayers = 0;
        public bool hasPassword = false;
        public string password = "";

        [Header("房间状态")]
        public RoomStatus status = RoomStatus.Waiting;
        public float createTime = 0f;

        [Header("游戏设置")]
        public string gameMode = "Classic";
        public int gameDifficulty = 1;

        /// <summary>
        /// 房间状态枚举
        /// </summary>
        public enum RoomStatus
        {
            Waiting,    // 等待玩家
            Full,       // 房间已满
            InGame,     // 游戏中
            Closed      // 房间关闭
        }

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public LobbyRoomData()
        {
            roomName = "";
            roomId = System.Guid.NewGuid().ToString();
            hostPlayerName = "";
            maxPlayers = 4;
            currentPlayers = 0;
            hasPassword = false;
            password = "";
            status = RoomStatus.Waiting;
            createTime = Time.time;
            gameMode = "Classic";
            gameDifficulty = 1;
        }

        /// <summary>
        /// 带参数构造函数
        /// </summary>
        public LobbyRoomData(string name, int maxPlayer, string host)
        {
            roomName = name;
            roomId = System.Guid.NewGuid().ToString();
            hostPlayerName = host;
            maxPlayers = maxPlayer;
            currentPlayers = 1; // 主机算一个玩家
            hasPassword = false;
            password = "";
            status = RoomStatus.Waiting;
            createTime = Time.time;
            gameMode = "Classic";
            gameDifficulty = 1;
        }

        /// <summary>
        /// 检查房间是否可以加入
        /// </summary>
        public bool CanJoin()
        {
            return status == RoomStatus.Waiting && currentPlayers < maxPlayers;
        }

        /// <summary>
        /// 检查房间是否已满
        /// </summary>
        public bool IsFull()
        {
            return currentPlayers >= maxPlayers;
        }

        /// <summary>
        /// 获取房间状态文本
        /// </summary>
        public string GetStatusText()
        {
            switch (status)
            {
                case RoomStatus.Waiting:
                    return IsFull() ? "房间已满" : "等待中";
                case RoomStatus.Full:
                    return "房间已满";
                case RoomStatus.InGame:
                    return "游戏中";
                case RoomStatus.Closed:
                    return "已关闭";
                default:
                    return "未知状态";
            }
        }

        /// <summary>
        /// 获取玩家数量文本
        /// </summary>
        public string GetPlayerCountText()
        {
            return $"{currentPlayers}/{maxPlayers}";
        }

        /// <summary>
        /// 验证房间名称是否有效
        /// </summary>
        public bool IsRoomNameValid()
        {
            return !string.IsNullOrEmpty(roomName) &&
                   roomName.Length >= 2 &&
                   roomName.Length <= 30;
        }

        /// <summary>
        /// 验证房间设置是否有效
        /// </summary>
        public bool IsRoomSettingsValid()
        {
            return IsRoomNameValid() &&
                   maxPlayers >= 2 &&
                   maxPlayers <= 8 &&
                   !string.IsNullOrEmpty(hostPlayerName);
        }

        /// <summary>
        /// 创建副本
        /// </summary>
        public LobbyRoomData Clone()
        {
            var clone = new LobbyRoomData();
            clone.roomName = this.roomName;
            clone.roomId = this.roomId;
            clone.hostPlayerName = this.hostPlayerName;
            clone.maxPlayers = this.maxPlayers;
            clone.currentPlayers = this.currentPlayers;
            clone.hasPassword = this.hasPassword;
            clone.password = this.password;
            clone.status = this.status;
            clone.createTime = this.createTime;
            clone.gameMode = this.gameMode;
            clone.gameDifficulty = this.gameDifficulty;
            return clone;
        }

        /// <summary>
        /// 更新房间状态
        /// </summary>
        public void UpdateStatus()
        {
            if (currentPlayers >= maxPlayers)
            {
                status = RoomStatus.Full;
            }
            else if (currentPlayers > 0)
            {
                status = RoomStatus.Waiting;
            }
            else
            {
                status = RoomStatus.Closed;
            }
        }

        /// <summary>
        /// 获取调试信息
        /// </summary>
        public override string ToString()
        {
            return $"LobbyRoomData[Name: {roomName}, Players: {currentPlayers}/{maxPlayers}, Status: {status}, Host: {hostPlayerName}]";
        }
    }
}