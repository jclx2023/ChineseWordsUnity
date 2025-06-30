using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Cards.Core;
using Cards.Player;
using Core.Network;

namespace Cards.UI
{
    /// <summary>
    /// 卡牌UI管理器 - 调度中心
    /// 负责统一管理卡牌UI系统，协调各模块行为，处理输入和状态管理
    /// 支持事件驱动的初始化和数据同步
    /// </summary>
    public class CardUIManager : MonoBehaviour
    {
        [Header("Canvas设置")]
        [SerializeField] private Canvas cardUICanvas; // 独立的卡牌UI Canvas
        [SerializeField] private int canvasSortingOrder = 105; // Canvas层级

        [Header("组件引用（可选，留空则自动查找/创建）")]
        [SerializeField] private CardUIComponents cardUIComponents; // 组件工厂（可选）
        [SerializeField] private CardDisplayUI cardDisplayUI; // 展示控制器（可选）

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

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool showReleaseArea = false; // 显示释放区域

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
        private bool isWaitingForSystemReady = true;
        private bool isMyTurn = false;
        private bool canUseCards = true;

        // 卡牌数据
        private List<CardDisplayData> currentHandCards = new List<CardDisplayData>();
        private GameObject draggedCard = null;
        private CardData pendingCardData = null; // 等待目标选择的卡牌
        private int myPlayerId = -1;

        // 拖拽相关（TODO: 箭头绘制）
        private bool isDragging = false;
        private Vector3 dragStartPosition;

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
            if (Instance == null)
            {
                Instance = this;
                InitializeManager();
            }
            else
            {
                LogDebug("发现重复的CardUIManager实例，销毁当前实例");
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // 订阅系统事件并等待初始化
            SubscribeToSystemEvents();
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        private void Update()
        {
            if (!isInitialized) return;

            HandleInput();
            HandleDragging();
        }

        #endregion

        #region 事件驱动初始化

        /// <summary>
        /// 订阅系统事件
        /// </summary>
        private void SubscribeToSystemEvents()
        {
            LogDebug("订阅CardSystemManager事件");

            // 订阅UI系统就绪事件
            CardSystemManager.OnUISystemReady += OnUISystemReady;

            // 订阅系统初始化完成事件
            CardSystemManager.OnSystemInitialized += OnSystemInitialized;

            // 订阅玩家数据就绪事件
            CardSystemManager.OnPlayerDataReady += OnPlayerDataReady;
        }

        /// <summary>
        /// UI系统就绪事件处理
        /// </summary>
        private void OnUISystemReady()
        {
            LogDebug("收到UI系统就绪事件，开始初始化UI管理器");
            isWaitingForSystemReady = false;

            StartCoroutine(DelayedInitialization());
        }

        /// <summary>
        /// 系统初始化完成事件处理
        /// </summary>
        private void OnSystemInitialized(bool success)
        {
            if (success)
            {
                LogDebug("CardSystemManager初始化成功");

                // 如果还在等待系统就绪，尝试初始化
                if (isWaitingForSystemReady)
                {
                    LogDebug("尝试立即初始化（可能错过了UI就绪事件）");
                    isWaitingForSystemReady = false;
                    StartCoroutine(DelayedInitialization());
                }
            }
            else
            {
                LogError("CardSystemManager初始化失败，UI管理器无法正常工作");
            }
        }

        /// <summary>
        /// 玩家数据就绪事件处理
        /// </summary>
        private void OnPlayerDataReady(int playerId)
        {
            LogDebug($"玩家{playerId}数据就绪，刷新手牌显示");

            // 确定我的玩家ID
            if (myPlayerId == -1 && NetworkManager.Instance != null)
            {
                myPlayerId = NetworkManager.Instance.ClientId;
            }

            // 如果是我的数据，刷新显示
            if (playerId == myPlayerId)
            {
                RefreshPlayerCardDisplay(playerId);
            }
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化管理器
        /// </summary>
        private void InitializeManager()
        {
            // 设置Canvas
            SetupCanvas();

            LogDebug("CardUIManager基础初始化完成，等待系统就绪");
        }

        /// <summary>
        /// 延迟初始化
        /// </summary>
        private IEnumerator DelayedInitialization()
        {
            // 等待一帧确保所有系统完全就绪
            yield return null;

            // 查找或创建组件
            FindOrCreateComponents();

            // 验证系统依赖
            if (!ValidateSystemDependencies())
            {
                LogError("系统依赖验证失败，将在3秒后重试");
                yield return new WaitForSeconds(3f);

                // 重试验证
                if (!ValidateSystemDependencies())
                {
                    LogError("系统依赖验证仍然失败，CardUIManager无法正常工作");
                    yield break;
                }
            }

            // 获取我的玩家ID
            if (NetworkManager.Instance != null)
            {
                myPlayerId = NetworkManager.Instance.ClientId;
                LogDebug($"确定我的玩家ID: {myPlayerId}");
            }

            // 初始化完成
            isInitialized = true;
            LogDebug("CardUIManager完全初始化完成");

            // 直接显示缩略图（不再需要按E键）
            RefreshAndShowThumbnail();
        }

        /// <summary>
        /// 设置Canvas
        /// </summary>
        private void SetupCanvas()
        {
            if (cardUICanvas == null)
            {
                // 创建独立Canvas
                GameObject canvasObject = new GameObject("CardUICanvas");
                canvasObject.transform.SetParent(transform, false);

                cardUICanvas = canvasObject.AddComponent<Canvas>();
                cardUICanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                cardUICanvas.sortingOrder = canvasSortingOrder;

                // 添加CanvasScaler用于适配
                CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                // 添加GraphicRaycaster用于UI交互
                canvasObject.AddComponent<GraphicRaycaster>();

                LogDebug("创建了独立的CardUICanvas");
            }
            else
            {
                // 设置现有Canvas
                cardUICanvas.sortingOrder = canvasSortingOrder;
            }
        }

        /// <summary>
        /// 查找或创建组件
        /// </summary>
        private void FindOrCreateComponents()
        {
            // 优先从CardSystemManager获取组件
            var cardSystemManager = CardSystemManager.Instance;
            if (cardSystemManager != null)
            {
                cardUIComponents = cardSystemManager.GetCardUIComponents();
                LogDebug("从CardSystemManager获取CardUIComponents");
            }

            // 如果没有获取到，查找或创建
            if (cardUIComponents == null)
            {
                cardUIComponents = CardUIComponents.Instance;
                if (cardUIComponents == null)
                {
                    cardUIComponents = FindObjectOfType<CardUIComponents>();
                    if (cardUIComponents == null)
                    {
                        GameObject componentsObject = new GameObject("CardUIComponents");
                        componentsObject.transform.SetParent(transform, false);
                        cardUIComponents = componentsObject.AddComponent<CardUIComponents>();
                        LogDebug("创建了新的CardUIComponents实例");
                    }
                    else
                    {
                        LogDebug("找到了场景中的CardUIComponents实例");
                    }
                }
                else
                {
                    LogDebug("使用全局CardUIComponents单例");
                }
            }

            // 查找或创建CardDisplayUI
            if (cardDisplayUI == null)
            {
                cardDisplayUI = GetComponentInChildren<CardDisplayUI>();
                if (cardDisplayUI == null)
                {
                    GameObject displayObject = new GameObject("CardDisplayUI");
                    displayObject.transform.SetParent(cardUICanvas.transform, false);
                    cardDisplayUI = displayObject.AddComponent<CardDisplayUI>();
                    LogDebug("创建了新的CardDisplayUI实例");
                }
                else
                {
                    LogDebug("找到了子对象中的CardDisplayUI实例");
                }
            }
        }

        /// <summary>
        /// 验证系统依赖
        /// </summary>
        private bool ValidateSystemDependencies()
        {
            bool isValid = true;

            if (cardUIComponents == null)
            {
                LogError("CardUIComponents未找到");
                isValid = false;
            }

            if (cardDisplayUI == null)
            {
                LogError("CardDisplayUI未找到");
                isValid = false;
            }

            if (CardSystemManager.Instance == null)
            {
                LogError("CardSystemManager未找到");
                isValid = false;
            }
            else if (!CardSystemManager.Instance.IsSystemReady())
            {
                LogError("CardSystemManager未就绪");
                isValid = false;
            }

            return isValid;
        }

        #endregion

        #region 事件订阅

        /// <summary>
        /// 订阅事件
        /// </summary>
        private void SubscribeToEvents()
        {
            // 订阅回合变化事件
            NetworkManager.OnPlayerTurnChanged += OnPlayerTurnChanged;

            // 订阅卡牌显示UI事件
            if (cardDisplayUI != null)
            {
                cardDisplayUI.OnCardSelected += OnCardSelected;
                cardDisplayUI.OnCardHoverEnter += OnCardHoverEnter;
                cardDisplayUI.OnCardHoverExit += OnCardHoverExit;
            }

            // 订阅卡牌系统事件
            var playerCardManager = CardSystemManager.GetPlayerCardManagerStatic();
            if (playerCardManager != null)
            {
                playerCardManager.OnHandSizeChanged += OnHandSizeChanged;
                playerCardManager.OnCardAcquired += OnCardAcquired;
                playerCardManager.OnCardUsed += OnCardUsed;
            }

            LogDebug("事件订阅完成");
        }

        /// <summary>
        /// 取消订阅事件
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            // 取消系统事件订阅
            CardSystemManager.OnUISystemReady -= OnUISystemReady;
            CardSystemManager.OnSystemInitialized -= OnSystemInitialized;
            CardSystemManager.OnPlayerDataReady -= OnPlayerDataReady;

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

            // 取消订阅卡牌系统事件
            var playerCardManager = CardSystemManager.GetPlayerCardManagerStatic();
            if (playerCardManager != null)
            {
                playerCardManager.OnHandSizeChanged -= OnHandSizeChanged;
                playerCardManager.OnCardAcquired -= OnCardAcquired;
                playerCardManager.OnCardUsed -= OnCardUsed;
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
            // 检查是否是我的回合
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
                    RefreshAndShowThumbnail();
                }
            }

            // 更新UI状态
            UpdateUIAvailability();
        }

        /// <summary>
        /// 处理手牌数量变化
        /// </summary>
        private void OnHandSizeChanged(int playerId, int newHandSize)
        {
            if (playerId == myPlayerId)
            {
                LogDebug($"我的手牌数量变化: {newHandSize}");
                RefreshPlayerCardDisplay(playerId);
            }
        }

        /// <summary>
        /// 处理卡牌获得事件
        /// </summary>
        private void OnCardAcquired(int playerId, int cardId, string cardName)
        {
            if (playerId == myPlayerId)
            {
                LogDebug($"我获得了卡牌: {cardName}");
                RefreshPlayerCardDisplay(playerId);
            }
        }

        /// <summary>
        /// 处理卡牌使用事件
        /// </summary>
        private void OnCardUsed(int playerId, int cardId, int targetPlayerId)
        {
            if (playerId == myPlayerId)
            {
                LogDebug($"我使用了卡牌: {cardId}");
                RefreshPlayerCardDisplay(playerId);
            }
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
            // 实现悬停提示
            ShowCardTooltip(cardUI);
        }

        /// <summary>
        /// 处理卡牌悬停离开
        /// </summary>
        private void OnCardHoverExit(GameObject cardUI)
        {
            // 隐藏悬停提示
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
                        RefreshAndShowThumbnail();
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
            // 根据卡牌的目标类型判断
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

            // 显示目标选择提示
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

            // TODO: 显示实际的UI面板，这里先用Debug
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

            // 隐藏目标选择UI
            HideTargetSelectionUI();
        }

        /// <summary>
        /// 隐藏目标选择UI
        /// </summary>
        private void HideTargetSelectionUI()
        {
            // TODO: 隐藏实际的UI面板
        }

        /// <summary>
        /// 验证目标
        /// </summary>
        private bool ValidateTarget(CardData cardData, int targetPlayerId)
        {
            // 基础验证
            if (cardData.targetType == TargetType.Self && targetPlayerId != myPlayerId)
            {
                return false;
            }

            if (cardData.targetType == TargetType.SinglePlayer)
            {
                // 验证目标玩家是否存在且存活
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
            // 简化实现，实际应该检查玩家是否存在且存活
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

            // 切换到拖拽状态
            SetUIState(UIState.Dragging);

            // TODO: 创建拖拽箭头

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

            // TODO: 更新拖拽箭头位置
            UpdateDragArrow();
        }

        /// <summary>
        /// 更新拖拽箭头 (TODO)
        /// </summary>
        private void UpdateDragArrow()
        {
            // TODO: 实现箭头绘制逻辑
            // 箭头从卡牌位置以曲线形式指向鼠标位置
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

            // TODO: 销毁拖拽箭头

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
            var playerCardManager = CardSystemManager.GetPlayerCardManagerStatic();
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
            // 使用CardSystemManager的验证逻辑
            var cardSystemManager = CardSystemManager.Instance;
            if (cardSystemManager?.GetCardConfig() == null)
            {
                LogError("CardSystemManager或配置不可用");
                return false;
            }

            // 查找卡牌数据
            var cardData = GetCardDataById(cardId);
            if (cardData == null)
            {
                LogError($"未找到卡牌数据: {cardId}");
                return false;
            }

            // 检查游戏状态
            if (!Cards.Core.CardUtilities.Validator.ValidateGameState(true))
            {
                LogDebug("游戏状态验证失败");
                return false;
            }

            // 检查回合状态
            if (isMyTurn)
            {
                LogDebug("我的回合时无法使用卡牌");
                return false;
            }

            // 检查玩家手牌中是否有这张卡
            var playerCardManager = CardSystemManager.GetPlayerCardManagerStatic();
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

            // TODO: 显示实际的提示UI，这里先用Debug
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
        /// 显示卡牌UI（从外部调用）
        /// </summary>
        public void ShowCardUI()
        {
            LogDebug("显示卡牌UI");

            // 刷新手牌数据
            RefreshHandCards();

            // 显示缩略图
            ShowThumbnail();

            // 触发事件
            OnCardUIOpened?.Invoke();
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

            // 设置回缩略图状态而不是Hidden
            SetUIState(UIState.Thumbnail);
            RefreshAndShowThumbnail();

            // 触发事件
            OnCardUIClosed?.Invoke();
        }

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
        /// 刷新玩家卡牌显示（供CardSystemManager调用）
        /// </summary>
        public void RefreshPlayerCardDisplay(int playerId)
        {
            if (playerId != myPlayerId) return;

            LogDebug($"刷新玩家{playerId}的卡牌显示");

            // 只有在显示状态时才刷新
            if (IsCardUIVisible)
            {
                RefreshHandCards();

                // 根据当前状态刷新对应的显示
                switch (currentUIState)
                {
                    case UIState.Thumbnail:
                        ShowThumbnail();
                        break;
                    case UIState.FanDisplay:
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

            // 如果当前有展示，先隐藏
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
                RefreshAndShowThumbnail();
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

            var playerCardManager = CardSystemManager.GetPlayerCardManagerStatic();
            if (playerCardManager != null && myPlayerId != -1)
            {
                try
                {
                    // 获取真实的手牌数据
                    var playerHand = playerCardManager.GetPlayerHand(myPlayerId);
                    var cardConfig = CardSystemManager.GetCardConfigStatic();

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
        /// 刷新并显示缩略图
        /// </summary>
        private void RefreshAndShowThumbnail()
        {
            RefreshHandCards();
            ShowThumbnail();
        }

        /// <summary>
        /// 根据ID获取卡牌数据
        /// </summary>
        private CardData GetCardDataById(int cardId)
        {
            var cardConfig = CardSystemManager.GetCardConfigStatic();
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
            canUseCards = !isMyTurn && isInitialized; // 非回合时且已初始化才可以使用卡牌

            LogDebug($"UI可用性更新 - 我的回合: {isMyTurn}, 可用卡牌: {canUseCards}, 已初始化: {isInitialized}");
        }

        #endregion

        #region 调试工具

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

        #region 清理资源

        private void OnDestroy()
        {
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

            // 清理实例引用
            if (Instance == this)
            {
                Instance = null;
            }

            LogDebug("CardUIManager已销毁，资源已清理");
        }

        #endregion
    }
}