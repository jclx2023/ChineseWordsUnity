using UnityEngine;

namespace Lobby.Data
{
    /// <summary>
    /// Lobby��������ģ��
    /// �洢�����ڴ����б�����ʾ����Ϣ
    /// </summary>
    [System.Serializable]
    public class LobbyRoomData
    {
        [Header("������Ϣ")]
        public string roomName = "";
        public string roomId = "";
        public string hostPlayerName = "";

        [Header("��������")]
        public int maxPlayers = 4;
        public int currentPlayers = 0;
        public bool hasPassword = false;
        public string password = "";

        [Header("����״̬")]
        public RoomStatus status = RoomStatus.Waiting;
        public float createTime = 0f;

        [Header("��Ϸ����")]
        public string gameMode = "Classic";
        public int gameDifficulty = 1;

        /// <summary>
        /// ����״̬ö��
        /// </summary>
        public enum RoomStatus
        {
            Waiting,    // �ȴ����
            Full,       // ��������
            InGame,     // ��Ϸ��
            Closed      // ����ر�
        }

        /// <summary>
        /// Ĭ�Ϲ��캯��
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
        /// ���������캯��
        /// </summary>
        public LobbyRoomData(string name, int maxPlayer, string host)
        {
            roomName = name;
            roomId = System.Guid.NewGuid().ToString();
            hostPlayerName = host;
            maxPlayers = maxPlayer;
            currentPlayers = 1; // ������һ�����
            hasPassword = false;
            password = "";
            status = RoomStatus.Waiting;
            createTime = Time.time;
            gameMode = "Classic";
            gameDifficulty = 1;
        }

        /// <summary>
        /// ��鷿���Ƿ���Լ���
        /// </summary>
        public bool CanJoin()
        {
            return status == RoomStatus.Waiting && currentPlayers < maxPlayers;
        }

        /// <summary>
        /// ��鷿���Ƿ�����
        /// </summary>
        public bool IsFull()
        {
            return currentPlayers >= maxPlayers;
        }

        /// <summary>
        /// ��ȡ����״̬�ı�
        /// </summary>
        public string GetStatusText()
        {
            switch (status)
            {
                case RoomStatus.Waiting:
                    return IsFull() ? "��������" : "�ȴ���";
                case RoomStatus.Full:
                    return "��������";
                case RoomStatus.InGame:
                    return "��Ϸ��";
                case RoomStatus.Closed:
                    return "�ѹر�";
                default:
                    return "δ֪״̬";
            }
        }

        /// <summary>
        /// ��ȡ��������ı�
        /// </summary>
        public string GetPlayerCountText()
        {
            return $"{currentPlayers}/{maxPlayers}";
        }

        /// <summary>
        /// ��֤���������Ƿ���Ч
        /// </summary>
        public bool IsRoomNameValid()
        {
            return !string.IsNullOrEmpty(roomName) &&
                   roomName.Length >= 2 &&
                   roomName.Length <= 30;
        }

        /// <summary>
        /// ��֤���������Ƿ���Ч
        /// </summary>
        public bool IsRoomSettingsValid()
        {
            return IsRoomNameValid() &&
                   maxPlayers >= 2 &&
                   maxPlayers <= 8 &&
                   !string.IsNullOrEmpty(hostPlayerName);
        }

        /// <summary>
        /// ��������
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
        /// ���·���״̬
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
        /// ��ȡ������Ϣ
        /// </summary>
        public override string ToString()
        {
            return $"LobbyRoomData[Name: {roomName}, Players: {currentPlayers}/{maxPlayers}, Status: {status}, Host: {hostPlayerName}]";
        }
    }
}