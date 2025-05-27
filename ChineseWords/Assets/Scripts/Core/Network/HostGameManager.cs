using UnityEngine;
using Riptide;
using Core.Network;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UI;

namespace Core.Network
{
    /// <summary>
    /// �Ż����Host��Ϸ������
    /// רע����Ϸ���̹�����Ŀ���ݻ�ȡί�и�QuestionDataService
    /// ְ����Ϸ���̿��� + ���״̬���� + ������Ϣ�㲥
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

        // ��ʼ��״̬
        private bool isInitialized = false;
        private bool isWaitingForNetworkManager = false;

        // ��Ϸ״̬
        private Dictionary<ushort, PlayerGameState> playerStates;
        private ushort currentTurnPlayerId;
        private bool gameInProgress;
        private NetworkQuestionData currentQuestion;
        private float gameStartDelay = 2f;

        // ��Ŀ��� - ����ʹ�÷��������ֱ�ӹ���
        private QuestionDataService questionDataService;
        private NetworkQuestionManagerController questionController;

        // ����
        public bool IsGameInProgress => gameInProgress;
        public int PlayerCount => playerStates?.Count ?? 0;
        public ushort CurrentTurnPlayer => currentTurnPlayerId;
        public bool IsInitialized => isInitialized;

        private void Awake()
        {
            LogDebug($"Awake ִ��ʱ��: {Time.time}");

            if (Instance == null)
            {
                Instance = this;
                LogDebug("HostGameManager �����Ѵ���");
            }
            else
            {
                LogDebug("�����ظ���HostGameManagerʵ��");
                return;
            }
        }

        private void Start()
        {
            LogDebug($"Start ִ��ʱ��: {Time.time}");

            // ����Ƿ�Ӧ�ü��ֻ��ȷ������Ϸģʽ�£�
            if (MainMenuManager.SelectedGameMode != MainMenuManager.GameMode.Host)
            {
                LogDebug($"��ǰ��Ϸģʽ: {MainMenuManager.SelectedGameMode}����Hostģʽ������HostGameManager");
                this.enabled = false;
                return;
            }

            // ��ʼ��ʼ������
            StartCoroutine(InitializeHostManagerCoroutine());
        }

        /// <summary>
        /// ��ʼ��������������Э��
        /// </summary>
        private IEnumerator InitializeHostManagerCoroutine()
        {
            LogDebug("��ʼ��ʼ��HostGameManager");

            // 1. �ȴ� NetworkManager ׼������
            yield return StartCoroutine(WaitForNetworkManager());

            // 2. �������״̬�������¼�
            yield return StartCoroutine(CheckNetworkStatusAndSubscribe());

            LogDebug("HostGameManager ��ʼ���������");
        }

        /// <summary>
        /// �ȴ� NetworkManager ʵ��׼������
        /// </summary>
        private IEnumerator WaitForNetworkManager()
        {
            LogDebug("�ȴ� NetworkManager ʵ��׼������...");
            isWaitingForNetworkManager = true;

            int waitFrames = 0;
            const int maxWaitFrames = 300; // 5�볬ʱ��60fps��

            while (NetworkManager.Instance == null && waitFrames < maxWaitFrames)
            {
                yield return null;
                waitFrames++;
            }

            isWaitingForNetworkManager = false;

            if (NetworkManager.Instance == null)
            {
                Debug.LogError("[HostGameManager] �ȴ� NetworkManager ��ʱ������ HostGameManager");
                this.enabled = false;
                yield break;
            }

            LogDebug($"NetworkManager ʵ����׼���������ȴ��� {waitFrames} ֡");
        }

        /// <summary>
        /// �������״̬����������¼�
        /// </summary>
        private IEnumerator CheckNetworkStatusAndSubscribe()
        {
            LogDebug($"NetworkManager ״̬��� - IsHost: {NetworkManager.Instance.IsHost}, IsConnected: {NetworkManager.Instance.IsConnected}");

            if (NetworkManager.Instance.IsHost)
            {
                // �Ѿ���������ֱ�ӳ�ʼ��
                LogDebug("��⵽����Hostģʽ��������ʼ��");
                yield return StartCoroutine(InitializeHostGameCoroutine());
            }
            else
            {
                // �������������������������¼�
                LogDebug("�ȴ����������¼�...");
                SubscribeToNetworkEvents();

                // ���ó�ʱ���
                StartCoroutine(HostStartTimeoutCheck());
            }
        }

        /// <summary>
        /// ���������¼�
        /// </summary>
        private void SubscribeToNetworkEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnHostStarted += OnHostStarted;
                NetworkManager.OnPlayerJoined += OnPlayerJoined;
                NetworkManager.OnPlayerLeft += OnPlayerLeft;
                LogDebug("�Ѷ��������¼�");
            }
        }

        /// <summary>
        /// ȡ�����������¼�
        /// </summary>
        private void UnsubscribeFromNetworkEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnHostStarted -= OnHostStarted;
                NetworkManager.OnPlayerJoined -= OnPlayerJoined;
                NetworkManager.OnPlayerLeft -= OnPlayerLeft;
                LogDebug("��ȡ�����������¼�");
            }
        }

        /// <summary>
        /// ����������ʱ���
        /// </summary>
        private IEnumerator HostStartTimeoutCheck()
        {
            yield return new WaitForSeconds(10f); // 10�볬ʱ

            if (!isInitialized && this.enabled)
            {
                Debug.LogWarning("[HostGameManager] ����������ʱ�����ܲ���Hostģʽ������HostGameManager");
                this.enabled = false;
            }
        }

        /// <summary>
        /// ���������¼�����
        /// </summary>
        private void OnHostStarted()
        {
            LogDebug("�յ����������¼�");

            if (!isInitialized)
            {
                StartCoroutine(InitializeHostGameCoroutine());
            }
        }

        /// <summary>
        /// ��ʼ��������Ϸ��Э��
        /// </summary>
        private IEnumerator InitializeHostGameCoroutine()
        {
            if (isInitialized)
            {
                LogDebug("HostGameManager �Ѿ���ʼ���������ظ���ʼ��");
                yield break;
            }

            LogDebug("��ʼ��ʼ��������Ϸ�߼�");

            // ��ʼ����Ϸ״̬
            playerStates = new Dictionary<ushort, PlayerGameState>();
            gameInProgress = false;
            currentQuestion = null;

            // ��ʼ����������
            yield return StartCoroutine(InitializeServices());

            if (questionDataService == null)
            {
                Debug.LogError("[HostGameManager] QuestionDataService ��ʼ��ʧ��");
                this.enabled = false;
                yield break;
            }

            // ���Host�Ѿ����ӣ��������
            if (NetworkManager.Instance.IsConnected)
            {
                AddPlayer(NetworkManager.Instance.ClientId, "����");
            }

            // ���Ϊ�ѳ�ʼ��
            isInitialized = true;
            LogDebug("������Ϸ��ʼ�����");

            // �����Ϸ��ʼ����
            CheckGameStartConditions();
        }

        /// <summary>
        /// ��ʼ����������
        /// </summary>
        private IEnumerator InitializeServices()
        {
            LogDebug("��ʼ����������...");

            // 1. ��ȡ�򴴽� QuestionDataService
            questionDataService = QuestionDataService.Instance;
            if (questionDataService == null)
            {
                // �ڳ����в���
                questionDataService = FindObjectOfType<QuestionDataService>();

                if (questionDataService == null)
                {
                    // �����µķ���ʵ��
                    GameObject serviceObj = new GameObject("QuestionDataService");
                    questionDataService = serviceObj.AddComponent<QuestionDataService>();
                    LogDebug("�������µ� QuestionDataService ʵ��");
                }
                else
                {
                    LogDebug("�ҵ����е� QuestionDataService ʵ��");
                }
            }

            // 2. ������Ŀ����������
            yield return StartCoroutine(FindQuestionController());

            // 3. Ԥ���س��õ������ṩ�ߣ���ѡ�Ż���
            if (questionDataService != null)
            {
                LogDebug("Ԥ������Ŀ�����ṩ��...");
                questionDataService.PreloadAllProviders();
            }

            LogDebug("����������ʼ�����");
        }

        /// <summary>
        /// ������Ŀ������
        /// </summary>
        private IEnumerator FindQuestionController()
        {
            LogDebug("���� NetworkQuestionManagerController...");

            int attempts = 0;
            const int maxAttempts = 10;

            while (questionController == null && attempts < maxAttempts)
            {
                questionController = FindObjectOfType<NetworkQuestionManagerController>();

                if (questionController == null)
                {
                    LogDebug($"�� {attempts + 1} �β��� NetworkQuestionManagerController ʧ�ܣ���������...");
                    yield return new WaitForSeconds(0.5f);
                    attempts++;
                }
            }

            if (questionController != null)
            {
                LogDebug("NetworkQuestionManagerController �ҵ�");
            }
            else
            {
                Debug.LogError("[HostGameManager] ���� NetworkQuestionManagerController ʧ��");
            }
        }

        /// <summary>
        /// ������
        /// </summary>
        private void AddPlayer(ushort playerId, string playerName = null)
        {
            if (!isInitialized)
            {
                LogDebug($"HostGameManager δ��ʼ�����ӳ������� {playerId}");
                StartCoroutine(DelayedAddPlayer(playerId, playerName));
                return;
            }

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
        /// �ӳ������ң��ȴ���ʼ����ɣ�
        /// </summary>
        private IEnumerator DelayedAddPlayer(ushort playerId, string playerName)
        {
            while (!isInitialized)
            {
                yield return new WaitForSeconds(0.1f);
            }

            AddPlayer(playerId, playerName);
        }

        /// <summary>
        /// �Ƴ����
        /// </summary>
        private void RemovePlayer(ushort playerId)
        {
            if (!isInitialized || !playerStates.ContainsKey(playerId))
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
            if (!isInitialized || gameInProgress || !autoStartGame)
                return;

            // ������Ҫ1����Ҳ��ܿ�ʼ��Ϸ
            if (playerStates.Count >= 1)
            {
                LogDebug("������Ϸ��ʼ������׼����ʼ��Ϸ");
                Invoke(nameof(StartHostGame), gameStartDelay);
            }
        }

        /// <summary>
        /// �ӷ���ϵͳ������Ϸ���·�����
        /// </summary>
        public void StartGameFromRoom()
        {
            if (!isInitialized)
            {
                LogDebug("HostGameManager δ��ʼ�����޷���ʼ��Ϸ");
                return;
            }

            if (gameInProgress)
            {
                LogDebug("��Ϸ�Ѿ��ڽ�����");
                return;
            }

            // �ӷ���ϵͳ��ȡ�����Ϣ
            if (RoomManager.Instance?.CurrentRoom != null)
            {
                SyncPlayersFromRoom();
            }

            LogDebug("�ӷ���ϵͳ������Ϸ");
            StartHostGame();
        }

        /// <summary>
        /// �ӷ���ϵͳͬ�������Ϣ
        /// </summary>
        private void SyncPlayersFromRoom()
        {
            var room = RoomManager.Instance.CurrentRoom;
            if (room == null) return;

            // ����������״̬
            playerStates = new Dictionary<ushort, PlayerGameState>();

            // �ӷ�������������
            foreach (var roomPlayer in room.players.Values)
            {
                playerStates[roomPlayer.playerId] = new PlayerGameState
                {
                    playerId = roomPlayer.playerId,
                    playerName = roomPlayer.playerName,
                    health = initialPlayerHealth,
                    isAlive = true,
                    score = 0,
                    isReady = true // �ܽ�����Ϸ�Ķ���׼���õ�
                };

                LogDebug($"�ӷ���ͬ�����: {roomPlayer.playerName} (ID: {roomPlayer.playerId})");
            }

            LogDebug($"ͬ����ɣ���Ϸ�����: {playerStates.Count}");
        }

        /// <summary>
        /// ���ɲ�������Ŀ���ع��汾��
        /// </summary>
        private void GenerateAndSendQuestion()
        {
            if (!gameInProgress || !isInitialized)
                return;

            LogDebug("��ʼ��������Ŀ");

            // ѡ����Ŀ����
            var questionType = SelectRandomQuestionType();
            LogDebug($"ѡ�����Ŀ����: {questionType}");

            // ʹ�� QuestionDataService ��ȡ��Ŀ����
            NetworkQuestionData question = GetQuestionFromService(questionType);

            if (question != null)
            {
                currentQuestion = question;
                // �㲥��Ŀ�����пͻ���
                BroadcastQuestion(question);
                LogDebug($"��Ŀ�ѷ���: {questionType} - {question.questionText}");
            }
            else
            {
                Debug.LogError($"���� {questionType} ��Ŀʧ�ܣ�������������");
                // ����������Ŀ
                Invoke(nameof(GenerateAndSendQuestion), 1f);
            }
        }

        /// <summary>
        /// �ӷ����ȡ��Ŀ���ݣ��·�����
        /// </summary>
        private NetworkQuestionData GetQuestionFromService(QuestionType questionType)
        {
            if (questionDataService == null)
            {
                Debug.LogError("[HostGameManager] QuestionDataService δ��ʼ��");
                return CreateFallbackQuestion(questionType);
            }

            try
            {
                LogDebug($"ʹ�� QuestionDataService ��ȡ��Ŀ: {questionType}");
                var questionData = questionDataService.GetQuestionData(questionType);

                if (questionData != null)
                {
                    LogDebug($"�ɹ��ӷ����ȡ��Ŀ: {questionData.questionText}");
                    return questionData;
                }
                else
                {
                    LogDebug($"���񷵻ؿ���Ŀ��ʹ�ñ�����Ŀ");
                    return CreateFallbackQuestion(questionType);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"�ӷ����ȡ��Ŀʧ��: {e.Message}");
                return CreateFallbackQuestion(questionType);
            }
        }

        /// <summary>
        /// ����������Ŀ��������ʧ��ʱ��
        /// </summary>
        private NetworkQuestionData CreateFallbackQuestion(QuestionType questionType)
        {
            return NetworkQuestionDataExtensions.CreateFromLocalData(
                questionType,
                $"����һ��{questionType}���͵ı�����Ŀ",
                "���ô�",
                questionType == QuestionType.ExplanationChoice || questionType == QuestionType.SimularWordChoice
                    ? new string[] { "ѡ��A", "���ô�", "ѡ��C", "ѡ��D" }
                    : null,
                questionTimeLimit,
                "{\"source\": \"host_fallback\", \"isDefault\": true}"
            );
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
                { QuestionType.ExplanationChoice, 100f },
                { QuestionType.SimularWordChoice, 1f },
                { QuestionType.TextPinyin, 1f },
                { QuestionType.HardFill, 1f },
                { QuestionType.SoftFill, 1f }
            };
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
            if (!isInitialized)
            {
                LogDebug("HostGameManager δ��ʼ�����޷���ʼ��Ϸ");
                return;
            }

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

            // ���������ӷ�����Ϸ������Ϣ�����пͻ���
            // BroadcastGameEnd(reason);
        }

        /// <summary>
        /// ������Ҵ�
        /// </summary>
        public void HandlePlayerAnswer(ushort playerId, string answer)
        {
            if (!isInitialized || !gameInProgress || playerId != currentTurnPlayerId || currentQuestion == null)
            {
                LogDebug($"��Ч�Ĵ��ύ: playerId={playerId}, currentTurn={currentTurnPlayerId}, gameInProgress={gameInProgress}, initialized={isInitialized}");
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
            if (!isInitialized || NetworkManager.Instance == null)
                return;

            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.SendQuestion);
            message.AddBytes(question.Serialize());
            NetworkManager.Instance.BroadcastMessage(message);
        }

        /// <summary>
        /// �㲥��һغϱ��
        /// </summary>
        private void BroadcastPlayerTurnChanged(ushort playerId)
        {
            if (!isInitialized || NetworkManager.Instance == null)
                return;

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
            if (!isInitialized || NetworkManager.Instance == null)
                return;

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
            if (!isInitialized || NetworkManager.Instance == null)
                return;

            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.AnswerResult);
            message.AddBool(isCorrect);
            message.AddString(correctAnswer);
            NetworkManager.Instance.BroadcastMessage(message);
        }

        #endregion

        #region �����¼�����

        private void OnPlayerJoined(ushort playerId)
        {
            LogDebug($"�����¼�: ��� {playerId} ����");
            AddPlayer(playerId);
        }

        private void OnPlayerLeft(ushort playerId)
        {
            LogDebug($"�����¼�: ��� {playerId} �뿪");
            RemovePlayer(playerId);
        }

        #endregion

        #region �����ӿں�״̬���

        /// <summary>
        /// ǿ�����³�ʼ�������ڵ��ԣ�
        /// </summary>
        [ContextMenu("ǿ�����³�ʼ��")]
        public void ForceReinitialize()
        {
            if (Application.isPlaying)
            {
                LogDebug("ǿ�����³�ʼ��");
                isInitialized = false;
                StartCoroutine(InitializeHostManagerCoroutine());
            }
        }

        /// <summary>
        /// ��ȡ��ǰ״̬��Ϣ�����ڵ��ԣ�
        /// </summary>
        public string GetStatusInfo()
        {
            return $"Initialized: {isInitialized}, " +
                   $"GameInProgress: {gameInProgress}, " +
                   $"PlayerCount: {PlayerCount}, " +
                   $"CurrentTurn: {currentTurnPlayerId}, " +
                   $"NetworkManager: {NetworkManager.Instance != null}, " +
                   $"IsHost: {NetworkManager.Instance?.IsHost}, " +
                   $"QuestionDataService: {questionDataService != null}";
        }

        /// <summary>
        /// ������񻺴�
        /// </summary>
        public void ClearServiceCache()
        {
            if (questionDataService != null)
            {
                questionDataService.ClearCache();
                LogDebug("��������Ŀ���ݷ��񻺴�");
            }
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

        #region Unity ��������

        private void OnDestroy()
        {
            LogDebug("HostGameManager ������");
            UnsubscribeFromNetworkEvents();

            // ������񻺴�
            ClearServiceCache();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && isInitialized)
            {
                LogDebug("Ӧ����ͣ����ͣ��Ϸ�߼�");
            }
            else if (!pauseStatus && isInitialized)
            {
                LogDebug("Ӧ�ûָ����ָ���Ϸ�߼�");
            }
        }

        #endregion
    }

    /// <summary>
    /// �����Ϸ״̬�����ֲ��䣩
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