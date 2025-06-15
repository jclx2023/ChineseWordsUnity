using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cards.Core;

namespace Cards.Core
{
    /// <summary>
    /// 卡牌系统工具类（简化版）
    /// 包含通用工具方法、验证器和日志系统
    /// </summary>
    public static class CardUtilities
    {
        #region 随机和抽卡工具

        /// <summary>
        /// 根据卡牌权重随机抽取卡牌
        /// </summary>
        /// <param name="allCards">所有可用卡牌</param>
        /// <param name="excludeIds">排除的卡牌ID</param>
        /// <returns>抽中的卡牌数据</returns>
        public static CardData DrawRandomCard(List<CardData> allCards, HashSet<int> excludeIds = null)
        {
            if (allCards == null || allCards.Count == 0)
            {
                LogError("抽卡失败：卡牌列表为空");
                return null;
            }

            // 过滤排除的卡牌
            var availableCards = excludeIds == null
                ? allCards
                : allCards.Where(card => !excludeIds.Contains(card.cardId)).ToList();

            if (availableCards.Count == 0)
            {
                LogWarning("抽卡失败：没有可用的卡牌");
                return null;
            }

            // 计算总权重
            float totalWeight = 0f;
            foreach (var card in availableCards)
            {
                totalWeight += card.drawWeight;
            }

            if (totalWeight <= 0)
            {
                LogWarning("抽卡失败：总权重为0，随机选择一张卡牌");
                return availableCards[UnityEngine.Random.Range(0, availableCards.Count)];
            }

            // 随机抽取
            float randomValue = UnityEngine.Random.Range(0f, totalWeight);
            float currentWeight = 0f;

            foreach (var card in availableCards)
            {
                currentWeight += card.drawWeight;
                if (randomValue <= currentWeight)
                {
                    LogDebug($"抽中卡牌: {card.cardName} (权重: {card.drawWeight})");
                    return card;
                }
            }

            // 兜底返回最后一张
            return availableCards.Last();
        }

        /// <summary>
        /// 批量抽卡
        /// </summary>
        /// <param name="allCards">所有可用卡牌</param>
        /// <param name="count">抽卡数量</param>
        /// <param name="allowDuplicates">是否允许重复</param>
        /// <returns>抽中的卡牌ID列表</returns>
        public static List<int> DrawMultipleCards(List<CardData> allCards, int count, bool allowDuplicates = true)
        {
            var result = new List<int>();
            var drawnIds = new HashSet<int>();

            for (int i = 0; i < count; i++)
            {
                var excludeIds = allowDuplicates ? null : drawnIds;
                var drawnCard = DrawRandomCard(allCards, excludeIds);

                if (drawnCard != null)
                {
                    result.Add(drawnCard.cardId);
                    drawnIds.Add(drawnCard.cardId);
                }
                else
                {
                    LogWarning($"第{i + 1}次抽卡失败");
                    break;
                }
            }

            LogDebug($"批量抽卡完成，共抽到{result.Count}张卡牌");
            return result;
        }

        /// <summary>
        /// 打乱列表顺序
        /// </summary>
        public static void Shuffle<T>(List<T> list)
        {
            if (list == null) return;

            for (int i = list.Count - 1; i > 0; i--)
            {
                int randomIndex = UnityEngine.Random.Range(0, i + 1);
                T temp = list[i];
                list[i] = list[randomIndex];
                list[randomIndex] = temp;
            }
        }

        #endregion

        #region 验证器

        /// <summary>
        /// 验证器工具类
        /// </summary>
        public static class Validator
        {
            /// <summary>
            /// 验证卡牌使用请求的基本有效性
            /// </summary>
            public static bool ValidateCardUseRequest(CardUseRequest request, CardData cardData)
            {
                if (request == null)
                {
                    LogError("卡牌使用请求为空");
                    return false;
                }

                if (cardData == null)
                {
                    LogError("卡牌数据为空");
                    return false;
                }

                if (request.userId <= 0)
                {
                    LogError("无效的用户ID");
                    return false;
                }

                if (request.cardId != cardData.cardId)
                {
                    LogError("请求中的卡牌ID与数据不匹配");
                    return false;
                }

                return true;
            }

            /// <summary>
            /// 验证目标选择
            /// </summary>
            public static bool ValidateTargetSelection(CardUseRequest request, CardData cardData, List<int> availablePlayers)
            {
                if (!ValidateCardUseRequest(request, cardData))
                {
                    return false;
                }

                // 检查是否需要选择目标
                if (cardData.NeedsTargetSelection)
                {
                    if (request.targetPlayerId <= 0)
                    {
                        LogError("指向型卡牌需要选择有效目标");
                        return false;
                    }

                    if (availablePlayers != null && !availablePlayers.Contains(request.targetPlayerId))
                    {
                        LogError("选择的目标玩家不在可用列表中");
                        return false;
                    }

                    // 不能选择自己作为目标（除非卡牌明确允许）
                    if (request.targetPlayerId == request.userId && cardData.targetType != TargetType.Self)
                    {
                        LogError("不能选择自己作为目标");
                        return false;
                    }
                }

                return true;
            }

            /// <summary>
            /// 验证玩家是否可以使用该卡牌（简化版）
            /// </summary>
            public static bool ValidatePlayerCanUseCard(int playerId, CardData cardData, PlayerCardState playerState, bool isMyTurn)
            {
                if (cardData == null || playerState == null)
                {
                    LogError("卡牌数据或玩家状态为空");
                    return false;
                }

                // 检查玩家是否拥有该卡牌
                if (!playerState.handCards.Contains(cardData.cardId))
                {
                    LogError("玩家不拥有该卡牌");
                    return false;
                }

                // 检查本轮是否已使用过卡牌
                if (!playerState.canUseCardThisRound)
                {
                    LogError("本轮已使用过卡牌，请等待下一轮");
                    return false;
                }

                // 检查是否在自己回合使用（根据卡牌设置）
                if (isMyTurn && !cardData.canUseWhenNotMyTurn)
                {
                    LogError("该卡牌不能在自己回合时使用");
                    return false;
                }

                return true;
            }

            /// <summary>
            /// 验证游戏状态是否允许使用卡牌（简化版）
            /// </summary>
            public static bool ValidateGameState(bool isGameActive)
            {
                if (!isGameActive)
                {
                    LogError("游戏未激活状态，无法使用卡牌");
                    return false;
                }

                return true;
            }
        }

        #endregion

        #region 回合管理工具

        /// <summary>
        /// 回合管理工具类
        /// </summary>
        public static class RoundManager
        {
            /// <summary>
            /// 重置特定玩家的卡牌使用机会（在该玩家答题完成后调用）
            /// </summary>
            public static void ResetPlayerCardUsage(PlayerCardState playerState)
            {
                if (playerState != null)
                {
                    playerState.ResetForNewRound();
                    LogDebug($"玩家{playerState.playerId}答题完成，卡牌使用机会已重置");
                    CardEvents.OnPlayerCardUsageReset?.Invoke(playerState.playerId);
                }
            }

            /// <summary>
            /// 重置指定ID玩家的卡牌使用机会
            /// </summary>
            public static void ResetPlayerCardUsage(List<PlayerCardState> allPlayers, int playerId)
            {
                var player = allPlayers?.Find(p => p.playerId == playerId);
                if (player != null)
                {
                    ResetPlayerCardUsage(player);
                }
                else
                {
                    LogWarning($"未找到玩家{playerId}，无法重置其卡牌使用机会");
                }
            }

            /// <summary>
            /// 标记玩家本轮已使用卡牌
            /// </summary>
            public static void MarkPlayerUsedCard(PlayerCardState playerState)
            {
                if (playerState != null)
                {
                    playerState.MarkCardUsedThisRound();
                    LogDebug($"玩家{playerState.playerId}本轮卡牌使用机会已用完");
                }
            }

            /// <summary>
            /// 检查玩家是否还能使用卡牌
            /// </summary>
            public static bool CanPlayerUseCard(PlayerCardState playerState)
            {
                return playerState?.canUseCardThisRound ?? false;
            }

            /// <summary>
            /// 获取可使用卡牌的玩家列表
            /// </summary>
            public static List<int> GetPlayersWhoCanUseCards(List<PlayerCardState> allPlayers)
            {
                var result = new List<int>();
                if (allPlayers != null)
                {
                    foreach (var player in allPlayers)
                    {
                        if (CanPlayerUseCard(player))
                        {
                            result.Add(player.playerId);
                        }
                    }
                }
                return result;
            }
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 根据效果类型获取默认效果描述
        /// </summary>
        public static string GetEffectDescription(EffectType effectType, float effectValue)
        {
            return effectType switch
            {
                EffectType.AddTime => $"增加{effectValue}秒答题时间",
                EffectType.ReduceTime => $"减少{effectValue}秒答题时间",
                EffectType.Heal => $"回复{effectValue}点生命值",
                EffectType.Damage => effectValue > 1 ? $"造成{effectValue}倍伤害" : "造成伤害",
                EffectType.SkipQuestion => "跳过下一题",
                EffectType.SpecifyQuestion => "指定题目类型",
                EffectType.ChengYuChain => "下一题为成语接龙",
                EffectType.JudgeQuestion => "下一题为判断题",
                EffectType.GetCard => effectValue > 0 ? $"获得{effectValue}张卡牌" : "移除一张卡牌",
                EffectType.ExtraChance => "获得额外提示",
                EffectType.RandomCard => "随机获得卡牌",
                EffectType.DoubleTime => "双倍时间奖励",
                _ => "未知效果"
            };
        }

        /// <summary>
        /// 根据稀有度获取颜色
        /// </summary>
        public static Color GetRarityColor(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.Common => Color.white,
                CardRarity.Uncommon => Color.green,
                CardRarity.Rare => Color.blue,
                CardRarity.Epic => new Color(0.6f, 0.3f, 0.9f), // 紫色
                CardRarity.Legendary => new Color(1f, 0.6f, 0f), // 橙色
                _ => Color.gray
            };
        }

        /// <summary>
        /// 获取稀有度的中文名称
        /// </summary>
        public static string GetRarityName(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.Common => "普通",
                CardRarity.Uncommon => "不常见",
                CardRarity.Rare => "稀有",
                CardRarity.Epic => "史诗",
                CardRarity.Legendary => "传说",
                _ => "未知"
            };
        }

        /// <summary>
        /// 计算两个向量之间的距离
        /// </summary>
        public static float CalculateDistance(Vector3 from, Vector3 to)
        {
            return Vector3.Distance(from, to);
        }

        /// <summary>
        /// 生成唯一ID
        /// </summary>
        public static string GenerateUniqueId()
        {
            return Guid.NewGuid().ToString("N")[..8]; // 取前8位
        }

        /// <summary>
        /// 安全转换字符串为整数
        /// </summary>
        public static int SafeParseInt(string str, int defaultValue = 0)
        {
            return int.TryParse(str, out int result) ? result : defaultValue;
        }

        /// <summary>
        /// 安全转换字符串为浮点数
        /// </summary>
        public static float SafeParseFloat(string str, float defaultValue = 0f)
        {
            return float.TryParse(str, out float result) ? result : defaultValue;
        }

        /// <summary>
        /// 限制数值在指定范围内
        /// </summary>
        public static T Clamp<T>(T value, T min, T max) where T : IComparable<T>
        {
            if (value.CompareTo(min) < 0) return min;
            if (value.CompareTo(max) > 0) return max;
            return value;
        }

        /// <summary>
        /// 格式化时间显示
        /// </summary>
        public static string FormatTime(float seconds)
        {
            if (seconds < 60)
            {
                return $"{seconds:F1}秒";
            }
            else
            {
                int minutes = Mathf.FloorToInt(seconds / 60);
                int remainingSeconds = Mathf.FloorToInt(seconds % 60);
                return $"{minutes}分{remainingSeconds}秒";
            }
        }

        #endregion

        #region 集合操作工具

        /// <summary>
        /// 安全地从列表中移除元素
        /// </summary>
        public static bool SafeRemove<T>(List<T> list, T item)
        {
            if (list == null || item == null) return false;
            return list.Remove(item);
        }

        /// <summary>
        /// 安全地向列表添加元素（避免重复）
        /// </summary>
        public static bool SafeAdd<T>(List<T> list, T item, bool allowDuplicates = true)
        {
            if (list == null || item == null) return false;

            if (!allowDuplicates && list.Contains(item))
            {
                return false;
            }

            list.Add(item);
            return true;
        }

        /// <summary>
        /// 获取列表中的随机元素
        /// </summary>
        public static T GetRandomElement<T>(List<T> list)
        {
            if (list == null || list.Count == 0) return default(T);
            return list[UnityEngine.Random.Range(0, list.Count)];
        }

        /// <summary>
        /// 安全地获取字典值
        /// </summary>
        public static TValue SafeGetValue<TKey, TValue>(Dictionary<TKey, TValue> dict, TKey key, TValue defaultValue = default(TValue))
        {
            if (dict == null) return defaultValue;
            return dict.ContainsKey(key) ? dict[key] : defaultValue;
        }

        #endregion

        #region 日志系统

        private static bool enableLogs = true;

        /// <summary>
        /// 设置日志开关
        /// </summary>
        public static void SetLogEnabled(bool enabled)
        {
            enableLogs = enabled;
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        public static void LogDebug(string message)
        {
            if (enableLogs)
            {
                Debug.Log($"{CardConstants.LOG_TAG} {message}");
            }
        }

        /// <summary>
        /// 警告日志
        /// </summary>
        public static void LogWarning(string message)
        {
            if (enableLogs)
            {
                Debug.LogWarning($"{CardConstants.LOG_TAG} {message}");
            }
        }

        /// <summary>
        /// 错误日志
        /// </summary>
        public static void LogError(string message)
        {
            if (enableLogs)
            {
                Debug.LogError($"{CardConstants.LOG_TAG} {message}");
            }
        }

        /// <summary>
        /// 带标签的日志
        /// </summary>
        public static void LogWithTag(string tag, string message, LogType logType = LogType.Log)
        {
            if (!enableLogs) return;

            string fullMessage = $"{tag} {message}";
            switch (logType)
            {
                case LogType.Log:
                    Debug.Log(fullMessage);
                    break;
                case LogType.Warning:
                    Debug.LogWarning(fullMessage);
                    break;
                case LogType.Error:
                    Debug.LogError(fullMessage);
                    break;
            }
        }

        /// <summary>
        /// 记录卡牌使用日志
        /// </summary>
        public static void LogCardUse(int playerId, int cardId, string cardName, string result)
        {
            LogWithTag(CardConstants.LOG_TAG_EFFECT,
                $"玩家{playerId}使用卡牌[{cardId}]{cardName} -> {result}");
        }

        /// <summary>
        /// 记录网络事件日志
        /// </summary>
        public static void LogNetworkEvent(string eventName, string details)
        {
            LogWithTag(CardConstants.LOG_TAG_NETWORK,
                $"{eventName}: {details}");
        }

        /// <summary>
        /// 记录UI事件日志
        /// </summary>
        public static void LogUIEvent(string eventName, string details)
        {
            LogWithTag(CardConstants.LOG_TAG_UI,
                $"{eventName}: {details}");
        }

        #endregion

        #region 性能优化工具

        /// <summary>
        /// 对象池简单实现
        /// </summary>
        public static class ObjectPool<T> where T : new()
        {
            private static readonly Stack<T> pool = new Stack<T>();

            public static T Get()
            {
                return pool.Count > 0 ? pool.Pop() : new T();
            }

            public static void Return(T obj)
            {
                if (obj != null && pool.Count < 100) // 限制池大小
                {
                    pool.Push(obj);
                }
            }

            public static void Clear()
            {
                pool.Clear();
            }
        }

        /// <summary>
        /// 延迟执行工具
        /// </summary>
        public static void DelayedCall(float delay, System.Action action)
        {
            if (action == null) return;

            var coroutineObject = new GameObject("DelayedCall");
            var coroutineRunner = coroutineObject.AddComponent<CoroutineRunner>();
            coroutineRunner.StartDelayedCall(delay, action);
        }

        #endregion
    }

    #region 辅助类

    /// <summary>
    /// 协程运行器（用于静态延迟调用）
    /// </summary>
    public class CoroutineRunner : MonoBehaviour
    {
        public void StartDelayedCall(float delay, System.Action action)
        {
            StartCoroutine(DelayedCallCoroutine(delay, action));
        }

        private IEnumerator DelayedCallCoroutine(float delay, System.Action action)
        {
            yield return new WaitForSeconds(delay);
            action?.Invoke();
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 简单的事件调度器
    /// </summary>
    public static class CardEventDispatcher
    {
        private static readonly Dictionary<string, List<System.Action<object>>> eventListeners =
            new Dictionary<string, List<System.Action<object>>>();

        /// <summary>
        /// 订阅事件
        /// </summary>
        public static void Subscribe(string eventName, System.Action<object> listener)
        {
            if (!eventListeners.ContainsKey(eventName))
            {
                eventListeners[eventName] = new List<System.Action<object>>();
            }
            eventListeners[eventName].Add(listener);
        }

        /// <summary>
        /// 取消订阅事件
        /// </summary>
        public static void Unsubscribe(string eventName, System.Action<object> listener)
        {
            if (eventListeners.ContainsKey(eventName))
            {
                eventListeners[eventName].Remove(listener);
            }
        }

        /// <summary>
        /// 触发事件
        /// </summary>
        public static void Dispatch(string eventName, object eventData = null)
        {
            if (eventListeners.ContainsKey(eventName))
            {
                foreach (var listener in eventListeners[eventName])
                {
                    try
                    {
                        listener?.Invoke(eventData);
                    }
                    catch (System.Exception e)
                    {
                        CardUtilities.LogError($"事件处理异常 {eventName}: {e.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 清理所有事件监听器
        /// </summary>
        public static void Clear()
        {
            eventListeners.Clear();
        }
    }

    #endregion
}