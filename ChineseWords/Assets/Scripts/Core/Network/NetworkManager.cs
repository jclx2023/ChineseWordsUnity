using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using UI;
using Core.Network;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Core.Network
{
    /// <summary>
    /// ����Photon��������������������ݰ棩
    /// ������Ϸ������ͨ�š���ҹ���RPC��Ϣ����
    /// </summary>
    public class NetworkManager : MonoBehaviourPun, IPunObservable, IConnectionCallbacks, IMatchmakingCallbacks, IInRoomCallbacks
    {
        [Header("��������")]
        [SerializeField] private ushort maxClients = 8;
        [SerializeField] private bool enableDebugLogs = true;

        public static NetworkManager Instance { get; private set; }

        // === ����״̬���ԣ���ȫ���ݣ� ===
        public bool IsHost => PhotonNetwork.IsMasterClient;
        public bool IsClient => PhotonNetwork.IsConnected && PhotonNetwork.InRoom;
        public bool IsConnected => IsClient;

        // ��ҪID���� - ����ushort����
        public ushort ClientId => (ushort)PhotonNetwork.LocalPlayer.ActorNumber;
        public ushort Port => 7777; // ��������

        // ������Ϣ����
        public string RoomName => PhotonNetwork.CurrentRoom?.Name ?? "";
        public int MaxPlayers => PhotonNetwork.CurrentRoom?.MaxPlayers ?? 0;
        public int ConnectedPlayerCount => PhotonNetwork.CurrentRoom?.PlayerCount ?? 0;

        // Host��ݹ�������ushort��
        private ushort hostPlayerId = 0;
        private bool isHostClientReady = false;

        // ��Ϸ״̬����
        private bool isGameInProgress = false;
        private ushort lastTurnPlayerId = 0;
        private int gameProgressSequence = 0;
        private bool gameStartReceived = false;

        // === ��ȫ���ݵ��¼�ϵͳ ===
        public static event Action OnConnected;
        public static event Action OnDisconnected;
        public static event Action<NetworkQuestionData> OnQuestionReceived;
        public static event Action<bool, string> OnAnswerResultReceived;
        public static event Action<ushort, int, int> OnHealthUpdated;  // ����ushort
        public static event Action<ushort> OnPlayerTurnChanged;        // ����ushort

        // Host�����¼�������ushort���ݣ�
        public static event Action OnHostStarted;
        public static event Action OnHostStopped;
        public static event Action<ushort> OnPlayerJoined;
        public static event Action<ushort> OnPlayerLeft;
        public static event Action OnHostPlayerReady;

        private void Awake()
        {
            Application.runInBackground = true;

            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                // ȷ����PhotonView���
                if (GetComponent<PhotonView>() == null)
                {
                    gameObject.AddComponent<PhotonView>();
                    LogDebug("�Զ����PhotonView���");
                }

                // ע��Photon�ص�
                PhotonNetwork.AddCallbackTarget(this);

                InitializeNetwork();
                LogDebug("NetworkManager �����Ѵ���");
            }
            else
            {
                LogDebug("�����ظ���NetworkManagerʵ��");
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            // �Ƴ�Photon�ص�ע��
            if (PhotonNetwork.NetworkingClient != null)
            {
                PhotonNetwork.RemoveCallbackTarget(this);
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (IsMainMenuScene(currentSceneName))
            {
                LogDebug("��⵽���˵�������NetworkManager���ִ���״̬");
                return;
            }

            InitializeFromMainMenu();
        }

        #region ��ʼ������

        /// <summary>
        /// ��ʼ���������
        /// </summary>
        private void InitializeNetwork()
        {
            // ������ڷ����У�����ͬ��״̬
            if (!PhotonNetwork.InRoom)
            {
                LogDebug("��ǰ����Photon������");
                return;
            }

            SyncPhotonState();
        }

        /// <summary>
        /// ͬ��Photon״̬
        /// </summary>
        private void SyncPhotonState()
        {
            if (PhotonNetwork.InRoom)
            {
                LogDebug($"ͬ��Photon״̬ - ����: {PhotonNetwork.CurrentRoom.Name}");

                if (PhotonNetwork.IsMasterClient)
                {
                    hostPlayerId = (ushort)PhotonNetwork.LocalPlayer.ActorNumber;
                    isHostClientReady = true;
                    LogDebug($"��⵽MasterClient��� - Host���ID: {hostPlayerId}");
                    OnHostStarted?.Invoke();
                    OnHostPlayerReady?.Invoke();
                }

                OnConnected?.Invoke();
            }
        }

        /// <summary>
        /// ����Ƿ�Ϊ���˵�����
        /// </summary>
        private bool IsMainMenuScene(string sceneName)
        {
            string[] mainMenuScenes = { "MainMenuScene", "MainMenu", "Menu", "Lobby" };
            foreach (string menuScene in mainMenuScenes)
            {
                if (sceneName.Equals(menuScene, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// �������˵�ѡ���ʼ��
        /// </summary>
        private void InitializeFromMainMenu()
        {
            LogDebug($"�������˵���ʼ����ѡ��ģʽ: {MainMenuManager.SelectedGameMode}");

            switch (MainMenuManager.SelectedGameMode)
            {
                case MainMenuManager.GameMode.Host:
                    InitializeAsHost();
                    break;

                case MainMenuManager.GameMode.Client:
                    InitializeAsClient();
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
        /// ��ʼ��Hostģʽ
        /// </summary>
        private void InitializeAsHost()
        {
            if (!PhotonNetwork.InRoom)
            {
                Debug.LogError("Hostģʽ��ʼ��ʧ�ܣ�δ��Photon������");
                return;
            }

            if (!PhotonNetwork.IsMasterClient)
            {
                Debug.LogError("Hostģʽ��ʼ��ʧ�ܣ�����MasterClient");
                return;
            }

            hostPlayerId = (ushort)PhotonNetwork.LocalPlayer.ActorNumber;
            isHostClientReady = true;
            ResetGameState();

            LogDebug($"Hostģʽ��ʼ����� - ���ID: {hostPlayerId}");
            OnHostStarted?.Invoke();
            OnHostPlayerReady?.Invoke();
            OnConnected?.Invoke();
        }

        /// <summary>
        /// ��ʼ��Clientģʽ
        /// </summary>
        private void InitializeAsClient()
        {
            if (!PhotonNetwork.InRoom)
            {
                Debug.LogError("Clientģʽ��ʼ��ʧ�ܣ�δ��Photon������");
                return;
            }

            ResetGameState();
            LogDebug($"Clientģʽ��ʼ����� - ���ID: {ClientId}");
            OnConnected?.Invoke();
        }

        /// <summary>
        /// ������Ϸ״̬
        /// </summary>
        private void ResetGameState()
        {
            gameStartReceived = false;
            lastTurnPlayerId = 0;
            gameProgressSequence = 0;
            isGameInProgress = false;
            LogDebug("��Ϸ״̬������");
        }

        #endregion

        #region Photon�¼��ص���ͨ���ӿ�ʵ�֣�

        // IConnectionCallbacks
        void IConnectionCallbacks.OnConnected()
        {
            LogDebug("Photon�������ӳɹ�");
        }

        void IConnectionCallbacks.OnConnectedToMaster()
        {
            LogDebug("���ӵ�Photon��������");
        }

        void IConnectionCallbacks.OnDisconnected(DisconnectCause cause)
        {
            LogDebug($"��Photon�Ͽ�����: {cause}");
            ResetGameState();
            OnDisconnected?.Invoke();
        }

        void IConnectionCallbacks.OnRegionListReceived(RegionHandler regionHandler)
        {
            // ����Ҫ����
        }

        void IConnectionCallbacks.OnCustomAuthenticationResponse(Dictionary<string, object> data)
        {
            // ����Ҫ����
        }

        void IConnectionCallbacks.OnCustomAuthenticationFailed(string debugMessage)
        {
            LogDebug($"Photon��֤ʧ��: {debugMessage}");
        }

        // IMatchmakingCallbacks
        void IMatchmakingCallbacks.OnFriendListUpdate(List<FriendInfo> friendList)
        {
            // ����Ҫ����
        }

        void IMatchmakingCallbacks.OnCreatedRoom()
        {
            LogDebug("Photon���䴴���ɹ�");
        }

        void IMatchmakingCallbacks.OnCreateRoomFailed(short returnCode, string message)
        {
            LogDebug($"Photon���䴴��ʧ��: {message}");
        }

        void IMatchmakingCallbacks.OnJoinedRoom()
        {
            LogDebug($"���뷿��ɹ�: {PhotonNetwork.CurrentRoom.Name}");
            SyncPhotonState();
        }

        void IMatchmakingCallbacks.OnJoinRoomFailed(short returnCode, string message)
        {
            LogDebug($"���뷿��ʧ��: {message}");
        }

        void IMatchmakingCallbacks.OnJoinRandomFailed(short returnCode, string message)
        {
            LogDebug($"�����������ʧ��: {message}");
        }

        void IMatchmakingCallbacks.OnLeftRoom()
        {
            LogDebug("�뿪����");
            ResetGameState();
            OnDisconnected?.Invoke();
        }

        // IInRoomCallbacks
        void IInRoomCallbacks.OnPlayerEnteredRoom(Player newPlayer)
        {
            ushort playerId = (ushort)newPlayer.ActorNumber;
            LogDebug($"��Ҽ���: {newPlayer.NickName} (ID: {playerId})");
            OnPlayerJoined?.Invoke(playerId);
        }

        void IInRoomCallbacks.OnPlayerLeftRoom(Player otherPlayer)
        {
            ushort playerId = (ushort)otherPlayer.ActorNumber;
            LogDebug($"����뿪: {otherPlayer.NickName} (ID: {playerId})");
            OnPlayerLeft?.Invoke(playerId);
        }

        void IInRoomCallbacks.OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
            LogDebug("�������Ը���");
        }

        void IInRoomCallbacks.OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            LogDebug($"������Ը���: {targetPlayer.NickName}");
        }

        void IInRoomCallbacks.OnMasterClientSwitched(Player newMasterClient)
        {
            LogDebug($"MasterClient�л���: {newMasterClient.NickName} (ID: {newMasterClient.ActorNumber})");

            if (PhotonNetwork.IsMasterClient)
            {
                // ������ҳ�Ϊ�µ�Host
                hostPlayerId = (ushort)PhotonNetwork.LocalPlayer.ActorNumber;
                isHostClientReady = true;
                LogDebug($"������ҳ�Ϊ��Host - ID: {hostPlayerId}");
                OnHostStarted?.Invoke();
                OnHostPlayerReady?.Invoke();
            }
            else
            {
                // ������ҳ�ΪHost
                hostPlayerId = (ushort)newMasterClient.ActorNumber;
                OnHostStopped?.Invoke();
            }
        }

        #endregion

        #region ��ȫ���ݵĹ����ӿڷ���

        /// <summary>
        /// ���ӵ������������ݷ�����
        /// </summary>
        public void Connect(string ip = null, ushort? serverPort = null)
        {
            LogDebug("Connect���������ã�����ǰʹ��Photon��������");
        }

        /// <summary>
        /// �Ͽ�����
        /// </summary>
        public void Disconnect()
        {
            LogDebug("�Ͽ�Photon��������");
            PhotonNetwork.LeaveRoom();
        }

        /// <summary>
        /// ��ȫ�ر�����
        /// </summary>
        public void Shutdown()
        {
            LogDebug("����������ر�");
            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LeaveRoom();
            }
        }

        /// <summary>
        /// ��ȡHost���ID������ushort��
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
        /// ��ȡ���ڷ��䴴����Host��Ϣ�����ݷ�����
        /// </summary>
        public (ushort hostId, bool isReady) GetHostRoomInfo()
        {
            return (hostPlayerId, isHostClientReady);
        }

        /// <summary>
        /// �ύ��
        /// </summary>
        public void SubmitAnswer(string answer)
        {
            if (!PhotonNetwork.InRoom)
            {
                Debug.LogWarning("δ�ڷ����У��޷��ύ��");
                return;
            }

            photonView.RPC("OnAnswerSubmitted", RpcTarget.MasterClient, (int)ClientId, answer);
            LogDebug($"�ύ��: {answer}");
        }

        /// <summary>
        /// ������Ŀ�����ݷ�����
        /// </summary>
        public void RequestQuestion()
        {
            LogDebug("RequestQuestion�����ã���Photonʹ��RPCϵͳ");
        }

        /// <summary>
        /// ��ȡ������Ϣ
        /// </summary>
        public string GetRoomInfo()
        {
            if (PhotonNetwork.InRoom)
            {
                return $"����: {RoomName} | ���: {ConnectedPlayerCount}/{MaxPlayers} | ���ID: {ClientId} | Host: {IsHost}";
            }
            return "δ�ڷ�����";
        }

        /// <summary>
        /// ��ȡ����״̬
        /// </summary>
        public string GetNetworkStatus()
        {
            return $"IsHost: {IsHost}, IsClient: {IsClient}, IsConnected: {IsConnected}, " +
                   $"ClientId: {ClientId}, HostPlayerId: {hostPlayerId}, HostClientReady: {isHostClientReady}";
        }

        #endregion

        #region ���ݵ���Ϣ���ͷ���

        /// <summary>
        /// ������Ϣ�����ݷ�����
        /// </summary>
        public void SendMessage(object message)
        {
            LogDebug("SendMessage�����ã���Photonʹ��RPCϵͳ");
        }

        /// <summary>
        /// �㲥��Ϣ�����ݷ�����
        /// </summary>
        public void BroadcastMessage(object message)
        {
            LogDebug("BroadcastMessage�����ã���Photonʹ��RPCϵͳ");
        }

        /// <summary>
        /// ������Ϣ��ָ���ͻ��ˣ����ݷ�����
        /// </summary>
        public void SendMessageToClient(ushort clientId, object message)
        {
            LogDebug($"SendMessageToClient�����ã���Photonʹ��RPCϵͳ");
        }

        #endregion

        #region ���������Ϣ�������ݷ�����

        /// <summary>
        /// ���ͷ������ݸ�ָ���ͻ��ˣ����ݷ�����
        /// </summary>
        public void SendRoomDataToClient(ushort clientId, RoomData roomData)
        {
            LogDebug($"SendRoomDataToClient������: �ͻ���{clientId}");
        }

        /// <summary>
        /// �㲥��Ҽ��뷿�䣨���ݷ�����
        /// </summary>
        public void BroadcastPlayerJoinRoom(RoomPlayer player)
        {
            LogDebug($"BroadcastPlayerJoinRoom������: {player.playerName}");
        }

        /// <summary>
        /// �㲥����뿪���䣨���ݷ�����
        /// </summary>
        public void BroadcastPlayerLeaveRoom(ushort playerId)
        {
            LogDebug($"BroadcastPlayerLeaveRoom������: ���{playerId}");
        }

        /// <summary>
        /// �㲥���׼��״̬���£����ݷ�����
        /// </summary>
        public void BroadcastPlayerReadyUpdate(ushort playerId, bool isReady)
        {
            LogDebug($"BroadcastPlayerReadyUpdate������: ���{playerId} -> {isReady}");
        }

        /// <summary>
        /// �㲥��Ϸ��ʼ������ݷ�����
        /// </summary>
        public void BroadcastGameStart()
        {
            LogDebug("BroadcastGameStart������");
        }

        /// <summary>
        /// ���󷿼���Ϣ�����ݷ�����
        /// </summary>
        public void RequestRoomInfo()
        {
            LogDebug("RequestRoomInfo������");
        }

        /// <summary>
        /// ����ı�׼��״̬�����ݷ�����
        /// </summary>
        public void RequestReadyStateChange(bool isReady)
        {
            LogDebug($"RequestReadyStateChange������: {isReady}");
        }

        #endregion

        #region RPC��Ϣ���մ���

        [PunRPC]
        void OnQuestionReceived_RPC(byte[] questionData)
        {
            NetworkQuestionData question = NetworkQuestionData.Deserialize(questionData);
            if (question != null)
            {
                LogDebug($"�յ���Ŀ: {question.questionType} - {question.questionText}");

                if (!gameStartReceived)
                {
                    Debug.LogWarning("�յ���Ŀ����Ϸ��δ��ʼ������");
                    return;
                }

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
                }
            }
        }

        [PunRPC]
        void OnAnswerResult_RPC(bool isCorrect, string correctAnswer)
        {
            LogDebug($"�յ�������: {(isCorrect ? "��ȷ" : "����")} - {correctAnswer}");
            OnAnswerResultReceived?.Invoke(isCorrect, correctAnswer);
        }

        [PunRPC]
        void OnHealthUpdate_RPC(int playerId, int newHealth, int maxHealth)
        {
            ushort playerIdUShort = (ushort)playerId;
            LogDebug($"�յ�Ѫ������: ���{playerIdUShort} {newHealth}/{maxHealth}");
            OnHealthUpdated?.Invoke(playerIdUShort, newHealth, maxHealth);

            // ת����NetworkUI
            var networkUI = GameObject.FindObjectOfType<NetworkUI>();
            if (networkUI != null)
            {
                networkUI.OnHealthUpdateReceived(playerIdUShort, newHealth, maxHealth);
            }
        }

        [PunRPC]
        void OnPlayerTurnChanged_RPC(int newTurnPlayerId)
        {
            ushort playerIdUShort = (ushort)newTurnPlayerId;
            LogDebug($"�յ��غϱ��: ���{playerIdUShort}");

            if (lastTurnPlayerId == playerIdUShort)
            {
                Debug.LogWarning($"�ظ��Ļغϱ����Ϣ: {playerIdUShort}");
                return;
            }

            lastTurnPlayerId = playerIdUShort;
            OnPlayerTurnChanged?.Invoke(playerIdUShort);

            // ת����NetworkUI
            var networkUI = GameObject.FindObjectOfType<NetworkUI>();
            if (networkUI != null)
            {
                networkUI.OnTurnChangedReceived(playerIdUShort);
            }

            // ֪ͨNQMC
            if (NetworkQuestionManagerController.Instance != null)
            {
                NetworkQuestionManagerController.Instance.GetType()
                    .GetMethod("OnNetworkPlayerTurnChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(NetworkQuestionManagerController.Instance, new object[] { playerIdUShort });
            }
        }

        [PunRPC]
        void OnGameStart_RPC(int totalPlayerCount, int alivePlayerCount, int firstTurnPlayerId)
        {
            ushort firstTurnPlayerIdUShort = (ushort)firstTurnPlayerId;
            LogDebug($"�յ���Ϸ��ʼ: �����{totalPlayerCount}, ���{alivePlayerCount}, �׻غ����{firstTurnPlayerIdUShort}");

            gameStartReceived = true;
            lastTurnPlayerId = 0;
            gameProgressSequence = 0;
            isGameInProgress = true;

            // ת����NetworkUI
            var networkUI = GameObject.FindObjectOfType<NetworkUI>();
            if (networkUI != null)
            {
                networkUI.OnGameStartReceived(totalPlayerCount, alivePlayerCount, firstTurnPlayerIdUShort);
            }
        }

        [PunRPC]
        void OnGameProgress_RPC(int questionNumber, int alivePlayerCount, int turnPlayerId, int questionType, float timeLimit)
        {
            ushort turnPlayerIdUShort = (ushort)turnPlayerId;
            LogDebug($"�յ���Ϸ����: ��{questionNumber}��, ���{alivePlayerCount}��, �غ����{turnPlayerIdUShort}");

            if (questionNumber < gameProgressSequence)
            {
                Debug.LogWarning($"�յ����ڵ���Ϸ������Ϣ: {questionNumber} < {gameProgressSequence}");
                return;
            }

            gameProgressSequence = questionNumber;

            // ת����NetworkUI
            var networkUI = GameObject.FindObjectOfType<NetworkUI>();
            if (networkUI != null)
            {
                networkUI.OnGameProgressReceived(questionNumber, alivePlayerCount, turnPlayerIdUShort);
            }
        }

        [PunRPC]
        void OnPlayerStateSync_RPC(int playerId, string playerName, bool isHost, int currentHealth, int maxHealth, bool isAlive)
        {
            ushort playerIdUShort = (ushort)playerId;
            LogDebug($"�յ����״̬ͬ��: {playerName} (ID:{playerIdUShort}) HP:{currentHealth}/{maxHealth}");

            var networkUI = GameObject.FindObjectOfType<NetworkUI>();
            if (networkUI != null)
            {
                networkUI.OnPlayerStateSyncReceived(playerIdUShort, playerName, isHost, currentHealth, maxHealth, isAlive);
            }
        }

        [PunRPC]
        void OnPlayerAnswerResult_RPC(int playerId, bool isCorrect, string answer)
        {
            ushort playerIdUShort = (ushort)playerId;
            LogDebug($"�յ���Ҵ�����: ���{playerIdUShort} {(isCorrect ? "��ȷ" : "����")} - {answer}");

            var networkUI = GameObject.FindObjectOfType<NetworkUI>();
            if (networkUI != null)
            {
                networkUI.OnPlayerAnswerResultReceived(playerIdUShort, isCorrect, answer);
            }
        }

        [PunRPC]
        void OnAnswerSubmitted(int playerId, string answer)
        {
            // ֻ��Host������ύ
            if (!IsHost)
                return;

            ushort playerIdUShort = (ushort)playerId;
            LogDebug($"�յ����ύ: ���{playerIdUShort} - {answer}");

            // ת����HostGameManager����
            if (HostGameManager.Instance != null)
            {
                HostGameManager.Instance.HandlePlayerAnswer(playerIdUShort, answer);
            }
        }

        #endregion

        #region ���������ط������򻯰棩

        /// <summary>
        /// �������׼��״̬��ͨ��Photon�Զ������ԣ�
        /// </summary>
        public void SetPlayerReady(bool isReady)
        {
            var props = new Hashtable();
            props["isReady"] = isReady;
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
            LogDebug($"����׼��״̬: {isReady}");
        }

        /// <summary>
        /// ��ȡ���׼��״̬
        /// </summary>
        public bool GetPlayerReady(Player player)
        {
            if (player.CustomProperties.TryGetValue("isReady", out object isReady))
            {
                return (bool)isReady;
            }
            return false;
        }

        /// <summary>
        /// �����������Ƿ�׼������
        /// </summary>
        public bool AreAllPlayersReady()
        {
            foreach (var player in PhotonNetwork.PlayerList)
            {
                if (!GetPlayerReady(player))
                    return false;
            }
            return true;
        }

        #endregion

        #region IPunObservableʵ��

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                // ������Ϸ״̬���ݣ������Ҫ�Ļ���
                stream.SendNext(isGameInProgress);
                stream.SendNext(gameProgressSequence);
            }
            else
            {
                // ������Ϸ״̬����
                isGameInProgress = (bool)stream.ReceiveNext();
                gameProgressSequence = (int)stream.ReceiveNext();
            }
        }

        #endregion

        #region ���Է���

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[NetworkManager] {message}");
            }
        }

        [ContextMenu("��ʾ����״̬")]
        public void ShowNetworkStatus()
        {
            Debug.Log($"=== ����״̬ ===\n{GetNetworkStatus()}");
        }

        [ContextMenu("��ʾ������Ϣ")]
        public void ShowRoomInfo()
        {
            Debug.Log($"=== ������Ϣ ===\n{GetRoomInfo()}");
        }

        [ContextMenu("������Ϸ״̬")]
        public void DebugResetGameState()
        {
            ResetGameState();
        }

        #endregion
    }
}