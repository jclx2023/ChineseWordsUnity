using UnityEngine;
using Photon.Realtime;
using ExitGames.Client.Photon;
using System.Collections.Generic;
using Lobby.Data;

namespace Lobby.Network
{
    /// <summary>
    /// Photon与Lobby数据转换器
    /// 负责在Photon数据格式和Lobby数据格式之间转换
    /// </summary>
    public static class PhotonLobbyDataConverter
    {
        // 自定义房间属性键名
        public const string ROOM_GAME_MODE = "gameMode";
        public const string ROOM_DIFFICULTY = "difficulty";
        public const string ROOM_HAS_PASSWORD = "hasPassword";
        public const string ROOM_HOST_NAME = "hostName";
        public const string ROOM_CREATE_TIME = "createTime";

        #region Photon -> Lobby 数据转换

        /// <summary>
        /// 将Photon RoomInfo转换为LobbyRoomData
        /// </summary>
        public static LobbyRoomData FromPhotonRoom(RoomInfo roomInfo)
        {
            if (roomInfo == null)
            {
                Debug.LogWarning("[PhotonLobbyDataConverter] RoomInfo为空");
                return null;
            }

            var lobbyRoom = new LobbyRoomData();

            // 基础信息
            lobbyRoom.roomName = roomInfo.Name;
            lobbyRoom.roomId = roomInfo.Name; // Photon使用房间名作为ID
            lobbyRoom.maxPlayers = roomInfo.MaxPlayers;
            lobbyRoom.currentPlayers = roomInfo.PlayerCount;

            // 从自定义属性中获取额外信息
            var customProps = roomInfo.CustomProperties;

            // 主机名称
            if (customProps.TryGetValue(ROOM_HOST_NAME, out object hostName))
            {
                lobbyRoom.hostPlayerName = hostName.ToString();
            }
            else
            {
                lobbyRoom.hostPlayerName = "Unknown";
            }

            // 密码状态
            if (customProps.TryGetValue(ROOM_HAS_PASSWORD, out object hasPassword))
            {
                lobbyRoom.hasPassword = (bool)hasPassword;
            }

            // 游戏模式
            if (customProps.TryGetValue(ROOM_GAME_MODE, out object gameMode))
            {
                lobbyRoom.gameMode = gameMode.ToString();
            }

            // 难度
            if (customProps.TryGetValue(ROOM_DIFFICULTY, out object difficulty))
            {
                lobbyRoom.gameDifficulty = (int)difficulty;
            }

            // 创建时间
            if (customProps.TryGetValue(ROOM_CREATE_TIME, out object createTime))
            {
                lobbyRoom.createTime = (float)createTime;
            }

            // 确定房间状态
            lobbyRoom.status = DetermineRoomStatus(roomInfo);

            return lobbyRoom;
        }

        /// <summary>
        /// 批量转换房间列表
        /// </summary>
        public static List<LobbyRoomData> FromPhotonRoomList(List<RoomInfo> roomList)
        {
            var lobbyRooms = new List<LobbyRoomData>();

            if (roomList == null)
                return lobbyRooms;

            foreach (var roomInfo in roomList)
            {
                // 过滤掉已关闭或无效的房间
                if (roomInfo.RemovedFromList || !roomInfo.IsOpen || !roomInfo.IsVisible)
                    continue;

                var lobbyRoom = FromPhotonRoom(roomInfo);
                if (lobbyRoom != null)
                {
                    lobbyRooms.Add(lobbyRoom);
                }
            }

            return lobbyRooms;
        }

        /// <summary>
        /// 确定房间状态
        /// </summary>
        private static LobbyRoomData.RoomStatus DetermineRoomStatus(RoomInfo roomInfo)
        {
            if (!roomInfo.IsOpen)
                return LobbyRoomData.RoomStatus.Closed;

            if (roomInfo.PlayerCount >= roomInfo.MaxPlayers)
                return LobbyRoomData.RoomStatus.Full;

            return LobbyRoomData.RoomStatus.Waiting;
        }

        #endregion

        #region Lobby -> Photon 数据转换

        /// <summary>
        /// 将LobbyRoomData转换为Photon RoomOptions
        /// </summary>
        public static RoomOptions ToPhotonRoomOptions(LobbyRoomData lobbyRoom, string password = "")
        {
            if (lobbyRoom == null)
            {
                Debug.LogWarning("[PhotonLobbyDataConverter] LobbyRoomData为空");
                return null;
            }

            var roomOptions = new RoomOptions();

            // 基础设置
            roomOptions.MaxPlayers = (byte)lobbyRoom.maxPlayers;
            roomOptions.IsVisible = true;
            roomOptions.IsOpen = true;

            // 自定义属性
            roomOptions.CustomRoomProperties = CreateCustomProperties(lobbyRoom);

            // 将自定义属性添加到大厅可见属性列表
            roomOptions.CustomRoomPropertiesForLobby = new string[]
            {
                ROOM_GAME_MODE,
                ROOM_DIFFICULTY,
                ROOM_HAS_PASSWORD,
                ROOM_HOST_NAME,
                ROOM_CREATE_TIME
            };

            return roomOptions;
        }

        /// <summary>
        /// 创建自定义房间属性
        /// </summary>
        private static Hashtable CreateCustomProperties(LobbyRoomData lobbyRoom)
        {
            var customProps = new Hashtable();

            // 基础属性
            customProps[ROOM_GAME_MODE] = lobbyRoom.gameMode;
            customProps[ROOM_DIFFICULTY] = lobbyRoom.gameDifficulty;
            customProps[ROOM_HAS_PASSWORD] = lobbyRoom.hasPassword;
            customProps[ROOM_HOST_NAME] = lobbyRoom.hostPlayerName;

            // 安全设置创建时间
            if (Application.isPlaying)
            {
                customProps[ROOM_CREATE_TIME] = Time.time;
            }
            else
            {
                customProps[ROOM_CREATE_TIME] = lobbyRoom.createTime > 0f ? lobbyRoom.createTime : 0f;
            }

            return customProps;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 颜色转字符串
        /// </summary>
        private static string ColorToString(Color color)
        {
            return $"{color.r:F2},{color.g:F2},{color.b:F2},{color.a:F2}";
        }

        /// <summary>
        /// 清理房间名称使其符合Photon要求
        /// </summary>
        public static string CleanRoomNameForPhoton(string roomName)
        {
            if (string.IsNullOrEmpty(roomName))
                return "Room_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);

            // 移除不允许的字符
            roomName = roomName.Replace("\n", "").Replace("\r", "");

            // 限制长度
            if (roomName.Length > 50)
                roomName = roomName.Substring(0, 47) + "...";

            return roomName;
        }

        #endregion
    }
}