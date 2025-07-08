using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cards.Core
{
    /// <summary>
    /// ����ϵͳ�����л������塢ö�١����ݽṹ�ͽӿ�
    /// </summary>

    #region ö�ٶ���

    /// <summary>
    /// ��������
    /// </summary>
    public enum CardType
    {
        SelfTarget,     // �Է��ͣ�����������Լ�ʹ��
        PlayerTarget,   // ָ���ͣ���Ҫѡ��Ŀ�����
        Special         // �����ͣ������߼�����
    }

    /// <summary>
    /// Ч������
    /// </summary>
    public enum EffectType
    {
        // ʱ�������
        AddTime,        // ��ʱ - ID4: ������
        ReduceTime,     // ��ʱ - ID10: ��ʱ��

        // ����ֵ������
        Heal,           // �����Ѫ - ID1: ţ�̺�
        GroupHeal,      // Ⱥ���Ѫ - ID7: ���ջ��� (����)
        Damage,         // �ӱ��˺� - ID3: �����۱�
        ProbabilityDamage, // �����˺� - ID9: ��ֽ�� (����)

        // ��Ŀ������
        SkipQuestion,   // ������Ŀ - ID2: �����
        ChengYuChain,   // ������� - ID5: �������
        JudgeQuestion,  // �ж��� - ID6: �ж���

        // ���Ʋ�����
        GetCard         // ���Ʋ��� - ID8: ���ⲹϰ, ID11: ������Ƥ, ID12: һ�з۱�
    }

    /// <summary>
    /// Ŀ������
    /// </summary>
    public enum TargetType
    {
        Self,           // �Լ�
        SinglePlayer,   // �����������
        AllPlayers,     // �������
        AllOthers,      // ���Լ�����������
        Random          // ������
    }

    /// <summary>
    /// ����ϡ�жȣ���������UI��ʾ����ѡ��
    /// </summary>
    public enum CardRarity
    {
        Common,         // ��ͨ
        Uncommon,       // ������
        Rare,           // ϡ��
        Epic,           // ʷʫ
        Legendary       // ��˵
    }

    /// <summary>
    /// ����ʹ��״̬
    /// </summary>
    public enum CardUseResult
    {
        Success,        // �ɹ�ʹ��
        Failed,         // ʹ��ʧ��
        InvalidTarget,  // ��ЧĿ��
        NotEnoughResource, // ��Դ����
        Canceled        // ȡ��ʹ��
    }

    #endregion

    #region ���ݽṹ

    /// <summary>
    /// ���ƻ�������
    /// </summary>
    [Serializable]
    public class CardData
    {
        [Header("������Ϣ")]
        public int cardId;
        public string cardName;
        public string description;
        public Sprite cardIcon;

        [Header("��������")]
        public CardType cardType;
        public EffectType effectType;
        public TargetType targetType;
        public CardRarity rarity = CardRarity.Common;  // ��ѡ����Ҫ����UI��ʾ

        [Header("�鿨����")]
        [Range(0f, 100f)]
        public float drawWeight = 10f;          // �鿨Ȩ��/����

        [Header("Ч������")]
        public float effectValue;           // Ч����ֵ�����ʱ5�롢��Ѫ2�㣩

        [Header("ʹ������")]
        public bool canUseWhenNotMyTurn = true;  // �Ƿ�����ڲ����Լ��غ�ʱʹ��

        [Header("UI��ʾ")]
        public Color cardBackgroundColor = Color.white;
        public bool showEffectValue = true; // �Ƿ���UI����ʾЧ����ֵ

        /// <summary>
        /// ���캯��
        /// </summary>
        public CardData()
        {
            cardId = 0;
            cardName = "";
            description = "";
            cardIcon = null;
            cardType = CardType.SelfTarget;
            effectType = EffectType.Heal;
            targetType = TargetType.Self;
            rarity = CardRarity.Common;
            drawWeight = 10f;
            effectValue = 1f;
            canUseWhenNotMyTurn = true;
            cardBackgroundColor = Color.white;
            showEffectValue = true;
        }

        /// <summary>
        /// �Ƿ�Ϊָ���Ϳ���
        /// </summary>
        public bool IsTargetCard => cardType == CardType.PlayerTarget;

        /// <summary>
        /// �Ƿ���Ҫѡ��Ŀ��
        /// </summary>
        public bool NeedsTargetSelection => targetType == TargetType.SinglePlayer;

        /// <summary>
        /// ��ȡ��ʾ�ı�
        /// </summary>
        public string GetDisplayText()
        {
            if (showEffectValue && effectValue > 0)
            {
                return $"{cardName} ({effectValue})";
            }
            return cardName;
        }
    }

    /// <summary>
    /// ��ҿ���״̬���򻯰棩
    /// </summary>
    [Serializable]
    public class PlayerCardState
    {
        public int playerId;
        public string playerName;
        public List<int> handCards;         // ����ID�б�
        public bool canUseCardThisRound;    // �����Ƿ����ʹ�ÿ���

        public PlayerCardState()
        {
            playerId = 0;
            playerName = "";
            handCards = new List<int>();
            canUseCardThisRound = true; // ��Ϸ��ʼʱ����ʹ��
        }

        /// <summary>
        /// �Ƿ������Ӹ��࿨��
        /// </summary>
        public bool CanAddCard => handCards.Count < CardConstants.DEFAULT_MAX_HAND_SIZE;

        /// <summary>
        /// ʹ�ÿ��ƺ��Ǳ�����ʹ��
        /// </summary>
        public void MarkCardUsedThisRound()
        {
            canUseCardThisRound = false;
        }

        /// <summary>
        /// �»غ�����ʹ�û���
        /// </summary>
        public void ResetForNewRound()
        {
            canUseCardThisRound = true;
        }
    }

    /// <summary>
    /// ����ʹ������
    /// </summary>
    [Serializable]
    public class CardUseRequest
    {
        public int userId;              // ʹ����ID
        public int cardId;              // ����ID
        public int targetPlayerId;      // Ŀ�����ID�������Ҫ��
        public Vector3 usePosition;     // ʹ��λ�ã�UI��أ�
        public float timestamp;         // ʱ���

        public CardUseRequest()
        {
            userId = 0;
            cardId = 0;
            targetPlayerId = -1;
            usePosition = Vector3.zero;
            timestamp = 0f;
        }
    }

    /// <summary>
    /// ����Ч�����
    /// </summary>
    [Serializable]
    public class CardEffectResult
    {
        public bool success;
        public string message;
        public float resultValue;       // �����ֵ
        public Dictionary<string, object> additionalData; // ��������

        public CardEffectResult()
        {
            success = false;
            message = "";
            resultValue = 0f;
            additionalData = new Dictionary<string, object>();
        }

        public CardEffectResult(bool success, string message = "", float resultValue = 0f)
        {
            this.success = success;
            this.message = message;
            this.resultValue = resultValue;
            this.additionalData = new Dictionary<string, object>();
        }
    }

    #endregion

    #region �ӿڶ���

    /// <summary>
    /// ����Ч���ӿ�
    /// </summary>
    public interface ICardEffect
    {
        /// <summary>
        /// ִ��Ч��
        /// </summary>
        CardEffectResult Execute(CardUseRequest request, CardData cardData);

        /// <summary>
        /// ��֤�Ƿ����ʹ��
        /// </summary>
        bool CanUse(CardUseRequest request, CardData cardData);

        /// <summary>
        /// ��ȡЧ������
        /// </summary>
        string GetDescription(CardData cardData);
    }

    /// <summary>
    /// ����Ŀ��ѡ�����ӿ�
    /// </summary>
    public interface ITargetSelector
    {
        /// <summary>
        /// ѡ��Ŀ��
        /// </summary>
        List<int> SelectTargets(CardUseRequest request, CardData cardData);

        /// <summary>
        /// ��֤Ŀ���Ƿ���Ч
        /// </summary>
        bool IsValidTarget(int targetId, CardUseRequest request, CardData cardData);
    }

    /// <summary>
    /// �����¼��������ӿ�
    /// </summary>
    public interface ICardEventListener
    {
        /// <summary>
        /// ����ʹ��ǰ
        /// </summary>
        void OnCardBeforeUse(CardUseRequest request, CardData cardData);

        /// <summary>
        /// ����ʹ�ú�
        /// </summary>
        void OnCardAfterUse(CardUseRequest request, CardData cardData, CardEffectResult result);

        /// <summary>
        /// ���ƻ��
        /// </summary>
        void OnCardReceived(int playerId, int cardId);

        /// <summary>
        /// �����Ƴ�
        /// </summary>
        void OnCardRemoved(int playerId, int cardId);
    }

    #endregion

    #region ��������

    /// <summary>
    /// ����ϵͳ����
    /// </summary>
    public static class CardConstants
    {
        // Ĭ��ֵ
        public const int DEFAULT_MAX_HAND_SIZE = 5;
        public const int DEFAULT_STARTING_CARDS = 3;

        // Ч������
        public const float DEFAULT_HEAL_AMOUNT = 1f;
        public const float DEFAULT_TIME_BONUS = 5f;
        public const float DEFAULT_DAMAGE_AMOUNT = 1f;

        // UI���
        public const float CARD_ANIMATION_DURATION = 0.3f;
        public const float TARGET_SELECTION_TIMEOUT = 10f;

        // ϡ�ж�Ȩ�أ������Ա�UIʹ�ã��鿨��drawWeightΪ׼��
        public static readonly Dictionary<CardRarity, float> RARITY_WEIGHTS = new Dictionary<CardRarity, float>
        {
            { CardRarity.Common, 50f },
            { CardRarity.Uncommon, 30f },
            { CardRarity.Rare, 15f },
            { CardRarity.Epic, 4f },
            { CardRarity.Legendary, 1f }
        };

        // ��־��ǩ
        public const string LOG_TAG = "[CardSystemManager]";
        public const string LOG_TAG_EFFECT = "[CardEffect]";
        public const string LOG_TAG_UI = "[CardUI]";
        public const string LOG_TAG_NETWORK = "[CardNetwork]";
    }

    #endregion

    #region �¼�����

    /// <summary>
    /// ����ϵͳ�¼�
    /// </summary>
    public static class CardEvents
    {
        // ϵͳ�¼�
        public static System.Action OnSystemInitialized;
        public static System.Action OnSystemShutdown;

        // ����ʹ���¼�
        public static System.Action<CardUseRequest, CardData> OnCardUseRequested;
        public static System.Action<CardUseRequest, CardData, CardEffectResult> OnCardUsed;
        public static System.Action<CardUseRequest> OnCardUseCanceled;

        // ��������¼�
        public static System.Action<int, int> OnCardAddedToHand;    // playerId, cardId
        public static System.Action<int, int> OnCardRemovedFromHand; // playerId, cardId

        // �غ��¼�
        public static System.Action<int> OnPlayerCardUsageReset;        // �ض���ҿ���ʹ�û�������

        // UI�¼�
        public static System.Action<int, bool> OnCardHighlighted;   // cardId, highlighted
        public static System.Action OnHandUIRefresh;
        public static System.Action<string> OnCardMessage;         // message to display
    }

    #endregion
}