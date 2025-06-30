using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using Cards.Core;
using Cards.Effects;
using Cards.Player;
using Cards.Network;
using Cards.Integration;
using Cards.UI;

namespace Cards.Core
{
    /// <summary>
    /// 轻量化卡牌系统管理器
    /// 负责统一管理和协调卡牌相关子系统的初始化和引用
    /// 确保所有子系统按正确顺序初始化，避免重复初始化
    /// </summary>
    public class CardSystemManager : MonoBehaviour
    {
        [Header("系统配置")]
        [SerializeField] public CardConfig cardConfig;
        [SerializeField] private bool autoInitializeOnStart = true;
        [SerializeField] private bool enableDebugLogs = true;

        [Header("初始化设置")]
        [SerializeField] private float initializationDelay = 0.1f;

        // 单例实例
        public static CardSystemManager Instance { get; private set; }

        // 子系统引用
        private CardEffectSystem cardEffectSystem;
        private PlayerCardManager playerCardManager;
        private CardNetworkManager cardNetworkManager;
        private CardGameBridge cardGameBridge;
        private CardUIManager cardUIManager;
        private CardUIComponents cardUIComponents;

        // 系统状态
        private bool isInitialized = false;
        private bool isInitializing = false;

        #region 事件定义

        /// <summary>
        /// 系统初始化完成事件
        /// </summary>
        public static event Action<bool> OnSystemInitialized;

        /// <summary>
        /// 系统错误事件
        /// </summary>
        public static event Action<string> OnSystemError;

        /// <summary>
        /// 核心系统就绪事件（供其他系统监听）
        /// </summary>
        public static event Action OnCoreSystemReady;

        /// <summary>
        /// UI系统就绪事件
        /// </summary>
        public static event Action OnUISystemReady;

        /// <summary>
        /// 玩家数据就绪事件
        /// </summary>
        public static event Action<int> OnPlayerDataReady;

        #endregion

        #region Unity生命周期

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                LogDebug("CardSystemManager实例已创建");
            }
            else
            {
                LogDebug("发现重复的CardSystemManager实例，销毁当前实例");
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            if (autoInitializeOnStart)
            {
                StartCoroutine(DelayedInitialization());
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                UnsubscribeFromEvents();
                Instance = null;
                LogDebug("CardSystemManager已销毁");
            }
        }

        #endregion

        #region 系统初始化

        /// <summary>
        /// 延迟初始化
        /// </summary>
        private IEnumerator DelayedInitialization()
        {
            LogDebug("开始延迟初始化");
            yield return new WaitForSeconds(initializationDelay);

            if (!isInitialized && !isInitializing)
            {
                yield return StartCoroutine(InitializeCardSystem());
            }
        }

        /// <summary>
        /// 初始化卡牌系统（公共接口）
        /// </summary>
        public void Initialize()
        {
            if (isInitialized)
            {
                LogDebug("卡牌系统已初始化");
                return;
            }

            if (isInitializing)
            {
                LogDebug("卡牌系统正在初始化中");
                return;
            }

            StartCoroutine(InitializeCardSystem());
        }

        /// <summary>
        /// 初始化卡牌系统协程
        /// </summary>
        private IEnumerator InitializeCardSystem()
        {
            isInitializing = true;
            LogDebug("开始初始化卡牌系统");

            bool success = true;
            string errorMessage = "";

            // 1. 初始化配置
            success = InitializeConfig(out errorMessage);
            if (!success)
            {
                FinishInitialization(false, errorMessage);
                yield break;
            }

            yield return null;

            // 2. 初始化效果系统（包含效果注册）
            success = InitializeEffectSystem(out errorMessage);
            if (!success)
            {
                FinishInitialization(false, errorMessage);
                yield break;
            }

            yield return null;

            // 3. 初始化玩家管理器
            success = InitializePlayerManager(out errorMessage);
            if (!success)
            {
                FinishInitialization(false, errorMessage);
                yield break;
            }

            yield return null;

            // 4. 初始化网络管理器
            InitializeNetworkManager(); // 网络初始化失败不影响整体

            yield return null;

            // 触发核心系统就绪事件
            LogDebug("核心系统初始化完成，触发就绪事件");
            OnCoreSystemReady?.Invoke();

            yield return null;

            // 5. 初始化UI系统（依赖核心系统）
            success = InitializeUISystem(out errorMessage);
            if (!success)
            {
                LogDebug($"UI系统初始化失败: {errorMessage}");
                // UI初始化失败不影响整体，但要记录警告
            }

            yield return null;

            // 6. 初始化游戏桥接器（必须在效果系统之后）
            success = InitializeGameBridge(out errorMessage);
            if (!success)
            {
                LogDebug($"游戏桥接器初始化失败: {errorMessage}");
                // 桥接器初始化失败不影响整体，但要记录警告
            }

            yield return null;

            // 7. 建立事件连接
            success = EstablishEventConnections(out errorMessage);
            if (!success)
            {
                FinishInitialization(false, errorMessage);
                yield break;
            }

            FinishInitialization(true, "系统初始化成功");
        }

        /// <summary>
        /// 完成初始化
        /// </summary>
        private void FinishInitialization(bool success, string message)
        {
            isInitializing = false;
            isInitialized = success;

            if (success)
            {
                LogDebug(message);
            }
            else
            {
                LogError(message);
                OnSystemError?.Invoke(message);
            }

            OnSystemInitialized?.Invoke(success);
        }

        /// <summary>
        /// 初始化配置
        /// </summary>
        private bool InitializeConfig(out string errorMessage)
        {
            try
            {
                LogDebug("初始化卡牌配置");

                if (cardConfig == null)
                {
                    cardConfig = Resources.Load<CardConfig>("CardConfig");
                    if (cardConfig == null)
                    {
                        errorMessage = "无法加载CardConfig资源";
                        return false;
                    }
                }

                if (!cardConfig.ValidateConfig())
                {
                    errorMessage = "CardConfig验证失败";
                    return false;
                }

                LogDebug($"卡牌配置加载成功: {cardConfig.AllCards.Count}张卡牌");
                errorMessage = "";
                return true;
            }
            catch (Exception e)
            {
                errorMessage = $"配置初始化失败: {e.Message}";
                return false;
            }
        }

        /// <summary>
        /// 初始化效果系统（负责创建实例和注册所有效果）
        /// </summary>
        private bool InitializeEffectSystem(out string errorMessage)
        {
            try
            {
                LogDebug("初始化CardEffectSystem");

                cardEffectSystem = CardEffectSystem.Instance;
                if (cardEffectSystem == null)
                {
                    var effectSystemGO = new GameObject("CardEffectSystem");
                    effectSystemGO.transform.SetParent(transform);
                    cardEffectSystem = effectSystemGO.AddComponent<CardEffectSystem>();
                }

                // 统一在这里注册所有效果，避免重复注册
                LogDebug("注册所有卡牌效果");
                CardEffectRegistrar.RegisterAllEffects(cardEffectSystem);

                if (!cardEffectSystem.IsSystemReady())
                {
                    errorMessage = "CardEffectSystem未正确初始化";
                    return false;
                }

                LogDebug("CardEffectSystem初始化并注册效果完成");
                errorMessage = "";
                return true;
            }
            catch (Exception e)
            {
                errorMessage = $"效果系统初始化失败: {e.Message}";
                return false;
            }
        }

        /// <summary>
        /// 初始化玩家管理器
        /// </summary>
        private bool InitializePlayerManager(out string errorMessage)
        {
            try
            {
                LogDebug("初始化PlayerCardManager");

                playerCardManager = PlayerCardManager.Instance;
                if (playerCardManager == null)
                {
                    var playerManagerGO = new GameObject("PlayerCardManager");
                    playerManagerGO.transform.SetParent(transform);
                    playerCardManager = playerManagerGO.AddComponent<PlayerCardManager>();
                }

                if (!playerCardManager.IsInitialized)
                {
                    playerCardManager.Initialize();
                }

                if (!playerCardManager.IsInitialized)
                {
                    errorMessage = "PlayerCardManager初始化失败";
                    return false;
                }

                LogDebug("PlayerCardManager初始化成功");
                errorMessage = "";
                return true;
            }
            catch (Exception e)
            {
                errorMessage = $"玩家管理器初始化失败: {e.Message}";
                return false;
            }
        }

        /// <summary>
        /// 初始化网络管理器
        /// </summary>
        private void InitializeNetworkManager()
        {
            try
            {
                LogDebug("初始化网络管理器");
                cardNetworkManager = CardNetworkManager.Instance;

                if (cardNetworkManager != null)
                {
                    LogDebug("网络管理器初始化成功");
                }
                else
                {
                    LogDebug("网络管理器不可用");
                }
            }
            catch (Exception e)
            {
                LogDebug($"网络管理器初始化警告: {e.Message}");
            }
        }

        /// <summary>
        /// 初始化UI系统
        /// </summary>
        private bool InitializeUISystem(out string errorMessage)
        {
            try
            {
                LogDebug("初始化UI系统");

                // 初始化CardUIComponents
                cardUIComponents = CardUIComponents.Instance;
                if (cardUIComponents == null)
                {
                    var uiComponentsGO = new GameObject("CardUIComponents");
                    uiComponentsGO.transform.SetParent(transform);
                    cardUIComponents = uiComponentsGO.AddComponent<CardUIComponents>();
                }

                // 初始化CardUIManager
                cardUIManager = FindObjectOfType<CardUIManager>();
                if (cardUIManager == null)
                {
                    LogDebug("未找到现有的CardUIManager，尝试创建新实例");
                    var uiManagerGO = new GameObject("CardUIManager");
                    uiManagerGO.transform.SetParent(transform);
                    cardUIManager = uiManagerGO.AddComponent<CardUIManager>();
                }

                // 等待一帧确保UI组件完全初始化
                StartCoroutine(DelayedUIInitialization());

                LogDebug("UI系统初始化完成");
                errorMessage = "";
                return true;
            }
            catch (Exception e)
            {
                errorMessage = $"UI系统初始化失败: {e.Message}";
                return false;
            }
        }

        /// <summary>
        /// 延迟UI初始化完成处理
        /// </summary>
        private IEnumerator DelayedUIInitialization()
        {
            yield return null; // 等待一帧

            LogDebug("UI系统延迟初始化完成，触发UI就绪事件");
            OnUISystemReady?.Invoke();
        }

        /// <summary>
        /// 初始化游戏桥接器（必须在效果系统初始化后调用）
        /// </summary>
        private bool InitializeGameBridge(out string errorMessage)
        {
            try
            {
                LogDebug("初始化游戏桥接器");

                cardGameBridge = CardGameBridge.Instance;
                if (cardGameBridge == null)
                {
                    var bridgeGO = new GameObject("CardGameBridge");
                    bridgeGO.transform.SetParent(transform);
                    cardGameBridge = bridgeGO.AddComponent<CardGameBridge>();
                }

                if (cardGameBridge != null)
                {
                    // 刷新系统引用，确保桥接器能获取到已初始化的系统
                    cardGameBridge.RefreshSystemReferences();

                    if (cardGameBridge.IsReady())
                    {
                        LogDebug("游戏桥接器初始化成功");
                        errorMessage = "";
                        return true;
                    }
                    else
                    {
                        errorMessage = "游戏桥接器未就绪";
                        return false;
                    }
                }
                else
                {
                    errorMessage = "无法创建游戏桥接器实例";
                    return false;
                }
            }
            catch (Exception e)
            {
                errorMessage = $"游戏桥接器初始化失败: {e.Message}";
                return false;
            }
        }

        /// <summary>
        /// 建立事件连接
        /// </summary>
        private bool EstablishEventConnections(out string errorMessage)
        {
            try
            {
                LogDebug("建立事件连接");

                // 订阅核心事件
                if (playerCardManager != null)
                {
                    playerCardManager.OnCardUsed += HandlePlayerCardUsed;
                    playerCardManager.OnCardAcquired += HandlePlayerCardAcquired;
                    playerCardManager.OnCardTransferred += HandlePlayerCardTransferred;

                    // 订阅手牌变化事件，转发给UI系统
                    playerCardManager.OnHandSizeChanged += HandlePlayerHandChanged;
                }

                // 订阅网络事件（如果可用）
                if (cardNetworkManager != null)
                {
                    CardNetworkManager.OnCardUsed += HandleNetworkCardUsed;
                    CardNetworkManager.OnCardAdded += HandleNetworkCardAdded;
                    CardNetworkManager.OnCardRemoved += HandleNetworkCardRemoved;
                    CardNetworkManager.OnCardTransferred += HandleNetworkCardTransferred;
                    CardNetworkManager.OnCardMessage += HandleNetworkCardMessage;
                }

                LogDebug("事件连接建立完成");
                errorMessage = "";
                return true;
            }
            catch (Exception e)
            {
                errorMessage = $"事件连接建立失败: {e.Message}";
                return false;
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 处理玩家卡牌使用
        /// </summary>
        private void HandlePlayerCardUsed(int playerId, int cardId, int targetPlayerId)
        {
            LogDebug($"玩家{playerId}使用卡牌{cardId}, 目标:{targetPlayerId}");

            // 同步到网络
            if (cardNetworkManager != null && cardNetworkManager.CanSendRPC())
            {
                cardNetworkManager.BroadcastCardUsed((ushort)playerId, cardId, (ushort)targetPlayerId);
            }

            // 通知UI更新
            RequestUIUpdate(playerId);
        }

        /// <summary>
        /// 处理玩家卡牌获得
        /// </summary>
        private void HandlePlayerCardAcquired(int playerId, int cardId, string cardName)
        {
            LogDebug($"玩家{playerId}获得{cardName}");

            // 同步到网络
            if (cardNetworkManager != null && cardNetworkManager.CanSendRPC())
            {
                cardNetworkManager.BroadcastCardAdded((ushort)playerId, cardId);
            }

            // 通知UI更新
            RequestUIUpdate(playerId);
        }

        /// <summary>
        /// 处理卡牌转移
        /// </summary>
        private void HandlePlayerCardTransferred(int fromPlayerId, int toPlayerId, int cardId)
        {
            LogDebug($"卡牌{cardId}从玩家{fromPlayerId}转移到玩家{toPlayerId}");

            // 同步到网络
            if (cardNetworkManager != null && cardNetworkManager.CanSendRPC())
            {
                cardNetworkManager.BroadcastCardTransferred((ushort)fromPlayerId, cardId, (ushort)toPlayerId);
            }

            // 通知相关玩家的UI更新
            RequestUIUpdate(fromPlayerId);
            RequestUIUpdate(toPlayerId);
        }

        /// <summary>
        /// 处理玩家手牌变化
        /// </summary>
        private void HandlePlayerHandChanged(int playerId, int newHandSize)
        {
            LogDebug($"玩家{playerId}手牌数量变化: {newHandSize}");

            // 通知UI更新
            RequestUIUpdate(playerId);
        }

        /// <summary>
        /// 请求UI更新
        /// </summary>
        private void RequestUIUpdate(int playerId)
        {
            if (cardUIManager != null)
            {
                // 延迟一帧执行UI更新，确保数据已完全同步
                StartCoroutine(DelayedUIUpdate(playerId));
            }
        }

        /// <summary>
        /// 延迟UI更新
        /// </summary>
        private IEnumerator DelayedUIUpdate(int playerId)
        {
            yield return null; // 等待一帧

            try
            {
                if (cardUIManager != null)
                {
                    // 触发UI更新（这里需要根据CardUIManager的实际接口调整）
                    cardUIManager.RefreshPlayerCardDisplay(playerId);
                }
            }
            catch (Exception e)
            {
                LogDebug($"UI更新失败: {e.Message}");
            }
        }

        /// <summary>
        /// 处理网络卡牌使用
        /// </summary>
        private void HandleNetworkCardUsed(ushort playerId, int cardId, ushort targetPlayerId, string cardName)
        {
            LogDebug($"网络同步: 玩家{playerId}使用{cardName}");
        }

        /// <summary>
        /// 处理网络卡牌添加
        /// </summary>
        private void HandleNetworkCardAdded(ushort playerId, int cardId)
        {
            LogDebug($"网络同步: 玩家{playerId}获得卡牌{cardId}");
        }

        /// <summary>
        /// 处理网络卡牌移除
        /// </summary>
        private void HandleNetworkCardRemoved(ushort playerId, int cardId)
        {
            LogDebug($"网络同步: 玩家{playerId}失去卡牌{cardId}");
        }

        /// <summary>
        /// 处理网络卡牌转移
        /// </summary>
        private void HandleNetworkCardTransferred(ushort fromPlayerId, int cardId, ushort toPlayerId)
        {
            LogDebug($"网络同步: 卡牌{cardId}从玩家{fromPlayerId}转移到玩家{toPlayerId}");
        }

        /// <summary>
        /// 处理网络卡牌消息
        /// </summary>
        private void HandleNetworkCardMessage(string message, ushort fromPlayerId)
        {
            LogDebug($"网络消息: {message} (来自玩家{fromPlayerId})");
        }

        /// <summary>
        /// 取消订阅所有事件
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (playerCardManager != null)
            {
                playerCardManager.OnCardUsed -= HandlePlayerCardUsed;
                playerCardManager.OnCardAcquired -= HandlePlayerCardAcquired;
                playerCardManager.OnCardTransferred -= HandlePlayerCardTransferred;
                playerCardManager.OnHandSizeChanged -= HandlePlayerHandChanged;
            }

            if (cardNetworkManager != null)
            {
                CardNetworkManager.OnCardUsed -= HandleNetworkCardUsed;
                CardNetworkManager.OnCardAdded -= HandleNetworkCardAdded;
                CardNetworkManager.OnCardRemoved -= HandleNetworkCardRemoved;
                CardNetworkManager.OnCardTransferred -= HandleNetworkCardTransferred;
                CardNetworkManager.OnCardMessage -= HandleNetworkCardMessage;
            }

            LogDebug("已取消所有事件订阅");
        }

        #endregion

        #region 游戏生命周期集成

        /// <summary>
        /// 游戏开始时调用
        /// </summary>
        public void OnGameStarted(List<int> playerIds)
        {
            LogDebug($"游戏开始: {playerIds.Count}名玩家");

            if (!IsSystemReady())
            {
                LogError("系统未就绪，无法开始游戏");
                return;
            }

            try
            {
                foreach (int playerId in playerIds)
                {
                    OnPlayerJoined(playerId, $"Player{playerId}");
                }
                LogDebug("游戏开始处理完成");
            }
            catch (Exception e)
            {
                LogError($"游戏开始处理失败: {e.Message}");
            }
        }

        /// <summary>
        /// 玩家加入时调用
        /// </summary>
        public void OnPlayerJoined(int playerId, string playerName = "")
        {
            LogDebug($"玩家加入: {playerId} ({playerName})");

            try
            {
                if (playerCardManager != null)
                {
                    playerCardManager.InitializePlayer(playerId, playerName);

                    // 触发玩家数据就绪事件
                    OnPlayerDataReady?.Invoke(playerId);

                    // 请求UI更新
                    RequestUIUpdate(playerId);
                }
            }
            catch (Exception e)
            {
                LogError($"玩家{playerId}加入处理失败: {e.Message}");
            }
        }

        /// <summary>
        /// 玩家离开时调用
        /// </summary>
        public void OnPlayerLeft(int playerId)
        {
            LogDebug($"玩家离开: {playerId}");

            try
            {
                if (playerCardManager != null)
                {
                    playerCardManager.RemovePlayer(playerId);
                }

                if (cardGameBridge != null)
                {
                    cardGameBridge.ClearPlayerEffectStates(playerId);
                }

                // 清理UI显示
                if (cardUIManager != null)
                {
                    cardUIManager.ClearPlayerDisplay(playerId);
                }
            }
            catch (Exception e)
            {
                LogError($"玩家{playerId}离开处理失败: {e.Message}");
            }
        }

        /// <summary>
        /// 游戏结束时调用
        /// </summary>
        public void OnGameEnded()
        {
            LogDebug("游戏结束");

            try
            {
                if (playerCardManager != null)
                {
                    playerCardManager.ClearAllPlayers();
                }

                if (cardGameBridge != null)
                {
                    cardGameBridge.ClearAllEffectStates();
                }

                if (cardUIManager != null)
                {
                    cardUIManager.ClearAllDisplays();
                }

                LogDebug("游戏结束处理完成");
            }
            catch (Exception e)
            {
                LogError($"游戏结束处理失败: {e.Message}");
            }
        }

        #endregion

        #region 公共查询接口

        /// <summary>
        /// 检查系统是否就绪
        /// </summary>
        public bool IsSystemReady()
        {
            return isInitialized &&
                   cardConfig != null &&
                   cardEffectSystem != null &&
                   cardEffectSystem.IsSystemReady() &&
                   playerCardManager != null &&
                   playerCardManager.IsInitialized;
        }

        /// <summary>
        /// 检查网络是否就绪
        /// </summary>
        public bool IsNetworkReady()
        {
            return cardNetworkManager != null && cardNetworkManager.CanSendRPC();
        }

        /// <summary>
        /// 检查UI系统是否就绪
        /// </summary>
        public bool IsUISystemReady()
        {
            return cardUIManager != null && cardUIComponents != null;
        }

        /// <summary>
        /// 获取系统状态摘要
        /// </summary>
        public string GetSystemStatus()
        {
            var status = "=== 卡牌系统状态 ===\n";
            status += $"系统就绪: {(IsSystemReady() ? "✓" : "✗")}\n";
            status += $"网络就绪: {(IsNetworkReady() ? "✓" : "✗")}\n";
            status += $"UI系统就绪: {(IsUISystemReady() ? "✓" : "✗")}\n";
            status += $"配置: {(cardConfig != null ? "✓" : "✗")}\n";
            status += $"效果系统: {(cardEffectSystem?.IsSystemReady() == true ? "✓" : "✗")}\n";
            status += $"玩家管理: {(playerCardManager?.IsInitialized == true ? "✓" : "✗")}\n";
            status += $"游戏桥接: {(cardGameBridge?.IsReady() == true ? "✓" : "✗")}\n";
            status += $"网络管理: {(cardNetworkManager != null ? "✓" : "✗")}\n";
            status += $"UI管理: {(cardUIManager != null ? "✓" : "✗")}\n";
            status += $"UI组件: {(cardUIComponents != null ? "✓" : "✗")}\n";
            return status;
        }

        /// <summary>
        /// 获取卡牌配置（供外部访问）
        /// </summary>
        public CardConfig GetCardConfig()
        {
            return cardConfig;
        }

        /// <summary>
        /// 获取效果系统实例（供外部访问）
        /// </summary>
        public CardEffectSystem GetEffectSystem()
        {
            return cardEffectSystem;
        }

        /// <summary>
        /// 获取玩家管理器实例（供外部访问）
        /// </summary>
        public PlayerCardManager GetPlayerCardManager()
        {
            return playerCardManager;
        }

        /// <summary>
        /// 获取游戏桥接器实例（供外部访问）
        /// </summary>
        public CardGameBridge GetCardGameBridge()
        {
            return cardGameBridge;
        }

        /// <summary>
        /// 获取UI管理器实例（供外部访问）
        /// </summary>
        public CardUIManager GetCardUIManager()
        {
            return cardUIManager;
        }

        /// <summary>
        /// 获取UI组件实例（供外部访问）
        /// </summary>
        public CardUIComponents GetCardUIComponents()
        {
            return cardUIComponents;
        }

        #endregion

        /// <summary>
        /// 显示系统状态（调试用）
        /// </summary>
        [ContextMenu("显示系统状态")]
        public void ShowSystemStatus()
        {
            Debug.Log(GetSystemStatus());
        }


        #region 工具方法

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[CardSystemManager] {message}");
            }
        }

        /// <summary>
        /// 错误日志
        /// </summary>
        private void LogError(string message)
        {
            Debug.LogError($"[CardSystemManager] {message}");
        }

        #endregion

        #region 静态访问接口

        /// <summary>
        /// 静态访问玩家管理器
        /// </summary>
        public static PlayerCardManager GetPlayerCardManagerStatic()
        {
            return Instance?.playerCardManager;
        }

        /// <summary>
        /// 静态访问卡牌配置
        /// </summary>
        public static CardConfig GetCardConfigStatic()
        {
            return Instance?.cardConfig;
        }

        /// <summary>
        /// 静态访问UI管理器
        /// </summary>
        public static CardUIManager GetCardUIManagerStatic()
        {
            return Instance?.cardUIManager;
        }

        /// <summary>
        /// 静态检查系统是否就绪
        /// </summary>
        public static bool IsSystemReadyStatic()
        {
            return Instance?.IsSystemReady() == true;
        }

        #endregion
    }
}