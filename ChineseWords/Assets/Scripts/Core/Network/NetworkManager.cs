using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System;
using UI;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using System.Collections.Generic;
using Cards.Player;
using UI.MessageSystem;

namespace Core.Network
{
    /// <summary>
    /// 修复后的网络管理器 - 统一RPC目标为RpcTarget.All
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
        public static event Action<ushort, string> OnPlayerDied;
        public static event Action<ushort, string, string> OnGameVictory;
        public static event Action<string> OnGameEndWithoutWinner;
        public static event Action<ushort, string> OnReturnToRoomRequest;
        public static event Action<string> OnForceReturnToRoom;

        // 房间事件（RoomScene需要）
        public static event Action<ushort> OnPlayerJoined;
        public static event Action<ushort> OnPlayerLeft;
        public static event Action<ushort, bool> OnPlayerReadyChanged;

        //卡牌事件
        public static event Action<ushort, int, ushort, string> OnCardUsed; // 使用者ID, 卡牌ID, 目标ID, 卡牌名称
        public static event Action<ushort, string> OnCardEffectTriggered; // 玩家ID, 效果描述
        public static event Action<ushort, int> OnCardAdded; // 玩家ID, 卡牌ID
        public static event Action<ushort, int> OnCardRemoved; // 玩家ID, 卡牌ID
        public static event Action<ushort, int> OnHandSizeChanged; // 玩家ID, 新手牌数量
        public static event Action<string, ushort> OnCardMessage; // 消息内容, 来源玩家ID
        public static event Action<ushort, int, ushort> OnCardTransferred; // 从玩家ID, 卡牌ID, 到玩家ID


        #endregion

        #region Unity生命周期

        private void Awake()
        {
            // 移除单例逻辑，改为由PersistentNetworkManager管理
            LogDebug("NetworkManager 已初始化（修复版）");
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
        /// 提交答案（Client → Host）- 保持 RpcTarget.MasterClient
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
            LogDebug($"★ 收到RPC调用，数据长度: {questionData?.Length ?? -1}");

            if (questionData == null || questionData.Length == 0)
            {
                LogDebug("接收到空的题目数据");
                return;
            }

            var question = NetworkQuestionData.Deserialize(questionData);
            LogDebug($"题目反序列化结果: {(question != null ? question.questionType.ToString() : "失败")}");

            if (question != null)
            {
                LogDebug($"✓ 成功接收题目: {question.questionType} - {question.questionText}");
                OnQuestionReceived?.Invoke(question);
                NotifyNQMCQuestionReceived(question);
            }
        }

        [PunRPC]
        void OnAnswerResult_RPC(bool isCorrect, string correctAnswer)
        {
            LogDebug($"收到答题结果: {(isCorrect ? "正确" : "错误")} - {correctAnswer}");
            OnAnswerResultReceived?.Invoke(isCorrect, correctAnswer);
            MessageNotifier.Show($"回答{(isCorrect ? "正确" : "错误")} - {correctAnswer}", isCorrect ? MessageType.Success : MessageType.Error);
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
            MessageNotifier.Show("游戏开始",MessageType.Info);
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
        [PunRPC]
        void OnPlayerDeath_RPC(int playerId, string playerName)
        {
            ushort playerIdUShort = (ushort)playerId;
            LogDebug($"收到玩家死亡通知: {playerName} (ID: {playerIdUShort})");

            // 触发玩家死亡事件，供UI系统监听
            OnPlayerDied?.Invoke(playerIdUShort, playerName);
        }

        [PunRPC]
        void OnGameVictory_RPC(int winnerId, string winnerName, string reason)
        {
            ushort winnerIdUShort = (ushort)winnerId;
            LogDebug($"收到游戏胜利通知: 获胜者 {winnerName} - {reason}");

            // 触发游戏胜利事件
            OnGameVictory?.Invoke(winnerIdUShort, winnerName, reason);

            NotifyNQMCGameEnded();
        }

        [PunRPC]
        void OnGameEndWithoutWinner_RPC(string reason)
        {
            LogDebug($"收到无胜利者游戏结束通知: {reason}");

            // 触发无胜利者游戏结束事件
            OnGameEndWithoutWinner?.Invoke(reason);

            NotifyNQMCGameEnded();
        }

        [PunRPC]
        void OnReturnToRoomRequest_RPC(int playerId, string reason)
        {
            ushort playerIdUShort = (ushort)playerId;
            LogDebug($"收到返回房间请求: 玩家{playerIdUShort} - {reason}");
            // 触发返回房间请求事件
            OnReturnToRoomRequest?.Invoke(playerIdUShort, reason);
        }

        [PunRPC]
        void OnForceReturnToRoom_RPC(string reason)
        {
            LogDebug($"收到强制返回房间通知: {reason}");
            MessageNotifier.Show("强制返回房间", MessageType.System);
            // 触发强制返回房间事件
            OnForceReturnToRoom?.Invoke(reason);

            StartCoroutine(HandleReturnToRoom(reason));
        }

        //卡牌相关RPC
        [PunRPC]
        void OnCardUsed_RPC(int playerId, int cardId, int targetPlayerId, string cardName)
        {
            ushort playerIdUShort = (ushort)playerId;
            ushort targetPlayerIdUShort = (ushort)targetPlayerId;

            LogDebug($"收到卡牌使用RPC: 玩家{playerIdUShort}使用{cardName}(ID:{cardId}), 目标:{targetPlayerIdUShort}");
            // 触发静态事件
            OnCardUsed?.Invoke(playerIdUShort, cardId, targetPlayerIdUShort, cardName);

            if (Cards.Network.CardNetworkManager.Instance != null)
            {
                Cards.Network.CardNetworkManager.Instance.OnCardUsedReceived(playerIdUShort, cardId, targetPlayerIdUShort, cardName);
            }
        }

        [PunRPC]
        void OnCardEffectTriggered_RPC(int playerId, string effectDescription)
        {
            ushort playerIdUShort = (ushort)playerId;

            LogDebug($"收到卡牌效果RPC: 玩家{playerIdUShort} - {effectDescription}");

            // 触发静态事件
            OnCardEffectTriggered?.Invoke(playerIdUShort, effectDescription);

            if (Cards.Network.CardNetworkManager.Instance != null)
            {
                Cards.Network.CardNetworkManager.Instance.OnCardEffectTriggeredReceived(playerIdUShort, effectDescription);
            }
        }

        [PunRPC]
        void OnCardAdded_RPC(int playerId, int cardId)
        {
            ushort playerIdUShort = (ushort)playerId;

            LogDebug($"收到卡牌添加RPC: 玩家{playerIdUShort}获得卡牌{cardId}");

            // 触发静态事件
            OnCardAdded?.Invoke(playerIdUShort, cardId);

            if (Cards.Network.CardNetworkManager.Instance != null)
            {
                Cards.Network.CardNetworkManager.Instance.OnCardAddedReceived(playerIdUShort, cardId);
            }
        }

        [PunRPC]
        void OnCardRemoved_RPC(int playerId, int cardId)
        {
            ushort playerIdUShort = (ushort)playerId;

            LogDebug($"收到卡牌移除RPC: 玩家{playerIdUShort}失去卡牌{cardId}");

            // 触发静态事件
            OnCardRemoved?.Invoke(playerIdUShort, cardId);

            if (Cards.Network.CardNetworkManager.Instance != null)
            {
                Cards.Network.CardNetworkManager.Instance.OnCardRemovedReceived(playerIdUShort, cardId);
            }
        }

        [PunRPC]
        void OnHandSizeChanged_RPC(int playerId, int newHandSize)
        {
            ushort playerIdUShort = (ushort)playerId;

            LogDebug($"收到手牌变化RPC: 玩家{playerIdUShort}手牌数量:{newHandSize}");

            // 触发静态事件
            OnHandSizeChanged?.Invoke(playerIdUShort, newHandSize);

            if (Cards.Network.CardNetworkManager.Instance != null)
            {
                Cards.Network.CardNetworkManager.Instance.OnHandSizeChangedReceived(playerIdUShort, newHandSize);
            }
        }

        [PunRPC]
        void OnCardMessage_RPC(string message, int fromPlayerId)
        {
            ushort fromPlayerIdUShort = (ushort)fromPlayerId;

            LogDebug($"收到卡牌消息RPC: {message} (来自玩家{fromPlayerIdUShort})");

            // 触发静态事件
            OnCardMessage?.Invoke(message, fromPlayerIdUShort);

            if (Cards.Network.CardNetworkManager.Instance != null)
            {
                Cards.Network.CardNetworkManager.Instance.OnCardMessageReceived(message, fromPlayerIdUShort);
            }
        }

        [PunRPC]
        void OnCardTransferred_RPC(int fromPlayerId, int cardId, int toPlayerId)
        {
            ushort fromPlayerIdUShort = (ushort)fromPlayerId;
            ushort toPlayerIdUShort = (ushort)toPlayerId;

            LogDebug($"收到卡牌转移RPC: 卡牌{cardId}从玩家{fromPlayerIdUShort}转移到玩家{toPlayerIdUShort}");

            // 触发静态事件
            OnCardTransferred?.Invoke(fromPlayerIdUShort, cardId, toPlayerIdUShort);

            // 转发给CardNetworkManager（如果存在）
            if (Cards.Network.CardNetworkManager.Instance != null)
            {
                Cards.Network.CardNetworkManager.Instance.OnCardTransferredReceived(fromPlayerIdUShort, cardId, toPlayerIdUShort);
            }
        }

        [PunRPC]
        private void OnSeatsAndCharacters_RPC(int[] playerIds, int[] seatIndices)
        {
            LogDebug($"收到座位和角色数据RPC: {playerIds.Length} 名玩家");

            ushort[] playerIdsUShort = new ushort[playerIds.Length];
            for (int i = 0; i < playerIds.Length; i++)
            {
                playerIdsUShort[i] = (ushort)playerIds[i];
            }

            var spawner = FindObjectOfType<Classroom.Player.NetworkPlayerSpawner>();
            if (spawner != null)
            {
                spawner.ReceiveSeatsAndCharactersData(playerIdsUShort, seatIndices);
            }
        }

        [PunRPC]
        private void OnPlayerTransform_RPC(int playerId, float posX, float posY, float posZ,
            float rotX, float rotY, float rotZ, float rotW, float velX, float velY, float velZ, float timestamp)
        {
            Vector3 position = new Vector3(posX, posY, posZ);
            Quaternion rotation = new Quaternion(rotX, rotY, rotZ, rotW);
            Vector3 velocity = new Vector3(velX, velY, velZ);

            var allSyncComponents = FindObjectsOfType<Classroom.Player.PlayerNetworkSync>();
            foreach (var syncComponent in allSyncComponents)
            {
                if (syncComponent.PlayerId == (ushort)playerId)
                {
                    syncComponent.ReceiveNetworkTransform(position, rotation, velocity, timestamp);
                    break;
                }
            }
        }
        [PunRPC]
        void OnPlayerHealthChangeRequested_RPC(int playerId, int healthChange, string reason)
        {
            // 只有MasterClient处理这个请求
            if (!PhotonNetwork.IsMasterClient) return;

            LogDebug($"Host收到血量修改请求: 玩家{playerId}, 变化{healthChange}, 原因:{reason}");

            // 通过HostGameManager执行血量修改
            if (HostGameManager.Instance != null)
            {
                // 使用反射获取hpManager并执行修改
                var hostManager = HostGameManager.Instance;
                var hpManagerField = hostManager.GetType().GetField("hpManager",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (hpManagerField != null)
                {
                    var hpManager = hpManagerField.GetValue(hostManager) as PlayerHPManager;
                    if (hpManager != null)
                    {
                        bool success = false;
                        if (healthChange > 0)
                        {
                            success = hpManager.HealPlayer((ushort)playerId, healthChange, out int newHealth);
                            LogDebug($"Host执行回血: 玩家{playerId} +{healthChange} → {newHealth}, 成功:{success}");
                        }
                        else if (healthChange < 0)
                        {
                            success = hpManager.ApplyDamage((ushort)playerId, out int newHealth, out bool isDead, -healthChange);
                            LogDebug($"Host执行扣血: 玩家{playerId} {healthChange} → {newHealth}, 死亡:{isDead}, 成功:{success}");
                        }

                        LogDebug($"Host执行血量修改结果: {success}");
                    }
                    else
                    {
                        LogDebug("Host的hpManager为空，无法执行血量修改");
                    }
                }
                else
                {
                    LogDebug("Host无法通过反射获取hpManager字段");
                }
            }
            else
            {
                LogDebug("HostGameManager.Instance为空，无法执行血量修改");
            }
        }

    
        [PunRPC]
        void OnPlayerHandCardsReceived_RPC(int playerId, int[] cardIds)
        {
            ushort playerIdUShort = (ushort)playerId;

            LogDebug($"收到手牌分发RPC: 玩家{playerIdUShort}, {cardIds?.Length ?? 0}张卡牌");

            if (cardIds == null || cardIds.Length == 0)
            {
                Debug.LogWarning("[NetworkManager] 收到空的手牌数据");
                return;
            }

            // 转发给CardNetworkManager处理
            if (Cards.Network.CardNetworkManager.Instance != null)
            {
                List<int> cardIdsList = new List<int>(cardIds);
                Cards.Network.CardNetworkManager.Instance.OnHandCardsDistributionReceived(playerIdUShort, cardIdsList);
                LogDebug($"✓ 已转发手牌数据给CardNetworkManager");
            }
            else
            {
                Debug.LogError("[NetworkManager] CardNetworkManager实例不存在，无法处理手牌分发");
            }
        }

        #endregion

        #region Host专用RPC发送方法 - 修复版：统一使用 RpcTarget.All

        /// <summary>
        /// 广播题目（Host → All）- 已修复
        /// </summary>
        public void BroadcastQuestion(NetworkQuestionData question)
        {
            if (!IsHost) return;

            var persistentManager = PersistentNetworkManager.Instance;
            LogDebug($"准备广播题目 - PhotonView可用: {persistentManager?.photonView != null}");
            LogDebug($"PhotonView ID: {persistentManager?.photonView?.ViewID}");

            byte[] questionData = question.Serialize();

            if (questionData != null && questionData.Length > 0)
            {
                persistentManager.photonView.RPC("OnQuestionReceived_RPC", RpcTarget.All, questionData);
                LogDebug($"✓ RPC已发送到所有玩家（包括Host）");
            }
            else
            {
                LogDebug("序列化数据无效，跳过RPC发送");
            }
        }

        /// <summary>
        /// 广播答题结果（Host → All）- 修复：改为 RpcTarget.All
        /// </summary>
        public void BroadcastAnswerResult(bool isCorrect, string correctAnswer)
        {
            if (!IsHost) return;

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnAnswerResult_RPC", RpcTarget.All, isCorrect, correctAnswer);
                LogDebug($"✓ 广播答题结果到所有玩家: {(isCorrect ? "正确" : "错误")}");
            }
        }

        /// <summary>
        /// 广播血量更新（Host → All）- 修复：改为 RpcTarget.All
        /// </summary>
        public void BroadcastHealthUpdate(ushort playerId, int newHealth, int maxHealth)
        {
            if (!IsHost) return;

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnHealthUpdate_RPC", RpcTarget.All, (int)playerId, newHealth, maxHealth);
                LogDebug($"✓ 广播血量更新到所有玩家: 玩家{playerId} {newHealth}/{maxHealth}");
            }
        }

        /// <summary>
        /// 广播回合变更（Host → All）- 修复：改为 RpcTarget.All
        /// </summary>
        public void BroadcastPlayerTurnChanged(ushort newTurnPlayerId)
        {
            if (!IsHost) return;

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnPlayerTurnChanged_RPC", RpcTarget.All, (int)newTurnPlayerId);
                LogDebug($"✓ 广播回合变更到所有玩家: 玩家{newTurnPlayerId}");
            }
        }

        /// <summary>
        /// 广播游戏开始（Host → All）- 修复：改为 RpcTarget.All
        /// </summary>
        public void BroadcastGameStart(int totalPlayerCount, int alivePlayerCount, ushort firstTurnPlayerId)
        {
            if (!IsHost) return;

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnGameStart_RPC", RpcTarget.All, totalPlayerCount, alivePlayerCount, (int)firstTurnPlayerId);
                LogDebug($"✓ 广播游戏开始到所有玩家: 总玩家{totalPlayerCount}, 首回合玩家{firstTurnPlayerId}");
            }
        }

        /// <summary>
        /// 广播游戏进度（Host → All）- 修复：改为 RpcTarget.All
        /// </summary>
        public void BroadcastGameProgress(int questionNumber, int alivePlayerCount, ushort turnPlayerId, int questionType, float timeLimit)
        {
            if (!IsHost) return;

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnGameProgress_RPC", RpcTarget.All, questionNumber, alivePlayerCount, (int)turnPlayerId, questionType, timeLimit);
                LogDebug($"✓ 广播游戏进度到所有玩家: 第{questionNumber}题");
            }
        }

        /// <summary>
        /// 广播玩家状态同步（Host → All）- 修复：改为 RpcTarget.All
        /// </summary>
        public void BroadcastPlayerStateSync(ushort playerId, string playerName, bool isHost, int currentHealth, int maxHealth, bool isAlive)
        {
            if (!IsHost) return;

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnPlayerStateSync_RPC", RpcTarget.All, (int)playerId, playerName, isHost, currentHealth, maxHealth, isAlive);
                LogDebug($"✓ 广播玩家状态同步到所有玩家: {playerName}");
            }
        }

        /// <summary>
        /// 广播玩家答题结果（Host → All）- 修复：改为 RpcTarget.All
        /// </summary>
        public void BroadcastPlayerAnswerResult(ushort playerId, bool isCorrect, string answer)
        {
            if (!IsHost) return;

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnPlayerAnswerResult_RPC", RpcTarget.All, (int)playerId, isCorrect, answer);
                LogDebug($"✓ 广播玩家答题结果到所有玩家: 玩家{playerId} {(isCorrect ? "正确" : "错误")}");

                var cardManager = GameObject.FindObjectOfType<PlayerCardManager>();
                if (cardManager != null)
                {
                    cardManager.ResetPlayerUsageOpportunity(playerId);
                    LogDebug($"✓ 已通过 NetworkManager 重置玩家{playerId}卡牌使用机会");
                }
                else
                {
                    LogDebug("✗ 找不到 PlayerCardManager，无法重置卡牌使用");
                }
            }
        }
        /// <summary>
        /// 广播玩家死亡事件（Host → All）
        /// </summary>
        public void BroadcastPlayerDeath(ushort playerId, string playerName)
        {
            if (!IsHost) return;

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnPlayerDeath_RPC", RpcTarget.All, (int)playerId, playerName);
                LogDebug($"✓ 广播玩家死亡到所有玩家: {playerName} (ID: {playerId})");
            }
        }

        /// <summary>
        /// 广播游戏胜利（Host → All）
        /// </summary>
        public void BroadcastGameVictory(ushort winnerId, string winnerName, string reason)
        {
            if (!IsHost) return;

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnGameVictory_RPC", RpcTarget.All, (int)winnerId, winnerName, reason);
                LogDebug($"✓ 广播游戏胜利到所有玩家: 获胜者 {winnerName}");
            }
        }

        /// <summary>
        /// 广播无胜利者游戏结束（Host → All）
        /// </summary>
        public void BroadcastGameEndWithoutWinner(string reason)
        {
            if (!IsHost) return;

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnGameEndWithoutWinner_RPC", RpcTarget.All, reason);
                LogDebug($"✓ 广播无胜利者游戏结束到所有玩家: {reason}");
            }
        }

        /// <summary>
        /// 广播返回房间请求（Host → All）
        /// </summary>
        public void BroadcastReturnToRoomRequest(ushort playerId, string reason)
        {
            if (!IsHost) return;

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnReturnToRoomRequest_RPC", RpcTarget.All, (int)playerId, reason);
                LogDebug($"✓ 广播返回房间请求到所有玩家: 玩家{playerId}");
            }
        }

        /// <summary>
        /// 广播强制返回房间（Host → All）
        /// </summary>
        public void BroadcastForceReturnToRoom(string reason)
        {
            if (!IsHost) return;

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnForceReturnToRoom_RPC", RpcTarget.All, reason);
                LogDebug($"✓ 广播强制返回房间到所有玩家: {reason}");
            }
        }

        //卡牌相关
        /// <summary>
        /// 广播卡牌使用（Host → All）
        /// </summary>
        public void BroadcastCardUsed(ushort playerId, int cardId, ushort targetPlayerId, string cardName)
        {
            if (!IsHost) return;

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnCardUsed_RPC", RpcTarget.All,
                    (int)playerId, cardId, (int)targetPlayerId, cardName);
                LogDebug($"✓ 广播卡牌使用: 玩家{playerId}使用{cardName}, 目标:{targetPlayerId}");
            }
        }

        /// <summary>
        /// 广播卡牌效果触发（Host → All）
        /// </summary>
        public void BroadcastCardEffectTriggered(ushort playerId, string effectDescription)
        {
            if (!IsHost) return;

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnCardEffectTriggered_RPC", RpcTarget.All,
                    (int)playerId, effectDescription);
                LogDebug($"✓ 广播卡牌效果: 玩家{playerId} - {effectDescription}");
            }
        }

        /// <summary>
        /// 广播卡牌添加（Host → All）
        /// </summary>
        public void BroadcastCardAdded(ushort playerId, int cardId)
        {
            if (!IsHost) return;

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnCardAdded_RPC", RpcTarget.All,
                    (int)playerId, cardId);
                LogDebug($"✓ 广播卡牌添加: 玩家{playerId}获得卡牌{cardId}");
            }
        }
        /// <summary>
        /// 请求修改玩家血量（Client → Host）
        /// </summary>
        public void RequestPlayerHealthChange(int playerId, int healthChange, string reason = "")
        {
            if (!PhotonNetwork.InRoom)
            {
                Debug.LogWarning("[NetworkManager] 未在房间中，无法请求血量修改");
                return;
            }

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnPlayerHealthChangeRequested_RPC", RpcTarget.MasterClient, playerId, healthChange, reason);
                LogDebug($"请求修改玩家{playerId}血量: {healthChange} (原因: {reason})");
            }
            else
            {
                Debug.LogError("[NetworkManager] 无法发送RPC，PersistentNetworkManager不可用");
            }
        }

        /// <summary>
        /// 发送手牌数据给指定玩家（Host → Specific Client）
        /// </summary>
        public void SendPlayerHandCards(ushort playerId, List<int> cardIds)
        {
            if (!IsHost)
            {
                Debug.LogWarning("[NetworkManager] 只有Host可以分发手牌");
                return;
            }

            if (cardIds == null || cardIds.Count == 0)
            {
                Debug.LogWarning($"[NetworkManager] 玩家{playerId}的手牌数据为空");
                return;
            }

            // 转换为int数组以兼容RPC
            int[] cardIdsArray = cardIds.ToArray();

            // 获取目标玩家
            var targetPlayer = GetPlayerById(playerId);
            if (targetPlayer == null)
            {
                Debug.LogError($"[NetworkManager] 找不到玩家{playerId}");
                return;
            }

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                // 发送给特定玩家
                persistentManager.photonView.RPC("OnPlayerHandCardsReceived_RPC", targetPlayer, (int)playerId, cardIdsArray);
                LogDebug($"✓ 已发送手牌给玩家{playerId}: {cardIds.Count}张卡牌");
            }
            else
            {
                Debug.LogError("[NetworkManager] 无法发送RPC，PersistentNetworkManager不可用");
            }
        }

        /// <summary>
        /// 广播卡牌移除（Host → All）
        /// </summary>
        public void BroadcastCardRemoved(ushort playerId, int cardId)
        {
            if (!IsHost) return;

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnCardRemoved_RPC", RpcTarget.All,
                    (int)playerId, cardId);
                LogDebug($"✓ 广播卡牌移除: 玩家{playerId}失去卡牌{cardId}");
            }
        }

        /// <summary>
        /// 广播手牌数量变化（Host → All）
        /// </summary>
        public void BroadcastHandSizeChanged(ushort playerId, int newHandSize)
        {
            if (!IsHost) return;

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnHandSizeChanged_RPC", RpcTarget.All,
                    (int)playerId, newHandSize);
                LogDebug($"✓ 广播手牌变化: 玩家{playerId}手牌数量:{newHandSize}");
            }
        }

        /// <summary>
        /// 广播卡牌消息（Host → All）
        /// </summary>
        public void BroadcastCardMessage(string message, ushort fromPlayerId)
        {
            if (!IsHost) return;

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnCardMessage_RPC", RpcTarget.All,
                    message, (int)fromPlayerId);
                LogDebug($"✓ 广播卡牌消息: {message} (来自玩家{fromPlayerId})");
            }
        }

        /// <summary>
        /// 广播卡牌转移（Host → All）
        /// </summary>
        public void BroadcastCardTransferred(ushort fromPlayerId, int cardId, ushort toPlayerId)
        {
            if (!IsHost) return;

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnCardTransferred_RPC", RpcTarget.All,
                    (int)fromPlayerId, cardId, (int)toPlayerId);
                LogDebug($"✓ 广播卡牌转移: 卡牌{cardId}从玩家{fromPlayerId}转移到玩家{toPlayerId}");
            }
        }

        /// <summary>
        /// 广播座位和角色数据（Host → All）
        /// </summary>
        public void BroadcastSeatsAndCharacters(ushort[] playerIds, int[] seatIndices)
        {
            if (!IsHost) return;

            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                // 将ushort数组转换为int数组以兼容RPC
                int[] playerIdsInt = new int[playerIds.Length];
                for (int i = 0; i < playerIds.Length; i++)
                {
                    playerIdsInt[i] = playerIds[i];
                }

                persistentManager.photonView.RPC("OnSeatsAndCharacters_RPC", RpcTarget.All, playerIdsInt, seatIndices);
                LogDebug($"✓ 广播座位和角色数据到所有玩家: {playerIds.Length} 名玩家");
            }
        }

        /// <summary>
        /// 同步玩家位置数据（Any → All）
        /// </summary>
        public void SyncPlayerTransform(ushort playerId, Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            var persistentManager = PersistentNetworkManager.Instance;
            if (persistentManager != null && persistentManager.photonView != null)
            {
                persistentManager.photonView.RPC("OnPlayerTransform_RPC", RpcTarget.Others,
                    (int)playerId, position.x, position.y, position.z,
                    rotation.x, rotation.y, rotation.z, rotation.w,
                    velocity.x, velocity.y, velocity.z, Time.time);

                LogDebug($"同步玩家 {playerId} 位置数据");
            }
        }
        #endregion

        #region 游戏结束和返回房间逻辑

        /// <summary>
        /// 通知NQMC游戏结束
        /// </summary>
        private void NotifyNQMCGameEnded()
        {
            LogDebug("通知NQMC游戏结束");

            if (NetworkQuestionManagerController.Instance != null)
            {
                NetworkQuestionManagerController.Instance.StopGame();
                LogDebug("✓ NQMC已停止游戏");
            }
            else
            {
                LogDebug("✗ NetworkQuestionManagerController.Instance 为空");
            }
        }

        /// <summary>
        /// 处理返回房间逻辑
        /// </summary>
        private System.Collections.IEnumerator HandleReturnToRoom(string reason)
        {

            yield return new WaitForSeconds(1f);
            try
            {
                // 检查当前场景
                string currentScene = SceneManager.GetActiveScene().name;
                LogDebug($"当前场景: {currentScene}");
                if (currentScene.Contains("Game") || currentScene.Contains("Network"))
                {
                    LogDebug("从游戏场景返回房间场景");
                    SceneManager.LoadScene("RoomScene");
                }
                else if (currentScene.Contains("Room"))
                {
                    LogDebug("重新加载房间场景以重置状态");
                    SceneManager.LoadScene(currentScene);
                }
                else
                {
                    LogDebug("返回主菜单场景");
                    SceneManager.LoadScene("MainMenuScene");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkManager] 返回房间失败: {e.Message}");
                // 备用方案：离开房间
                if (PhotonNetwork.InRoom)
                {
                    LogDebug("场景切换失败，尝试离开Photon房间");
                    PhotonNetwork.LeaveRoom();
                }
            }
        }

        #endregion

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


        #region 组件通知方法

        /// <summary>
        /// 通知NQMC收到题目
        /// </summary>
        private void NotifyNQMCQuestionReceived(NetworkQuestionData question)
        {
            LogDebug($"通知NQMC收到题目: {question.questionType}");

            if (NetworkQuestionManagerController.Instance != null)
            {
                var method = NetworkQuestionManagerController.Instance.GetType()
                    .GetMethod("OnNetworkQuestionReceived", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (method != null)
                {
                    method.Invoke(NetworkQuestionManagerController.Instance, new object[] { question });
                    LogDebug("✓ NQMC反射调用成功");
                }
                else
                {
                    LogDebug("✗ 未找到NQMC的OnNetworkQuestionReceived方法");
                }
            }
            else
            {
                LogDebug("✗ NetworkQuestionManagerController.Instance 为空");
            }
        }

        /// <summary>
        /// 通知NQMC回合变更
        /// </summary>
        private void NotifyNQMCTurnChanged(ushort playerId)
        {
            LogDebug($"通知NQMC回合变更: 玩家{playerId}");

            if (NetworkQuestionManagerController.Instance != null)
            {
                var method = NetworkQuestionManagerController.Instance.GetType()
                    .GetMethod("OnNetworkPlayerTurnChanged", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (method != null)
                {
                    method.Invoke(NetworkQuestionManagerController.Instance, new object[] { playerId });
                    LogDebug("✓ NQMC回合变更通知成功");
                }
                else
                {
                    LogDebug("✗ 未找到NQMC的OnNetworkPlayerTurnChanged方法");
                }
            }
        }

        /// <summary>
        /// 通知NQMC游戏开始
        /// </summary>
        private void NotifyNQMCGameStart(int totalPlayerCount, int alivePlayerCount, ushort firstTurnPlayerId)
        {
            LogDebug($"通知NQMC游戏开始: 首回合玩家{firstTurnPlayerId}");

            if (NetworkQuestionManagerController.Instance != null)
            {
                var method = NetworkQuestionManagerController.Instance.GetType()
                    .GetMethod("OnNetworkGameStarted", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (method != null)
                {
                    method.Invoke(NetworkQuestionManagerController.Instance, new object[] { totalPlayerCount, alivePlayerCount, firstTurnPlayerId });
                    LogDebug("✓ NQMC游戏开始通知成功");
                }
                else
                {
                    LogDebug("✗ 未找到NQMC的OnNetworkGameStarted方法");
                }
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

            // 同时通知NQMC游戏开始
            NotifyNQMCGameStart(totalPlayerCount, alivePlayerCount, firstTurnPlayerId);
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

        #region 玩家查询方法
        /// <summary>
        /// 获取所有在线玩家的ID列表
        /// </summary>
        public List<ushort> GetAllOnlinePlayerIds()
        {
            var playerIds = new List<ushort>();
            if (!PhotonNetwork.InRoom) return playerIds;

            foreach (var player in PhotonNetwork.PlayerList)
            {
                playerIds.Add((ushort)player.ActorNumber);
            }
            return playerIds;
        }

        /// <summary>
        /// 获取玩家名称
        /// </summary>
        public string GetPlayerName(ushort playerId)
        {
            var player = GetPlayerById(playerId);
            return player?.NickName ?? $"Player_{playerId}";
        }
        #endregion 

        #region 调试方法

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[NetworkManager-Fixed] {message}");
            }
        }

        #endregion
    }
}