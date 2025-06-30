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

        #region 自动配置方法

        /// <summary>
        /// 自动配置12张卡牌数据
        /// 在Inspector中点击右键菜单或通过代码调用
        /// </summary>
        [ContextMenu("自动配置12张卡牌")]
        public void AutoConfigureCards()
        {
            Debug.Log("[CardConfig] 开始自动配置卡牌数据...");

            allCards.Clear();

            // ID1: 牛奶盒 - 自发型回血卡
            allCards.Add(new CardData
            {
                cardId = 1,
                cardName = "牛奶盒",
                description = "为自身回复1点生命值",
                cardType = CardType.SelfTarget,
                effectType = EffectType.Heal,
                targetType = TargetType.Self,
                rarity = CardRarity.Common,
                drawWeight = 15f,
                effectValue = 1f,
                canUseWhenNotMyTurn = true,
                cardBackgroundColor = Color.white,
                showEffectValue = true
            });

            // ID2: 请假条 - 自发型跳过卡
            allCards.Add(new CardData
            {
                cardId = 2,
                cardName = "请假条",
                description = "下次轮到自己答题时跳过这次回合",
                cardType = CardType.SelfTarget,
                effectType = EffectType.SkipQuestion,
                targetType = TargetType.Self,
                rarity = CardRarity.Common,
                drawWeight = 12f,
                effectValue = 1f,
                canUseWhenNotMyTurn = true,
                cardBackgroundColor = Color.white,
                showEffectValue = false
            });

            // ID3: 两根粉笔 - 自发型加倍伤害卡
            allCards.Add(new CardData
            {
                cardId = 3,
                cardName = "两根粉笔",
                description = "下次有玩家答错的该次伤害翻倍",
                cardType = CardType.SelfTarget,
                effectType = EffectType.Damage,
                targetType = TargetType.AllOthers,
                rarity = CardRarity.Uncommon,
                drawWeight = 8f,
                effectValue = 2f,
                canUseWhenNotMyTurn = true,
                cardBackgroundColor = Color.yellow,
                showEffectValue = true
            });

            // ID4: 再想想 - 自发型加时卡
            allCards.Add(new CardData
            {
                cardId = 4,
                cardName = "再想想",
                description = "下次轮到自己答题时增加5秒答题时间",
                cardType = CardType.SelfTarget,
                effectType = EffectType.AddTime,
                targetType = TargetType.Self,
                rarity = CardRarity.Common,
                drawWeight = 10f,
                effectValue = 5f,
                canUseWhenNotMyTurn = true,
                cardBackgroundColor = Color.white,
                showEffectValue = true
            });

            // ID5: 成语接龙 - 自发型题目类型指定卡
            allCards.Add(new CardData
            {
                cardId = 5,
                cardName = "成语接龙",
                description = "下次轮到自己答题时必定为成语接龙题目",
                cardType = CardType.SelfTarget,
                effectType = EffectType.ChengYuChain,
                targetType = TargetType.Self,
                rarity = CardRarity.Rare,
                drawWeight = 5f,
                effectValue = 1f,
                canUseWhenNotMyTurn = true,
                cardBackgroundColor = Color.cyan,
                showEffectValue = false
            });

            // ID6: 判断题 - 自发型题目类型指定卡
            allCards.Add(new CardData
            {
                cardId = 6,
                cardName = "判断题",
                description = "下次轮到自己答题时必定是判断题",
                cardType = CardType.SelfTarget,
                effectType = EffectType.JudgeQuestion,
                targetType = TargetType.Self,
                rarity = CardRarity.Common,
                drawWeight = 12f,
                effectValue = 1f,
                canUseWhenNotMyTurn = true,
                cardBackgroundColor = Color.white,
                showEffectValue = false
            });

            // ID7: 文艺汇演 - 自发型群体回血卡
            allCards.Add(new CardData
            {
                cardId = 7,
                cardName = "文艺汇演",
                description = "所有玩家回复1点生命值",
                cardType = CardType.SelfTarget,
                effectType = EffectType.GroupHeal,
                targetType = TargetType.AllPlayers,
                rarity = CardRarity.Uncommon,
                drawWeight = 6f,
                effectValue = 1f,
                canUseWhenNotMyTurn = true,
                cardBackgroundColor = Color.green,
                showEffectValue = true
            });

            // ID8: 课外补习 - 自发型获得卡牌
            allCards.Add(new CardData
            {
                cardId = 8,
                cardName = "课外补习",
                description = "随机获得两张卡牌",
                cardType = CardType.SelfTarget,
                effectType = EffectType.GetCard,
                targetType = TargetType.Self,
                rarity = CardRarity.Rare,
                drawWeight = 4f,
                effectValue = 2f,
                canUseWhenNotMyTurn = true,
                cardBackgroundColor = Color.cyan,
                showEffectValue = true
            });

            // ID9: 丢纸团 - 指向型概率伤害卡
            allCards.Add(new CardData
            {
                cardId = 9,
                cardName = "丢纸团",
                description = "50%几率命中，对指定玩家造成1点伤害",
                cardType = CardType.PlayerTarget,
                effectType = EffectType.ProbabilityDamage,
                targetType = TargetType.SinglePlayer,
                rarity = CardRarity.Rare,
                drawWeight = 3f,
                effectValue = 0.5f, // 表示50%概率
                canUseWhenNotMyTurn = true,
                cardBackgroundColor = Color.red,
                showEffectValue = false
            });

            // ID10: 减时卡 - 指向型减时卡
            allCards.Add(new CardData
            {
                cardId = 10,
                cardName = "减时卡",
                description = "下次轮到被指定的玩家答题时会减少3秒答题时间",
                cardType = CardType.PlayerTarget,
                effectType = EffectType.ReduceTime,
                targetType = TargetType.SinglePlayer,
                rarity = CardRarity.Common,
                drawWeight = 10f,
                effectValue = 3f,
                canUseWhenNotMyTurn = true,
                cardBackgroundColor = Color.white,
                showEffectValue = true
            });

            // ID11: 借下橡皮 - 指向型偷取卡牌
            allCards.Add(new CardData
            {
                cardId = 11,
                cardName = "借下橡皮",
                description = "从指定玩家处偷取一张卡",
                cardType = CardType.PlayerTarget,
                effectType = EffectType.GetCard,
                targetType = TargetType.SinglePlayer,
                rarity = CardRarity.Uncommon,
                drawWeight = 6f,
                effectValue = -1f, // 负值表示偷取
                canUseWhenNotMyTurn = true,
                cardBackgroundColor = Color.yellow,
                showEffectValue = false
            });

            // ID12: 一盒粉笔 - 自发型获得特定卡牌
            allCards.Add(new CardData
            {
                cardId = 12,
                cardName = "一盒粉笔",
                description = "获得两张加倍卡",
                cardType = CardType.SelfTarget,
                effectType = EffectType.GetCard,
                targetType = TargetType.Self,
                rarity = CardRarity.Epic,
                drawWeight = 2f,
                effectValue = 3f, // 特殊值，表示获得加倍卡
                canUseWhenNotMyTurn = true,
                cardBackgroundColor = Color.magenta,
                showEffectValue = false
            });

            Debug.Log($"[CardConfig] 自动配置完成！共配置了{allCards.Count}张卡牌");

            // 标记为脏数据，确保保存
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// 重置为默认系统设置
        /// </summary>
        [ContextMenu("重置系统设置")]
        public void ResetSystemSettings()
        {
            systemSettings = new CardSystemSettings
            {
                maxHandSize = 5,
                startingCardCount = 3,
                cardsReceivedOnElimination = 2,
                allowCardUseWhenNotMyTurn = true,
                enableDebugLogs = true,
                showEffectValues = true
            };

            drawSettings = new CardDrawSettings
            {
                allowDuplicates = true,
                maxSameCardInHand = 3,
                guaranteeRareInStarting = false,
                bannedCardIds = new List<int>(),
                forcedStartingCards = new List<int>()
            };

            gameRules = new CardGameRules
            {
                oneCardPerRound = true,
                resetUsageAfterEachTurn = true,
                enableCardStealing = true,
                discardUsedCards = true
            };

            Debug.Log("[CardConfig] 系统设置已重置为默认值");

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// 快速调整卡牌权重（用于平衡性调整）
        /// </summary>
        [ContextMenu("调整卡牌权重")]
        public void AdjustCardWeights()
        {
            foreach (var card in allCards)
            {
                switch (card.rarity)
                {
                    case CardRarity.Common:
                        card.drawWeight = Random.Range(10f, 15f);
                        break;
                    case CardRarity.Uncommon:
                        card.drawWeight = Random.Range(6f, 8f);
                        break;
                    case CardRarity.Rare:
                        card.drawWeight = Random.Range(3f, 5f);
                        break;
                    case CardRarity.Epic:
                        card.drawWeight = Random.Range(1f, 2f);
                        break;
                    case CardRarity.Legendary:
                        card.drawWeight = Random.Range(0.5f, 1f);
                        break;
                }
            }

            Debug.Log("[CardConfig] 卡牌权重已根据稀有度重新调整");

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        #endregion

        #region Unity编辑器支持

#if UNITY_EDITOR
        /// <summary>
        /// Inspector面板验证时自动配置（可选）
        /// </summary>
        private void OnValidate()
        {
            if (autoConfigureCardsOnValidate && (allCards == null || allCards.Count == 0))
            {
                AutoConfigureCards();
            }
        }
#endif

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

        /// <summary>
        /// 获取卡牌统计信息
        /// </summary>
        public string GetCardStatistics()
        {
            var stats = $"=== 卡牌统计 ===\n";
            stats += $"总卡牌数: {allCards.Count}\n";
            stats += $"自发型: {GetSelfTargetCards().Count}张\n";
            stats += $"指向型: {GetPlayerTargetCards().Count}张\n";
            stats += $"普通: {GetCardsByRarity(CardRarity.Common).Count}张\n";
            stats += $"不常见: {GetCardsByRarity(CardRarity.Uncommon).Count}张\n";
            stats += $"稀有: {GetCardsByRarity(CardRarity.Rare).Count}张\n";
            stats += $"史诗: {GetCardsByRarity(CardRarity.Epic).Count}张\n";
            stats += $"传说: {GetCardsByRarity(CardRarity.Legendary).Count}张\n";

            return stats;
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
            //Debug.Log(GetCardStatistics());
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