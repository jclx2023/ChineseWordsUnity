using System.Collections.Generic;
using UnityEngine;
using Cards.Core;
using Cards.Integration;

namespace Cards.Effects
{
    /// <summary>
    /// 所有卡牌效果的具体实现
    /// 已更新 - 集成CardGameBridge，替换所有TODO
    /// </summary>

    #region 生命值类效果

    /// <summary>
    /// 回血卡效果
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
    /// 加倍卡效果 - 下次错误伤害翻倍
    /// </summary>
    public class DamageMultiplierEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // 设置伤害倍数标记
            SetDamageMultiplier(cardData.effectValue);

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

        private void SetDamageMultiplier(float multiplier)
        {
            // 通过CardGameBridge设置全局伤害倍数
            CardGameBridge.SetGlobalDamageMultiplier(multiplier);
        }
    }

    #endregion

    #region 时间类效果

    /// <summary>
    /// 加时卡效果
    /// </summary>
    public class AddTimeEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // 为使用者缓存加时效果
            SetTimeBonus(request.userId, cardData.effectValue);

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

        private void SetTimeBonus(int playerId, float bonusTime)
        {
            // 通过CardGameBridge设置时间加成
            CardGameBridge.SetPlayerTimeBonus(playerId, bonusTime);
        }
    }

    /// <summary>
    /// 减时卡效果
    /// </summary>
    public class ReduceTimeEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // 为目标玩家设置减时效果
            SetTimePenalty(request.targetPlayerId, cardData.effectValue);

            CardUtilities.LogDebug($"玩家{request.targetPlayerId}被减少{cardData.effectValue}秒答题时间");
            return new CardEffectResult(true, $"目标玩家下次答题将减少{cardData.effectValue}秒时间");
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            // 检查目标玩家是否有效且存活
            return request.targetPlayerId > 0 &&
                   request.targetPlayerId != request.userId &&
                   CardGameBridge.IsPlayerAlive(request.targetPlayerId);
        }

        public string GetDescription(CardData cardData)
        {
            return cardData.description;
        }

        private void SetTimePenalty(int playerId, float penaltyTime)
        {
            // 通过CardGameBridge设置时间减成
            CardGameBridge.SetPlayerTimePenalty(playerId, penaltyTime);
        }
    }

    #endregion

    #region 题目类效果

    /// <summary>
    /// 跳过卡效果
    /// </summary>
    public class SkipQuestionEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // 为使用者设置跳过标记
            SetSkipFlag(request.userId);

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

        private void SetSkipFlag(int playerId)
        {
            // 通过CardGameBridge设置跳过标记
            CardGameBridge.SetPlayerSkipFlag(playerId, true);
        }
    }

    /// <summary>
    /// 成语接龙效果
    /// </summary>
    public class ChengYuChainEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // 为使用者设置下次题目类型
            SetNextQuestionType(request.userId, "IdiomChain");

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

        private void SetNextQuestionType(int playerId, string questionType)
        {
            // 通过CardGameBridge设置下次题目类型
            CardGameBridge.SetPlayerNextQuestionType(playerId, questionType);
        }
    }

    /// <summary>
    /// 判断题效果
    /// </summary>
    public class JudgeQuestionEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // 为使用者设置下次题目类型
            SetNextQuestionType(request.userId, "TrueFalse");

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

        private void SetNextQuestionType(int playerId, string questionType)
        {
            // 通过CardGameBridge设置下次题目类型
            CardGameBridge.SetPlayerNextQuestionType(playerId, questionType);
        }
    }

    /// <summary>
    /// 指定回答卡效果
    /// </summary>
    public class SpecifyPlayerAnswerEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // 设置下次答题时由指定玩家代替回答
            SetAnswerDelegate(request.userId, request.targetPlayerId);

            CardUtilities.LogDebug($"玩家{request.userId}下次答题将由玩家{request.targetPlayerId}代替");
            return new CardEffectResult(true, "下次答题将由指定玩家代替回答");
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            // 检查目标玩家是否有效且不是自己，并且存活
            return request.targetPlayerId > 0 &&
                   request.targetPlayerId != request.userId &&
                   CardGameBridge.IsPlayerAlive(request.targetPlayerId);
        }

        public string GetDescription(CardData cardData)
        {
            return cardData.description;
        }

        private void SetAnswerDelegate(int originalPlayerId, int delegatePlayerId)
        {
            // 通过CardGameBridge设置答题代理
            CardGameBridge.SetAnswerDelegate(originalPlayerId, delegatePlayerId);
        }
    }

    #endregion

    #region 特殊效果

    /// <summary>
    /// 小抄效果 - 额外提示
    /// </summary>
    public class ExtraHintEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // 为使用者设置额外提示标记
            SetExtraHintFlag(request.userId);

            CardUtilities.LogDebug($"玩家{request.userId}下次答题将获得额外提示");
            return new CardEffectResult(true, "下次答题将获得更多提示文字");
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            return true;
        }

        public string GetDescription(CardData cardData)
        {
            return cardData.description;
        }

        private void SetExtraHintFlag(int playerId)
        {
            // 通过CardGameBridge设置额外提示标记
            CardGameBridge.SetPlayerExtraHint(playerId, true);
        }
    }

    /// <summary>
    /// 统一的卡牌操作效果 - 处理所有 EffectType.GetCard 的情况
    /// 通过 effectValue 和 targetType 区分不同行为
    /// </summary>
    public class CardManipulationEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            int effectValue = (int)cardData.effectValue;

            // 根据 effectValue 和 targetType 判断具体行为
            if (effectValue < 0)
            {
                // effectValue < 0: 偷取卡牌 ("借下橡皮")
                return ExecuteStealCard(request, cardData);
            }
            else if (cardData.targetType == TargetType.Self && effectValue > 0)
            {
                // targetType = Self + effectValue > 0: 自己获得随机卡牌
                return ExecuteDrawRandomCards(request, cardData);
            }
            else if (cardData.targetType == TargetType.Self && effectValue == 0)
            {
                // 特殊情况: "烫手山芋" - 获得特定卡牌
                return ExecuteDrawSpecificCards(request, cardData);
            }
            else
            {
                // 默认情况: 普通获得卡牌
                return ExecuteDrawRandomCards(request, cardData);
            }
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            int effectValue = (int)cardData.effectValue;

            if (effectValue < 0)
            {
                // 偷取卡牌: 需要有效的目标玩家
                return request.targetPlayerId > 0 &&
                       request.targetPlayerId != request.userId &&
                       CardGameBridge.IsPlayerAlive(request.targetPlayerId);
            }
            else
            {
                // 获得卡牌: 总是可以使用（手牌满了也会在桥接器中处理）
                return true;
            }
        }

        public string GetDescription(CardData cardData)
        {
            return cardData.description;
        }

        #region 具体行为实现

        /// <summary>
        /// 执行偷取卡牌
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
        /// 执行获得随机卡牌
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
        /// 执行获得特定卡牌（烫手山芋）
        /// </summary>
        private CardEffectResult ExecuteDrawSpecificCards(CardUseRequest request, CardData cardData)
        {
            // "烫手山芋"：获得2张加倍卡(ID=3)
            bool success = CardGameBridge.GiveCardToPlayer(request.userId, 3, 2);

            CardUtilities.LogDebug($"玩家{request.userId}获得2张加倍卡，成功:{success}");

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
            system.RegisterEffect(EffectType.Heal, new HealEffect());
            system.RegisterEffect(EffectType.Damage, new DamageMultiplierEffect());

            // 时间类
            system.RegisterEffect(EffectType.AddTime, new AddTimeEffect());
            system.RegisterEffect(EffectType.ReduceTime, new ReduceTimeEffect());

            // 题目类
            system.RegisterEffect(EffectType.SkipQuestion, new SkipQuestionEffect());
            system.RegisterEffect(EffectType.ChengYuChain, new ChengYuChainEffect());
            system.RegisterEffect(EffectType.JudgeQuestion, new JudgeQuestionEffect());
            system.RegisterEffect(EffectType.SpecifyQuestion, new SpecifyPlayerAnswerEffect());

            // 特殊类
            system.RegisterEffect(EffectType.ExtraChance, new ExtraHintEffect());
            system.RegisterEffect(EffectType.GetCard, new CardManipulationEffect());

            CardUtilities.LogDebug("所有卡牌效果注册完成");
            CardUtilities.LogDebug("CardManipulationEffect 已注册，统一处理：随机获得卡牌、偷取卡牌、获得特定卡牌");
        }
    }

    #endregion
}