using UnityEngine;
using Riptide;
using Core.Network;
using System.Collections.Generic;
using System.Linq;
using UI;

namespace Core.Network
{
    /// <summary>
    /// ������Ϸ������
    /// ֻ��Hostģʽ�¼��������Ϸ�߼�����
    /// </summary>
    public class HostGameManager : MonoBehaviour
    {
        [Header("��Ϸ����")]
        [SerializeField] private float questionTimeLimit = 30f;
        [SerializeField] private int initialPlayerHealth = 100;
        [SerializeField] private int damagePerWrongAnswer = 20;
        [SerializeField] private bool autoStartGame = true;

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        public static HostGameManager Instance { get; private set; }

        // ��Ϸ״̬
        private Dictionary<ushort, PlayerGameState> playerStates;
        private ushort currentTurnPlayerId;
        private bool gameInProgress;
        private NetworkQuestionData currentQuestion;
        private float gameStartDelay = 2f;

        // ��Ŀ����
        private NetworkQuestionManagerController questionController;

        // ����
        public bool IsGameInProgress => gameInProgress;
        public int PlayerCount => playerStates?.Count ?? 0;
        public ushort CurrentTurnPlayer => currentTurnPlayerId;

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
            if (ShouldActivateHostManager())
            {
                InitializeHostGame();
            }
            else
            {
                // ����Hostģʽ���������
                this.enabled = false;
                LogDebug("��Hostģʽ������HostGameManager");
            }
        }

        /// <summary>
        /// �ж��Ƿ�Ӧ�ü���Host������
        /// </summary>
        private bool ShouldActivateHostManager()
        {
            return MainMenuManager.SelectedGameMode == MainMenuManager.GameMode.Host &&
                   NetworkManager.Instance?.IsHost == true;
        }

        /// <summary>
        /// ��ʼ��������Ϸ
        /// </summary>
        private void InitializeHostGame()
        {
            LogDebug("��ʼ��������Ϸ�߼�");

            playerStates = new Dictionary<ushort, PlayerGameState>();
            questionController = FindObjectOfType<NetworkQuestionManagerController>();

            if (questionController == null)
            {
                Debug.LogError("δ�ҵ�NetworkQuestionManagerController��");
                return;
            }

            // ע�������¼�
            RegisterNetworkEvents();

            // ���Host�Ѿ����ӣ��������
            if (NetworkManager.Instance.IsClient)
            {
                AddPlayer(NetworkManager.Instance.ClientId, "����");
            }

            LogDebug("������Ϸ��ʼ�����");
        }

        /// <summary>
        /// ע�������¼�
        /// </summary>
        private void RegisterNetworkEvents()
        {
            NetworkManager.OnPlayerJoined += OnPlayerJoined;
            NetworkManager.OnPlayerLeft += OnPlayerLeft;
        }

        /// <summary>
        /// ע�������¼�
        /// </summary>
        private void UnregisterNetworkEvents()
        {
            NetworkManager.OnPlayerJoined -= OnPlayerJoined;
            NetworkManager.OnPlayerLeft -= OnPlayerLeft;
        }

        /// <summary>
        /// ������
        /// </summary>
        private void AddPlayer(ushort playerId, string playerName = null)
        {
            if (playerStates.ContainsKey(playerId))
            {
                LogDebug($"��� {playerId} �Ѵ��ڣ��������");
                return;
            }

            playerStates[playerId] = new PlayerGameState
            {
                playerId = playerId,
                playerName = playerName ?? $"���{playerId}",
                health = initialPlayerHealth,
                isAlive = true,
                score = 0,
                isReady = false
            };

            LogDebug($"������: {playerId} ({playerName}), ��ǰ�����: {playerStates.Count}");

            // ����Ƿ���Կ�ʼ��Ϸ
            CheckGameStartConditions();
        }

        /// <summary>
        /// �Ƴ����
        /// </summary>
        private void RemovePlayer(ushort playerId)
        {
            if (!playerStates.ContainsKey(playerId))
                return;

            string playerName = playerStates[playerId].playerName;
            playerStates.Remove(playerId);

            LogDebug($"�Ƴ����: {playerId} ({playerName}), ʣ�������: {playerStates.Count}");

            // �����ǰ�غϵ�����뿪���л�����һ�����
            if (currentTurnPlayerId == playerId && gameInProgress)
            {
                NextPlayerTurn();
            }

            // �����Ϸ�Ƿ�Ӧ�ý���
            CheckGameEndConditions();
        }

        /// <summary>
        /// �����Ϸ��ʼ����
        /// </summary>
        private void CheckGameStartConditions()
        {
            if (gameInProgress || !autoStartGame)
                return;

            // ������Ҫ1����Ҳ��ܿ�ʼ��Ϸ
            if (playerStates.Count >= 1)
            {
                LogDebug("������Ϸ��ʼ������׼����ʼ��Ϸ");
                Invoke(nameof(StartHostGame), gameStartDelay);
            }
        }

        /// <summary>
        /// �����Ϸ��������
        /// </summary>
        private void CheckGameEndConditions()
        {
            if (!gameInProgress)
                return;

            var alivePlayers = playerStates.Where(p => p.Value.isAlive).ToList();

            if (alivePlayers.Count == 0)
            {
                LogDebug("��Ϸ������û�д������");
                EndGame("��Ϸ������������Ҷ�����̭");
            }
            else if (alivePlayers.Count == 1)
            {
                var winner = alivePlayers.First();
                LogDebug($"��Ϸ��������� {winner.Key} ��ʤ");
                EndGame($"��Ϸ������{winner.Value.playerName} ��ʤ��");
            }
            else if (playerStates.Count == 0)
            {
                LogDebug("��Ϸ������û�����");
                EndGame("��Ϸ������������Ҷ��뿪��");
            }
        }

        /// <summary>
        /// ��ʼ������Ϸ
        /// </summary>
        public void StartHostGame()
        {
            if (gameInProgress)
            {
                LogDebug("��Ϸ�Ѿ��ڽ�����");
                return;
            }

            LogDebug("������ʼ��Ϸ");
            gameInProgress = true;

            // ѡ���һ����ҿ�ʼ
            var firstPlayer = playerStates.Keys.FirstOrDefault();
            if (firstPlayer != 0)
            {
                currentTurnPlayerId = firstPlayer;
                BroadcastPlayerTurnChanged(currentTurnPlayerId);

                // ���ɵ�һ��
                Invoke(nameof(GenerateAndSendQuestion), 1f);
            }
            else
            {
                Debug.LogError("û����ҿ��Կ�ʼ��Ϸ");
                gameInProgress = false;
            }
        }

        /// <summary>
        /// ������Ϸ
        /// </summary>
        public void EndGame(string reason = "��Ϸ����")
        {
            if (!gameInProgress)
                return;

            LogDebug($"������Ϸ: {reason}");
            gameInProgress = false;
            currentQuestion = null;

            // ������Է�����Ϸ������Ϣ�����пͻ���
            // BroadcastGameEnd(reason);
        }

        /// <summary>
        /// ���ɲ�������Ŀ
        /// </summary>
        private void GenerateAndSendQuestion()
        {
            if (!gameInProgress)
                return;

            LogDebug("��������Ŀ");

            // ʹ�����е���Ŀ�����߼����򻯰���ʾ��
            var questionType = SelectRandomQuestionType();
            currentQuestion = CreateSampleQuestion(questionType);

            if (currentQuestion != null)
            {
                // �㲥��Ŀ�����пͻ���
                BroadcastQuestion(currentQuestion);
                LogDebug($"��Ŀ�ѷ���: {questionType}");
            }
            else
            {
                Debug.LogError("��Ŀ����ʧ��");
                // �������Ի�����
                Invoke(nameof(GenerateAndSendQuestion), 1f);
            }
        }

        /// <summary>
        /// ѡ�������Ŀ����
        /// </summary>
        private QuestionType SelectRandomQuestionType()
        {
            // ����NetworkQuestionManagerController��Ȩ���߼�
            var weights = questionController?.TypeWeights ?? GetDefaultTypeWeights();

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
        /// ��ȡĬ����Ŀ����Ȩ��
        /// </summary>
        private Dictionary<QuestionType, float> GetDefaultTypeWeights()
        {
            return new Dictionary<QuestionType, float>
            {
                { QuestionType.ExplanationChoice, 1f },
                { QuestionType.SimularWordChoice, 1f },
                { QuestionType.TextPinyin, 1f },
                { QuestionType.HardFill, 1f }
            };
        }

        /// <summary>
        /// ����ʾ����Ŀ����ʱʵ�֣�ʵ��Ӧ���������ݿ⣩
        /// </summary>
        private NetworkQuestionData CreateSampleQuestion(QuestionType questionType)
        {
            return new NetworkQuestionData
            {
                questionType = questionType,
                questionText = $"ʾ��{questionType}��Ŀ����ѡ����ȷ��",
                correctAnswer = "��ȷ��",
                options = new string[] { "ѡ��A", "��ȷ��", "ѡ��C", "ѡ��D" },
                timeLimit = questionTimeLimit,
                additionalData = "{\"sampleData\": true}"
            };
        }

        /// <summary>
        /// ������Ҵ�
        /// </summary>
        public void HandlePlayerAnswer(ushort playerId, string answer)
        {
            if (!gameInProgress || playerId != currentTurnPlayerId || currentQuestion == null)
            {
                LogDebug($"��Ч�Ĵ��ύ: playerId={playerId}, currentTurn={currentTurnPlayerId}, gameInProgress={gameInProgress}");
                return;
            }

            LogDebug($"������� {playerId} �Ĵ�: {answer}");

            // ��֤��
            bool isCorrect = ValidateAnswer(answer, currentQuestion);

            // �������״̬
            UpdatePlayerState(playerId, isCorrect);

            // �㲥������
            BroadcastAnswerResult(isCorrect, currentQuestion.correctAnswer);

            // �����Ϸ�Ƿ����
            CheckGameEndConditions();

            // �����Ϸ���ڽ��У��л�����һ�����
            if (gameInProgress)
            {
                Invoke(nameof(NextPlayerTurn), 2f);
            }
        }

        /// <summary>
        /// �������״̬
        /// </summary>
        private void UpdatePlayerState(ushort playerId, bool isCorrect)
        {
            if (!playerStates.ContainsKey(playerId))
                return;

            var playerState = playerStates[playerId];

            if (isCorrect)
            {
                playerState.score += 10; // ��Ե÷�
                LogDebug($"��� {playerId} ��ԣ��÷�: {playerState.score}");
            }
            else
            {
                playerState.health -= damagePerWrongAnswer;
                if (playerState.health <= 0)
                {
                    playerState.health = 0;
                    playerState.isAlive = false;
                    LogDebug($"��� {playerId} ����̭");
                }

                // �㲥Ѫ������
                BroadcastHealthUpdate(playerId, playerState.health);
                LogDebug($"��� {playerId} ���Ѫ��: {playerState.health}");
            }
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
        /// �л�����һ�����
        /// </summary>
        private void NextPlayerTurn()
        {
            var alivePlayers = playerStates.Where(p => p.Value.isAlive).Select(p => p.Key).ToList();

            if (alivePlayers.Count == 0)
            {
                LogDebug("û�д�����ң���Ϸ����");
                EndGame("û�д������");
                return;
            }

            if (alivePlayers.Count == 1)
            {
                var winner = playerStates[alivePlayers[0]];
                LogDebug($"��Ϸ������{winner.playerName} ��ʤ");
                EndGame($"{winner.playerName} ��ʤ��");
                return;
            }

            // �ҵ���һ���������
            int currentIndex = alivePlayers.IndexOf(currentTurnPlayerId);
            if (currentIndex == -1)
            {
                // ��ǰ��Ҳ��ڴ���б��У�ѡ���һ���������
                currentTurnPlayerId = alivePlayers[0];
            }
            else
            {
                // ѡ����һ���������
                int nextIndex = (currentIndex + 1) % alivePlayers.Count;
                currentTurnPlayerId = alivePlayers[nextIndex];
            }

            // �㲥�غϱ������������Ŀ
            BroadcastPlayerTurnChanged(currentTurnPlayerId);

            // �����ӳٺ���������Ŀ
            Invoke(nameof(GenerateAndSendQuestion), 1f);
        }

        #region ������Ϣ�㲥

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

            LogDebug($"�㲥�غϱ��: �ֵ���� {playerId}");
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
        /// �㲥������
        /// </summary>
        private void BroadcastAnswerResult(bool isCorrect, string correctAnswer)
        {
            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.AnswerResult);
            message.AddBool(isCorrect);
            message.AddString(correctAnswer);
            NetworkManager.Instance.BroadcastMessage(message);
        }

        #endregion

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

        #region ��������

        /// <summary>
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[HostGameManager] {message}");
            }
        }

        #endregion

        private void OnDestroy()
        {
            UnregisterNetworkEvents();
        }
    }

    /// <summary>
    /// �����Ϸ״̬����չ�棩
    /// </summary>
    [System.Serializable]
    public class PlayerGameState
    {
        public ushort playerId;
        public string playerName;
        public int health;
        public bool isAlive;
        public int score;
        public bool isReady;
        public float lastActiveTime;

        public PlayerGameState()
        {
            lastActiveTime = Time.time;
        }
    }
}