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

        [Header("�Զ�����")]
        [SerializeField] private bool autoConfigureCardsOnValidate = false;

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
        /// ����ϡ�жȻ�ȡ�����б�
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

        /// <summary>
        /// ��ȡ�����Է��Ϳ���
        /// </summary>
        public List<CardData> GetSelfTargetCards()
        {
            return GetCardsByType(CardType.SelfTarget);
        }

        /// <summary>
        /// ��ȡ����ָ���Ϳ���
        /// </summary>
        public List<CardData> GetPlayerTargetCards()
        {
            return GetCardsByType(CardType.PlayerTarget);
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

            // ��֤�鿨����
            if (!drawSettings.Validate())
            {
                Debug.LogError($"{CardConstants.LOG_TAG} �鿨������֤ʧ��");
                return false;
            }

            Debug.Log($"{CardConstants.LOG_TAG} ����������֤�ɹ�����{allCards.Count}�ſ���");
            return true;
        }

        #endregion
    }

    #region �������ݽṹ�����ֲ��䣩

    /// <summary>
    /// ����ϵͳ����
    /// </summary>
    [System.Serializable]
    public class CardSystemSettings
    {
        [Header("��������")]
        public int maxHandSize = CardConstants.DEFAULT_MAX_HAND_SIZE;
        public int startingCardCount = CardConstants.DEFAULT_STARTING_CARDS;
        public int cardsReceivedOnElimination = 2;

        [Header("ʹ�ù���")]
        public bool allowCardUseWhenNotMyTurn = true;

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
    /// ������Ϸ����
    /// </summary>
    [System.Serializable]
    public class CardGameRules
    {
        [Header("��������")]
        public bool oneCardPerRound = true;
        public bool resetUsageAfterEachTurn = true;

        [Header("�������")]
        public bool enableCardStealing = true;
        public bool discardUsedCards = true;

        public bool Validate()
        {
            return true;
        }
    }

    #endregion
}