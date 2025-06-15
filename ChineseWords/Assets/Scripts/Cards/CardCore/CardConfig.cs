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
        /// 根据稀有度获取卡牌列表（可选功能）
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

            // 验证系统设置
            if (!systemSettings.Validate())
            {
                Debug.LogError($"{CardConstants.LOG_TAG} 系统设置验证失败");
                return false;
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

        /// <summary>
        /// 创建默认卡牌数据（基于你提供的卡牌效果表）
        /// </summary>
        [ContextMenu("创建默认卡牌数据")]
        public void CreateDefaultCards()
        {
            allCards.Clear();

            // 根据你的卡牌效果表创建卡牌
            var defaultCards = new List<CardData>
            {
                // 自发型卡牌
                new CardData
                {
                    cardId = 1, cardName = "回血卡", description = "为自身回复1点生命值",
                    cardType = CardType.SelfTarget, effectType = EffectType.Heal, targetType = TargetType.Self,
                    effectValue = 1f, rarity = CardRarity.Common, drawWeight = 15f, canUseWhenNotMyTurn = true
                },
                new CardData
                {
                    cardId = 2, cardName = "跳过卡", description = "下次轮到自己答题时跳过这次回合",
                    cardType = CardType.SelfTarget, effectType = EffectType.SkipQuestion, targetType = TargetType.Self,
                    effectValue = 1f, rarity = CardRarity.Common, drawWeight = 12f, canUseWhenNotMyTurn = true
                },
                new CardData
                {
                    cardId = 3, cardName = "加倍卡", description = "下次有玩家错误的该次伤害翻倍",
                    cardType = CardType.SelfTarget, effectType = EffectType.Damage, targetType = TargetType.AllOthers,
                    effectValue = 2f, rarity = CardRarity.Uncommon, drawWeight = 8f, canUseWhenNotMyTurn = true
                },
                new CardData
                {
                    cardId = 4, cardName = "加时卡", description = "下次轮到自己答题时增加5秒答题时间",
                    cardType = CardType.SelfTarget, effectType = EffectType.AddTime, targetType = TargetType.Self,
                    effectValue = 5f, rarity = CardRarity.Common, drawWeight = 10f, canUseWhenNotMyTurn = true
                },
                new CardData
                {
                    cardId = 5, cardName = "成语接龙", description = "下次轮到自己答题时必定为成语接龙题目",
                    cardType = CardType.SelfTarget, effectType = EffectType.ChengYuChain, targetType = TargetType.Self,
                    effectValue = 1f, rarity = CardRarity.Rare, drawWeight = 5f, canUseWhenNotMyTurn = true
                },
                new CardData
                {
                    cardId = 6, cardName = "判断题", description = "下次轮到自己答题时必定为判断题",
                    cardType = CardType.SelfTarget, effectType = EffectType.JudgeQuestion, targetType = TargetType.Self,
                    effectValue = 1f, rarity = CardRarity.Common, drawWeight = 12f, canUseWhenNotMyTurn = true
                },
                new CardData
                {
                    cardId = 7, cardName = "小抄", description = "下次轮到自己答题时获得更多提示文字",
                    cardType = CardType.SelfTarget, effectType = EffectType.ExtraChance, targetType = TargetType.Self,
                    effectValue = 1f, rarity = CardRarity.Uncommon, drawWeight = 8f, canUseWhenNotMyTurn = true
                },
                new CardData
                {
                    cardId = 8, cardName = "随机获得两张卡牌", description = "随机获得两张卡牌",
                    cardType = CardType.SelfTarget, effectType = EffectType.GetCard, targetType = TargetType.Self,
                    effectValue = 2f, rarity = CardRarity.Rare, drawWeight = 4f, canUseWhenNotMyTurn = true
                },

                // 指向型卡牌
                new CardData
                {
                    cardId = 9, cardName = "指定回答卡", description = "下次轮到自己答题时会让指定的玩家回答且已不可回答",
                    cardType = CardType.PlayerTarget, effectType = EffectType.SpecifyQuestion, targetType = TargetType.SinglePlayer,
                    effectValue = 1f, rarity = CardRarity.Rare, drawWeight = 3f, canUseWhenNotMyTurn = true
                },
                new CardData
                {
                    cardId = 10, cardName = "减时卡", description = "下次轮到指定玩家答题时会减少3秒答题时间",
                    cardType = CardType.PlayerTarget, effectType = EffectType.ReduceTime, targetType = TargetType.SinglePlayer,
                    effectValue = 3f, rarity = CardRarity.Common, drawWeight = 10f, canUseWhenNotMyTurn = true
                },
                new CardData
                {
                    cardId = 11, cardName = "去除神秘", description = "从指定玩家手中移除一张卡牌",
                    cardType = CardType.PlayerTarget, effectType = EffectType.GetCard, targetType = TargetType.SinglePlayer,
                    effectValue = -1f, rarity = CardRarity.Uncommon, drawWeight = 6f, canUseWhenNotMyTurn = true
                },
                new CardData
                {
                    cardId = 12, cardName = "亲手卡手", description = "获得两张加倍卡",
                    cardType = CardType.SelfTarget, effectType = EffectType.GetCard, targetType = TargetType.Self,
                    effectValue = 2f, rarity = CardRarity.Epic, drawWeight = 2f, canUseWhenNotMyTurn = true
                }
            };

            allCards.AddRange(defaultCards);
            Debug.Log($"{CardConstants.LOG_TAG} 已创建{defaultCards.Count}张默认卡牌");
        }

        #endregion
    }

    #region 配置数据结构

    /// <summary>
    /// 卡牌系统设置（简化版）
    /// </summary>
    [System.Serializable]
    public class CardSystemSettings
    {
        [Header("基础设置")]
        public int maxHandSize = CardConstants.DEFAULT_MAX_HAND_SIZE;
        public int startingCardCount = CardConstants.DEFAULT_STARTING_CARDS;
        public int cardsReceivedOnElimination = 2;

        [Header("使用规则")]
        public bool allowCardUseWhenNotMyTurn = true;  // 是否允许在非自己回合时使用卡牌

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
    /// 卡牌游戏规则（简化版）
    /// </summary>
    [System.Serializable]
    public class CardGameRules
    {
        [Header("基本规则")]
        public bool oneCardPerRound = true;             // 每轮只能使用一张卡牌
        public bool resetUsageAfterEachTurn = true;     // 每次答题后重置使用机会

        [Header("特殊规则")]
        public bool enableCardStealing = true;          // 启用卡牌偷取（去除神秘卡）
        public bool discardUsedCards = true;            // 使用后的卡牌丢弃

        public bool Validate()
        {
            // 简化后基本不会有验证失败的情况
            return true;
        }
    }

    #endregion
}