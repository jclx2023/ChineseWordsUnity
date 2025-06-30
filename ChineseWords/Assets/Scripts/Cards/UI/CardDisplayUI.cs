using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using Classroom.Player;

namespace Cards.UI
{
    /// <summary>
    /// 卡牌展示UI控制器
    /// 负责管理卡牌的视觉展示状态：缩略图 -> 扇形展示 -> 悬停交互
    /// </summary>
    public class CardDisplayUI : MonoBehaviour
    {
        [Header("缩略图设置")]
        [SerializeField] private Transform thumbnailContainer; // 缩略图容器
        [SerializeField] private Vector2 thumbnailPosition = new Vector2(-400f, -300f); // 左下角位置
        [SerializeField] private float thumbnailScale = 0.4f; // 缩略图缩放
        [SerializeField] private float thumbnailFanRadius = 80f; // 缩略图扇形半径
        [SerializeField] private float thumbnailFanAngle = 30f; // 缩略图扇形角度
        [SerializeField] private int maxThumbnailCards = 3; // 最多显示3张缩略图

        [Header("扇形展示设置")]
        [SerializeField] private Transform fanDisplayContainer; // 扇形展示容器
        [SerializeField] private float fanRadius = 400f; // 扇形半径
        [SerializeField] private float fanAngleSpread = 60f; // 扇形角度范围
        [SerializeField] private Vector2 fanCenter = new Vector2(0f, -200f); // 扇形中心点

        [Header("悬停交互设置")]
        [SerializeField] private float hoverScale = 1.2f; // 悬停时的放大倍数
        [SerializeField] private float hoverAnimationDuration = 0.2f; // 悬停动画时长
        [SerializeField] private AnimationCurve hoverCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // 悬停动画曲线

        [Header("切换动画设置")]
        [SerializeField] private float transitionDuration = 0.5f; // 状态切换动画时长
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool showDebugGizmos = false;

        // 展示状态枚举
        public enum DisplayState
        {
            Hidden,        // 隐藏状态
            Thumbnail,     // 缩略图状态
            FanDisplay,    // 扇形展示状态
            Transitioning  // 转换中
        }

        // 当前状态
        private DisplayState currentState = DisplayState.Hidden;
        private List<GameObject> currentCardUIs = new List<GameObject>();
        private List<GameObject> thumbnailCardUIs = new List<GameObject>(); // 缩略图卡牌UI列表
        private List<Vector3> fanPositions = new List<Vector3>();
        private List<Vector3> fanRotations = new List<Vector3>();
        private List<Vector3> thumbnailPositions = new List<Vector3>(); // 缩略图位置
        private List<Vector3> thumbnailRotations = new List<Vector3>(); // 缩略图旋转
        private GameObject hoveredCard = null;
        private Coroutine transitionCoroutine = null;
        private Coroutine hoverCoroutine = null;

        // 摄像机控制
        private PlayerCameraController playerCameraController;
        private bool cameraControllerReady = false;

        // 事件
        public System.Action<GameObject> OnCardHoverEnter;
        public System.Action<GameObject> OnCardHoverExit;
        public System.Action<GameObject> OnCardSelected; // 为后续拖拽准备

        // 属性
        public DisplayState CurrentState => currentState;
        public bool IsInFanDisplay => currentState == DisplayState.FanDisplay;
        public bool CanShowFanDisplay => currentState == DisplayState.Thumbnail && cameraControllerReady;

        private void Awake()
        {
            InitializeComponents();
        }

        private void Start()
        {
            // 等待摄像机控制器就绪
            StartCoroutine(WaitForCameraController());
        }

        private void OnEnable()
        {
            SubscribeToClassroomManagerEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromClassroomManagerEvents();
        }

        #region 摄像机控制器等待逻辑

        /// <summary>
        /// 订阅ClassroomManager事件
        /// </summary>
        private void SubscribeToClassroomManagerEvents()
        {
            Classroom.ClassroomManager.OnCameraSetupCompleted += OnCameraSetupCompleted;
            Classroom.ClassroomManager.OnClassroomInitialized += OnClassroomInitialized;
        }

        /// <summary>
        /// 取消订阅ClassroomManager事件
        /// </summary>
        private void UnsubscribeFromClassroomManagerEvents()
        {
            Classroom.ClassroomManager.OnCameraSetupCompleted -= OnCameraSetupCompleted;
            Classroom.ClassroomManager.OnClassroomInitialized -= OnClassroomInitialized;
        }

        /// <summary>
        /// 响应摄像机设置完成事件
        /// </summary>
        private void OnCameraSetupCompleted()
        {
            LogDebug("收到摄像机设置完成事件，开始查找摄像机控制器");
            StartCoroutine(FindPlayerCameraControllerCoroutine());
        }

        /// <summary>
        /// 响应教室初始化完成事件（备用方案）
        /// </summary>
        private void OnClassroomInitialized()
        {
            if (!cameraControllerReady)
            {
                LogDebug("教室初始化完成，尝试查找摄像机控制器（备用方案）");
                StartCoroutine(FindPlayerCameraControllerCoroutine());
            }
        }

        /// <summary>
        /// 等待摄像机控制器就绪
        /// </summary>
        private IEnumerator WaitForCameraController()
        {
            float timeout = 10f; // 10秒超时
            float elapsed = 0f;

            while (elapsed < timeout && !cameraControllerReady)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!cameraControllerReady)
            {
                LogWarning("摄像机控制器等待超时，将使用备用方案");
            }
        }

        /// <summary>
        /// 协程方式查找玩家摄像机控制器
        /// </summary>
        private IEnumerator FindPlayerCameraControllerCoroutine()
        {
            LogDebug("开始查找玩家摄像机控制器");

            float timeout = 5f; // 5秒超时
            float elapsed = 0f;

            while (elapsed < timeout && playerCameraController == null)
            {
                playerCameraController = FindPlayerCameraController();

                if (playerCameraController != null)
                {
                    cameraControllerReady = true;
                    LogDebug($"成功找到摄像机控制器: {playerCameraController.name}");
                    break;
                }

                elapsed += 0.1f;
                yield return new WaitForSeconds(0.1f);
            }

            if (playerCameraController == null)
            {
                LogWarning("未能找到摄像机控制器，摄像机控制功能将不可用");
            }
        }

        /// <summary>
        /// 查找玩家摄像机控制器
        /// </summary>
        private PlayerCameraController FindPlayerCameraController()
        {
            // 方式1: 通过ClassroomManager获取（推荐方式）
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

            return null;
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化组件
        /// </summary>
        private void InitializeComponents()
        {
            // 确保扇形展示容器存在
            if (fanDisplayContainer == null)
            {
                GameObject container = new GameObject("FanDisplayContainer");
                container.transform.SetParent(transform, false);
                fanDisplayContainer = container.transform;
            }

            // 确保缩略图容器存在
            if (thumbnailContainer == null)
            {
                GameObject thumbContainer = new GameObject("ThumbnailContainer");
                thumbContainer.transform.SetParent(transform, false);
                thumbnailContainer = thumbContainer.transform;

                // 设置缩略图容器位置
                RectTransform thumbRect = thumbContainer.AddComponent<RectTransform>();
                thumbRect.anchoredPosition = thumbnailPosition;
                thumbRect.localScale = Vector3.one * thumbnailScale;
            }

            // 设置初始状态
            SetState(DisplayState.Hidden);

            LogDebug("CardDisplayUI组件初始化完成");
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
                case DisplayState.Hidden:
                    HideAllCards();
                    break;

                case DisplayState.Thumbnail:
                    ShowThumbnail();
                    break;

                case DisplayState.FanDisplay:
                    ShowFanDisplay();
                    break;

                case DisplayState.Transitioning:
                    // 转换状态，不需要特殊处理
                    break;
            }
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 显示卡牌展示UI（从隐藏状态到缩略图状态）
        /// </summary>
        /// <param name="cardDataList">手牌数据</param>
        public void ShowCardDisplay(List<CardDisplayData> cardDataList)
        {
            if (currentState != DisplayState.Hidden)
            {
                LogWarning("当前状态不是Hidden，无法显示卡牌展示UI");
                return;
            }

            if (cardDataList == null || cardDataList.Count == 0)
            {
                LogWarning("手牌数据为空，无法显示缩略图");
                return;
            }

            LogDebug($"显示卡牌展示UI，手牌数量: {cardDataList.Count}");

            // 创建缩略图卡牌
            CreateThumbnailCards(cardDataList);

            SetState(DisplayState.Thumbnail);
        }

        /// <summary>
        /// 隐藏卡牌展示UI
        /// </summary>
        public void HideCardDisplay()
        {
            LogDebug("隐藏卡牌展示UI");

            // 恢复摄像机控制
            SetCameraControl(true);

            SetState(DisplayState.Hidden);
        }

        /// <summary>
        /// 切换到扇形展示
        /// </summary>
        public bool ShowFanDisplayWithCards(List<CardDisplayData> cardDataList)
        {
            if (!CanShowFanDisplay)
            {
                LogWarning($"无法显示扇形展示 - 当前状态: {currentState}, 摄像机就绪: {cameraControllerReady}");
                return false;
            }

            if (cardDataList == null || cardDataList.Count == 0)
            {
                LogWarning("卡牌数据列表为空，无法显示扇形展示");
                return false;
            }

            LogDebug($"开始显示扇形展示，卡牌数量: {cardDataList.Count}");

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
        /// 从扇形展示返回缩略图
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
                GameObject thumbnailCard = CardUIComponents.Instance.CreateCardUI(cardDataList[i], thumbnailContainer);
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
        /// 显示缩略图
        /// </summary>
        private void ShowThumbnail()
        {
            if (thumbnailContainer != null)
            {
                thumbnailContainer.gameObject.SetActive(true);
                LogDebug("缩略图显示完成");
            }
            else
            {
                LogWarning("缩略图容器不存在");
            }
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
                    if (thumbnailCard != null)
                    {
                        CardUIComponents.Instance.DestroyCardUI(thumbnailCard);
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
            ClearCurrentCardUIs();

            // 创建新卡牌
            currentCardUIs = CardUIComponents.Instance.CreateMultipleCardUI(cardDataList, fanDisplayContainer);

            // 为每张卡牌添加悬停检测
            foreach (var cardUI in currentCardUIs)
            {
                AddHoverDetection(cardUI);
            }

            LogDebug($"创建了 {currentCardUIs.Count} 张卡牌UI");
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

                // 计算位置
                Vector3 position = new Vector3(
                    fanCenter.x + Mathf.Sin(radians) * fanRadius,
                    fanCenter.y + Mathf.Cos(radians) * fanRadius,
                    0f
                );

                // 计算旋转
                Vector3 rotation = new Vector3(0, 0, -angle);

                fanPositions.Add(position);
                fanRotations.Add(rotation);
            }

            LogDebug($"计算了 {cardCount} 张卡牌的扇形位置");
        }

        /// <summary>
        /// 显示扇形展示
        /// </summary>
        private void ShowFanDisplay()
        {
            // 隐藏缩略图
            if (thumbnailContainer != null)
            {
                thumbnailContainer.gameObject.SetActive(false);
            }

            // 显示扇形卡牌
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

            LogDebug("扇形展示完成");
        }

        #endregion

        #region 动画处理

        /// <summary>
        /// 转换到扇形展示的动画
        /// </summary>
        private IEnumerator TransitionToFanDisplay()
        {
            float elapsed = 0f;

            // 获取缩略图的起始位置（需要考虑容器的缩放和位置）
            Vector3 containerWorldPos = thumbnailContainer.position;
            Vector3 containerScale = thumbnailContainer.localScale;

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
            ClearCurrentCardUIs();

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

            // 鼠标点击事件（为后续拖拽准备）
            EventTrigger.Entry pointerClick = new EventTrigger.Entry();
            pointerClick.eventID = EventTriggerType.PointerClick;
            pointerClick.callback.AddListener((eventData) => OnCardPointerClick(cardUI));
            eventTrigger.triggers.Add(pointerClick);
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

            LogDebug($"卡牌悬停进入: {cardUI.name}");
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

            LogDebug($"卡牌悬停离开: {cardUI.name}");
        }

        /// <summary>
        /// 卡牌点击事件（为后续拖拽准备）
        /// </summary>
        private void OnCardPointerClick(GameObject cardUI)
        {
            if (currentState != DisplayState.FanDisplay) return;

            // 触发选择事件
            OnCardSelected?.Invoke(cardUI);

            LogDebug($"卡牌被选择: {cardUI.name}");
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

        /// <summary>
        /// 隐藏所有卡牌
        /// </summary>
        private void HideAllCards()
        {
            // 隐藏缩略图
            if (thumbnailContainer != null)
            {
                thumbnailContainer.gameObject.SetActive(false);
            }

            // 清理扇形卡牌
            ClearCurrentCardUIs();

            // 清理缩略图卡牌
            ClearThumbnailCards();
        }

        /// <summary>
        /// 清理当前卡牌UI
        /// </summary>
        private void ClearCurrentCardUIs()
        {
            if (currentCardUIs.Count > 0)
            {
                CardUIComponents.Instance.DestroyMultipleCardUI(currentCardUIs);
                currentCardUIs.Clear();
            }

            fanPositions.Clear();
            fanRotations.Clear();
            hoveredCard = null;
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[CardDisplayUI] {message}");
            }
        }

        /// <summary>
        /// 警告日志
        /// </summary>
        private void LogWarning(string message)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[CardDisplayUI] {message}");
            }
        }

        #endregion

        #region 清理资源

        private void OnDestroy()
        {
            // 停止所有协程
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }

            if (hoverCoroutine != null)
            {
                StopCoroutine(hoverCoroutine);
            }

            // 清理UI
            ClearCurrentCardUIs();

            // 取消事件订阅
            UnsubscribeFromClassroomManagerEvents();

            LogDebug("CardDisplayUI已销毁，资源已清理");
        }

        #endregion
    }
}