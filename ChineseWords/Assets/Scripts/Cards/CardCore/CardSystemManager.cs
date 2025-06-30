using UnityEngine;
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
    /// 卡牌系统管理器 - 重构版
    /// 完全控制系统创建顺序和依赖注入，解决时序问题
    /// </summary>
    public class CardSystemManager : MonoBehaviour
    {
        [Header("系统配置")]
        [SerializeField] public CardConfig cardConfig;
        [SerializeField] private bool enableDebugLogs = true;

        // 单例实例
        public static CardSystemManager Instance { get; private set; }

        // 系统引用仓库
        private CardEffectSystem cardEffectSystem;
        private PlayerCardManager playerCardManager;
        private CardNetworkManager cardNetworkManager;
        private CardGameBridge cardGameBridge;
        private CardUIComponents cardUIComponents;
        private CardUIManager cardUIManager;

        // 子对象引用
        private Transform coreSystemsParent;
        private Transform networkSystemsParent;
        private Transform integrationSystemsParent;
        private Transform uiSystemsParent;

        // 系统状态
        private bool isInitialized = false;

        #region 系统就绪事件

        /// <summary>
        /// 系统初始化完成事件
        /// </summary>
        public static event Action<bool> OnSystemInitialized;

        /// <summary>
        /// 核心系统就绪事件
        /// </summary>
        public static event Action OnCoreSystemReady;

        /// <summary>
        /// UI系统就绪事件（为兼容性保留）
        /// </summary>
        public static event Action OnUISystemReady;

        /// <summary>
        /// 玩家数据就绪事件（为兼容性保留）
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
                LogError("发现重复的CardSystemManager实例");
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            InitializeCardSystem();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                CleanupSystems();
                Instance = null;
            }
        }

        #endregion

        #region 系统初始化

        /// <summary>
        /// 初始化卡牌系统
        /// </summary>
        private void InitializeCardSystem()
        {
            LogDebug("开始初始化卡牌系统");

            try
            {
                // 阶段1-8：现有的初始化步骤
                ValidateConfiguration();
                CreateGameObjectStructure();
                CreateCoreSystems();
                CreateNetworkSystems();
                CreateUISystems();
                CreateIntegrationSystems();
                InjectDependencies();
                EstablishEventConnections();
                InitializeNetworkSystems();

                // 完成基础初始化
                FinishInitialization(true);

                // 阶段9：系统完全就绪后，检查现有玩家
                CheckExistingPlayersAfterSystemReady();
            }
            catch (Exception e)
            {
                LogError($"系统初始化失败: {e.Message}");
                FinishInitialization(false);
                throw;
            }
        }

        /// <summary>
        /// 系统完全就绪后检查现有玩家
        /// </summary>
        private void CheckExistingPlayersAfterSystemReady()
        {
            LogDebug("系统完全就绪，检查现有玩家");

            if (cardNetworkManager != null)
            {
                cardNetworkManager.CheckExistingPlayersAfterSystemReady();
            }
        }

        /// <summary>
        /// 验证配置
        /// </summary>
        private void ValidateConfiguration()
        {
            LogDebug("验证系统配置");

            if (cardConfig == null)
            {
                cardConfig = Resources.Load<CardConfig>("QuestionConfigs/CardConfig");
                if (cardConfig == null)
                {
                    throw new InvalidOperationException("无法加载CardConfig资源");
                }
            }

            if (!cardConfig.ValidateConfig())
            {
                throw new InvalidOperationException("CardConfig验证失败");
            }

            LogDebug($"配置验证成功: {cardConfig.AllCards.Count}张卡牌");
        }

        /// <summary>
        /// 创建GameObject结构
        /// </summary>
        private void CreateGameObjectStructure()
        {
            LogDebug("创建GameObject层级结构");

            // 创建核心系统容器
            var coreSystemsGO = new GameObject("CoreSystems");
            coreSystemsGO.transform.SetParent(transform);
            coreSystemsParent = coreSystemsGO.transform;

            // 创建网络系统容器
            var networkSystemsGO = new GameObject("NetworkSystems");
            networkSystemsGO.transform.SetParent(transform);
            networkSystemsParent = networkSystemsGO.transform;

            // 创建UI系统容器
            var uiSystemsGO = new GameObject("UISystems");
            uiSystemsGO.transform.SetParent(transform);
            uiSystemsParent = uiSystemsGO.transform;

            // 创建集成系统容器
            var integrationSystemsGO = new GameObject("IntegrationSystems");
            integrationSystemsGO.transform.SetParent(transform);
            integrationSystemsParent = integrationSystemsGO.transform;

            LogDebug("GameObject结构创建完成");
        }

        /// <summary>
        /// 创建核心系统
        /// </summary>
        private void CreateCoreSystems()
        {
            LogDebug("创建核心系统");

            // 创建效果系统
            var effectSystemGO = new GameObject("CardEffectSystem");
            effectSystemGO.transform.SetParent(coreSystemsParent);
            cardEffectSystem = effectSystemGO.AddComponent<CardEffectSystem>();

            // 手动设置单例
            if (!SetSystemInstance(cardEffectSystem))
            {
                throw new InvalidOperationException("CardEffectSystem单例设置失败");
            }

            // 注册效果
            CardEffectRegistrar.RegisterAllEffects(cardEffectSystem);

            // 验证效果系统
            if (!cardEffectSystem.IsSystemReady())
            {
                throw new InvalidOperationException("CardEffectSystem初始化失败");
            }

            // 创建玩家管理器
            var playerManagerGO = new GameObject("PlayerCardManager");
            playerManagerGO.transform.SetParent(coreSystemsParent);
            playerCardManager = playerManagerGO.AddComponent<PlayerCardManager>();

            // 手动设置单例
            if (!SetSystemInstance(playerCardManager))
            {
                throw new InvalidOperationException("PlayerCardManager单例设置失败");
            }

            // 初始化玩家管理器
            playerCardManager.Initialize();
            if (!playerCardManager.IsInitialized)
            {
                throw new InvalidOperationException("PlayerCardManager初始化失败");
            }

            LogDebug("核心系统创建完成");

            // 触发核心系统就绪事件
            OnCoreSystemReady?.Invoke();
        }

        /// <summary>
        /// 创建网络系统（仅创建实例，不初始化）
        /// </summary>
        private void CreateNetworkSystems()
        {
            try
            {
                LogDebug("创建网络系统实例");

                // 尝试获取现有实例
                cardNetworkManager = CardNetworkManager.Instance;

                // 如果不存在，创建新实例
                if (cardNetworkManager == null)
                {
                    var networkGO = new GameObject("CardNetworkManager");
                    networkGO.transform.SetParent(networkSystemsParent);
                    cardNetworkManager = networkGO.AddComponent<CardNetworkManager>();
                    LogDebug("创建了新的CardNetworkManager实例");
                }

                LogDebug("网络系统实例创建完成");
            }
            catch (Exception e)
            {
                LogDebug($"网络系统创建警告: {e.Message}");
            }
        }

        /// <summary>
        /// 初始化网络系统（在所有其他系统就绪后调用）
        /// </summary>
        private void InitializeNetworkSystems()
        {
            try
            {
                LogDebug("初始化网络系统");

                if (cardNetworkManager != null)
                {
                    cardNetworkManager.Initialize();
                    LogDebug("网络系统初始化完成");
                }
                else
                {
                    LogDebug("网络管理器不存在，跳过初始化");
                }
            }
            catch (Exception e)
            {
                LogDebug($"网络系统初始化警告: {e.Message}");
            }
        }

        /// <summary>
        /// 创建UI系统
        /// </summary>
        private void CreateUISystems()
        {
            LogDebug("创建UI系统");

            // 创建CardUIComponents
            var uiComponentsGO = new GameObject("CardUIComponents");
            uiComponentsGO.transform.SetParent(uiSystemsParent);
            cardUIComponents = uiComponentsGO.AddComponent<CardUIComponents>();

            // 手动设置单例
            if (!SetSystemInstance(cardUIComponents))
            {
                throw new InvalidOperationException("CardUIComponents单例设置失败");
            }

            // 创建CardUIManager
            var uiManagerGO = new GameObject("CardUIManager");
            uiManagerGO.transform.SetParent(uiSystemsParent);
            cardUIManager = uiManagerGO.AddComponent<CardUIManager>();

            // 手动设置单例
            if (!SetSystemInstance(cardUIManager))
            {
                throw new InvalidOperationException("CardUIManager单例设置失败");
            }

            LogDebug("UI系统创建完成");

            // 触发UI系统就绪事件
            OnUISystemReady?.Invoke();
        }

        /// <summary>
        /// 创建集成系统
        /// </summary>
        private void CreateIntegrationSystems()
        {
            LogDebug("创建集成系统");

            // 创建游戏桥接器
            var bridgeGO = new GameObject("CardGameBridge");
            bridgeGO.transform.SetParent(integrationSystemsParent);
            cardGameBridge = bridgeGO.AddComponent<CardGameBridge>();

            // 手动设置单例
            if (!SetSystemInstance(cardGameBridge))
            {
                throw new InvalidOperationException("CardGameBridge单例设置失败");
            }

            LogDebug("集成系统创建完成");
        }

        /// <summary>
        /// 注入依赖
        /// </summary>
        private void InjectDependencies()
        {
            LogDebug("注入系统依赖");

            // 为CardGameBridge注入依赖
            if (cardGameBridge != null)
            {
                var bridgeInjector = cardGameBridge.GetComponent<ISystemDependencyInjection>();
                if (bridgeInjector != null)
                {
                    bridgeInjector.InjectDependencies(
                        cardEffectSystem,
                        playerCardManager,
                        cardConfig,
                        cardNetworkManager
                    );
                }
                else
                {
                    // 如果CardGameBridge还没有实现依赖注入接口，使用反射临时解决
                    InjectDependenciesViaReflection();
                }
            }

            // 为CardUIManager注入依赖
            if (cardUIManager != null)
            {
                var uiInjector = cardUIManager.GetComponent<IUISystemDependencyInjection>();
                if (uiInjector != null)
                {
                    uiInjector.InjectDependencies(
                        cardEffectSystem,
                        playerCardManager,
                        cardConfig,
                        cardNetworkManager,
                        cardUIComponents
                    );
                }
                else
                {
                    // 使用反射注入UI依赖
                    InjectUIDependenciesViaReflection();
                }
            }

            LogDebug("依赖注入完成");
        }

        /// <summary>
        /// 通过反射注入依赖（临时方案）
        /// </summary>
        private void InjectDependenciesViaReflection()
        {
            if (cardGameBridge == null) return;

            var bridgeType = cardGameBridge.GetType();

            // 注入效果系统引用
            var effectSystemField = bridgeType.GetField("effectSystem",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            effectSystemField?.SetValue(cardGameBridge, cardEffectSystem);

            // 注入玩家管理器引用
            var playerManagerField = bridgeType.GetField("playerCardManager",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            playerManagerField?.SetValue(cardGameBridge, playerCardManager);

            // 注入配置引用
            var configField = bridgeType.GetField("cardConfig",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            configField?.SetValue(cardGameBridge, cardConfig);

            // 注入卡牌网络管理器引用
            var cardNetworkManagerField = bridgeType.GetField("cardNetworkManager",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            cardNetworkManagerField?.SetValue(cardGameBridge, cardNetworkManager);

            LogDebug("通过反射完成依赖注入");
        }

        /// <summary>
        /// 通过反射注入UI依赖（临时方案）
        /// </summary>
        private void InjectUIDependenciesViaReflection()
        {
            if (cardUIManager == null) return;

            var uiManagerType = cardUIManager.GetType();

            // 注入系统引用
            var playerManagerField = uiManagerType.GetField("playerCardManager",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            playerManagerField?.SetValue(cardUIManager, playerCardManager);

            var configField = uiManagerType.GetField("cardConfig",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            configField?.SetValue(cardUIManager, cardConfig);

            var componentsField = uiManagerType.GetField("cardUIComponents",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            componentsField?.SetValue(cardUIManager, cardUIComponents);

            var networkManagerField = uiManagerType.GetField("cardNetworkManager",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            networkManagerField?.SetValue(cardUIManager, cardNetworkManager);

            // 设置初始化标记
            var isInjectedField = uiManagerType.GetField("isDependencyInjected",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            isInjectedField?.SetValue(cardUIManager, true);

            LogDebug("通过反射完成UI依赖注入");
        }

        /// <summary>
        /// 建立事件连接
        /// </summary>
        private void EstablishEventConnections()
        {
            LogDebug("建立事件连接");

            // 订阅玩家管理器事件
            if (playerCardManager != null)
            {
                playerCardManager.OnCardUsed += HandlePlayerCardUsed;
                playerCardManager.OnCardAcquired += HandlePlayerCardAcquired;
                playerCardManager.OnCardTransferred += HandlePlayerCardTransferred;
                playerCardManager.OnHandSizeChanged += HandlePlayerHandChanged;
            }

            // 订阅网络事件（如果可用）
            if (cardNetworkManager != null)
            {
                CardNetworkManager.OnCardUsed += HandleNetworkCardUsed;
                CardNetworkManager.OnCardAdded += HandleNetworkCardAdded;
                CardNetworkManager.OnCardRemoved += HandleNetworkCardRemoved;
                CardNetworkManager.OnCardTransferred += HandleNetworkCardTransferred;
            }

            // 初始化UI系统
            if (cardUIManager != null)
            {
                // 调用UI管理器的初始化方法
                var initMethod = cardUIManager.GetType().GetMethod("InitializeFromSystemManager",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                initMethod?.Invoke(cardUIManager, null);
            }

            LogDebug("事件连接建立完成");
        }

        /// <summary>
        /// 手动设置系统单例
        /// </summary>
        private bool SetSystemInstance<T>(T instance) where T : MonoBehaviour
        {
            var type = typeof(T);
            var instanceProperty = type.GetProperty("Instance",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

            if (instanceProperty != null && instanceProperty.CanWrite)
            {
                instanceProperty.SetValue(null, instance);
                LogDebug($"{type.Name}单例设置成功");
                return true;
            }
            else
            {
                // 如果没有可写的Instance属性，尝试调用设置方法
                var setInstanceMethod = type.GetMethod("SetInstance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (setInstanceMethod != null)
                {
                    setInstanceMethod.Invoke(null, new object[] { instance });
                    LogDebug($"{type.Name}单例通过方法设置成功");
                    return true;
                }
            }

            LogError($"{type.Name}单例设置失败");
            return false;
        }

        /// <summary>
        /// 完成初始化
        /// </summary>
        private void FinishInitialization(bool success)
        {
            isInitialized = success;

            if (success)
            {
                LogDebug("卡牌系统初始化成功");
            }
            else
            {
                LogError("卡牌系统初始化失败");
            }

            OnSystemInitialized?.Invoke(success);
        }

        #endregion

        #region 事件处理

        private void HandlePlayerCardUsed(int playerId, int cardId, int targetPlayerId)
        {
            LogDebug($"玩家{playerId}使用卡牌{cardId}, 目标:{targetPlayerId}");

            if (cardNetworkManager != null && cardNetworkManager.CanSendRPC())
            {
                cardNetworkManager.BroadcastCardUsed((ushort)playerId, cardId, (ushort)targetPlayerId);
            }

            // 通知UI更新
            RequestUIUpdate(playerId);
        }

        private void HandlePlayerCardAcquired(int playerId, int cardId, string cardName)
        {
            LogDebug($"玩家{playerId}获得{cardName}");

            if (cardNetworkManager != null && cardNetworkManager.CanSendRPC())
            {
                cardNetworkManager.BroadcastCardAdded((ushort)playerId, cardId);
            }

            // 通知UI更新
            RequestUIUpdate(playerId);
        }

        private void HandlePlayerCardTransferred(int fromPlayerId, int toPlayerId, int cardId)
        {
            LogDebug($"卡牌{cardId}从玩家{fromPlayerId}转移到玩家{toPlayerId}");

            if (cardNetworkManager != null && cardNetworkManager.CanSendRPC())
            {
                cardNetworkManager.BroadcastCardTransferred((ushort)fromPlayerId, cardId, (ushort)toPlayerId);
            }

            // 通知UI更新
            RequestUIUpdate(fromPlayerId);
            RequestUIUpdate(toPlayerId);
        }

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
                    cardUIManager.RefreshPlayerCardDisplay(playerId);
                }
            }
            catch (Exception e)
            {
                LogDebug($"UI更新失败: {e.Message}");
            }
        }

        private void HandleNetworkCardUsed(ushort playerId, int cardId, ushort targetPlayerId, string cardName)
        {
            LogDebug($"网络同步: 玩家{playerId}使用{cardName}");
        }

        private void HandleNetworkCardAdded(ushort playerId, int cardId)
        {
            LogDebug($"网络同步: 玩家{playerId}获得卡牌{cardId}");
        }

        private void HandleNetworkCardRemoved(ushort playerId, int cardId)
        {
            LogDebug($"网络同步: 玩家{playerId}失去卡牌{cardId}");
        }

        private void HandleNetworkCardTransferred(ushort fromPlayerId, int cardId, ushort toPlayerId)
        {
            LogDebug($"网络同步: 卡牌{cardId}从玩家{fromPlayerId}转移到玩家{toPlayerId}");
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

            foreach (int playerId in playerIds)
            {
                OnPlayerJoined(playerId, $"Player{playerId}");
            }

            LogDebug("游戏开始处理完成");
        }

        /// <summary>
        /// 玩家加入时调用
        /// </summary>
        public void OnPlayerJoined(int playerId, string playerName = "")
        {
            LogDebug($"玩家加入: {playerId} ({playerName})");

            if (playerCardManager != null)
            {
                playerCardManager.InitializePlayer(playerId, playerName);

                // 触发玩家数据就绪事件
                OnPlayerDataReady?.Invoke(playerId);

                // 请求UI更新
                RequestUIUpdate(playerId);
            }
        }

        /// <summary>
        /// 玩家离开时调用
        /// </summary>
        public void OnPlayerLeft(int playerId)
        {
            LogDebug($"玩家离开: {playerId}");

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

        /// <summary>
        /// 游戏结束时调用
        /// </summary>
        public void OnGameEnded()
        {
            LogDebug("游戏结束");

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

        #endregion

        #region 公共接口（最小化）

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
                   playerCardManager.IsInitialized &&
                   cardGameBridge != null &&
                   cardUIComponents != null &&
                   cardUIManager != null;
        }

        public CardEffectSystem GetEffectSystem()
        {
            return cardEffectSystem;
        }
        public PlayerCardManager GetPlayerCardManager()
        {
            return playerCardManager;
        }
        public CardGameBridge GetCardGameBridge()
        {
            return cardGameBridge;
        }
        public CardUIComponents GetCardUIComponents()
        {
            return cardUIComponents;
        }
        public CardUIManager GetCardUIManager()
        {
            return cardUIManager;
        }
        public CardConfig GetCardConfig()
        {
            return cardConfig;
        }

        #endregion

        #region 静态访问接口
        public static PlayerCardManager GetPlayerCardManagerStatic()
        {
            return Instance?.playerCardManager;
        }
        public static CardConfig GetCardConfigStatic()
        {
            return Instance?.cardConfig;
        }
        public static CardUIManager GetCardUIManagerStatic()
        {
            return Instance?.cardUIManager;
        }
        public static CardUIComponents GetCardUIComponentsStatic()
        {
            return Instance?.cardUIComponents;
        }
        public static bool IsSystemReadyStatic()
        {
            return Instance?.IsSystemReady() == true;
        }

        #endregion

        #region 系统清理

        /// <summary>
        /// 清理系统
        /// </summary>
        private void CleanupSystems()
        {
            LogDebug("清理系统资源");

            // 取消事件订阅
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
            }

            LogDebug("系统清理完成");
        }

        #endregion

        #region 工具方法

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[CardSystemManager] {message}");
            }
        }

        private void LogError(string message)
        {
            Debug.LogError($"[CardSystemManager] {message}");
        }

        #endregion
    }

    /// <summary>
    /// 系统依赖注入接口
    /// </summary>
    public interface ISystemDependencyInjection
    {
        void InjectDependencies(
            CardEffectSystem effectSystem,
            PlayerCardManager playerCardManager,
            CardConfig cardConfig,
            CardNetworkManager networkManager
        );
    }

    /// <summary>
    /// UI系统依赖注入接口
    /// </summary>
    public interface IUISystemDependencyInjection
    {
        void InjectDependencies(
            CardEffectSystem effectSystem,
            PlayerCardManager playerCardManager,
            CardConfig cardConfig,
            CardNetworkManager networkManager,
            CardUIComponents uiComponents
        );
    }
}