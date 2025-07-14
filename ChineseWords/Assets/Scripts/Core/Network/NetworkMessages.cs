using System;

namespace Core.Network
{
    /// <summary>
    /// ������Ŀ���ݽṹ
    /// ͳһ������Ŀ���͵����紫���ʽ
    /// </summary>
    [Serializable]
    public class NetworkQuestionData
    {
        public QuestionType questionType;
        public string questionText;
        public string[] options;        // ѡ����ѡ�������Ϊ��
        public string correctAnswer;
        public float timeLimit;
        public string additionalData;   // �洢������Ŀ�Ķ�������(JSON��ʽ)

        public NetworkQuestionData()
        {
            options = new string[0];
            timeLimit = 30f; // Ĭ��30��
        }

        /// <summary>
        /// ���л�Ϊ�ֽ������������紫��
        /// </summary>
        public byte[] Serialize()
        {
            string json = UnityEngine.JsonUtility.ToJson(this);
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        /// <summary>
        /// ���ֽ����鷴���л�
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
                UnityEngine.Debug.LogError($"�����л���Ŀ����ʧ��: {e.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// �����Ϣ
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
    /// ��Ϸ״̬��Ϣ
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