using System;

namespace Core.Network
{
    /// <summary>
    /// 网络题目数据结构
    /// 统一所有题目类型的网络传输格式
    /// </summary>
    [Serializable]
    public class NetworkQuestionData
    {
        public QuestionType questionType;
        public string questionText;
        public string[] options;        // 选择题选项，填空题可为空
        public string correctAnswer;
        public float timeLimit;
        public string additionalData;   // 存储特殊题目的额外数据(JSON格式)

        public NetworkQuestionData()
        {
            options = new string[0];
            timeLimit = 30f; // 默认30秒
        }

        /// <summary>
        /// 序列化为字节数组用于网络传输
        /// </summary>
        public byte[] Serialize()
        {
            string json = UnityEngine.JsonUtility.ToJson(this);
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        /// <summary>
        /// 从字节数组反序列化
        /// </summary>
        public static NetworkQuestionData Deserialize(byte[] data)
        {
            if (data == null || data.Length == 0)
                return null;

            try
            {
                string json = System.Text.Encoding.UTF8.GetString(data);
                return UnityEngine.JsonUtility.FromJson<NetworkQuestionData>(json);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"反序列化题目数据失败: {e.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// 玩家信息
    /// </summary>
    [Serializable]
    public class NetworkPlayerInfo
    {
        public ushort playerId;
        public string playerName;
        public int health;
        public bool isMyTurn;
        public bool isConnected;

        public NetworkPlayerInfo()
        {
            health = 100;
            isMyTurn = false;
            isConnected = true;
            playerName = "Player";
        }
    }

    /// <summary>
    /// 游戏状态信息
    /// </summary>
    [Serializable]
    public class NetworkGameState
    {
        public bool isGameStarted;
        public ushort currentPlayerId;
        public int totalPlayers;
        public float questionTimeRemaining;
        public bool isWaitingForAnswer;

        public NetworkGameState()
        {
            isGameStarted = false;
            currentPlayerId = 0;
            totalPlayers = 0;
            questionTimeRemaining = 0f;
            isWaitingForAnswer = false;
        }
    }
}