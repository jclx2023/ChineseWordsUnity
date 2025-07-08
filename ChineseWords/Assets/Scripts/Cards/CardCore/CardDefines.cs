using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cards.Core
{
    /// <summary>
    /// 卡牌系统的所有基础定义、枚举、数据结构和接口
    /// </summary>

    #region 枚举定义

    /// <summary>
    /// 卡牌类型
    /// </summary>
    public enum CardType
    {
        SelfTarget,     // 自发型：玩家主动对自己使用
        PlayerTarget,   // 指向型：需要选择目标玩家
        Special         // 特殊型：特殊逻辑处理
    }

    /// <summary>
    /// 效果类型
    /// </summary>
    public enum EffectType
    {
        // 时间操作类
        AddTime,        // 加时 - ID4: 再想想
        ReduceTime,     // 减时 - ID10: 减时卡

        // 生命值操作类
        Heal,           // 单体回血 - ID1: 牛奶盒
        GroupHeal,      // 群体回血 - ID7: 文艺汇演 (新增)
        Damage,         // 加倍伤害 - ID3: 两根粉笔
        ProbabilityDamage, // 概率伤害 - ID9: 丢纸团 (新增)

        // 题目操作类
        SkipQuestion,   // 跳过题目 - ID2: 请假条
        ChengYuChain,   // 成语接龙 - ID5: 成语接龙
        JudgeQuestion,  // 判断题 - ID6: 判断题

        // 卡牌操作类
        GetCard         // 卡牌操作 - ID8: 课外补习, ID11: 借下橡皮, ID12: 一盒粉笔
    }

    /// <summary>
    /// 目标类型
    /// </summary>
    public enum TargetType
    {
        Self,           // 自己
        SinglePlayer,   // 单个其他玩家
        AllPlayers,     // 所有玩家
        AllOthers,      // 除自己外的所有玩家
        Random          // 随机玩家
    }

    /// <summary>
    /// 卡牌稀有度（保留用于UI显示，可选）
    /// </summary>
    public enum CardRarity
    {
        Common,         // 普通
        Uncommon,       // 不常见
        Rare,           // 稀有
        Epic,           // 史诗
        Legendary       // 传说
    }

    /// <summary>
    /// 卡牌使用状态
    /// </summary>
    public enum CardUseResult
    {
        Success,        // 成功使用
        Failed,         // 使用失败
        InvalidTarget,  // 无效目标
        NotEnoughResource, // 资源不足
        Canceled        // 取消使用
    }

    #endregion

    #region 数据结构

    /// <summary>
    /// 卡牌基础数据
    /// </summary>
    [Serializable]
    public class CardData
    {
        [Header("基本信息")]
        public int cardId;
        public string cardName;
        public string description;
        public Sprite cardIcon;

        [Header("卡牌属性")]
        public CardType cardType;
        public EffectType effectType;
        public TargetType targetType;
        public CardRarity rarity = CardRarity.Common;  // 可选，主要用于UI显示

        [Header("抽卡设置")]
        [Range(0f, 100f)]
        public float drawWeight = 10f;          // 抽卡权重/概率

        [Header("效果参数")]
        public float effectValue;           // 效果数值（如加时5秒、回血2点）

        [Header("使用条件")]
        public bool canUseWhenNotMyTurn = true;  // 是否可以在不是自己回合时使用

        [Header("UI显示")]
        public Color cardBackgroundColor = Color.white;
        public bool showEffectValue = true; // 是否在UI上显示效果数值

        /// <summary>
        /// 构造函数
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
        /// 是否为指向型卡牌
        /// </summary>
        public bool IsTargetCard => cardType == CardType.PlayerTarget;

        /// <summary>
        /// 是否需要选择目标
        /// </summary>
        public bool NeedsTargetSelection => targetType == TargetType.SinglePlayer;

        /// <summary>
        /// 获取显示文本
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
    /// 玩家卡牌状态（简化版）
    /// </summary>
    [Serializable]
    public class PlayerCardState
    {
        public int playerId;
        public string playerName;
        public List<int> handCards;         // 手牌ID列表
        public bool canUseCardThisRound;    // 本轮是否可以使用卡牌

        public PlayerCardState()
        {
            playerId = 0;
            playerName = "";
            handCards = new List<int>();
            canUseCardThisRound = true; // 游戏开始时可以使用
        }

        /// <summary>
        /// 是否可以添加更多卡牌
        /// </summary>
        public bool CanAddCard => handCards.Count < CardConstants.DEFAULT_MAX_HAND_SIZE;

        /// <summary>
        /// 使用卡牌后标记本轮已使用
        /// </summary>
        public void MarkCardUsedThisRound()
        {
            canUseCardThisRound = false;
        }

        /// <summary>
        /// 新回合重置使用机会
        /// </summary>
        public void ResetForNewRound()
        {
            canUseCardThisRound = true;
        }
    }

    /// <summary>
    /// 卡牌使用请求
    /// </summary>
    [Serializable]
    public class CardUseRequest
    {
        public int userId;              // 使用者ID
        public int cardId;              // 卡牌ID
        public int targetPlayerId;      // 目标玩家ID（如果需要）
        public Vector3 usePosition;     // 使用位置（UI相关）
        public float timestamp;         // 时间戳

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
    /// 卡牌效果结果
    /// </summary>
    [Serializable]
    public class CardEffectResult
    {
        public bool success;
        public string message;
        public float resultValue;       // 结果数值
        public Dictionary<string, object> additionalData; // 额外数据

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

    #region 接口定义

    /// <summary>
    /// 卡牌效果接口
    /// </summary>
    public interface ICardEffect
    {
        /// <summary>
        /// 执行效果
        /// </summary>
        CardEffectResult Execute(CardUseRequest request, CardData cardData);

        /// <summary>
        /// 验证是否可以使用
        /// </summary>
        bool CanUse(CardUseRequest request, CardData cardData);

        /// <summary>
        /// 获取效果描述
        /// </summary>
        string GetDescription(CardData cardData);
    }

    /// <summary>
    /// 卡牌目标选择器接口
    /// </summary>
    public interface ITargetSelector
    {
        /// <summary>
        /// 选择目标
        /// </summary>
        List<int> SelectTargets(CardUseRequest request, CardData cardData);

        /// <summary>
        /// 验证目标是否有效
        /// </summary>
        bool IsValidTarget(int targetId, CardUseRequest request, CardData cardData);
    }

    /// <summary>
    /// 卡牌事件监听器接口
    /// </summary>
    public interface ICardEventListener
    {
        /// <summary>
        /// 卡牌使用前
        /// </summary>
        void OnCardBeforeUse(CardUseRequest request, CardData cardData);

        /// <summary>
        /// 卡牌使用后
        /// </summary>
        void OnCardAfterUse(CardUseRequest request, CardData cardData, CardEffectResult result);

        /// <summary>
        /// 卡牌获得
        /// </summary>
        void OnCardReceived(int playerId, int cardId);

        /// <summary>
        /// 卡牌移除
        /// </summary>
        void OnCardRemoved(int playerId, int cardId);
    }

    #endregion

    #region 常量定义

    /// <summary>
    /// 卡牌系统常量
    /// </summary>
    public static class CardConstants
    {
        // 默认值
        public const int DEFAULT_MAX_HAND_SIZE = 5;
        public const int DEFAULT_STARTING_CARDS = 3;

        // 效果参数
        public const float DEFAULT_HEAL_AMOUNT = 1f;
        public const float DEFAULT_TIME_BONUS = 5f;
        public const float DEFAULT_DAMAGE_AMOUNT = 1f;

        // UI相关
        public const float CARD_ANIMATION_DURATION = 0.3f;
        public const float TARGET_SELECTION_TIMEOUT = 10f;

        // 稀有度权重（保留以备UI使用，抽卡以drawWeight为准）
        public static readonly Dictionary<CardRarity, float> RARITY_WEIGHTS = new Dictionary<CardRarity, float>
        {
            { CardRarity.Common, 50f },
            { CardRarity.Uncommon, 30f },
            { CardRarity.Rare, 15f },
            { CardRarity.Epic, 4f },
            { CardRarity.Legendary, 1f }
        };

        // 日志标签
        public const string LOG_TAG = "[CardSystemManager]";
        public const string LOG_TAG_EFFECT = "[CardEffect]";
        public const string LOG_TAG_UI = "[CardUI]";
        public const string LOG_TAG_NETWORK = "[CardNetwork]";
    }

    #endregion

    #region 事件定义

    /// <summary>
    /// 卡牌系统事件
    /// </summary>
    public static class CardEvents
    {
        // 系统事件
        public static System.Action OnSystemInitialized;
        public static System.Action OnSystemShutdown;

        // 卡牌使用事件
        public static System.Action<CardUseRequest, CardData> OnCardUseRequested;
        public static System.Action<CardUseRequest, CardData, CardEffectResult> OnCardUsed;
        public static System.Action<CardUseRequest> OnCardUseCanceled;

        // 玩家手牌事件
        public static System.Action<int, int> OnCardAddedToHand;    // playerId, cardId
        public static System.Action<int, int> OnCardRemovedFromHand; // playerId, cardId

        // 回合事件
        public static System.Action<int> OnPlayerCardUsageReset;        // 特定玩家卡牌使用机会重置

        // UI事件
        public static System.Action<int, bool> OnCardHighlighted;   // cardId, highlighted
        public static System.Action OnHandUIRefresh;
        public static System.Action<string> OnCardMessage;         // message to display
    }

    #endregion
}