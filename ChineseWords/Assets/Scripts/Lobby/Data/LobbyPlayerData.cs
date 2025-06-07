using UnityEngine;

namespace Lobby.Data
{
    /// <summary>
    /// Lobby�������ģ��
    /// �洢����ڴ����е������Ϣ
    /// </summary>
    [System.Serializable]
    public class LobbyPlayerData
    {
        [Header("������Ϣ")]
        public string playerName = "";
        public string playerId = "";

        [Header("��ʾ����")]
        public int avatarIndex = 0;
        public Color playerColor = Color.white;

        [Header("��Ϸƫ��")]
        public bool autoJoinRoom = false;
        public int preferredMaxPlayers = 4;

        /// <summary>
        /// Ĭ�Ϲ��캯��
        /// </summary>
        public LobbyPlayerData()
        {
            playerName = "";
            playerId = System.Guid.NewGuid().ToString();
            avatarIndex = 0;
            playerColor = Color.white;
            autoJoinRoom = false;
            preferredMaxPlayers = 4;
        }

        /// <summary>
        /// ���������캯��
        /// </summary>
        public LobbyPlayerData(string name)
        {
            playerName = name;
            playerId = System.Guid.NewGuid().ToString();
            avatarIndex = 0;
            playerColor = Color.white;
            autoJoinRoom = false;
            preferredMaxPlayers = 4;
        }

        /// <summary>
        /// ��֤��������Ƿ���Ч
        /// </summary>
        public bool IsPlayerNameValid()
        {
            return !string.IsNullOrEmpty(playerName) &&
                   playerName.Length >= 2 &&
                   playerName.Length <= 20;
        }

        /// <summary>
        /// ��ȡ��ʾ�õ��������
        /// </summary>
        public string GetDisplayName()
        {
            if (IsPlayerNameValid())
            {
                return playerName;
            }
            return "δ�������";
        }

        /// <summary>
        /// ��������
        /// </summary>
        public LobbyPlayerData Clone()
        {
            var clone = new LobbyPlayerData();
            clone.playerName = this.playerName;
            clone.playerId = this.playerId;
            clone.avatarIndex = this.avatarIndex;
            clone.playerColor = this.playerColor;
            clone.autoJoinRoom = this.autoJoinRoom;
            clone.preferredMaxPlayers = this.preferredMaxPlayers;
            return clone;
        }

        /// <summary>
        /// ���浽PlayerPrefs
        /// </summary>
        public void SaveToPlayerPrefs()
        {
            PlayerPrefs.SetString("LobbyPlayer_Name", playerName);
            PlayerPrefs.SetString("LobbyPlayer_Id", playerId);
            PlayerPrefs.SetInt("LobbyPlayer_Avatar", avatarIndex);
            PlayerPrefs.SetFloat("LobbyPlayer_ColorR", playerColor.r);
            PlayerPrefs.SetFloat("LobbyPlayer_ColorG", playerColor.g);
            PlayerPrefs.SetFloat("LobbyPlayer_ColorB", playerColor.b);
            PlayerPrefs.SetInt("LobbyPlayer_AutoJoin", autoJoinRoom ? 1 : 0);
            PlayerPrefs.SetInt("LobbyPlayer_MaxPlayers", preferredMaxPlayers);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// ��PlayerPrefs����
        /// </summary>
        public void LoadFromPlayerPrefs()
        {
            playerName = PlayerPrefs.GetString("LobbyPlayer_Name", "");
            playerId = PlayerPrefs.GetString("LobbyPlayer_Id", System.Guid.NewGuid().ToString());
            avatarIndex = PlayerPrefs.GetInt("LobbyPlayer_Avatar", 0);

            float r = PlayerPrefs.GetFloat("LobbyPlayer_ColorR", 1f);
            float g = PlayerPrefs.GetFloat("LobbyPlayer_ColorG", 1f);
            float b = PlayerPrefs.GetFloat("LobbyPlayer_ColorB", 1f);
            playerColor = new Color(r, g, b, 1f);

            autoJoinRoom = PlayerPrefs.GetInt("LobbyPlayer_AutoJoin", 0) == 1;
            preferredMaxPlayers = PlayerPrefs.GetInt("LobbyPlayer_MaxPlayers", 4);
        }

        /// <summary>
        /// ��ȡ������Ϣ
        /// </summary>
        public override string ToString()
        {
            return $"LobbyPlayerData[Name: {playerName}, ID: {playerId}, Avatar: {avatarIndex}]";
        }
    }
}