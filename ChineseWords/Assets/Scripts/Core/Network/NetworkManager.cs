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
    /// ֧��Host-Client�ܹ����ȿ�����Ϊ������������+�ͻ��ˣ���Ҳ������Ϊ���ͻ���
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        [Header("��������")]
        [SerializeField] private ushort defaultPort = 7777;
        [SerializeField] private ushort maxClients = 8;
        [SerializeField] private ushort timeoutTime = 5000;

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
        public static event Action<ushort, int> OnHealthUpdated;
        public static event Action<ushort> OnPlayerTurnChanged;

        // ����Host-Client�����¼�
        public static event Action OnHostStarted;
        public static event Action OnHostStopped;
        public static event Action<ushort> OnPlayerJoined;  // ������Ҽ���
        public static event Action<ushort> OnPlayerLeft;    // ������뿪

        private void Awake()
        {
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
            // �������˵�ѡ���ģʽ���г�ʼ��
            InitializeFromMainMenu();
        }

        private void Update()
        {
            // �����������
            if (server != null && server.IsRunning)
                server.Update();

            if (client != null)
                client.Update();
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
        /// ��Ϊ����������Hostģʽ��
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

                // �������� OnHostStarted �¼�
                LogDebug("���� OnHostStarted �¼�");
                OnHostStarted?.Invoke();

                // ͬʱ��Ϊ�ͻ������ӵ��Լ��ķ��������ӳ�һ��ȷ����������ȫ������
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

            IsHost = false;
            isHostInitialized = false;

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

                // �����¼�����
                client.Connected -= OnSelfClientConnected;
                client.Connected -= OnClientConnectedToHost;
                client.Disconnected -= OnSelfClientDisconnected;
                client.Disconnected -= OnClientDisconnectedFromHost;
                client.ConnectionFailed -= OnSelfClientConnectionFailed;
                client.ConnectionFailed -= OnClientConnectionFailed;

                client = null;
            }
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

        #region �������¼�����Hostģʽ��

        private void OnServerClientConnected(object sender, ServerConnectedEventArgs e)
        {
            LogDebug($"��Ҽ��뷿��: ID={e.Client.Id}");
            OnPlayerJoined?.Invoke(e.Client.Id);
        }

        private void OnServerClientDisconnected(object sender, ServerDisconnectedEventArgs e)
        {
            LogDebug($"����뿪����: ID={e.Client.Id}");
            OnPlayerLeft?.Invoke(e.Client.Id);
        }

        #endregion

        #region �ͻ����¼�����Hostģʽ�µ������ӣ�

        private void OnSelfClientConnected(object sender, EventArgs e)
        {
            LogDebug($"�����ͻ������ӳɹ�! ID: {ClientId}");

            if (!isHostInitialized)
            {
                isHostInitialized = true;
                LogDebug("������ȫ��ʼ�����");

                // �����û�д����� OnHostStarted���������ٴδ���
                // ��ȷ���˼�ʹʱ��������Ҳ����ȷ֪ͨ
                OnHostStarted?.Invoke();
            }

            OnConnected?.Invoke(); // ���������¼�
        }

        private void OnSelfClientDisconnected(object sender, EventArgs e)
        {
            LogDebug("�����ͻ��˶Ͽ�����");
            OnDisconnected?.Invoke(); // ���������¼�
        }

        private void OnSelfClientConnectionFailed(object sender, EventArgs e)
        {
            Debug.LogError("�����ͻ�������ʧ��");
            StopHost();
        }

        #endregion

        #region �ͻ����¼�����Clientģʽ��

        private void OnClientConnectedToHost(object sender, EventArgs e)
        {
            LogDebug($"�ɹ����ӵ�����! ID: {ClientId}");
            OnConnected?.Invoke(); // ���������¼�
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

        #region �����ӿڷ��������ּ��ݣ�

        /// <summary>
        /// ��ȡ������Ϣ
        /// </summary>
        public string GetRoomInfo()
        {
            if (IsHost)
            {
                return $"����: {RoomName} | ���: {ConnectedPlayerCount}/{MaxPlayers} | �˿�: {Port}";
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

            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.RequestQuestion);
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

            Message message = Message.Create(MessageSendMode.Reliable, NetworkMessageType.SubmitAnswer);
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
                   $"HostInitialized: {isHostInitialized}";
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