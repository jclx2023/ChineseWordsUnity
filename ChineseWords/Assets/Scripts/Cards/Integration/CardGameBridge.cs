using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Core;
using Core.Network;
using Cards.Core;
using Cards.Effects;
using Cards.Player;
using Managers;
using UI;

namespace Cards.Integration
{
    /// <summary>
    /// 卡牌游戏桥接器 - 完全解耦版
    /// 负责连接卡牌系统与现有游戏系统，实现卡牌效果的具体执行
    /// 修复：彻底解耦PUN依赖，使用HostGameManager状态判断，移除单例模式
    /// </summary>
    public class CardGameBridge : MonoBehaviour
    {
        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 静态实例引用（非单例模式）
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

        #region Host端效果状态缓存（只在Host端有效）

        // 持续性效果状态 - 只在Host端维护
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
            // 设置静态引用（由CardSystemManager管理生命周期）
            Instance = this;
            LogDebug($"{GetType().Name} 静态引用已设置，由CardSystemManager管理");
            InitializeStateCaches();
        }

        private void Start()
        {
            // 订阅CardSystemManager的核心系统就绪事件
            SubscribeToSystemEvents();
        }

        private void OnDestroy()
        {
            // 清理静态引用
            if (Instance == this)
            {
                UnsubscribeFromAllEvents();
                Instance = null;
                LogDebug("CardGameBridge静态引用已清理");
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
                cardConfig = Resources.Load<CardConfig>("QuestionConfigs/CardConfig");
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

        #region 网络化的系统访问代理 - 供CardEffects使用

        /// <summary>
        /// 检查当前端是否为Host端（通过HostGameManager是否启用判断）
        /// </summary>
        private static bool IsHostSide()
        {
            var hostManager = HostGameManager.Instance;
            return hostManager != null && hostManager.enabled && hostManager.IsInitialized;
        }

        /// <summary>
        /// 修改玩家生命值 - 网络同步版本（解耦版）
        /// </summary>
        public static bool ModifyPlayerHealth(int playerId, int healthChange)
        {
            // 如果是Host端且可以直接执行，直接执行
            if (IsHostSide() && Instance?.hpManager != null)
            {
                Instance?.LogDebug($"Host端直接执行血量修改: 玩家{playerId}, 变化{healthChange}");
                return ExecuteHealthChangeLocally(playerId, healthChange);
            }

            // 如果是客户端或无法直接执行，通过网络请求Host执行
            if (NetworkManager.Instance != null)
            {
                Instance?.LogDebug($"通过网络请求血量修改: 玩家{playerId}, 变化{healthChange}");
                string reason = healthChange > 0 ? "卡牌治疗效果" : "卡牌伤害效果";
                NetworkManager.Instance.RequestPlayerHealthChange(playerId, healthChange, reason);
                return true; // 假设成功，实际结果通过网络同步回来
            }

            Instance?.LogDebug("无法修改玩家血量 - 既无法直接执行也无法发送网络请求");
            return false;
        }

        /// <summary>
        /// 在Host端执行血量修改
        /// </summary>
        private static bool ExecuteHealthChangeLocally(int playerId, int healthChange)
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
        /// 设置玩家时间加成 - 网络化修复版（解耦版）
        /// </summary>
        public static void SetPlayerTimeBonus(int playerId, float bonusTime)
        {
            // Host端直接设置
            if (IsHostSide() && Instance != null)
            {
                Instance.playerTimeBonuses[playerId] = bonusTime;
                Instance.LogDebug($"Host端直接设置玩家{playerId}时间加成:{bonusTime}秒");
                return;
            }

            // Client端通过网络请求
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.RequestSetPlayerTimeBonus(playerId, bonusTime);
                Instance?.LogDebug($"请求设置玩家{playerId}时间加成:{bonusTime}秒");
            }
            else
            {
                Instance?.LogDebug("NetworkManager不可用，无法设置时间加成");
            }
        }

        /// <summary>
        /// 设置玩家时间减成 - 网络化修复版（解耦版）
        /// </summary>
        public static void SetPlayerTimePenalty(int playerId, float penaltyTime)
        {
            // Host端直接设置
            if (IsHostSide() && Instance != null)
            {
                Instance.playerTimePenalties[playerId] = penaltyTime;
                Instance.LogDebug($"Host端直接设置玩家{playerId}时间减成:{penaltyTime}秒");
                return;
            }

            // Client端通过网络请求
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.RequestSetPlayerTimePenalty(playerId, penaltyTime);
                Instance?.LogDebug($"请求设置玩家{playerId}时间减成:{penaltyTime}秒");
            }
            else
            {
                Instance?.LogDebug("NetworkManager不可用，无法设置时间减成");
            }
        }

        /// <summary>
        /// 设置玩家跳过标记 - 网络化修复版（解耦版）
        /// </summary>
        public static void SetPlayerSkipFlag(int playerId, bool shouldSkip = true)
        {
            // Host端直接设置
            if (IsHostSide() && Instance != null)
            {
                Instance.playerSkipFlags[playerId] = shouldSkip;
                Instance.LogDebug($"Host端直接设置玩家{playerId}跳过标记:{shouldSkip}");
                return;
            }

            // Client端通过网络请求
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.RequestSetPlayerSkipFlag(playerId, shouldSkip);
                Instance?.LogDebug($"请求设置玩家{playerId}跳过标记:{shouldSkip}");
            }
            else
            {
                Instance?.LogDebug("NetworkManager不可用，无法设置跳过标记");
            }
        }

        /// <summary>
        /// 设置玩家下次题目类型 - 网络化修复版（解耦版）
        /// </summary>
        public static void SetPlayerNextQuestionType(int playerId, string questionType)
        {
            // Host端直接设置
            if (IsHostSide() && Instance != null)
            {
                Instance.playerQuestionTypes[playerId] = questionType;
                Instance.LogDebug($"Host端直接设置玩家{playerId}下次题目类型:{questionType}");
                return;
            }

            // Client端通过网络请求
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.RequestSetPlayerNextQuestionType(playerId, questionType);
                Instance?.LogDebug($"请求设置玩家{playerId}下次题目类型:{questionType}");
            }
            else
            {
                Instance?.LogDebug("NetworkManager不可用，无法设置题目类型");
            }
        }

        /// <summary>
        /// 设置全局伤害倍数 - 网络化修复版（解耦版）
        /// </summary>
        public static void SetGlobalDamageMultiplier(float multiplier)
        {
            // Host端直接设置
            if (IsHostSide() && Instance != null)
            {
                Instance.globalDamageMultiplier = multiplier;
                Instance.LogDebug($"Host端直接设置全局伤害倍数:{multiplier}");
                return;
            }

            // Client端通过网络请求
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.RequestSetGlobalDamageMultiplier(multiplier);
                Instance?.LogDebug($"请求设置全局伤害倍数:{multiplier}");
            }
            else
            {
                Instance?.LogDebug("NetworkManager不可用，无法设置伤害倍数");
            }
        }

        /// <summary>
        /// 给玩家添加卡牌 - 网络化修复版（解耦版）
        /// </summary>
        public static bool GiveCardToPlayer(int playerId, int cardId = 0, int count = 1)
        {
            // Host端直接执行
            if (IsHostSide() && Instance?.playerCardManager != null)
            {
                bool success = true;
                for (int i = 0; i < count; i++)
                {
                    if (!Instance.playerCardManager.GiveCardToPlayer(playerId, cardId))
                    {
                        success = false;
                        break;
                    }
                }
                Instance.LogDebug($"Host端直接给玩家{playerId}添加{count}张卡牌(ID:{cardId})，成功:{success}");
                return success;
            }

            // Client端通过网络请求
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.RequestGiveCardToPlayer(playerId, cardId, count);
                Instance?.LogDebug($"请求给玩家{playerId}添加{count}张卡牌(ID:{cardId})");
                return true; // 假设成功，实际结果通过网络同步回来
            }
            else
            {
                Instance?.LogDebug("NetworkManager不可用，无法添加卡牌");
                return false;
            }
        }

        /// <summary>
        /// 偷取玩家卡牌 - 网络化修复版（解耦版）
        /// </summary>
        public static int? StealRandomCardFromPlayer(int fromPlayerId, int toPlayerId)
        {
            // Host端直接执行
            if (IsHostSide() && Instance?.playerCardManager != null)
            {
                var fromPlayerHand = Instance.playerCardManager.GetPlayerHand(fromPlayerId);
                if (fromPlayerHand.Count == 0)
                {
                    Instance.LogDebug($"Host端：玩家{fromPlayerId}没有可偷取的卡牌");
                    return null;
                }

                // 随机选择一张卡牌
                int randomIndex = Random.Range(0, fromPlayerHand.Count);
                int stolenCardId = fromPlayerHand[randomIndex];

                // 执行转移
                bool success = Instance.playerCardManager.TransferCard(fromPlayerId, toPlayerId, stolenCardId);

                if (success)
                {
                    Instance.LogDebug($"Host端成功从玩家{fromPlayerId}偷取卡牌{stolenCardId}到玩家{toPlayerId}");
                    return stolenCardId;
                }
                else
                {
                    Instance.LogDebug($"Host端偷取卡牌失败");
                    return null;
                }
            }

            // Client端通过网络请求
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.RequestStealRandomCard(fromPlayerId, toPlayerId);
                Instance?.LogDebug($"请求从玩家{fromPlayerId}偷取卡牌到玩家{toPlayerId}");
                return 0; // 返回占位值，实际结果通过网络同步回来
            }
            else
            {
                Instance?.LogDebug("NetworkManager不可用，无法偷取卡牌");
                return null;
            }
        }

        #endregion

        #region Host端RPC接收方法 - 处理来自Client的请求（简化版）

        /// <summary>
        /// Host端接收并执行时间加成设置
        /// </summary>
        public void OnSetPlayerTimeBonusReceived(int playerId, float bonusTime)
        {
            if (!IsHostSide()) return;

            playerTimeBonuses[playerId] = bonusTime;
            LogDebug($"Host端通过RPC设置玩家{playerId}时间加成:{bonusTime}秒");
        }

        /// <summary>
        /// Host端接收并执行时间减成设置
        /// </summary>
        public void OnSetPlayerTimePenaltyReceived(int playerId, float penaltyTime)
        {
            if (!IsHostSide()) return;

            playerTimePenalties[playerId] = penaltyTime;
            LogDebug($"Host端通过RPC设置玩家{playerId}时间减成:{penaltyTime}秒");
        }

        /// <summary>
        /// Host端接收并执行跳过标记设置
        /// </summary>
        public void OnSetPlayerSkipFlagReceived(int playerId, bool shouldSkip)
        {
            if (!IsHostSide()) return;

            playerSkipFlags[playerId] = shouldSkip;
            LogDebug($"Host端通过RPC设置玩家{playerId}跳过标记:{shouldSkip}");
        }

        /// <summary>
        /// Host端接收并执行题目类型设置
        /// </summary>
        public void OnSetPlayerNextQuestionTypeReceived(int playerId, string questionType)
        {
            if (!IsHostSide()) return;

            playerQuestionTypes[playerId] = questionType;
            LogDebug($"Host端通过RPC设置玩家{playerId}下次题目类型:{questionType}");
        }

        /// <summary>
        /// Host端接收并执行全局伤害倍数设置
        /// </summary>
        public void OnSetGlobalDamageMultiplierReceived(float multiplier)
        {
            if (!IsHostSide()) return;

            globalDamageMultiplier = multiplier;
            LogDebug($"Host端通过RPC设置全局伤害倍数:{multiplier}");
        }

        /// <summary>
        /// Host端接收并执行给卡牌请求
        /// </summary>
        public void OnGiveCardToPlayerReceived(int playerId, int cardId, int count)
        {
            if (!IsHostSide() || playerCardManager == null) return;

            bool success = true;
            for (int i = 0; i < count; i++)
            {
                if (!playerCardManager.GiveCardToPlayer(playerId, cardId))
                {
                    success = false;
                    break;
                }
            }

            LogDebug($"Host端通过RPC给玩家{playerId}添加{count}张卡牌(ID:{cardId})，成功:{success}");
        }

        /// <summary>
        /// Host端接收并执行偷取卡牌请求
        /// </summary>
        public void OnStealRandomCardReceived(int fromPlayerId, int toPlayerId)
        {
            if (!IsHostSide() || playerCardManager == null) return;

            var fromPlayerHand = playerCardManager.GetPlayerHand(fromPlayerId);
            if (fromPlayerHand.Count == 0)
            {
                LogDebug($"Host端通过RPC：玩家{fromPlayerId}没有可偷取的卡牌");
                return;
            }

            // 随机选择一张卡牌
            int randomIndex = Random.Range(0, fromPlayerHand.Count);
            int stolenCardId = fromPlayerHand[randomIndex];

            // 执行转移
            bool success = playerCardManager.TransferCard(fromPlayerId, toPlayerId, stolenCardId);

            if (success)
            {
                LogDebug($"Host端通过RPC成功从玩家{fromPlayerId}偷取卡牌{stolenCardId}到玩家{toPlayerId}");
            }
            else
            {
                LogDebug($"Host端通过RPC偷取卡牌失败");
            }
        }

        #endregion

        #region 效果状态查询 - 供Host端游戏系统使用（保持不变）

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
        /// 获取当前伤害倍数
        /// </summary>
        public float GetCurrentDamageMultiplier()
        {
            return globalDamageMultiplier;
        }

        #endregion

        #region 游戏流程集成钩子 - Host端使用（保持不变）

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

        #region 玩家状态查询 - 供CardEffectSystem使用（修复版）

        /// <summary>
        /// 获取所有存活玩家ID - 修复版（使用NetworkUI）
        /// </summary>
        public static List<int> GetAllAlivePlayerIds()
        {
            var result = new List<int>();

            // 方案1：通过NetworkUI + NetworkManager组合获取（适用于所有端）
            var networkUI = FindObjectOfType<NetworkUI>();
            var networkManager = NetworkManager.Instance;

            if (networkUI != null && networkManager != null)
            {
                // 获取所有在线玩家ID
                var allOnlinePlayerIds = networkManager.GetAllOnlinePlayerIds();

                // 过滤出存活的玩家
                foreach (var playerId in allOnlinePlayerIds)
                {
                    if (networkUI.ContainsPlayer(playerId) && networkUI.IsPlayerAlive(playerId))
                    {
                        result.Add((int)playerId);
                    }
                }

                Instance?.LogDebug($"从NetworkUI+NetworkManager获取到{result.Count}名存活玩家");
                return result;
            }

            // 方案2：Host端直接使用hpManager（向后兼容）
            if (Instance?.hpManager != null)
            {
                var alivePlayerIds = Instance.hpManager.GetAlivePlayerIds();
                foreach (var playerId in alivePlayerIds)
                {
                    result.Add((int)playerId);
                }
                Instance?.LogDebug($"从PlayerHPManager获取到{result.Count}名存活玩家");
                return result;
            }

            Instance?.LogDebug("所有方法都失败，返回空列表");
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
        /// 检查玩家是否存活 - 修复版（使用NetworkUI）
        /// </summary>
        public static bool IsPlayerAlive(int playerId)
        {
            ushort playerIdUShort = (ushort)playerId;

            // 方案1：从NetworkUI获取状态（适用于所有端）
            var networkUI = FindObjectOfType<NetworkUI>();
            if (networkUI != null && networkUI.ContainsPlayer(playerIdUShort))
            {
                bool isAlive = networkUI.IsPlayerAlive(playerIdUShort);
                return isAlive;
            }

            // 方案2：回退到Host端的hpManager
            if (Instance?.hpManager != null)
            {
                return Instance.hpManager.IsPlayerAlive(playerIdUShort);
            }

            // 方案3：最后的回退
            return true;
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
    }
}