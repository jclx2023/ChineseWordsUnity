using System.Collections.Generic;
using UnityEngine;
using Cards.Core;

namespace Cards.Effects
{
    /// <summary>
    /// ���п���Ч���ľ���ʵ��
    /// �򻯰棬ֻʵ�ֻ�������
    /// </summary>

    #region ����ֵ��Ч��

    /// <summary>
    /// ��Ѫ��Ч��
    /// </summary>
    public class HealEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // TODO: ��PlayerHealthManager����
            // PlayerHealthManager.Instance.AddHealth(request.userId, (int)cardData.effectValue);

            CardUtilities.LogDebug($"���{request.userId}�ظ�{cardData.effectValue}������ֵ");
            return new CardEffectResult(true, $"�ظ���{cardData.effectValue}������ֵ");
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            // TODO: �������Ƿ񻹻��ţ��Ƿ�����Ѫ
            return true;
        }

        public string GetDescription(CardData cardData)
        {
            return cardData.description;
        }
    }

    /// <summary>
    /// �ӱ���Ч�� - �´δ����˺�����
    /// </summary>
    public class DamageMultiplierEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // �����˺��������
            SetDamageMultiplier(cardData.effectValue);

            CardUtilities.LogDebug($"���{request.userId}ʹ�üӱ������´δ����˺�x{cardData.effectValue}");
            return new CardEffectResult(true, $"�´δ����˺�����{cardData.effectValue}��");
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
            // TODO: ����Ϸϵͳ���ɣ������˺�����
            // GameManager.Instance.SetNextDamageMultiplier(multiplier);
        }
    }

    #endregion

    #region ʱ����Ч��

    /// <summary>
    /// ��ʱ��Ч��
    /// </summary>
    public class AddTimeEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // Ϊʹ���߻����ʱЧ��
            SetTimeBonus(request.userId, cardData.effectValue);

            CardUtilities.LogDebug($"���{request.userId}���{cardData.effectValue}���ʱ");
            return new CardEffectResult(true, $"�´δ��⽫���{cardData.effectValue}�����ʱ��");
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
            // TODO: ��TimerManager����
            // TimerManager.Instance.SetTimeBonusForPlayer(playerId, bonusTime);
        }
    }

    /// <summary>
    /// ��ʱ��Ч��
    /// </summary>
    public class ReduceTimeEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // ΪĿ��������ü�ʱЧ��
            SetTimePenalty(request.targetPlayerId, cardData.effectValue);

            CardUtilities.LogDebug($"���{request.targetPlayerId}������{cardData.effectValue}�����ʱ��");
            return new CardEffectResult(true, $"Ŀ������´δ��⽫����{cardData.effectValue}��ʱ��");
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            // ���Ŀ������Ƿ���Ч
            return request.targetPlayerId > 0 && request.targetPlayerId != request.userId;
        }

        public string GetDescription(CardData cardData)
        {
            return cardData.description;
        }

        private void SetTimePenalty(int playerId, float penaltyTime)
        {
            // TODO: ��TimerManager����
            // TimerManager.Instance.SetTimePenaltyForPlayer(playerId, penaltyTime);
        }
    }

    #endregion

    #region ��Ŀ��Ч��

    /// <summary>
    /// ������Ч��
    /// </summary>
    public class SkipQuestionEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // Ϊʹ���������������
            SetSkipFlag(request.userId);

            CardUtilities.LogDebug($"���{request.userId}�´δ��⽫������");
            return new CardEffectResult(true, "�´��ֵ���ʱ���Զ�����");
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
            // TODO: ��غϹ���ϵͳ����
            // TurnManager.Instance.SetSkipFlagForPlayer(playerId);
        }
    }

    /// <summary>
    /// �������Ч��
    /// </summary>
    public class ChengYuChainEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // Ϊʹ���������´���Ŀ����
            SetNextQuestionType(request.userId, "IdiomChain");

            CardUtilities.LogDebug($"���{request.userId}�´δ��⽫�ǳ������");
            return new CardEffectResult(true, "�´δ��⽫�ǳ��������Ŀ");
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
            // TODO: ����Ŀ����������
            // QuestionManager.Instance.SetNextQuestionTypeForPlayer(playerId, questionType);
        }
    }

    /// <summary>
    /// �ж���Ч��
    /// </summary>
    public class JudgeQuestionEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // Ϊʹ���������´���Ŀ����
            SetNextQuestionType(request.userId, "TrueFalse");

            CardUtilities.LogDebug($"���{request.userId}�´δ��⽫���ж���");
            return new CardEffectResult(true, "�´δ��⽫���ж���");
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
            // TODO: ����Ŀ����������
            // QuestionManager.Instance.SetNextQuestionTypeForPlayer(playerId, questionType);
        }
    }

    /// <summary>
    /// ָ���ش�Ч��
    /// </summary>
    public class SpecifyPlayerAnswerEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // �����´δ���ʱ��ָ����Ҵ���ش�
            SetAnswerDelegate(request.userId, request.targetPlayerId);

            CardUtilities.LogDebug($"���{request.userId}�´δ��⽫�����{request.targetPlayerId}����");
            return new CardEffectResult(true, "�´δ��⽫��ָ����Ҵ���ش�");
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            // ���Ŀ������Ƿ���Ч�Ҳ����Լ�
            return request.targetPlayerId > 0 && request.targetPlayerId != request.userId;
        }

        public string GetDescription(CardData cardData)
        {
            return cardData.description;
        }

        private void SetAnswerDelegate(int originalPlayerId, int delegatePlayerId)
        {
            // TODO: ��غϹ���ϵͳ����
            // TurnManager.Instance.SetAnswerDelegate(originalPlayerId, delegatePlayerId);
        }
    }

    #endregion

    #region ����Ч��

    /// <summary>
    /// С��Ч�� - ������ʾ
    /// </summary>
    public class ExtraHintEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // Ϊʹ�������ö�����ʾ���
            SetExtraHintFlag(request.userId);

            CardUtilities.LogDebug($"���{request.userId}�´δ��⽫��ö�����ʾ");
            return new CardEffectResult(true, "�´δ��⽫��ø�����ʾ����");
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
            // TODO: ��UIϵͳ����
            // UIManager.Instance.SetExtraHintForPlayer(playerId);
        }
    }

    /// <summary>
    /// ��ÿ���Ч�� - ���������ú�"����ɽ��"
    /// </summary>
    public class DrawCardEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            int cardCount = (int)cardData.effectValue;

            if (cardCount > 0)
            {
                // ��ͨ��ÿ���
                DrawCardsForPlayer(request.userId, cardCount);
                CardUtilities.LogDebug($"���{request.userId}���{cardCount}�ſ���");
                return new CardEffectResult(true, $"�����{cardCount}�ſ���");
            }
            else
            {
                // ����"����ɽ��"������ض�����
                DrawSpecificCard(request.userId, 3, 2); // ���2�żӱ���(ID=3)
                CardUtilities.LogDebug($"���{request.userId}���2�żӱ���");
                return new CardEffectResult(true, "�����2�żӱ���");
            }
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            // TODO: ��������Ƿ�����
            return true;
        }

        public string GetDescription(CardData cardData)
        {
            return cardData.description;
        }

        private void DrawCardsForPlayer(int playerId, int count)
        {
            // TODO: �뿨�ƹ���ϵͳ����
            // CardSystemManager.Instance.DrawCardsForPlayer(playerId, count);
        }

        private void DrawSpecificCard(int playerId, int cardId, int count)
        {
            // TODO: �뿨�ƹ���ϵͳ����
            // CardSystemManager.Instance.AddSpecificCardToPlayer(playerId, cardId, count);
        }
    }

    /// <summary>
    /// ͵ȡ����Ч�� - "������Ƥ"
    /// ��Ŀ��������������͵ȡһ�ſ��Ƶ��Լ�����
    /// </summary>
    public class StealCardEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // ��Ŀ�����������͵ȡһ�ſ��Ƶ��Լ�����
            var stolenCard = StealRandomCardFromPlayer(request.targetPlayerId, request.userId);

            if (stolenCard.HasValue)
            {
                CardUtilities.LogDebug($"���{request.userId}�����{request.targetPlayerId}͵ȡ�˿���{stolenCard.Value}");
                return new CardEffectResult(true, "�ɹ�͵ȡ��Ŀ����ҵ�һ�ſ���");
            }
            else
            {
                CardUtilities.LogDebug($"���{request.targetPlayerId}û�п��ƿ�͵ȡ");
                return new CardEffectResult(false, "Ŀ�����û�п��ƿ�͵ȡ");
            }
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            // ���Ŀ������Ƿ���Ч�Ҳ����Լ�
            if (request.targetPlayerId <= 0 || request.targetPlayerId == request.userId)
            {
                return false;
            }

            // TODO: ���Ŀ������Ƿ��п��ƣ��Լ������Ƿ�����
            // return HasCards(request.targetPlayerId) && !IsHandFull(request.userId);
            return true;
        }

        public string GetDescription(CardData cardData)
        {
            return cardData.description;
        }

        private int? StealRandomCardFromPlayer(int fromPlayerId, int toPlayerId)
        {
            // TODO: �뿨�ƹ���ϵͳ����
            // return CardSystemManager.Instance.StealRandomCardBetweenPlayers(fromPlayerId, toPlayerId);
            return 1; // ��ʱ���ؼٵĿ���ID
        }
    }

    #endregion

    #region Ч��ע����

    /// <summary>
    /// Ч��ע���� - ����ע������Ч����ϵͳ
    /// </summary>
    public static class CardEffectRegistrar
    {
        /// <summary>
        /// ע������Ĭ��Ч��
        /// </summary>
        public static void RegisterAllEffects(CardEffectSystem system)
        {
            if (system == null)
            {
                CardUtilities.LogError("CardEffectSystemΪ�գ��޷�ע��Ч��");
                return;
            }

            // ����ֵ��
            system.RegisterEffect(EffectType.Heal, new HealEffect());
            system.RegisterEffect(EffectType.Damage, new DamageMultiplierEffect());

            // ʱ����
            system.RegisterEffect(EffectType.AddTime, new AddTimeEffect());
            system.RegisterEffect(EffectType.ReduceTime, new ReduceTimeEffect());

            // ��Ŀ��
            system.RegisterEffect(EffectType.SkipQuestion, new SkipQuestionEffect());
            system.RegisterEffect(EffectType.ChengYuChain, new ChengYuChainEffect());
            system.RegisterEffect(EffectType.JudgeQuestion, new JudgeQuestionEffect());
            system.RegisterEffect(EffectType.SpecifyQuestion, new SpecifyPlayerAnswerEffect());

            // ������
            system.RegisterEffect(EffectType.ExtraChance, new ExtraHintEffect());
            system.RegisterEffect(EffectType.GetCard, new DrawCardEffect());
            system.RegisterEffect(EffectType.GetCard, new StealCardEffect()); // ע�⣺���������Ҫ����

            CardUtilities.LogDebug("���п���Ч��ע�����");
        }
    }

    #endregion
}