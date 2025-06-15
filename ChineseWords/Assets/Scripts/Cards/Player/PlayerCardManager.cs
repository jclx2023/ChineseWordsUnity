using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Core;
using Core.Network;
using Cards.Core;

namespace Cards.Player
{
    /// <summary>
    /// 玩家卡牌管理器
    /// 负责管理每个玩家的卡牌状态、使用权限、手牌操作等
    /// 与现有游戏系统集成，实现个人回合制的卡牌机制
    /// </summary>
    public class PlayerCardManager : MonoBehaviour
    {
        [Header("配置设置")]
        [SerializeField] private int maxHandSize = 5;
        [SerializeField] private int initialCardCount = 3;
        [SerializeField] private bool enableDebugLogs = true;

        [Header("卡牌配置")]
        [SerializeField] private CardConfig cardConfig;

        [Header("网络设置")]
        [SerializeField] private bool enableNetworkSync = true;

        // 单例实例
        public static PlayerCardManager Instance { get; private set; }

        // 玩家卡牌状态管理（简化：只使用一个Dictionary）
        private Dictionary<int, EnhancedPlayerCardState> playerCardStates;

        // 当前回合信息
        private int currentTurnPlayerId = 0;
        private bool isInitialized = false;

        #region 事件定义

        /// <summary>
        /// 卡牌使用事件 - playerId, cardId, targetPlayerId（如果有）
        /// </summary>
        public System.Action<int, int, int> OnCardUsed;

        /// <summary>
        /// 卡牌获得事件 - playerId, cardId, cardName
        /// </summary>
        public System.Action<int, int, string> OnCardAcquired;

        /// <summary>
        /// 使用机会重置事件 - playerId
        /// </summary>
        public System.Action<int> OnUsageOpportunityReset;

        /// <summary>
        /// 卡牌转移事件 - fromPlayerId, toPlayerId, cardId
        /// </summary>
        public System.Action<int, int, int> OnCardTransferred;

        #endregion

        #region Unity生命周期

        private void Awake()
        {
            // 单例模式
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                playerCardStates = new Dictionary<int, EnhancedPlayerCardState>();

                LogDebug("PlayerCardManager实例已创建");
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                UnsubscribeFromEvents();
                Instance = null;
            }
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化卡牌管理器
        /// </summary>
        public void Initialize()
        {
            if (isInitialized)
            {
                LogDebug("PlayerCardManager已初始化，跳过重复初始化");
                return;
            }

            try
            {
                // 获取卡牌配置
                if (cardConfig == null)
                {
                    cardConfig = Resources.Load<CardConfig>("CardConfig");
                    if (cardConfig == null)
                    {
                        Debug.LogError("[PlayerCardManager] 无法加载CardConfig资源");
                        return;
                    }
                }

                // 验证配置
                if (!cardConfig.ValidateConfig())
                {
                    Debug.LogError("[PlayerCardManager] CardConfig验证失败");
                    return;
                }

                // 从配置中获取设置
                maxHandSize = cardConfig.SystemSettings.maxHandSize;
                initialCardCount = cardConfig.SystemSettings.startingCardCount;

                // 订阅游戏事件
                SubscribeToGameEvents();

                isInitialized = true;
                LogDebug("PlayerCardManager初始化完成");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PlayerCardManager] 初始化失败: {e.Message}");
            }
        }

        /// <summary>
        /// 订阅游戏事件
        /// </summary>
        private void SubscribeToGameEvents()
        {
            // 订阅卡牌系统事件
            CardEvents.OnPlayerTurnCompleted += OnPlayerAnswerCompleted;

            LogDebug("已订阅游戏事件");
        }

        /// <summary>
        /// 取消订阅游戏事件
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            // 取消订阅卡牌系统事件
            CardEvents.OnPlayerTurnCompleted -= OnPlayerAnswerCompleted;

            // 取消自定义事件订阅
            OnCardUsed = null;
            OnCardAcquired = null;
            OnUsageOpportunityReset = null;
            OnCardTransferred = null;

            LogDebug("已取消所有事件订阅");
        }

        #endregion

        #region 玩家管理

        /// <summary>
        /// 初始化玩家卡牌状态
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="playerName">玩家名称</param>
        public void InitializePlayer(int playerId, string playerName = "")
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[PlayerCardManager] 管理器未初始化，无法初始化玩家");
                return;
            }

            if (playerCardStates.ContainsKey(playerId))
            {
                LogDebug($"玩家 {playerId} 的卡牌状态已存在，跳过重复初始化");
                return;
            }

            // 创建增强的玩家卡牌状态
            var cardState = new EnhancedPlayerCardState(maxHandSize, cardConfig.DrawSettings)
            {
                playerId = playerId,
                playerName = string.IsNullOrEmpty(playerName) ? $"Player{playerId}" : playerName,
                canUseCardThisRound = true
            };

            // 给玩家初始卡牌
            for (int i = 0; i < initialCardCount; i++)
            {
                var randomCard = DrawRandomCard();
                if (randomCard != null && cardState.CanAddSpecificCard(randomCard.cardId))
                {
                    cardState.AddCard(randomCard.cardId);

                    LogDebug($"为玩家 {playerId} 添加初始卡牌: {randomCard.cardName}");

                    // 触发卡牌获得事件
                    OnCardAcquired?.Invoke(playerId, randomCard.cardId, randomCard.cardName);
                    CardEvents.OnCardAddedToHand?.Invoke(playerId, randomCard.cardId);
                }
            }

            playerCardStates[playerId] = cardState;

            LogDebug($"玩家 {playerId} 卡牌状态初始化完成 - 初始卡牌数: {cardState.HandCount}");
        }

        /// <summary>
        /// 移除玩家
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        public void RemovePlayer(int playerId)
        {
            if (playerCardStates.ContainsKey(playerId))
            {
                playerCardStates.Remove(playerId);
                LogDebug($"玩家 {playerId} 的卡牌状态已移除");
            }
        }

        /// <summary>
        /// 清理所有玩家数据
        /// </summary>
        public void ClearAllPlayers()
        {
            playerCardStates.Clear();
            currentTurnPlayerId = 0;
            LogDebug("所有玩家卡牌数据已清理");
        }

        #endregion

        #region 卡牌使用

        /// <summary>
        /// 使用卡牌
        /// </summary>
        /// <param name="playerId">使用者ID</param>
        /// <param name="cardId">卡牌ID</param>
        /// <param name="targetPlayerId">目标玩家ID（可选）</param>
        /// <returns>是否成功使用</returns>
        public bool UseCard(int playerId, int cardId, int targetPlayerId = -1)
        {
            if (!ValidateCardUsage(playerId, cardId))
            {
                return false;
            }

            try
            {
                // 获取卡牌数据
                var cardData = cardConfig.GetCardById(cardId);
                if (cardData == null)
                {
                    LogDebug($"未找到卡牌: {cardId}");
                    return false;
                }

                // 从玩家手牌中移除并标记已使用
                var cardState = playerCardStates[playerId];
                cardState.RemoveCard(cardId);
                cardState.MarkCardUsedThisRound();

                LogDebug($"玩家 {playerId} 使用卡牌: {cardData.cardName}");

                // 创建使用请求
                var useRequest = new CardUseRequest
                {
                    userId = playerId,
                    cardId = cardId,
                    targetPlayerId = targetPlayerId,
                    timestamp = Time.time
                };

                // 触发卡牌使用事件
                CardEvents.OnCardUseRequested?.Invoke(useRequest, cardData);
                OnCardUsed?.Invoke(playerId, cardId, targetPlayerId);

                // TODO: 执行卡牌效果（等CardEffectSystem实现后）
                // 这里应该调用CardEffectSystem来执行实际效果

                LogDebug($"卡牌 {cardData.cardName} 使用成功");

                // 触发移除事件
                CardEvents.OnCardRemovedFromHand?.Invoke(playerId, cardId);

                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PlayerCardManager] 使用卡牌失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 验证卡牌使用是否有效
        /// </summary>
        private bool ValidateCardUsage(int playerId, int cardId)
        {
            if (!playerCardStates.ContainsKey(playerId))
            {
                LogDebug($"玩家 {playerId} 的卡牌状态不存在");
                return false;
            }

            var cardState = playerCardStates[playerId];

            if (!cardState.canUseCardThisRound)
            {
                LogDebug($"玩家 {playerId} 本回合已使用过卡牌");
                return false;
            }

            if (!cardState.HasCard(cardId))
            {
                LogDebug($"玩家 {playerId} 手牌中没有卡牌: {cardId}");
                return false;
            }

            // 检查卡牌是否允许在非自己回合使用
            var cardData = cardConfig.GetCardById(cardId);
            if (cardData != null && !cardData.canUseWhenNotMyTurn && currentTurnPlayerId != playerId)
            {
                LogDebug($"卡牌 {cardData.cardName} 不允许在非自己回合使用");
                return false;
            }

            return true;
        }

        #endregion

        #region 卡牌获得

        /// <summary>
        /// 给玩家添加卡牌
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="cardId">卡牌ID（0表示随机获得）</param>
        /// <returns>是否成功添加</returns>
        public bool GiveCardToPlayer(int playerId, int cardId = 0)
        {
            if (!playerCardStates.ContainsKey(playerId))
            {
                LogDebug($"玩家 {playerId} 的卡牌状态不存在");
                return false;
            }

            var cardState = playerCardStates[playerId];

            CardData cardData;
            if (cardId == 0)
            {
                // 随机获得卡牌
                cardData = DrawRandomCard();
                if (cardData == null)
                {
                    LogDebug("随机抽取卡牌失败");
                    return false;
                }
            }
            else
            {
                // 获得指定卡牌
                cardData = cardConfig.GetCardById(cardId);
                if (cardData == null)
                {
                    LogDebug($"未找到指定卡牌: {cardId}");
                    return false;
                }
            }

            // 检查是否可以添加（包含容量和规则检查）
            if (!cardState.CanAddSpecificCard(cardData.cardId))
            {
                LogDebug($"玩家 {playerId} 无法添加卡牌 {cardData.cardName}（手牌已满或违反规则）");
                return false;
            }

            // 添加到玩家手牌
            cardState.AddCard(cardData.cardId);

            LogDebug($"玩家 {playerId} 获得卡牌: {cardData.cardName}");

            // 触发卡牌获得事件
            OnCardAcquired?.Invoke(playerId, cardData.cardId, cardData.cardName);
            CardEvents.OnCardAddedToHand?.Invoke(playerId, cardData.cardId);

            return true;
        }

        /// <summary>
        /// 随机抽取卡牌
        /// </summary>
        private CardData DrawRandomCard()
        {
            if (cardConfig == null || cardConfig.AllCards.Count == 0)
            {
                return null;
            }

            // 基于权重的随机抽取
            float totalWeight = 0f;
            foreach (var card in cardConfig.AllCards)
            {
                totalWeight += card.drawWeight;
            }

            float randomValue = Random.Range(0f, totalWeight);
            float currentWeight = 0f;

            foreach (var card in cardConfig.AllCards)
            {
                currentWeight += card.drawWeight;
                if (randomValue <= currentWeight)
                {
                    return card;
                }
            }

            // 如果没有选中，返回第一张卡牌
            return cardConfig.AllCards[0];
        }

        /// <summary>
        /// 转移卡牌（从一个玩家到另一个玩家）
        /// </summary>
        public bool TransferCard(int fromPlayerId, int toPlayerId, int cardId)
        {
            if (!playerCardStates.ContainsKey(fromPlayerId) || !playerCardStates.ContainsKey(toPlayerId))
            {
                LogDebug("源玩家或目标玩家状态不存在");
                return false;
            }

            var fromState = playerCardStates[fromPlayerId];
            var toState = playerCardStates[toPlayerId];

            // 验证源玩家有此卡牌
            if (!fromState.HasCard(cardId))
            {
                LogDebug($"玩家 {fromPlayerId} 没有卡牌: {cardId}");
                return false;
            }

            // 验证目标玩家是否可以接收卡牌
            if (!toState.CanAddSpecificCard(cardId))
            {
                LogDebug($"玩家 {toPlayerId} 无法接收卡牌: {cardId}");
                return false;
            }

            // 执行转移
            fromState.RemoveCard(cardId);
            toState.AddCard(cardId);

            var cardData = cardConfig.GetCardById(cardId);
            LogDebug($"卡牌转移成功: {cardData?.cardName} 从玩家{fromPlayerId}转移到玩家{toPlayerId}");

            // 触发转移事件
            OnCardTransferred?.Invoke(fromPlayerId, toPlayerId, cardId);
            CardEvents.OnCardRemovedFromHand?.Invoke(fromPlayerId, cardId);
            CardEvents.OnCardAddedToHand?.Invoke(toPlayerId, cardId);

            return true;
        }

        #endregion

        #region 回合管理

        /// <summary>
        /// 设置当前回合玩家
        /// </summary>
        /// <param name="playerId">当前回合玩家ID</param>
        public void SetCurrentTurnPlayer(int playerId)
        {
            currentTurnPlayerId = playerId;
            LogDebug($"当前回合玩家设置为: {playerId}");
        }

        /// <summary>
        /// 重置玩家的卡牌使用机会（个人回合制）
        /// 应在玩家答题完成后调用
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        public void ResetPlayerUsageOpportunity(int playerId)
        {
            if (!playerCardStates.ContainsKey(playerId))
            {
                LogDebug($"玩家 {playerId} 的卡牌状态不存在");
                return;
            }

            var cardState = playerCardStates[playerId];
            cardState.ResetForNewRound();

            LogDebug($"玩家 {playerId} 的卡牌使用机会已重置");

            // 触发重置事件
            OnUsageOpportunityReset?.Invoke(playerId);
            CardEvents.OnPlayerCardUsageReset?.Invoke(playerId);
        }

        /// <summary>
        /// 重置所有玩家的卡牌使用机会
        /// </summary>
        public void ResetAllPlayersUsageOpportunity()
        {
            foreach (var playerId in playerCardStates.Keys.ToList())
            {
                ResetPlayerUsageOpportunity(playerId);
            }
            LogDebug("所有玩家的卡牌使用机会已重置");
        }

        #endregion

        #region 查询方法

        /// <summary>
        /// 获取玩家手牌ID列表
        /// </summary>
        public List<int> GetPlayerHand(int playerId)
        {
            if (playerCardStates.ContainsKey(playerId))
            {
                return playerCardStates[playerId].GetHandCards();
            }
            return new List<int>();
        }

        /// <summary>
        /// 获取玩家手牌的CardData列表
        /// </summary>
        public List<CardData> GetPlayerHandCards(int playerId)
        {
            var handCardIds = GetPlayerHand(playerId);
            var handCards = new List<CardData>();

            foreach (var cardId in handCardIds)
            {
                var cardData = cardConfig.GetCardById(cardId);
                if (cardData != null)
                {
                    handCards.Add(cardData);
                }
            }

            return handCards;
        }

        /// <summary>
        /// 检查玩家是否可以使用卡牌
        /// </summary>
        public bool CanPlayerUseCards(int playerId)
        {
            if (!playerCardStates.ContainsKey(playerId))
                return false;

            var cardState = playerCardStates[playerId];
            return cardState.canUseCardThisRound;
        }

        /// <summary>
        /// 获取玩家手牌数量
        /// </summary>
        public int GetPlayerHandCount(int playerId)
        {
            if (playerCardStates.ContainsKey(playerId))
            {
                return playerCardStates[playerId].HandCount;
            }
            return 0;
        }

        /// <summary>
        /// 获取玩家卡牌状态摘要
        /// </summary>
        public string GetPlayerCardSummary(int playerId)
        {
            if (!playerCardStates.ContainsKey(playerId))
                return "玩家状态不存在";

            var cardState = playerCardStates[playerId];
            return $"玩家 {cardState.playerName}: 手牌数 {cardState.HandCount}/{maxHandSize}, " +
                   $"可使用: {(cardState.canUseCardThisRound ? "是" : "否")}";
        }

        /// <summary>
        /// 获取玩家状态
        /// </summary>
        public EnhancedPlayerCardState GetPlayerState(int playerId)
        {
            return playerCardStates.ContainsKey(playerId) ? playerCardStates[playerId] : null;
        }

        #endregion

        #region 与现有系统集成

        /// <summary>
        /// 答题完成后的回调（集成点）
        /// 应该在HostGameManager.HandlePlayerAnswer中调用
        /// </summary>
        public void OnPlayerAnswerCompleted(int playerId)
        {
            // 重置该玩家的卡牌使用机会
            ResetPlayerUsageOpportunity(playerId);

            LogDebug($"玩家 {playerId} 答题完成，卡牌使用机会已重置");
        }

        #endregion

        #region 调试和工具方法

        /// <summary>
        /// 获取所有玩家的卡牌状态（调试用）
        /// </summary>
        public Dictionary<int, string> GetAllPlayerCardSummaries()
        {
            var summaries = new Dictionary<int, string>();
            foreach (var playerId in playerCardStates.Keys)
            {
                summaries[playerId] = GetPlayerCardSummary(playerId);
            }
            return summaries;
        }

        /// <summary>
        /// 强制给玩家添加指定卡牌（调试用）
        /// </summary>
        public bool ForceGiveCard(int playerId, int cardId)
        {
            LogDebug($"[调试] 强制给玩家 {playerId} 添加卡牌: {cardId}");
            return GiveCardToPlayer(playerId, cardId);
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerCardManager] {message}");
            }
        }

        #endregion

        #region 公共接口属性

        public bool IsInitialized => isInitialized;
        public int PlayerCount => playerCardStates.Count;
        public int CurrentTurnPlayerId => currentTurnPlayerId;
        public CardConfig Config => cardConfig;

        #endregion
    }

    #region 增强的PlayerCardState

    /// <summary>
    /// 增强的玩家卡牌状态
    /// 整合了原来CardInventory的功能
    /// </summary>
    [System.Serializable]
    public class EnhancedPlayerCardState : PlayerCardState
    {
        private int maxHandSize;
        private CardDrawSettings drawSettings;

        public EnhancedPlayerCardState(int maxSize, CardDrawSettings settings)
        {
            maxHandSize = maxSize;
            drawSettings = settings;
            handCards = new List<int>();
        }

        /// <summary>
        /// 手牌数量
        /// </summary>
        public int HandCount => handCards.Count;

        /// <summary>
        /// 是否手牌已满
        /// </summary>
        public bool IsFull => handCards.Count >= maxHandSize;

        /// <summary>
        /// 剩余容量
        /// </summary>
        public int RemainingCapacity => maxHandSize - handCards.Count;

        /// <summary>
        /// 检查是否可以添加卡牌（覆盖基类的简单容量检查）
        /// </summary>
        public new bool CanAddCard => !IsFull;

        /// <summary>
        /// 检查是否可以添加指定卡牌（包含规则验证）
        /// </summary>
        public bool CanAddSpecificCard(int cardId)
        {
            // 检查容量限制
            if (IsFull)
            {
                return false;
            }

            // 检查禁用卡牌
            if (drawSettings.bannedCardIds.Contains(cardId))
            {
                return false;
            }

            // 检查重复限制
            if (!drawSettings.allowDuplicates && handCards.Contains(cardId))
            {
                return false;
            }

            // 检查同种卡牌数量限制
            int cardCount = handCards.Count(id => id == cardId);
            if (cardCount >= drawSettings.maxSameCardInHand)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 添加卡牌
        /// </summary>
        public bool AddCard(int cardId)
        {
            if (!CanAddSpecificCard(cardId))
            {
                return false;
            }

            handCards.Add(cardId);
            return true;
        }

        /// <summary>
        /// 移除卡牌
        /// </summary>
        public bool RemoveCard(int cardId)
        {
            return handCards.Remove(cardId);
        }

        /// <summary>
        /// 检查是否拥有指定卡牌
        /// </summary>
        public bool HasCard(int cardId)
        {
            return handCards.Contains(cardId);
        }

        /// <summary>
        /// 获取手牌副本
        /// </summary>
        public List<int> GetHandCards()
        {
            return new List<int>(handCards);
        }

        /// <summary>
        /// 获取某种卡牌的数量
        /// </summary>
        public int GetCardCount(int cardId)
        {
            return handCards.Count(id => id == cardId);
        }
    }

    #endregion
}