using UnityEngine;
using Photon.Realtime;
using ExitGames.Client.Photon;

namespace Lobby.Data
{
    /// <summary>
    /// Lobby房间数据模型
    /// 存储房间在大厅列表中显示的信息，支持Photon集成
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

        [Header("Photon集成")]
        public bool isPhotonRoom = false; // 标记是否为Photon房间
        public string photonRoomName = ""; // Photon内部房间名称

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

        #region 构造函数

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
            createTime = 0f; // 初始化为0，后续通过Initialize方法设置
            gameMode = "Classic";
            gameDifficulty = 1;
            isPhotonRoom = false;
            photonRoomName = "";
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
            createTime = 0f; // 初始化为0，后续通过Initialize方法设置
            gameMode = "Classic";
            gameDifficulty = 1;
            isPhotonRoom = false;
            photonRoomName = name; // 默认使用相同名称
        }

        #endregion

        #region 初始化方法

        /// <summary>
        /// 初始化房间数据（在运行时调用，解决序列化问题）
        /// </summary>
        public void Initialize()
        {
            // 设置创建时间
            if (createTime <= 0f)
            {
                createTime = Time.time;
            }

            // 确保有有效的ID
            if (string.IsNullOrEmpty(roomId))
            {
                roomId = System.Guid.NewGuid().ToString();
            }

            // 如果是Photon房间且photonRoomName为空，使用roomName
            if (isPhotonRoom && string.IsNullOrEmpty(photonRoomName))
            {
                photonRoomName = roomName;
            }
        }

        /// <summary>
        /// 创建新的房间数据（静态工厂方法）
        /// </summary>
        public static LobbyRoomData CreateNew(string name, int maxPlayer, string host)
        {
            var roomData = new LobbyRoomData(name, maxPlayer, host);
            roomData.Initialize(); // 立即初始化
            return roomData;
        }

        /// <summary>
        /// 创建默认房间数据
        /// </summary>
        public static LobbyRoomData CreateDefault()
        {
            var roomData = new LobbyRoomData();
            roomData.Initialize(); // 立即初始化
            return roomData;
        }

        #endregion

        #region Photon集成方法

        /// <summary>
        /// 从Photon RoomInfo创建LobbyRoomData
        /// </summary>
        public static LobbyRoomData FromPhotonRoom(RoomInfo roomInfo)
        {
            if (roomInfo == null)
            {
                Debug.LogWarning("[LobbyRoomData] RoomInfo为空，无法创建房间数据");
                return null;
            }

            var lobbyRoom = new LobbyRoomData();

            // 标记为Photon房间
            lobbyRoom.isPhotonRoom = true;
            lobbyRoom.photonRoomName = roomInfo.Name;

            // 基础信息
            lobbyRoom.roomName = roomInfo.Name;
            lobbyRoom.roomId = roomInfo.Name; // Photon使用房间名作为唯一标识
            lobbyRoom.maxPlayers = roomInfo.MaxPlayers;
            lobbyRoom.currentPlayers = roomInfo.PlayerCount;

            // 从自定义属性中获取额外信息
            var customProps = roomInfo.CustomProperties;

            // 主机名称
            if (customProps.TryGetValue("hostName", out object hostName))
            {
                lobbyRoom.hostPlayerName = hostName.ToString();
            }
            else
            {
                lobbyRoom.hostPlayerName = "Unknown Host";
            }

            // 密码状态
            if (customProps.TryGetValue("hasPassword", out object hasPassword))
            {
                lobbyRoom.hasPassword = (bool)hasPassword;
            }

            // 游戏模式
            if (customProps.TryGetValue("gameMode", out object gameMode))
            {
                lobbyRoom.gameMode = gameMode.ToString();
            }

            // 游戏难度
            if (customProps.TryGetValue("difficulty", out object difficulty))
            {
                lobbyRoom.gameDifficulty = (int)difficulty;
            }

            // 创建时间 - 安全获取
            if (customProps.TryGetValue("createTime", out object createTime))
            {
                lobbyRoom.createTime = (float)createTime;
            }
            else
            {
                // 如果没有创建时间信息，使用当前时间（仅在运行时）
                if (Application.isPlaying)
                {
                    lobbyRoom.createTime = Time.time;
                }
                else
                {
                    lobbyRoom.createTime = 0f; // 编辑器模式下设为0
                }
            }

            // 确定房间状态
            lobbyRoom.status = DeterminePhotonRoomStatus(roomInfo);

            return lobbyRoom;
        }

        /// <summary>
        /// 转换为Photon RoomOptions
        /// </summary>
        public RoomOptions ToPhotonRoomOptions()
        {
            var roomOptions = new RoomOptions();

            // 基础设置
            roomOptions.MaxPlayers = (byte)Mathf.Clamp(maxPlayers, 2, 20); // Photon限制
            roomOptions.IsVisible = true;
            roomOptions.IsOpen = true;

            // 设置自定义属性
            roomOptions.CustomRoomProperties = CreatePhotonCustomProperties();

            // 设置在大厅中可见的属性
            roomOptions.CustomRoomPropertiesForLobby = new string[]
            {
                "hostName",
                "hasPassword",
                "gameMode",
                "difficulty",
                "createTime"
            };

            return roomOptions;
        }

        /// <summary>
        /// 创建Photon自定义属性
        /// </summary>
        private Hashtable CreatePhotonCustomProperties()
        {
            var customProps = new Hashtable();

            customProps["hostName"] = hostPlayerName;
            customProps["hasPassword"] = hasPassword;
            customProps["gameMode"] = gameMode;
            customProps["difficulty"] = gameDifficulty;

            // 安全设置创建时间
            if (Application.isPlaying)
            {
                customProps["createTime"] = Time.time;
            }
            else
            {
                customProps["createTime"] = createTime > 0f ? createTime : 0f;
            }

            // 可以根据需要添加更多自定义属性
            customProps["version"] = Application.version;

            return customProps;
        }

        /// <summary>
        /// 确定Photon房间状态
        /// </summary>
        private static RoomStatus DeterminePhotonRoomStatus(RoomInfo roomInfo)
        {
            // 房间已关闭或从列表中移除
            if (!roomInfo.IsOpen || roomInfo.RemovedFromList)
                return RoomStatus.Closed;

            // 房间已满
            if (roomInfo.PlayerCount >= roomInfo.MaxPlayers)
                return RoomStatus.Full;

            // 检查是否在游戏中（从自定义属性）
            if (roomInfo.CustomProperties.TryGetValue("inGame", out object inGame) && (bool)inGame)
                return RoomStatus.InGame;

            // 默认为等待状态
            return RoomStatus.Waiting;
        }

        /// <summary>
        /// 与Photon房间同步数据
        /// </summary>
        public void SyncWithPhotonRoom(RoomInfo roomInfo)
        {
            if (roomInfo == null || !isPhotonRoom)
            {
                Debug.LogWarning("[LobbyRoomData] 无法同步：RoomInfo为空或不是Photon房间");
                return;
            }

            // 同步基础信息
            currentPlayers = roomInfo.PlayerCount;
            maxPlayers = roomInfo.MaxPlayers;

            // 更新状态
            status = DeterminePhotonRoomStatus(roomInfo);

            // 同步自定义属性
            var customProps = roomInfo.CustomProperties;

            if (customProps.TryGetValue("hostName", out object hostName))
                hostPlayerName = hostName.ToString();

            if (customProps.TryGetValue("hasPassword", out object hasPassword))
                this.hasPassword = (bool)hasPassword;

            if (customProps.TryGetValue("gameMode", out object gameMode))
                this.gameMode = gameMode.ToString();

            if (customProps.TryGetValue("difficulty", out object difficulty))
                this.gameDifficulty = (int)difficulty;
        }

        #endregion

        #region 验证方法

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
        /// 验证是否符合Photon要求
        /// </summary>
        public bool IsValidForPhoton()
        {
            // Photon房间名不能包含换行符
            if (roomName.Contains("\n") || roomName.Contains("\r"))
                return false;

            // Photon房间名长度限制
            if (roomName.Length > 50)
                return false;

            // 检查特殊字符
            if (roomName.Contains("\"") || roomName.Contains("'"))
                return false;

            return IsRoomSettingsValid();
        }

        #endregion

        #region 显示方法

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
        /// 获取房间类型标识
        /// </summary>
        public string GetRoomTypeText()
        {
            if (isPhotonRoom)
                return "[Photon]";
            return "[Local]";
        }

        /// <summary>
        /// 获取完整的房间显示名称
        /// </summary>
        public string GetFullDisplayName()
        {
            string displayName = roomName;

            if (hasPassword)
                displayName = "🔒 " + displayName;

            if (isPhotonRoom)
                displayName += " [P]"; // Photon标识

            return displayName;
        }

        #endregion

        #region 实用方法

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
            clone.isPhotonRoom = this.isPhotonRoom;
            clone.photonRoomName = this.photonRoomName;
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
        /// 清理房间名称使其符合Photon要求
        /// </summary>
        public void CleanRoomNameForPhoton()
        {
            if (string.IsNullOrEmpty(roomName))
            {
                roomName = "Room_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
                return;
            }

            // 移除不允许的字符
            roomName = roomName.Replace("\n", "").Replace("\r", "").Replace("\"", "").Replace("'", "");

            // 限制长度
            if (roomName.Length > 50)
                roomName = roomName.Substring(0, 47) + "...";

            // 更新Photon房间名
            photonRoomName = roomName;
        }

        /// <summary>
        /// 比较两个房间是否相同
        /// </summary>
        public bool IsSameRoom(LobbyRoomData other)
        {
            if (other == null) return false;

            // 如果都是Photon房间，比较Photon房间名
            if (isPhotonRoom && other.isPhotonRoom)
                return photonRoomName == other.photonRoomName;

            // 否则比较房间ID
            return roomId == other.roomId;
        }

        /// <summary>
        /// 获取房间年龄（创建后经过的时间）
        /// </summary>
        public float GetRoomAge()
        {
            return Time.time - createTime;
        }

        /// <summary>
        /// 获取房间填充率
        /// </summary>
        public float GetFillRate()
        {
            if (maxPlayers <= 0) return 0f;
            return (float)currentPlayers / maxPlayers;
        }

        #endregion

        #region 调试方法

        /// <summary>
        /// 获取详细的调试信息
        /// </summary>
        public string GetDetailedInfo()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine("=== 房间详细信息 ===");
            info.AppendLine($"房间名称: {roomName}");
            info.AppendLine($"房间ID: {roomId}");
            info.AppendLine($"主机: {hostPlayerName}");
            info.AppendLine($"玩家数: {currentPlayers}/{maxPlayers} ({GetFillRate():P1})");
            info.AppendLine($"状态: {GetStatusText()}");
            info.AppendLine($"有密码: {(hasPassword ? "是" : "否")}");
            info.AppendLine($"游戏模式: {gameMode}");
            info.AppendLine($"难度: {gameDifficulty}");
            info.AppendLine($"房间年龄: {GetRoomAge():F1}秒");
            info.AppendLine($"Photon房间: {(isPhotonRoom ? "是" : "否")}");

            if (isPhotonRoom)
            {
                info.AppendLine($"Photon房间名: {photonRoomName}");
            }

            return info.ToString();
        }

        /// <summary>
        /// 获取调试信息
        /// </summary>
        public override string ToString()
        {
            string baseInfo = $"LobbyRoomData[Name: {roomName}, Players: {currentPlayers}/{maxPlayers}, Status: {status}, Host: {hostPlayerName}";

            if (isPhotonRoom)
            {
                baseInfo += $", Photon: {photonRoomName}";
            }

            baseInfo += "]";
            return baseInfo;
        }

        /// <summary>
        /// 验证数据完整性
        /// </summary>
        public bool ValidateDataIntegrity()
        {
            bool isValid = true;

            if (string.IsNullOrEmpty(roomName))
            {
                Debug.LogWarning("[LobbyRoomData] 房间名称为空");
                isValid = false;
            }

            if (string.IsNullOrEmpty(roomId))
            {
                Debug.LogWarning("[LobbyRoomData] 房间ID为空");
                isValid = false;
            }

            if (maxPlayers < 2 || maxPlayers > 20)
            {
                Debug.LogWarning($"[LobbyRoomData] 最大玩家数异常: {maxPlayers}");
                isValid = false;
            }

            if (currentPlayers < 0 || currentPlayers > maxPlayers)
            {
                Debug.LogWarning($"[LobbyRoomData] 当前玩家数异常: {currentPlayers}/{maxPlayers}");
                isValid = false;
            }

            if (isPhotonRoom && string.IsNullOrEmpty(photonRoomName))
            {
                Debug.LogWarning("[LobbyRoomData] Photon房间但房间名为空");
                isValid = false;
            }

            return isValid;
        }

        #endregion
    }
}