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
    /// �޸�����ͬ�������Host��Ϸ������
    /// ��Ҫ�޸���1. �ӷ���ϵͳ��ȷͬ��������� 2. ����Ϸ��ʼ�߼�
    /// </summary>
    public class HostGameManager : MonoBehaviour
    {
        [Header("��Ϸ����")]
        [SerializeField] private float questionTimeLimit = 30f;
        [SerializeField] private int initialPlayerHealth = 100;
        [SerializeField] private int damagePerWrongAnswer = 20;
        [SerializeField] private bool autoStartGame = true;

        [Header("˫��ܹ�����")]
        [SerializeField] private bool isDualLayerArchitecture = true;
        [SerializeField] private bool isServerLayerOnly = false; // �Ƿ�ֻ��Ϊ������������

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        // ˫��ܹ����
        private NetworkManager serverNetworkManager;    // ID=0, ��������
        private NetworkManager playerHostNetworkManager; // ID=1, ���Host

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
                Destroy(gameObject);
                return;
            }

            // ����Ƿ�Ϊ˫��ܹ�
            CheckDualLayerArchitecture();
        }

        /// <summary>
        /// ���˫��ܹ�����
        /// </summary>
        private void CheckDualLayerArchitecture()
        {
            if (!isDualLayerArchitecture)
            {
                LogDebug("ʹ�ô�ͳ����ܹ�");
                return;
            }

            // ��鵱ǰ�Ƿ�Ϊ��������
            var serverMarker = GetComponent<ServerLayerMarker>();
            var playerHostMarker = GetComponent<PlayerHostLayerMarker>();

            if (serverMarker != null)
            {
                isServerLayerOnly = true;
                LogDebug("��ǰHostGameManager�����ڷ������� (ID=0)");
            }
            else if (playerHostMarker != null)
            {
                isServerLayerOnly = false;
                LogDebug("��ǰHostGameManager���������Host�� (ID=1)");
            }
            else
            {
                // ����ͨ��NetworkManager�ж�
                var localNetworkManager = GetComponentInParent<NetworkManager>() ?? FindObjectOfType<NetworkManager>();
                if (localNetworkManager != null)
                {
                    isServerLayerOnly = (localNetworkManager.ClientId == 0);
                    LogDebug($"ͨ��NetworkManager ClientId�жϲ㼶: {(isServerLayerOnly ? "��������" : "��Ҳ�")}");
                }
            }

            // ����˫��ܹ��е�NetworkManagerʵ��
            FindDualLayerNetworkManagers();
        }

        /// <summary>
        /// ����˫��ܹ��е�NetworkManagerʵ��
        /// </summary>
        private void FindDualLayerNetworkManagers()
        {
            var allNetworkManagers = FindObjectsOfType<NetworkManager>();

            foreach (var nm in allNetworkManagers)
            {
                if (nm.ClientId == 0)
                {
                    serverNetworkManager = nm;
                    LogDebug($"�ҵ���������NetworkManager: ClientId={nm.ClientId}");
                }
                else if (nm.ClientId == 1)
                {
                    playerHostNetworkManager = nm;
                    LogDebug($"�ҵ����Host��NetworkManager: ClientId={nm.ClientId}");
                }
            }

            if (isDualLayerArchitecture)
            {
                LogDebug($"˫��ܹ�״̬ - ��������: {serverNetworkManager != null}, ���Host��: {playerHostNetworkManager != null}");
            }
        }

        private void Start()
        {
            LogDebug($"[HostGameManager]Start ִ��ʱ��: {Time.time}");

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
        /// ��ʼ��������Ϸ��Э�̣��޸��ظ��������⣩
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

            // ��¼NetworkManager�ĵ�ǰ״̬
            if (NetworkManager.Instance != null)
            {
                LogDebug($"NetworkManager״̬ - ClientId: {NetworkManager.Instance.ClientId}, IsHost: {NetworkManager.Instance.IsHost}, IsConnected: {NetworkManager.Instance.IsConnected}");
            }

            // �ؼ��޸����ӷ���ϵͳͬ���������
            SyncPlayersFromRoomSystem();

            // ���Ϊ�ѳ�ʼ������ͬ����Һ�������ǣ����������¼��ظ���ӣ�
            isInitialized = true;
            LogDebug($"������Ϸ��ʼ����ɣ���ǰ�����: {playerStates.Count}");

            // �޸�����ȫ�Ƴ��Զ����Host���߼�
            // ����ϵͳ�Ѿ�����������������ݣ�����Ҫ�������
            LogDebug("����Host�Զ���ӣ���ȫ������������");

            // ��֤��������
            ValidateHostCount();

            // �򻯿�ʼ�߼����ӷ������ľ�ֱ�ӿ�ʼ
            if (autoStartGame && playerStates.Count > 0)
            {
                LogDebug("�ӷ���ϵͳ���룬ֱ�ӿ�ʼ��Ϸ");
                Invoke(nameof(StartHostGame), gameStartDelay);
            }
        }

        /// <summary>
        /// �ӷ���ϵͳͬ��������ݣ��޸��ظ��������⣩
        /// </summary>
        private void SyncPlayersFromRoomSystem()
        {
            LogDebug("ͬ������ϵͳ�������");

            // ����������״̬�������ظ�
            playerStates.Clear();

            // ����1����RoomManager��ȡ
            if (RoomManager.Instance?.CurrentRoom != null)
            {
                var room = RoomManager.Instance.CurrentRoom;
                LogDebug($"��RoomManagerͬ�������������: {room.players.Count}");

                foreach (var roomPlayer in room.players.Values)
                {
                    AddPlayerFromRoom(roomPlayer.playerId, roomPlayer.playerName);
                }

                LogDebug($"�������ͬ����ɣ��ܼ������: {playerStates.Count}");

                // ��֤Host����
                ValidateHostCount();
                return;
            }

            // ����2����NetworkManager������״̬��ȡ
            if (NetworkManager.Instance != null)
            {
                LogDebug($"��NetworkManagerͬ�������������: {NetworkManager.Instance.ConnectedPlayerCount}");

                // **�ؼ��޸���ʹ��ͳһ��Host���ID�ӿ�**
                ushort hostPlayerId = NetworkManager.Instance.GetHostPlayerId();
                if (hostPlayerId != 0 && NetworkManager.Instance.IsHostClientReady)
                {
                    AddPlayerFromRoom(hostPlayerId, "����");
                    LogDebug($"���Host���: ID={hostPlayerId}");
                }
                else
                {
                    LogDebug($"Host�����δ׼������: ID={hostPlayerId}, Ready={NetworkManager.Instance.IsHostClientReady}");
                }
            }

            LogDebug($"���ͬ����ɣ��ܼ������: {playerStates.Count}");
            ValidateHostCount();
        }

        /// <summary>
        /// ��֤���������������ã�
        /// </summary>
        private void ValidateHostCount()
        {
            var hostPlayers = playerStates.Values.Where(p => p.playerName.Contains("����")).ToList();

            if (hostPlayers.Count > 1)
            {
                Debug.LogError($"[HostGameManager] ��⵽�������: {hostPlayers.Count} ��");

                foreach (var host in hostPlayers)
                {
                    LogDebug($"�ظ�����: ID={host.playerId}, Name={host.playerName}");
                }

                // �޸��ظ�Host
                FixDuplicateHosts();
            }
            else if (hostPlayers.Count == 1)
            {
                LogDebug($"Host��֤ͨ��: ID={hostPlayers[0].playerId}");

                // **��֤��NetworkManager��һ����**
                if (NetworkManager.Instance != null)
                {
                    ushort networkHostId = NetworkManager.Instance.GetHostPlayerId();
                    if (hostPlayers[0].playerId != networkHostId)
                    {
                        Debug.LogError($"[HostGameManager] Host ID��һ�£���Ϸ��: {hostPlayers[0].playerId}, NetworkManager: {networkHostId}");
                    }
                }
            }
            else
            {
                Debug.LogWarning("[HostGameManager] û���ҵ�����");
            }
        }


        /// <summary>
        /// �޸��ظ��������⣨��ǿ�棩
        /// </summary>
        private void FixDuplicateHosts()
        {
            if (NetworkManager.Instance == null) return;

            ushort correctHostId = NetworkManager.Instance.GetHostPlayerId();
            LogDebug($"�޸��ظ���������ȷ��Host ID: {correctHostId}");

            // ��ȡ���з������
            var hostPlayers = playerStates.Where(p => p.Value.playerName.Contains("����")).ToList();

            if (hostPlayers.Count <= 1)
            {
                LogDebug("�������������������޸�");
                return;
            }

            LogDebug($"���� {hostPlayers.Count} ����������ʼ�޸�");

            // ������ȷID�ķ������Ƴ�������
            var correctHost = hostPlayers.FirstOrDefault(h => h.Key == correctHostId);

            if (correctHost.Value != null)
            {
                LogDebug($"������ȷ����: ID={correctHost.Key}, Name={correctHost.Value.playerName}");

                // �Ƴ���������
                var duplicateHosts = hostPlayers.Where(h => h.Key != correctHostId).ToList();
                foreach (var duplicateHost in duplicateHosts)
                {
                    playerStates.Remove(duplicateHost.Key);
                    LogDebug($"�Ƴ��ظ�����: ID={duplicateHost.Key}, Name={duplicateHost.Value.playerName}");
                }
            }
            else
            {
                // ���û���ҵ���ȷID�ķ�����������СID�ķ���
                var primaryHost = hostPlayers.OrderBy(h => h.Key).First();
                var duplicateHosts = hostPlayers.Skip(1).ToList();

                LogDebug($"����������: ID={primaryHost.Key}, �Ƴ��ظ�����: {string.Join(", ", duplicateHosts.Select(h => h.Key))}");

                foreach (var duplicateHost in duplicateHosts)
                {
                    playerStates.Remove(duplicateHost.Key);
                    LogDebug($"�Ƴ��ظ�����: ID={duplicateHost.Key}, Name={duplicateHost.Value.playerName}");
                }
            }

            // ������֤
            LogDebug($"�޸���ɣ���ǰ�����: {playerStates.Count}");
            ValidateHostCount();
        }

        /// <summary>
        /// �ӷ������������ң��޸��汾��
        /// </summary>
        private void AddPlayerFromRoom(ushort playerId, string playerName)
        {
            if (playerStates.ContainsKey(playerId))
            {
                LogDebug($"��� {playerId} �Ѵ��ڣ��������");
                return;
            }

            // **ʹ��NetworkManager��ͳһ�ӿ��ж��Ƿ�ΪHost**
            bool isHostPlayer = NetworkManager.Instance?.IsHostPlayer(playerId) ?? false;

            playerStates[playerId] = new PlayerGameState
            {
                playerId = playerId,
                playerName = isHostPlayer ? "����" : playerName,
                health = initialPlayerHealth,
                isAlive = true,
                isReady = true // �ӷ������Ķ���׼���õ�
            };

            LogDebug($"�ӷ���������: {playerName} (ID: {playerId}, IsHost: {isHostPlayer})");
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
        /// �����ң���ֹ�ظ���ӣ�
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
                LogDebug($"��� {playerId} �Ѵ��ڣ������ظ����");
                return;
            }

            playerStates[playerId] = new PlayerGameState
            {
                playerId = playerId,
                playerName = playerName ?? $"���{playerId}",
                health = initialPlayerHealth,
                isAlive = true,
                isReady = false
            };

            LogDebug($"������: {playerId} ({playerName}), ��ǰ�����: {playerStates.Count}");
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
        /// �ӷ���ϵͳ������Ϸ���ⲿ���ýӿڣ�
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

            LogDebug("�ӷ���ϵͳ������Ϸ - ׼����Ϸ����");


            PrepareGameData();

            LogDebug("HostGameManager��Ϸ����׼����ɣ��ȴ������л�");
        }
        /// <summary>
        /// ׼����Ϸ���ݣ�����������
        /// </summary>
        private void PrepareGameData()
        {
            LogDebug("׼����Ϸ����...");

            // ͬ������ϵͳ���������
            SyncPlayersFromRoomSystem();

            // Ԥ������Ŀ����
            if (questionDataService != null)
            {
                LogDebug("1Ԥ������Ŀ�����ṩ��...");
                questionDataService.PreloadAllProviders();
            }

            // ��֤��Ҫ���
            if (!ValidateGameComponents())
            {
                Debug.LogError("[HostGameManager] ��Ϸ�����֤ʧ��");
                return;
            }

            LogDebug("��Ϸ����׼�����");
        }
        /// <summary>
        /// ��֤��Ϸ���������������
        /// </summary>
        private bool ValidateGameComponents()
        {
            bool isValid = true;

            if (questionDataService == null)
            {
                Debug.LogError("QuestionDataService δ��ʼ��");
                isValid = false;
            }

            if (NetworkManager.Instance == null)
            {
                Debug.LogError("NetworkManager ʵ��������");
                isValid = false;
            }

            if (playerStates == null || playerStates.Count == 0)
            {
                Debug.LogError("�������Ϊ��");
                isValid = false;
            }

            return isValid;
        }

        /// <summary>
        /// ���ɲ�������Ŀ���޸���ʹ����ȷ��NQMC�ӿڣ�
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

                // �㲥��Ŀ�����пͻ��ˣ�����Host�Լ���
                BroadcastQuestion(question);
                LogDebug($"��Ŀ�ѷ���: {questionType} - {question.questionText}");

                // Host��Ҳ��ͨ�������¼����յ��Լ����͵���Ŀ
                // ������֤Host��Clientʹ����ͬ�Ľ���·��
            }
            else
            {
                Debug.LogError($"���� {questionType} ��Ŀʧ�ܣ�������������");
                // ����������Ŀ
                Invoke(nameof(GenerateAndSendQuestion), 1f);
            }
        }

        /// <summary>
        /// �ӷ����ȡ��Ŀ����
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
                { QuestionType.IdiomChain, 1f },
                { QuestionType.TextPinyin, 1f },
                { QuestionType.HardFill, 1f },
                { QuestionType.SoftFill, 1f },
                { QuestionType.SentimentTorF, 1f },
                { QuestionType.SimularWordChoice, 1f },
                { QuestionType.UsageTorF, 1f },
                { QuestionType.ExplanationChoice, 1f },
            };
        }

        /// <summary>
        /// �����Ϸ�����������򻯰汾��
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
        /// ��ʼ������Ϸ���޸���ȷ��NQMCҲ������
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

            LogDebug($"������ʼ��Ϸ�������: {playerStates.Count}");

            // ����Ƿ������
            if (playerStates.Count == 0)
            {
                Debug.LogError("û����ҿ��Կ�ʼ��Ϸ - ����б�Ϊ��");
                return;
            }

            gameInProgress = true;

            // **�ؼ��޸ģ�ȷ��NQMCҲ����������Ϸģʽ**
            if (NetworkQuestionManagerController.Instance != null)
            {
                LogDebug("����NQMC������Ϸģʽ");
                NetworkQuestionManagerController.Instance.StartGame(true); // ����ģʽ
            }
            else
            {
                Debug.LogError("NetworkQuestionManagerController.InstanceΪ�գ��޷�������Ŀ����");
            }

            // ѡ���һ��������ҿ�ʼ���޸�ID=0�����⣩
            var alivePlayers = playerStates.Where(p => p.Value.isAlive).ToList();
            if (alivePlayers.Count > 0)
            {
                currentTurnPlayerId = alivePlayers.First().Key;
                LogDebug($"ѡ����� {currentTurnPlayerId} ({playerStates[currentTurnPlayerId].playerName}) ��ʼ��Ϸ");

                BroadcastPlayerTurnChanged(currentTurnPlayerId);

                // ���ɵ�һ��
                Invoke(nameof(GenerateAndSendQuestion), 1f);
            }
            else
            {
                Debug.LogError("û�д�����ҿ��Կ�ʼ��Ϸ");
                gameInProgress = false;
            }
        }
        /// <summary>
        /// ����µĹ��������������л���ɺ�ĳ�ʼ��
        /// </summary>
        public void OnGameSceneLoaded()
        {
            LogDebug("��Ϸ����������ɣ�������Ϸ�߼�");

            if (isInitialized && !gameInProgress)
            {
                // �ӳ�������ȷ������������Ѿ���
                Invoke(nameof(StartHostGame), 1f);
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
            if (!isInitialized || !gameInProgress || currentQuestion == null)
            {
                LogDebug($"��Ч�Ĵ��ύ״̬: initialized={isInitialized}, gameInProgress={gameInProgress}, hasQuestion={currentQuestion != null}");
                return;
            }

            // **ʹ��ͳһ�ӿ���֤������**
            if (!playerStates.ContainsKey(playerId))
            {
                LogDebug($"δ֪����ύ��: {playerId}");
                return;
            }

            // ����Ƿ��ֵ���ǰ���
            if (playerId != currentTurnPlayerId)
            {
                LogDebug($"���ǵ�ǰ��ҵĻغ�: �ύ��={playerId}, ��ǰ�غ�={currentTurnPlayerId}");
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
        /// �������״̬���򻯰汾��
        /// </summary>
        private void UpdatePlayerState(ushort playerId, bool isCorrect)
        {
            if (!playerStates.ContainsKey(playerId))
                return;

            var playerState = playerStates[playerId];

            if (isCorrect)
            {
                LogDebug($"��� {playerId} �����");
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
                else
                {
                    LogDebug($"��� {playerId} ���Ѫ��: {playerState.health}");
                }

                // �㲥Ѫ������
                BroadcastHealthUpdate(playerId, playerState.health);
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
        /// �㲥��Ŀ��˫��ܹ����䣩
        /// </summary>
        private void BroadcastQuestion(NetworkQuestionData question)
        {
            if (!isInitialized || NetworkManager.Instance == null)
            {
                Debug.LogError("[HostGameManager] �޷��㲥��Ŀ��δ��ʼ����NetworkManagerΪ��");
                return;
            }

            LogDebug($"׼���㲥��Ŀ: {question.questionType} - {question.questionText}");

            if (isDualLayerArchitecture && isServerLayerOnly)
            {
                // ˫��ܹ�����������㲥��Ŀ
                BroadcastQuestionDualLayer(question);
            }
            else
            {
                // ��ͳ�ܹ���ֱ�ӹ㲥
                BroadcastQuestionTraditional(question);
            }
        }

        /// <summary>
        /// ˫��ܹ�����Ŀ�㲥
        /// </summary>
        private void BroadcastQuestionDualLayer(NetworkQuestionData question)
        {
            LogDebug("˫��ܹ�����������㲥��Ŀ");

            // 1. ͨ������㲥�������ⲿ�ͻ���
            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.SendQuestion);
            message.AddBytes(question.Serialize());

            // ʹ�÷��������NetworkManager�㲥
            if (serverNetworkManager != null)
            {
                serverNetworkManager.BroadcastMessage(message);
                LogDebug("��Ŀ��ͨ����������㲥���ⲿ�ͻ���");
            }

            // 2. ͨ��˫��ܹ����������͸��������Host��
            if (DualLayerArchitectureManager.Instance != null)
            {
                DualLayerArchitectureManager.Instance.ServerToPlayerHostQuestion(question);
                LogDebug("��Ŀ�ѷ��͸��������Host��");
            }
            else
            {
                Debug.LogError("˫��ܹ������������ڣ��޷�������Ŀ�����Host��");
            }
        }

        /// <summary>
        /// ��ͳ�ܹ�����Ŀ�㲥
        /// </summary>
        private void BroadcastQuestionTraditional(NetworkQuestionData question)
        {
            LogDebug("��ͳ�ܹ���ֱ�ӹ㲥��Ŀ");

            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.SendQuestion);
            message.AddBytes(question.Serialize());
            NetworkManager.Instance.BroadcastMessage(message);

            LogDebug("��Ŀ��ͨ������㲥");

            // ȷ��HostҲ���յ���Ŀ
            if (NetworkManager.Instance.IsHost)
            {
                TriggerHostQuestionReceive(NetworkQuestionManagerController.Instance, question);
            }
        }

        /// <summary>
        /// ����Host����Ŀ���գ�ͨ������򹫹��ӿڣ�
        /// </summary>
        private void TriggerHostQuestionReceive(NetworkQuestionManagerController nqmc, NetworkQuestionData question)
        {
            try
            {
                LogDebug("���Դ���Host����Ŀ����");

                // ����1������Ƿ��й�����������ֱ�ӵ���
                var publicMethod = typeof(NetworkQuestionManagerController).GetMethod("ReceiveNetworkQuestion");
                if (publicMethod != null)
                {
                    LogDebug("ͨ���������� ReceiveNetworkQuestion ����");
                    publicMethod.Invoke(nqmc, new object[] { question });
                    return;
                }

                // ����2��ͨ���������˽�з���
                var privateMethod = typeof(NetworkQuestionManagerController).GetMethod("OnNetworkQuestionReceived",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (privateMethod != null)
                {
                    LogDebug("ͨ��������� OnNetworkQuestionReceived");
                    privateMethod.Invoke(nqmc, new object[] { question });
                }
                else
                {
                    Debug.LogError("�޷��ҵ����ʵķ���������Host��Ŀ����");

                    // ����3�����NQMC�Ƿ��Ѿ�������Ϸ�����û��������
                    if (!nqmc.IsGameStarted)
                    {
                        LogDebug("NQMCδ��������������������Ϸ");
                        nqmc.StartGame(true);
                    }

                    LogDebug("������NetworkManager��BroadcastMessageʵ���Ƿ����Host�Լ�");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"����Host��Ŀ����ʧ��: {e.Message}");
            }
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

        #region �����ӿں͵��Է���

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
        public string GetGameStats()
        {
            var stats = "=== ��Ϸͳ�� ===\n";
            stats += $"��Ϸ״̬: {(gameInProgress ? "������" : "δ��ʼ")}\n";
            stats += $"��ʼ�����: {isInitialized}\n";
            stats += $"��ǰ�غ����: {currentTurnPlayerId}\n";
            stats += $"�������: {playerStates?.Count ?? 0}\n";

            if (NetworkManager.Instance != null)
            {
                stats += $"Host���ID: {NetworkManager.Instance.GetHostPlayerId()}\n";
                stats += $"Host�ͻ��˾���: {NetworkManager.Instance.IsHostClientReady}\n";
            }

            if (playerStates != null)
            {
                stats += "���״̬:\n";
                foreach (var player in playerStates.Values)
                {
                    stats += $"  - {player.playerName} (ID: {player.playerId}, Ѫ��: {player.health}, ���: {player.isAlive})\n";
                }
            }

            if (RoomManager.Instance?.CurrentRoom != null)
            {
                var room = RoomManager.Instance.CurrentRoom;
                stats += $"���������: {room.players.Count}\n";
                stats += $"����Host ID: {room.hostId}\n";
            }

            return stats;
        }
        /// <summary>
        /// �ֶ�ͬ���������ݣ������ã�
        /// </summary>
        [ContextMenu("�ֶ�ͬ����������")]
        public void ManualSyncFromRoom()
        {
            if (Application.isPlaying)
            {
                LogDebug("�ֶ�ͬ����������");
                SyncPlayersFromRoomSystem();
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
                   $"HostPlayerId: {NetworkManager.Instance?.GetHostPlayerId()}, " +
                   $"HostClientReady: {NetworkManager.Instance?.IsHostClientReady}, " +
                   $"QuestionDataService: {questionDataService != null}, " +
                   $"RoomManager: {RoomManager.Instance != null}, " +
                   $"CurrentRoom: {RoomManager.Instance?.CurrentRoom != null}";
        }

        /// <summary>
        /// ��ʾ��ǰ����б������ã�
        /// </summary>
        [ContextMenu("��ʾ����б�")]
        public void ShowPlayerList()
        {
            if (Application.isPlaying)
            {
                LogDebug("=== ��ǰ����б� ===");
                if (playerStates == null || playerStates.Count == 0)
                {
                    LogDebug("û�����");
                    return;
                }

                foreach (var player in playerStates.Values)
                {
                    LogDebug($"���: {player.playerName} (ID: {player.playerId}, Ѫ��: {player.health}, ���: {player.isAlive})");
                }
            }
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
    /// �����Ϸ״̬���򻯰汾���Ƴ�������
    /// </summary>
    [System.Serializable]
    public class PlayerGameState
    {
        public ushort playerId;
        public string playerName;
        public int health;
        public bool isAlive;
        public bool isReady;
        public float lastActiveTime;

        public PlayerGameState()
        {
            lastActiveTime = Time.time;
        }
    }
}