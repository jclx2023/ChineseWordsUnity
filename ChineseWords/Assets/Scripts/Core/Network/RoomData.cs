using System.Collections.Generic;
using UnityEngine;

namespace Core.Network
{
    /// <summary>
    /// ����״̬ö��
    /// </summary>
    public enum RoomState
    {
        Waiting,    // �ȴ���Ҽ���
        Ready,      // ׼����ʼ�����������׼����
        Starting,   // ����������Ϸ
        InGame,     // ��Ϸ������
        Ended       // ��Ϸ����
    }

    /// <summary>
    /// ��ҷ���״̬
    /// </summary>
    public enum PlayerRoomState
    {
        Connected,  // �����ӣ�δ׼��
        Ready,      // ��׼��
        InGame,     // ��Ϸ��
        Disconnected // �Ѷ���
    }

    /// <summary>
    /// �����������Ϣ
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
    /// ��������
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
        /// �����ҵ�����
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
        /// �Ƴ����
        /// </summary>
        public bool RemovePlayer(ushort playerId)
        {
            return players.Remove(playerId);
        }

        /// <summary>
        /// �������׼��״̬
        /// </summary>
        public bool SetPlayerReady(ushort playerId, bool ready)
        {
            if (!players.ContainsKey(playerId))
                return false;

            players[playerId].state = ready ? PlayerRoomState.Ready : PlayerRoomState.Connected;
            return true;
        }

        /// <summary>
        /// ����Ƿ�������Ҷ���׼������������Ҫ׼����
        /// </summary>
        public bool AreAllPlayersReady()
        {
            if (players.Count < 2) // ������Ҫ2�����
                return false;

            foreach (var player in players.Values)
            {
                // ��������Ҫ׼�����������
                if (player.isHost)
                    continue;

                // ������ұ���׼��
                if (player.state != PlayerRoomState.Ready)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// ��ȡ׼�������������������������
        /// </summary>
        public int GetReadyPlayerCount()
        {
            int count = 0;
            foreach (var player in players.Values)
            {
                // ����������׼��������
                if (player.isHost)
                    continue;

                if (player.state == PlayerRoomState.Ready)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// ��ȡ��Ҫ׼�������������������������
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