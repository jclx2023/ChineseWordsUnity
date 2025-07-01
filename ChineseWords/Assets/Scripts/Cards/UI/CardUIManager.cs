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
    /// 卡牌UI管理器 - Bug修复版
    /// 修复：1.摄像机查找时机问题 2.Canvas挂载问题 3.频繁刷新问题
    /// </summary>
    public class CardUIManager : MonoBehaviour, IUISystemDependencyInjection
    {
        [Header("Canvas设置")]
        [SerializeField] private Canvas cardUICanvas; // 独立的卡牌UI Canvas
        [SerializeField] private int canvasSortingOrder = 105; // Canvas层级

        [Header("输入设置")]
        [SerializeField] private KeyCode cardDisplayTriggerKey = KeyCode.E; // 卡牌展示触发键

        [Header("拖拽设置 - 释放区域（屏幕百分比）")]
        [SerializeField] private float releaseAreaCenterX = 0.5f; // 50%
        [SerializeField] private float releaseAreaCenterY = 0.5f; // 50%
        [SerializeField] private float releaseAreaWidth = 0.3f; // 30%
        [SerializeField] private float releaseAreaHeight = 0.3f; // 30%

        [Header("目标选择设置")]
        [SerializeField] private KeyCode targetSelfKey = KeyCode.Alpha1;      // 目标自己
        [SerializeField] private KeyCode targetPlayer2Key = KeyCode.Alpha2;   // 目标玩家2
        [SerializeField] private KeyCode targetPlayer3Key = KeyCode.Alpha3;   // 目标玩家3
        [SerializeField] private KeyCode targetPlayer4Key = KeyCode.Alpha4;   // 目标玩家4
        [SerializeField] private KeyCode targetAllKey = KeyCode.Alpha0;       // 目标所有玩家

        [Header("防抖设置")]
        [SerializeField] private float refreshCooldown = 0.1f; // 刷新冷却时间

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 单例实例
        public static CardUIManager Instance { get; private set; }

        // UI状态
        public enum UIState
        {
            Thumbnail,        // 缩略图状态（默认状态）
            FanDisplay,       // 扇形展示状态
            Dragging,         // 拖拽状态
            TargetSelection,  // 目标选择状态
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
        private GameObject draggedCard = null;
        private CardData pendingCardData = null; // 等待目标选择的卡牌
        private int myPlayerId = -1;

        // 拖拽相关
        private bool isDragging = false;
        private Vector3 dragStartPosition;

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

            // 如果依赖已注入，开始初始化
            StartCoroutine(DelayedInitialization());
        }

        private void Update()
        {
            if (!isInitialized) return;

            HandleInput();
            HandleDragging();
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
            LogDebug("开始依赖注入");

            this.cardEffectSystem = effectSystem;
            this.playerCardManager = playerCardManager;
            this.cardConfig = cardConfig;
            this.cardNetworkManager = networkManager;
            this.cardUIComponents = uiComponents;

            isDependencyInjected = true;

            LogDebug("依赖注入完成");

            // 如果Start已经被调用，立即开始初始化
            if (gameObject.activeInHierarchy)
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

            StartCoroutine(DelayedInitialization());
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 延迟初始化
        /// </summary>
        private IEnumerator DelayedInitialization()
        {
            if (isInitialized)
            {
                LogDebug("已经初始化，跳过重复初始化");
                yield break;
            }

            LogDebug("开始延迟初始化");

            // 等待一帧确保所有系统完全就绪
            yield return null;

            // 验证依赖
            if (!ValidateDependencies())
            {
                LogError("依赖验证失败，无法初始化UI");
                yield break;
            }

            // 设置Canvas
            SetupCanvas();

            // 创建或查找CardDisplayUI
            SetupCardDisplayUI();

            // 初始化CardDisplayUI（可能没有摄像机）
            InitializeCardDisplayUI();

            // 获取我的玩家ID
            GetMyPlayerId();

            // 订阅事件
            SubscribeToEvents();

            // 初始化完成
            isInitialized = true;
            LogDebug("CardUIManager初始化完成");

            // 直接显示缩略图
            RefreshAndShowThumbnailWithDebounce();
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
        /// 验证依赖
        /// </summary>
        private bool ValidateDependencies()
        {
            bool isValid = true;

            if (cardConfig == null)
            {
                LogError("CardConfig未注入");
                isValid = false;
            }

            if (playerCardManager == null)
            {
                LogError("PlayerCardManager未注入");
                isValid = false;
            }

            if (cardUIComponents == null)
            {
                LogError("CardUIComponents未注入");
                isValid = false;
            }

            return isValid;
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
                LogDebug("找到了子对象中的CardDisplayUI实例");

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
            else
            {
                LogWarning("NetworkManager不可用，无法确定玩家ID");
                myPlayerId = 1; // 默认值
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
                cardDisplayUI.OnCardSelected += OnCardSelected;
                cardDisplayUI.OnCardHoverEnter += OnCardHoverEnter;
                cardDisplayUI.OnCardHoverExit += OnCardHoverExit;
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
                cardDisplayUI.OnCardSelected -= OnCardSelected;
                cardDisplayUI.OnCardHoverEnter -= OnCardHoverEnter;
                cardDisplayUI.OnCardHoverExit -= OnCardHoverExit;
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

            UpdateUIAvailability();
        }

        /// <summary>
        /// 处理卡牌选择事件
        /// </summary>
        private void OnCardSelected(GameObject cardUI)
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

            // 检查是否需要目标选择
            if (NeedsTargetSelection(cardData))
            {
                StartTargetSelection(cardUI, cardData);
            }
            else
            {
                // 开始拖拽
                StartDragging(cardUI);
            }
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

            // ESC键关闭卡牌UI
            if (Input.GetKeyDown(KeyCode.Escape) && IsCardUIVisible)
            {
                HideCardUI();
            }

            // 目标选择输入处理
            if (currentUIState == UIState.TargetSelection)
            {
                HandleTargetSelectionInput();
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

                case UIState.TargetSelection:
                    CancelTargetSelection();
                    break;

                case UIState.Disabled:
                    LogDebug("卡牌UI当前被禁用");
                    break;
            }
        }

        /// <summary>
        /// 处理目标选择输入
        /// </summary>
        private void HandleTargetSelectionInput()
        {
            int targetPlayerId = -1;

            if (Input.GetKeyDown(targetSelfKey))
            {
                targetPlayerId = myPlayerId;
            }
            else if (Input.GetKeyDown(targetPlayer2Key))
            {
                targetPlayerId = 2;
            }
            else if (Input.GetKeyDown(targetPlayer3Key))
            {
                targetPlayerId = 3;
            }
            else if (Input.GetKeyDown(targetPlayer4Key))
            {
                targetPlayerId = 4;
            }
            else if (Input.GetKeyDown(targetAllKey))
            {
                targetPlayerId = 0; // 特殊值表示所有玩家
            }

            if (targetPlayerId != -1)
            {
                CompleteTargetSelection(targetPlayerId);
            }
        }

        #endregion

        #region 目标选择系统

        /// <summary>
        /// 检查是否需要目标选择
        /// </summary>
        private bool NeedsTargetSelection(CardData cardData)
        {
            return cardData.targetType == TargetType.SinglePlayer ||
                   cardData.targetType == TargetType.AllPlayers;
        }

        /// <summary>
        /// 开始目标选择
        /// </summary>
        private void StartTargetSelection(GameObject cardUI, CardData cardData)
        {
            pendingCardData = cardData;
            draggedCard = cardUI;

            SetUIState(UIState.TargetSelection);

            LogDebug($"开始目标选择 - 卡牌: {cardData.cardName}");
            ShowTargetSelectionUI(cardData);
        }

        /// <summary>
        /// 显示目标选择UI
        /// </summary>
        private void ShowTargetSelectionUI(CardData cardData)
        {
            string message = $"请选择 {cardData.cardName} 的目标：\n";

            if (cardData.targetType == TargetType.SinglePlayer)
            {
                message += "1-自己  2-玩家2  3-玩家3  4-玩家4";
            }
            else if (cardData.targetType == TargetType.AllPlayers)
            {
                message += "0-所有玩家";
            }

            message += "\nESC-取消";

            LogDebug(message);
        }

        /// <summary>
        /// 完成目标选择
        /// </summary>
        private void CompleteTargetSelection(int targetPlayerId)
        {
            if (pendingCardData == null)
            {
                LogError("没有待处理的卡牌数据");
                return;
            }

            // 验证目标是否有效
            if (!ValidateTarget(pendingCardData, targetPlayerId))
            {
                LogWarning($"无效的目标: {targetPlayerId}");
                return;
            }

            LogDebug($"目标选择完成 - 卡牌: {pendingCardData.cardName}, 目标: {targetPlayerId}");

            // 使用卡牌
            ExecuteCardUsage(pendingCardData.cardId, targetPlayerId);

            // 清理目标选择状态
            ClearTargetSelection();
        }

        /// <summary>
        /// 取消目标选择
        /// </summary>
        private void CancelTargetSelection()
        {
            LogDebug("取消目标选择");
            ClearTargetSelection();
            SetUIState(UIState.FanDisplay);
        }

        /// <summary>
        /// 清理目标选择状态
        /// </summary>
        private void ClearTargetSelection()
        {
            pendingCardData = null;
            draggedCard = null;
        }

        /// <summary>
        /// 验证目标
        /// </summary>
        private bool ValidateTarget(CardData cardData, int targetPlayerId)
        {
            if (cardData.targetType == TargetType.Self && targetPlayerId != myPlayerId)
            {
                return false;
            }

            if (cardData.targetType == TargetType.SinglePlayer)
            {
                return IsValidPlayerTarget(targetPlayerId);
            }

            if (cardData.targetType == TargetType.AllPlayers && targetPlayerId != 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 检查是否为有效的玩家目标
        /// </summary>
        private bool IsValidPlayerTarget(int playerId)
        {
            return playerId >= 1 && playerId <= 4;
        }

        #endregion

        #region 拖拽处理

        /// <summary>
        /// 开始拖拽
        /// </summary>
        private void StartDragging(GameObject cardUI)
        {
            if (cardUI == null) return;

            draggedCard = cardUI;
            isDragging = true;
            dragStartPosition = cardUI.transform.position;

            SetUIState(UIState.Dragging);

            LogDebug($"开始拖拽卡牌: {cardUI.name}");
        }

        /// <summary>
        /// 处理拖拽
        /// </summary>
        private void HandleDragging()
        {
            if (!isDragging || draggedCard == null) return;

            // 鼠标抬起时结束拖拽
            if (Input.GetMouseButtonUp(0))
            {
                EndDragging();
                return;
            }
        }

        /// <summary>
        /// 结束拖拽
        /// </summary>
        private void EndDragging()
        {
            if (!isDragging || draggedCard == null) return;

            // 检查释放位置
            bool isInReleaseArea = IsMouseInReleaseArea();

            if (isInReleaseArea)
            {
                // 获取卡牌数据
                CardUIIdentifier identifier = draggedCard.GetComponent<CardUIIdentifier>();
                if (identifier != null)
                {
                    // 在释放区域内，使用卡牌（无目标或自目标）
                    ExecuteCardUsage(identifier.cardId, myPlayerId);
                }
            }
            else
            {
                LogDebug("拖拽释放位置无效，取消使用卡牌");
            }

            // 清理拖拽状态
            CleanupDragging();
        }

        /// <summary>
        /// 清理拖拽状态
        /// </summary>
        private void CleanupDragging()
        {
            isDragging = false;
            draggedCard = null;

            // 返回到扇形展示状态
            SetUIState(UIState.FanDisplay);

            LogDebug("拖拽状态已清理");
        }

        /// <summary>
        /// 检查鼠标是否在释放区域内
        /// </summary>
        private bool IsMouseInReleaseArea()
        {
            Vector2 mouseScreenPos = Input.mousePosition;
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);

            // 转换为屏幕百分比
            Vector2 mousePercent = new Vector2(mouseScreenPos.x / screenSize.x, mouseScreenPos.y / screenSize.y);

            // 计算释放区域边界
            float leftBound = releaseAreaCenterX - releaseAreaWidth / 2f;
            float rightBound = releaseAreaCenterX + releaseAreaWidth / 2f;
            float bottomBound = releaseAreaCenterY - releaseAreaHeight / 2f;
            float topBound = releaseAreaCenterY + releaseAreaHeight / 2f;

            // 检查是否在区域内
            bool inArea = mousePercent.x >= leftBound && mousePercent.x <= rightBound &&
                         mousePercent.y >= bottomBound && mousePercent.y <= topBound;

            return inArea;
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
                LogWarning($"卡牌 {cardData?.cardName ?? cardId.ToString()} 使用条件不满足");
                return;
            }

            // 通过PlayerCardManager使用卡牌
            if (playerCardManager != null)
            {
                bool success = playerCardManager.UseCard(myPlayerId, cardId, targetPlayerId);
                if (success)
                {
                    LogDebug($"卡牌使用成功: {cardId}");

                    // 触发使用请求事件（供其他系统监听）
                    OnCardUseRequested?.Invoke(cardId, targetPlayerId);

                    // 关闭卡牌UI
                    HideCardUI();
                }
                else
                {
                    LogError($"卡牌使用失败: {cardId}");
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
            LogDebug($"卡牌提示: {tooltip}");
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

            // 清理各种状态
            if (isDragging)
            {
                CleanupDragging();
            }

            if (currentUIState == UIState.TargetSelection)
            {
                ClearTargetSelection();
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
                            cardDisplayUI.ShowFanDisplayWithCards(currentHandCards);
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

        /// <summary>
        /// 强制禁用卡牌UI（答题期间调用）
        /// </summary>
        public void DisableCardUI()
        {
            LogDebug("强制禁用卡牌UI");

            if (currentUIState == UIState.FanDisplay)
            {
                if (cardDisplayUI != null)
                {
                    cardDisplayUI.HideCardDisplay();
                }
            }

            SetUIState(UIState.Disabled);
        }

        /// <summary>
        /// 启用卡牌UI
        /// </summary>
        public void EnableCardUI()
        {
            LogDebug("启用卡牌UI");

            if (currentUIState == UIState.Disabled)
            {
                SetUIState(UIState.Thumbnail);
                RefreshAndShowThumbnailWithDebounce();
                UpdateUIAvailability();
            }
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

            // 清理各种状态
            if (isDragging)
            {
                CleanupDragging();
            }

            if (currentUIState == UIState.TargetSelection)
            {
                ClearTargetSelection();
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