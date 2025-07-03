using System.Collections.Generic;
using UnityEngine;
using Cards.Core;
using Cards.Integration;

namespace Cards.Effects
{
    /// <summary>
    /// 所有卡牌效果的具体实现
    /// 更新版 - 支持新的12张卡牌效果
    /// </summary>

    #region 生命值类效果

    /// <summary>
    /// 单体回血卡效果 - ID1: 牛奶盒
    /// </summary>
    public class HealEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // 通过CardGameBridge修改玩家生命值
            bool success = CardGameBridge.ModifyPlayerHealth(request.userId, (int)cardData.effectValue);

            if (success)
            {
                CardUtilities.LogDebug($"玩家{request.userId}回复{cardData.effectValue}点生命值");
                return new CardEffectResult(true, $"回复了{cardData.effectValue}点生命值");
            }
            else
            {
                CardUtilities.LogDebug($"玩家{request.userId}回血失败 - 可能已满血或已死亡");
                return new CardEffectResult(false, "回血失败");
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
    /// 群体回血卡效果 - ID7: 文艺汇演
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

            CardUtilities.LogDebug($"群体回血：{successCount}/{allPlayerIds.Count}名玩家回复{healAmount}点生命值");

            if (successCount > 0)
            {
                return new CardEffectResult(true, $"所有玩家回复了{healAmount}点生命值");
            }
            else
            {
                return new CardEffectResult(false, "群体回血失败");
            }
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            // 只要有玩家存活就可以使用
            var alivePlayerIds = CardGameBridge.GetAllAlivePlayerIds();
            return alivePlayerIds.Count > 0;
        }

        public string GetDescription(CardData cardData)
        {
            return cardData.description;
        }
    }

    /// <summary>
    /// 加倍伤害卡效果 - ID3: 两根粉笔
    /// </summary>
    public class DamageMultiplierEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // 设置伤害倍数标记
            CardGameBridge.SetGlobalDamageMultiplier(cardData.effectValue);

            CardUtilities.LogDebug($"玩家{request.userId}使用加倍卡，下次错误伤害x{cardData.effectValue}");
            return new CardEffectResult(true, $"下次错误伤害将翻{cardData.effectValue}倍");
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
    /// 概率伤害卡效果 - ID9: 丢纸团
    /// </summary>
    public class ProbabilityDamageEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            float hitChance = cardData.effectValue; // effectValue表示命中概率(0.5 = 50%)
            float randomValue = Random.Range(0f, 1f);

            if (randomValue <= hitChance)
            {
                // 命中！造成1点伤害
                bool success = CardGameBridge.ModifyPlayerHealth(request.targetPlayerId, -10);

                CardUtilities.LogDebug($"玩家{request.userId}的丢纸团命中玩家{request.targetPlayerId}！造成10点伤害");

                if (success)
                {
                    return new CardEffectResult(true, "纸团命中！目标玩家受到10点伤害");
                }
                else
                {
                    return new CardEffectResult(false, "纸团命中了，但目标玩家已死亡");
                }
            }
            else
            {
                // 未命中
                CardUtilities.LogDebug($"玩家{request.userId}的丢纸团未命中玩家{request.targetPlayerId}");
                return new CardEffectResult(true, "纸团没有命中目标");
            }
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            // 检查目标玩家是否有效且存活，且不是自己
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

    #region 时间类效果

    /// <summary>
    /// 加时卡效果 - ID4: 再想想
    /// </summary>
    public class AddTimeEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // 为使用者缓存加时效果
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
    /// 减时卡效果 - ID10: 减时卡
    /// </summary>
    public class ReduceTimeEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // 为目标玩家设置减时效果
            CardGameBridge.SetPlayerTimePenalty(request.targetPlayerId, cardData.effectValue);

            CardUtilities.LogDebug($"玩家{request.targetPlayerId}被减少{cardData.effectValue}秒答题时间");
            return new CardEffectResult(true, $"目标玩家下次答题将减少{cardData.effectValue}秒时间");
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            // 检查目标玩家是否有效且存活，且不是自己
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

    #region 题目类效果

    /// <summary>
    /// 跳过卡效果 - ID2: 请假条
    /// </summary>
    public class SkipQuestionEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // 为使用者设置跳过标记
            CardGameBridge.SetPlayerSkipFlag(request.userId, true);

            CardUtilities.LogDebug($"玩家{request.userId}下次答题将被跳过");
            return new CardEffectResult(true, "下次轮到你时将自动跳过");
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

    #region 卡牌操作效果

    /// <summary>
    /// 统一的卡牌操作效果 - 处理所有 EffectType.GetCard 的情况
    /// ID8: 课外补习 (随机获得两张卡牌)
    /// ID11: 借下橡皮 (从指定玩家偷取一张卡)
    /// ID12: 一盒粉笔 (获得两张加倍卡)
    /// </summary>
    public class CardManipulationEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            int effectValue = (int)cardData.effectValue;

            // 根据卡牌ID和effectValue判断具体行为
            switch (cardData.cardId)
            {
                case 8: // 课外补习：随机获得两张卡牌
                    return ExecuteDrawRandomCards(request, cardData);

                case 11: // 借下橡皮：偷取卡牌 (effectValue = -1)
                    return ExecuteStealCard(request, cardData);

                case 12: // 一盒粉笔：获得两张加倍卡 (effectValue = 3，表示加倍卡的ID)
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
                case 8: // 课外补习：自发型，总是可用
                case 12: // 一盒粉笔：自发型，总是可用
                    return true;

                case 11: // 借下橡皮：指向型，需要有效目标
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
                        // 获得类：总是可用
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
        /// 执行获得随机卡牌 - ID8: 课外补习
        /// </summary>
        private CardEffectResult ExecuteDrawRandomCards(CardUseRequest request, CardData cardData)
        {
            int cardCount = (int)cardData.effectValue;
            bool success = CardGameBridge.GiveCardToPlayer(request.userId, 0, cardCount); // cardId=0表示随机

            CardUtilities.LogDebug($"玩家{request.userId}获得{cardCount}张随机卡牌，成功:{success}");

            if (success)
            {
                return new CardEffectResult(true, $"获得了{cardCount}张卡牌");
            }
            else
            {
                return new CardEffectResult(false, "手牌已满，无法获得更多卡牌");
            }
        }

        /// <summary>
        /// 执行偷取卡牌 - ID11: 借下橡皮
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
        /// 执行获得特定卡牌 - ID12: 一盒粉笔
        /// </summary>
        private CardEffectResult ExecuteDrawSpecificCards(CardUseRequest request, CardData cardData)
        {
            // 一盒粉笔：获得2张加倍卡(ID=3)
            int targetCardId = 3; // 加倍卡的ID
            int cardCount = 2;    // 获得2张

            bool success = CardGameBridge.GiveCardToPlayer(request.userId, targetCardId, cardCount);

            CardUtilities.LogDebug($"玩家{request.userId}获得{cardCount}张加倍卡，成功:{success}");

            if (success)
            {
                return new CardEffectResult(true, "获得了2张加倍卡");
            }
            else
            {
                return new CardEffectResult(false, "手牌已满，无法获得加倍卡");
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

            // 卡牌操作类
            system.RegisterEffect(EffectType.GetCard, new CardManipulationEffect());    // ID8: 课外补习, ID11: 借下橡皮, ID12: 一盒粉笔

            CardUtilities.LogDebug("所有卡牌效果注册完成");
        }
    }

    #endregion

    #region 工具类扩展

    /// <summary>
    /// 卡牌工具类扩展
    /// </summary>
    public static class CardEffectUtilities
    {
        /// <summary>
        /// 调试日志输出
        /// </summary>
        public static void LogDebug(string message)
        {
            Debug.Log($"[CardEffects] {message}");
        }

        /// <summary>
        /// 错误日志输出
        /// </summary>
        public static void LogError(string message)
        {
            Debug.LogError($"[CardEffects] {message}");
        }

        /// <summary>
        /// 计算概率是否命中
        /// </summary>
        public static bool RollProbability(float chance)
        {
            return Random.Range(0f, 1f) <= chance;
        }

        /// <summary>
        /// 获取效果描述（带数值）
        /// </summary>
        public static string GetEffectDescription(CardData cardData)
        {
            if (cardData.showEffectValue && cardData.effectValue > 0)
            {
                switch (cardData.effectType)
                {
                    case EffectType.Heal:
                    case EffectType.GroupHeal:
                        return $"{cardData.description} ({cardData.effectValue}点生命值)";

                    case EffectType.AddTime:
                        return $"{cardData.description} ({cardData.effectValue}秒)";

                    case EffectType.ReduceTime:
                        return $"{cardData.description} ({cardData.effectValue}秒)";

                    case EffectType.Damage:
                        return $"{cardData.description} (×{cardData.effectValue})";

                    case EffectType.ProbabilityDamage:
                        return $"{cardData.description} ({cardData.effectValue * 100}%命中率)";

                    case EffectType.GetCard:
                        if (cardData.effectValue > 0)
                            return $"{cardData.description} ({cardData.effectValue}张)";
                        break;
                }
            }

            return cardData.description;
        }
    }

    #endregion
}