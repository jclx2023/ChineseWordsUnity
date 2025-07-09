using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using Classroom.Player;

namespace Cards.UI
{
    /// <summary>
    /// 卡牌展示UI控制器 - 手动容器配置版
    /// 负责管理卡牌的视觉展示状态：缩略图 <-> 扇形展示
    /// 支持按压事件替代点击事件
    /// </summary>
    public class CardDisplayUI : MonoBehaviour
    {
        [Header("容器引用 (自动查找)")]
        [SerializeField] private Transform thumbnailContainer; // 缩略图容器 (自动查找)
        [SerializeField] private Transform fanDisplayContainer; // 扇形展示容器 (自动查找)

        [Header("缩略图设置")]
        [SerializeField] private Vector2 thumbnailPosition = new Vector2(110f, 0f); // 调整到合适的位置
        [SerializeField] private float thumbnailRotation = -10f; // 缩略图容器旋转角度
        [SerializeField] private float thumbnailScale = 0.6f; // 缩略图缩放（从0.4增大到0.6）
        [SerializeField] private float thumbnailFanRadius = 100f; // 缩略图扇形半径（从80增大到100）
        [SerializeField] private float thumbnailFanAngle = 40f; // 缩略图扇形角度（从30增大到40）
        [SerializeField] private int maxThumbnailCards = 3; // 最多显示3张缩略图

        [Header("扇形展示设置")]
        [SerializeField] private float fanRadius = 400f; // 扇形半径（增大让卡牌更平）
        [SerializeField] private float fanAngleSpread = 40f; // 扇形角度范围（减小让卡牌更紧密）
        [SerializeField] private float fanCenterOffsetY = 200f; // 扇形中心超出屏幕底部的距离
        [SerializeField] private float cardOverlapRatio = 0.85f; // 卡牌露出屏幕的比例（0.7表示露出70%）

        [Header("悬停交互设置")]
        [SerializeField] private float hoverScale = 1.2f; // 悬停时的放大倍数
        [SerializeField] private float hoverAnimationDuration = 0.2f; // 悬停动画时长
        [SerializeField] private AnimationCurve hoverCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // 悬停动画曲线

        [Header("切换动画设置")]
        [SerializeField] private float transitionDuration = 0.5f; // 状态切换动画时长
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("按压检测设置")]
        [SerializeField] private float pressDetectionThreshold = 0.1f; // 按压检测阈值（秒）

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 展示状态枚举
        public enum DisplayState
        {
            Thumbnail,     // 缩略图状态（默认状态）
            FanDisplay,    // 扇形展示状态
            Transitioning, // 转换中
            Disabled       // 禁用状态（答题期间）
        }

        // 当前状态
        private DisplayState currentState = DisplayState.Thumbnail;
        private List<GameObject> currentCardUIs = new List<GameObject>();
        private List<GameObject> thumbnailCardUIs = new List<GameObject>(); // 缩略图卡牌UI列表
        private List<Vector3> fanPositions = new List<Vector3>();
        private List<Vector3> fanRotations = new List<Vector3>();
        private List<Vector3> thumbnailPositions = new List<Vector3>(); // 缩略图位置
        private List<Vector3> thumbnailRotations = new List<Vector3>(); // 缩略图旋转
        private GameObject hoveredCard = null;
        private Coroutine transitionCoroutine = null;
        private Coroutine hoverCoroutine = null;

        // 按压检测相关
        private GameObject pressedCard = null;
        private Vector2 pressStartPosition = Vector2.zero;
        private float pressStartTime = 0f;
        private bool isPressing = false;

        // 动态计算的扇形中心点
        private Vector2 dynamicFanCenter;

        // 初始化状态
        private bool isInitialized = false;

        // 依赖引用 - 通过CardUIManager传递
        private PlayerCameraController playerCameraController;
        private CardUIComponents cardUIComponents;

        // 事件
        public System.Action<GameObject> OnCardHoverEnter;
        public System.Action<GameObject> OnCardHoverExit;
        public System.Action<GameObject, Vector2> OnCardPressStart;
        public System.Action<GameObject> OnCardPressEnd;

        // 属性
        public DisplayState CurrentState => currentState;
        public bool IsInFanDisplay => currentState == DisplayState.FanDisplay;
        public bool IsEnabled => currentState != DisplayState.Disabled;
        public bool CanShowFanDisplay => currentState == DisplayState.Thumbnail && IsEnabled && isInitialized;

        #region 初始化

        /// <summary>
        /// 由CardUIManager调用的初始化方法
        /// </summary>
        public void Initialize(CardUIComponents uiComponents, PlayerCameraController cameraController = null)
        {
            LogDebug("开始初始化CardDisplayUI");

            // 保存依赖引用
            cardUIComponents = uiComponents;
            playerCameraController = cameraController;

            // 验证容器设置
            if (!ValidateContainers())
            {
                LogError("容器设置无效，无法初始化");
                return;
            }

            // 初始化UI组件
            InitializeUIComponents();

            // 设置初始状态为缩略图
            SetState(DisplayState.Thumbnail);

            isInitialized = true;
            LogDebug("CardDisplayUI初始化完成");
        }

        /// <summary>
        /// 验证容器设置
        /// </summary>
        private bool ValidateContainers()
        {
            // 自动查找容器
            FindContainers();

            if (thumbnailContainer == null)
            {
                LogError("找不到ThumbnailContainer！请确保CardCanvas下存在该容器");
                return false;
            }

            if (fanDisplayContainer == null)
            {
                LogError("找不到FanDisplayContainer！请确保CardCanvas下存在该容器");
                return false;
            }

            LogDebug("容器设置验证通过");
            return true;
        }

        /// <summary>
        /// 查找容器
        /// </summary>
        private void FindContainers()
        {
            // 查找CardCanvas
            Canvas cardCanvas = GetComponentInParent<Canvas>();
            if (cardCanvas == null)
            {
                LogError("找不到父Canvas");
                return;
            }

            // 在CardCanvas下查找容器
            thumbnailContainer = cardCanvas.transform.Find("ThumbnailContainer");
            fanDisplayContainer = cardCanvas.transform.Find("FanDisplayContainer");

            if (thumbnailContainer != null)
                LogDebug($"找到缩略图容器: {thumbnailContainer.name}");

            if (fanDisplayContainer != null)
                LogDebug($"找到扇形展示容器: {fanDisplayContainer.name}");
        }

        /// <summary>
        /// 初始化UI组件
        /// </summary>
        private void InitializeUIComponents()
        {
            // 验证容器层级
            ValidateContainerHierarchy();

            LogDebug("UI组件初始化完成");
        }

        /// <summary>
        /// 验证容器层级是否正确
        /// </summary>
        private void ValidateContainerHierarchy()
        {
            Transform parent = transform.parent;
            Canvas parentCanvas = null;

            // 向上查找Canvas
            Transform current = parent;
            while (current != null && parentCanvas == null)
            {
                parentCanvas = current.GetComponent<Canvas>();
                current = current.parent;
            }

            if (parentCanvas != null)
            {
                LogDebug($"CardDisplayUI层级验证通过，Canvas: {parentCanvas.name}");
            }
            else
            {
                LogWarning("CardDisplayUI未找到父Canvas，可能会导致UI显示问题");
            }
        }

        #endregion

        #region Unity生命周期

        private void Update()
        {
            // 处理按压检测
            HandlePressDetection();
        }

        #endregion

        #region 按压检测系统

        /// <summary>
        /// 处理按压检测
        /// </summary>
        private void HandlePressDetection()
        {
            if (currentState != DisplayState.FanDisplay) return;

            // 检测鼠标按下
            if (Input.GetMouseButtonDown(0))
            {
                HandleMouseDown();
            }
            // 检测鼠标抬起
            else if (Input.GetMouseButtonUp(0))
            {
                HandleMouseUp();
            }
        }

        /// <summary>
        /// 处理鼠标按下
        /// </summary>
        private void HandleMouseDown()
        {
            // 获取鼠标位置
            Vector2 mousePosition = Input.mousePosition;

            // 检测是否点击在卡牌上
            GameObject clickedCard = GetCardUnderMouse(mousePosition);
            if (clickedCard != null)
            {
                StartCardPress(clickedCard, mousePosition);
            }
        }

        /// <summary>
        /// 处理鼠标抬起
        /// </summary>
        private void HandleMouseUp()
        {
            if (isPressing && pressedCard != null)
            {
                EndCardPress();
            }
        }

        /// <summary>
        /// 开始卡牌按压
        /// </summary>
        private void StartCardPress(GameObject card, Vector2 mousePosition)
        {
            pressedCard = card;
            pressStartPosition = mousePosition;
            pressStartTime = Time.time;
            isPressing = true;

            LogDebug($"开始按压卡牌: {card.name}");

            // 转换鼠标屏幕坐标为Canvas坐标
            Vector2 canvasPosition;
            RectTransform canvasRect = GetCanvasRectTransform();
            if (canvasRect != null)
            {
                Camera uiCamera = GetUICamera();
                bool success = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, mousePosition, uiCamera, out canvasPosition);

                if (success)
                {
                    // 触发按压开始事件
                    OnCardPressStart?.Invoke(card, canvasPosition);
                }
                else
                {
                    LogWarning("无法转换鼠标坐标到Canvas坐标");
                    OnCardPressStart?.Invoke(card, mousePosition);
                }
            }
            else
            {
                // 使用原始屏幕坐标
                OnCardPressStart?.Invoke(card, mousePosition);
            }
        }

        /// <summary>
        /// 结束卡牌按压
        /// </summary>
        private void EndCardPress()
        {
            if (pressedCard != null)
            {
                LogDebug($"结束按压卡牌: {pressedCard.name}");

                // 触发按压结束事件
                OnCardPressEnd?.Invoke(pressedCard);
            }

            // 清理按压状态
            pressedCard = null;
            pressStartPosition = Vector2.zero;
            pressStartTime = 0f;
            isPressing = false;
        }

        /// <summary>
        /// 获取鼠标下的卡牌
        /// </summary>
        private GameObject GetCardUnderMouse(Vector2 mousePosition)
        {
            // 使用EventSystem进行Raycast检测
            PointerEventData eventData = new PointerEventData(EventSystem.current)
            {
                position = mousePosition
            };

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            // 查找第一个匹配的卡牌
            foreach (var result in results)
            {
                GameObject obj = result.gameObject;

                // 检查是否是当前的卡牌UI
                if (currentCardUIs.Contains(obj))
                {
                    return obj;
                }

                // 检查父对象是否是卡牌UI
                Transform parent = obj.transform.parent;
                while (parent != null)
                {
                    if (currentCardUIs.Contains(parent.gameObject))
                    {
                        return parent.gameObject;
                    }
                    parent = parent.parent;
                }
            }

            return null;
        }

        /// <summary>
        /// 获取UI摄像机
        /// </summary>
        private Camera GetUICamera()
        {
            Canvas canvas = GetCanvasRectTransform()?.GetComponent<Canvas>();
            return canvas?.worldCamera;
        }

        #endregion

        #region 状态管理

        /// <summary>
        /// 设置展示状态
        /// </summary>
        private void SetState(DisplayState newState)
        {
            if (currentState == newState) return;

            DisplayState oldState = currentState;
            currentState = newState;

            LogDebug($"状态切换: {oldState} -> {newState}");

            // 状态切换处理
            OnStateChanged(oldState, newState);
        }

        /// <summary>
        /// 状态切换处理
        /// </summary>
        private void OnStateChanged(DisplayState oldState, DisplayState newState)
        {
            switch (newState)
            {
                case DisplayState.Thumbnail:
                    // 显示缩略图，隐藏扇形
                    ShowThumbnailMode();
                    break;

                case DisplayState.FanDisplay:
                    // 显示扇形，隐藏缩略图
                    ShowFanDisplayMode();
                    break;

                case DisplayState.Disabled:
                    // 禁用所有交互，但保持当前显示状态
                    DisableAllInteractions();
                    break;

                case DisplayState.Transitioning:
                    // 转换状态，不需要特殊处理
                    break;
            }
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 刷新缩略图显示
        /// </summary>
        public void RefreshThumbnailDisplay(List<CardDisplayData> cardDataList)
        {
            if (!isInitialized)
            {
                LogWarning("CardDisplayUI未初始化，无法刷新缩略图");
                return;
            }

            if (cardDataList == null || cardDataList.Count == 0)
            {
                LogDebug("手牌数据为空，清理缩略图显示");
                ClearThumbnailCards();
                return;
            }

            LogDebug($"刷新缩略图显示，手牌数量: {cardDataList.Count}");

            // 重新创建缩略图卡牌
            CreateThumbnailCards(cardDataList);

            // 如果当前在缩略图状态，确保显示
            if (currentState == DisplayState.Thumbnail)
            {
                ShowThumbnailMode();
            }
        }

        /// <summary>
        /// 显示扇形展示
        /// </summary>
        public bool ShowFanDisplayWithCards(List<CardDisplayData> cardDataList)
        {
            if (!CanShowFanDisplay)
            {
                LogWarning($"无法显示扇形展示 - 当前状态: {currentState}, 已初始化: {isInitialized}");
                return false;
            }

            if (cardDataList == null || cardDataList.Count == 0)
            {
                LogWarning("卡牌数据列表为空，无法显示扇形展示");
                return false;
            }

            LogDebug($"开始显示扇形展示，卡牌数量: {cardDataList.Count}");

            // 计算动态扇形中心（自适应屏幕底部）
            CalculateDynamicFanCenter();

            // 创建卡牌UI
            CreateCardUIs(cardDataList);

            // 计算扇形位置
            CalculateFanPositions(cardDataList.Count);

            // 切换状态
            SetState(DisplayState.Transitioning);

            // 开始转换动画
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }
            transitionCoroutine = StartCoroutine(TransitionToFanDisplay());

            // 禁用摄像机控制
            SetCameraControl(false);

            return true;
        }

        /// <summary>
        /// 更新扇形展示（卡牌使用后调用）
        /// </summary>
        public bool UpdateFanDisplayWithCards(List<CardDisplayData> cardDataList)
        {
            if (currentState != DisplayState.FanDisplay)
            {
                LogWarning("当前不在扇形展示状态，无法更新扇形展示");
                return false;
            }

            if (cardDataList == null || cardDataList.Count == 0)
            {
                LogDebug("卡牌数据为空，返回缩略图状态");
                ReturnToThumbnail();
                return true;
            }

            LogDebug($"更新扇形展示，新的卡牌数量: {cardDataList.Count}");

            // 重新计算动态扇形中心
            CalculateDynamicFanCenter();

            // 清理当前扇形卡牌
            ClearFanDisplayCards();

            // 创建新的卡牌UI
            CreateCardUIs(cardDataList);

            // 重新计算扇形位置
            CalculateFanPositions(cardDataList.Count);

            // 直接设置到扇形位置（无动画，因为是更新）
            ShowFanDisplayMode();

            // 同时更新缩略图（为返回做准备）
            CreateThumbnailCards(cardDataList);

            LogDebug("扇形展示更新完成");
            return true;
        }

        /// <summary>
        /// 返回缩略图状态
        /// </summary>
        public void ReturnToThumbnail()
        {
            if (currentState != DisplayState.FanDisplay)
            {
                LogWarning("当前不在扇形展示状态，无法返回缩略图");
                return;
            }

            LogDebug("从扇形展示返回缩略图");

            SetState(DisplayState.Transitioning);

            // 开始返回动画
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }
            transitionCoroutine = StartCoroutine(TransitionToThumbnail());

            // 恢复摄像机控制
            SetCameraControl(true);
        }

        /// <summary>
        /// 设置启用/禁用状态
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            if (enabled)
            {
                if (currentState == DisplayState.Disabled)
                {
                    SetState(DisplayState.Thumbnail);
                    LogDebug("启用CardDisplayUI");
                }
            }
            else
            {
                if (currentState != DisplayState.Disabled)
                {
                    // 清理按压状态
                    if (isPressing)
                    {
                        EndCardPress();
                    }

                    SetState(DisplayState.Disabled);
                    LogDebug("禁用CardDisplayUI");
                }
            }
        }

        /// <summary>
        /// 强制切换到缩略图状态（供CardUIManager调用）
        /// </summary>
        public void ForceToThumbnailState()
        {
            LogDebug("强制切换到缩略图状态");

            // 停止所有动画
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
                transitionCoroutine = null;
            }

            if (hoverCoroutine != null)
            {
                StopCoroutine(hoverCoroutine);
                hoverCoroutine = null;
            }

            // 清理按压状态
            if (isPressing)
            {
                EndCardPress();
            }

            // 清理扇形卡牌
            ClearFanDisplayCards();

            // 恢复摄像机控制
            SetCameraControl(true);

            // 切换到缩略图状态
            SetState(DisplayState.Thumbnail);
        }

        /// <summary>
        /// 隐藏卡牌显示
        /// </summary>
        public void HideCardDisplay()
        {
            LogDebug("隐藏卡牌显示（强制回到缩略图状态）");
            ForceToThumbnailState();
        }

        /// <summary>
        /// 显示卡牌显示
        /// </summary>
        public void ShowCardDisplay(List<CardDisplayData> cardDataList)
        {
            LogDebug("显示卡牌显示（刷新缩略图）");
            RefreshThumbnailDisplay(cardDataList);
        }

        /// <summary>
        /// 完全清理显示（组件销毁时调用）
        /// </summary>
        public void ClearAllDisplays()
        {
            LogDebug("清理所有显示");

            // 停止所有协程
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }

            if (hoverCoroutine != null)
            {
                StopCoroutine(hoverCoroutine);
            }

            // 清理按压状态
            if (isPressing)
            {
                EndCardPress();
            }

            // 清理所有UI
            ClearThumbnailCards();
            ClearFanDisplayCards();

            // 恢复摄像机控制
            SetCameraControl(true);
        }

        #endregion

        #region 动态位置计算

        /// <summary>
        /// 计算动态扇形中心点
        /// </summary>
        private void CalculateDynamicFanCenter()
        {
            // 获取Canvas的RectTransform
            RectTransform canvasRect = GetCanvasRectTransform();
            if (canvasRect == null)
            {
                LogWarning("无法获取Canvas RectTransform，使用默认位置");
                dynamicFanCenter = new Vector2(0f, -400f);
                return;
            }

            // 获取屏幕尺寸（在Canvas坐标系中）
            Rect canvasSize = canvasRect.rect;
            float screenHeight = canvasSize.height;

            // 计算扇形中心位置
            // X轴：屏幕中央
            float centerX = 0f;

            // Y轴：屏幕底部 + 向下偏移，让扇形中心在屏幕外
            // 卡牌高度279，希望露出70%，所以需要让中心在底部下方一定距离
            float cardHeight = 279f; // 卡牌实际高度
            float visibleCardHeight = cardHeight * cardOverlapRatio; // 卡牌可见部分
            float hiddenCardHeight = cardHeight - visibleCardHeight; // 卡牌被遮挡部分

            // 扇形中心Y坐标 = 屏幕底部 - 扇形偏移距离
            float centerY = -screenHeight / 2f - fanCenterOffsetY;

            dynamicFanCenter = new Vector2(centerX, centerY);

            LogDebug($"屏幕高度: {screenHeight}, 卡牌可见高度: {visibleCardHeight}");
        }

        /// <summary>
        /// 获取Canvas的RectTransform
        /// </summary>
        private RectTransform GetCanvasRectTransform()
        {
            // 向上查找Canvas
            Transform current = transform;
            while (current != null)
            {
                Canvas canvas = current.GetComponent<Canvas>();
                if (canvas != null)
                {
                    return canvas.GetComponent<RectTransform>();
                }
                current = current.parent;
            }
            return null;
        }

        #endregion

        #region 缩略图显示

        /// <summary>
        /// 创建缩略图卡牌
        /// </summary>
        private void CreateThumbnailCards(List<CardDisplayData> cardDataList)
        {

            // 清理现有缩略图
            ClearThumbnailCards();

            // 限制缩略图数量
            int thumbnailCount = Mathf.Min(cardDataList.Count, maxThumbnailCards);

            // 计算缩略图位置
            CalculateThumbnailPositions(thumbnailCount);

            // 创建缩略图UI
            for (int i = 0; i < thumbnailCount; i++)
            {
                GameObject thumbnailCard = cardUIComponents.CreateCardUI(cardDataList[i], thumbnailContainer);
                if (thumbnailCard != null)
                {
                    // 设置缩略图位置和旋转
                    RectTransform rectTransform = thumbnailCard.GetComponent<RectTransform>();
                    if (rectTransform != null && i < thumbnailPositions.Count)
                    {
                        rectTransform.anchoredPosition = thumbnailPositions[i];
                        rectTransform.localEulerAngles = thumbnailRotations[i];
                        rectTransform.localScale = Vector3.one; // 容器已经缩放了
                    }

                    thumbnailCardUIs.Add(thumbnailCard);
                }
            }

            LogDebug($"创建了 {thumbnailCardUIs.Count} 张缩略图卡牌");
        }

        /// <summary>
        /// 计算缩略图位置（小扇形排列）
        /// </summary>
        private void CalculateThumbnailPositions(int cardCount)
        {
            thumbnailPositions.Clear();
            thumbnailRotations.Clear();

            if (cardCount == 0) return;

            // 计算角度间隔
            float angleStep = cardCount > 1 ? thumbnailFanAngle / (cardCount - 1) : 0f;
            float startAngle = -thumbnailFanAngle / 2f;

            for (int i = 0; i < cardCount; i++)
            {
                float angle = startAngle + (angleStep * i);
                float radians = angle * Mathf.Deg2Rad;

                // 计算位置（相对于缩略图容器）
                Vector3 position = new Vector3(
                    Mathf.Sin(radians) * thumbnailFanRadius,
                    Mathf.Cos(radians) * thumbnailFanRadius,
                    0f
                );

                // 计算旋转
                Vector3 rotation = new Vector3(0, 0, -angle);

                thumbnailPositions.Add(position);
                thumbnailRotations.Add(rotation);
            }

            LogDebug($"计算了 {cardCount} 张缩略图的扇形位置");
        }

        /// <summary>
        /// 显示缩略图模式
        /// </summary>
        private void ShowThumbnailMode()
        {
            // 显示缩略图容器
            if (thumbnailContainer != null)
            {
                thumbnailContainer.gameObject.SetActive(true);

                // 确保透明度正常
                CanvasGroup thumbnailCanvasGroup = thumbnailContainer.GetComponent<CanvasGroup>();
                if (thumbnailCanvasGroup != null)
                {
                    thumbnailCanvasGroup.alpha = 1f;
                }
            }

            // 隐藏扇形展示
            if (fanDisplayContainer != null)
            {
                fanDisplayContainer.gameObject.SetActive(false);
            }

            LogDebug("缩略图模式显示完成");
        }

        /// <summary>
        /// 清理缩略图卡牌
        /// </summary>
        private void ClearThumbnailCards()
        {
            if (thumbnailCardUIs.Count > 0)
            {
                foreach (var thumbnailCard in thumbnailCardUIs)
                {
                    if (thumbnailCard != null && cardUIComponents != null)
                    {
                        cardUIComponents.DestroyCardUI(thumbnailCard);
                    }
                }
                thumbnailCardUIs.Clear();
            }

            thumbnailPositions.Clear();
            thumbnailRotations.Clear();
        }

        #endregion

        #region 扇形展示

        /// <summary>
        /// 创建卡牌UI
        /// </summary>
        private void CreateCardUIs(List<CardDisplayData> cardDataList)
        {

            // 清理现有卡牌
            ClearFanDisplayCards();

            // 创建新卡牌
            currentCardUIs = cardUIComponents.CreateMultipleCardUI(cardDataList, fanDisplayContainer);

            // 为每张卡牌添加悬停检测
            foreach (var cardUI in currentCardUIs)
            {
                AddHoverDetection(cardUI);
            }

            LogDebug($"创建了 {currentCardUIs.Count} 张扇形卡牌UI");
        }

        /// <summary>
        /// 计算扇形位置
        /// </summary>
        private void CalculateFanPositions(int cardCount)
        {
            fanPositions.Clear();
            fanRotations.Clear();

            if (cardCount == 0) return;

            // 计算角度间隔
            float angleStep = cardCount > 1 ? fanAngleSpread / (cardCount - 1) : 0f;
            float startAngle = -fanAngleSpread / 2f;

            for (int i = 0; i < cardCount; i++)
            {
                float angle = startAngle + (angleStep * i);
                float radians = angle * Mathf.Deg2Rad;

                // 使用动态计算的扇形中心
                Vector3 position = new Vector3(
                    dynamicFanCenter.x + Mathf.Sin(radians) * fanRadius,
                    dynamicFanCenter.y + Mathf.Cos(radians) * fanRadius,
                    0f
                );

                // 计算旋转
                Vector3 rotation = new Vector3(0, 0, -angle);

                fanPositions.Add(position);
                fanRotations.Add(rotation);
            }

            LogDebug($"计算了 {cardCount} 张卡牌的扇形位置，中心点: {dynamicFanCenter}");
        }

        /// <summary>
        /// 显示扇形展示模式
        /// </summary>
        private void ShowFanDisplayMode()
        {
            // 隐藏缩略图
            if (thumbnailContainer != null)
            {
                thumbnailContainer.gameObject.SetActive(false);
            }

            // 显示扇形卡牌
            if (fanDisplayContainer != null)
            {
                fanDisplayContainer.gameObject.SetActive(true);
            }

            // 设置扇形卡牌位置
            for (int i = 0; i < currentCardUIs.Count && i < fanPositions.Count; i++)
            {
                GameObject cardUI = currentCardUIs[i];
                RectTransform rectTransform = cardUI.GetComponent<RectTransform>();

                if (rectTransform != null)
                {
                    rectTransform.anchoredPosition = fanPositions[i];
                    rectTransform.localEulerAngles = fanRotations[i];
                    rectTransform.localScale = Vector3.one;
                }

                cardUI.SetActive(true);
            }

            LogDebug("扇形展示模式显示完成");
        }

        /// <summary>
        /// 清理扇形展示卡牌
        /// </summary>
        private void ClearFanDisplayCards()
        {
            if (currentCardUIs.Count > 0 && cardUIComponents != null)
            {
                cardUIComponents.DestroyMultipleCardUI(currentCardUIs);
                currentCardUIs.Clear();
            }

            fanPositions.Clear();
            fanRotations.Clear();
            hoveredCard = null;
        }

        #endregion

        #region 动画处理

        /// <summary>
        /// 转换到扇形展示的动画
        /// </summary>
        private IEnumerator TransitionToFanDisplay()
        {
            float elapsed = 0f;

            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / transitionDuration;
                float curveValue = transitionCurve.Evaluate(t);

                // 动画每张卡牌从对应缩略图位置到扇形位置
                for (int i = 0; i < currentCardUIs.Count && i < fanPositions.Count; i++)
                {
                    GameObject cardUI = currentCardUIs[i];
                    RectTransform rectTransform = cardUI.GetComponent<RectTransform>();

                    if (rectTransform != null)
                    {
                        // 计算起始位置（对应的缩略图位置）
                        Vector3 startPos;
                        Vector3 startRot;
                        Vector3 startScale;

                        if (i < thumbnailPositions.Count)
                        {
                            // 有对应的缩略图位置
                            Vector3 thumbnailPos3D = new Vector3(thumbnailPosition.x, thumbnailPosition.y, 0f);
                            startPos = thumbnailPos3D + thumbnailPositions[i] * thumbnailScale;
                            startRot = thumbnailRotations[i];
                            startScale = Vector3.one * thumbnailScale;
                        }
                        else
                        {
                            // 超出缩略图数量的卡牌从中心开始
                            startPos = new Vector3(thumbnailPosition.x, thumbnailPosition.y, 0f);
                            startRot = Vector3.zero;
                            startScale = Vector3.one * thumbnailScale;
                        }

                        // 位置插值
                        Vector3 targetPos = fanPositions[i];
                        rectTransform.anchoredPosition = Vector3.Lerp(startPos, targetPos, curveValue);

                        // 旋转插值
                        Vector3 targetRot = fanRotations[i];
                        rectTransform.localEulerAngles = Vector3.Lerp(startRot, targetRot, curveValue);

                        // 缩放插值
                        rectTransform.localScale = Vector3.Lerp(startScale, Vector3.one, curveValue);
                    }

                    cardUI.SetActive(true);
                }

                // 缩略图容器淡出
                if (thumbnailContainer != null && t > 0.3f)
                {
                    CanvasGroup thumbnailCanvasGroup = thumbnailContainer.GetComponent<CanvasGroup>();
                    if (thumbnailCanvasGroup == null)
                    {
                        thumbnailCanvasGroup = thumbnailContainer.gameObject.AddComponent<CanvasGroup>();
                    }
                    thumbnailCanvasGroup.alpha = 1f - ((t - 0.3f) / 0.7f);
                }

                yield return null;
            }

            // 隐藏缩略图
            if (thumbnailContainer != null)
            {
                thumbnailContainer.gameObject.SetActive(false);
            }

            SetState(DisplayState.FanDisplay);
            LogDebug("转换到扇形展示动画完成");
        }

        /// <summary>
        /// 转换到缩略图的动画
        /// </summary>
        private IEnumerator TransitionToThumbnail()
        {
            float elapsed = 0f;

            // 记录扇形的最终位置
            List<Vector3> startPositions = new List<Vector3>();
            List<Vector3> startRotations = new List<Vector3>();

            for (int i = 0; i < currentCardUIs.Count; i++)
            {
                RectTransform rectTransform = currentCardUIs[i].GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    startPositions.Add(rectTransform.anchoredPosition);
                    startRotations.Add(rectTransform.localEulerAngles);
                }
            }

            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / transitionDuration;
                float curveValue = transitionCurve.Evaluate(t);

                // 动画每张卡牌从扇形位置回到对应的缩略图位置
                for (int i = 0; i < currentCardUIs.Count && i < startPositions.Count; i++)
                {
                    GameObject cardUI = currentCardUIs[i];
                    RectTransform rectTransform = cardUI.GetComponent<RectTransform>();

                    if (rectTransform != null)
                    {
                        // 计算目标位置（对应的缩略图位置）
                        Vector3 targetPos;
                        Vector3 targetRot;
                        Vector3 targetScale;

                        if (i < thumbnailPositions.Count)
                        {
                            // 有对应的缩略图位置
                            Vector3 thumbnailPos3D = new Vector3(thumbnailPosition.x, thumbnailPosition.y, 0f);
                            targetPos = thumbnailPos3D + thumbnailPositions[i] * thumbnailScale;
                            targetRot = thumbnailRotations[i];
                            targetScale = Vector3.one * thumbnailScale;
                        }
                        else
                        {
                            // 超出缩略图数量的卡牌回到中心
                            targetPos = new Vector3(thumbnailPosition.x, thumbnailPosition.y, 0f);
                            targetRot = Vector3.zero;
                            targetScale = Vector3.one * thumbnailScale;
                        }

                        // 位置插值
                        rectTransform.anchoredPosition = Vector3.Lerp(startPositions[i], targetPos, curveValue);

                        // 旋转插值
                        rectTransform.localEulerAngles = Vector3.Lerp(startRotations[i], targetRot, curveValue);

                        // 缩放插值
                        rectTransform.localScale = Vector3.Lerp(Vector3.one, targetScale, curveValue);
                    }
                }

                yield return null;
            }

            // 清理扇形卡牌UI
            ClearFanDisplayCards();

            // 重新显示缩略图
            if (thumbnailContainer != null)
            {
                thumbnailContainer.gameObject.SetActive(true);

                // 恢复缩略图透明度
                CanvasGroup thumbnailCanvasGroup = thumbnailContainer.GetComponent<CanvasGroup>();
                if (thumbnailCanvasGroup != null)
                {
                    thumbnailCanvasGroup.alpha = 1f;
                }
            }

            SetState(DisplayState.Thumbnail);
            LogDebug("转换到缩略图动画完成");
        }

        #endregion

        #region 悬停交互

        /// <summary>
        /// 为卡牌添加悬停检测
        /// </summary>
        private void AddHoverDetection(GameObject cardUI)
        {
            // 添加EventTrigger组件
            EventTrigger eventTrigger = cardUI.GetComponent<EventTrigger>();
            if (eventTrigger == null)
            {
                eventTrigger = cardUI.AddComponent<EventTrigger>();
            }

            // 鼠标进入事件
            EventTrigger.Entry pointerEnter = new EventTrigger.Entry();
            pointerEnter.eventID = EventTriggerType.PointerEnter;
            pointerEnter.callback.AddListener((eventData) => OnCardPointerEnter(cardUI));
            eventTrigger.triggers.Add(pointerEnter);

            // 鼠标离开事件
            EventTrigger.Entry pointerExit = new EventTrigger.Entry();
            pointerExit.eventID = EventTriggerType.PointerExit;
            pointerExit.callback.AddListener((eventData) => OnCardPointerExit(cardUI));
            eventTrigger.triggers.Add(pointerExit);
        }

        /// <summary>
        /// 卡牌鼠标进入事件
        /// </summary>
        private void OnCardPointerEnter(GameObject cardUI)
        {
            if (currentState != DisplayState.FanDisplay) return;

            hoveredCard = cardUI;

            // 开始悬停动画
            if (hoverCoroutine != null)
            {
                StopCoroutine(hoverCoroutine);
            }
            hoverCoroutine = StartCoroutine(HoverScaleAnimation(cardUI, hoverScale));

            // 触发事件
            OnCardHoverEnter?.Invoke(cardUI);

        }

        /// <summary>
        /// 卡牌鼠标离开事件
        /// </summary>
        private void OnCardPointerExit(GameObject cardUI)
        {
            if (currentState != DisplayState.FanDisplay) return;

            if (hoveredCard == cardUI)
            {
                hoveredCard = null;
            }

            // 开始还原动画
            if (hoverCoroutine != null)
            {
                StopCoroutine(hoverCoroutine);
            }
            hoverCoroutine = StartCoroutine(HoverScaleAnimation(cardUI, 1f));

            // 触发事件
            OnCardHoverExit?.Invoke(cardUI);

        }

        /// <summary>
        /// 悬停缩放动画
        /// </summary>
        private IEnumerator HoverScaleAnimation(GameObject cardUI, float targetScale)
        {
            if (cardUI == null) yield break;

            RectTransform rectTransform = cardUI.GetComponent<RectTransform>();
            if (rectTransform == null) yield break;

            Vector3 startScale = rectTransform.localScale;
            Vector3 endScale = Vector3.one * targetScale;
            float elapsed = 0f;

            while (elapsed < hoverAnimationDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / hoverAnimationDuration;
                float curveValue = hoverCurve.Evaluate(t);

                rectTransform.localScale = Vector3.Lerp(startScale, endScale, curveValue);

                yield return null;
            }

            rectTransform.localScale = endScale;
        }

        /// <summary>
        /// 禁用所有交互
        /// </summary>
        private void DisableAllInteractions()
        {
            // 清理按压状态
            if (isPressing)
            {
                EndCardPress();
            }

            LogDebug("禁用所有交互");
        }

        #endregion

        #region 摄像机控制

        /// <summary>
        /// 控制摄像机
        /// </summary>
        private void SetCameraControl(bool enabled)
        {
            if (playerCameraController != null)
            {
                playerCameraController.SetControlEnabled(enabled);
                LogDebug($"摄像机控制: {(enabled ? "启用" : "禁用")}");
            }
            else
            {
                LogWarning("摄像机控制器不可用，无法控制摄像机");
            }
        }

        #endregion

        #region 工具方法

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[CardDisplayUI] {message}");
            }
        }

        private void LogWarning(string message)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[CardDisplayUI] {message}");
            }
        }

        private void LogError(string message)
        {
            Debug.LogError($"[CardDisplayUI] {message}");
        }

        #endregion

        #region Unity生命周期

        private void OnDestroy()
        {
            // 清理所有资源
            ClearAllDisplays();

            LogDebug("CardDisplayUI已销毁，资源已清理");
        }

        #endregion
    }
}