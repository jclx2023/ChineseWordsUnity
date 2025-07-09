using System;
using System.Collections.Generic;
using UnityEngine;
using Cards.Core;

namespace Cards.Effects
{
    /// <summary>
    /// 卡牌效果系统（简化版）
    /// 负责效果的注册、执行、验证、目标选择及UI交互处理
    /// 更新版 - 支持新的12种卡牌效果
    /// </summary>
    public class CardEffectSystem : MonoBehaviour
    {
        [Header("系统配置")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private float effectExecutionTimeout = 10f;

        // 效果注册器
        private Dictionary<EffectType, ICardEffect> registeredEffects;

        // 效果执行器
        private CardEffectExecutor effectExecutor;

        // 单例实例
        public static CardEffectSystem Instance { get; private set; }

        #region 生命周期

        private void Awake()
        {
            LogDebug($"{GetType().Name} 单例已创建，等待进一步初始化");
            InitializeSystem();
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
            LogDebug("开始初始化卡牌效果系统");

            // 初始化容器
            registeredEffects = new Dictionary<EffectType, ICardEffect>();
            effectExecutor = new CardEffectExecutor();

            // 配置执行器
            effectExecutor.SetTimeout(effectExecutionTimeout);

            // 注册事件
            RegisterEvents();

            LogDebug("卡牌效果系统初始化完成");
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
        /// 注册效果实例
        /// </summary>
        public void RegisterEffect(EffectType effectType, ICardEffect effect)
        {
            registeredEffects[effectType] = effect;
        }

        /// <summary>
        /// 注销效果实例
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
        /// 获取已注册效果
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

        /// <summary>
        /// 获取所有已注册效果类型
        /// </summary>
        public List<EffectType> GetRegisteredEffectTypes()
        {
            return new List<EffectType>(registeredEffects.Keys);
        }

        #endregion

        #region 效果执行入口

        /// <summary>
        /// 使用卡牌（主要接口）
        /// 目标应通过UI拖拽确定或直接执行效果
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
                    // 自发动卡牌，目标是使用者自己
                    request.targetPlayerId = request.userId;
                    LogDebug($"自发动卡牌，目标设置为使用者: {request.userId}");
                    break;

                case CardType.PlayerTarget:
                    // 指定卡牌，UI层应该已经设置了targetPlayerId
                    if (request.targetPlayerId <= 0)
                    {
                        LogError("指定卡牌缺少有效目标");
                        return;
                    }
                    LogDebug($"指定卡牌，目标玩家: {request.targetPlayerId}");
                    break;

                case CardType.Special:
                    // 特殊卡牌，根据具体效果决定
                    LogDebug("特殊动卡牌，目标由效果逻辑决定");
                    break;
            }
        }

        /// <summary>
        /// 验证卡牌使用请求
        /// </summary>
        private bool ValidateCardUseRequest(CardUseRequest request, CardData cardData)
        {

            // 检查效果是否已注册
            if (!IsEffectRegistered(cardData.effectType))
            {
                LogError($"效果未注册: {cardData.effectType}");
                return false;
            }

            // 验证目标（针对指定动卡牌）
            if (cardData.cardType == CardType.PlayerTarget)
            {
                if (!IsValidTarget(request.targetPlayerId, request.userId, cardData))
                {
                    LogError($"无效的目标玩家: {request.targetPlayerId}");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 验证目标是否有效（更新版，支持新卡牌）
        /// </summary>
        private bool IsValidTarget(int targetId, int userId, CardData cardData)
        {
            if (targetId <= 0)
            {
                LogError("目标ID无效");
                return false;
            }

            // 指定动卡牌不能选择自己作为目标
            if (targetId == userId)
            {
                LogError($"指定动卡牌不能选择自己作为目标：{cardData.cardName}");
                return false;
            }

            // 通过CardGameBridge检查目标玩家是否存活
            if (!Cards.Integration.CardGameBridge.IsPlayerAlive(targetId))
            {
                LogError($"目标玩家{targetId}已死亡或不存在");
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
            LogDebug($"执行卡牌效果: {cardData.cardName} ({cardData.effectType})");

            // 获取效果实例
            var effect = GetEffect(cardData.effectType);
            if (effect == null)
            {
                LogError($"效果实例未找到: {cardData.effectType}");
                return;
            }

            // 前置验证
            if (!effect.CanUse(request, cardData))
            {
                LogWarning($"效果验证失败，无法使用卡牌: {cardData.cardName}");
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
            LogDebug($"卡牌效果执行完成: {cardData.cardName}, 成功: {result.success}, 消息: {result.message}");

            // 触发使用完成事件
            CardEvents.OnCardUsed?.Invoke(request, cardData, result);

            // 显示结果消息
            if (!string.IsNullOrEmpty(result.message))
            {
                CardEvents.OnCardMessage?.Invoke(result.message);
            }

            // 如果成功使用，广播卡牌使用消息（给其他同步）
            if (result.success)
            {
                BroadcastCardUsage(request, cardData);
            }
        }

        /// <summary>
        /// 广播卡牌使用消息
        /// </summary>
        private void BroadcastCardUsage(CardUseRequest request, CardData cardData)
        {
            // 通过CardGameBridge广播卡牌使用消息
            Cards.Integration.CardGameBridge.BroadcastCardUsage(
                request.userId,
                request.cardId,
                request.targetPlayerId,
                cardData.cardName
            );
        }

        #endregion

        #region 卡牌使用事件处理

        /// <summary>
        /// 处理卡牌使用请求事件回调函数
        /// </summary>
        private void HandleCardUseRequest(CardUseRequest request, CardData cardData)
        {
            UseCard(request, cardData);
        }

        #endregion

        /// <summary>
        /// 检查系统是否准备就绪
        /// </summary>
        public bool IsSystemReady()
        {
            return registeredEffects != null && registeredEffects.Count > 0;
        }


        #region 日志工具

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                CardUtilities.LogDebug(message);
            }
        }

        private void LogWarning(string message)
        {
            if (enableDebugLogs)
            {
                CardUtilities.LogError(message);
            }
        }

        private void LogError(string message)
        {
            if (enableDebugLogs)
            {
                CardUtilities.LogError(message);
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
                CardUtilities.LogDebug($"开始执行效果: {cardData.effectType} - {cardData.cardName}");

                // 记录执行开始时间
                float startTime = Time.time;

                // 执行效果
                var result = effect.Execute(request, cardData);

                // 记录执行时间
                float executionTime = Time.time - startTime;
                CardUtilities.LogDebug($"效果执行完成，耗时: {executionTime:F3}秒");

                // 调用完成回调
                onCompleted?.Invoke(request, cardData, result);
            }
            catch (System.Exception e)
            {
                CardUtilities.LogError($"效果执行异常 [{cardData.cardName}]: {e.Message}");
                CardUtilities.LogError($"异常堆栈: {e.StackTrace}");

                var errorResult = new CardEffectResult(false, $"执行失败: {e.Message}");
                onCompleted?.Invoke(request, cardData, errorResult);
            }
        }
    }

    #endregion
}