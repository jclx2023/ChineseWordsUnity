using System.Collections.Generic;
using UnityEngine;
using Cards.Core;
using Cards.Integration;

namespace Cards.Effects
{
    /// <summary>
    /// ���п���Ч���ľ���ʵ��
    /// ���°� - ֧���µ�12�ſ���Ч��
    /// </summary>

    #region ����ֵ��Ч��

    /// <summary>
    /// ������?��Ч�� - ID1: ţ�̺�
    /// </summary>
    public class HealEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // ͨ��CardGameBridge�޸���������?
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
    /// Ⱥ����?��Ч�� - ID7: ���ջ���
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

            CardUtilities.LogDebug($"Ⱥ����?��{successCount}/{allPlayerIds.Count}����һظ�{healAmount}������ֵ");

            if (successCount > 0)
            {
                return new CardEffectResult(true, $"������һظ���{healAmount}������ֵ");
            }
            else
            {
                return new CardEffectResult(false, "Ⱥ����?ʧ��");
            }
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            // ֻҪ����Ҵ��Ϳ���ʹ��?
            var alivePlayerIds = CardGameBridge.GetAllAlivePlayerIds();
            return alivePlayerIds.Count > 0;
        }

        public string GetDescription(CardData cardData)
        {
            return cardData.description;
        }
    }

    /// <summary>
    /// �ӱ��˺���Ч�� - ID3: �����۱�
    /// </summary>
    public class DamageMultiplierEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // �����˺��������?
            CardGameBridge.SetGlobalDamageMultiplier(cardData.effectValue);

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
    }

    /// <summary>
    /// �����˺���Ч�� - ID9: ��ֽ��
    /// </summary>
    public class ProbabilityDamageEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            float hitChance = cardData.effectValue; // effectValue��ʾ���и���(0.5 = 50%)
            float randomValue = Random.Range(0f, 1f);

            if (randomValue <= hitChance)
            {
                // ���У����?1���˺�
                bool success = CardGameBridge.ModifyPlayerHealth(request.targetPlayerId, -10);

                CardUtilities.LogDebug($"���{request.userId}�Ķ�ֽ���������{request.targetPlayerId}�����?10���˺�");

                if (success)
                {
                    return new CardEffectResult(true, "ֽ�����У�Ŀ������ܵ�?10���˺�");
                }
                else
                {
                    return new CardEffectResult(false, "ֽ�������ˣ���Ŀ�����������?");
                }
            }
            else
            {
                // δ����
                CardUtilities.LogDebug($"���{request.userId}�Ķ�ֽ��δ�������{request.targetPlayerId}");
                return new CardEffectResult(true, "ֽ��û������Ŀ��");
            }
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            // ���Ŀ������Ƿ���Ч�Ҵ��?�Ҳ����Լ�
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

    #region ʱ����Ч��

    /// <summary>
    /// ��ʱ��Ч�� - ID4: ������
    /// </summary>
    public class AddTimeEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // Ϊʹ���߻����ʱЧ��?
            CardGameBridge.SetPlayerTimeBonus(request.userId, cardData.effectValue);

            CardUtilities.LogDebug($"���{request.userId}���{cardData.effectValue}����?");
            return new CardEffectResult(true, $"�´δ��⽫���{cardData.effectValue}�����ʱ��?");
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
    /// ��ʱ��Ч�� - ID10: ��ʱ��
    /// </summary>
    public class ReduceTimeEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // ΪĿ��������ü�ʱЧ��?
            CardGameBridge.SetPlayerTimePenalty(request.targetPlayerId, cardData.effectValue);

            CardUtilities.LogDebug($"���{request.targetPlayerId}������{cardData.effectValue}�����ʱ��?");
            return new CardEffectResult(true, $"Ŀ������´δ���?����{cardData.effectValue}��ʱ��");
        }

        public bool CanUse(CardUseRequest request, CardData cardData)
        {
            // ���Ŀ������Ƿ���Ч�Ҵ��?�Ҳ����Լ�
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

    #region ��Ŀ��Ч��

    /// <summary>
    /// ������Ч�� - ID2: �����?
    /// </summary>
    public class SkipQuestionEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // Ϊʹ���������������?
            CardGameBridge.SetPlayerSkipFlag(request.userId, true);

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
    }

    /// <summary>
    /// �������Ч��? - ID5: �������?
    /// </summary>
    public class ChengYuChainEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // Ϊʹ���������´���Ŀ����
            CardGameBridge.SetPlayerNextQuestionType(request.userId, "IdiomChain");

            CardUtilities.LogDebug($"���{request.userId}�´δ��⽫�ǳ������?");
            return new CardEffectResult(true, "�´δ��⽫�ǳ���������?");
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
    /// �ж���Ч�� - ID6: �ж���
    /// </summary>
    public class JudgeQuestionEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            // Ϊʹ���������´���Ŀ����
            CardGameBridge.SetPlayerNextQuestionType(request.userId, "TrueFalse");

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
    }

    #endregion

    #region ���Ʋ���Ч��

    /// <summary>
    /// ͳһ�Ŀ��Ʋ���Ч�� - �������� EffectType.GetCard �����?
    /// ID8: ���ⲹϰ (���������ſ���)
    /// ID11: ������Ƥ (��ָ�����͵ȡһ�ſ�?)
    /// ID12: һ�з۱� (������żӱ���?)
    /// </summary>
    public class CardManipulationEffect : ICardEffect
    {
        public CardEffectResult Execute(CardUseRequest request, CardData cardData)
        {
            int effectValue = (int)cardData.effectValue;

            // ���ݿ���ID��effectValue�жϾ�����Ϊ
            switch (cardData.cardId)
            {
                case 8: // ���ⲹϰ�����������ſ���
                    return ExecuteDrawRandomCards(request, cardData);

                case 11: // ������Ƥ��͵ȡ���� (effectValue = -1)
                    return ExecuteStealCard(request, cardData);

                case 12: // һ�з۱ʣ�������żӱ���? (effectValue = 3����ʾ�ӱ�����ID)
                    return ExecuteDrawSpecificCards(request, cardData);

                default:
                    // �����߼�������effectValue�ж�
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
            // ���ݿ���ID�ж�ʹ������
            switch (cardData.cardId)
            {
                case 8: // ���ⲹϰ���Է��ͣ����ǿ���
                case 12: // һ�з۱ʣ��Է��ͣ����ǿ���
                    return true;

                case 11: // ������Ƥ��ָ���ͣ���Ҫ��ЧĿ��
                    return request.targetPlayerId > 0 &&
                           request.targetPlayerId != request.userId &&
                           CardGameBridge.IsPlayerAlive(request.targetPlayerId);

                default:
                    // �����߼�
                    int effectValue = (int)cardData.effectValue;
                    if (effectValue < 0)
                    {
                        // ͵ȡ�ࣺ��Ҫ��ЧĿ��
                        return request.targetPlayerId > 0 &&
                               request.targetPlayerId != request.userId &&
                               CardGameBridge.IsPlayerAlive(request.targetPlayerId);
                    }
                    else
                    {
                        // ����ࣺ���ǿ���?
                        return true;
                    }
            }
        }

        public string GetDescription(CardData cardData)
        {
            return cardData.description;
        }

        #region ������Ϊʵ��

        /// <summary>
        /// ִ�л��������� - ID8: ���ⲹϰ
        /// </summary>
        private CardEffectResult ExecuteDrawRandomCards(CardUseRequest request, CardData cardData)
        {
            int cardCount = (int)cardData.effectValue;
            bool success = CardGameBridge.GiveCardToPlayer(request.userId, 0, cardCount); // cardId=0��ʾ���?

            CardUtilities.LogDebug($"���{request.userId}���{cardCount}��������ƣ��ɹ�?:{success}");

            if (success)
            {
                return new CardEffectResult(true, $"�����{cardCount}�ſ���");
            }
            else
            {
                return new CardEffectResult(false, "�����������޷���ø���?��");
            }
        }

        /// <summary>
        /// ִ��͵ȡ���� - ID11: ������Ƥ
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
                return new CardEffectResult(false, "Ŀ�����û�п��ƿ�͵�?");
            }
        }

        /// <summary>
        /// ִ�л���ض�����? - ID12: һ�з۱�
        /// </summary>
        private CardEffectResult ExecuteDrawSpecificCards(CardUseRequest request, CardData cardData)
        {
            // һ�з۱ʣ����?2�żӱ���(ID=3)
            int targetCardId = 3; // �ӱ�����ID
            int cardCount = 2;    // ���?2��

            bool success = CardGameBridge.GiveCardToPlayer(request.userId, targetCardId, cardCount);

            CardUtilities.LogDebug($"���{request.userId}���{cardCount}�żӱ������ɹ�:{success}");

            if (success)
            {
                return new CardEffectResult(true, "�����?2�żӱ���");
            }
            else
            {
                return new CardEffectResult(false, "�����������޷���üӱ���?");
            }
        }

        #endregion
    }

    #endregion

    #region Ч��ע����

    /// <summary>
    /// Ч��ע���� - ����ע������Ч����ϵͳ
    /// ���°� - ֧���µ�Ч������
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
            system.RegisterEffect(EffectType.Heal, new HealEffect());                    // ID1: ţ�̺�
            system.RegisterEffect(EffectType.GroupHeal, new GroupHealEffect());          // ID7: ���ջ���
            system.RegisterEffect(EffectType.Damage, new DamageMultiplierEffect());     // ID3: �����۱�
            system.RegisterEffect(EffectType.ProbabilityDamage, new ProbabilityDamageEffect()); // ID9: ��ֽ��

            // ʱ����
            system.RegisterEffect(EffectType.AddTime, new AddTimeEffect());             // ID4: ������
            system.RegisterEffect(EffectType.ReduceTime, new ReduceTimeEffect());       // ID10: ��ʱ��

            // ��Ŀ��
            system.RegisterEffect(EffectType.SkipQuestion, new SkipQuestionEffect());   // ID2: �����?
            system.RegisterEffect(EffectType.ChengYuChain, new ChengYuChainEffect());   // ID5: �������?
            system.RegisterEffect(EffectType.JudgeQuestion, new JudgeQuestionEffect()); // ID6: �ж���

            // ���Ʋ�����
            system.RegisterEffect(EffectType.GetCard, new CardManipulationEffect());    // ID8: ���ⲹϰ, ID11: ������Ƥ, ID12: һ�з۱�

            CardUtilities.LogDebug("���п���Ч��ע�����?");
        }
    }

    #endregion
}