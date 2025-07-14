using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Cards.Core;
using Cards.Player;
using Core.Network;
using Cards.Effects;
using Cards.Network;
using Classroom.Player;

namespace Cards.UI
{
    /// <summary>
    /// 卡牌UI管理器 - 箭头系统重构版
    /// 完全移除键盘操作，使用贝塞尔箭头进行目标选择
    /// </summary>
    public class CardUIManager : MonoBehaviour, IUISystemDependencyInjection
    {
        [Header("Canvas设置")]
        [SerializeField] private Canvas cardUICanvas; // 独立的卡牌UI Canvas
        [SerializeField] private int canvasSortingOrder = 105; // Canvas层级

        [Header("输入设置")]
        [SerializeField] private KeyCode cardDisplayTriggerKey = KeyCode.E; // 卡牌展示触发键

        [Header("箭头系统设置")]
        [SerializeField] private GameObject arrowPrefab; // 贝塞尔箭头预制体
        [SerializeField] private string arrowPrefabPath = "Prefabs/UI/InGame/Arr"; // 箭头预制体路径
        [SerializeField] private float pressHoldTime = 0.2f; // 按住时间阈值

        [Header("拖拽设置 - 释放区域（屏幕百分比）")]
        [SerializeField] private float releaseAreaCenterX = 0.5f; // 50%
        [SerializeField] private float releaseAreaCenterY = 0.5f; // 50%
        [SerializeField] private float releaseAreaWidth = 0.3f; // 30%
        [SerializeField] private float releaseAreaHeight = 0.3f; // 30%

        [Header("防抖设置")]
        [SerializeField] private float refreshCooldown = 0.1f; // 刷新冷却时间

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 单例实例
        public static CardUIManager Instance { get; private set; }

        // UI状态 - 重构后的状态
        public enum UIState
        {
            Thumbnail,        // 缩略图状态（默认状态）
            FanDisplay,       // 扇形展示状态
            ArrowTargeting,   // 箭头目标选择状态（替代原Dragging和TargetSelection）
            Disabled          // 禁用状态（答题期间）
        }

        private UIState currentUIState = UIState.Thumbnail;
        private bool isInitialized = false;
        private bool isDependencyInjected = false;
        private bool isMyTurn = false;
        private bool canUseCards = true;

        // 依赖注入的系统引用
        private CardEffectSystem cardEffectSystem;
        private PlayerCardManager playerCardManager;
        private CardConfig cardConfig;
        private CardNetworkManager cardNetworkManager;
        private CardUIComponents cardUIComponents;
        private CardDisplayUI cardDisplayUI;

        // 卡牌数据
        private List<CardDisplayData> currentHandCards = new List<CardDisplayData>();
        private int myPlayerId = -1;

        // 箭头系统相关
        private ArrowManager arrowManager;
        private GameObject currentPressedCard = null;
        private CardData currentCardData = null;
        private Vector2 pressStartPosition = Vector2.zero;
        private float pressStartTime = 0f;
        private bool isWaitingForHold = false;

        // 防抖相关
        private float lastRefreshTime = 0f;
        private bool hasPendingRefresh = false;

        // 摄像机等待相关
        private bool waitingForCamera = false;
        private Coroutine cameraWaitCoroutine = null;

        // 事件
        public System.Action<int, int> OnCardUseRequested; // cardId, targetPlayerId
        public System.Action OnCardUIOpened;
        public System.Action OnCardUIClosed;

        // 属性
        public UIState CurrentState => currentUIState;
        public bool IsCardUIVisible => currentUIState != UIState.Disabled;
        public bool CanOpenCardUI => currentUIState == UIState.Thumbnail && !isMyTurn && canUseCards && isInitialized;

        #region Unity生命周期

        private void Awake()
        {
            LogDebug($"{GetType().Name} 组件已创建，等待单例设置");
        }

        private void Start()
        {
            // 等待依赖注入完成
            if (!isDependencyInjected)
            {
                LogDebug("等待依赖注入完成");
                return;
            }

            if (!isInitialized && !isInitializing)
            {
                StartCoroutine(DelayedInitialization());
            }
        }

        private void Update()
        {
            if (!isInitialized) return;

            HandleInput();
            HandleCardPressLogic();
            HandlePendingRefresh();
        }

        private void OnEnable()
        {
            // 订阅ClassroomManager的摄像机设置完成事件
            Classroom.ClassroomManager.OnCameraSetupCompleted += OnCameraSetupCompleted;
        }

        private void OnDisable()
        {
            // 取消订阅ClassroomManager事件
            Classroom.ClassroomManager.OnCameraSetupCompleted -= OnCameraSetupCompleted;
        }

        private void OnDestroy()
        {
            CleanupUI();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region 摄像机事件处理

        /// <summary>
        /// 响应ClassroomManager的摄像机设置完成事件
        /// </summary>
        private void OnCameraSetupCompleted()
        {
            LogDebug("收到摄像机设置完成事件");

            if (waitingForCamera && cardDisplayUI != null)
            {
                // 尝试重新初始化CardDisplayUI，这次应该能找到摄像机了
                PlayerCameraController cameraController = FindPlayerCameraController();
                if (cameraController != null)
                {
                    LogDebug("摄像机设置完成后成功找到摄像机控制器，重新初始化CardDisplayUI");
                    cardDisplayUI.Initialize(cardUIComponents, cameraController);
                    waitingForCamera = false;

                    // 停止等待协程
                    if (cameraWaitCoroutine != null)
                    {
                        StopCoroutine(cameraWaitCoroutine);
                        cameraWaitCoroutine = null;
                    }
                }
            }
        }

        #endregion

        #region 依赖注入接口实现

        /// <summary>
        /// 实现IUISystemDependencyInjection接口
        /// </summary>
        public void InjectDependencies(
            CardEffectSystem effectSystem,
            PlayerCardManager playerCardManager,
            CardConfig cardConfig,
            CardNetworkManager networkManager,
            CardUIComponents uiComponents)
        {
            this.cardEffectSystem = effectSystem;
            this.playerCardManager = playerCardManager;
            this.cardConfig = cardConfig;
            this.cardNetworkManager = networkManager;
            this.cardUIComponents = uiComponents;

            isDependencyInjected = true;
            LogDebug("依赖注入完成");

            if (gameObject.activeInHierarchy && !isInitialized && !isInitializing)
            {
                StartCoroutine(DelayedInitialization());
            }
        }

        /// <summary>
        /// 由CardSystemManager调用的初始化方法
        /// </summary>
        public void InitializeFromSystemManager()
        {
            LogDebug("收到来自CardSystemManager的初始化调用");

            if (!isDependencyInjected)
            {
                LogError("依赖未注入，无法初始化");
                return;
            }

            if (!isInitialized && !isInitializing)
            {
                StartCoroutine(DelayedInitialization());
            }
            else
            {
                LogDebug($"跳过SystemManager初始化调用 - 已初始化: {isInitialized}, 正在初始化: {isInitializing}");
            }
        }

        #endregion

        #region 初始化
        private bool isInitializing = false;
        /// <summary>
        /// 延迟初始化
        /// </summary>
        private IEnumerator DelayedInitialization()
        {
            // 检查是否已经初始化或正在初始化
            if (isInitialized || isInitializing)
            {
                LogDebug($"跳过重复初始化 - 已初始化: {isInitialized}, 正在初始化: {isInitializing}");
                yield break;
            }

            // 设置初始化锁定
            isInitializing = true;
            LogDebug("开始延迟初始化");

            // 等待一帧确保所有系统完全就绪
            yield return null;

            // 再次检查，防止在等待期间状态改变
            if (isInitialized)
            {
                LogDebug("在等待期间已完成初始化，退出");
                isInitializing = false;
                yield break;
            }

            try
            {
                // 设置Canvas
                SetupCanvas();

                // 创建或查找CardDisplayUI
                SetupCardDisplayUI();

                // 初始化CardDisplayUI（可能没有摄像机）
                InitializeCardDisplayUI();

                // 创建箭头管理器
                SetupArrowManager();

                // 获取我的玩家ID
                GetMyPlayerId();

                // 订阅事件
                SubscribeToEvents();

                isInitialized = true;
                LogDebug("CardUIManager初始化完成");

                // 直接显示缩略图
                RefreshAndShowThumbnailWithDebounce();
            }
            catch (System.Exception e)
            {
                LogError($"初始化过程中发生异常: {e.Message}");
            }
            finally
            {
                // 🔧 释放初始化锁定
                isInitializing = false;
            }
        }

        /// <summary>
        /// 初始化CardDisplayUI
        /// </summary>
        private void InitializeCardDisplayUI()
        {
            if (cardDisplayUI == null)
            {
                LogError("CardDisplayUI为空，无法初始化");
                return;
            }

            if (cardUIComponents == null)
            {
                LogError("CardUIComponents为空，无法初始化CardDisplayUI");
                return;
            }

            // 查找摄像机控制器
            PlayerCameraController cameraController = FindPlayerCameraController();

            if (cameraController == null)
            {
                LogDebug("暂时未找到摄像机控制器，将等待ClassroomManager完成初始化");
                waitingForCamera = true;

                // 启动等待协程，避免无限等待
                cameraWaitCoroutine = StartCoroutine(WaitForCameraWithTimeout());
            }

            // 即使没有摄像机也先初始化CardDisplayUI
            cardDisplayUI.Initialize(cardUIComponents, cameraController);

            LogDebug("CardDisplayUI初始化完成（摄像机可能为空）");
        }

        /// <summary>
        /// 设置箭头管理器
        /// </summary>
        private void SetupArrowManager()
        {
            // 加载箭头预制体（如果未在Inspector中设置）
            if (arrowPrefab == null)
            {
                LoadArrowPrefab();
            }

            CleanupExistingArrowManagers();

            // 使用工厂方法创建ArrowManager
            arrowManager = ArrowManager.CreateArrowManager(transform, cardUICanvas, arrowPrefab);

            if (arrowManager != null)
            {
                // 同步中央区域设置
                arrowManager.SyncCenterAreaSettings(
                    releaseAreaCenterX, releaseAreaCenterY,
                    releaseAreaWidth, releaseAreaHeight
                );

                // 订阅箭头事件
                arrowManager.OnValidPlayerTargetDetected += OnArrowValidPlayerTarget;
                arrowManager.OnValidCenterAreaDetected += OnArrowValidCenterArea;
                arrowManager.OnInvalidTargetDetected += OnArrowInvalidTarget;
                arrowManager.OnNoTargetDetected += OnArrowNoTarget;

                LogDebug("ArrowManager创建并配置完成");
            }
        }
        private void CleanupExistingArrowManagers()
        {
            // 查找并删除所有旧的ArrowManager实例
            ArrowManager[] existingManagers = GetComponentsInChildren<ArrowManager>();
            if (existingManagers.Length > 0)
            {
                LogDebug($"发现 {existingManagers.Length} 个旧的ArrowManager实例，开始清理");

                foreach (var manager in existingManagers)
                {
                    if (manager != null)
                    {
                        DestroyImmediate(manager.gameObject);
                    }
                }
            }
        }

        /// <summary>
        /// 加载箭头预制体
        /// </summary>
        private void LoadArrowPrefab()
        {
                GameObject loadedPrefab = Resources.Load<GameObject>(arrowPrefabPath);

                if (loadedPrefab != null)
                {
                    arrowPrefab = loadedPrefab;
                }
        }

        /// <summary>
        /// 等待摄像机设置，带超时机制
        /// </summary>
        private IEnumerator WaitForCameraWithTimeout()
        {
            float timeout = 10f; // 10秒超时
            float elapsed = 0f;

            while (waitingForCamera && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (waitingForCamera)
            {
                LogWarning("等待摄像机设置超时，继续运行（摄像机功能可能不可用）");
                waitingForCamera = false;
            }

            cameraWaitCoroutine = null;
        }

        /// <summary>
        /// 查找玩家摄像机控制器
        /// </summary>
        private PlayerCameraController FindPlayerCameraController()
        {
            // 方式1: 通过ClassroomManager获取
            var classroomManager = FindObjectOfType<Classroom.ClassroomManager>();
            if (classroomManager != null && classroomManager.IsInitialized)
            {
                var cameraController = classroomManager.GetLocalPlayerCameraController();
                if (cameraController != null)
                {
                    LogDebug($"通过ClassroomManager找到摄像机控制器: {cameraController.name}");
                    return cameraController;
                }
            }

            // 方式2: 遍历所有PlayerCameraController找到本地玩家的
            var controllers = FindObjectsOfType<PlayerCameraController>();
            foreach (var controller in controllers)
            {
                if (controller.IsLocalPlayer)
                {
                    LogDebug($"通过遍历找到本地玩家摄像机控制器: {controller.name}");
                    return controller;
                }
            }

            return null; // 没有警告日志，避免频繁打印
        }

        /// <summary>
        /// 设置Canvas
        /// </summary>
        private void SetupCanvas()
        {
            if (cardUICanvas == null)
            {
                Canvas[] canvases = FindObjectsOfType<Canvas>();
                foreach (Canvas canvas in canvases)
                {
                    if (canvas.name == "CardCanvas")
                    {
                        cardUICanvas = canvas;
                        break;
                    }
                }
            }

            // 确保Canvas配置正确
            if (cardUICanvas != null)
            {
                cardUICanvas.sortingOrder = canvasSortingOrder;
                LogDebug($"Canvas设置完成: {cardUICanvas.name}, SortingOrder: {cardUICanvas.sortingOrder}");
            }
        }

        /// <summary>
        /// 设置CardDisplayUI
        /// </summary>
        private void SetupCardDisplayUI()
        {
            cardDisplayUI = GetComponentInChildren<CardDisplayUI>();
            if (cardDisplayUI == null)
            {
                // 在场景中查找已有的CardDisplayUI
                cardDisplayUI = FindObjectOfType<CardDisplayUI>();

                if (cardDisplayUI == null)
                {
                    // 创建新的CardDisplayUI
                    GameObject displayObject = new GameObject("CardDisplayUI");
                    displayObject.transform.SetParent(cardUICanvas.transform, false);

                    // 添加RectTransform组件确保正确的UI布局
                    RectTransform rectTransform = displayObject.AddComponent<RectTransform>();
                    rectTransform.anchorMin = Vector2.zero;
                    rectTransform.anchorMax = Vector2.one;
                    rectTransform.sizeDelta = Vector2.zero;
                    rectTransform.anchoredPosition = Vector2.zero;

                    cardDisplayUI = displayObject.AddComponent<CardDisplayUI>();
                    LogDebug("创建了新的CardDisplayUI实例，已正确挂载到Canvas下");
                }
                else
                {
                    LogDebug("找到了场景中现有的CardDisplayUI实例");

                    // 确保现有的CardDisplayUI也在正确的父对象下
                    if (cardDisplayUI.transform.parent != cardUICanvas.transform)
                    {
                        LogDebug($"将现有CardDisplayUI从 {cardDisplayUI.transform.parent?.name} 移动到 {cardUICanvas.name} 下");
                        cardDisplayUI.transform.SetParent(cardUICanvas.transform, false);
                    }
                }
            }
            else
            {
                // 确保现有的CardDisplayUI也在正确的父对象下
                if (cardDisplayUI.transform.parent != cardUICanvas.transform)
                {
                    LogDebug($"将现有CardDisplayUI从 {cardDisplayUI.transform.parent?.name} 移动到 {cardUICanvas.name} 下");
                    cardDisplayUI.transform.SetParent(cardUICanvas.transform, false);
                }
            }

            LogDebug($"CardDisplayUI设置完成，父对象: {cardDisplayUI.transform.parent?.name}");
        }

        /// <summary>
        /// 获取我的玩家ID
        /// </summary>
        private void GetMyPlayerId()
        {
            if (NetworkManager.Instance != null)
            {
                myPlayerId = NetworkManager.Instance.ClientId;
                LogDebug($"确定我的玩家ID: {myPlayerId}");
            }
        }

        #endregion

        #region 防抖机制

        /// <summary>
        /// 处理等待中的刷新请求
        /// </summary>
        private void HandlePendingRefresh()
        {
            if (hasPendingRefresh && Time.time - lastRefreshTime >= refreshCooldown)
            {
                hasPendingRefresh = false;
                ExecuteRefreshAndShowThumbnail();
            }
        }

        /// <summary>
        /// 带防抖的刷新并显示缩略图
        /// </summary>
        private void RefreshAndShowThumbnailWithDebounce()
        {
            if (Time.time - lastRefreshTime < refreshCooldown)
            {
                // 在冷却期内，标记等待刷新
                hasPendingRefresh = true;
                return;
            }

            lastRefreshTime = Time.time;
            ExecuteRefreshAndShowThumbnail();
        }

        /// <summary>
        /// 执行实际的刷新操作
        /// </summary>
        private void ExecuteRefreshAndShowThumbnail()
        {
            RefreshHandCards();
            ShowThumbnail();
        }

        #endregion

        #region 事件订阅

        /// <summary>
        /// 订阅事件
        /// </summary>
        private void SubscribeToEvents()
        {
            // 订阅回合变化事件
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerTurnChanged += OnPlayerTurnChanged;
            }

            // 订阅卡牌显示UI事件
            if (cardDisplayUI != null)
            {
                cardDisplayUI.OnCardHoverEnter += OnCardHoverEnter;
                cardDisplayUI.OnCardHoverExit += OnCardHoverExit;

                // 新的按压事件（替代原来的OnCardSelected）
                cardDisplayUI.OnCardPressStart += OnCardPressStart;
                cardDisplayUI.OnCardPressEnd += OnCardPressEnd;
            }

            LogDebug("事件订阅完成");
        }

        /// <summary>
        /// 取消订阅事件
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            // 取消订阅回合变化事件
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerTurnChanged -= OnPlayerTurnChanged;
            }

            // 取消订阅卡牌显示UI事件
            if (cardDisplayUI != null)
            {
                cardDisplayUI.OnCardHoverEnter -= OnCardHoverEnter;
                cardDisplayUI.OnCardHoverExit -= OnCardHoverExit;
                cardDisplayUI.OnCardPressStart -= OnCardPressStart;
                cardDisplayUI.OnCardPressEnd -= OnCardPressEnd;
            }

            // 取消订阅箭头事件
            if (arrowManager != null)
            {
                arrowManager.OnValidPlayerTargetDetected -= OnArrowValidPlayerTarget;
                arrowManager.OnValidCenterAreaDetected -= OnArrowValidCenterArea;
                arrowManager.OnInvalidTargetDetected -= OnArrowInvalidTarget;
                arrowManager.OnNoTargetDetected -= OnArrowNoTarget;
            }

            LogDebug("取消事件订阅");
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 处理回合变化事件
        /// </summary>
        private void OnPlayerTurnChanged(ushort newTurnPlayerId)
        {
            bool wasMyTurn = isMyTurn;
            isMyTurn = (myPlayerId == newTurnPlayerId);

            LogDebug($"回合变化 - 当前玩家: {newTurnPlayerId}, 是我的回合: {isMyTurn}");

            // 如果轮到我的回合，关闭到缩略图状态
            if (isMyTurn && currentUIState == UIState.FanDisplay)
            {
                LogDebug("轮到我的回合，切换到缩略图状态");
                SetUIState(UIState.Thumbnail);
                if (cardDisplayUI != null)
                {
                    cardDisplayUI.HideCardDisplay();
                    RefreshAndShowThumbnailWithDebounce();
                }
            }

            // 如果轮到我的回合且在箭头状态，取消箭头
            if (isMyTurn && currentUIState == UIState.ArrowTargeting)
            {
                CancelArrowTargeting();
            }

            UpdateUIAvailability();
        }

        /// <summary>
        /// 处理卡牌按压开始
        /// </summary>
        private void OnCardPressStart(GameObject cardUI, Vector2 pressPosition)
        {
            if (currentUIState != UIState.FanDisplay) return;

            // 获取卡牌数据
            CardUIIdentifier identifier = cardUI.GetComponent<CardUIIdentifier>();
            if (identifier == null)
            {
                LogError("卡牌UI缺少标识组件");
                return;
            }

            // 获取卡牌配置
            var cardData = GetCardDataById(identifier.cardId);
            if (cardData == null)
            {
                LogError($"未找到卡牌数据: {identifier.cardId}");
                return;
            }

            // 开始按压逻辑
            StartCardPress(cardUI, cardData, pressPosition);
        }

        /// <summary>
        /// 处理卡牌按压结束
        /// </summary>
        private void OnCardPressEnd(GameObject cardUI)
        {
            if (currentPressedCard != cardUI) return;

            // 结束按压逻辑
            EndCardPress();
        }

        /// <summary>
        /// 处理卡牌悬停进入
        /// </summary>
        private void OnCardHoverEnter(GameObject cardUI)
        {
            ShowCardTooltip(cardUI);
        }

        /// <summary>
        /// 处理卡牌悬停离开
        /// </summary>
        private void OnCardHoverExit(GameObject cardUI)
        {
            HideCardTooltip();
        }

        #endregion

        #region 箭头事件处理

        private void OnArrowValidPlayerTarget(ushort playerId)
        {
            LogDebug($"箭头指向有效玩家目标: {playerId}");
        }

        private void OnArrowValidCenterArea()
        {
            LogDebug("箭头指向有效中央区域");
        }

        private void OnArrowInvalidTarget()
        {
            LogDebug("箭头指向无效目标");
        }

        private void OnArrowNoTarget()
        {
        }

        #endregion

        #region 输入处理

        /// <summary>
        /// 处理输入
        /// </summary>
        private void HandleInput()
        {
            // E键切换卡牌UI
            if (Input.GetKeyDown(cardDisplayTriggerKey))
            {
                HandleCardDisplayToggle();
            }

            // ESC键关闭卡牌UI或取消箭头
            if (Input.GetKeyDown(KeyCode.Escape) && IsCardUIVisible)
            {
                if (currentUIState == UIState.ArrowTargeting)
                {
                    CancelArrowTargeting();
                }
                else
                {
                    HideCardUI();
                }
            }
        }

        /// <summary>
        /// 处理卡牌展示切换
        /// </summary>
        private void HandleCardDisplayToggle()
        {
            switch (currentUIState)
            {
                case UIState.Thumbnail:
                    if (!isMyTurn && canUseCards && isInitialized)
                    {
                        ShowFanDisplay();
                    }
                    else
                    {
                        LogDebug($"无法打开扇形展示 - 我的回合: {isMyTurn}, 可用状态: {canUseCards}, 已初始化: {isInitialized}");
                    }
                    break;

                case UIState.FanDisplay:
                    // 切换回缩略图状态
                    SetUIState(UIState.Thumbnail);
                    if (cardDisplayUI != null)
                    {
                        cardDisplayUI.HideCardDisplay();
                        RefreshAndShowThumbnailWithDebounce();
                    }
                    break;

                case UIState.ArrowTargeting:
                    CancelArrowTargeting();
                    break;

                case UIState.Disabled:
                    LogDebug("卡牌UI当前被禁用");
                    break;
            }
        }

        #endregion

        #region 卡牌按压逻辑

        /// <summary>
        /// 处理卡牌按压逻辑
        /// </summary>
        private void HandleCardPressLogic()
        {
            if (isWaitingForHold && currentPressedCard != null)
            {
                float pressDuration = Time.time - pressStartTime;

                // 检查是否达到按压时间阈值
                if (pressDuration >= pressHoldTime)
                {
                    // 开始箭头目标选择
                    StartArrowTargeting();
                    isWaitingForHold = false;
                }

                // 检查是否提前松开了鼠标
                if (!Input.GetMouseButton(0))
                {
                    // 取消操作
                    CancelCardPress();
                }
            }
        }

        /// <summary>
        /// 开始卡牌按压
        /// </summary>
        private void StartCardPress(GameObject cardUI, CardData cardData, Vector2 pressPosition)
        {
            if (currentUIState != UIState.FanDisplay)
            {
                LogWarning("不在扇形展示状态，无法开始卡牌按压");
                return;
            }

            LogDebug($"开始卡牌按压: {cardData.cardName}");

            currentPressedCard = cardUI;
            currentCardData = cardData;
            pressStartPosition = pressPosition;
            pressStartTime = Time.time;
            isWaitingForHold = true;
        }

        /// <summary>
        /// 结束卡牌按压
        /// </summary>
        private void EndCardPress()
        {
            if (currentUIState == UIState.ArrowTargeting)
            {
                // 在箭头模式下，结束按压意味着释放卡牌
                CompleteArrowTargeting();
            }
            else if (isWaitingForHold)
            {
                // 还在等待按压时间，视为取消
                CancelCardPress();
            }
        }

        /// <summary>
        /// 取消卡牌按压
        /// </summary>
        private void CancelCardPress()
        {
            LogDebug("取消卡牌按压");

            currentPressedCard = null;
            currentCardData = null;
            pressStartPosition = Vector2.zero;
            pressStartTime = 0f;
            isWaitingForHold = false;
        }

        #endregion

        #region 箭头目标选择系统

        /// <summary>
        /// 开始箭头目标选择
        /// </summary>
        private void StartArrowTargeting()
        {
            if (arrowManager == null || currentCardData == null)
            {
                LogError("ArrowManager或卡牌数据为空，无法开始箭头目标选择");
                CancelCardPress();
                return;
            }

            LogDebug($"开始箭头目标选择: {currentCardData.cardName}");

            // 启动箭头管理器
            bool success = arrowManager.StartArrowTargeting(pressStartPosition, currentCardData);
            if (success)
            {
                SetUIState(UIState.ArrowTargeting);
                LogDebug("箭头目标选择模式已激活");
            }
            else
            {
                LogError("启动箭头目标选择失败");
                CancelCardPress();
            }
        }

        /// <summary>
        /// 完成箭头目标选择
        /// </summary>
        private void CompleteArrowTargeting()
        {
            if (arrowManager == null || currentCardData == null)
            {
                LogError("ArrowManager或卡牌数据为空，无法完成箭头目标选择");
                return;
            }

            // 获取最终目标结果
            ushort targetPlayerId;
            ArrowManager.TargetDetectionResult result = arrowManager.EndArrowTargeting(out targetPlayerId);

            LogDebug($"箭头目标选择完成 - 结果: {result}, 目标玩家: {targetPlayerId}");

            // 根据结果执行相应操作
            switch (result)
            {
                case ArrowManager.TargetDetectionResult.PlayerConsole:
                    // 指向玩家，使用卡牌
                    ExecuteCardUsage(currentCardData.cardId, targetPlayerId);
                    break;

                case ArrowManager.TargetDetectionResult.CenterArea:
                    // 指向中央区域，根据卡牌类型决定目标
                    int centerTargetId = GetCenterAreaTargetId(currentCardData);
                    ExecuteCardUsage(currentCardData.cardId, centerTargetId);
                    break;

                case ArrowManager.TargetDetectionResult.Invalid:
                case ArrowManager.TargetDetectionResult.None:
                default:
                    LogDebug("无效目标或无目标，取消卡牌使用");
                    break;
            }

            // 清理状态
            CleanupArrowTargeting();
        }

        /// <summary>
        /// 取消箭头目标选择
        /// </summary>
        private void CancelArrowTargeting()
        {
            LogDebug("取消箭头目标选择");

            if (arrowManager != null)
            {
                arrowManager.CancelArrowTargeting();
            }

            CleanupArrowTargeting();
        }

        /// <summary>
        /// 清理箭头目标选择状态
        /// </summary>
        private void CleanupArrowTargeting()
        {
            // 清理卡牌按压状态
            CancelCardPress();

            // 返回扇形展示状态
            SetUIState(UIState.FanDisplay);

            LogDebug("箭头目标选择状态已清理");
        }

        /// <summary>
        /// 获取中央区域目标ID
        /// </summary>
        private int GetCenterAreaTargetId(CardData cardData)
        {
            switch (cardData.targetType)
            {
                case TargetType.Self:
                    return myPlayerId;

                case TargetType.AllPlayers:
                    return 0; // 特殊值表示所有玩家

                default:
                    LogWarning($"卡牌类型 {cardData.targetType} 不应该拖到中央区域");
                    return myPlayerId;
            }
        }

        #endregion

        #region 卡牌使用

        /// <summary>
        /// 执行卡牌使用
        /// </summary>
        private void ExecuteCardUsage(int cardId, int targetPlayerId)
        {
            LogDebug($"执行卡牌使用: 卡牌ID {cardId}, 目标 {targetPlayerId}");

            // 验证卡牌使用条件
            if (!ValidateCardUsage(cardId))
            {
                var cardData = GetCardDataById(cardId);
                LogWarning($"卡牌 {cardData?.cardName ?? "未知"} 使用条件验证失败");
                return;
            }

            // 通过PlayerCardManager使用卡牌
            if (playerCardManager != null)
            {
                bool success = playerCardManager.UseCard(myPlayerId, cardId, targetPlayerId);

                if (success)
                {
                    LogDebug($"卡牌使用成功: {cardId}");

                    // 修复：立即更新UI显示
                    RefreshHandCards();

                    // 如果当前在扇形展示状态，更新扇形展示
                    if (currentUIState == UIState.FanDisplay && cardDisplayUI != null)
                    {
                        bool updateSuccess = cardDisplayUI.UpdateFanDisplayWithCards(currentHandCards);
                        if (!updateSuccess)
                        {
                            LogDebug("扇形展示更新后自动返回缩略图状态");
                            SetUIState(UIState.Thumbnail);
                        }
                    }
                }
                else
                {
                    LogWarning($"卡牌使用失败: {cardId}");
                }
            }
            else
            {
                LogError("PlayerCardManager不可用，无法使用卡牌");
            }
        }

        /// <summary>
        /// 验证卡牌使用条件
        /// </summary>
        private bool ValidateCardUsage(int cardId)
        {
            // 查找卡牌数据
            var cardData = GetCardDataById(cardId);
            if (cardData == null)
            {
                LogError($"未找到卡牌数据: {cardId}");
                return false;
            }

            // 检查回合状态
            if (isMyTurn)
            {
                LogDebug("我的回合时无法使用卡牌");
                return false;
            }

            // 检查玩家手牌中是否有这张卡
            if (playerCardManager != null)
            {
                var playerHand = playerCardManager.GetPlayerHand(myPlayerId);
                if (!playerHand.Contains(cardId))
                {
                    LogDebug($"玩家手牌中没有卡牌: {cardId}");
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region 提示系统

        /// <summary>
        /// 显示卡牌提示
        /// </summary>
        private void ShowCardTooltip(GameObject cardUI)
        {
            CardUIIdentifier identifier = cardUI.GetComponent<CardUIIdentifier>();
            if (identifier == null) return;

            var cardData = GetCardDataById(identifier.cardId);
            if (cardData == null) return;

            string tooltip = $"{cardData.cardName}\n{cardData.description}";
            // TODO: 显示实际的提示UI
        }

        /// <summary>
        /// 隐藏卡牌提示
        /// </summary>
        private void HideCardTooltip()
        {
            // TODO: 隐藏实际的提示UI
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 显示扇形展示
        /// </summary>
        public void ShowFanDisplay()
        {
            if (currentUIState != UIState.Thumbnail)
            {
                LogWarning("当前状态不是Thumbnail，无法显示扇形展示");
                return;
            }

            LogDebug("显示扇形展示");

            // 刷新手牌数据
            RefreshHandCards();

            // 显示扇形展示
            if (cardDisplayUI != null && cardDisplayUI.ShowFanDisplayWithCards(currentHandCards))
            {
                SetUIState(UIState.FanDisplay);
            }
        }

        /// <summary>
        /// 隐藏卡牌UI
        /// </summary>
        public void HideCardUI()
        {
            LogDebug("隐藏卡牌UI");

            // 清理箭头状态
            if (currentUIState == UIState.ArrowTargeting)
            {
                CancelArrowTargeting();
            }

            // 清理按压状态
            if (isWaitingForHold)
            {
                CancelCardPress();
            }

            // 隐藏展示UI
            if (cardDisplayUI != null)
            {
                cardDisplayUI.HideCardDisplay();
            }

            // 设置回缩略图状态
            SetUIState(UIState.Thumbnail);
            RefreshAndShowThumbnailWithDebounce();

            // 触发事件
            OnCardUIClosed?.Invoke();
        }

        /// <summary>
        /// 刷新玩家卡牌显示（供CardSystemManager调用）
        /// </summary>
        public void RefreshPlayerCardDisplay(int playerId)
        {
            if (playerId != myPlayerId) return;

            LogDebug($"刷新玩家{playerId}的卡牌显示");

            // 只有在显示状态时才刷新
            if (IsCardUIVisible)
            {
                // 根据当前状态刷新对应的显示
                switch (currentUIState)
                {
                    case UIState.Thumbnail:
                        RefreshAndShowThumbnailWithDebounce();
                        break;
                    case UIState.FanDisplay:
                        RefreshHandCards();
                        if (cardDisplayUI != null)
                        {
                            bool updateSuccess = cardDisplayUI.UpdateFanDisplayWithCards(currentHandCards);
                            if (!updateSuccess)
                            {
                                LogWarning("扇形展示更新失败，回退到缩略图状态");
                                SetUIState(UIState.Thumbnail);
                                RefreshAndShowThumbnailWithDebounce();
                            }
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// 清理玩家显示（供CardSystemManager调用）
        /// </summary>
        public void ClearPlayerDisplay(int playerId)
        {
            if (playerId == myPlayerId)
            {
                LogDebug($"清理玩家{playerId}的显示");
                HideCardUI();
                currentHandCards.Clear();
            }
        }

        /// <summary>
        /// 清理所有显示（供CardSystemManager调用）
        /// </summary>
        public void ClearAllDisplays()
        {
            LogDebug("清理所有显示");
            HideCardUI();
            currentHandCards.Clear();
        }

        #endregion

        #region 数据管理

        /// <summary>
        /// 刷新手牌数据
        /// </summary>
        private void RefreshHandCards()
        {
            currentHandCards.Clear();

            if (playerCardManager != null && myPlayerId != -1)
            {
                try
                {
                    // 获取真实的手牌数据
                    var playerHand = playerCardManager.GetPlayerHand(myPlayerId);

                    if (cardConfig != null)
                    {
                        foreach (int cardId in playerHand)
                        {
                            var cardData = cardConfig.GetCardById(cardId);
                            if (cardData != null)
                            {
                                currentHandCards.Add(new CardDisplayData(cardData));
                            }
                        }
                    }

                    LogDebug($"刷新手牌数据完成，当前手牌数量: {currentHandCards.Count}");
                }
                catch (System.Exception e)
                {
                    LogError($"刷新手牌数据失败: {e.Message}");

                    // 如果玩家状态不存在，尝试创建
                    if (e.Message.Contains("状态不存在"))
                    {
                        LogDebug("尝试初始化玩家状态");
                        playerCardManager.InitializePlayer(myPlayerId, $"Player{myPlayerId}");
                    }
                }
            }
            else
            {
                LogWarning($"无法刷新手牌 - PlayerCardManager: {playerCardManager != null}, MyPlayerId: {myPlayerId}");
            }
        }

        /// <summary>
        /// 根据ID获取卡牌数据
        /// </summary>
        private CardData GetCardDataById(int cardId)
        {
            return cardConfig?.GetCardById(cardId);
        }

        #endregion

        #region 状态管理

        /// <summary>
        /// 设置UI状态
        /// </summary>
        private void SetUIState(UIState newState)
        {
            if (currentUIState == newState) return;

            UIState oldState = currentUIState;
            currentUIState = newState;

            LogDebug($"UI状态切换: {oldState} -> {newState}");
        }

        /// <summary>
        /// 显示缩略图
        /// </summary>
        private void ShowThumbnail()
        {
            if (cardDisplayUI != null && currentHandCards.Count > 0)
            {
                cardDisplayUI.ShowCardDisplay(currentHandCards);
                SetUIState(UIState.Thumbnail);
            }
            else
            {
                LogWarning($"无法显示缩略图 - cardDisplayUI: {cardDisplayUI != null}, 手牌数量: {currentHandCards.Count}");
            }
        }

        /// <summary>
        /// 更新UI可用性
        /// </summary>
        private void UpdateUIAvailability()
        {
            canUseCards = !isMyTurn && isInitialized;

            LogDebug($"UI可用性更新 - 我的回合: {isMyTurn}, 可用卡牌: {canUseCards}, 已初始化: {isInitialized}");
        }

        #endregion

        #region 资源清理

        /// <summary>
        /// 清理UI资源
        /// </summary>
        private void CleanupUI()
        {
            LogDebug("清理UI资源");

            // 停止等待摄像机的协程
            if (cameraWaitCoroutine != null)
            {
                StopCoroutine(cameraWaitCoroutine);
                cameraWaitCoroutine = null;
            }

            // 取消事件订阅
            UnsubscribeFromEvents();

            // 清理箭头状态
            if (currentUIState == UIState.ArrowTargeting)
            {
                CancelArrowTargeting();
            }

            // 清理按压状态
            if (isWaitingForHold)
            {
                CancelCardPress();
            }

            // 清理数据
            currentHandCards.Clear();

            LogDebug("UI资源清理完成");
        }

        #endregion

        #region 工具方法

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[CardUIManager] {message}");
            }
        }

        private void LogWarning(string message)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[CardUIManager] {message}");
            }
        }

        private void LogError(string message)
        {
            Debug.LogError($"[CardUIManager] {message}");
        }

        #endregion
    }
}