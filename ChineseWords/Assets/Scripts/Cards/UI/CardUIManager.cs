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

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool showReleaseArea = false; // 显示释放区域

        // 单例实例
        public static CardUIManager Instance { get; private set; }

        // UI状态
        public enum UIState
        {
            Hidden,           // 隐藏状态
            Thumbnail,        // 缩略图状态
            FanDisplay,       // 扇形展示状态
            Dragging,         // 拖拽状态
            Disabled          // 禁用状态（答题期间）
        }

        private UIState currentUIState = UIState.Hidden;
        private bool isInitialized = false;
        private bool isMyTurn = false;
        private bool canUseCards = true;

        // 卡牌数据
        private List<CardDisplayData> currentHandCards = new List<CardDisplayData>();
        private GameObject draggedCard = null;
        private Vector3 dragOffset = Vector3.zero;

        // 拖拽相关（TODO: 箭头绘制）
        private bool isDragging = false;
        private Vector3 dragStartPosition;

        // 事件
        public System.Action<int, int> OnCardUseRequested; // cardId, targetPlayerId
        public System.Action OnCardUIOpened;
        public System.Action OnCardUIClosed;

        // 属性
        public UIState CurrentState => currentUIState;
        public bool IsCardUIVisible => currentUIState != UIState.Hidden && currentUIState != UIState.Disabled;
        public bool CanOpenCardUI => currentUIState == UIState.Hidden && !isMyTurn && canUseCards;

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
            StartCoroutine(DelayedInitialization());
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
            HandleInput();
            HandleDragging();
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

            // 查找或创建组件
            FindOrCreateComponents();

            LogDebug("CardUIManager初始化开始");
        }

        /// <summary>
        /// 延迟初始化
        /// </summary>
        private IEnumerator DelayedInitialization()
        {
            // 等待其他系统初始化完成
            yield return new WaitForSeconds(0.5f);

            // 验证系统依赖
            if (!ValidateSystemDependencies())
            {
                LogError("系统依赖验证失败，CardUIManager无法正常工作");
                yield break;
            }

            // 初始化完成
            isInitialized = true;
            LogDebug("CardUIManager初始化完成");

            // 显示缩略图
            ShowThumbnail();
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
            // 查找CardUIComponents（优先使用全局单例）
            if (cardUIComponents == null)
            {
                cardUIComponents = CardUIComponents.Instance;
                if (cardUIComponents == null)
                {
                    // 如果没有全局实例，查找或创建本地实例
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
            // 检查是否是我的回合
            bool wasMyTurn = isMyTurn;
            isMyTurn = (NetworkManager.Instance != null && NetworkManager.Instance.ClientId == newTurnPlayerId);

            LogDebug($"回合变化 - 当前玩家: {newTurnPlayerId}, 是我的回合: {isMyTurn}");

            // 如果轮到我的回合，自动关闭卡牌UI
            if (isMyTurn && IsCardUIVisible)
            {
                LogDebug("轮到我的回合，自动关闭卡牌UI");
                HideCardUI();
            }

            // 更新UI状态
            UpdateUIAvailability();
        }

        /// <summary>
        /// 处理卡牌选择事件
        /// </summary>
        private void OnCardSelected(GameObject cardUI)
        {
            if (currentUIState != UIState.FanDisplay) return;

            // 开始拖拽
            StartDragging(cardUI);
        }

        /// <summary>
        /// 处理卡牌悬停进入
        /// </summary>
        private void OnCardHoverEnter(GameObject cardUI)
        {
            // TODO: 可以添加悬停提示信息
            LogDebug($"卡牌悬停进入: {cardUI.name}");
        }

        /// <summary>
        /// 处理卡牌悬停离开
        /// </summary>
        private void OnCardHoverExit(GameObject cardUI)
        {
            LogDebug($"卡牌悬停离开: {cardUI.name}");
        }

        #endregion

        #region 输入处理

        /// <summary>
        /// 处理输入
        /// </summary>
        private void HandleInput()
        {
            if (!isInitialized) return;

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
        }

        /// <summary>
        /// 处理卡牌展示切换
        /// </summary>
        private void HandleCardDisplayToggle()
        {
            switch (currentUIState)
            {
                case UIState.Hidden:
                    if (CanOpenCardUI)
                    {
                        ShowCardUI();
                    }
                    else
                    {
                        LogDebug($"无法打开卡牌UI - 我的回合: {isMyTurn}, 可用状态: {canUseCards}");
                    }
                    break;

                case UIState.Thumbnail:
                    ShowFanDisplay();
                    break;

                case UIState.FanDisplay:
                    HideCardUI();
                    break;

                case UIState.Disabled:
                    LogDebug("卡牌UI当前被禁用");
                    break;
            }
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

            // 计算鼠标偏移
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            dragOffset = cardUI.transform.position - mouseWorldPos;

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
            // 这里暂时不移动卡牌本身，只绘制箭头
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
                // 在释放区域内，尝试使用卡牌
                TryUseCard(draggedCard);
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
            dragOffset = Vector3.zero;

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

            LogDebug($"鼠标位置检查 - 百分比: {mousePercent}, 在释放区域内: {inArea}");
            return inArea;
        }

        #endregion

        #region 卡牌使用

        /// <summary>
        /// 尝试使用卡牌
        /// </summary>
        private void TryUseCard(GameObject cardUI)
        {
            if (cardUI == null) return;

            // 获取卡牌ID
            CardUIIdentifier identifier = cardUI.GetComponent<CardUIIdentifier>();
            if (identifier == null)
            {
                LogError("卡牌UI缺少标识组件，无法使用");
                return;
            }

            int cardId = identifier.cardId;
            LogDebug($"尝试使用卡牌: {identifier.cardName} (ID: {cardId})");

            // 验证卡牌使用条件
            if (!ValidateCardUsage(cardId))
            {
                LogWarning($"卡牌 {identifier.cardName} 使用条件不满足");
                return;
            }

            // TODO: 如果是指向型卡牌，需要选择目标
            // 目前默认使用 -1 作为目标（自发型卡牌或无目标）
            int targetPlayerId = -1;

            // 触发卡牌使用请求
            OnCardUseRequested?.Invoke(cardId, targetPlayerId);

            LogDebug($"卡牌使用请求已发送: {identifier.cardName}");

            // 关闭卡牌UI
            HideCardUI();
        }

        /// <summary>
        /// 验证卡牌使用条件
        /// </summary>
        private bool ValidateCardUsage(int cardId)
        {
            // 使用CardUtilities中的验证逻辑
            if (CardSystemManager.Instance?.cardConfig == null)
            {
                LogError("CardSystemManager或配置不可用");
                return false;
            }

            // 查找卡牌数据
            var cardData = CardSystemManager.Instance.cardConfig.AllCards.Find(c => c.cardId == cardId);
            if (cardData == null)
            {
                LogError($"未找到卡牌数据: {cardId}");
                return false;
            }

            // 获取玩家状态（这里简化处理，实际应该从PlayerCardManager获取）
            // TODO: 集成真实的玩家状态验证
            bool isMyTurnForValidation = false; // 非回合时使用卡牌

            // 使用CardUtilities验证
            return Cards.Core.CardUtilities.Validator.ValidateGameState(true) &&
                   !isMyTurnForValidation; // 简化的回合验证
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 显示卡牌UI（从外部调用）
        /// </summary>
        public void ShowCardUI()
        {
            if (!CanOpenCardUI)
            {
                LogWarning("当前无法打开卡牌UI");
                return;
            }

            LogDebug("显示卡牌UI");

            // 刷新手牌数据
            RefreshHandCards();

            // 显示缩略图（传入手牌数据）
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

            // 清理拖拽状态
            if (isDragging)
            {
                CleanupDragging();
            }

            // 隐藏展示UI
            if (cardDisplayUI != null)
            {
                cardDisplayUI.HideCardDisplay();
            }

            // 设置状态
            SetUIState(UIState.Hidden);

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
        /// 强制禁用卡牌UI（答题期间调用）
        /// </summary>
        public void DisableCardUI()
        {
            LogDebug("强制禁用卡牌UI");

            // 如果当前有显示，先隐藏
            if (IsCardUIVisible)
            {
                HideCardUI();
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
                SetUIState(UIState.Hidden);
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

            // TODO: 从PlayerCardManager获取真实的手牌数据
            // 这里先使用模拟数据
            if (CardSystemManager.Instance?.cardConfig != null)
            {
                // 模拟获取前5张卡牌作为手牌（实际情况下应该从玩家手牌获取）
                var allCards = CardSystemManager.Instance.cardConfig.AllCards;
                for (int i = 0; i < Mathf.Min(5, allCards.Count); i++)
                {
                    currentHandCards.Add(new CardDisplayData(allCards[i]));
                }
            }

            LogDebug($"刷新手牌数据完成，当前手牌数量: {currentHandCards.Count}");
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
                LogWarning("无法显示缩略图：cardDisplayUI为空或手牌数据为空");
            }
        }

        /// <summary>
        /// 更新UI可用性
        /// </summary>
        private void UpdateUIAvailability()
        {
            canUseCards = !isMyTurn; // 非回合时可以使用卡牌

            LogDebug($"UI可用性更新 - 我的回合: {isMyTurn}, 可用卡牌: {canUseCards}");
        }

        #endregion

        #region 调试工具

        /// <summary>
        /// 获取系统状态信息
        /// </summary>
        public string GetSystemStatus()
        {
            return $"CardUIManager状态:\n" +
                   $"- 初始化: {isInitialized}\n" +
                   $"- UI状态: {currentUIState}\n" +
                   $"- 我的回合: {isMyTurn}\n" +
                   $"- 可用卡牌: {canUseCards}\n" +
                   $"- 手牌数量: {currentHandCards.Count}\n" +
                   $"- 拖拽中: {isDragging}";
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[CardUIManager] {message}");
            }
        }

        /// <summary>
        /// 警告日志
        /// </summary>
        private void LogWarning(string message)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[CardUIManager] {message}");
            }
        }

        /// <summary>
        /// 错误日志
        /// </summary>
        private void LogError(string message)
        {
            Debug.LogError($"[CardUIManager] {message}");
        }

        #endregion

        #region Unity编辑器调试

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!showReleaseArea) return;

            // 绘制释放区域
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            if (screenSize.x > 0 && screenSize.y > 0)
            {
                Vector2 areaSize = new Vector2(
                    screenSize.x * releaseAreaWidth,
                    screenSize.y * releaseAreaHeight
                );

                Vector2 areaCenter = new Vector2(
                    screenSize.x * releaseAreaCenterX,
                    screenSize.y * releaseAreaCenterY
                );

                Gizmos.color = new Color(1f, 0f, 0f, 0.3f);

                // 转换屏幕坐标到世界坐标
                if (Camera.main != null)
                {
                    Vector3 worldCenter = Camera.main.ScreenToWorldPoint(new Vector3(areaCenter.x, areaCenter.y, 10f));
                    Vector3 worldSize = new Vector3(areaSize.x / 100f, areaSize.y / 100f, 1f);

                    Gizmos.DrawCube(worldCenter, worldSize);
                }
            }
        }
#endif

        #endregion

        #region 清理资源

        private void OnDestroy()
        {
            // 取消事件订阅
            UnsubscribeFromEvents();

            // 清理拖拽状态
            if (isDragging)
            {
                CleanupDragging();
            }

            LogDebug("CardUIManager已销毁，资源已清理");
        }

        #endregion
    }
}