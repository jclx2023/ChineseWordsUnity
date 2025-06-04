using UnityEngine;
using Riptide;
using Riptide.Utils;
using Core.Network;
using System;
using System.Collections;
using UI;

namespace Core.Network
{
    /// <summary>
    /// �����������֧��Host-Client�ܹ���
    /// �޸��汾�����Host ID��һ�����⣬ͳһHost�����ݹ���
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        [Header("��������")]
        [SerializeField] private ushort defaultPort = 7777;
        [SerializeField] private ushort maxClients = 8;
        [SerializeField] private ushort timeoutTime = 20000;

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        public static NetworkManager Instance { get; private set; }

        // �������
        private Server server;  // Hostģʽ�µķ�����
        private Client client;  // �ͻ��ˣ�Host��Clientģʽ����Ҫ��

        // ģʽ״̬
        public bool IsHost { get; private set; }
        public bool IsClient => client?.IsConnected ?? false;
        public bool IsServer => server?.IsRunning ?? false;
        public bool IsConnected => IsClient; // ������ԭNetworkManager�ļ�����
        public ushort ClientId => client?.Id ?? 0;
        public ushort Port { get; private set; }

        // **�ؼ��޸���ͳһHost��ݹ���**
        private ushort hostPlayerId = 0;  // Host��Ϊ��ҵ���ʵID
        private bool isHostClientReady = false;  // Host�ͻ����Ƿ�׼������

        // Hostģʽ�µķ�������Ϣ
        public string RoomName { get; private set; }
        public int MaxPlayers { get; private set; }
        public int ConnectedPlayerCount => server?.ClientCount ?? 0;

        // ��ʼ��״̬
        private bool isHostInitialized = false;

        // �¼���������ԭNetworkManager���ݣ�
        public static event Action OnConnected;
        public static event Action OnDisconnected;
        public static event Action<NetworkQuestionData> OnQuestionReceived;
        public static event Action<bool, string> OnAnswerResultReceived;
        public static event Action<ushort, int, int> OnHealthUpdated;
        public static event Action<ushort> OnPlayerTurnChanged;

        // ����Host-Client�����¼�
        public static event Action OnHostStarted;
        public static event Action OnHostStopped;
        public static event Action<ushort> OnPlayerJoined;
        public static event Action<ushort> OnPlayerLeft;

        // **������Host���׼�������¼�**
        public static event Action OnHostPlayerReady;  // Host��Ϊ���׼������


        private ushort lastTurnPlayerId = 0;  // ��һ�εĻغ����ID
        private int gameProgressSequence = 0;  // ��Ϸ�������к�
        private bool gameStartReceived = false;  // �Ƿ����յ���Ϸ��ʼ��Ϣ

        // ��Ϣ����������ֹ��Ϣ����
        private struct PendingQuestionMessage
        {
            public NetworkQuestionData question;
            public float receivedTime;
        }
        private PendingQuestionMessage? pendingQuestion = null;
        private const float QUESTION_BUFFER_TIMEOUT = 2f;  // ���峬ʱʱ��

        private void Awake()
        {
            Application.runInBackground = true;
            LogDebug($"NetworkManager Awake ִ��ʱ��: {Time.time}");

            // ����ģʽ
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeNetwork();
                LogDebug("NetworkManager �����Ѵ���");
            }
            else
            {
                LogDebug("�����ظ���NetworkManagerʵ��");
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            LogDebug($"NetworkManager Start ִ��ʱ��: {Time.time}");

            // ��鵱ǰ��������������˵��������Զ���ʼ��
            string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (IsMainMenuScene(currentSceneName))
            {
                LogDebug("��⵽���˵�������NetworkManager���ִ���״̬");
                return;
            }

            // ֻ����������Ϸ�����Ÿ������˵�ѡ���ģʽ���г�ʼ��
            InitializeFromMainMenu();
        }

        /// <summary>
        /// ����Ƿ�Ϊ���˵�����
        /// </summary>
        private bool IsMainMenuScene(string sceneName)
        {
            string[] mainMenuScenes = { "MainMenuScene", "MainMenu", "Menu" };

            foreach (string menuScene in mainMenuScenes)
            {
                if (sceneName.Equals(menuScene, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// �ֶ����������ʼ������MainMenuManager���ã�
        /// </summary>
        public void ManualInitializeNetwork()
        {
            LogDebug("�ֶ����������ʼ��");
            InitializeFromMainMenu();
        }

        private void Update()
        {
            // �������������ԭ���߼���
            if (server != null && server.IsRunning)
                server.Update();

            if (client != null)
                client.Update();

            // ����������������Ϣ
            CheckPendingMessages();
        }

        private void OnApplicationQuit()
        {
            Shutdown();
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        /// <summary>
        /// ��ʼ���������
        /// </summary>
        private void InitializeNetwork()
        {
            // ����Riptide��־����
            RiptideLogger.Initialize(Debug.Log, Debug.Log, Debug.LogWarning, Debug.LogError, false);
            LogDebug("Riptide��־ϵͳ��ʼ�����");
        }

        /// <summary>
        /// �������˵�ѡ���ʼ��
        /// </summary>
        private void InitializeFromMainMenu()
        {
            LogDebug($"�������˵���ʼ����ѡ��ģʽ: {MainMenuManager.SelectedGameMode}");
            LogDebug($"MainMenuManager ���� - Port: {MainMenuManager.Port}, RoomName: {MainMenuManager.RoomName}, MaxPlayers: {MainMenuManager.MaxPlayers}");

            switch (MainMenuManager.SelectedGameMode)
            {
                case MainMenuManager.GameMode.Host:
                    // ʹ��Э����ȷ����ȷ�ĳ�ʼ��˳��
                    StartCoroutine(StartHostWithDelay());
                    break;

                case MainMenuManager.GameMode.Client:
                    Connect(MainMenuManager.HostIP, MainMenuManager.Port);
                    break;

                case MainMenuManager.GameMode.SinglePlayer:
                    LogDebug("����ģʽ������Ҫ��������");
                    break;

                default:
                    Debug.LogWarning("δ֪����Ϸģʽ");
                    break;
            }
        }

        /// <summary>
        /// �ӳ�����Host��ȷ�������������׼������
        /// </summary>
        private IEnumerator StartHostWithDelay()
        {
            LogDebug("��ʼHost��������...");

            // �ȴ���֡ȷ�����������ʼ�����
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            // ����Host
            StartAsHost(MainMenuManager.Port, MainMenuManager.RoomName, MainMenuManager.MaxPlayers);
        }

        /// <summary>
        /// ���ӵ���������������ԭNetworkManager���ݣ�
        /// </summary>
        public void Connect(string ip = null, ushort? serverPort = null)
        {
            string targetIP = ip ?? "127.0.0.1";
            ushort targetPort = serverPort ?? defaultPort;

            ConnectAsClient(targetIP, targetPort);
        }

        /// <summary>
        /// �Ͽ����ӣ�������ԭNetworkManager���ݣ�
        /// </summary>
        public void Disconnect()
        {
            if (IsHost)
                StopHost();
            else
                DisconnectClient();
        }

        /// <summary>
        /// ��Ϊ����������Hostģʽ��- �޸��汾
        /// </summary>
        public void StartAsHost(ushort port, string roomName, int maxPlayers)
        {
            if (IsHost || IsServer)
            {
                LogDebug("�Ѿ�����������ģʽ");
                return;
            }

            Port = port;
            RoomName = roomName;
            MaxPlayers = maxPlayers;

            // **����Host״̬**
            hostPlayerId = 0;
            isHostClientReady = false;

            LogDebug($"��������ģʽ - ����: {roomName}, �˿�: {port}, ������: {maxPlayers}");

            try
            {
                // ����������
                server = new Server();
                server.ClientConnected += OnServerClientConnected;
                server.ClientDisconnected += OnServerClientDisconnected;

                LogDebug("��������������...");
                server.Start(port, (ushort)maxPlayers);
                LogDebug($"�����������ɹ���IsRunning: {server.IsRunning}");

                // ��������״̬
                IsHost = true;
                LogDebug($"IsHost ����Ϊ: {IsHost}");

                // **�޸ģ��ȴ��������������¼�����������Host���׼���¼�**
                LogDebug("���� OnHostStarted �¼�����������������");
                OnHostStarted?.Invoke();

                // ͬʱ��Ϊ�ͻ������ӵ��Լ��ķ�����
                StartCoroutine(ConnectSelfClientWithDelay(port));
            }
            catch (System.Exception e)
            {
                Debug.LogError($"��������ʧ��: {e.Message}");
                StopHost();
            }
        }

        /// <summary>
        /// �ӳ������Լ��Ŀͻ��˵�������
        /// </summary>
        private IEnumerator ConnectSelfClientWithDelay(ushort port)
        {
            // �ȴ���������ȫ����
            yield return new WaitForSeconds(0.1f);

            LogDebug("��ʼ�����Լ��Ŀͻ��˵�������...");

            try
            {
                client = new Client();
                client.Connected += OnSelfClientConnected;
                client.Disconnected += OnSelfClientDisconnected;
                client.ConnectionFailed += OnSelfClientConnectionFailed;

                LogDebug($"�ͻ������ӵ�: 127.0.0.1:{port}");
                client.Connect($"127.0.0.1:{port}", timeoutTime);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"������ʧ��: {e.Message}");
                StopHost();
            }
        }

        /// <summary>
        /// ��Ϊ�ͻ������ӣ�Clientģʽ��
        /// </summary>
        public void ConnectAsClient(string hostIP, ushort port)
        {
            if (IsClient)
            {
                LogDebug("�Ѿ����ӵ�����");
                return;
            }

            Port = port;
            IsHost = false;

            LogDebug($"���ӵ�����: {hostIP}:{port}");

            try
            {
                client = new Client();
                client.Connected += OnClientConnectedToHost;
                client.Disconnected += OnClientDisconnectedFromHost;
                client.ConnectionFailed += OnClientConnectionFailed;
                client.Connect($"{hostIP}:{port}", timeoutTime);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"��������ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// ֹͣ����
        /// </summary>
        public void StopHost()
        {
            if (!IsHost)
                return;

            LogDebug("ֹͣ����");

            // ֹͣ������
            if (server != null)
            {
                server.ClientConnected -= OnServerClientConnected;
                server.ClientDisconnected -= OnServerClientDisconnected;
                server.Stop();
                server = null;
                LogDebug("��������ֹͣ");
            }

            // �Ͽ��ͻ���
            DisconnectClient();

            // **����Host״̬**
            IsHost = false;
            isHostInitialized = false;
            hostPlayerId = 0;
            isHostClientReady = false;

            LogDebug("���� OnHostStopped �¼�");
            OnHostStopped?.Invoke();
        }

        /// <summary>
        /// �Ͽ��ͻ�������
        /// </summary>
        public void DisconnectClient()
        {
            if (client != null)
            {
                if (IsClient)
                {
                    LogDebug("�Ͽ��ͻ�������");
                    client.Disconnect();
                }

                // �����¼����ģ�ԭ���߼���
                client.Connected -= OnSelfClientConnected;
                client.Connected -= OnClientConnectedToHost;
                client.Disconnected -= OnSelfClientDisconnected;
                client.Disconnected -= OnClientDisconnectedFromHost;
                client.ConnectionFailed -= OnSelfClientConnectionFailed;
                client.ConnectionFailed -= OnClientConnectionFailed;

                client = null;
            }

            // ������������Ϣ״̬
            gameStartReceived = false;
            lastTurnPlayerId = 0;
            gameProgressSequence = 0;
            pendingQuestion = null;
            LogDebug("������Ϣ״̬������");
        }

        /// <summary>
        /// ��ȫ�ر�����
        /// </summary>
        public void Shutdown()
        {
            LogDebug("����������ر�");
            StopHost();
            DisconnectClient();
        }

        #region ͳһHost��ݹ���ӿ�

        /// <summary>
        /// ��ȡHost���ID��ͳһ�ӿڣ�
        /// </summary>
        public ushort GetHostPlayerId()
        {
            return hostPlayerId;
        }

        /// <summary>
        /// ���ָ��ID�Ƿ�ΪHost���
        /// </summary>
        public bool IsHostPlayer(ushort playerId)
        {
            return IsHost && playerId == hostPlayerId;
        }

        /// <summary>
        /// Host�ͻ����Ƿ�׼������
        /// </summary>
        public bool IsHostClientReady => isHostClientReady;

        /// <summary>
        /// ��ȡ���ڷ��䴴����Host��Ϣ
        /// </summary>
        public (ushort hostId, bool isReady) GetHostRoomInfo()
        {
            return (hostPlayerId, isHostClientReady);
        }

        #endregion

        /// <summary>
        /// ������Ϣ��������ԭNetworkManager���ݣ�
        /// </summary>
        public void SendMessage(Message message)
        {
            if (IsClient)
            {
                client.Send(message);
            }
            else
            {
                Debug.LogWarning("δ���ӵ����������޷�������Ϣ");
            }
        }

        /// <summary>
        /// �㲥��Ϣ�����пͻ��ˣ�����ʹ�ã�
        /// </summary>
        public void BroadcastMessage(Message message)
        {
            if (IsServer)
            {
                server.SendToAll(message);
            }
            else
            {
                Debug.LogWarning("�����������޷��㲥��Ϣ");
            }
        }

        /// <summary>
        /// ������Ϣ��ָ���ͻ��ˣ�����ʹ�ã�
        /// </summary>
        public void SendMessageToClient(ushort clientId, Message message)
        {
            if (IsServer)
            {
                server.Send(message, clientId);
            }
            else
            {
                Debug.LogWarning("�����������޷�������Ϣ���ض��ͻ���");
            }
        }

        #region ���������Ϣ����

        /// <summary>
        /// ���ͷ������ݸ�ָ���ͻ���
        /// </summary>
        public void SendRoomDataToClient(ushort clientId, RoomData roomData)
        {
            if (!IsHost || server == null)
                return;

            Message message = Message.Create(MessageSendMode.Reliable, (ushort)NetworkMessageType.RoomDataSync);
            message.SerializeRoomData(roomData);
            server.Send(message, clientId);

            LogDebug($"���ͷ������ݸ��ͻ��� {clientId}");
        }

        /// <summary>
        /// �㲥��Ҽ��뷿��
        /// </summary>
        public void BroadcastPlayerJoinRoom(RoomPlayer player)
        {
            if (!IsHost || server == null)
                return;

            Message message = Message.Create(MessageSendMode.Reliable, (ushort)NetworkMessageType.PlayerJoinRoom);
            message.SerializePlayer(player);
            server.SendToAll(message);

            LogDebug($"�㲥��Ҽ���: {player.playerName}");
        }

        /// <summary>
        /// �㲥����뿪����
        /// </summary>
        public void BroadcastPlayerLeaveRoom(ushort playerId)
        {
            if (!IsHost || server == null)
                return;

            Message message = Message.Create(MessageSendMode.Reliable, (ushort)NetworkMessageType.PlayerLeaveRoom);
            message.AddUShort(playerId);
            server.SendToAll(message);

            LogDebug($"�㲥����뿪: {playerId}");
        }

        /// <summary>
        /// �㲥���׼��״̬����
        /// </summary>
        public void BroadcastPlayerReadyUpdate(ushort playerId, bool isReady)
        {
            if (!IsHost || server == null)
                return;

            Message message = Message.Create(MessageSendMode.Reliable, (ushort)NetworkMessageType.PlayerReadyUpdate);
            message.SerializeReadyChange(playerId, isReady);
            server.SendToAll(message);

            LogDebug($"�㲥׼��״̬: ���{playerId} -> {isReady}");
        }

        /// <summary>
        /// �㲥��Ϸ��ʼ����
        /// </summary>
        public void BroadcastGameStart()
        {
            if (!IsHost || server == null)
                return;

            Message message = Message.Create(MessageSendMode.Reliable, (ushort)NetworkMessageType.GameStartRequest);
            server.SendToAll(message);

            LogDebug("�㲥��Ϸ��ʼ����");
        }

        /// <summary>
        /// ���󷿼���Ϣ
        /// </summary>
        public void RequestRoomInfo()
        {
            if (IsHost || client == null)
                return;

            Message message = Message.Create(MessageSendMode.Reliable, (ushort)NetworkMessageType.RoomInfoRequest);
            client.Send(message);

            LogDebug("���󷿼���Ϣ");
        }

        /// <summary>
        /// ����ı�׼��״̬
        /// </summary>
        public void RequestReadyStateChange(bool isReady)
        {
            if (IsHost || client == null)
                return;

            Message message = Message.Create(MessageSendMode.Reliable, (ushort)NetworkMessageType.PlayerReadyRequest);
            message.SerializeReadyChange(ClientId, isReady);
            client.Send(message);

            LogDebug($"����ı�׼��״̬: {isReady}");
        }

        #endregion

        #region �������¼�����Hostģʽ��

        private void OnServerClientConnected(object sender, ServerConnectedEventArgs e)
        {
            LogDebug($"�ͻ������ӵ�������: ID={e.Client.Id}");

            // **�ؼ��޸�������Host�Լ��Ŀͻ��˺��������**
            if (IsHost && !isHostClientReady)
            {
                // ����Host�Լ��Ŀͻ�������
                hostPlayerId = e.Client.Id;
                LogDebug($"Host���IDȷ��Ϊ: {hostPlayerId}");
                // ������������isHostClientReady���ȴ�OnSelfClientConnected
                return;
            }

            // ������Ҽ���
            LogDebug($"����Ҽ��뷿��: ID={e.Client.Id}");
            OnPlayerJoined?.Invoke(e.Client.Id);
        }

        private void OnServerClientDisconnected(object sender, ServerDisconnectedEventArgs e)
        {
            LogDebug($"�ͻ��˶Ͽ�����: ID={e.Client.Id}");

            // ����Ƿ���Host��ҶϿ�
            if (IsHost && e.Client.Id == hostPlayerId)
            {
                LogDebug("Host��ҿͻ��˶Ͽ�����");
                isHostClientReady = false;
                return;
            }

            // ��������뿪
            LogDebug($"����뿪����: ID={e.Client.Id}");
            OnPlayerLeft?.Invoke(e.Client.Id);
        }

        #endregion

        #region �ͻ����¼�����Hostģʽ�µ������ӣ�

        /// <summary>
        /// �޸�Host�����¼����� - ������Ϣ״̬
        /// </summary>
        private void OnSelfClientConnected(object sender, EventArgs e)
        {
            LogDebug($"Host�ͻ������ӳɹ�! ���ID: {ClientId}");

            // ԭ���߼�...
            if (hostPlayerId == 0 || hostPlayerId != ClientId)
            {
                hostPlayerId = ClientId;
                LogDebug($"Host���ID����Ϊ: {hostPlayerId}");
            }

            isHostClientReady = true;

            if (!isHostInitialized)
            {
                isHostInitialized = true;
                LogDebug("Host��ȫ��ʼ�����");
            }

            // ������������Ϣ״̬
            gameStartReceived = false;
            lastTurnPlayerId = 0;
            gameProgressSequence = 0;
            pendingQuestion = null;
            LogDebug("Host��Ϣ״̬������");

            OnHostPlayerReady?.Invoke();
            OnConnected?.Invoke();
        }

        private void OnSelfClientDisconnected(object sender, EventArgs e)
        {
            LogDebug("Host�ͻ��˶Ͽ�����");
            isHostClientReady = false;
            OnDisconnected?.Invoke(); // ���������¼�
        }

        private void OnSelfClientConnectionFailed(object sender, EventArgs e)
        {
            Debug.LogError("Host�ͻ�������ʧ��");
            StopHost();
        }

        #endregion

        #region �ͻ����¼�����Clientģʽ��

        private void OnClientConnectedToHost(object sender, EventArgs e)
        {
            LogDebug($"�ɹ����ӵ�����! ID: {ClientId}");

            // ������������Ϣ״̬
            gameStartReceived = false;
            lastTurnPlayerId = 0;
            gameProgressSequence = 0;
            pendingQuestion = null;
            LogDebug("Client��Ϣ״̬������");

            OnConnected?.Invoke();
        }

        private void OnClientDisconnectedFromHost(object sender, EventArgs e)
        {
            LogDebug("�������Ͽ�����");
            OnDisconnected?.Invoke(); // ���������¼�
        }

        private void OnClientConnectionFailed(object sender, EventArgs e)
        {
            Debug.LogError("��������ʧ��");
        }

        #endregion

        #region ��Ϸ��Ϣ��������������ԭNetworkManager���ݣ�

        /// <summary>
        /// ������Ϸ������Ϣ - �޸��汾�����������֤
        /// </summary>
        [MessageHandler((ushort)NetworkMessageType.GameProgress)]
        private static void HandleGameProgress(Message message)
        {
            int questionNumber = message.GetInt();
            int alivePlayerCount = message.GetInt();
            ushort turnPlayerId = message.GetUShort();

            // ��ѡ����ȡ����Ľ�����Ϣ
            int questionTypeInt = -1;
            float timeLimit = 0f;
            try
            {
                questionTypeInt = message.GetInt();
                timeLimit = message.GetFloat();
            }
            catch
            {
                // ���ݾɰ汾��Ϣ��ʽ
            }

            Debug.Log($"[NetworkManager] �յ���Ϸ����: ��{questionNumber}��, ���{alivePlayerCount}��, �غ����{turnPlayerId}");

            if (Instance != null)
            {
                // ��֤�������У���ֹ����
                if (questionNumber < Instance.gameProgressSequence)
                {
                    Debug.LogWarning($"[NetworkManager] �յ����ڵ���Ϸ������Ϣ: {questionNumber} < {Instance.gameProgressSequence}");
                    return;
                }

                Instance.gameProgressSequence = questionNumber;
                Debug.Log($"[NetworkManager] ��Ϸ���������Ѹ���: {questionNumber}");
            }

            // ת����NetworkUI
            var networkUI = FindObjectOfType<NetworkUI>();
            if (networkUI != null)
            {
                networkUI.OnGameProgressReceived(questionNumber, alivePlayerCount, turnPlayerId);
            }
        }

        /// <summary>
        /// ����غϱ����Ϣ - �޸��汾�����״̬��֤�ͻ��崦��
        /// </summary>
        [MessageHandler((ushort)NetworkMessageType.PlayerTurnChanged)]
        private static void HandlePlayerTurnChanged(Message message)
        {
            ushort newTurnPlayerId = message.GetUShort();

            Debug.Log($"[NetworkManager] �յ��غϱ��: ���{newTurnPlayerId}");

            if (Instance != null)
            {
                // ��֤�غϱ���ĺ�����
                if (Instance.lastTurnPlayerId == newTurnPlayerId)
                {
                    Debug.LogWarning($"[NetworkManager] �ظ��Ļغϱ����Ϣ: {newTurnPlayerId}");
                    return;
                }

                Instance.lastTurnPlayerId = newTurnPlayerId;
                Debug.Log($"[NetworkManager] �غ�״̬�Ѹ���: {newTurnPlayerId}");

                // ����Ƿ��д��������Ŀ
                Instance.ProcessPendingQuestion();
            }

            // �ȸ���UI�еĻغ�״̬
            var networkUI = FindObjectOfType<NetworkUI>();
            if (networkUI != null)
            {
                networkUI.OnTurnChangedReceived(newTurnPlayerId);
            }

            // ��֪ͨNQMC��ȷ��UI״̬�ȸ��£�
            if (NetworkQuestionManagerController.Instance != null)
            {
                NetworkQuestionManagerController.Instance.GetType()
                    .GetMethod("OnNetworkPlayerTurnChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(NetworkQuestionManagerController.Instance, new object[] { newTurnPlayerId });
            }
        }

        /// <summary>
        /// ����Ѫ��������Ϣ�������û�еĻ���
        /// </summary>
        [MessageHandler((ushort)NetworkMessageType.HealthUpdate)]
        private static void HandleHealthUpdate(Message message)
        {
            ushort playerId = message.GetUShort();
            int newHealth = message.GetInt();
            int maxHealth = message.GetInt();

            Debug.Log($"[NetworkManager] �յ�Ѫ������: ���{playerId} {newHealth}/{maxHealth}");

            // ת����NetworkUI
            var networkUI = FindObjectOfType<NetworkUI>();
            if (networkUI != null)
            {
                networkUI.OnHealthUpdateReceived(playerId, newHealth, maxHealth);
            }
        }

        /// <summary>
        /// ������Ҵ�������Ϣ���޸��汾��
        /// </summary>
        [MessageHandler((ushort)NetworkMessageType.PlayerAnswerResult)]
        private static void HandlePlayerAnswerResult(Message message)
        {
            ushort playerId = message.GetUShort();
            bool isCorrect = message.GetBool();
            string answer = message.GetString();

            Debug.Log($"[NetworkManager] �յ�������: ���{playerId} {(isCorrect ? "��ȷ" : "����")} - {answer}");

            // ת����NetworkUI�����NetworkUI��Ҫ��ʾ��������
            var networkUI = FindObjectOfType<NetworkUI>();
            if (networkUI != null)
            {
                // ��Ҫ��NetworkUI����Ӷ�Ӧ�Ĵ�����
                networkUI.OnPlayerAnswerResultReceived(playerId, isCorrect, answer);
            }
        }

        [MessageHandler((ushort)NetworkMessageType.SendQuestion)]
        private static void HandleQuestionReceived(Message message)
        {
            byte[] questionData = message.GetBytes();
            NetworkQuestionData question = NetworkQuestionData.Deserialize(questionData);

            if (question != null)
            {
                Debug.Log($"[NetworkManager] �յ���Ŀ: {question.questionType} - {question.questionText}");

                if (Instance != null)
                {
                    // ��֤����ʱ��
                    if (!Instance.gameStartReceived)
                    {
                        Debug.LogWarning("[NetworkManager] �յ���Ŀ����Ϸ��δ��ʼ������");
                        return;
                    }

                    // ����Ƿ�Ӧ�û�����Ŀ���ȴ��غ�״̬���£�
                    if (Instance.ShouldBufferQuestion())
                    {
                        Debug.Log("[NetworkManager] ������Ŀ���ȴ��غ�״̬����");
                        Instance.pendingQuestion = new PendingQuestionMessage
                        {
                            question = question,
                            receivedTime = Time.time
                        };
                        return;
                    }

                    // ����������Ŀ
                    Instance.ProcessQuestionImmediate(question);
                }
                else
                {
                    // ���ô�����Instanceʱ��
                    ProcessQuestionFallback(question);
                }
            }
            else
            {
                Debug.LogError("[NetworkManager] ��Ŀ���ݽ���ʧ��");
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

        #endregion

        #region ������Ϣ������

        [MessageHandler((ushort)NetworkMessageType.RoomInfoRequest)]
        private static void HandleRoomInfoRequest(ushort fromClientId, Message message)
        {
            Debug.Log($"[NetworkManager] �յ�������Ϣ�������Կͻ��� {fromClientId}");

            // ��ȡ��ǰ��������
            if (RoomManager.Instance?.CurrentRoom != null)
            {
                Instance.SendRoomDataToClient(fromClientId, RoomManager.Instance.CurrentRoom);
            }
        }

        [MessageHandler((ushort)NetworkMessageType.PlayerReadyRequest)]
        private static void HandlePlayerReadyRequest(ushort fromClientId, Message message)
        {
            var (playerId, isReady) = message.DeserializeReadyChange();

            Debug.Log($"[NetworkManager] �յ�׼��״̬����: ���{playerId} -> {isReady}");

            // ���·�������ҵ�׼��״̬
            if (RoomManager.Instance?.CurrentRoom != null)
            {
                bool success = RoomManager.Instance.CurrentRoom.SetPlayerReady(playerId, isReady);
                if (success)
                {
                    // �㲥׼��״̬�仯�����пͻ���
                    Instance.BroadcastPlayerReadyUpdate(playerId, isReady);
                }
            }
        }

        [MessageHandler((ushort)NetworkMessageType.RoomDataSync)]
        private static void HandleRoomDataSync(Message message)
        {
            RoomData roomData = message.DeserializeRoomData();
            Debug.Log($"[NetworkManager] �յ���������ͬ��: {roomData.roomName}");

            // ֪ͨRoomManager���·�������
            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.UpdateRoomFromNetwork(roomData);
            }
        }

        [MessageHandler((ushort)NetworkMessageType.PlayerJoinRoom)]
        private static void HandlePlayerJoinRoom(Message message)
        {
            RoomPlayer player = message.DeserializePlayer();
            Debug.Log($"[NetworkManager] �յ���Ҽ���֪ͨ: {player.playerName}");

            // ֪ͨRoomManager
            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.OnNetworkPlayerJoined(player);
            }
        }

        [MessageHandler((ushort)NetworkMessageType.PlayerLeaveRoom)]
        private static void HandlePlayerLeaveRoom(Message message)
        {
            ushort playerId = message.GetUShort();
            Debug.Log($"[NetworkManager] �յ�����뿪֪ͨ: {playerId}");

            // ֪ͨRoomManager
            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.OnNetworkPlayerLeftMessage(playerId);
            }
        }

        [MessageHandler((ushort)NetworkMessageType.PlayerReadyUpdate)]
        private static void HandlePlayerReadyUpdate(Message message)
        {
            var (playerId, isReady) = message.DeserializeReadyChange();
            Debug.Log($"[NetworkManager] �յ�׼��״̬����: ���{playerId} -> {isReady}");

            // ֪ͨRoomManager
            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.OnNetworkPlayerReadyChanged(playerId, isReady);
            }
        }
        [MessageHandler((ushort)NetworkMessageType.GameStartRequest)]
        private static void HandleGameStartRequest(Message message)
        {
            Debug.Log("[NetworkManager] �յ���Ϸ��ʼ����");

            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.OnNetworkGameStart();
                Debug.Log("[NetworkManager] ��֪ͨRoomManager������Ϸ��ʼ");
            }
            else
            {
                Debug.LogError("[NetworkManager] RoomManagerʵ�������ڣ��޷�������Ϸ��ʼ����");
            }
        }
        /// <summary>
        /// ������Ϸ��ʼ��Ϣ - �޸��汾������״̬
        /// </summary>
        [MessageHandler((ushort)NetworkMessageType.GameStart)]
        private static void HandleGameStart(Message message)
        {
            int totalPlayerCount = message.GetInt();
            int alivePlayerCount = message.GetInt();
            ushort firstTurnPlayerId = message.GetUShort();

            Debug.Log($"[NetworkManager] �յ���Ϸ��ʼ: �����{totalPlayerCount}, ���{alivePlayerCount}, �׻غ����{firstTurnPlayerId}");

            // ����״̬
            if (Instance != null)
            {
                Instance.gameStartReceived = true;
                Instance.lastTurnPlayerId = 0;
                Instance.gameProgressSequence = 0;
                Instance.pendingQuestion = null;

                Debug.Log("[NetworkManager] ��Ϸ״̬������");
            }

            // ת����NetworkUI
            var networkUI = FindObjectOfType<NetworkUI>();
            if (networkUI != null)
            {
                networkUI.OnGameStartReceived(totalPlayerCount, alivePlayerCount, firstTurnPlayerId);
            }
        }
        /// <summary>
        /// �������״̬ͬ����Ϣ
        /// </summary>
        [MessageHandler((ushort)NetworkMessageType.PlayerStateSync)]
        private static void HandlePlayerStateSync(Message message)
        {
            ushort playerId = message.GetUShort();
            string playerName = message.GetString();
            bool isHost = message.GetBool();
            int currentHealth = message.GetInt();
            int maxHealth = message.GetInt();
            bool isAlive = message.GetBool();

            Debug.Log($"[NetworkManager] �յ����״̬ͬ��: {playerName} (ID:{playerId}) HP:{currentHealth}/{maxHealth}");

            var networkUI = FindObjectOfType<NetworkUI>();
            if (networkUI != null)
            {
                networkUI.OnPlayerStateSyncReceived(playerId, playerName, isHost, currentHealth, maxHealth, isAlive);
            }
        }
        #endregion

        #region �����ӿڷ��������ּ��ݣ�

        /// <summary>
        /// ��ȡ������Ϣ
        /// </summary>
        public string GetRoomInfo()
        {
            if (IsHost)
            {
                return $"����: {RoomName} | ���: {ConnectedPlayerCount}/{MaxPlayers} | �˿�: {Port} | Host���ID: {hostPlayerId}";
            }
            else if (IsClient)
            {
                return $"�����ӵ����� | ���ID: {ClientId}";
            }
            else
            {
                return "δ����";
            }
        }

        /// <summary>
        /// ������Ŀ���ͻ��˵��ã�
        /// </summary>
        public void RequestQuestion()
        {
            if (!IsClient)
            {
                Debug.LogWarning("δ���ӵ�������");
                return;
            }

            Message message = Message.Create(MessageSendMode.Reliable, (ushort)NetworkMessageType.RequestQuestion);
            SendMessage(message);
            LogDebug("������Ŀ...");
        }

        /// <summary>
        /// �ύ�𰸣��ͻ��˵��ã�
        /// </summary>
        public void SubmitAnswer(string answer)
        {
            if (!IsClient)
            {
                Debug.LogWarning("δ���ӵ�������");
                return;
            }

            Message message = Message.Create(MessageSendMode.Reliable, (ushort)NetworkMessageType.SubmitAnswer);
            message.AddString(answer);
            SendMessage(message);
            LogDebug($"�ύ��: {answer}");
        }

        /// <summary>
        /// ��ȡ��ǰ����״̬�������ã�
        /// </summary>
        public string GetNetworkStatus()
        {
            return $"IsHost: {IsHost}, IsServer: {IsServer}, IsClient: {IsClient}, " +
                   $"IsConnected: {IsConnected}, ClientId: {ClientId}, " +
                   $"HostPlayerId: {hostPlayerId}, HostClientReady: {isHostClientReady}, " +
                   $"HostInitialized: {isHostInitialized}";
        }

        #endregion
        #region ������������

        /// <summary>
        /// �ж��Ƿ�Ӧ�û�����Ŀ
        /// </summary>
        private bool ShouldBufferQuestion()
        {
            // �����Host������Ҫ����
            if (IsHost)
            {
                return false;
            }

            // �����û���յ��κλغϱ������Ҫ����
            if (lastTurnPlayerId == 0)
            {
                Debug.Log("[NetworkManager] ��δ�յ��غϱ����������Ŀ");
                return true;
            }

            // ��鵱ǰ�Ƿ�ΪClientģʽ�һغ�״̬��ȷ
            return false;
        }

        /// <summary>
        /// ������������Ŀ
        /// </summary>
        private void ProcessPendingQuestion()
        {
            if (pendingQuestion.HasValue)
            {
                var pending = pendingQuestion.Value;

                // ����Ƿ�ʱ
                if (Time.time - pending.receivedTime > QUESTION_BUFFER_TIMEOUT)
                {
                    Debug.LogWarning("[NetworkManager] ��������Ŀ��ʱ������");
                    pendingQuestion = null;
                    return;
                }

                Debug.Log("[NetworkManager] ������������Ŀ");
                ProcessQuestionImmediate(pending.question);
                pendingQuestion = null;
            }
        }

        /// <summary>
        /// ����������Ŀ
        /// </summary>
        private void ProcessQuestionImmediate(NetworkQuestionData question)
        {
            Debug.Log($"[NetworkManager] ����������Ŀ: {question.questionType}");

            // ������Ŀ�����¼�
            OnQuestionReceived?.Invoke(question);

            // ֱ��֪ͨNQMC
            if (NetworkQuestionManagerController.Instance != null)
            {
                var onQuestionMethod = NetworkQuestionManagerController.Instance.GetType()
                    .GetMethod("OnNetworkQuestionReceived", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (onQuestionMethod != null)
                {
                    onQuestionMethod.Invoke(NetworkQuestionManagerController.Instance, new object[] { question });
                }
                else
                {
                    Debug.LogWarning("[NetworkManager] �޷��ҵ�NQMC����Ŀ���շ���");
                }
            }
        }

        /// <summary>
        /// ������Ŀ������Instanceʱ��
        /// </summary>
        private static void ProcessQuestionFallback(NetworkQuestionData question)
        {
            Debug.Log($"[NetworkManager] ���ô�����Ŀ: {question.questionType}");
            OnQuestionReceived?.Invoke(question);
        }

        /// <summary>
        /// ���ڼ���������Ϣ����Update�е��ã�
        /// </summary>
        private void CheckPendingMessages()
        {
            if (pendingQuestion.HasValue)
            {
                var pending = pendingQuestion.Value;

                // ��ʱ���
                if (Time.time - pending.receivedTime > QUESTION_BUFFER_TIMEOUT)
                {
                    Debug.LogWarning("[NetworkManager] ��������Ŀ��ʱ��ǿ�ƴ���");
                    ProcessQuestionImmediate(pending.question);
                    pendingQuestion = null;
                }
            }
        }

        #endregion
        #region �ͻ���Ѫ������

        /// <summary>
        /// ���������Ѫ������
        /// </summary>
        private static void ProcessLocalPlayerHealthUpdate(ushort playerId, int newHealth, int maxHealth)
        {
            // ����Ƿ��Ǳ������
            if (IsLocalPlayer(playerId))
            {
                var healthManager = FindObjectOfType<Managers.PlayerHealthManager>();
                if (healthManager != null)
                {
                    healthManager.OnNetworkHealthUpdate(newHealth, maxHealth);
                    Debug.Log($"[NetworkManager] ���±������Ѫ��: {newHealth}/{maxHealth}");
                }
                else
                {
                    Debug.LogWarning("[NetworkManager] δ�ҵ�PlayerHealthManager���");
                }
            }
        }

        /// <summary>
        /// ���ָ�����ID�Ƿ�Ϊ�������
        /// </summary>
        private static bool IsLocalPlayer(ushort playerId)
        {
            if (Instance == null) return false;

            // Hostģʽ������Ƿ���Host���
            if (Instance.IsHost)
            {
                return Instance.IsHostPlayer(playerId);
            }
            // Clientģʽ������Ƿ����Լ���ClientId
            else if (Instance.IsClient)
            {
                return playerId == Instance.ClientId;
            }

            return false;
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
                Debug.Log($"[NetworkManager] {message}");
            }
        }

        #endregion
    }
}