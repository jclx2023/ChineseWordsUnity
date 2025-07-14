using UnityEngine;
using Photon.Realtime;
using ExitGames.Client.Photon;
using System.Collections.Generic;
using Lobby.Data;

namespace Lobby.Network
{
    /// <summary>
    /// Photon��Lobby����ת����
    /// ������Photon���ݸ�ʽ��Lobby���ݸ�ʽ֮��ת��
    /// </summary>
    public static class PhotonLobbyDataConverter
    {
        // �Զ��巿�����Լ���
        public const string ROOM_GAME_MODE = "gameMode";
        public const string ROOM_DIFFICULTY = "difficulty";
        public const string ROOM_HAS_PASSWORD = "hasPassword";
        public const string ROOM_HOST_NAME = "hostName";
        public const string ROOM_CREATE_TIME = "createTime";

        #region Photon -> Lobby ����ת��

        /// <summary>
        /// ��Photon RoomInfoת��ΪLobbyRoomData
        /// </summary>
        public static LobbyRoomData FromPhotonRoom(RoomInfo roomInfo)
        {
            if (roomInfo == null)
            {
                Debug.LogWarning("[PhotonLobbyDataConverter] RoomInfoΪ��");
                return null;
            }

            var lobbyRoom = new LobbyRoomData();

            // ������Ϣ
            lobbyRoom.roomName = roomInfo.Name;
            lobbyRoom.roomId = roomInfo.Name; // Photonʹ�÷�������ΪID
            lobbyRoom.maxPlayers = roomInfo.MaxPlayers;
            lobbyRoom.currentPlayers = roomInfo.PlayerCount;

            // ���Զ��������л�ȡ������Ϣ
            var customProps = roomInfo.CustomProperties;

            // ��������
            if (customProps.TryGetValue(ROOM_HOST_NAME, out object hostName))
            {
                lobbyRoom.hostPlayerName = hostName.ToString();
            }
            else
            {
                lobbyRoom.hostPlayerName = "Unknown";
            }

            // ����״̬
            if (customProps.TryGetValue(ROOM_HAS_PASSWORD, out object hasPassword))
            {
                lobbyRoom.hasPassword = (bool)hasPassword;
            }

            // ��Ϸģʽ
            if (customProps.TryGetValue(ROOM_GAME_MODE, out object gameMode))
            {
                lobbyRoom.gameMode = gameMode.ToString();
            }

            // �Ѷ�
            if (customProps.TryGetValue(ROOM_DIFFICULTY, out object difficulty))
            {
                lobbyRoom.gameDifficulty = (int)difficulty;
            }

            // ����ʱ��
            if (customProps.TryGetValue(ROOM_CREATE_TIME, out object createTime))
            {
                lobbyRoom.createTime = (float)createTime;
            }

            // ȷ������״̬
            lobbyRoom.status = DetermineRoomStatus(roomInfo);

            return lobbyRoom;
        }

        /// <summary>
        /// ����ת�������б�
        /// </summary>
        public static List<LobbyRoomData> FromPhotonRoomList(List<RoomInfo> roomList)
        {
            var lobbyRooms = new List<LobbyRoomData>();

            if (roomList == null)
                return lobbyRooms;

            foreach (var roomInfo in roomList)
            {
                // ���˵��ѹرջ���Ч�ķ���
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
        /// ȷ������״̬
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

        #region Lobby -> Photon ����ת��

        /// <summary>
        /// ��LobbyRoomDataת��ΪPhoton RoomOptions
        /// </summary>
        public static RoomOptions ToPhotonRoomOptions(LobbyRoomData lobbyRoom, string password = "")
        {
            if (lobbyRoom == null)
            {
                Debug.LogWarning("[PhotonLobbyDataConverter] LobbyRoomDataΪ��");
                return null;
            }

            var roomOptions = new RoomOptions();

            // ��������
            roomOptions.MaxPlayers = (byte)lobbyRoom.maxPlayers;
            roomOptions.IsVisible = true;
            roomOptions.IsOpen = true;

            // �Զ�������
            roomOptions.CustomRoomProperties = CreateCustomProperties(lobbyRoom);

            // ���Զ���������ӵ������ɼ������б�
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
        /// �����Զ��巿������
        /// </summary>
        private static Hashtable CreateCustomProperties(LobbyRoomData lobbyRoom)
        {
            var customProps = new Hashtable();

            // ��������
            customProps[ROOM_GAME_MODE] = lobbyRoom.gameMode;
            customProps[ROOM_DIFFICULTY] = lobbyRoom.gameDifficulty;
            customProps[ROOM_HAS_PASSWORD] = lobbyRoom.hasPassword;
            customProps[ROOM_HOST_NAME] = lobbyRoom.hostPlayerName;

            // ��ȫ���ô���ʱ��
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

        #region ��������

        /// <summary>
        /// ��ɫת�ַ���
        /// </summary>
        private static string ColorToString(Color color)
        {
            return $"{color.r:F2},{color.g:F2},{color.b:F2},{color.a:F2}";
        }

        /// <summary>
        /// ����������ʹ�����PhotonҪ��
        /// </summary>
        public static string CleanRoomNameForPhoton(string roomName)
        {
            if (string.IsNullOrEmpty(roomName))
                return "Room_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);

            // �Ƴ���������ַ�
            roomName = roomName.Replace("\n", "").Replace("\r", "");

            // ���Ƴ���
            if (roomName.Length > 50)
                roomName = roomName.Substring(0, 47) + "...";

            return roomName;
        }

        #endregion
    }
}