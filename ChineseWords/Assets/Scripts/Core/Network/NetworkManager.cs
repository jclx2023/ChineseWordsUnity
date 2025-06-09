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
    /// 基于Photon的网络管理器（完整兼容版）
    /// 负责游戏内网络通信、玩家管理、RPC消息处理
    /// </summary>
    public class NetworkManager : MonoBehaviourPun, IPunObservable, IConnectionCallbacks, IMatchmakingCallbacks, IInRoomCallbacks
    {
        [Header("网络配置")]
        [SerializeField] private ushort maxClients = 8;
        [SerializeField] private bool enableDebugLogs = true;

        public static NetworkManager Instance { get; private set; }

        // === 网络状态属性（完全兼容） ===
        public bool IsHost => PhotonNetwork.IsMasterClient;
        public bool IsClient => PhotonNetwork.IsConnected && PhotonNetwork.InRoom;
        public bool IsConnected => IsClient;

        // 主要ID属性 - 保持ushort兼容
        public ushort ClientId => (ushort)PhotonNetwork.LocalPlayer.ActorNumber;
        public ushort Port => 7777; // 兼容属性

        // 房间信息属性
        public string RoomName => PhotonNetwork.CurrentRoom?.Name ?? "";
        public int MaxPlayers => PhotonNetwork.CurrentRoom?.MaxPlayers ?? 0;
        public int ConnectedPlayerCount => PhotonNetwork.CurrentRoom?.PlayerCount ?? 0;

        // Host身份管理（兼容ushort）
        private ushort hostPlayerId = 0;
        private bool isHostClientReady = false;

        // 游戏状态管理
        private bool isGameInProgress = false;
        private ushort lastTurnPlayerId = 0;
        private int gameProgressSequence = 0;
        private bool gameStartReceived = false;

        // === 完全兼容的事件系统 ===
        public static event Action OnConnected;
        public static event Action OnDisconnected;
        public static event Action<NetworkQuestionData> OnQuestionReceived;
        public static event Action<bool, string> OnAnswerResultReceived;
        public static event Action<ushort, int, int> OnHealthUpdated;  // 保持ushort
        public static event Action<ushort> OnPlayerTurnChanged;        // 保持ushort

        // Host特有事件（保持ushort兼容）
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

                // 确保有PhotonView组件
                if (GetComponent<PhotonView>() == null)
                {
                    gameObject.AddComponent<PhotonView>();
                    LogDebug("自动添加PhotonView组件");
                }

                // 注册Photon回调
                PhotonNetwork.AddCallbackTarget(this);

                InitializeNetwork();
                LogDebug("NetworkManager 单例已创建");
            }
            else
            {
                LogDebug("销毁重复的NetworkManager实例");
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            // 移除Photon回调注册
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
                LogDebug("检测到主菜单场景，NetworkManager保持待命状态");
                return;
            }

            InitializeFromMainMenu();
        }

        #region 初始化方法

        /// <summary>
        /// 初始化网络组件
        /// </summary>
        private void InitializeNetwork()
        {
            // 如果不在房间中，尝试同步状态
            if (!PhotonNetwork.InRoom)
            {
                LogDebug("当前不在Photon房间中");
                return;
            }

            SyncPhotonState();
        }

        /// <summary>
        /// 同步Photon状态
        /// </summary>
        private void SyncPhotonState()
        {
            if (PhotonNetwork.InRoom)
            {
                LogDebug($"同步Photon状态 - 房间: {PhotonNetwork.CurrentRoom.Name}");

                if (PhotonNetwork.IsMasterClient)
                {
                    hostPlayerId = (ushort)PhotonNetwork.LocalPlayer.ActorNumber;
                    isHostClientReady = true;
                    LogDebug($"检测到MasterClient身份 - Host玩家ID: {hostPlayerId}");
                    OnHostStarted?.Invoke();
                    OnHostPlayerReady?.Invoke();
                }

                OnConnected?.Invoke();
            }
        }

        /// <summary>
        /// 检查是否为主菜单场景
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
        /// 根据主菜单选择初始化
        /// </summary>
        private void InitializeFromMainMenu()
        {
            LogDebug($"根据主菜单初始化，选定模式: {MainMenuManager.SelectedGameMode}");

            switch (MainMenuManager.SelectedGameMode)
            {
                case MainMenuManager.GameMode.Host:
                    InitializeAsHost();
                    break;

                case MainMenuManager.GameMode.Client:
                    InitializeAsClient();
                    break;

                case MainMenuManager.GameMode.SinglePlayer:
                    LogDebug("单机模式，不需要网络连接");
                    break;

                default:
                    Debug.LogWarning("未知的游戏模式");
                    break;
            }
        }

        /// <summary>
        /// 初始化Host模式
        /// </summary>
        private void InitializeAsHost()
        {
            if (!PhotonNetwork.InRoom)
            {
                Debug.LogError("Host模式初始化失败：未在Photon房间中");
                return;
            }

            if (!PhotonNetwork.IsMasterClient)
            {
                Debug.LogError("Host模式初始化失败：不是MasterClient");
                return;
            }

            hostPlayerId = (ushort)PhotonNetwork.LocalPlayer.ActorNumber;
            isHostClientReady = true;
            ResetGameState();

            LogDebug($"Host模式初始化完成 - 玩家ID: {hostPlayerId}");
            OnHostStarted?.Invoke();
            OnHostPlayerReady?.Invoke();
            OnConnected?.Invoke();
        }

        /// <summary>
        /// 初始化Client模式
        /// </summary>
        private void InitializeAsClient()
        {
            if (!PhotonNetwork.InRoom)
            {
                Debug.LogError("Client模式初始化失败：未在Photon房间中");
                return;
            }

            ResetGameState();
            LogDebug($"Client模式初始化完成 - 玩家ID: {ClientId}");
            OnConnected?.Invoke();
        }

        /// <summary>
        /// 重置游戏状态
        /// </summary>
        private void ResetGameState()
        {
            gameStartReceived = false;
            lastTurnPlayerId = 0;
            gameProgressSequence = 0;
            isGameInProgress = false;
            LogDebug("游戏状态已重置");
        }

        #endregion

        #region Photon事件回调（通过接口实现）

        // IConnectionCallbacks
        void IConnectionCallbacks.OnConnected()
        {
            LogDebug("Photon网络连接成功");
        }

        void IConnectionCallbacks.OnConnectedToMaster()
        {
            LogDebug("连接到Photon主服务器");
        }

        void IConnectionCallbacks.OnDisconnected(DisconnectCause cause)
        {
            LogDebug($"与Photon断开连接: {cause}");
            ResetGameState();
            OnDisconnected?.Invoke();
        }

        void IConnectionCallbacks.OnRegionListReceived(RegionHandler regionHandler)
        {
            // 不需要处理
        }

        void IConnectionCallbacks.OnCustomAuthenticationResponse(Dictionary<string, object> data)
        {
            // 不需要处理
        }

        void IConnectionCallbacks.OnCustomAuthenticationFailed(string debugMessage)
        {
            LogDebug($"Photon认证失败: {debugMessage}");
        }

        // IMatchmakingCallbacks
        void IMatchmakingCallbacks.OnFriendListUpdate(List<FriendInfo> friendList)
        {
            // 不需要处理
        }

        void IMatchmakingCallbacks.OnCreatedRoom()
        {
            LogDebug("Photon房间创建成功");
        }

        void IMatchmakingCallbacks.OnCreateRoomFailed(short returnCode, string message)
        {
            LogDebug($"Photon房间创建失败: {message}");
        }

        void IMatchmakingCallbacks.OnJoinedRoom()
        {
            LogDebug($"加入房间成功: {PhotonNetwork.CurrentRoom.Name}");
            SyncPhotonState();
        }

        void IMatchmakingCallbacks.OnJoinRoomFailed(short returnCode, string message)
        {
            LogDebug($"加入房间失败: {message}");
        }

        void IMatchmakingCallbacks.OnJoinRandomFailed(short returnCode, string message)
        {
            LogDebug($"加入随机房间失败: {message}");
        }

        void IMatchmakingCallbacks.OnLeftRoom()
        {
            LogDebug("离开房间");
            ResetGameState();
            OnDisconnected?.Invoke();
        }

        // IInRoomCallbacks
        void IInRoomCallbacks.OnPlayerEnteredRoom(Player newPlayer)
        {
            ushort playerId = (ushort)newPlayer.ActorNumber;
            LogDebug($"玩家加入: {newPlayer.NickName} (ID: {playerId})");
            OnPlayerJoined?.Invoke(playerId);
        }

        void IInRoomCallbacks.OnPlayerLeftRoom(Player otherPlayer)
        {
            ushort playerId = (ushort)otherPlayer.ActorNumber;
            LogDebug($"玩家离开: {otherPlayer.NickName} (ID: {playerId})");
            OnPlayerLeft?.Invoke(playerId);
        }

        void IInRoomCallbacks.OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
            LogDebug("房间属性更新");
        }

        void IInRoomCallbacks.OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            LogDebug($"玩家属性更新: {targetPlayer.NickName}");
        }

        void IInRoomCallbacks.OnMasterClientSwitched(Player newMasterClient)
        {
            LogDebug($"MasterClient切换到: {newMasterClient.NickName} (ID: {newMasterClient.ActorNumber})");

            if (PhotonNetwork.IsMasterClient)
            {
                // 本地玩家成为新的Host
                hostPlayerId = (ushort)PhotonNetwork.LocalPlayer.ActorNumber;
                isHostClientReady = true;
                LogDebug($"本地玩家成为新Host - ID: {hostPlayerId}");
                OnHostStarted?.Invoke();
                OnHostPlayerReady?.Invoke();
            }
            else
            {
                // 其他玩家成为Host
                hostPlayerId = (ushort)newMasterClient.ActorNumber;
                OnHostStopped?.Invoke();
            }
        }

        #endregion

        #region 完全兼容的公共接口方法

        /// <summary>
        /// 连接到服务器（兼容方法）
        /// </summary>
        public void Connect(string ip = null, ushort? serverPort = null)
        {
            LogDebug("Connect方法被调用，但当前使用Photon管理连接");
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            LogDebug("断开Photon房间连接");
            PhotonNetwork.LeaveRoom();
        }

        /// <summary>
        /// 完全关闭网络
        /// </summary>
        public void Shutdown()
        {
            LogDebug("网络管理器关闭");
            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LeaveRoom();
            }
        }

        /// <summary>
        /// 获取Host玩家ID（兼容ushort）
        /// </summary>
        public ushort GetHostPlayerId()
        {
            return hostPlayerId;
        }

        /// <summary>
        /// 检查指定ID是否为Host玩家
        /// </summary>
        public bool IsHostPlayer(ushort playerId)
        {
            return IsHost && playerId == hostPlayerId;
        }

        /// <summary>
        /// Host客户端是否准备就绪
        /// </summary>
        public bool IsHostClientReady => isHostClientReady;

        /// <summary>
        /// 获取用于房间创建的Host信息（兼容方法）
        /// </summary>
        public (ushort hostId, bool isReady) GetHostRoomInfo()
        {
            return (hostPlayerId, isHostClientReady);
        }

        /// <summary>
        /// 提交答案
        /// </summary>
        public void SubmitAnswer(string answer)
        {
            if (!PhotonNetwork.InRoom)
            {
                Debug.LogWarning("未在房间中，无法提交答案");
                return;
            }

            photonView.RPC("OnAnswerSubmitted", RpcTarget.MasterClient, (int)ClientId, answer);
            LogDebug($"提交答案: {answer}");
        }

        /// <summary>
        /// 请求题目（兼容方法）
        /// </summary>
        public void RequestQuestion()
        {
            LogDebug("RequestQuestion被调用，但Photon使用RPC系统");
        }

        /// <summary>
        /// 获取房间信息
        /// </summary>
        public string GetRoomInfo()
        {
            if (PhotonNetwork.InRoom)
            {
                return $"房间: {RoomName} | 玩家: {ConnectedPlayerCount}/{MaxPlayers} | 玩家ID: {ClientId} | Host: {IsHost}";
            }
            return "未在房间中";
        }

        /// <summary>
        /// 获取网络状态
        /// </summary>
        public string GetNetworkStatus()
        {
            return $"IsHost: {IsHost}, IsClient: {IsClient}, IsConnected: {IsConnected}, " +
                   $"ClientId: {ClientId}, HostPlayerId: {hostPlayerId}, HostClientReady: {isHostClientReady}";
        }

        #endregion

        #region 兼容的消息发送方法

        /// <summary>
        /// 发送消息（兼容方法）
        /// </summary>
        public void SendMessage(object message)
        {
            LogDebug("SendMessage被调用，但Photon使用RPC系统");
        }

        /// <summary>
        /// 广播消息（兼容方法）
        /// </summary>
        public void BroadcastMessage(object message)
        {
            LogDebug("BroadcastMessage被调用，但Photon使用RPC系统");
        }

        /// <summary>
        /// 发送消息给指定客户端（兼容方法）
        /// </summary>
        public void SendMessageToClient(ushort clientId, object message)
        {
            LogDebug($"SendMessageToClient被调用，但Photon使用RPC系统");
        }

        #endregion

        #region 房间管理消息处理（兼容方法）

        /// <summary>
        /// 发送房间数据给指定客户端（兼容方法）
        /// </summary>
        public void SendRoomDataToClient(ushort clientId, RoomData roomData)
        {
            LogDebug($"SendRoomDataToClient被调用: 客户端{clientId}");
        }

        /// <summary>
        /// 广播玩家加入房间（兼容方法）
        /// </summary>
        public void BroadcastPlayerJoinRoom(RoomPlayer player)
        {
            LogDebug($"BroadcastPlayerJoinRoom被调用: {player.playerName}");
        }

        /// <summary>
        /// 广播玩家离开房间（兼容方法）
        /// </summary>
        public void BroadcastPlayerLeaveRoom(ushort playerId)
        {
            LogDebug($"BroadcastPlayerLeaveRoom被调用: 玩家{playerId}");
        }

        /// <summary>
        /// 广播玩家准备状态更新（兼容方法）
        /// </summary>
        public void BroadcastPlayerReadyUpdate(ushort playerId, bool isReady)
        {
            LogDebug($"BroadcastPlayerReadyUpdate被调用: 玩家{playerId} -> {isReady}");
        }

        /// <summary>
        /// 广播游戏开始命令（兼容方法）
        /// </summary>
        public void BroadcastGameStart()
        {
            LogDebug("BroadcastGameStart被调用");
        }

        /// <summary>
        /// 请求房间信息（兼容方法）
        /// </summary>
        public void RequestRoomInfo()
        {
            LogDebug("RequestRoomInfo被调用");
        }

        /// <summary>
        /// 请求改变准备状态（兼容方法）
        /// </summary>
        public void RequestReadyStateChange(bool isReady)
        {
            LogDebug($"RequestReadyStateChange被调用: {isReady}");
        }

        #endregion

        #region RPC消息接收处理

        [PunRPC]
        void OnQuestionReceived_RPC(byte[] questionData)
        {
            NetworkQuestionData question = NetworkQuestionData.Deserialize(questionData);
            if (question != null)
            {
                LogDebug($"收到题目: {question.questionType} - {question.questionText}");

                if (!gameStartReceived)
                {
                    Debug.LogWarning("收到题目但游戏尚未开始，忽略");
                    return;
                }

                OnQuestionReceived?.Invoke(question);

                // 直接通知NQMC
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
            LogDebug($"收到答题结果: {(isCorrect ? "正确" : "错误")} - {correctAnswer}");
            OnAnswerResultReceived?.Invoke(isCorrect, correctAnswer);
        }

        [PunRPC]
        void OnHealthUpdate_RPC(int playerId, int newHealth, int maxHealth)
        {
            ushort playerIdUShort = (ushort)playerId;
            LogDebug($"收到血量更新: 玩家{playerIdUShort} {newHealth}/{maxHealth}");
            OnHealthUpdated?.Invoke(playerIdUShort, newHealth, maxHealth);

            // 转发给NetworkUI
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
            LogDebug($"收到回合变更: 玩家{playerIdUShort}");

            if (lastTurnPlayerId == playerIdUShort)
            {
                Debug.LogWarning($"重复的回合变更消息: {playerIdUShort}");
                return;
            }

            lastTurnPlayerId = playerIdUShort;
            OnPlayerTurnChanged?.Invoke(playerIdUShort);

            // 转发给NetworkUI
            var networkUI = GameObject.FindObjectOfType<NetworkUI>();
            if (networkUI != null)
            {
                networkUI.OnTurnChangedReceived(playerIdUShort);
            }

            // 通知NQMC
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
            LogDebug($"收到游戏开始: 总玩家{totalPlayerCount}, 存活{alivePlayerCount}, 首回合玩家{firstTurnPlayerIdUShort}");

            gameStartReceived = true;
            lastTurnPlayerId = 0;
            gameProgressSequence = 0;
            isGameInProgress = true;

            // 转发给NetworkUI
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
            LogDebug($"收到游戏进度: 第{questionNumber}题, 存活{alivePlayerCount}人, 回合玩家{turnPlayerIdUShort}");

            if (questionNumber < gameProgressSequence)
            {
                Debug.LogWarning($"收到过期的游戏进度消息: {questionNumber} < {gameProgressSequence}");
                return;
            }

            gameProgressSequence = questionNumber;

            // 转发给NetworkUI
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
            LogDebug($"收到玩家状态同步: {playerName} (ID:{playerIdUShort}) HP:{currentHealth}/{maxHealth}");

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
            LogDebug($"收到玩家答题结果: 玩家{playerIdUShort} {(isCorrect ? "正确" : "错误")} - {answer}");

            var networkUI = GameObject.FindObjectOfType<NetworkUI>();
            if (networkUI != null)
            {
                networkUI.OnPlayerAnswerResultReceived(playerIdUShort, isCorrect, answer);
            }
        }

        [PunRPC]
        void OnAnswerSubmitted(int playerId, string answer)
        {
            // 只有Host处理答案提交
            if (!IsHost)
                return;

            ushort playerIdUShort = (ushort)playerId;
            LogDebug($"收到答案提交: 玩家{playerIdUShort} - {answer}");

            // 转发给HostGameManager处理
            if (HostGameManager.Instance != null)
            {
                HostGameManager.Instance.HandlePlayerAnswer(playerIdUShort, answer);
            }
        }

        #endregion

        #region 房间管理相关方法（简化版）

        /// <summary>
        /// 设置玩家准备状态（通过Photon自定义属性）
        /// </summary>
        public void SetPlayerReady(bool isReady)
        {
            var props = new Hashtable();
            props["isReady"] = isReady;
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
            LogDebug($"设置准备状态: {isReady}");
        }

        /// <summary>
        /// 获取玩家准备状态
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
        /// 检查所有玩家是否准备就绪
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

        #region IPunObservable实现

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                // 发送游戏状态数据（如果需要的话）
                stream.SendNext(isGameInProgress);
                stream.SendNext(gameProgressSequence);
            }
            else
            {
                // 接收游戏状态数据
                isGameInProgress = (bool)stream.ReceiveNext();
                gameProgressSequence = (int)stream.ReceiveNext();
            }
        }

        #endregion

        #region 调试方法

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[NetworkManager] {message}");
            }
        }

        [ContextMenu("显示网络状态")]
        public void ShowNetworkStatus()
        {
            Debug.Log($"=== 网络状态 ===\n{GetNetworkStatus()}");
        }

        [ContextMenu("显示房间信息")]
        public void ShowRoomInfo()
        {
            Debug.Log($"=== 房间信息 ===\n{GetRoomInfo()}");
        }

        [ContextMenu("重置游戏状态")]
        public void DebugResetGameState()
        {
            ResetGameState();
        }

        #endregion
    }
}