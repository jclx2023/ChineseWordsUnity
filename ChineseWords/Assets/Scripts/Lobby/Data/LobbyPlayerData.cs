using UnityEngine;

namespace Lobby.Data
{
    /// <summary>
    /// Lobby玩家数据模型
    /// 存储玩家在大厅中的相关信息
    /// </summary>
    [System.Serializable]
    public class LobbyPlayerData
    {
        [Header("基础信息")]
        public string playerName = "";
        public string playerId = "";

        [Header("显示设置")]
        public int avatarIndex = 0;
        public Color playerColor = Color.white;

        [Header("游戏偏好")]
        public bool autoJoinRoom = false;
        public int preferredMaxPlayers = 4;

        /// <summary>
        /// 默认构造函数
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
        /// 带参数构造函数
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
        /// 验证玩家名称是否有效
        /// </summary>
        public bool IsPlayerNameValid()
        {
            return !string.IsNullOrEmpty(playerName) &&
                   playerName.Length >= 2 &&
                   playerName.Length <= 20;
        }

        /// <summary>
        /// 获取显示用的玩家名称
        /// </summary>
        public string GetDisplayName()
        {
            if (IsPlayerNameValid())
            {
                return playerName;
            }
            return "未命名玩家";
        }

        /// <summary>
        /// 创建副本
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
        /// 保存到PlayerPrefs
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
        /// 从PlayerPrefs加载
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
        /// 获取调试信息
        /// </summary>
        public override string ToString()
        {
            return $"LobbyPlayerData[Name: {playerName}, ID: {playerId}, Avatar: {avatarIndex}]";
        }
    }
}