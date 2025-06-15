using System.Collections.Generic;
using UnityEngine;
using Cards.Core;

namespace Cards.Effects
{
    /// <summary>
    /// 所有卡牌效果的具体实现
    /// 简化版，只实现基础功能
    /// </summary>

    #region 生命值类效果

    /// <summary>
    /// 回血卡效果
    /// </summary>
    public class HealEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // TODO: 与PlayerHealthManager集成
            // PlayerHealthManager.Instance.AddHealth(request.userId, (int)cardData.effectValue);

            CardUtilities.LogDebug($"玩家{request.userId}回复{cardData.effectValue}点生命值");
            return new CardEffectResult(true, $"回复了{cardData.effectValue}点生命值");
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            // TODO: 检查玩家是否还活着，是否已满血
            return true;
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
            // TODO: 与游戏系统集成，设置伤害倍数
            // GameManager.Instance.SetNextDamageMultiplier(multiplier);
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
            // TODO: 与TimerManager集成
            // TimerManager.Instance.SetTimeBonusForPlayer(playerId, bonusTime);
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
            // 检查目标玩家是否有效
            return request.targetPlayerId > 0 && request.targetPlayerId != request.userId;
        }

        public string GetDescription(CardData cardData)
        {
            return cardData.description;
        }

        private void SetTimePenalty(int playerId, float penaltyTime)
        {
            // TODO: 与TimerManager集成
            // TimerManager.Instance.SetTimePenaltyForPlayer(playerId, penaltyTime);
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
            // TODO: 与回合管理系统集成
            // TurnManager.Instance.SetSkipFlagForPlayer(playerId);
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
            // TODO: 与题目管理器集成
            // QuestionManager.Instance.SetNextQuestionTypeForPlayer(playerId, questionType);
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
            // TODO: 与题目管理器集成
            // QuestionManager.Instance.SetNextQuestionTypeForPlayer(playerId, questionType);
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
            // 检查目标玩家是否有效且不是自己
            return request.targetPlayerId > 0 && request.targetPlayerId != request.userId;
        }

        public string GetDescription(CardData cardData)
        {
            return cardData.description;
        }

        private void SetAnswerDelegate(int originalPlayerId, int delegatePlayerId)
        {
            // TODO: 与回合管理系统集成
            // TurnManager.Instance.SetAnswerDelegate(originalPlayerId, delegatePlayerId);
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
            // TODO: 与UI系统集成
            // UIManager.Instance.SetExtraHintForPlayer(playerId);
        }
    }

    /// <summary>
    /// 获得卡牌效果 - 包括随机获得和"烫手山芋"
    /// </summary>
    public class DrawCardEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            int cardCount = (int)cardData.effectValue;

            if (cardCount > 0)
            {
                // 普通获得卡牌
                DrawCardsForPlayer(request.userId, cardCount);
                CardUtilities.LogDebug($"玩家{request.userId}获得{cardCount}张卡牌");
                return new CardEffectResult(true, $"获得了{cardCount}张卡牌");
            }
            else
            {
                // 这是"烫手山芋"，获得特定卡牌
                DrawSpecificCard(request.userId, 3, 2); // 获得2张加倍卡(ID=3)
                CardUtilities.LogDebug($"玩家{request.userId}获得2张加倍卡");
                return new CardEffectResult(true, "获得了2张加倍卡");
            }
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            // TODO: 检查手牌是否已满
            return true;
        }

        public string GetDescription(CardData cardData)
        {
            return cardData.description;
        }

        private void DrawCardsForPlayer(int playerId, int count)
        {
            // TODO: 与卡牌管理系统集成
            // CardSystemManager.Instance.DrawCardsForPlayer(playerId, count);
        }

        private void DrawSpecificCard(int playerId, int cardId, int count)
        {
            // TODO: 与卡牌管理系统集成
            // CardSystemManager.Instance.AddSpecificCardToPlayer(playerId, cardId, count);
        }
    }

    /// <summary>
    /// 偷取卡牌效果 - "借下橡皮"
    /// 从目标玩家手牌中随机偷取一张卡牌到自己手中
    /// </summary>
    public class StealCardEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // 从目标玩家手牌中偷取一张卡牌到自己手中
            var stolenCard = StealRandomCardFromPlayer(request.targetPlayerId, request.userId);

            if (stolenCard.HasValue)
            {
                CardUtilities.LogDebug($"玩家{request.userId}从玩家{request.targetPlayerId}偷取了卡牌{stolenCard.Value}");
                return new CardEffectResult(true, "成功偷取了目标玩家的一张卡牌");
            }
            else
            {
                CardUtilities.LogDebug($"玩家{request.targetPlayerId}没有卡牌可偷取");
                return new CardEffectResult(false, "目标玩家没有卡牌可偷取");
            }
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            // 检查目标玩家是否有效且不是自己
            if (request.targetPlayerId <= 0 || request.targetPlayerId == request.userId)
            {
                return false;
            }

            // TODO: 检查目标玩家是否有卡牌，自己手牌是否已满
            // return HasCards(request.targetPlayerId) && !IsHandFull(request.userId);
            return true;
        }

        public string GetDescription(CardData cardData)
        {
            return cardData.description;
        }

        private int? StealRandomCardFromPlayer(int fromPlayerId, int toPlayerId)
        {
            // TODO: 与卡牌管理系统集成
            // return CardSystemManager.Instance.StealRandomCardBetweenPlayers(fromPlayerId, toPlayerId);
            return 1; // 暂时返回假的卡牌ID
        }
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
            system.RegisterEffect(EffectType.GetCard, new DrawCardEffect());
            system.RegisterEffect(EffectType.GetCard, new StealCardEffect()); // 注意：这里可能需要区分

            CardUtilities.LogDebug("所有卡牌效果注册完成");
        }
    }

    #endregion
}