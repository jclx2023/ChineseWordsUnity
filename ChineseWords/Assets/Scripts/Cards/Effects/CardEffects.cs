using System.Collections.Generic;
using UnityEngine;
using Cards.Core;
using Cards.Integration;

namespace Cards.Effects
{
    /// <summary>
    /// ���п���Ч���ľ���ʵ��
    /// �Ѹ��� - ����CardGameBridge���滻����TODO
    /// </summary>

    #region ����ֵ��Ч��

    /// <summary>
    /// ��Ѫ��Ч��
    /// </summary>
    public class HealEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // ͨ��CardGameBridge�޸��������ֵ
            bool success = CardGameBridge.ModifyPlayerHealth(request.userId, (int)cardData.effectValue);

            if (success)
            {
                CardUtilities.LogDebug($"���{request.userId}�ظ�{cardData.effectValue}������ֵ");
                return new CardEffectResult(true, $"�ظ���{cardData.effectValue}������ֵ");
            }
            else
            {
                CardUtilities.LogDebug($"���{request.userId}��Ѫʧ�� - ��������Ѫ��������");
                return new CardEffectResult(false, "��Ѫʧ��");
            }
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            // �������Ƿ񻹻���
            return CardGameBridge.IsPlayerAlive(request.userId);
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
            // ͨ��CardGameBridge����ȫ���˺�����
            CardGameBridge.SetGlobalDamageMultiplier(multiplier);
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
            // ͨ��CardGameBridge����ʱ��ӳ�
            CardGameBridge.SetPlayerTimeBonus(playerId, bonusTime);
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
            // ���Ŀ������Ƿ���Ч�Ҵ��
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
            // ͨ��CardGameBridge����ʱ�����
            CardGameBridge.SetPlayerTimePenalty(playerId, penaltyTime);
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
            // ͨ��CardGameBridge�����������
            CardGameBridge.SetPlayerSkipFlag(playerId, true);
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
            // ͨ��CardGameBridge�����´���Ŀ����
            CardGameBridge.SetPlayerNextQuestionType(playerId, questionType);
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
            // ͨ��CardGameBridge�����´���Ŀ����
            CardGameBridge.SetPlayerNextQuestionType(playerId, questionType);
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
            // ���Ŀ������Ƿ���Ч�Ҳ����Լ������Ҵ��
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
            // ͨ��CardGameBridge���ô������
            CardGameBridge.SetAnswerDelegate(originalPlayerId, delegatePlayerId);
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
            // ͨ��CardGameBridge���ö�����ʾ���
            CardGameBridge.SetPlayerExtraHint(playerId, true);
        }
    }

    /// <summary>
    /// ͳһ�Ŀ��Ʋ���Ч�� - �������� EffectType.GetCard �����
    /// ͨ�� effectValue �� targetType ���ֲ�ͬ��Ϊ
    /// </summary>
    public class CardManipulationEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            int effectValue = (int)cardData.effectValue;

            // ���� effectValue �� targetType �жϾ�����Ϊ
            if (effectValue < 0)
            {
                // effectValue < 0: ͵ȡ���� ("������Ƥ")
                return ExecuteStealCard(request, cardData);
            }
            else if (cardData.targetType == TargetType.Self && effectValue > 0)
            {
                // targetType = Self + effectValue > 0: �Լ�����������
                return ExecuteDrawRandomCards(request, cardData);
            }
            else if (cardData.targetType == TargetType.Self && effectValue == 0)
            {
                // �������: "����ɽ��" - ����ض�����
                return ExecuteDrawSpecificCards(request, cardData);
            }
            else
            {
                // Ĭ�����: ��ͨ��ÿ���
                return ExecuteDrawRandomCards(request, cardData);
            }
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            int effectValue = (int)cardData.effectValue;

            if (effectValue < 0)
            {
                // ͵ȡ����: ��Ҫ��Ч��Ŀ�����
                return request.targetPlayerId > 0 &&
                       request.targetPlayerId != request.userId &&
                       CardGameBridge.IsPlayerAlive(request.targetPlayerId);
            }
            else
            {
                // ��ÿ���: ���ǿ���ʹ�ã���������Ҳ�����Ž����д���
                return true;
            }
        }

        public string GetDescription(CardData cardData)
        {
            return cardData.description;
        }

        #region ������Ϊʵ��

        /// <summary>
        /// ִ��͵ȡ����
        /// </summary>
        private CardEffectResult ExecuteStealCard(CardUseRequest request, CardData cardData)
        {
            var stolenCard = CardGameBridge.StealRandomCardFromPlayer(request.targetPlayerId, request.userId);

            if (stolenCard.HasValue)
            {
                CardUtilities.LogDebug($"���{request.userId}�����{request.targetPlayerId}͵ȡ�˿���{stolenCard.Value}");

                var stolenCardData = CardGameBridge.GetCardDataById(stolenCard.Value);
                string cardName = stolenCardData?.cardName ?? "δ֪����";

                return new CardEffectResult(true, $"�ɹ�͵ȡ��Ŀ����ҵ�һ�ſ��ƣ�{cardName}");
            }
            else
            {
                CardUtilities.LogDebug($"���{request.targetPlayerId}û�п��ƿ�͵ȡ");
                return new CardEffectResult(false, "Ŀ�����û�п��ƿ�͵ȡ");
            }
        }

        /// <summary>
        /// ִ�л���������
        /// </summary>
        private CardEffectResult ExecuteDrawRandomCards(CardUseRequest request, CardData cardData)
        {
            int cardCount = (int)cardData.effectValue;
            bool success = CardGameBridge.GiveCardToPlayer(request.userId, 0, cardCount); // cardId=0��ʾ���

            CardUtilities.LogDebug($"���{request.userId}���{cardCount}��������ƣ��ɹ�:{success}");

            if (success)
            {
                return new CardEffectResult(true, $"�����{cardCount}�ſ���");
            }
            else
            {
                return new CardEffectResult(false, "�����������޷���ø��࿨��");
            }
        }

        /// <summary>
        /// ִ�л���ض����ƣ�����ɽ��
        /// </summary>
        private CardEffectResult ExecuteDrawSpecificCards(CardUseRequest request, CardData cardData)
        {
            // "����ɽ��"�����2�żӱ���(ID=3)
            bool success = CardGameBridge.GiveCardToPlayer(request.userId, 3, 2);

            CardUtilities.LogDebug($"���{request.userId}���2�żӱ������ɹ�:{success}");

            if (success)
            {
                return new CardEffectResult(true, "�����2�żӱ���");
            }
            else
            {
                return new CardEffectResult(false, "�����������޷���üӱ���");
            }
        }

        #endregion
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
            system.RegisterEffect(EffectType.GetCard, new CardManipulationEffect());

            CardUtilities.LogDebug("���п���Ч��ע�����");
            CardUtilities.LogDebug("CardManipulationEffect ��ע�ᣬͳһ���������ÿ��ơ�͵ȡ���ơ�����ض�����");
        }
    }

    #endregion
}