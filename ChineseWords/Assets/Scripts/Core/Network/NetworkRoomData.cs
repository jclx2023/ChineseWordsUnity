using Riptide;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Network
{
    /// <summary>
    /// ���緿������ - ֧��Riptide���л�
    /// </summary>
    public static class NetworkRoomData
    {
        /// <summary>
        /// ���л��������ݵ���Ϣ
        /// </summary>
        public static void SerializeRoomData(this Message message, RoomData roomData)
        {
            message.AddString(roomData.roomName);
            message.AddString(roomData.roomCode);
            message.AddByte((byte)roomData.state);
            message.AddUShort(roomData.hostId);
            message.AddInt(roomData.maxPlayers);
            message.AddFloat(roomData.createTime);

            // ���л��������
            message.AddInt(roomData.players.Count);
            foreach (var player in roomData.players.Values)
            {
                SerializePlayer(message, player);
            }
        }

        /// <summary>
        /// ����Ϣ�����л���������
        /// </summary>
        public static RoomData DeserializeRoomData(this Message message)
        {
            string roomName = message.GetString();
            string roomCode = message.GetString();
            RoomState state = (RoomState)message.GetByte();
            ushort hostId = message.GetUShort();
            int maxPlayers = message.GetInt();
            float createTime = message.GetFloat();

            // ������������
            RoomData roomData = new RoomData(roomName, roomCode, hostId, maxPlayers);
            roomData.state = state;
            roomData.createTime = createTime;

            // �����л��������
            int playerCount = message.GetInt();
            for (int i = 0; i < playerCount; i++)
            {
                RoomPlayer player = DeserializePlayer(message);
                roomData.players[player.playerId] = player;
            }

            return roomData;
        }

        /// <summary>
        /// ���л������������
        /// </summary>
        public static void SerializePlayer(this Message message, RoomPlayer player)
        {
            message.AddUShort(player.playerId);
            message.AddString(player.playerName);
            message.AddByte((byte)player.state);
            message.AddBool(player.isHost);
            message.AddFloat(player.joinTime);
        }

        /// <summary>
        /// �����л������������
        /// </summary>
        public static RoomPlayer DeserializePlayer(this Message message)
        {
            ushort playerId = message.GetUShort();
            string playerName = message.GetString();
            PlayerRoomState state = (PlayerRoomState)message.GetByte();
            bool isHost = message.GetBool();
            float joinTime = message.GetFloat();

            RoomPlayer player = new RoomPlayer(playerId, playerName, isHost);
            player.state = state;
            player.joinTime = joinTime;

            return player;
        }

        /// <summary>
        /// ���л�׼��״̬�仯
        /// </summary>
        public static void SerializeReadyChange(this Message message, ushort playerId, bool isReady)
        {
            message.AddUShort(playerId);
            message.AddBool(isReady);
        }

        /// <summary>
        /// �����л�׼��״̬�仯
        /// </summary>
        public static (ushort playerId, bool isReady) DeserializeReadyChange(this Message message)
        {
            ushort playerId = message.GetUShort();
            bool isReady = message.GetBool();
            return (playerId, isReady);
        }
    }
}