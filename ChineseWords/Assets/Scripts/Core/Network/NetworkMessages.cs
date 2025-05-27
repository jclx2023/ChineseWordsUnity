using System;

namespace Core.Network
{
    /// <summary>
    /// ������Ϣ���Ͷ���
    /// �ͻ��˺ͷ���˶���Ҫʹ����ͬ����ϢID
    /// </summary>
    public enum NetworkMessageType : ushort
    {
        // �������
        PlayerJoined = 1,
        PlayerLeft = 2,

        // ��Ϸ����
        RequestQuestion = 10,
        SendQuestion = 11,
        SubmitAnswer = 12,
        AnswerResult = 13,

        // ״̬ͬ��
        HealthUpdate = 20,
        PlayerTurnChanged = 21,
        GameStateSync = 22,

        // ��Ϸ����
        GameStart = 30,
        EndGame = 31,
        RestartGame = 32,

        RoomDataSync = 201,           // Host -> Client: ͬ��������������
        PlayerJoinRoom = 202,         // Server -> All: ��Ҽ��뷿��֪ͨ
        PlayerLeaveRoom = 203,        // Server -> All: ����뿪����֪ͨ
        PlayerReadyRequest = 204,     // Client -> Host: �ͻ�������ı�׼��״̬
        PlayerReadyUpdate = 205,      // Host -> All: �㲥���׼��״̬�仯
        GameStartRequest = 206,       // Host -> All: ����������Ϸ
        RoomInfoRequest = 207,        // Client -> Host: ���󷿼���Ϣ
    }

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