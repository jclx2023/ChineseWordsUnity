using System.Collections.Generic;
using UnityEngine;
using Cards.Core;
using Cards.Integration;

namespace Cards.Effects
{
    /// <summary>
    /// 卡牌效果的具体实现
    /// 更新版 - 支持新的12种卡牌效果
    /// </summary>

    #region 生命值效果

    /// <summary>
    /// 治疗效果 - ID1: 牛奶盒
    /// </summary>
    public class HealEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // 通过CardGameBridge修改玩家生命值
            bool success = CardGameBridge.ModifyPlayerHealth(request.userId, (int)cardData.effectValue);

            if (success)
            {
                CardUtilities.LogDebug($"玩家{request.userId}恢复{cardData.effectValue}点生命值");
                return new CardEffectResult(true, $"恢复了{cardData.effectValue}点生命值");
            }
            else
            {
                CardUtilities.LogDebug($"玩家{request.userId}治疗失败 - 可能已经满血或死亡");
                return new CardEffectResult(false, "治疗失败");
            }
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            // 检查玩家是否还活着
            return CardGameBridge.IsPlayerAlive(request.userId);
        }

        public string GetDescription(CardData cardData)
        {
            return cardData.description;
        }
    }

    /// <summary>
    /// 群体治疗效果 - ID7: 文艺汇演
    /// </summary>
    public class GroupHealEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            int healAmount = (int)cardData.effectValue;
            var allPlayerIds = CardGameBridge.GetAllAlivePlayerIds();
            int successCount = 0;

            foreach (int playerId in allPlayerIds)
            {
                bool success = CardGameBridge.ModifyPlayerHealth(playerId, healAmount);
                if (success)
                {
                    successCount++;
                }
            }

            CardUtilities.LogDebug($"群体治疗让{successCount}/{allPlayerIds.Count}名玩家恢复{healAmount}点生命值");

            if (successCount > 0)
            {
                return new CardEffectResult(true, $"所有玩家恢复了{healAmount}点生命值");
            }
            else
            {
                return new CardEffectResult(false, "群体治疗失败");
            }
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            // 只要有玩家存在就可以使用
            var alivePlayerIds = CardGameBridge.GetAllAlivePlayerIds();
            return alivePlayerIds.Count > 0;
        }

        public string GetDescription(CardData cardData)
        {
            return cardData.description;
        }
    }

    /// <summary>
    /// 伤害倍数效果 - ID3: 两根粉笔
    /// </summary>
    public class DamageMultiplierEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // 设置伤害倍数器
            CardGameBridge.SetGlobalDamageMultiplier(cardData.effectValue);

            CardUtilities.LogDebug($"玩家{request.userId}使用加倍器，下次答错伤害x{cardData.effectValue}");
            return new CardEffectResult(true, $"下次答错伤害翻倍{cardData.effectValue}倍");
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            return true;
        }

        public string GetDescription(CardData cardData)
        {
            return cardData.description;
        }
    }

    /// <summary>
    /// 概率伤害效果 - ID9: 丢纸团
    /// </summary>
    public class ProbabilityDamageEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            float hitChance = cardData.effectValue; // effectValue表示命中概率(0.5 = 50%)
            float randomValue = Random.Range(0f, 1f);

            if (randomValue <= hitChance)
            {
                // 命中，造成10点伤害
                bool success = CardGameBridge.ModifyPlayerHealth(request.targetPlayerId, -10);

                CardUtilities.LogDebug($"玩家{request.userId}的丢纸团击中玩家{request.targetPlayerId}，造成10点伤害");

                if (success)
                {
                    return new CardEffectResult(true, "丢纸团命中，目标受到了10点伤害");
                }
                else
                {
                    return new CardEffectResult(false, "丢纸团击中了，但目标已经死亡");
                }
            }
            else
            {
                // 未命中
                CardUtilities.LogDebug($"玩家{request.userId}的丢纸团未击中玩家{request.targetPlayerId}");
                return new CardEffectResult(true, "丢纸团没有击中目标");
            }
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            // 需要目标玩家是否有效且存活，且不是自己
            return request.targetPlayerId > 0 &&
                   request.targetPlayerId != request.userId &&
                   CardGameBridge.IsPlayerAlive(request.targetPlayerId);
        }

        public string GetDescription(CardData cardData)
        {
            return cardData.description;
        }
    }

    #endregion

    #region 时间效果

    /// <summary>
    /// 增时效果 - ID4: 再想想
    /// </summary>
    public class AddTimeEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // 为使用者获得加时效果
            CardGameBridge.SetPlayerTimeBonus(request.userId, cardData.effectValue);

            CardUtilities.LogDebug($"玩家{request.userId}获得{cardData.effectValue}秒加时");
            return new CardEffectResult(true, $"下次答题将获得{cardData.effectValue}秒额外时间");
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            return true;
        }

        public string GetDescription(CardData cardData)
        {
            return cardData.description;
        }
    }

    /// <summary>
    /// 减时效果 - ID10: 减时卡
    /// </summary>
    public class ReduceTimeEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // 为目标玩家设置减时效果
            CardGameBridge.SetPlayerTimePenalty(request.targetPlayerId, cardData.effectValue);

            CardUtilities.LogDebug($"玩家{request.targetPlayerId}受到减{cardData.effectValue}秒答题时间");
            return new CardEffectResult(true, $"目标玩家下次答题减少{cardData.effectValue}秒时间");
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            // 需要目标玩家是否有效且存活，且不是自己
            return request.targetPlayerId > 0 &&
                   request.targetPlayerId != request.userId &&
                   CardGameBridge.IsPlayerAlive(request.targetPlayerId);
        }

        public string GetDescription(CardData cardData)
        {
            return cardData.description;
        }
    }

    #endregion

    #region 题目效果

    /// <summary>
    /// 跳题效果 - ID2: 请假条
    /// </summary>
    public class SkipQuestionEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // 为使用者设置跳题标志
            CardGameBridge.SetPlayerSkipFlag(request.userId, true);

            CardUtilities.LogDebug($"玩家{request.userId}下次答题将被跳过");
            return new CardEffectResult(true, "下轮遇到题目时会自动跳过");
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            return true;
        }

        public string GetDescription(CardData cardData)
        {
            return cardData.description;
        }
    }

    /// <summary>
    /// 成语接龙效果 - ID5: 成语接龙
    /// </summary>
    public class ChengYuChainEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // 为使用者设置下次题目类型
            CardGameBridge.SetPlayerNextQuestionType(request.userId, "IdiomChain");

            CardUtilities.LogDebug($"玩家{request.userId}下次答题将是成语接龙");
            return new CardEffectResult(true, "下次答题将是成语接龙题目");
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            return true;
        }

        public string GetDescription(CardData cardData)
        {
            return cardData.description;
        }
    }

    /// <summary>
    /// 判断题效果 - ID6: 判断题
    /// </summary>
    public class JudgeQuestionEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // 为使用者设置下次题目类型
            CardGameBridge.SetPlayerNextQuestionType(request.userId, "TrueFalse");

            CardUtilities.LogDebug($"玩家{request.userId}下次答题将是判断题");
            return new CardEffectResult(true, "下次答题将是判断题");
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            return true;
        }

        public string GetDescription(CardData cardData)
        {
            return cardData.description;
        }
    }

    #endregion

    #region 操作卡牌效果

    /// <summary>
    /// 统一的操作卡牌效果 - 涵盖各类 EffectType.GetCard 的行为
    /// ID8: 课外补习 (抽取随机卡牌)
    /// ID11: "借下橡皮" (偷指定玩家的一张卡牌)
    /// ID12: 一盒粉笔 (获得两张加倍器)
    /// </summary>
    public class CardManipulationEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            int effectValue = (int)cardData.effectValue;

            // 根据卡牌ID和effectValue判断具体行为
            switch (cardData.cardId)
            {
                case 8: // 课外补习：抽取随机卡牌
                    return ExecuteDrawRandomCards(request, cardData);

                case 11: // "借下橡皮"：偷取卡牌 (effectValue = -1)
                    return ExecuteStealCard(request, cardData);

                case 12: // 一盒粉笔：获得两张加倍器 (effectValue = 3，表示加倍器的ID)
                    return ExecuteDrawSpecificCards(request, cardData);

                default:
                    // 兜底逻辑：根据effectValue判断
                    if (effectValue < 0)
                    {
                        return ExecuteStealCard(request, cardData);
                    }
                    else
                    {
                        return ExecuteDrawRandomCards(request, cardData);
                    }
            }
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            // 根据卡牌ID判断使用条件
            switch (cardData.cardId)
            {
                case 8: // 课外补习：自己发动，任何情况
                case 12: // 一盒粉笔：自己发动，任何情况
                    return true;

                case 11: // "借下橡皮"：指定发动，需要有效目标
                    return request.targetPlayerId > 0 &&
                           request.targetPlayerId != request.userId &&
                           CardGameBridge.IsPlayerAlive(request.targetPlayerId);

                default:
                    // 兜底逻辑
                    int effectValue = (int)cardData.effectValue;
                    if (effectValue < 0)
                    {
                        // 偷取类：需要有效目标
                        return request.targetPlayerId > 0 &&
                               request.targetPlayerId != request.userId &&
                               CardGameBridge.IsPlayerAlive(request.targetPlayerId);
                    }
                    else
                    {
                        // 抽取类：任何情况
                        return true;
                    }
            }
        }

        public string GetDescription(CardData cardData)
        {
            return cardData.description;
        }

        #region 具体行为实现

        /// <summary>
        /// 执行获取随机卡牌 - ID8: 课外补习
        /// </summary>
        private CardEffectResult ExecuteDrawRandomCards(CardUseRequest request, CardData cardData)
        {
            int cardCount = (int)cardData.effectValue;
            bool success = CardGameBridge.GiveCardToPlayer(request.userId, 0, cardCount); // cardId=0表示随机

            CardUtilities.LogDebug($"玩家{request.userId}抽取{cardCount}张随机卡牌，成功:{success}");

            if (success)
            {
                return new CardEffectResult(true, $"获得了{cardCount}张卡牌");
            }
            else
            {
                return new CardEffectResult(false, "抽取卡牌失败，无法获得更多卡牌");
            }
        }

        /// <summary>
        /// 执行偷取卡牌 - ID11: "借下橡皮"
        /// </summary>
        private CardEffectResult ExecuteStealCard(CardUseRequest request, CardData cardData)
        {
            var stolenCard = CardGameBridge.StealRandomCardFromPlayer(request.targetPlayerId, request.userId);

            if (stolenCard.HasValue)
            {
                CardUtilities.LogDebug($"玩家{request.userId}从玩家{request.targetPlayerId}偷取了卡牌{stolenCard.Value}");

                var stolenCardData = CardGameBridge.GetCardDataById(stolenCard.Value);
                string cardName = stolenCardData?.cardName ?? "未知卡牌";

                return new CardEffectResult(true, $"成功偷取了目标玩家的一张卡牌：{cardName}");
            }
            else
            {
                CardUtilities.LogDebug($"玩家{request.targetPlayerId}没有卡牌可偷取");
                return new CardEffectResult(false, "目标玩家没有卡牌可偷取");
            }
        }

        /// <summary>
        /// 执行获取特定卡牌 - ID12: 一盒粉笔
        /// </summary>
        private CardEffectResult ExecuteDrawSpecificCards(CardUseRequest request, CardData cardData)
        {
            // 一盒粉笔：获得2张加倍器(ID=3)
            int targetCardId = 3; // 加倍器的ID
            int cardCount = 2;    // 获得2张

            bool success = CardGameBridge.GiveCardToPlayer(request.userId, targetCardId, cardCount);

            CardUtilities.LogDebug($"玩家{request.userId}获得{cardCount}张加倍器，成功:{success}");

            if (success)
            {
                return new CardEffectResult(true, "获得了2张加倍器");
            }
            else
            {
                return new CardEffectResult(false, "获得卡牌失败，无法获得加倍器");
            }
        }

        #endregion
    }

    #endregion

    #region 效果注册器

    /// <summary>
    /// 效果注册器 - 负责注册所有效果到系统
    /// 更新版 - 支持新的效果类型
    /// </summary>
    public static class CardEffectRegistrar
    {
        /// <summary>
        /// 注册所有默认效果
        /// </summary>
        public static void RegisterAllEffects(CardEffectSystem system)
        {
            if (system == null)
            {
                CardUtilities.LogError("CardEffectSystem为空，无法注册效果");
                return;
            }

            // 生命值类
            system.RegisterEffect(EffectType.Heal, new HealEffect());                    // ID1: 牛奶盒
            system.RegisterEffect(EffectType.GroupHeal, new GroupHealEffect());          // ID7: 文艺汇演
            system.RegisterEffect(EffectType.Damage, new DamageMultiplierEffect());     // ID3: 两根粉笔
            system.RegisterEffect(EffectType.ProbabilityDamage, new ProbabilityDamageEffect()); // ID9: 丢纸团

            // 时间类
            system.RegisterEffect(EffectType.AddTime, new AddTimeEffect());             // ID4: 再想想
            system.RegisterEffect(EffectType.ReduceTime, new ReduceTimeEffect());       // ID10: 减时卡

            // 题目类
            system.RegisterEffect(EffectType.SkipQuestion, new SkipQuestionEffect());   // ID2: 请假条
            system.RegisterEffect(EffectType.ChengYuChain, new ChengYuChainEffect());   // ID5: 成语接龙
            system.RegisterEffect(EffectType.JudgeQuestion, new JudgeQuestionEffect()); // ID6: 判断题

            // 操作卡牌类
            system.RegisterEffect(EffectType.GetCard, new CardManipulationEffect());    // ID8: 课外补习, ID11: "借下橡皮", ID12: 一盒粉笔

            CardUtilities.LogDebug("所有卡牌效果注册完成");
        }
    }

    #endregion
}