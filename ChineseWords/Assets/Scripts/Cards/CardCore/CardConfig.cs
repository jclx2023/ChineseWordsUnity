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

        #region �Զ����÷���

        /// <summary>
        /// �Զ�����12�ſ�������
        /// ��Inspector�е���Ҽ��˵���ͨ���������
        /// </summary>
        [ContextMenu("�Զ�����12�ſ���")]
        public void AutoConfigureCards()
        {
            Debug.Log("[CardConfig] ��ʼ�Զ����ÿ�������...");

            allCards.Clear();

            // ID1: ţ�̺� - �Է��ͻ�Ѫ��
            allCards.Add(new CardData
            {
                cardId = 1,
                cardName = "ţ�̺�",
                description = "Ϊ����ظ�1������ֵ",
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

            // ID2: ����� - �Է���������
            allCards.Add(new CardData
            {
                cardId = 2,
                cardName = "�����",
                description = "�´��ֵ��Լ�����ʱ������λغ�",
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

            // ID3: �����۱� - �Է��ͼӱ��˺���
            allCards.Add(new CardData
            {
                cardId = 3,
                cardName = "�����۱�",
                description = "�´�����Ҵ��ĸô��˺�����",
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

            // ID4: ������ - �Է��ͼ�ʱ��
            allCards.Add(new CardData
            {
                cardId = 4,
                cardName = "������",
                description = "�´��ֵ��Լ�����ʱ����5�����ʱ��",
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

            // ID5: ������� - �Է�����Ŀ����ָ����
            allCards.Add(new CardData
            {
                cardId = 5,
                cardName = "�������",
                description = "�´��ֵ��Լ�����ʱ�ض�Ϊ���������Ŀ",
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

            // ID6: �ж��� - �Է�����Ŀ����ָ����
            allCards.Add(new CardData
            {
                cardId = 6,
                cardName = "�ж���",
                description = "�´��ֵ��Լ�����ʱ�ض����ж���",
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

            // ID7: ���ջ��� - �Է���Ⱥ���Ѫ��
            allCards.Add(new CardData
            {
                cardId = 7,
                cardName = "���ջ���",
                description = "������һظ�1������ֵ",
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

            // ID8: ���ⲹϰ - �Է��ͻ�ÿ���
            allCards.Add(new CardData
            {
                cardId = 8,
                cardName = "���ⲹϰ",
                description = "���������ſ���",
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

            // ID9: ��ֽ�� - ָ���͸����˺���
            allCards.Add(new CardData
            {
                cardId = 9,
                cardName = "��ֽ��",
                description = "50%�������У���ָ��������1���˺�",
                cardType = CardType.PlayerTarget,
                effectType = EffectType.ProbabilityDamage,
                targetType = TargetType.SinglePlayer,
                rarity = CardRarity.Rare,
                drawWeight = 3f,
                effectValue = 0.5f, // ��ʾ50%����
                canUseWhenNotMyTurn = true,
                cardBackgroundColor = Color.red,
                showEffectValue = false
            });

            // ID10: ��ʱ�� - ָ���ͼ�ʱ��
            allCards.Add(new CardData
            {
                cardId = 10,
                cardName = "��ʱ��",
                description = "�´��ֵ���ָ������Ҵ���ʱ�����3�����ʱ��",
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

            // ID11: ������Ƥ - ָ����͵ȡ����
            allCards.Add(new CardData
            {
                cardId = 11,
                cardName = "������Ƥ",
                description = "��ָ����Ҵ�͵ȡһ�ſ�",
                cardType = CardType.PlayerTarget,
                effectType = EffectType.GetCard,
                targetType = TargetType.SinglePlayer,
                rarity = CardRarity.Uncommon,
                drawWeight = 6f,
                effectValue = -1f, // ��ֵ��ʾ͵ȡ
                canUseWhenNotMyTurn = true,
                cardBackgroundColor = Color.yellow,
                showEffectValue = false
            });

            // ID12: һ�з۱� - �Է��ͻ���ض�����
            allCards.Add(new CardData
            {
                cardId = 12,
                cardName = "һ�з۱�",
                description = "������żӱ���",
                cardType = CardType.SelfTarget,
                effectType = EffectType.GetCard,
                targetType = TargetType.Self,
                rarity = CardRarity.Epic,
                drawWeight = 2f,
                effectValue = 3f, // ����ֵ����ʾ��üӱ���
                canUseWhenNotMyTurn = true,
                cardBackgroundColor = Color.magenta,
                showEffectValue = false
            });

            Debug.Log($"[CardConfig] �Զ�������ɣ���������{allCards.Count}�ſ���");

            // ���Ϊ�����ݣ�ȷ������
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// ����ΪĬ��ϵͳ����
        /// </summary>
        [ContextMenu("����ϵͳ����")]
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

            Debug.Log("[CardConfig] ϵͳ����������ΪĬ��ֵ");

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// ���ٵ�������Ȩ�أ�����ƽ���Ե�����
        /// </summary>
        [ContextMenu("��������Ȩ��")]
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

            Debug.Log("[CardConfig] ����Ȩ���Ѹ���ϡ�ж����µ���");

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        #endregion

        #region Unity�༭��֧��

#if UNITY_EDITOR
        /// <summary>
        /// Inspector�����֤ʱ�Զ����ã���ѡ��
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

        /// <summary>
        /// ��ȡ����ͳ����Ϣ
        /// </summary>
        public string GetCardStatistics()
        {
            var stats = $"=== ����ͳ�� ===\n";
            stats += $"�ܿ�����: {allCards.Count}\n";
            stats += $"�Է���: {GetSelfTargetCards().Count}��\n";
            stats += $"ָ����: {GetPlayerTargetCards().Count}��\n";
            stats += $"��ͨ: {GetCardsByRarity(CardRarity.Common).Count}��\n";
            stats += $"������: {GetCardsByRarity(CardRarity.Uncommon).Count}��\n";
            stats += $"ϡ��: {GetCardsByRarity(CardRarity.Rare).Count}��\n";
            stats += $"ʷʫ: {GetCardsByRarity(CardRarity.Epic).Count}��\n";
            stats += $"��˵: {GetCardsByRarity(CardRarity.Legendary).Count}��\n";

            return stats;
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
            //Debug.Log(GetCardStatistics());
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