using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System;
using UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Core.Network
{
    /// <summary>
    /// 持久化网络管理器 - 专注于游戏内RPC通信
    /// 移除单例模式，改为由PersistentNetworkManager管理
    /// 适用场景：RoomScene 和 NetworkGameScene
    /// 职责：玩家状态同步、游戏内RPC消息处理
    /// </summary>
    public class NetworkManager : MonoBehaviourPun, IInRoomCallbacks
    {
        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 移除单例模式，改为通过PersistentNetworkManager访问
        public static NetworkManager Instance => PersistentNetworkManager.Instance?.GameNetwork;

        #region 核心状态属性

        // 网络状态（只保留必要的）
        public bool IsHost => PhotonNetwork.IsMasterClient;
        public bool IsConnected => PhotonNetwork.IsConnected && PhotonNetwork.InRoom;
        public ushort ClientId => (ushort)PhotonNetwork.LocalPlayer.ActorNumber;

        // 房间信息
        public string RoomName => PhotonNetwork.CurrentRoom?.Name ?? "";
        public int PlayerCount => PhotonNetwork.CurrentRoom?.PlayerCount ?? 0;
        public int MaxPlayers => PhotonNetwork.CurrentRoom?.MaxPlayers ?? 0;

        // Host身份管理
        public ushort HostPlayerId => (ushort)(PhotonNetwork.MasterClient?.ActorNumber ?? 0);
        public bool IsHostPlayer(ushort playerId) => IsHost && playerId == ClientId;

        #endregion

        #region 游戏内事件系统

        // 核心游戏事件（只保留游戏内需要的）
        public static event Action<NetworkQuestionData> OnQuestionReceived;
        public static event Action<bool, string> OnAnswerResultReceived;
        public static event Action<ushort, int, int> OnHealthUpdated;
        public static event Action<ushort> OnPlayerTurnChanged;
        public static event Action<int, int, ushort> OnGameStarted; // 总玩家数, 存活数, 首回合玩家

        // 房间事件（RoomScene需要）
        public static event Action<ushort> OnPlayerJoined;
        public static event Action<ushort> OnPlayerLeft;
        public static event Action<ushort, bool> OnPlayerReadyChanged;

        #endregion

        #region Unity生命周期

        private void Awake()
        {
            // 移除单例逻辑，改为由PersistentNetworkManager管理
            LogDebug("NetworkManager 已初始化（持久化版本）");
        }

        private void Start()
        {
            // 注册Photon回调
            PhotonNetwork.AddCallbackTarget(this);

            // 验证网络状态
            if (!PhotonNetwork.InRoom)
            {
                LogDebug("启动时未在Photon房间中，这是正常的（可能在其他场景）");
            }
            else
            {
                LogDebug($"NetworkManager已启动 - 房间: {RoomName}, 玩家ID: {ClientId}, 是否Host: {IsHost}");
            }
        }

        private void OnDestroy()
        {
            // 清理Photon回调
            if (PhotonNetwork.NetworkingClient != null)
            {
                PhotonNetwork.RemoveCallbackTarget(this);
            }

            LogDebug("NetworkManager已销毁");
        }

        #endregion

        #region Photon房间事件处理

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

        void IInRoomCallbacks.OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            // 处理玩家准备状态变更
            if (changedProps.TryGetValue("isReady", out object isReadyObj))
            {
                ushort playerId = (ushort)targetPlayer.ActorNumber;
                bool isReady = (bool)isReadyObj;
                LogDebug($"玩家准备状态更新: {targetPlayer.NickName} -> {isReady}");
                OnPlayerReadyChanged?.Invoke(playerId, isReady);
            }
        }

        void IInRoomCallbacks.OnMasterClientSwitched(Player newMasterClient)
        {
            LogDebug($"Host切换到: {newMasterClient.NickName} (ID: {newMasterClient.ActorNumber})");
            // Host切换时可能需要通知其他系统
        }

        void IInRoomCallbacks.OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
            // 处理房间属性变更（如游戏开始状态等）
        }

        #endregion

        #region 玩家准备状态管理

        /// <summary>
        /// 设置本地玩家准备状态
        /// </summary>
        public void SetPlayerReady(bool isReady)
        {
            if (!PhotonNetwork.InRoom)
            {
                Debug.LogWarning("[NetworkManager] 未在房间中，无法设置准备状态");
                return;
            }

            var props = new Hashtable { ["isReady"] = isReady };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
            LogDebug($"设置准备状态: {isReady}");
        }

        /// <summary>
        /// 获取指定玩家的准备状态
        /// </summary>
        public bool GetPlayerReady(Player player)
        {
            if (player?.CustomProperties?.TryGetValue("isReady", out object isReady) == true)
            {
                return (bool)isReady;
            }
            return false;
        }

        /// <summary>
        /// 获取指定ID玩家的准备状态
        /// </summary>
        public bool GetPlayerReady(ushort playerId)
        {
            var player = GetPlayerById(playerId);
            return player != null && GetPlayerReady(player);
        }

        /// <summary>
        /// 检查所有玩家是否都准备就绪
        /// </summary>
        public bool AreAllPlayersReady()
        {
            if (!PhotonNetwork.InRoom) return false;

            foreach (var player in PhotonNetwork.PlayerList)
            {
                if (!GetPlayerReady(player))
                {
                    return false;
                }
            }
            return PhotonNetwork.PlayerList.Length > 1; // 至少需要2个玩家
        }

        /// <summary>
        /// 获取准备就绪的玩家数量
        /// </summary>
        public int GetReadyPlayerCount()
        {
            if (!PhotonNetwork.InRoom) return 0;

            int readyCount = 0;
            foreach (var player in PhotonNetwork.PlayerList)
            {
                if (GetPlayerReady(player))
                {
                    readyCount++;
                }
            }
            return readyCount;
        }

        #endregion

        #region 游戏内RPC方法

        /// <summary>
        /// 提交答案（Client → Host）
        /// </summary>
        public void SubmitAnswer(string answer)
        {
            if (!PhotonNetwork.InRoom)
            {
                Debug.LogWarning("[NetworkManager] 未在房间中，无法提交答案");
                return;
            }

            // 使用PersistentNetworkManager的PhotonView发送RPC
            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnAnswerSubmitted", RpcTarget.MasterClient, (int)ClientId, answer);
                LogDebug($"提交答案: {answer}");
            }
            else
            {
                Debug.LogError("[NetworkManager] 无法发送RPC，PersistentNetworkManager或PhotonView不可用");
            }
        }

        [PunRPC]
        void OnAnswerSubmitted(int playerId, string answer)
        {
            // 只有Host处理答案提交
            if (!IsHost) return;

            ushort playerIdUShort = (ushort)playerId;
            LogDebug($"收到答案提交: 玩家{playerIdUShort} - {answer}");

            // 转发给HostGameManager处理
            if (HostGameManager.Instance != null)
            {
                HostGameManager.Instance.HandlePlayerAnswer(playerIdUShort, answer);
            }
        }

        [PunRPC]
        void OnQuestionReceived_RPC(byte[] questionData)
        {
            var question = NetworkQuestionData.Deserialize(questionData);
            if (question != null)
            {
                LogDebug($"收到题目: {question.questionType} - {question.questionText}");
                OnQuestionReceived?.Invoke(question);

                // 通知NQMC
                NotifyNQMCQuestionReceived(question);
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
            NotifyNetworkUIHealthUpdate(playerIdUShort, newHealth, maxHealth);
        }

        [PunRPC]
        void OnPlayerTurnChanged_RPC(int newTurnPlayerId)
        {
            ushort playerIdUShort = (ushort)newTurnPlayerId;
            LogDebug($"收到回合变更: 玩家{playerIdUShort}");
            OnPlayerTurnChanged?.Invoke(playerIdUShort);

            // 转发给NetworkUI和NQMC
            NotifyNetworkUITurnChanged(playerIdUShort);
            NotifyNQMCTurnChanged(playerIdUShort);
        }

        [PunRPC]
        void OnGameStart_RPC(int totalPlayerCount, int alivePlayerCount, int firstTurnPlayerId)
        {
            ushort firstTurnPlayerIdUShort = (ushort)firstTurnPlayerId;
            LogDebug($"收到游戏开始: 总玩家{totalPlayerCount}, 存活{alivePlayerCount}, 首回合玩家{firstTurnPlayerIdUShort}");

            OnGameStarted?.Invoke(totalPlayerCount, alivePlayerCount, firstTurnPlayerIdUShort);

            // 转发给NetworkUI
            NotifyNetworkUIGameStart(totalPlayerCount, alivePlayerCount, firstTurnPlayerIdUShort);
        }

        [PunRPC]
        void OnGameProgress_RPC(int questionNumber, int alivePlayerCount, int turnPlayerId, int questionType, float timeLimit)
        {
            ushort turnPlayerIdUShort = (ushort)turnPlayerId;
            LogDebug($"收到游戏进度: 第{questionNumber}题, 存活{alivePlayerCount}人, 回合玩家{turnPlayerIdUShort}");

            // 转发给NetworkUI
            NotifyNetworkUIGameProgress(questionNumber, alivePlayerCount, turnPlayerIdUShort);
        }

        [PunRPC]
        void OnPlayerStateSync_RPC(int playerId, string playerName, bool isHost, int currentHealth, int maxHealth, bool isAlive)
        {
            ushort playerIdUShort = (ushort)playerId;
            LogDebug($"收到玩家状态同步: {playerName} (ID:{playerIdUShort}) HP:{currentHealth}/{maxHealth}");

            // 转发给NetworkUI
            NotifyNetworkUIPlayerStateSync(playerIdUShort, playerName, isHost, currentHealth, maxHealth, isAlive);
        }

        [PunRPC]
        void OnPlayerAnswerResult_RPC(int playerId, bool isCorrect, string answer)
        {
            ushort playerIdUShort = (ushort)playerId;
            LogDebug($"收到玩家答题结果: 玩家{playerIdUShort} {(isCorrect ? "正确" : "错误")} - {answer}");

            // 转发给NetworkUI
            NotifyNetworkUIPlayerAnswerResult(playerIdUShort, isCorrect, answer);
        }

        #endregion

        #region Host专用RPC发送方法

        /// <summary>
        /// 广播题目（Host → All）
        /// </summary>
        public void BroadcastQuestion(NetworkQuestionData question)
        {
            if (!IsHost)
            {
                Debug.LogWarning("[NetworkManager] 只有Host可以广播题目");
                return;
            }

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                byte[] questionData = question.Serialize();
                persistentManager.photonView.RPC("OnQuestionReceived_RPC", RpcTarget.Others, questionData);
                LogDebug($"广播题目: {question.questionType}");
            }
            else
            {
                Debug.LogError("[NetworkManager] 无法广播题目，PersistentNetworkManager或PhotonView不可用");
            }
        }

        /// <summary>
        /// 广播答题结果（Host → All）
        /// </summary>
        public void BroadcastAnswerResult(bool isCorrect, string correctAnswer)
        {
            if (!IsHost) return;

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnAnswerResult_RPC", RpcTarget.Others, isCorrect, correctAnswer);
                LogDebug($"广播答题结果: {(isCorrect ? "正确" : "错误")}");
            }
        }

        /// <summary>
        /// 广播血量更新（Host → All）
        /// </summary>
        public void BroadcastHealthUpdate(ushort playerId, int newHealth, int maxHealth)
        {
            if (!IsHost) return;

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnHealthUpdate_RPC", RpcTarget.Others, (int)playerId, newHealth, maxHealth);
                LogDebug($"广播血量更新: 玩家{playerId} {newHealth}/{maxHealth}");
            }
        }

        /// <summary>
        /// 广播回合变更（Host → All）
        /// </summary>
        public void BroadcastPlayerTurnChanged(ushort newTurnPlayerId)
        {
            if (!IsHost) return;

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnPlayerTurnChanged_RPC", RpcTarget.Others, (int)newTurnPlayerId);
                LogDebug($"广播回合变更: 玩家{newTurnPlayerId}");
            }
        }

        /// <summary>
        /// 广播游戏开始（Host → All）
        /// </summary>
        public void BroadcastGameStart(int totalPlayerCount, int alivePlayerCount, ushort firstTurnPlayerId)
        {
            if (!IsHost) return;

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnGameStart_RPC", RpcTarget.Others, totalPlayerCount, alivePlayerCount, (int)firstTurnPlayerId);
                LogDebug($"广播游戏开始: 总玩家{totalPlayerCount}, 首回合玩家{firstTurnPlayerId}");
            }
        }

        /// <summary>
        /// 广播游戏进度（Host → All）
        /// </summary>
        public void BroadcastGameProgress(int questionNumber, int alivePlayerCount, ushort turnPlayerId, int questionType, float timeLimit)
        {
            if (!IsHost) return;

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnGameProgress_RPC", RpcTarget.Others, questionNumber, alivePlayerCount, (int)turnPlayerId, questionType, timeLimit);
            }
        }

        /// <summary>
        /// 广播玩家状态同步（Host → All）
        /// </summary>
        public void BroadcastPlayerStateSync(ushort playerId, string playerName, bool isHost, int currentHealth, int maxHealth, bool isAlive)
        {
            if (!IsHost) return;

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnPlayerStateSync_RPC", RpcTarget.Others, (int)playerId, playerName, isHost, currentHealth, maxHealth, isAlive);
            }
        }

        /// <summary>
        /// 广播玩家答题结果（Host → All）
        /// </summary>
        public void BroadcastPlayerAnswerResult(ushort playerId, bool isCorrect, string answer)
        {
            if (!IsHost) return;

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnPlayerAnswerResult_RPC", RpcTarget.Others, (int)playerId, isCorrect, answer);
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 根据ID获取玩家对象
        /// </summary>
        private Player GetPlayerById(ushort playerId)
        {
            if (!PhotonNetwork.InRoom) return null;

            foreach (var player in PhotonNetwork.PlayerList)
            {
                if (player.ActorNumber == playerId)
                {
                    return player;
                }
            }
            return null;
        }

        /// <summary>
        /// 获取房间状态信息
        /// </summary>
        public string GetRoomStatus()
        {
            if (!PhotonNetwork.InRoom)
                return "未在房间中";

            return $"房间: {RoomName} | 玩家: {PlayerCount}/{MaxPlayers} | 准备: {GetReadyPlayerCount()}/{PlayerCount} | Host: {IsHost}";
        }

        /// <summary>
        /// 检查是否具备RPC发送条件
        /// </summary>
        private bool CanSendRPC()
        {
            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager == null)
            {
                Debug.LogError("[NetworkManager] PersistentNetworkManager实例不存在");
                return false;
            }

            if (persistentManager.photonView == null)
            {
                Debug.LogError("[NetworkManager] PersistentNetworkManager的PhotonView不存在");
                return false;
            }

            if (!PhotonNetwork.InRoom)
            {
                Debug.LogWarning("[NetworkManager] 未在房间中，无法发送RPC");
                return false;
            }

            return true;
        }

        #endregion

        #region 组件通知方法（减少耦合）

        /// <summary>
        /// 通知NQMC收到题目
        /// </summary>
        private void NotifyNQMCQuestionReceived(NetworkQuestionData question)
        {
            if (NetworkQuestionManagerController.Instance != null)
            {
                var method = NetworkQuestionManagerController.Instance.GetType()
                    .GetMethod("OnNetworkQuestionReceived", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(NetworkQuestionManagerController.Instance, new object[] { question });
            }
        }

        /// <summary>
        /// 通知NQMC回合变更
        /// </summary>
        private void NotifyNQMCTurnChanged(ushort playerId)
        {
            if (NetworkQuestionManagerController.Instance != null)
            {
                var method = NetworkQuestionManagerController.Instance.GetType()
                    .GetMethod("OnNetworkPlayerTurnChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(NetworkQuestionManagerController.Instance, new object[] { playerId });
            }
        }

        /// <summary>
        /// 通知NetworkUI血量更新
        /// </summary>
        private void NotifyNetworkUIHealthUpdate(ushort playerId, int newHealth, int maxHealth)
        {
            var networkUI = FindObjectOfType<NetworkUI>();
            networkUI?.OnHealthUpdateReceived(playerId, newHealth, maxHealth);
        }

        /// <summary>
        /// 通知NetworkUI回合变更
        /// </summary>
        private void NotifyNetworkUITurnChanged(ushort playerId)
        {
            var networkUI = FindObjectOfType<NetworkUI>();
            networkUI?.OnTurnChangedReceived(playerId);
        }

        /// <summary>
        /// 通知NetworkUI游戏开始
        /// </summary>
        private void NotifyNetworkUIGameStart(int totalPlayerCount, int alivePlayerCount, ushort firstTurnPlayerId)
        {
            var networkUI = FindObjectOfType<NetworkUI>();
            networkUI?.OnGameStartReceived(totalPlayerCount, alivePlayerCount, firstTurnPlayerId);
        }

        /// <summary>
        /// 通知NetworkUI游戏进度
        /// </summary>
        private void NotifyNetworkUIGameProgress(int questionNumber, int alivePlayerCount, ushort turnPlayerId)
        {
            var networkUI = FindObjectOfType<NetworkUI>();
            networkUI?.OnGameProgressReceived(questionNumber, alivePlayerCount, turnPlayerId);
        }

        /// <summary>
        /// 通知NetworkUI玩家状态同步
        /// </summary>
        private void NotifyNetworkUIPlayerStateSync(ushort playerId, string playerName, bool isHost, int currentHealth, int maxHealth, bool isAlive)
        {
            var networkUI = FindObjectOfType<NetworkUI>();
            networkUI?.OnPlayerStateSyncReceived(playerId, playerName, isHost, currentHealth, maxHealth, isAlive);
        }

        /// <summary>
        /// 通知NetworkUI玩家答题结果
        /// </summary>
        private void NotifyNetworkUIPlayerAnswerResult(ushort playerId, bool isCorrect, string answer)
        {
            var networkUI = FindObjectOfType<NetworkUI>();
            networkUI?.OnPlayerAnswerResultReceived(playerId, isCorrect, answer);
        }

        #endregion

        #region 场景适配方法

        /// <summary>
        /// 为当前场景注册网络事件监听
        /// </summary>
        public void RegisterSceneNetworkListeners()
        {
            LogDebug("为当前场景注册网络事件监听");
            // 这里可以根据当前场景自动注册相应的UI更新监听
        }

        /// <summary>
        /// 取消当前场景的网络事件监听
        /// </summary>
        public void UnregisterSceneNetworkListeners()
        {
            LogDebug("取消当前场景的网络事件监听");
            // 这里可以清理场景相关的事件监听
        }

        /// <summary>
        /// 检查网络管理器是否可用于当前场景
        /// </summary>
        public bool IsAvailableForCurrentScene()
        {
            // 检查当前场景是否需要网络功能
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            bool needsNetworking = currentScene.Contains("Room") || currentScene.Contains("Game") || currentScene.Contains("Network");

            LogDebug($"当前场景: {currentScene}, 需要网络功能: {needsNetworking}");
            return needsNetworking;
        }

        #endregion

        #region 持久化状态管理

        /// <summary>
        /// 保存当前网络状态（场景切换前）
        /// </summary>
        public void SaveNetworkState()
        {
            LogDebug("保存网络状态");
            // 可以在这里保存游戏状态、玩家准备状态等
        }

        /// <summary>
        /// 恢复网络状态（场景切换后）
        /// </summary>
        public void RestoreNetworkState()
        {
            LogDebug("恢复网络状态");
            // 可以在这里恢复之前保存的状态
        }

        /// <summary>
        /// 重置网络状态
        /// </summary>
        public void ResetNetworkState()
        {
            LogDebug("重置网络状态");
            // 清理临时状态，但保持连接
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

        [ContextMenu("显示房间状态")]
        public void ShowRoomStatus()
        {
            Debug.Log($"=== 房间状态 ===\n{GetRoomStatus()}");
        }

        [ContextMenu("显示准备状态")]
        public void ShowReadyStatus()
        {
            if (!PhotonNetwork.InRoom)
            {
                Debug.Log("未在房间中");
                return;
            }

            string status = "=== 玩家准备状态 ===\n";
            foreach (var player in PhotonNetwork.PlayerList)
            {
                bool isReady = GetPlayerReady(player);
                status += $"{player.NickName} (ID: {player.ActorNumber}): {(isReady ? "✓ 准备" : "✗ 未准备")}\n";
            }
            status += $"总计: {GetReadyPlayerCount()}/{PlayerCount} 准备就绪";

            Debug.Log(status);
        }

        [ContextMenu("测试设置准备状态")]
        public void TestSetReady()
        {
            if (PhotonNetwork.LocalPlayer != null)
            {
                SetPlayerReady(!GetPlayerReady(PhotonNetwork.LocalPlayer));
            }
        }

        [ContextMenu("检查RPC发送条件")]
        public void CheckRPCConditions()
        {
            bool canSend = CanSendRPC();
            Debug.Log($"RPC发送条件检查: {(canSend ? "✓ 可以发送" : "✗ 不能发送")}");

            var persistentManager = PersistentNetworkManager.Instance;
            Debug.Log($"PersistentNetworkManager: {(persistentManager != null ? "✓" : "✗")}");
            Debug.Log($"PhotonView: {(persistentManager?.photonView != null ? "✓" : "✗")}");
            Debug.Log($"在房间中: {(PhotonNetwork.InRoom ? "✓" : "✗")}");
        }

        [ContextMenu("显示网络管理器状态")]
        public void ShowNetworkManagerStatus()
        {
            string status = "=== NetworkManager状态 ===\n";
            status += $"可用于当前场景: {IsAvailableForCurrentScene()}\n";
            status += $"房间状态: {GetRoomStatus()}\n";
            status += $"RPC发送条件: {(CanSendRPC() ? "✓" : "✗")}\n";

            Debug.Log(status);
        }

        [ContextMenu("测试RPC广播")]
        public void TestRPCBroadcast()
        {
            if (IsHost && CanSendRPC())
            {
                BroadcastAnswerResult(true, "测试答案");
                Debug.Log("已发送测试RPC");
            }
            else
            {
                Debug.Log("无法发送测试RPC - 不是Host或条件不满足");
            }
        }

        #endregion
    }
}