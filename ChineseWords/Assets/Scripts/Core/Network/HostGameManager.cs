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
    /// ������Ϸ������
    /// ֻ��Hostģʽ�¼��������Ϸ�߼�����
    /// ʹ���¼������ĳ�ʼ����ʽ������ű�ִ��˳������
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

        // ��Ŀ����
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
                DontDestroyOnLoad(gameObject);
                LogDebug("HostGameManager �����Ѵ���");
            }
            else
            {
                LogDebug("�����ظ���HostGameManagerʵ��");
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            LogDebug($"Start ִ��ʱ��: {Time.time}");

            // ����Ƿ�Ӧ�ü������ѡ������Ϸģʽ��
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

            // ������Ŀ������
            yield return StartCoroutine(FindQuestionController());

            if (questionController == null)
            {
                Debug.LogError("[HostGameManager] δ�ҵ�NetworkQuestionManagerController����ʼ��ʧ��");
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
        /// �ж��Ƿ�Ӧ�ü���Host������������ԭ���߼���
        /// </summary>
        private bool ShouldActivateHostManager()
        {
            bool shouldActivate = MainMenuManager.SelectedGameMode == MainMenuManager.GameMode.Host &&
                                 NetworkManager.Instance?.IsHost == true;

            LogDebug($"ShouldActivateHostManager: {shouldActivate} (GameMode: {MainMenuManager.SelectedGameMode}, IsHost: {NetworkManager.Instance?.IsHost})");

            return shouldActivate;
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
        /// �ӷ���ϵͳ������Ϸ��ʼ������������
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
        /// ���ɲ�������Ŀ���Ľ��棩
        /// </summary>
        private void GenerateAndSendQuestion()
        {
            if (!gameInProgress || !isInitialized)
                return;

            LogDebug("��ʼ��������Ŀ");

            // ѡ����Ŀ����
            var questionType = SelectRandomQuestionType();
            LogDebug($"ѡ�����Ŀ����: {questionType}");

            // ���ö�Ӧ�����͹�����������Ŀ
            NetworkQuestionData question = GenerateQuestionByType(questionType);

            if (question != null)
            {
                currentQuestion = question;
                // �㲥��Ŀ�����пͻ���
                BroadcastQuestion(question);
                LogDebug($"��Ŀ�ѷ���: {questionType} - {question.questionText}");
            }
            else
            {
                Debug.LogError($"���� {questionType} ��Ŀʧ��");
                // ����������Ŀ
                Invoke(nameof(GenerateAndSendQuestion), 1f);
            }
        }

        /// <summary>
        /// ������Ŀ����������Ŀ
        /// </summary>
        private NetworkQuestionData GenerateQuestionByType(QuestionType questionType)
        {
            try
            {
                // ���һ򴴽���Ӧ�����͹�����������
                QuestionManagerBase questionManager = GetOrCreateQuestionManager(questionType);

                if (questionManager == null)
                {
                    LogDebug($"�޷����� {questionType} ���͹�����");
                    return CreateDefaultQuestion(questionType);
                }

                // �������͹������ĳ����߼�
                NetworkQuestionData networkQuestion = ExtractQuestionFromManager(questionManager, questionType);

                if (networkQuestion != null)
                {
                    LogDebug($"�ɹ��� {questionType} ��������ȡ��Ŀ: {networkQuestion.questionText}");
                    return networkQuestion;
                }
                else
                {
                    LogDebug($"�� {questionType} ����������ʧ�ܣ�ʹ��Ĭ����Ŀ");
                    return CreateDefaultQuestion(questionType);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"������Ŀʧ��: {e.Message}");
                return CreateDefaultQuestion(questionType);
            }
        }

        /// <summary>
        /// ��ȡ�򴴽����͹�����
        /// </summary>
        private QuestionManagerBase GetOrCreateQuestionManager(QuestionType questionType)
        {
            // �����ڳ����в������еĹ�����
            QuestionManagerBase existingManager = FindQuestionManagerInScene(questionType);
            if (existingManager != null)
            {
                LogDebug($"�ҵ����е� {questionType} ������");
                return existingManager;
            }

            // ���û���ҵ�������һ����ʱ�Ĺ��������ڳ���
            return CreateTemporaryQuestionManager(questionType);
        }

        /// <summary>
        /// �ڳ����в������е����͹�����
        /// </summary>
        private QuestionManagerBase FindQuestionManagerInScene(QuestionType questionType)
        {
            switch (questionType)
            {
                case QuestionType.ExplanationChoice:
                    return FindObjectOfType<GameLogic.Choice.ExplanationChoiceQuestionManager>();
                case QuestionType.HardFill:
                    return FindObjectOfType<GameLogic.FillBlank.HardFillQuestionManager>();
                case QuestionType.SoftFill:
                    return FindObjectOfType<GameLogic.FillBlank.SoftFillQuestionManager>();
                case QuestionType.TextPinyin:
                    return FindObjectOfType<GameLogic.FillBlank.TextPinyinQuestionManager>();
                case QuestionType.SimularWordChoice:
                    return FindObjectOfType<GameLogic.Choice.SimularWordChoiceQuestionManager>();
                // ����������͹�����
                default:
                    return null;
            }
        }

        /// <summary>
        /// ������ʱ�����͹��������ڳ���
        /// </summary>
        private QuestionManagerBase CreateTemporaryQuestionManager(QuestionType questionType)
        {
            try
            {
                GameObject tempManagerObj = new GameObject($"Temp_{questionType}_Manager");
                tempManagerObj.transform.SetParent(this.transform); // ����ΪHostGameManager���Ӷ���

                QuestionManagerBase manager = null;

                switch (questionType)
                {
                    case QuestionType.ExplanationChoice:
                        manager = tempManagerObj.AddComponent<GameLogic.Choice.ExplanationChoiceQuestionManager>();
                        break;
                    case QuestionType.HardFill:
                        manager = tempManagerObj.AddComponent<GameLogic.FillBlank.HardFillQuestionManager>();
                        break;
                    case QuestionType.SoftFill:
                        manager = tempManagerObj.AddComponent<GameLogic.FillBlank.SoftFillQuestionManager>();
                        break;
                    case QuestionType.TextPinyin:
                        manager = tempManagerObj.AddComponent<GameLogic.FillBlank.TextPinyinQuestionManager>();
                        break;
                    case QuestionType.SimularWordChoice:
                        manager = tempManagerObj.AddComponent<GameLogic.Choice.SimularWordChoiceQuestionManager>();
                        break;
                    // �����������
                    default:
                        Destroy(tempManagerObj);
                        return null;
                }

                if (manager != null)
                {
                    LogDebug($"������ʱ {questionType} �������ɹ�");
                }

                return manager;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"������ʱ {questionType} ������ʧ��: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// �����͹�������ȡ��Ŀ����
        /// </summary>
        private NetworkQuestionData ExtractQuestionFromManager(QuestionManagerBase manager, QuestionType questionType)
        {
            try
            {
                // ���������Ƿ�ʵ����IQuestionDataProvider�ӿ�
                if (manager is IQuestionDataProvider dataProvider)
                {
                    LogDebug($"ʹ��IQuestionDataProvider�ӿڴ� {manager.GetType().Name} ��ȡ��Ŀ");
                    NetworkQuestionData questionData = dataProvider.GetQuestionData();

                    if (questionData != null)
                    {
                        LogDebug($"�ɹ���ȡ��Ŀ: {questionData.questionText}");
                        return questionData;
                    }
                    else
                    {
                        LogDebug($"�� {manager.GetType().Name} ��ȡ��Ŀ����Ϊ��");
                    }
                }
                else
                {
                    // �����������û��ʵ�ֽӿڣ�ʹ�÷����������ʽ����ʱ������
                    LogDebug($"{manager.GetType().Name} ��δʵ��IQuestionDataProvider�ӿڣ�ʹ����ʱ����");
                    return ExtractQuestionUsingReflection(manager, questionType);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"�ӹ�������ȡ��Ŀʧ��: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// ʹ�÷��䷽ʽ��ȡ��Ŀ����ʱ������
        /// ����Ϊ�˼��ݻ�û��ʵ��IQuestionDataProvider�ӿڵĹ�����
        /// </summary>
        private NetworkQuestionData ExtractQuestionUsingReflection(QuestionManagerBase manager, QuestionType questionType)
        {
            // �������ͨ��������ù�������˽�з�������ȡ��Ŀ����
            // ���ߴ���һ���򵥵�ģ����Ŀ

            LogDebug($"Ϊ {questionType} ����ģ����Ŀ����");

            // ��ʱ����ģ�����ݣ����龡��Ϊ����������ʵ��IQuestionDataProvider�ӿ�
            switch (questionType)
            {
                case QuestionType.ExplanationChoice:
                    return NetworkQuestionDataExtensions.CreateFromLocalData(
                        QuestionType.ExplanationChoice,
                        "�����ĸ��ǻ�Ȼ�������ȷ���ͣ�������ExplanationChoiceQuestionManager��",
                        "ͻȻ�������׹���",
                        new string[] { "ͻȻ�������׹���", "�е��ǳ�����", "�����úܺ�", "���ú�����" },
                        questionTimeLimit,
                        "{\"source\": \"reflection_fallback\"}"
                    );

                case QuestionType.HardFill:
                    return NetworkQuestionDataExtensions.CreateFromLocalData(
                        QuestionType.HardFill,
                        "ɽ��ˮ������·��___��___��һ�壨����HardFillQuestionManager��",
                        "��������",
                        null,
                        questionTimeLimit,
                        "{\"source\": \"reflection_fallback\", \"blanks\": 2}"
                    );

                // ����������͵���ʱ����
                default:
                    return NetworkQuestionDataExtensions.CreateFromLocalData(
                        questionType,
                        $"����һ��{questionType}���͵Ĳ�����Ŀ",
                        "���Դ�",
                        questionType == QuestionType.ExplanationChoice || questionType == QuestionType.SimularWordChoice
                            ? new string[] { "ѡ��A", "���Դ�", "ѡ��C", "ѡ��D" }
                            : null,
                        questionTimeLimit,
                        "{\"source\": \"reflection_fallback\"}"
                    );
            }
        }

        /// <summary>
        /// ����Ĭ����Ŀ�����÷�����
        /// </summary>
        private NetworkQuestionData CreateDefaultQuestion(QuestionType questionType)
        {
            return new NetworkQuestionData
            {
                questionType = questionType,
                questionText = $"Ĭ��{questionType}��Ŀ������һ��������Ŀ",
                correctAnswer = "���Դ�",
                options = questionType == QuestionType.ExplanationChoice || questionType == QuestionType.SimularWordChoice
                    ? new string[] { "ѡ��A", "���Դ�", "ѡ��C", "ѡ��D" }
                    : new string[0],
                timeLimit = questionTimeLimit,
                additionalData = "{\"isDefault\": true}"
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

            // ������Է�����Ϸ������Ϣ�����пͻ���
            // BroadcastGameEnd(reason);
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
                { QuestionType.HardFill, 1f }
            };
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
                   $"IsHost: {NetworkManager.Instance?.IsHost}";
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