﻿using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Core;
using Core.Network;
using Cards.Core;
using Cards.Effects;
using Cards.Player;
using Managers;

namespace Cards.Integration
{
    /// <summary>
    /// 卡牌游戏桥接器
    /// 负责连接卡牌系统与现有游戏系统，实现卡牌效果的具体执行
    /// 不负责效果注册，只提供效果执行的具体实现
    /// 使用事件驱动的初始化方式
    /// </summary>
    public class CardGameBridge : MonoBehaviour
    {
        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 单例实例
        public static CardGameBridge Instance { get; private set; }

        // 初始化状态
        private bool isInitialized = false;
        private bool isWaitingForCoreSystem = true;

        #region 系统引用

        private PlayerHPManager hpManager;
        private TimerManager timerManager;
        private HostGameManager hostGameManager;
        private NetworkManager networkManager;
        private CardConfig cardConfig;
        private CardEffectSystem effectSystem;
        private PlayerCardManager playerCardManager;

        #endregion

        #region 效果状态缓存

        // 持续性效果状态
        private Dictionary<int, float> playerTimeBonuses;      // 玩家ID -> 时间加成
        private Dictionary<int, float> playerTimePenalties;    // 玩家ID -> 时间减成
        private Dictionary<int, bool> playerSkipFlags;         // 玩家ID -> 跳过标记
        private Dictionary<int, string> playerQuestionTypes;   // 玩家ID -> 指定题目类型
        private Dictionary<int, int> playerAnswerDelegates;    // 玩家ID -> 代替答题者ID
        private Dictionary<int, bool> playerExtraHints;        // 玩家ID -> 额外提示标记
        private float globalDamageMultiplier = 1.0f;           // 全局伤害倍数

        #endregion

        #region Unity生命周期

        private void Awake()
        {
            //// 单例模式
            //if (Instance == null)
            //{
            //    Instance = this;
            //    InitializeStateCaches();
            //    LogDebug("CardGameBridge实例已创建");
            //}
            //else
            //{
            //    Destroy(gameObject);
            //}
            LogDebug($"{GetType().Name} 组件已创建，等待单例设置");
        }

        private void Start()
        {
            // 订阅CardSystemManager的核心系统就绪事件
            SubscribeToSystemEvents();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                UnsubscribeFromAllEvents();
                Instance = null;
            }
        }

        #endregion

        #region 事件驱动初始化

        /// <summary>
        /// 订阅系统事件
        /// </summary>
        private void SubscribeToSystemEvents()
        {
            LogDebug("订阅CardSystemManager事件");

            // 订阅核心系统就绪事件
            CardSystemManager.OnCoreSystemReady += OnCoreSystemReady;

            // 订阅系统初始化完成事件
            CardSystemManager.OnSystemInitialized += OnSystemInitialized;
        }

        /// <summary>
        /// 核心系统就绪事件处理
        /// </summary>
        private void OnCoreSystemReady()
        {
            LogDebug("收到核心系统就绪事件，开始初始化桥接器");
            isWaitingForCoreSystem = false;

            // 立即尝试初始化
            Initialize();
        }

        /// <summary>
        /// 系统初始化完成事件处理
        /// </summary>
        private void OnSystemInitialized(bool success)
        {
            if (success)
            {
                LogDebug("CardSystemManager初始化成功");

                // 如果还在等待核心系统，说明事件可能丢失，尝试初始化
                if (isWaitingForCoreSystem)
                {
                    LogDebug("尝试立即初始化（可能错过了核心系统就绪事件）");
                    isWaitingForCoreSystem = false;
                    Initialize();
                }
            }
            else
            {
                LogError("CardSystemManager初始化失败，桥接器无法正常工作");
            }
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化状态缓存
        /// </summary>
        private void InitializeStateCaches()
        {
            playerTimeBonuses = new Dictionary<int, float>();
            playerTimePenalties = new Dictionary<int, float>();
            playerSkipFlags = new Dictionary<int, bool>();
            playerQuestionTypes = new Dictionary<int, string>();
            playerAnswerDelegates = new Dictionary<int, int>();
            playerExtraHints = new Dictionary<int, bool>();
        }

        /// <summary>
        /// 初始化桥接器
        /// </summary>
        public void Initialize()
        {
            if (isInitialized)
            {
                LogDebug("桥接器已初始化，跳过重复初始化");
                return;
            }

            LogDebug("开始初始化CardGameBridge");

            // 获取系统引用
            if (!AcquireSystemReferences())
            {
                LogError("系统引用获取失败，将在5秒后重试");
                // 延迟重试，而不是直接失败
                Invoke(nameof(RetryInitialization), 5.0f);
                return;
            }

            // 验证效果系统是否已初始化
            if (!ValidateEffectSystemReady())
            {
                LogError("效果系统未就绪，将在3秒后重试");
                Invoke(nameof(RetryInitialization), 3.0f);
                return;
            }

            // 订阅游戏事件
            SubscribeToGameEvents();

            isInitialized = true;
            LogDebug("CardGameBridge初始化完成");
        }

        /// <summary>
        /// 重试初始化
        /// </summary>
        private void RetryInitialization()
        {
            LogDebug("重试初始化CardGameBridge");
            Initialize();
        }

        /// <summary>
        /// 获取系统引用
        /// </summary>
        private bool AcquireSystemReferences()
        {
            bool allReferencesAcquired = true;

            // 优先从CardSystemManager获取引用
            var cardSystemManager = CardSystemManager.Instance;
            if (cardSystemManager != null)
            {
                playerCardManager = cardSystemManager.GetPlayerCardManager();
                effectSystem = cardSystemManager.GetEffectSystem();
                cardConfig = cardSystemManager.GetCardConfig();

                LogDebug("从CardSystemManager获取系统引用成功");
            }
            else
            {
                LogWarning("CardSystemManager不可用，尝试直接获取引用");

                // 回退到直接获取
                playerCardManager = PlayerCardManager.Instance;
                effectSystem = CardEffectSystem.Instance;
                cardConfig = Resources.Load<CardConfig>("CardConfig");
            }

            // 验证核心引用
            if (playerCardManager == null)
            {
                LogError("无法获取PlayerCardManager实例");
                allReferencesAcquired = false;
            }

            if (effectSystem == null)
            {
                LogError("无法获取CardEffectSystem实例");
                allReferencesAcquired = false;
            }

            if (cardConfig == null)
            {
                LogError("无法获取CardConfig");
                allReferencesAcquired = false;
            }

            // 获取其他管理器（允许为空，运行时获取）
            AcquireGameManagerReferences();

            LogDebug($"系统引用获取完成 - 核心系统:{allReferencesAcquired}, HP:{hpManager != null}, Timer:{timerManager != null}, Host:{hostGameManager != null}, Network:{networkManager != null}");
            return allReferencesAcquired;
        }

        /// <summary>
        /// 获取游戏管理器引用
        /// </summary>
        private void AcquireGameManagerReferences()
        {
            // PlayerHPManager是普通类，通过HostGameManager获取
            hostGameManager = HostGameManager.Instance;
            if (hostGameManager != null)
            {
                // 通过反射获取HostGameManager的hpManager字段
                var hpManagerField = hostGameManager.GetType().GetField("hpManager",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (hpManagerField != null)
                {
                    hpManager = hpManagerField.GetValue(hostGameManager) as PlayerHPManager;
                }
            }

            timerManager = FindObjectOfType<TimerManager>();
            networkManager = NetworkManager.Instance;
        }

        /// <summary>
        /// 验证效果系统是否已准备就绪
        /// </summary>
        private bool ValidateEffectSystemReady()
        {
            if (effectSystem == null)
            {
                LogError("效果系统实例不存在");
                return false;
            }

            if (!effectSystem.IsSystemReady())
            {
                LogError("效果系统未就绪");
                return false;
            }

            LogDebug("效果系统验证通过");
            return true;
        }

        /// <summary>
        /// 订阅游戏事件
        /// </summary>
        private void SubscribeToGameEvents()
        {
            // 订阅回合完成事件，清理该玩家的临时效果
            if (playerCardManager != null)
            {
                playerCardManager.OnUsageOpportunityReset += OnPlayerTurnCompleted;
            }

            LogDebug("已订阅游戏事件");
        }

        /// <summary>
        /// 取消订阅游戏事件
        /// </summary>
        private void UnsubscribeFromGameEvents()
        {
            if (playerCardManager != null)
            {
                playerCardManager.OnUsageOpportunityReset -= OnPlayerTurnCompleted;
            }

            LogDebug("已取消游戏事件订阅");
        }

        /// <summary>
        /// 取消订阅所有事件
        /// </summary>
        private void UnsubscribeFromAllEvents()
        {
            // 取消系统事件订阅
            CardSystemManager.OnCoreSystemReady -= OnCoreSystemReady;
            CardSystemManager.OnSystemInitialized -= OnSystemInitialized;

            // 取消游戏事件订阅
            UnsubscribeFromGameEvents();

            LogDebug("已取消所有事件订阅");
        }

        #endregion

        #region 系统访问代理 - 供CardEffects使用

        /// <summary>
        /// 修改玩家生命值
        /// </summary>
        public static bool ModifyPlayerHealth(int playerId, int healthChange)
        {
            if (Instance?.hpManager == null)
            {
                Instance?.LogDebug("PlayerHPManager不可用，无法修改生命值");
                return false;
            }

            bool success = false;
            if (healthChange > 0)
            {
                // 回血
                success = Instance.hpManager.HealPlayer((ushort)playerId, healthChange, out int newHealth);
                Instance?.LogDebug($"玩家{playerId}回血{healthChange}点，新血量:{newHealth}，成功:{success}");
            }
            else if (healthChange < 0)
            {
                // 扣血
                success = Instance.hpManager.ApplyDamage((ushort)playerId, out int newHealth, out bool isDead, -healthChange);
                Instance?.LogDebug($"玩家{playerId}扣血{-healthChange}点，新血量:{newHealth}，死亡:{isDead}，成功:{success}");
            }

            return success;
        }

        /// <summary>
        /// 设置玩家时间加成
        /// </summary>
        public static void SetPlayerTimeBonus(int playerId, float bonusTime)
        {
            if (Instance == null) return;

            Instance.playerTimeBonuses[playerId] = bonusTime;
            Instance.LogDebug($"设置玩家{playerId}时间加成:{bonusTime}秒");
        }

        /// <summary>
        /// 设置玩家时间减成
        /// </summary>
        public static void SetPlayerTimePenalty(int playerId, float penaltyTime)
        {
            if (Instance == null) return;

            Instance.playerTimePenalties[playerId] = penaltyTime;
            Instance.LogDebug($"设置玩家{playerId}时间减成:{penaltyTime}秒");
        }

        /// <summary>
        /// 设置玩家跳过标记
        /// </summary>
        public static void SetPlayerSkipFlag(int playerId, bool shouldSkip = true)
        {
            if (Instance == null) return;

            Instance.playerSkipFlags[playerId] = shouldSkip;
            Instance.LogDebug($"设置玩家{playerId}跳过标记:{shouldSkip}");
        }

        /// <summary>
        /// 设置玩家下次题目类型
        /// </summary>
        public static void SetPlayerNextQuestionType(int playerId, string questionType)
        {
            if (Instance == null) return;

            Instance.playerQuestionTypes[playerId] = questionType;
            Instance.LogDebug($"设置玩家{playerId}下次题目类型:{questionType}");
        }

        /// <summary>
        /// 设置答题代理
        /// </summary>
        public static void SetAnswerDelegate(int originalPlayerId, int delegatePlayerId)
        {
            if (Instance == null) return;

            Instance.playerAnswerDelegates[originalPlayerId] = delegatePlayerId;
            Instance.LogDebug($"设置玩家{originalPlayerId}的答题代理为玩家{delegatePlayerId}");
        }

        /// <summary>
        /// 设置玩家额外提示
        /// </summary>
        public static void SetPlayerExtraHint(int playerId, bool hasExtraHint = true)
        {
            if (Instance == null) return;

            Instance.playerExtraHints[playerId] = hasExtraHint;
            Instance.LogDebug($"设置玩家{playerId}额外提示:{hasExtraHint}");
        }

        /// <summary>
        /// 设置全局伤害倍数
        /// </summary>
        public static void SetGlobalDamageMultiplier(float multiplier)
        {
            if (Instance == null) return;

            Instance.globalDamageMultiplier = multiplier;
            Instance.LogDebug($"设置全局伤害倍数:{multiplier}");
        }

        /// <summary>
        /// 给玩家添加卡牌
        /// </summary>
        public static bool GiveCardToPlayer(int playerId, int cardId = 0, int count = 1)
        {
            if (Instance?.playerCardManager == null) return false;

            bool success = true;
            for (int i = 0; i < count; i++)
            {
                if (!Instance.playerCardManager.GiveCardToPlayer(playerId, cardId))
                {
                    success = false;
                    break;
                }
            }

            Instance?.LogDebug($"给玩家{playerId}添加{count}张卡牌(ID:{cardId})，成功:{success}");
            return success;
        }

        /// <summary>
        /// 偷取玩家卡牌
        /// </summary>
        public static int? StealRandomCardFromPlayer(int fromPlayerId, int toPlayerId)
        {
            if (Instance?.playerCardManager == null) return null;

            var fromPlayerHand = Instance.playerCardManager.GetPlayerHand(fromPlayerId);
            if (fromPlayerHand.Count == 0)
            {
                Instance?.LogDebug($"玩家{fromPlayerId}没有可偷取的卡牌");
                return null;
            }

            // 随机选择一张卡牌
            int randomIndex = Random.Range(0, fromPlayerHand.Count);
            int stolenCardId = fromPlayerHand[randomIndex];

            // 执行转移
            bool success = Instance.playerCardManager.TransferCard(fromPlayerId, toPlayerId, stolenCardId);

            if (success)
            {
                Instance?.LogDebug($"成功从玩家{fromPlayerId}偷取卡牌{stolenCardId}到玩家{toPlayerId}");
                return stolenCardId;
            }
            else
            {
                Instance?.LogDebug($"偷取卡牌失败");
                return null;
            }
        }

        #endregion

        #region 效果状态查询 - 供游戏系统使用

        /// <summary>
        /// 获取玩家时间调整（加成-减成）
        /// </summary>
        public float GetPlayerTimeAdjustment(int playerId)
        {
            float bonus = playerTimeBonuses.ContainsKey(playerId) ? playerTimeBonuses[playerId] : 0f;
            float penalty = playerTimePenalties.ContainsKey(playerId) ? playerTimePenalties[playerId] : 0f;
            return bonus - penalty;
        }

        /// <summary>
        /// 检查玩家是否应该跳过
        /// </summary>
        public bool ShouldPlayerSkip(int playerId)
        {
            return playerSkipFlags.ContainsKey(playerId) && playerSkipFlags[playerId];
        }

        /// <summary>
        /// 获取玩家指定的题目类型
        /// </summary>
        public string GetPlayerSpecifiedQuestionType(int playerId)
        {
            return playerQuestionTypes.ContainsKey(playerId) ? playerQuestionTypes[playerId] : null;
        }

        /// <summary>
        /// 获取玩家的答题代理
        /// </summary>
        public int? GetPlayerAnswerDelegate(int playerId)
        {
            return playerAnswerDelegates.ContainsKey(playerId) ? playerAnswerDelegates[playerId] : null;
        }

        /// <summary>
        /// 检查玩家是否有额外提示
        /// </summary>
        public bool HasPlayerExtraHint(int playerId)
        {
            return playerExtraHints.ContainsKey(playerId) && playerExtraHints[playerId];
        }

        /// <summary>
        /// 获取当前伤害倍数
        /// </summary>
        public float GetCurrentDamageMultiplier()
        {
            return globalDamageMultiplier;
        }

        #endregion

        #region 游戏流程集成钩子

        /// <summary>
        /// 在计时器启动前调用，应用时间调整
        /// </summary>
        public void OnTimerStarting(int playerId, ref float timeLimit)
        {
            float adjustment = GetPlayerTimeAdjustment(playerId);
            if (adjustment != 0)
            {
                timeLimit += adjustment;
                LogDebug($"玩家{playerId}时间调整:{adjustment}秒，最终时限:{timeLimit}秒");

                // 清除时间效果（一次性生效）
                playerTimeBonuses.Remove(playerId);
                playerTimePenalties.Remove(playerId);
            }
        }

        /// <summary>
        /// 在答题开始前调用，检查跳过和代理
        /// </summary>
        public bool OnQuestionStarting(int playerId, out int actualAnswerPlayerId)
        {
            actualAnswerPlayerId = playerId;

            // 检查跳过
            if (ShouldPlayerSkip(playerId))
            {
                LogDebug($"玩家{playerId}跳过此题");
                playerSkipFlags.Remove(playerId); // 清除跳过标记
                return false; // 返回false表示跳过
            }

            // 检查答题代理
            var delegatePlayer = GetPlayerAnswerDelegate(playerId);
            if (delegatePlayer.HasValue)
            {
                actualAnswerPlayerId = delegatePlayer.Value;
                LogDebug($"玩家{playerId}的答题代理为玩家{actualAnswerPlayerId}");
                playerAnswerDelegates.Remove(playerId); // 清除代理标记
            }

            return true; // 返回true表示正常答题
        }

        /// <summary>
        /// 在伤害计算前调用，应用伤害倍数
        /// </summary>
        public int OnDamageCalculating(int originalDamage)
        {
            if (globalDamageMultiplier != 1.0f)
            {
                int modifiedDamage = Mathf.RoundToInt(originalDamage * globalDamageMultiplier);
                LogDebug($"伤害倍数应用:{originalDamage} x {globalDamageMultiplier} = {modifiedDamage}");

                // 重置伤害倍数（一次性生效）
                globalDamageMultiplier = 1.0f;
                return modifiedDamage;
            }

            return originalDamage;
        }

        /// <summary>
        /// 在题目生成前调用，检查指定题目类型
        /// </summary>
        public string OnQuestionTypeSelecting(int playerId)
        {
            string specifiedType = GetPlayerSpecifiedQuestionType(playerId);
            if (!string.IsNullOrEmpty(specifiedType))
            {
                LogDebug($"玩家{playerId}指定题目类型:{specifiedType}");
                playerQuestionTypes.Remove(playerId); // 清除指定类型（一次性生效）
                return specifiedType;
            }

            return null; // 返回null表示正常随机选择
        }

        #endregion

        #region 玩家状态查询 - 供CardEffectSystem使用

        /// <summary>
        /// 获取所有存活玩家ID
        /// </summary>
        public static List<int> GetAllAlivePlayerIds()
        {
            if (Instance?.hpManager == null)
            {
                Instance?.LogDebug("PlayerHPManager不可用，返回空列表");
                return new List<int>();
            }

            var alivePlayerIds = Instance.hpManager.GetAlivePlayerIds();
            var result = new List<int>();

            foreach (var playerId in alivePlayerIds)
            {
                result.Add((int)playerId);
            }

            Instance?.LogDebug($"获取到{result.Count}名存活玩家");
            return result;
        }

        /// <summary>
        /// 根据ID获取卡牌数据
        /// </summary>
        public static CardData GetCardDataById(int cardId)
        {
            if (Instance?.cardConfig == null)
            {
                Instance?.LogDebug("CardConfig不可用，无法获取卡牌数据");
                return null;
            }

            var cardData = Instance.cardConfig.GetCardById(cardId);
            Instance?.LogDebug($"获取卡牌数据:{cardId} -> {cardData?.cardName ?? "未找到"}");
            return cardData;
        }

        /// <summary>
        /// 检查玩家是否存活
        /// </summary>
        public static bool IsPlayerAlive(int playerId)
        {
            if (Instance?.hpManager == null) return false;
            return Instance.hpManager.IsPlayerAlive((ushort)playerId);
        }

        /// <summary>
        /// 获取所有玩家ID（包括死亡的）
        /// </summary>
        public static List<int> GetAllPlayerIds()
        {
            if (Instance?.playerCardManager == null) return new List<int>();

            var playerStates = Instance.playerCardManager.GetAllPlayerCardSummaries();
            return playerStates.Keys.ToList();
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 玩家回合完成处理
        /// </summary>
        private void OnPlayerTurnCompleted(int playerId)
        {
            // 清理该玩家的一次性效果（如果有的话）
            // 注意：大部分效果应该在使用时就清理，这里只是保险
            LogDebug($"玩家{playerId}回合完成，清理相关状态");
        }

        #endregion

        #region 网络同步支持

        /// <summary>
        /// 广播卡牌使用消息（供"老师播报"功能使用）
        /// </summary>
        public static void BroadcastCardUsage(int playerId, int cardId, int targetPlayerId, string cardName)
        {
            if (Instance?.networkManager == null) return;

            string message = $"玩家{playerId}使用了{cardName}";
            if (targetPlayerId > 0)
            {
                message += $"，目标是玩家{targetPlayerId}";
            }

            Instance.LogDebug($"广播卡牌使用消息: {message}");
            // TODO: 通过NetworkManager广播卡牌使用消息
            // Instance.networkManager.BroadcastCardUsageMessage(playerId, cardId, targetPlayerId, message);
        }

        #endregion

        #region 清理和重置

        /// <summary>
        /// 清理所有效果状态
        /// </summary>
        public void ClearAllEffectStates()
        {
            playerTimeBonuses.Clear();
            playerTimePenalties.Clear();
            playerSkipFlags.Clear();
            playerQuestionTypes.Clear();
            playerAnswerDelegates.Clear();
            playerExtraHints.Clear();
            globalDamageMultiplier = 1.0f;

            LogDebug("所有效果状态已清理");
        }

        /// <summary>
        /// 清理指定玩家的效果状态
        /// </summary>
        public void ClearPlayerEffectStates(int playerId)
        {
            playerTimeBonuses.Remove(playerId);
            playerTimePenalties.Remove(playerId);
            playerSkipFlags.Remove(playerId);
            playerQuestionTypes.Remove(playerId);
            playerAnswerDelegates.Remove(playerId);
            playerExtraHints.Remove(playerId);

            LogDebug($"玩家{playerId}的效果状态已清理");
        }

        #endregion

        #region 调试和状态查询

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[CardGameBridge] {message}");
            }
        }
        private void LogWarning(string message)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[CardGameBridge] {message}");
            }
        }
        private void LogError(string message)
        {
            Debug.LogError($"[CardGameBridge] {message}");
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 强制刷新系统引用（运行时使用）
        /// </summary>
        public void RefreshSystemReferences()
        {
            LogDebug("刷新系统引用");
            AcquireSystemReferences();
        }

        /// <summary>
        /// 检查桥接器是否准备就绪
        /// </summary>
        public bool IsReady()
        {
            return isInitialized &&
                   playerCardManager != null &&
                   effectSystem != null &&
                   effectSystem.IsSystemReady() &&
                   cardConfig != null;
        }

        #endregion
    }
}