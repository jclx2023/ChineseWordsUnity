using UnityEngine;
using Riptide;
using Riptide.Utils;
using Core.Network;
using System;

namespace Core.Network
{
    /// <summary>
    /// �ͻ������������
    /// ���������˵����Ӻ���Ϣ�շ�
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        [Header("��������")]
        [SerializeField] private string serverIP = "127.0.0.1";
        [SerializeField] private ushort port = 7777;
        [SerializeField] private ushort timeoutTime = 5000;

        public static NetworkManager Instance { get; private set; }

        private Client client;

        // �¼�
        public static event Action OnConnected;
        public static event Action OnDisconnected;
        public static event Action<NetworkQuestionData> OnQuestionReceived;
        public static event Action<bool, string> OnAnswerResultReceived;
        public static event Action<ushort, int> OnHealthUpdated;
        public static event Action<ushort> OnPlayerTurnChanged;

        // ����
        public bool IsConnected => client?.IsConnected ?? false;
        public ushort ClientId => client?.Id ?? 0;

        private void Awake()
        {
            // ����ģʽ
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeClient();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeClient()
        {
            // ����Riptide��־����
            RiptideLogger.Initialize(Debug.Log, Debug.Log, Debug.LogWarning, Debug.LogError, false);

            // �����ͻ���
            client = new Client();

            // �������¼�
            client.Connected += OnClientConnected;
            client.Disconnected += OnClientDisconnected;
            client.ConnectionFailed += OnClientConnectionFailed;
        }

        private void Update()
        {
            // ÿ֡����������Ϣ
            client?.Update();
        }

        private void OnApplicationQuit()
        {
            Disconnect();
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        /// <summary>
        /// ���ӵ�������
        /// </summary>
        public void Connect(string ip = null, ushort? serverPort = null)
        {
            if (IsConnected)
            {
                Debug.LogWarning("�Ѿ����ӵ���������");
                return;
            }

            string targetIP = ip ?? serverIP;
            ushort targetPort = serverPort ?? port;

            Debug.Log($"�������ӷ����� {targetIP}:{targetPort}...");
            client.Connect($"{targetIP}:{targetPort}", timeoutTime);
        }

        /// <summary>
        /// �Ͽ�����
        /// </summary>
        public void Disconnect()
        {
            if (client != null)
            {
                if (IsConnected)
                {
                    Debug.Log("�Ͽ�����������");
                }

                client.Disconnect();
                client = null;
            }
        }

        /// <summary>
        /// ������Ϣ��������
        /// </summary>
        public void SendMessage(Message message)
        {
            if (IsConnected)
            {
                client.Send(message);
            }
            else
            {
                Debug.LogWarning("δ���ӵ����������޷�������Ϣ");
            }
        }

        /// <summary>
        /// ������Ŀ
        /// </summary>
        public void RequestQuestion()
        {
            if (!IsConnected)
            {
                Debug.LogWarning("δ���ӵ�������");
                return;
            }

            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.RequestQuestion);
            SendMessage(message);
            Debug.Log("������Ŀ...");
        }

        /// <summary>
        /// �ύ��
        /// </summary>
        public void SubmitAnswer(string answer)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("δ���ӵ�������");
                return;
            }

            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.SubmitAnswer);
            message.AddString(answer);
            SendMessage(message);
            Debug.Log($"�ύ��: {answer}");
        }

        #region �����¼�����

        private void OnClientConnected(object sender, EventArgs e)
        {
            Debug.Log($"�ɹ����ӵ�������! �ͻ���ID: {ClientId}");
            OnConnected?.Invoke();
        }

        private void OnClientDisconnected(object sender, EventArgs e)
        {
            Debug.Log("��������Ͽ�����");
            OnDisconnected?.Invoke();
        }

        private void OnClientConnectionFailed(object sender, EventArgs e)
        {
            Debug.LogError("���ӷ�����ʧ��");
        }

        #endregion

        #region ��Ϣ������ - ע����շ������Ϣ

        [MessageHandler((ushort)NetworkMessageType.SendQuestion)]
        private static void HandleQuestionReceived(Message message)
        {
            byte[] questionData = message.GetBytes();
            NetworkQuestionData question = NetworkQuestionData.Deserialize(questionData);

            if (question != null)
            {
                Debug.Log($"�յ���Ŀ: {question.questionType} - {question.questionText}");
                OnQuestionReceived?.Invoke(question);
            }
            else
            {
                Debug.LogError("��Ŀ���ݽ���ʧ��");
            }
        }

        [MessageHandler((ushort)NetworkMessageType.AnswerResult)]
        private static void HandleAnswerResult(Message message)
        {
            bool isCorrect = message.GetBool();
            string correctAnswer = message.GetString();

            Debug.Log($"������: {(isCorrect ? "��ȷ" : "����")} - ��ȷ��: {correctAnswer}");
            OnAnswerResultReceived?.Invoke(isCorrect, correctAnswer);
        }

        [MessageHandler((ushort)NetworkMessageType.HealthUpdate)]
        private static void HandleHealthUpdate(Message message)
        {
            ushort playerId = message.GetUShort();
            int newHealth = message.GetInt();

            Debug.Log($"��� {playerId} Ѫ������: {newHealth}");
            OnHealthUpdated?.Invoke(playerId, newHealth);
        }

        [MessageHandler((ushort)NetworkMessageType.PlayerTurnChanged)]
        private static void HandlePlayerTurnChanged(Message message)
        {
            ushort currentPlayerId = message.GetUShort();

            Debug.Log($"�ֵ���� {currentPlayerId} ����");
            OnPlayerTurnChanged?.Invoke(currentPlayerId);
        }

        #endregion
    }
}