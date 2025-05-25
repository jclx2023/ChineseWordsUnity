using UnityEngine;
using Riptide;
using Core.Network;
using System.Collections.Generic;
using System.Linq;

namespace Core.Network
{
    /// <summary>
    /// ��������Ϸ�߼�������
    /// ������Ŀ���ɡ�����֤���غϹ������״̬����
    /// ֻ��Hostģʽ�¼���
    /// </summary>
    public class HostGameManager : MonoBehaviour
    {
        [Header("��Ϸ����")]
        [SerializeField] private float questionTimeLimit = 30f;
        [SerializeField] private int initialPlayerHealth = 100;
        [SerializeField] private int damagePerWrongAnswer = 20;

        public static HostGameManager Instance { get; private set; }

        // ��Ϸ״̬
        private Dictionary<ushort, PlayerGameState> playerStates;
        private ushort currentTurnPlayerId;
        private bool gameInProgress;
        private NetworkQuestionData currentQuestion;

        // ��Ŀ���ɣ����������߼���
        private NetworkQuestionManagerController questionController;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // ֻ��Hostģʽ�³�ʼ��
            if (NetworkManager.Instance?.IsHost == true)
            {
                InitializeHostGame();
            }
        }

        /// <summary>
        /// ��ʼ��������Ϸ
        /// </summary>
        private void InitializeHostGame()
        {
            Debug.Log("��ʼ��������Ϸ�߼�");

            playerStates = new Dictionary<ushort, PlayerGameState>();
            questionController = FindObjectOfType<NetworkQuestionManagerController>();

            // ע�������¼�
            NetworkManager.OnPlayerJoined += OnPlayerJoined;
            NetworkManager.OnPlayerLeft += OnPlayerLeft;

            // ע����Ϣ������
            RegisterMessageHandlers();

            // ��������Լ�Ϊ���
            if (NetworkManager.Instance.IsClient)
            {
                AddPlayer(NetworkManager.Instance.ClientId);
            }
        }

        /// <summary>
        /// ע����Ϣ������
        /// </summary>
        private void RegisterMessageHandlers()
        {
            // ��Щ������Ҫ��ӵ�EnhancedNetworkManager�򵥶�����Ϣ��������
        }

        /// <summary>
        /// ������
        /// </summary>
        private void AddPlayer(ushort playerId)
        {
            if (!playerStates.ContainsKey(playerId))
            {
                playerStates[playerId] = new PlayerGameState
                {
                    playerId = playerId,
                    health = initialPlayerHealth,
                    isAlive = true,
                    score = 0
                };

                Debug.Log($"�����ҵ���Ϸ: {playerId}, ��ǰ�����: {playerStates.Count}");

                // ����ǵ�һ���������Ϸδ��ʼ����ʼ��Ϸ
                if (playerStates.Count == 1 && !gameInProgress)
                {
                    StartHostGame();
                }
            }
        }

        /// <summary>
        /// �Ƴ����
        /// </summary>
        private void RemovePlayer(ushort playerId)
        {
            if (playerStates.ContainsKey(playerId))
            {
                playerStates.Remove(playerId);
                Debug.Log($"�Ƴ����: {playerId}, ʣ�������: {playerStates.Count}");

                // �����ǰ�غϵ�����뿪���л�����һ�����
                if (currentTurnPlayerId == playerId)
                {
                    NextPlayerTurn();
                }

                // ���û������ˣ�ֹͣ��Ϸ
                if (playerStates.Count == 0)
                {
                    StopHostGame();
                }
            }
        }

        /// <summary>
        /// ��ʼ������Ϸ
        /// </summary>
        private void StartHostGame()
        {
            Debug.Log("������ʼ��Ϸ");
            gameInProgress = true;

            // ѡ���һ����ҿ�ʼ
            currentTurnPlayerId = playerStates.Keys.FirstOrDefault();
            BroadcastPlayerTurnChanged(currentTurnPlayerId);

            // ���ɵ�һ��
            GenerateAndSendQuestion();
        }

        /// <summary>
        /// ֹͣ������Ϸ
        /// </summary>
        private void StopHostGame()
        {
            Debug.Log("����ֹͣ��Ϸ");
            gameInProgress = false;
            currentQuestion = null;
        }

        /// <summary>
        /// ���ɲ�������Ŀ
        /// </summary>
        private void GenerateAndSendQuestion()
        {
            if (!gameInProgress)
                return;

            Debug.Log("��������Ŀ");

            // ʹ�����е���Ŀ�����߼�
            var questionType = SelectRandomQuestionType();
            currentQuestion = GenerateQuestionData(questionType);

            if (currentQuestion != null)
            {
                // �㲥��Ŀ�����пͻ���
                BroadcastQuestion(currentQuestion);
                Debug.Log($"��Ŀ�ѷ���: {questionType}");
            }
            else
            {
                Debug.LogError("��Ŀ����ʧ��");
            }
        }

        /// <summary>
        /// ѡ�������Ŀ����
        /// </summary>
        private QuestionType SelectRandomQuestionType()
        {
            // ����NetworkQuestionManagerController��Ȩ���߼�
            var weights = questionController?.TypeWeights ?? new Dictionary<QuestionType, float>();

            if (weights.Count == 0)
            {
                // Ĭ��Ȩ��
                return QuestionType.ExplanationChoice;
            }

            float total = weights.Values.Sum();
            float random = Random.value * total;
            float accumulator = 0f;

            foreach (var pair in weights)
            {
                accumulator += pair.Value;
                if (random <= accumulator)
                    return pair.Key;
            }

            return weights.Keys.First();
        }

        /// <summary>
        /// ������Ŀ���ݣ��򻯰棬ʵ����Ҫ�������ݿ⣩
        /// </summary>
        private NetworkQuestionData GenerateQuestionData(QuestionType questionType)
        {
            // ����Ӧ�ø�����Ŀ���ʹ����ݿ�������ʵ����Ŀ
            // Ϊ����ʾ������һ��ʾ����Ŀ
            var questionData = new NetworkQuestionData
            {
                questionType = questionType,
                questionText = $"ʾ��{questionType}��Ŀ",
                correctAnswer = "ʾ����",
                options = new string[] { "ѡ��A", "ѡ��B", "ѡ��C", "ѡ��D" },
                timeLimit = questionTimeLimit
            };

            return questionData;
        }

        /// <summary>
        /// �㲥��Ŀ
        /// </summary>
        private void BroadcastQuestion(NetworkQuestionData question)
        {
            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.SendQuestion);
            message.AddBytes(question.Serialize());
            NetworkManager.Instance.BroadcastMessage(message);
        }

        /// <summary>
        /// �㲥��һغϱ��
        /// </summary>
        private void BroadcastPlayerTurnChanged(ushort playerId)
        {
            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.PlayerTurnChanged);
            message.AddUShort(playerId);
            NetworkManager.Instance.BroadcastMessage(message);

            Debug.Log($"�㲥�غϱ��: �ֵ���� {playerId}");
        }

        /// <summary>
        /// �㲥Ѫ������
        /// </summary>
        private void BroadcastHealthUpdate(ushort playerId, int newHealth)
        {
            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.HealthUpdate);
            message.AddUShort(playerId);
            message.AddInt(newHealth);
            NetworkManager.Instance.BroadcastMessage(message);
        }

        /// <summary>
        /// ������Ҵ�
        /// </summary>
        public void HandlePlayerAnswer(ushort playerId, string answer)
        {
            if (!gameInProgress || playerId != currentTurnPlayerId || currentQuestion == null)
            {
                Debug.LogWarning($"��Ч�Ĵ��ύ: playerId={playerId}, currentTurn={currentTurnPlayerId}");
                return;
            }

            Debug.Log($"������� {playerId} �Ĵ�: {answer}");

            // ��֤��
            bool isCorrect = ValidateAnswer(answer, currentQuestion);

            // �������״̬
            if (playerStates.ContainsKey(playerId))
            {
                var playerState = playerStates[playerId];

                if (!isCorrect)
                {
                    playerState.health -= damagePerWrongAnswer;
                    if (playerState.health <= 0)
                    {
                        playerState.health = 0;
                        playerState.isAlive = false;
                    }

                    // �㲥Ѫ������
                    BroadcastHealthUpdate(playerId, playerState.health);
                }
                else
                {
                    playerState.score += 10; // ��Ե÷�
                }
            }

            // �㲥������
            BroadcastAnswerResult(isCorrect, currentQuestion.correctAnswer);

            // �л�����һ�����
            NextPlayerTurn();
        }

        /// <summary>
        /// ��֤��
        /// </summary>
        private bool ValidateAnswer(string answer, NetworkQuestionData question)
        {
            if (question == null || string.IsNullOrEmpty(answer))
                return false;

            // �򵥵��ַ����Ƚϣ�ʵ�ʿ�����Ҫ�����ӵ��߼�
            return answer.Trim().Equals(question.correctAnswer.Trim(), System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// �㲥������
        /// </summary>
        private void BroadcastAnswerResult(bool isCorrect, string correctAnswer)
        {
            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.AnswerResult);
            message.AddBool(isCorrect);
            message.AddString(correctAnswer);
            NetworkManager.Instance.BroadcastMessage(message);
        }

        /// <summary>
        /// �л�����һ�����
        /// </summary>
        private void NextPlayerTurn()
        {
            var alivePlayers = playerStates.Where(p => p.Value.isAlive).Select(p => p.Key).ToList();

            if (alivePlayers.Count == 0)
            {
                Debug.Log("��Ϸ������û�д������");
                StopHostGame();
                return;
            }

            if (alivePlayers.Count == 1)
            {
                Debug.Log($"��Ϸ��������� {alivePlayers[0]} ��ʤ");
                // ������Է�����Ϸ������Ϣ
                return;
            }

            // �ҵ���һ���������
            int currentIndex = alivePlayers.IndexOf(currentTurnPlayerId);
            int nextIndex = (currentIndex + 1) % alivePlayers.Count;
            currentTurnPlayerId = alivePlayers[nextIndex];

            // �㲥�غϱ������������Ŀ
            BroadcastPlayerTurnChanged(currentTurnPlayerId);

            // �����ӳٺ���������Ŀ
            Invoke(nameof(GenerateAndSendQuestion), 2f);
        }

        #region �����¼�����

        private void OnPlayerJoined(ushort playerId)
        {
            AddPlayer(playerId);
        }

        private void OnPlayerLeft(ushort playerId)
        {
            RemovePlayer(playerId);
        }

        #endregion

        private void OnDestroy()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerJoined -= OnPlayerJoined;
                NetworkManager.OnPlayerLeft -= OnPlayerLeft;
            }
        }
    }

    /// <summary>
    /// �����Ϸ״̬
    /// </summary>
    [System.Serializable]
    public class PlayerGameState
    {
        public ushort playerId;
        public int health;
        public bool isAlive;
        public int score;
        public string playerName;
    }
}