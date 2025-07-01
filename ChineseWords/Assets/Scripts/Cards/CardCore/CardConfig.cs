using System.Collections.Generic;
using UnityEngine;
using Cards.Core;

namespace Cards.Core
{
    /// <summary>
    /// 卡牌配置ScriptableObject
    /// 用于在Inspector中配置卡牌数据和系统设置
    /// </summary>
    [CreateAssetMenu(fileName = "New Card Config", menuName = "Card System/Card Config")]
    public class CardConfig : ScriptableObject
    {
        [Header("卡牌数据")]
        [SerializeField] private List<CardData> allCards = new List<CardData>();

        [Header("系统设置")]
        [SerializeField] private CardSystemSettings systemSettings = new CardSystemSettings();

        [Header("抽卡设置")]
        [SerializeField] private CardDrawSettings drawSettings = new CardDrawSettings();

        [Header("游戏规则")]
        [SerializeField] private CardGameRules gameRules = new CardGameRules();

        [Header("自动配置")]
        [SerializeField] private bool autoConfigureCardsOnValidate = false;

        #region 属性访问器

        /// <summary>
        /// 所有卡牌数据
        /// </summary>
        public List<CardData> AllCards => allCards;

        /// <summary>
        /// 系统设置
        /// </summary>
        public CardSystemSettings SystemSettings => systemSettings;

        /// <summary>
        /// 抽卡设置
        /// </summary>
        public CardDrawSettings DrawSettings => drawSettings;

        /// <summary>
        /// 游戏规则
        /// </summary>
        public CardGameRules GameRules => gameRules;

        #endregion

        #region 卡牌查询方法

        /// <summary>
        /// 根据ID获取卡牌数据
        /// </summary>
        public CardData GetCardById(int cardId)
        {
            return allCards.Find(card => card.cardId == cardId);
        }

        /// <summary>
        /// 根据效果类型获取卡牌列表
        /// </summary>
        public List<CardData> GetCardsByEffectType(EffectType effectType)
        {
            return allCards.FindAll(card => card.effectType == effectType);
        }

        /// <summary>
        /// 根据稀有度获取卡牌列表
        /// </summary>
        public List<CardData> GetCardsByRarity(CardRarity rarity)
        {
            return allCards.FindAll(card => card.rarity == rarity);
        }

        /// <summary>
        /// 根据卡牌类型获取卡牌列表
        /// </summary>
        public List<CardData> GetCardsByType(CardType cardType)
        {
            return allCards.FindAll(card => card.cardType == cardType);
        }

        /// <summary>
        /// 获取所有自发型卡牌
        /// </summary>
        public List<CardData> GetSelfTargetCards()
        {
            return GetCardsByType(CardType.SelfTarget);
        }

        /// <summary>
        /// 获取所有指向型卡牌
        /// </summary>
        public List<CardData> GetPlayerTargetCards()
        {
            return GetCardsByType(CardType.PlayerTarget);
        }

        #endregion

        #region 验证和初始化

        /// <summary>
        /// 验证配置数据的有效性
        /// </summary>
        public bool ValidateConfig()
        {
            if (allCards == null || allCards.Count == 0)
            {
                Debug.LogWarning($"{CardConstants.LOG_TAG} 卡牌配置为空");
                return false;
            }

            // 检查卡牌ID唯一性
            HashSet<int> cardIds = new HashSet<int>();
            foreach (var card in allCards)
            {
                if (card.cardId <= 0)
                {
                    Debug.LogError($"{CardConstants.LOG_TAG} 卡牌 {card.cardName} 的ID无效: {card.cardId}");
                    return false;
                }

                if (cardIds.Contains(card.cardId))
                {
                    Debug.LogError($"{CardConstants.LOG_TAG} 重复的卡牌ID: {card.cardId}");
                    return false;
                }

                cardIds.Add(card.cardId);
            }

            // 验证抽卡设置
            if (!drawSettings.Validate())
            {
                Debug.LogError($"{CardConstants.LOG_TAG} 抽卡设置验证失败");
                return false;
            }

            Debug.Log($"{CardConstants.LOG_TAG} 卡牌配置验证成功，共{allCards.Count}张卡牌");
            return true;
        }

        #endregion
    }

    #region 配置数据结构（保持不变）

    /// <summary>
    /// 卡牌系统设置
    /// </summary>
    [System.Serializable]
    public class CardSystemSettings
    {
        [Header("基础设置")]
        public int maxHandSize = CardConstants.DEFAULT_MAX_HAND_SIZE;
        public int startingCardCount = CardConstants.DEFAULT_STARTING_CARDS;
        public int cardsReceivedOnElimination = 2;

        [Header("使用规则")]
        public bool allowCardUseWhenNotMyTurn = true;

        [Header("调试设置")]
        public bool enableDebugLogs = true;
        public bool showEffectValues = true;

        public bool Validate()
        {
            if (maxHandSize <= 0 || maxHandSize > 20)
            {
                Debug.LogError("最大手牌数量必须在1-20之间");
                return false;
            }

            if (startingCardCount < 0 || startingCardCount > maxHandSize)
            {
                Debug.LogError("起始卡牌数量不能超过最大手牌数量");
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// 抽卡设置
    /// </summary>
    [System.Serializable]
    public class CardDrawSettings
    {
        [Header("抽卡规则")]
        public bool allowDuplicates = true;
        public int maxSameCardInHand = 3;
        public bool guaranteeRareInStarting = false;

        [Header("特殊抽卡")]
        public List<int> bannedCardIds = new List<int>();
        public List<int> forcedStartingCards = new List<int>();

        public bool Validate()
        {
            if (maxSameCardInHand <= 0)
            {
                Debug.LogError("同种卡牌最大数量必须大于0");
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// 卡牌游戏规则
    /// </summary>
    [System.Serializable]
    public class CardGameRules
    {
        [Header("基本规则")]
        public bool oneCardPerRound = true;
        public bool resetUsageAfterEachTurn = true;

        [Header("特殊规则")]
        public bool enableCardStealing = true;
        public bool discardUsedCards = true;

        public bool Validate()
        {
            return true;
        }
    }

    #endregion
}