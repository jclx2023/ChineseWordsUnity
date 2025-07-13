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
using Classroom.Teacher;
using System.Collections;

namespace Core.Network
{
    /// <summary>
    /// 改进版网络管理器 - 增强房间状态管理和组件解耦合支持
    /// 新增：房间状态重置、统一属性查询接口、RoomScene解耦合支持
    /// </summary>
    public class NetworkManager : MonoBehaviourPun, IInRoomCallbacks
    {
        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        [Header("同步调试设置")]
        [SerializeField] private bool enableSyncDebugLogs = false;

        [Header("房间状态管理")]
        [SerializeField] private float roomStateResetDelay = 2f;

        // 玩家同步管理
        private Dictionary<ushort, Classroom.Player.PlayerNetworkSync> playerSyncComponents = new Dictionary<ushort, Classroom.Player.PlayerNetworkSync>();

        // 静态访问点
        public static NetworkManager Instance => PersistentNetworkManager.Instance?.GameNetwork;

        #region 核心属性

        public bool IsHost => PhotonNetwork.IsMasterClient;
        public bool IsConnected => PhotonNetwork.IsConnected && PhotonNetwork.InRoom;
        public ushort ClientId => (ushort)PhotonNetwork.LocalPlayer.ActorNumber;
        public string RoomName => PhotonNetwork.CurrentRoom?.Name ?? "";
        public int PlayerCount => PhotonNetwork.CurrentRoom?.PlayerCount ?? 0;
        public int MaxPlayers => PhotonNetwork.CurrentRoom?.MaxPlayers ?? 0;
        public bool IsRoomFull => PlayerCount >= MaxPlayers;

        // 缓存引用
        private PersistentNetworkManager persistentManager;

        #endregion

        #region 事件系统

        // 游戏核心事件
        public static event Action<NetworkQuestionData> OnQuestionReceived;
        public static event Action<bool, string> OnAnswerResultReceived;
        public static event Action<ushort, int, int> OnHealthUpdated;
        public static event Action<ushort> OnPlayerTurnChanged;
        public static event Action<int, int, ushort> OnGameStarted;
        public static event Action<ushort, string> OnPlayerDied;
        public static event Action<ushort, string, string> OnGameVictory;
        public static event Action<string> OnGameEndWithoutWinner;
        public static event Action<string> OnForceReturnToRoom;
        public static event Action<ushort, bool, string> OnPlayerAnswerResult;

        // 房间事件
        public static event Action<ushort> OnPlayerJoined;
        public static event Action<ushort> OnPlayerLeft;
        public static event Action<ushort, bool> OnPlayerReadyChanged;

        // 新增：房间状态管理事件
        public static event Action OnRoomStateReset;           // 房间状态重置
        public static event Action<string, object> OnRoomPropertyChanged;  // 房间属性变化
        public static event Action OnAllPlayersReady;          // 所有玩家准备就绪

        // 卡牌事件
        public static event Action<ushort, int, ushort, string> OnCardUsed;
        public static event Action<ushort, string> OnCardEffectTriggered;
        public static event Action<ushort, int> OnCardAdded;
        public static event Action<ushort, int> OnCardRemoved;
        public static event Action<ushort, int> OnHandSizeChanged;
        public static event Action<string, ushort> OnCardMessage;
        public static event Action<ushort, int, ushort> OnCardTransferred;

        // 头部旋转事件
        public static event Action<ushort, float, float> OnHeadRotationReceived;

        #endregion

        #region Unity生命周期

        private void Start()
        {
            PhotonNetwork.AddCallbackTarget(this);
            persistentManager = PersistentNetworkManager.Instance;
            LogDebug($"NetworkManager已启动 - 房间: {RoomName}, 玩家ID: {ClientId}, 是否Host: {IsHost}");
        }

        private void OnDestroy()
        {
            if (PhotonNetwork.NetworkingClient != null)
                PhotonNetwork.RemoveCallbackTarget(this);
            LogDebug("NetworkManager已销毁");
        }

        #endregion

        #region 新增：房间状态管理接口

        /// <summary>
        /// 获取房间属性的统一接口
        /// </summary>
        public T GetRoomProperty<T>(string key, T defaultValue = default(T))
        {
            if (!IsConnected) return defaultValue;
            return PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object value)
                ? (T)value : defaultValue;
        }

        /// <summary>
        /// 设置房间属性的统一接口（仅房主）
        /// </summary>
        public bool SetRoomProperty(string key, object value)
        {
            if (!IsHost || !IsConnected) return false;
            var props = new Hashtable { [key] = value };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            LogDebug($"设置房间属性: {key} = {value}");
            return true;
        }

        /// <summary>
        /// 批量设置房间属性（仅房主）
        /// </summary>
        public bool SetRoomProperties(Hashtable properties)
        {
            if (!IsHost || !IsConnected || properties == null) return false;
            PhotonNetwork.CurrentRoom.SetCustomProperties(properties);
            LogDebug($"批量设置房间属性: {properties.Count} 个属性");
            return true;
        }

        /// <summary>
        /// 获取玩家属性的统一接口
        /// </summary>
        public T GetPlayerProperty<T>(ushort playerId, string key, T defaultValue = default(T))
        {
            var player = GetPlayerById(playerId);
            return player?.CustomProperties?.TryGetValue(key, out object value) == true
                ? (T)value : defaultValue;
        }

        /// <summary>
        /// 设置本地玩家属性
        /// </summary>
        public bool SetMyPlayerProperty(string key, object value)
        {
            if (!IsConnected) return false;
            var props = new Hashtable { [key] = value };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
            LogDebug($"设置本地玩家属性: {key} = {value}");
            return true;
        }

        /// <summary>
        /// 游戏结束后重置房间状态（仅房主）
        /// </summary>
        public void ResetRoomStateAfterGame()
        {
            if (!IsHost || !IsConnected)
            {
                LogDebug("非房主或未连接，无法重置房间状态");
                return;
            }

            LogDebug("开始重置房间状态...");

            // 重置房间属性
            var roomProps = new Hashtable
            {
                ["gameStarted"] = false,
                ["gameEnded"] = false,
                ["roomState"] = 0, // RoomState.Waiting
                ["gameEndTime"] = null,
                ["winnerId"] = null,
                ["winnerName"] = null,
                ["gameEndReason"] = null
            };

            SetRoomProperties(roomProps);

            // 延迟清理所有玩家准备状态
            StartCoroutine(DelayedClearPlayerReadyStates());

            LogDebug("房间状态已重置为等待状态");
        }

        /// <summary>
        /// 延迟清理所有玩家准备状态
        /// </summary>
        private IEnumerator DelayedClearPlayerReadyStates()
        {
            yield return new WaitForSeconds(0.5f);
            ClearAllPlayerReadyStates();
        }

        /// <summary>
        /// 清理所有玩家准备状态（仅房主）
        /// </summary>
        public void ClearAllPlayerReadyStates()
        {
            if (!IsHost || !IsConnected) return;

            LogDebug("清理所有玩家准备状态");

            // 通过RPC通知所有客户端清理自己的准备状态
            SendRPC("OnClearReadyState_RPC", RpcTarget.All);
        }

        [PunRPC]
        void OnClearReadyState_RPC()
        {
            // 每个客户端清理自己的准备状态
            if (!IsHost) // 房主不需要准备状态
            {
                SetMyPlayerProperty("isReady", false);
                LogDebug("已清理本地玩家准备状态");
            }
        }

        /// <summary>
        /// 获取房间状态信息（供UI显示）
        /// </summary>
        public string GetRoomStatusInfo()
        {
            if (!IsConnected) return "未连接";

            int readyCount = GetReadyPlayerCount();
            int nonHostCount = GetNonHostPlayerCount();
            bool gameStarted = GetRoomProperty("gameStarted", false);

            return $"房间: {RoomName}, " +
                   $"玩家: {PlayerCount}/{MaxPlayers}, " +
                   $"准备: {readyCount}/{nonHostCount}, " +
                   $"房主: {(IsHost ? "是" : "否")}, " +
                   $"游戏状态: {(gameStarted ? "进行中" : "等待中")}";
        }

        /// <summary>
        /// 检查房间是否可以开始游戏
        /// </summary>
        public bool CanStartGame(int minPlayers = 2)
        {
            if (!IsHost || !IsConnected) return false;
            if (PlayerCount < minPlayers) return false;
            if (GetRoomProperty("gameStarted", false)) return false;

            // 检查所有非房主玩家是否都已准备
            return AreAllPlayersReady();
        }

        /// <summary>
        /// 获取游戏开始条件详情
        /// </summary>
        public string GetGameStartConditions(int minPlayers = 2)
        {
            if (!IsHost) return "不是房主";
            if (!IsConnected) return "未连接房间";
            if (GetRoomProperty("gameStarted", false)) return "游戏已开始";
            if (PlayerCount < minPlayers) return $"玩家数不足 ({PlayerCount}/{minPlayers})";

            int readyCount = GetReadyPlayerCount();
            int nonHostCount = GetNonHostPlayerCount();

            if (!AreAllPlayersReady()) return $"玩家未全部准备 ({readyCount}/{nonHostCount})";

            return "满足开始条件";
        }

        #endregion

        #region Photon回调

        void IInRoomCallbacks.OnPlayerEnteredRoom(Player newPlayer)
        {
            ushort playerId = (ushort)newPlayer.ActorNumber;
            LogDebug($"玩家加入: {newPlayer.NickName} (ID: {playerId})");
            OnPlayerJoined?.Invoke(playerId);
        }

        void IInRoomCallbacks.OnPlayerLeftRoom(Player otherPlayer)
        {
            ushort playerId = (ushort)otherPlayer.ActorNumber;
            playerSyncComponents.Remove(playerId);
            LogDebug($"玩家离开: {otherPlayer.NickName} (ID: {playerId})");
            OnPlayerLeft?.Invoke(playerId);
        }

        void IInRoomCallbacks.OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            if (changedProps.TryGetValue("isReady", out object isReadyObj))
            {
                ushort playerId = (ushort)targetPlayer.ActorNumber;
                bool isReady = (bool)isReadyObj;
                LogDebug($"玩家准备状态更新: {targetPlayer.NickName} -> {isReady}");
                OnPlayerReadyChanged?.Invoke(playerId, isReady);

                // 检查是否所有玩家都已准备
                if (AreAllPlayersReady() && GetNonHostPlayerCount() > 0)
                {
                    LogDebug("所有玩家都已准备就绪");
                    OnAllPlayersReady?.Invoke();
                }
            }
        }

        void IInRoomCallbacks.OnMasterClientSwitched(Player newMasterClient)
        {
            LogDebug($"房主切换到: {newMasterClient.NickName} (ID: {newMasterClient.ActorNumber})");
        }

        void IInRoomCallbacks.OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
            // 广播房间属性变化
            foreach (var kvp in propertiesThatChanged)
            {
                LogDebug($"房间属性更新: {kvp.Key} = {kvp.Value}");
                OnRoomPropertyChanged?.Invoke((string)kvp.Key, kvp.Value);
            }

            // 检查游戏状态重置
            if (propertiesThatChanged.ContainsKey("gameStarted") &&
                !(bool)propertiesThatChanged["gameStarted"])
            {
                LogDebug("检测到游戏状态重置");
                OnRoomStateReset?.Invoke();
            }
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 发送RPC的统一方法
        /// </summary>
        private void SendRPC(string methodName, RpcTarget target, params object[] parameters)
        {
            if (!PhotonNetwork.InRoom || persistentManager?.photonView == null) return;

            persistentManager.photonView.RPC(methodName, target, parameters);
        }

        /// <summary>
        /// 获取玩家对象
        /// </summary>
        private Player GetPlayerById(ushort playerId)
        {
            if (!PhotonNetwork.InRoom) return null;
            foreach (var player in PhotonNetwork.PlayerList)
                if (player.ActorNumber == playerId) return player;
            return null;
        }

        public string GetPlayerName(ushort playerId)
        {
            var player = GetPlayerById(playerId);
            return player?.NickName ?? $"Player_{playerId}";
        }

        public bool IsHostPlayer(ushort playerId)
        {
            return IsHost && playerId == ClientId;
        }

        /// <summary>
        /// 获取所有在线玩家ID
        /// </summary>
        public List<ushort> GetAllOnlinePlayerIds()
        {
            var playerIds = new List<ushort>();
            if (!PhotonNetwork.InRoom) return playerIds;

            foreach (var player in PhotonNetwork.PlayerList)
                playerIds.Add((ushort)player.ActorNumber);
            return playerIds;
        }

        /// <summary>
        /// 通知组件的统一方法
        /// </summary>
        private void NotifyComponent<T>(string methodName, params object[] parameters) where T : MonoBehaviour
        {
            var component = FindObjectOfType<T>();
            if (component == null) return;

            var method = component.GetType().GetMethod(methodName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(component, parameters);
        }

        #endregion

        #region 玩家准备状态

        public void SetPlayerReady(bool isReady)
        {
            if (!PhotonNetwork.InRoom) return;
            SetMyPlayerProperty("isReady", isReady);
        }

        public bool GetPlayerReady(ushort playerId)
        {
            return GetPlayerProperty<bool>(playerId, "isReady", false);
        }

        public bool GetMyReadyState()
        {
            return GetPlayerReady(ClientId);
        }

        public int GetReadyPlayerCount()
        {
            if (!IsConnected) return 0;

            int readyCount = 0;
            foreach (var player in PhotonNetwork.PlayerList)
            {
                if (!player.IsMasterClient && GetPlayerReady((ushort)player.ActorNumber))
                {
                    readyCount++;
                }
            }
            return readyCount;
        }

        public int GetNonHostPlayerCount()
        {
            if (!IsConnected) return 0;

            int nonHostCount = 0;
            foreach (var player in PhotonNetwork.PlayerList)
            {
                if (!player.IsMasterClient)
                {
                    nonHostCount++;
                }
            }
            return nonHostCount;
        }

        public bool AreAllPlayersReady()
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.PlayerList.Length < 2) return false;

            int nonHostCount = GetNonHostPlayerCount();
            if (nonHostCount == 0) return false;

            int readyCount = GetReadyPlayerCount();
            return readyCount == nonHostCount;
        }

        #endregion

        #region 答题和回合类消息

        public void SubmitAnswer(string answer)
        {
            SendRPC("OnAnswerSubmitted", RpcTarget.MasterClient, (int)ClientId, answer);
        }

        [PunRPC]
        void OnAnswerSubmitted(int playerId, string answer)
        {
            if (!IsHost) return;
            HostGameManager.Instance?.HandlePlayerAnswer((ushort)playerId, answer);
        }

        public void BroadcastQuestion(NetworkQuestionData question)
        {
            if (!IsHost) return;
            byte[] questionData = question.Serialize();
            if (questionData?.Length > 0)
                SendRPC("OnQuestionReceived_RPC", RpcTarget.All, questionData);
        }

        [PunRPC]
        void OnQuestionReceived_RPC(byte[] questionData)
        {
            if (questionData?.Length == 0) return;
            var question = NetworkQuestionData.Deserialize(questionData);
            if (question != null)
            {
                OnQuestionReceived?.Invoke(question);
                NotifyComponent<NetworkQuestionManagerController>("OnNetworkQuestionReceived", question);
            }
        }

        public void BroadcastAnswerResult(bool isCorrect, string correctAnswer)
        {
            if (!IsHost) return;
            SendRPC("OnAnswerResult_RPC", RpcTarget.All, isCorrect, correctAnswer);
        }

        [PunRPC]
        void OnAnswerResult_RPC(bool isCorrect, string correctAnswer)
        {
            OnAnswerResultReceived?.Invoke(isCorrect, correctAnswer);
            MessageNotifier.Show($"回答{(isCorrect ? "正确" : "错误")} - {correctAnswer}",
                isCorrect ? MessageType.Success : MessageType.Error);
        }

        public void BroadcastPlayerTurnChanged(ushort newTurnPlayerId)
        {
            if (!IsHost) return;
            SendRPC("OnPlayerTurnChanged_RPC", RpcTarget.All, (int)newTurnPlayerId);
        }

        [PunRPC]
        void OnPlayerTurnChanged_RPC(int newTurnPlayerId)
        {
            ushort playerId = (ushort)newTurnPlayerId;
            OnPlayerTurnChanged?.Invoke(playerId);
            NotifyComponent<NetworkUI>("OnTurnChangedReceived", playerId);
            NotifyComponent<NetworkQuestionManagerController>("OnNetworkPlayerTurnChanged", playerId);
        }

        public void BroadcastPlayerAnswerResult(ushort playerId, bool isCorrect, string answer)
        {
            if (!IsHost) return;
            SendRPC("OnPlayerAnswerResult_RPC", RpcTarget.All, (int)playerId, isCorrect, answer);
            var cardManager = PlayerCardManager.Instance;
            if (cardManager != null)
            {
                cardManager.ResetPlayerUsageOpportunity(playerId);
            }
            else
            {
                LogDebug("PlayerCardManager单例不存在，无法重置卡牌使用");
            }
        }

        [PunRPC]
        void OnPlayerAnswerResult_RPC(int playerId, bool isCorrect, string answer)
        {
            ushort playerIdUShort = (ushort)playerId;
            OnPlayerAnswerResult?.Invoke(playerIdUShort, isCorrect, answer);
            NotifyComponent<NetworkUI>("OnPlayerAnswerResultReceived", (ushort)playerId, isCorrect, answer);
            var cardManager = PlayerCardManager.Instance ?? FindObjectOfType<PlayerCardManager>();
            if (cardManager != null)
            {
                cardManager.ResetPlayerUsageOpportunity(playerId);
                LogDebug($"Client端也重置了玩家{playerId}的卡牌使用机会");
            }
        }

        #endregion

        #region 玩家状态类消息

        public void RequestPlayerHealthChange(int playerId, int healthChange, string reason = "")
        {
            SendRPC("OnPlayerHealthChangeRequested_RPC", RpcTarget.MasterClient, playerId, healthChange, reason);
        }

        [PunRPC]
        void OnPlayerHealthChangeRequested_RPC(int playerId, int healthChange, string reason)
        {
            if (!IsHost || HostGameManager.Instance == null) return;

            // 通过反射获取hpManager执行血量修改
            var hostManager = HostGameManager.Instance;
            var hpManagerField = hostManager.GetType().GetField("hpManager",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (hpManagerField?.GetValue(hostManager) is PlayerHPManager hpManager)
            {
                if (healthChange > 0)
                    hpManager.HealPlayer((ushort)playerId, healthChange, out _);
                else if (healthChange < 0)
                    hpManager.ApplyDamage((ushort)playerId, out _, out _, -healthChange);
            }
        }

        public void BroadcastHealthUpdate(ushort playerId, int newHealth, int maxHealth)
        {
            if (!IsHost) return;
            SendRPC("OnHealthUpdate_RPC", RpcTarget.All, (int)playerId, newHealth, maxHealth);
        }

        [PunRPC]
        void OnHealthUpdate_RPC(int playerId, int newHealth, int maxHealth)
        {
            ushort playerIdUShort = (ushort)playerId;
            OnHealthUpdated?.Invoke(playerIdUShort, newHealth, maxHealth);
            NotifyComponent<NetworkUI>("OnHealthUpdateReceived", playerIdUShort, newHealth, maxHealth);
        }

        public void BroadcastPlayerStateSync(ushort playerId, string playerName, bool isHost,
            int currentHealth, int maxHealth, bool isAlive)
        {
            if (!IsHost) return;
            SendRPC("OnPlayerStateSync_RPC", RpcTarget.All, (int)playerId, playerName, isHost,
                currentHealth, maxHealth, isAlive);
        }

        [PunRPC]
        void OnPlayerStateSync_RPC(int playerId, string playerName, bool isHost,
            int currentHealth, int maxHealth, bool isAlive)
        {
            NotifyComponent<NetworkUI>("OnPlayerStateSyncReceived", (ushort)playerId, playerName,
                isHost, currentHealth, maxHealth, isAlive);
        }

        public void BroadcastPlayerDeath(ushort playerId, string playerName)
        {
            if (!IsHost) return;
            SendRPC("OnPlayerDeath_RPC", RpcTarget.All, (int)playerId, playerName);
        }

        [PunRPC]
        void OnPlayerDeath_RPC(int playerId, string playerName)
        {
            OnPlayerDied?.Invoke((ushort)playerId, playerName);
        }

        #endregion

        #region 卡牌效果类消息

        public void RequestSetPlayerTimeBonus(int playerId, float bonusTime)
        {
            SendRPC("OnSetPlayerTimeBonusRequested_RPC", RpcTarget.MasterClient, playerId, bonusTime);
        }

        [PunRPC]
        void OnSetPlayerTimeBonusRequested_RPC(int playerId, float bonusTime)
        {
            if (!IsHost) return;
            Cards.Integration.CardGameBridge.Instance?.OnSetPlayerTimeBonusReceived(playerId, bonusTime);
        }

        public void RequestSetPlayerTimePenalty(int playerId, float penaltyTime)
        {
            SendRPC("OnSetPlayerTimePenaltyRequested_RPC", RpcTarget.MasterClient, playerId, penaltyTime);
        }

        [PunRPC]
        void OnSetPlayerTimePenaltyRequested_RPC(int playerId, float penaltyTime)
        {
            if (!IsHost) return;
            Cards.Integration.CardGameBridge.Instance?.OnSetPlayerTimePenaltyReceived(playerId, penaltyTime);
        }

        public void RequestSetPlayerSkipFlag(int playerId, bool shouldSkip)
        {
            SendRPC("OnSetPlayerSkipFlagRequested_RPC", RpcTarget.MasterClient, playerId, shouldSkip);
        }

        [PunRPC]
        void OnSetPlayerSkipFlagRequested_RPC(int playerId, bool shouldSkip)
        {
            if (!IsHost) return;
            Cards.Integration.CardGameBridge.Instance?.OnSetPlayerSkipFlagReceived(playerId, shouldSkip);
        }

        public void RequestSetPlayerNextQuestionType(int playerId, string questionType)
        {
            SendRPC("OnSetPlayerNextQuestionTypeRequested_RPC", RpcTarget.MasterClient, playerId, questionType);
        }

        [PunRPC]
        void OnSetPlayerNextQuestionTypeRequested_RPC(int playerId, string questionType)
        {
            if (!IsHost) return;
            Cards.Integration.CardGameBridge.Instance?.OnSetPlayerNextQuestionTypeReceived(playerId, questionType);
        }

        public void RequestSetGlobalDamageMultiplier(float multiplier)
        {
            SendRPC("OnSetGlobalDamageMultiplierRequested_RPC", RpcTarget.MasterClient, multiplier);
        }

        [PunRPC]
        void OnSetGlobalDamageMultiplierRequested_RPC(float multiplier)
        {
            if (!IsHost) return;
            Cards.Integration.CardGameBridge.Instance?.OnSetGlobalDamageMultiplierReceived(multiplier);
        }

        #endregion

        #region 卡牌操作类消息

        public void RequestGiveCardToPlayer(int playerId, int cardId, int count)
        {
            SendRPC("OnGiveCardToPlayerRequested_RPC", RpcTarget.MasterClient, playerId, cardId, count);
        }

        [PunRPC]
        void OnGiveCardToPlayerRequested_RPC(int playerId, int cardId, int count)
        {
            if (!IsHost) return;
            Cards.Integration.CardGameBridge.Instance?.OnGiveCardToPlayerReceived(playerId, cardId, count);
        }

        public void RequestStealRandomCard(int fromPlayerId, int toPlayerId)
        {
            SendRPC("OnStealRandomCardRequested_RPC", RpcTarget.MasterClient, fromPlayerId, toPlayerId);
        }

        [PunRPC]
        void OnStealRandomCardRequested_RPC(int fromPlayerId, int toPlayerId)
        {
            if (!IsHost) return;
            Cards.Integration.CardGameBridge.Instance?.OnStealRandomCardReceived(fromPlayerId, toPlayerId);
        }

        public void SendPlayerHandCards(ushort playerId, List<int> cardIds)
        {
            if (!IsHost || cardIds?.Count == 0) return;

            // 使用 RpcTarget.All，在 RPC 方法中判断目标玩家
            SendRPC("OnPlayerHandCardsReceived_RPC", RpcTarget.All, (int)playerId, cardIds.ToArray());
        }

        [PunRPC]
        void OnPlayerHandCardsReceived_RPC(int playerId, int[] cardIds)
        {
            if (ClientId == (ushort)playerId)
            {
                if (cardIds?.Length > 0)
                {
                    Cards.Network.CardNetworkManager.Instance?.OnHandCardsDistributionReceived(
                        (ushort)playerId, new List<int>(cardIds));
                }
            }
        }
        public void SendPlayerHandCardsToSpecificPlayer(ushort playerId, List<int> cardIds)
        {
            if (!IsHost || cardIds?.Count == 0) return;

            var targetPlayer = GetPlayerById(playerId);
            var persistentManager = PersistentNetworkManager.Instance;

            if (targetPlayer != null && persistentManager?.photonView != null)
            {
                // 直接使用 photonView.RPC 发送给特定玩家
                persistentManager.photonView.RPC("OnPlayerHandCardsReceived_RPC", targetPlayer, (int)playerId, cardIds.ToArray());
            }
        }

        // 卡牌广播方法组
        public void BroadcastCardUsed(ushort playerId, int cardId, ushort targetPlayerId, string cardName)
        {
            if (!IsHost) return;
            SendRPC("OnCardUsed_RPC", RpcTarget.All, (int)playerId, cardId, (int)targetPlayerId, cardName);
        }

        [PunRPC]
        void OnCardUsed_RPC(int playerId, int cardId, int targetPlayerId, string cardName)
        {
            ushort p = (ushort)playerId, t = (ushort)targetPlayerId;
            OnCardUsed?.Invoke(p, cardId, t, cardName);
            Cards.Network.CardNetworkManager.Instance?.OnCardUsedReceived(p, cardId, t, cardName);
        }

        public void BroadcastCardEffectTriggered(ushort playerId, string effectDescription)
        {
            if (!IsHost) return;
            SendRPC("OnCardEffectTriggered_RPC", RpcTarget.All, (int)playerId, effectDescription);
        }

        [PunRPC]
        void OnCardEffectTriggered_RPC(int playerId, string effectDescription)
        {
            ushort p = (ushort)playerId;
            OnCardEffectTriggered?.Invoke(p, effectDescription);
            Cards.Network.CardNetworkManager.Instance?.OnCardEffectTriggeredReceived(p, effectDescription);
        }

        public void BroadcastCardAdded(ushort playerId, int cardId)
        {
            if (!IsHost) return;
            SendRPC("OnCardAdded_RPC", RpcTarget.All, (int)playerId, cardId);
        }

        [PunRPC]
        void OnCardAdded_RPC(int playerId, int cardId)
        {
            ushort p = (ushort)playerId;
            OnCardAdded?.Invoke(p, cardId);
            Cards.Network.CardNetworkManager.Instance?.OnCardAddedReceived(p, cardId);
        }

        public void BroadcastCardRemoved(ushort playerId, int cardId)
        {
            if (!IsHost) return;
            SendRPC("OnCardRemoved_RPC", RpcTarget.All, (int)playerId, cardId);
        }

        [PunRPC]
        void OnCardRemoved_RPC(int playerId, int cardId)
        {
            ushort p = (ushort)playerId;
            OnCardRemoved?.Invoke(p, cardId);
            Cards.Network.CardNetworkManager.Instance?.OnCardRemovedReceived(p, cardId);
        }

        public void BroadcastHandSizeChanged(ushort playerId, int newHandSize)
        {
            if (!IsHost) return;
            SendRPC("OnHandSizeChanged_RPC", RpcTarget.All, (int)playerId, newHandSize);
        }

        [PunRPC]
        void OnHandSizeChanged_RPC(int playerId, int newHandSize)
        {
            ushort p = (ushort)playerId;
            OnHandSizeChanged?.Invoke(p, newHandSize);
            Cards.Network.CardNetworkManager.Instance?.OnHandSizeChangedReceived(p, newHandSize);
        }

        public void BroadcastCardMessage(string message, ushort fromPlayerId)
        {
            if (!IsHost) return;
            SendRPC("OnCardMessage_RPC", RpcTarget.All, message, (int)fromPlayerId);
        }

        [PunRPC]
        void OnCardMessage_RPC(string message, int fromPlayerId)
        {
            ushort p = (ushort)fromPlayerId;
            OnCardMessage?.Invoke(message, p);
            Cards.Network.CardNetworkManager.Instance?.OnCardMessageReceived(message, p);
        }

        public void BroadcastCardTransferred(ushort fromPlayerId, int cardId, ushort toPlayerId)
        {
            if (!IsHost) return;
            SendRPC("OnCardTransferred_RPC", RpcTarget.All, (int)fromPlayerId, cardId, (int)toPlayerId);
        }

        [PunRPC]
        void OnCardTransferred_RPC(int fromPlayerId, int cardId, int toPlayerId)
        {
            ushort f = (ushort)fromPlayerId, t = (ushort)toPlayerId;
            OnCardTransferred?.Invoke(f, cardId, t);
            Cards.Network.CardNetworkManager.Instance?.OnCardTransferredReceived(f, cardId, t);
        }

        #endregion

        #region 游戏流程类消息

        public void BroadcastGameStart(int totalPlayerCount, int alivePlayerCount, ushort firstTurnPlayerId)
        {
            if (!IsHost) return;
            SendRPC("OnGameStart_RPC", RpcTarget.All, totalPlayerCount, alivePlayerCount, (int)firstTurnPlayerId);
        }

        [PunRPC]
        void OnGameStart_RPC(int totalPlayerCount, int alivePlayerCount, int firstTurnPlayerId)
        {
            ushort firstPlayer = (ushort)firstTurnPlayerId;
            MessageNotifier.Show("游戏开始", MessageType.Info);
            OnGameStarted?.Invoke(totalPlayerCount, alivePlayerCount, firstPlayer);
            NotifyComponent<NetworkUI>("OnGameStartReceived", totalPlayerCount, alivePlayerCount, firstPlayer);
            NotifyComponent<NetworkQuestionManagerController>("OnNetworkGameStarted", totalPlayerCount, alivePlayerCount, firstPlayer);
        }

        public void BroadcastGameProgress(int questionNumber, int alivePlayerCount, ushort turnPlayerId, int questionType, float timeLimit)
        {
            if (!IsHost) return;
            SendRPC("OnGameProgress_RPC", RpcTarget.All, questionNumber, alivePlayerCount, (int)turnPlayerId, questionType, timeLimit);
        }

        [PunRPC]
        void OnGameProgress_RPC(int questionNumber, int alivePlayerCount, int turnPlayerId, int questionType, float timeLimit)
        {
            NotifyComponent<NetworkUI>("OnGameProgressReceived", questionNumber, alivePlayerCount, (ushort)turnPlayerId);
        }

        public void BroadcastGameVictory(ushort winnerId, string winnerName, string reason)
        {
            if (!IsHost) return;
            SendRPC("OnGameVictory_RPC", RpcTarget.All, (int)winnerId, winnerName, reason);
        }

        [PunRPC]
        void OnGameVictory_RPC(int winnerId, string winnerName, string reason)
        {
            OnGameVictory?.Invoke((ushort)winnerId, winnerName, reason);
            NotifyComponent<NetworkQuestionManagerController>("StopGame");

            // 新增：延迟重置房间状态
            if (IsHost)
            {
                StartCoroutine(DelayedRoomStateReset());
            }
        }

        public void BroadcastGameEndWithoutWinner(string reason)
        {
            if (!IsHost) return;
            SendRPC("OnGameEndWithoutWinner_RPC", RpcTarget.All, reason);
        }

        [PunRPC]
        void OnGameEndWithoutWinner_RPC(string reason)
        {
            OnGameEndWithoutWinner?.Invoke(reason);
            NotifyComponent<NetworkQuestionManagerController>("StopGame");

            // 新增：延迟重置房间状态
            if (IsHost)
            {
                StartCoroutine(DelayedRoomStateReset());
            }
        }

        /// <summary>
        /// 延迟重置房间状态（游戏结束后）
        /// </summary>
        private IEnumerator DelayedRoomStateReset()
        {
            // 等待游戏结束逻辑完成
            yield return new WaitForSeconds(roomStateResetDelay);

            // 检查是否还在房间中（可能已经返回了）
            if (IsConnected)
            {
                ResetRoomStateAfterGame();
                LogDebug("游戏结束后自动重置房间状态");
            }
        }

        public void BroadcastForceReturnToRoom(string reason)
        {
            if (!IsHost) return;
            SendRPC("OnForceReturnToRoom_RPC", RpcTarget.All, reason);
        }

        [PunRPC]
        void OnForceReturnToRoom_RPC(string reason)
        {
            MessageNotifier.Show("强制返回房间", MessageType.System);
            OnForceReturnToRoom?.Invoke(reason);
            StartCoroutine(HandleReturnToRoom());
        }

        private IEnumerator HandleReturnToRoom()
        {
            yield return new WaitForSeconds(1f);
            try
            {
                string currentScene = SceneManager.GetActiveScene().name;
                if (currentScene.Contains("Game") || currentScene.Contains("Network"))
                    SceneManager.LoadScene("RoomScene");
                else if (currentScene.Contains("Room"))
                    SceneManager.LoadScene(currentScene);
                else
                    SceneManager.LoadScene("MainMenuScene");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkManager] 返回房间失败: {e.Message}");
                if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
            }
        }

        #endregion

        #region 场景管理类消息

        public void BroadcastSeatsAndCharacters(ushort[] playerIds, int[] seatIndices)
        {
            if (!IsHost) return;
            int[] playerIdsInt = new int[playerIds.Length];
            for (int i = 0; i < playerIds.Length; i++) playerIdsInt[i] = playerIds[i];
            SendRPC("OnSeatsAndCharacters_RPC", RpcTarget.All, playerIdsInt, seatIndices);
        }

        [PunRPC]
        void OnSeatsAndCharacters_RPC(int[] playerIds, int[] seatIndices)
        {
            ushort[] playerIdsUShort = new ushort[playerIds.Length];
            for (int i = 0; i < playerIds.Length; i++) playerIdsUShort[i] = (ushort)playerIds[i];

            var spawner = FindObjectOfType<Classroom.Player.NetworkPlayerSpawner>();
            spawner?.ReceiveSeatsAndCharactersData(playerIdsUShort, seatIndices);
        }

        #endregion

        #region 头部旋转同步

        /// <summary>
        /// 同步玩家头部旋转（本地玩家 → 其他玩家）
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="horizontalAngle">头部水平角度</param>
        /// <param name="verticalAngle">头部垂直角度</param>
        public void SyncPlayerHeadRotation(ushort playerId, float horizontalAngle, float verticalAngle)
        {
            if (enableSyncDebugLogs)
            {
                LogDebug($"发送头部旋转数据: 玩家{playerId}, H={horizontalAngle:F1}°, V={verticalAngle:F1}°");
            }

            SendRPC("OnPlayerHeadRotation_RPC", RpcTarget.Others, (int)playerId, horizontalAngle, verticalAngle, Time.time);
        }

        /// <summary>
        /// 接收玩家头部旋转RPC
        /// </summary>
        [PunRPC]
        void OnPlayerHeadRotation_RPC(int playerId, float horizontalAngle, float verticalAngle, float timestamp)
        {
            ushort playerIdUShort = (ushort)playerId;

            if (enableSyncDebugLogs)
            {
                float lag = Time.time - timestamp;
                LogDebug($"接收头部旋转数据: 玩家{playerId}, H={horizontalAngle:F1}°, V={verticalAngle:F1}°, 延迟={lag:F3}s");
            }

            // 触发事件
            OnHeadRotationReceived?.Invoke(playerIdUShort, horizontalAngle, verticalAngle);

            // 直接调用对应的PlayerNetworkSync组件
            if (playerSyncComponents.ContainsKey(playerIdUShort))
            {
                playerSyncComponents[playerIdUShort].ReceiveNetworkHeadRotation(horizontalAngle, verticalAngle, timestamp);
            }
            else
            {
                // 备用方案：遍历查找并自动注册
                var allSyncComponents = FindObjectsOfType<Classroom.Player.PlayerNetworkSync>();
                foreach (var syncComponent in allSyncComponents)
                {
                    if (syncComponent.PlayerId == playerIdUShort)
                    {
                        syncComponent.ReceiveNetworkHeadRotation(horizontalAngle, verticalAngle, timestamp);
                        RegisterPlayerSync(playerIdUShort, syncComponent);
                        break;
                    }
                }
            }
        }

        #endregion

        #region 玩家位置同步

        /// <summary>
        /// 注册玩家同步组件
        /// </summary>
        public void RegisterPlayerSync(ushort playerId, Classroom.Player.PlayerNetworkSync syncComponent)
        {
            if (syncComponent == null) return;
            playerSyncComponents[playerId] = syncComponent;
            LogDebug($"注册玩家同步组件: PlayerId={playerId}");
        }

        /// <summary>
        /// 注销玩家同步组件
        /// </summary>
        public void UnregisterPlayerSync(ushort playerId)
        {
            if (playerSyncComponents.ContainsKey(playerId))
            {
                playerSyncComponents.Remove(playerId);
                LogDebug($"注销玩家同步组件: PlayerId={playerId}");
            }
        }

        #endregion

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[NetworkManager] {message}");
        }

    }
}