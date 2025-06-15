using System;
using System.Collections.Generic;
using UnityEngine;
using Cards.Core;

namespace Cards.Effects
{
    /// <summary>
    /// 卡牌效果系统核心（简化版）
    /// 负责效果的注册和执行，目标选择由UI层处理
    /// </summary>
    public class CardEffectSystem : MonoBehaviour
    {
        [Header("系统设置")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private float effectExecutionTimeout = 10f;

        // 效果注册表
        private Dictionary<EffectType, ICardEffect> registeredEffects;

        // 效果执行器
        private CardEffectExecutor effectExecutor;

        // 单例实例
        public static CardEffectSystem Instance { get; private set; }

        #region 生命周期

        private void Awake()
        {
            // 单例模式
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeSystem();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                UnregisterEvents();
                Instance = null;
            }
        }

        #endregion

        #region 系统初始化

        /// <summary>
        /// 初始化效果系统
        /// </summary>
        private void InitializeSystem()
        {
            LogDebug("初始化卡牌效果系统");

            // 初始化组件
            registeredEffects = new Dictionary<EffectType, ICardEffect>();
            effectExecutor = new CardEffectExecutor();

            // 配置组件
            effectExecutor.SetTimeout(effectExecutionTimeout);

            // 注册默认效果（将在CardEffects.cs中实现）
            RegisterDefaultEffects();

            // 注册事件
            RegisterEvents();

            LogDebug("卡牌效果系统初始化完成");
        }

        /// <summary>
        /// 注册默认效果
        /// </summary>
        private void RegisterDefaultEffects()
        {
            // 这里注册所有默认效果
            // 具体效果实现将在CardEffects.cs中定义
            LogDebug("注册默认效果（待CardEffects.cs实现）");
        }

        /// <summary>
        /// 注册事件
        /// </summary>
        private void RegisterEvents()
        {
            CardEvents.OnCardUseRequested += HandleCardUseRequest;
        }

        /// <summary>
        /// 注销事件
        /// </summary>
        private void UnregisterEvents()
        {
            CardEvents.OnCardUseRequested -= HandleCardUseRequest;
        }

        #endregion

        #region 效果注册管理

        /// <summary>
        /// 注册效果实现
        /// </summary>
        public void RegisterEffect(EffectType effectType, ICardEffect effect)
        {
            if (effect == null)
            {
                LogError($"尝试注册空效果: {effectType}");
                return;
            }

            if (registeredEffects.ContainsKey(effectType))
            {
                LogWarning($"效果 {effectType} 已存在，将被覆盖");
            }

            registeredEffects[effectType] = effect;
            LogDebug($"效果已注册: {effectType}");
        }

        /// <summary>
        /// 注销效果实现
        /// </summary>
        public void UnregisterEffect(EffectType effectType)
        {
            if (registeredEffects.Remove(effectType))
            {
                LogDebug($"效果已注销: {effectType}");
            }
            else
            {
                LogWarning($"尝试注销不存在的效果: {effectType}");
            }
        }

        /// <summary>
        /// 获取已注册的效果
        /// </summary>
        public ICardEffect GetEffect(EffectType effectType)
        {
            registeredEffects.TryGetValue(effectType, out ICardEffect effect);
            return effect;
        }

        /// <summary>
        /// 检查效果是否已注册
        /// </summary>
        public bool IsEffectRegistered(EffectType effectType)
        {
            return registeredEffects.ContainsKey(effectType);
        }

        #endregion

        #region 效果执行入口

        /// <summary>
        /// 使用卡牌（主要入口）
        /// 目标已通过UI拖拽确定，直接执行效果
        /// </summary>
        public void UseCard(CardUseRequest request, CardData cardData)
        {
            if (!ValidateCardUseRequest(request, cardData))
            {
                return;
            }

            LogDebug($"开始使用卡牌: {cardData.cardName} (玩家{request.userId})");

            // 根据卡牌类型设置目标
            SetupCardTarget(request, cardData);

            // 直接执行效果
            ExecuteCardEffect(request, cardData);
        }

        /// <summary>
        /// 设置卡牌目标
        /// </summary>
        private void SetupCardTarget(CardUseRequest request, CardData cardData)
        {
            switch (cardData.cardType)
            {
                case CardType.SelfTarget:
                    // 自发型卡牌，目标是使用者自己
                    request.targetPlayerId = request.userId;
                    break;

                case CardType.PlayerTarget:
                    // 指向型卡牌，UI层应该已经设置了targetPlayerId
                    if (request.targetPlayerId <= 0)
                    {
                        LogError("指向型卡牌缺少有效目标");
                        return;
                    }
                    break;

                case CardType.Special:
                    // 特殊型卡牌，根据具体效果处理
                    // 某些特殊卡牌可能不需要目标，或者有特殊的目标逻辑
                    break;
            }

            LogDebug($"卡牌目标设置: {request.targetPlayerId}");
        }

        /// <summary>
        /// 验证卡牌使用请求
        /// </summary>
        private bool ValidateCardUseRequest(CardUseRequest request, CardData cardData)
        {
            // 基础验证
            if (!CardUtilities.Validator.ValidateCardUseRequest(request, cardData))
            {
                return false;
            }

            // 检查效果是否已注册
            if (!IsEffectRegistered(cardData.effectType))
            {
                LogError($"效果未注册: {cardData.effectType}");
                return false;
            }

            // 验证目标（对于指向型卡牌）
            if (cardData.cardType == CardType.PlayerTarget)
            {
                if (!IsValidTarget(request.targetPlayerId, request.userId))
                {
                    LogError($"无效的目标玩家: {request.targetPlayerId}");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 验证目标是否有效
        /// </summary>
        private bool IsValidTarget(int targetId, int userId)
        {
            if (targetId <= 0)
            {
                return false;
            }

            // 检查目标玩家是否存活且在线
            var alivePlayers = GetAllAlivePlayers();
            if (!alivePlayers.Contains(targetId))
            {
                LogError($"目标玩家{targetId}不在存活列表中");
                return false;
            }

            return true;
        }

        #endregion

        #region 效果执行

        /// <summary>
        /// 执行卡牌效果
        /// </summary>
        private void ExecuteCardEffect(CardUseRequest request, CardData cardData)
        {
            LogDebug($"执行卡牌效果: {cardData.cardName}");

            // 获取效果实现
            var effect = GetEffect(cardData.effectType);
            if (effect == null)
            {
                LogError($"效果实现未找到: {cardData.effectType}");
                return;
            }

            // 最终验证
            if (!effect.CanUse(request, cardData))
            {
                LogWarning("效果验证失败，无法使用");
                CardEvents.OnCardMessage?.Invoke("当前无法使用该卡牌");
                return;
            }

            // 执行效果
            effectExecutor.ExecuteEffect(effect, request, cardData, OnEffectCompleted);
        }

        /// <summary>
        /// 效果执行完成回调
        /// </summary>
        private void OnEffectCompleted(CardUseRequest request, CardData cardData, CardEffectResult result)
        {
            LogDebug($"卡牌效果执行完成: {cardData.cardName}, 成功: {result.success}");

            // 记录使用日志
            CardUtilities.LogCardUse(request.userId, request.cardId, cardData.cardName,
                result.success ? "成功" : $"失败:{result.message}");

            // 触发使用完成事件
            CardEvents.OnCardUsed?.Invoke(request, cardData, result);

            // 显示结果消息
            if (!string.IsNullOrEmpty(result.message))
            {
                CardEvents.OnCardMessage?.Invoke(result.message);
            }
        }

        #endregion

        #region 卡牌使用请求处理

        /// <summary>
        /// 处理卡牌使用请求（事件回调）
        /// </summary>
        private void HandleCardUseRequest(CardUseRequest request, CardData cardData)
        {
            UseCard(request, cardData);
        }

        #endregion

        #region 玩家状态查询

        /// <summary>
        /// 获取所有存活玩家
        /// </summary>
        private List<int> GetAllAlivePlayers()
        {
            // 这里需要与游戏系统集成，获取当前存活的玩家列表
            // 暂时返回模拟数据
            var players = new List<int>();

            // TODO: 与PlayerManager或NetworkManager集成
            // 获取真实的玩家列表

            LogDebug($"获取到{players.Count}名存活玩家");
            return players;
        }

        /// <summary>
        /// 检查玩家是否存活
        /// </summary>
        public bool IsPlayerAlive(int playerId)
        {
            return GetAllAlivePlayers().Contains(playerId);
        }

        /// <summary>
        /// 获取所有玩家（包括死亡的）
        /// </summary>
        public List<int> GetAllPlayers()
        {
            // TODO: 与游戏系统集成
            return new List<int>();
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 根据ID获取卡牌数据（用于调试和验证）
        /// </summary>
        public CardData GetCardDataById(int cardId)
        {
            // 这里需要与CardConfig集成获取卡牌数据
            // 暂时返回null，实际实现时需要注入CardConfig引用
            LogDebug($"获取卡牌数据: {cardId}");
            return null; // TODO: 实现真实的卡牌数据获取
        }

        /// <summary>
        /// 获取效果描述
        /// </summary>
        public string GetEffectDescription(EffectType effectType, float effectValue)
        {
            return CardUtilities.GetEffectDescription(effectType, effectValue);
        }

        /// <summary>
        /// 检查系统是否准备就绪
        /// </summary>
        public bool IsSystemReady()
        {
            return registeredEffects != null && registeredEffects.Count > 0;
        }

        #endregion

        #region 日志方法

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                CardUtilities.LogWithTag(CardConstants.LOG_TAG_EFFECT, message);
            }
        }

        private void LogWarning(string message)
        {
            if (enableDebugLogs)
            {
                CardUtilities.LogWithTag(CardConstants.LOG_TAG_EFFECT, message, LogType.Warning);
            }
        }

        private void LogError(string message)
        {
            if (enableDebugLogs)
            {
                CardUtilities.LogWithTag(CardConstants.LOG_TAG_EFFECT, message, LogType.Error);
            }
        }

        #endregion
    }

    #region 效果执行器

    /// <summary>
    /// 效果执行器（简化版）
    /// 负责安全执行卡牌效果
    /// </summary>
    public class CardEffectExecutor
    {
        private float timeout = 10f;

        /// <summary>
        /// 设置超时时间
        /// </summary>
        public void SetTimeout(float timeoutSeconds)
        {
            timeout = timeoutSeconds;
        }

        /// <summary>
        /// 执行效果
        /// </summary>
        public void ExecuteEffect(ICardEffect effect, CardUseRequest request, CardData cardData,
            System.Action<CardUseRequest, CardData, CardEffectResult> onCompleted)
        {
            try
            {
                CardUtilities.LogDebug($"开始执行效果: {cardData.effectType}");

                // 执行效果
                var result = effect.Execute(request, cardData);

                // 调用完成回调
                onCompleted?.Invoke(request, cardData, result);
            }
            catch (System.Exception e)
            {
                CardUtilities.LogError($"效果执行异常: {e.Message}");

                var errorResult = new CardEffectResult(false, $"执行失败: {e.Message}");
                onCompleted?.Invoke(request, cardData, errorResult);
            }
        }
    }

    #endregion
}