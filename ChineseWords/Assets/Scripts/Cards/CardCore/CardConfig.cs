using System.Collections.Generic;
using UnityEngine;
using Cards.Core;

namespace Cards.Core
{
    /// <summary>
    /// ��������ScriptableObject
    /// ������Inspector�����ÿ������ݺ�ϵͳ����
    /// </summary>
    [CreateAssetMenu(fileName = "New Card Config", menuName = "Card System/Card Config")]
    public class CardConfig : ScriptableObject
    {
        [Header("��������")]
        [SerializeField] private List<CardData> allCards = new List<CardData>();

        [Header("ϵͳ����")]
        [SerializeField] private CardSystemSettings systemSettings = new CardSystemSettings();

        [Header("�鿨����")]
        [SerializeField] private CardDrawSettings drawSettings = new CardDrawSettings();

        [Header("��Ϸ����")]
        [SerializeField] private CardGameRules gameRules = new CardGameRules();

        #region ���Է�����

        /// <summary>
        /// ���п�������
        /// </summary>
        public List<CardData> AllCards => allCards;

        /// <summary>
        /// ϵͳ����
        /// </summary>
        public CardSystemSettings SystemSettings => systemSettings;

        /// <summary>
        /// �鿨����
        /// </summary>
        public CardDrawSettings DrawSettings => drawSettings;

        /// <summary>
        /// ��Ϸ����
        /// </summary>
        public CardGameRules GameRules => gameRules;

        #endregion

        #region ���Ʋ�ѯ����

        /// <summary>
        /// ����ID��ȡ��������
        /// </summary>
        public CardData GetCardById(int cardId)
        {
            return allCards.Find(card => card.cardId == cardId);
        }

        /// <summary>
        /// ����Ч�����ͻ�ȡ�����б�
        /// </summary>
        public List<CardData> GetCardsByEffectType(EffectType effectType)
        {
            return allCards.FindAll(card => card.effectType == effectType);
        }

        /// <summary>
        /// ����ϡ�жȻ�ȡ�����б���ѡ���ܣ�
        /// </summary>
        public List<CardData> GetCardsByRarity(CardRarity rarity)
        {
            return allCards.FindAll(card => card.rarity == rarity);
        }

        /// <summary>
        /// ���ݿ������ͻ�ȡ�����б�
        /// </summary>
        public List<CardData> GetCardsByType(CardType cardType)
        {
            return allCards.FindAll(card => card.cardType == cardType);
        }

        #endregion

        #region ��֤�ͳ�ʼ��

        /// <summary>
        /// ��֤�������ݵ���Ч��
        /// </summary>
        public bool ValidateConfig()
        {
            if (allCards == null || allCards.Count == 0)
            {
                Debug.LogWarning($"{CardConstants.LOG_TAG} ��������Ϊ��");
                return false;
            }

            // ��鿨��IDΨһ��
            HashSet<int> cardIds = new HashSet<int>();
            foreach (var card in allCards)
            {
                if (card.cardId <= 0)
                {
                    Debug.LogError($"{CardConstants.LOG_TAG} ���� {card.cardName} ��ID��Ч: {card.cardId}");
                    return false;
                }

                if (cardIds.Contains(card.cardId))
                {
                    Debug.LogError($"{CardConstants.LOG_TAG} �ظ��Ŀ���ID: {card.cardId}");
                    return false;
                }

                cardIds.Add(card.cardId);
            }

            // ��֤ϵͳ����
            if (!systemSettings.Validate())
            {
                Debug.LogError($"{CardConstants.LOG_TAG} ϵͳ������֤ʧ��");
                return false;
            }

            // ��֤�鿨����
            if (!drawSettings.Validate())
            {
                Debug.LogError($"{CardConstants.LOG_TAG} �鿨������֤ʧ��");
                return false;
            }

            Debug.Log($"{CardConstants.LOG_TAG} ����������֤�ɹ�����{allCards.Count}�ſ���");
            return true;
        }

        /// <summary>
        /// ����Ĭ�Ͽ������ݣ��������ṩ�Ŀ���Ч����
        /// </summary>
        [ContextMenu("����Ĭ�Ͽ�������")]
        public void CreateDefaultCards()
        {
            allCards.Clear();

            // ������Ŀ���Ч����������
            var defaultCards = new List<CardData>
            {
                // �Է��Ϳ���
                new CardData
                {
                    cardId = 1, cardName = "��Ѫ��", description = "Ϊ����ظ�1������ֵ",
                    cardType = CardType.SelfTarget, effectType = EffectType.Heal, targetType = TargetType.Self,
                    effectValue = 1f, rarity = CardRarity.Common, drawWeight = 15f, canUseWhenNotMyTurn = true
                },
                new CardData
                {
                    cardId = 2, cardName = "������", description = "�´��ֵ��Լ�����ʱ������λغ�",
                    cardType = CardType.SelfTarget, effectType = EffectType.SkipQuestion, targetType = TargetType.Self,
                    effectValue = 1f, rarity = CardRarity.Common, drawWeight = 12f, canUseWhenNotMyTurn = true
                },
                new CardData
                {
                    cardId = 3, cardName = "�ӱ���", description = "�´�����Ҵ���ĸô��˺�����",
                    cardType = CardType.SelfTarget, effectType = EffectType.Damage, targetType = TargetType.AllOthers,
                    effectValue = 2f, rarity = CardRarity.Uncommon, drawWeight = 8f, canUseWhenNotMyTurn = true
                },
                new CardData
                {
                    cardId = 4, cardName = "��ʱ��", description = "�´��ֵ��Լ�����ʱ����5�����ʱ��",
                    cardType = CardType.SelfTarget, effectType = EffectType.AddTime, targetType = TargetType.Self,
                    effectValue = 5f, rarity = CardRarity.Common, drawWeight = 10f, canUseWhenNotMyTurn = true
                },
                new CardData
                {
                    cardId = 5, cardName = "�������", description = "�´��ֵ��Լ�����ʱ�ض�Ϊ���������Ŀ",
                    cardType = CardType.SelfTarget, effectType = EffectType.ChengYuChain, targetType = TargetType.Self,
                    effectValue = 1f, rarity = CardRarity.Rare, drawWeight = 5f, canUseWhenNotMyTurn = true
                },
                new CardData
                {
                    cardId = 6, cardName = "�ж���", description = "�´��ֵ��Լ�����ʱ�ض�Ϊ�ж���",
                    cardType = CardType.SelfTarget, effectType = EffectType.JudgeQuestion, targetType = TargetType.Self,
                    effectValue = 1f, rarity = CardRarity.Common, drawWeight = 12f, canUseWhenNotMyTurn = true
                },
                new CardData
                {
                    cardId = 7, cardName = "С��", description = "�´��ֵ��Լ�����ʱ��ø�����ʾ����",
                    cardType = CardType.SelfTarget, effectType = EffectType.ExtraChance, targetType = TargetType.Self,
                    effectValue = 1f, rarity = CardRarity.Uncommon, drawWeight = 8f, canUseWhenNotMyTurn = true
                },
                new CardData
                {
                    cardId = 8, cardName = "���������ſ���", description = "���������ſ���",
                    cardType = CardType.SelfTarget, effectType = EffectType.GetCard, targetType = TargetType.Self,
                    effectValue = 2f, rarity = CardRarity.Rare, drawWeight = 4f, canUseWhenNotMyTurn = true
                },

                // ָ���Ϳ���
                new CardData
                {
                    cardId = 9, cardName = "ָ���ش�", description = "�´��ֵ��Լ�����ʱ����ָ������һش����Ѳ��ɻش�",
                    cardType = CardType.PlayerTarget, effectType = EffectType.SpecifyQuestion, targetType = TargetType.SinglePlayer,
                    effectValue = 1f, rarity = CardRarity.Rare, drawWeight = 3f, canUseWhenNotMyTurn = true
                },
                new CardData
                {
                    cardId = 10, cardName = "��ʱ��", description = "�´��ֵ�ָ����Ҵ���ʱ�����3�����ʱ��",
                    cardType = CardType.PlayerTarget, effectType = EffectType.ReduceTime, targetType = TargetType.SinglePlayer,
                    effectValue = 3f, rarity = CardRarity.Common, drawWeight = 10f, canUseWhenNotMyTurn = true
                },
                new CardData
                {
                    cardId = 11, cardName = "ȥ������", description = "��ָ����������Ƴ�һ�ſ���",
                    cardType = CardType.PlayerTarget, effectType = EffectType.GetCard, targetType = TargetType.SinglePlayer,
                    effectValue = -1f, rarity = CardRarity.Uncommon, drawWeight = 6f, canUseWhenNotMyTurn = true
                },
                new CardData
                {
                    cardId = 12, cardName = "���ֿ���", description = "������żӱ���",
                    cardType = CardType.SelfTarget, effectType = EffectType.GetCard, targetType = TargetType.Self,
                    effectValue = 2f, rarity = CardRarity.Epic, drawWeight = 2f, canUseWhenNotMyTurn = true
                }
            };

            allCards.AddRange(defaultCards);
            Debug.Log($"{CardConstants.LOG_TAG} �Ѵ���{defaultCards.Count}��Ĭ�Ͽ���");
        }

        #endregion
    }

    #region �������ݽṹ

    /// <summary>
    /// ����ϵͳ���ã��򻯰棩
    /// </summary>
    [System.Serializable]
    public class CardSystemSettings
    {
        [Header("��������")]
        public int maxHandSize = CardConstants.DEFAULT_MAX_HAND_SIZE;
        public int startingCardCount = CardConstants.DEFAULT_STARTING_CARDS;
        public int cardsReceivedOnElimination = 2;

        [Header("ʹ�ù���")]
        public bool allowCardUseWhenNotMyTurn = true;  // �Ƿ������ڷ��Լ��غ�ʱʹ�ÿ���

        [Header("��������")]
        public bool enableDebugLogs = true;
        public bool showEffectValues = true;

        public bool Validate()
        {
            if (maxHandSize <= 0 || maxHandSize > 20)
            {
                Debug.LogError("�����������������1-20֮��");
                return false;
            }

            if (startingCardCount < 0 || startingCardCount > maxHandSize)
            {
                Debug.LogError("��ʼ�����������ܳ��������������");
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// �鿨����
    /// </summary>
    [System.Serializable]
    public class CardDrawSettings
    {
        [Header("�鿨����")]
        public bool allowDuplicates = true;
        public int maxSameCardInHand = 3;
        public bool guaranteeRareInStarting = false;

        [Header("����鿨")]
        public List<int> bannedCardIds = new List<int>();
        public List<int> forcedStartingCards = new List<int>();

        public bool Validate()
        {
            if (maxSameCardInHand <= 0)
            {
                Debug.LogError("ͬ�ֿ�����������������0");
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// ������Ϸ���򣨼򻯰棩
    /// </summary>
    [System.Serializable]
    public class CardGameRules
    {
        [Header("��������")]
        public bool oneCardPerRound = true;             // ÿ��ֻ��ʹ��һ�ſ���
        public bool resetUsageAfterEachTurn = true;     // ÿ�δ��������ʹ�û���

        [Header("�������")]
        public bool enableCardStealing = true;          // ���ÿ���͵ȡ��ȥ�����ؿ���
        public bool discardUsedCards = true;            // ʹ�ú�Ŀ��ƶ���

        public bool Validate()
        {
            // �򻯺������������֤ʧ�ܵ����
            return true;
        }
    }

    #endregion
}