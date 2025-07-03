using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Cards.Core;
using UI;

namespace Cards.UI
{
    /// <summary>
    /// 箭头管理器 - 负责贝塞尔箭头的生命周期和目标检测
    /// </summary>
    public class ArrowManager : MonoBehaviour
    {
        [Header("箭头预制体")]
        private GameObject arrowPrefab; // 由CardUIManager传入，不在Inspector中设置

        [Header("目标检测设置")]
        [SerializeField] private LayerMask uiLayerMask = -1; // UI检测层级
        [SerializeField] private float detectionRadius = 50f; // PlayerConsole检测半径

        [Header("中央区域设置（从CardUIManager同步）")]
        [SerializeField] private float releaseAreaCenterX = 0.5f;
        [SerializeField] private float releaseAreaCenterY = 0.5f;
        [SerializeField] private float releaseAreaWidth = 0.3f;
        [SerializeField] private float releaseAreaHeight = 0.3f;

        [Header("视觉反馈设置")]
        [SerializeField] private Color normalArrowColor = Color.white;
        [SerializeField] private Color validTargetColor = Color.green;
        [SerializeField] private Color invalidTargetColor = Color.gray;
        [SerializeField] private float beChoosenFadeSpeed = 5f; // BeChoosen渐变速度

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool showDebugGizmos = false;

        // 目标类型枚举
        public enum TargetDetectionResult
        {
            None,           // 无目标
            PlayerConsole,  // 指向玩家Console
            CenterArea,     // 指向中央区域
            Invalid         // 无效区域
        }

        // 箭头状态
        private bool isArrowActive = false;
        private GameObject currentArrowInstance = null;
        private BezierArrowRenderer currentArrowRenderer = null;
        private Vector2 arrowStartPosition = Vector2.zero;

        // 目标检测相关
        private TargetDetectionResult currentTargetType = TargetDetectionResult.None;
        private ushort currentTargetPlayerId = 0;
        private GameObject currentTargetConsole = null;
        private CardData currentCardData = null;

        // 依赖引用
        private Canvas parentCanvas;
        private NetworkUI networkUI;
        private Camera uiCamera;

        // BeChoosen效果管理
        private Dictionary<GameObject, CanvasGroup> beChoosenCanvasGroups = new Dictionary<GameObject, CanvasGroup>();

        // 事件
        public System.Action<ushort> OnValidPlayerTargetDetected; // 检测到有效玩家目标
        public System.Action OnValidCenterAreaDetected; // 检测到有效中央区域
        public System.Action OnInvalidTargetDetected; // 检测到无效目标
        public System.Action OnNoTargetDetected; // 无目标

        #region Unity生命周期

        private void Update()
        {
            if (isArrowActive && currentArrowInstance != null)
            {
                UpdateTargetDetection();
                UpdateArrowVisuals();
                UpdateBeChoosenEffects();
            }
        }

        private void OnDestroy()
        {
            CleanupArrow();
        }

        #endregion

        #region 初始化和设置

        /// <summary>
        /// 由CardUIManager调用的初始化方法
        /// </summary>
        public void Initialize(Canvas canvas, GameObject arrowPrefabRef)
        {
            // 设置依赖引用
            parentCanvas = canvas;
            arrowPrefab = arrowPrefabRef;

            // 验证必要组件
            if (parentCanvas == null)
            {
                LogError("Canvas引用为空！");
                return;
            }

            if (arrowPrefab == null)
            {
                LogError("箭头预制体引用为空！");
                return;
            }

            // 获取UI摄像机
            uiCamera = parentCanvas.worldCamera;

            // 查找NetworkUI
            networkUI = FindObjectOfType<NetworkUI>();
            if (networkUI == null)
            {
                LogWarning("未找到NetworkUI组件，玩家目标检测可能无法正常工作");
            }

            LogDebug("ArrowManager初始化完成");
        }

        /// <summary>
        /// 由CardUIManager调用，设置箭头预制体引用
        /// </summary>
        public void SetArrowPrefab(GameObject prefab)
        {
            arrowPrefab = prefab;
            LogDebug($"箭头预制体已设置: {prefab?.name}");
        }

        /// <summary>
        /// 检查ArrowManager是否已正确初始化
        /// </summary>
        public bool IsInitialized()
        {
            return parentCanvas != null && arrowPrefab != null;
        }

        /// <summary>
        /// 同步中央区域设置（由CardUIManager调用）
        /// </summary>
        public void SyncCenterAreaSettings(float centerX, float centerY, float width, float height)
        {
            releaseAreaCenterX = centerX;
            releaseAreaCenterY = centerY;
            releaseAreaWidth = width;
            releaseAreaHeight = height;

            LogDebug($"中央区域设置已同步: Center({centerX}, {centerY}), Size({width}, {height})");
        }

        #endregion

        #region 箭头生命周期管理

        /// <summary>
        /// 开始箭头目标选择
        /// </summary>
        public bool StartArrowTargeting(Vector2 startPosition, CardData cardData)
        {
            if (!IsInitialized())
            {
                LogError("ArrowManager未正确初始化，无法开始箭头目标选择");
                return false;
            }

            if (isArrowActive)
            {
                LogWarning("箭头已经处于激活状态，无法重复开始");
                return false;
            }

            LogDebug($"开始箭头目标选择 - 起点: {startPosition}, 卡牌: {cardData?.cardName}");

            // 保存状态
            arrowStartPosition = startPosition;
            currentCardData = cardData;

            // 创建箭头实例
            if (!CreateArrowInstance())
            {
                return false;
            }

            // 设置箭头起点
            SetArrowStartPosition(startPosition);

            // 激活箭头
            isArrowActive = true;

            return true;
        }

        /// <summary>
        /// 结束箭头目标选择
        /// </summary>
        public TargetDetectionResult EndArrowTargeting(out ushort targetPlayerId)
        {
            targetPlayerId = 0;

            if (!isArrowActive)
            {
                LogWarning("箭头未激活，无法结束目标选择");
                return TargetDetectionResult.None;
            }

            LogDebug($"结束箭头目标选择 - 最终目标类型: {currentTargetType}");

            // 保存最终结果
            TargetDetectionResult finalResult = currentTargetType;
            targetPlayerId = currentTargetPlayerId;

            // 清理状态
            CleanupArrow();

            return finalResult;
        }

        /// <summary>
        /// 取消箭头目标选择
        /// </summary>
        public void CancelArrowTargeting()
        {
            if (!isArrowActive)
            {
                return;
            }

            LogDebug("取消箭头目标选择");

            // 清理状态
            CleanupArrow();
        }

        /// <summary>
        /// 创建箭头实例
        /// </summary>
        private bool CreateArrowInstance()
        {
            if (currentArrowInstance != null)
            {
                LogWarning("箭头实例已存在，先清理旧实例");
                DestroyArrowInstance();
            }

            // 实例化箭头预制体
            currentArrowInstance = Instantiate(arrowPrefab, transform);
            currentArrowInstance.name = "ActiveArrow";

            // 获取BezierArrowRenderer组件
            currentArrowRenderer = currentArrowInstance.GetComponent<BezierArrowRenderer>();
            if (currentArrowRenderer == null)
            {
                LogError("箭头预制体缺少BezierArrowRenderer组件！");
                DestroyArrowInstance();
                return false;
            }

            // 禁用BezierArrowRenderer的Update（我们手动控制）
            currentArrowRenderer.enabled = false;

            LogDebug("箭头实例创建成功");
            return true;
        }

        /// <summary>
        /// 销毁箭头实例
        /// </summary>
        private void DestroyArrowInstance()
        {
            if (currentArrowInstance != null)
            {
                Destroy(currentArrowInstance);
                currentArrowInstance = null;
                currentArrowRenderer = null;
                LogDebug("箭头实例已销毁");
            }
        }

        /// <summary>
        /// 设置箭头起点位置
        /// </summary>
        private void SetArrowStartPosition(Vector2 position)
        {
            if (currentArrowInstance != null)
            {
                RectTransform arrowRect = currentArrowInstance.GetComponent<RectTransform>();
                if (arrowRect != null)
                {
                    arrowRect.anchoredPosition = position;
                }
            }
        }

        /// <summary>
        /// 清理箭头相关状态
        /// </summary>
        private void CleanupArrow()
        {
            // 清理BeChoosen效果
            ClearAllBeChoosenEffects();

            // 销毁箭头实例
            DestroyArrowInstance();

            // 重置状态
            isArrowActive = false;
            currentTargetType = TargetDetectionResult.None;
            currentTargetPlayerId = 0;
            currentTargetConsole = null;
            currentCardData = null;
            arrowStartPosition = Vector2.zero;

            LogDebug("箭头状态已清理");
        }

        #endregion

        #region 目标检测

        /// <summary>
        /// 更新目标检测
        /// </summary>
        private void UpdateTargetDetection()
        {
            // 获取鼠标位置
            Vector2 mouseScreenPos = Input.mousePosition;

            // 先检测PlayerConsole
            TargetDetectionResult newTargetType = TargetDetectionResult.None;
            ushort newTargetPlayerId = 0;
            GameObject newTargetConsole = null;

            if (DetectPlayerConsoleTarget(mouseScreenPos, out newTargetPlayerId, out newTargetConsole))
            {
                newTargetType = TargetDetectionResult.PlayerConsole;
            }
            else if (DetectCenterAreaTarget(mouseScreenPos))
            {
                newTargetType = TargetDetectionResult.CenterArea;
            }

            // 验证目标有效性
            if (newTargetType != TargetDetectionResult.None)
            {
                if (!ValidateTarget(newTargetType, newTargetPlayerId))
                {
                    newTargetType = TargetDetectionResult.Invalid;
                }
            }

            // 更新目标状态
            UpdateTargetState(newTargetType, newTargetPlayerId, newTargetConsole);
        }

        /// <summary>
        /// 检测PlayerConsole目标
        /// </summary>
        private bool DetectPlayerConsoleTarget(Vector2 screenPosition, out ushort playerId, out GameObject console)
        {
            playerId = 0;
            console = null;

            if (networkUI == null)
            {
                return false;
            }

            // 获取所有PlayerConsole
            var playerConsoles = GetAllPlayerConsoles();

            foreach (var consoleData in playerConsoles)
            {
                if (consoleData.console == null) continue;

                // 检查鼠标是否在Console范围内
                if (IsPointInConsole(screenPosition, consoleData.console))
                {
                    playerId = consoleData.playerId;
                    console = consoleData.console;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 检测中央区域目标
        /// </summary>
        private bool DetectCenterAreaTarget(Vector2 screenPosition)
        {
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            Vector2 mousePercent = new Vector2(screenPosition.x / screenSize.x, screenPosition.y / screenSize.y);

            // 计算中央区域边界
            float leftBound = releaseAreaCenterX - releaseAreaWidth / 2f;
            float rightBound = releaseAreaCenterX + releaseAreaWidth / 2f;
            float bottomBound = releaseAreaCenterY - releaseAreaHeight / 2f;
            float topBound = releaseAreaCenterY + releaseAreaHeight / 2f;

            // 检查是否在区域内
            return mousePercent.x >= leftBound && mousePercent.x <= rightBound &&
                   mousePercent.y >= bottomBound && mousePercent.y <= topBound;
        }

        /// <summary>
        /// 验证目标是否有效
        /// </summary>
        private bool ValidateTarget(TargetDetectionResult targetType, ushort playerId)
        {
            if (currentCardData == null)
            {
                return false;
            }

            switch (currentCardData.targetType)
            {
                case TargetType.Self:
                case TargetType.AllPlayers:
                    // 自发型卡牌只能拖到中央区域
                    return targetType == TargetDetectionResult.CenterArea;

                case TargetType.SinglePlayer:
                case TargetType.AllOthers:
                case TargetType.Random:
                    // 指向型卡牌必须指向PlayerConsole
                    return targetType == TargetDetectionResult.PlayerConsole && playerId > 0;

                default:
                    return false;
            }
        }

        /// <summary>
        /// 更新目标状态
        /// </summary>
        private void UpdateTargetState(TargetDetectionResult newTargetType, ushort newPlayerId, GameObject newConsole)
        {
            // 检查是否有变化
            bool targetChanged = currentTargetType != newTargetType ||
                                currentTargetPlayerId != newPlayerId ||
                                currentTargetConsole != newConsole;

            if (!targetChanged) return;

            // 更新状态
            currentTargetType = newTargetType;
            currentTargetPlayerId = newPlayerId;
            currentTargetConsole = newConsole;

            // 触发相应事件
            switch (currentTargetType)
            {
                case TargetDetectionResult.PlayerConsole:
                    OnValidPlayerTargetDetected?.Invoke(currentTargetPlayerId);
                    LogDebug($"检测到有效玩家目标: {currentTargetPlayerId}");
                    break;

                case TargetDetectionResult.CenterArea:
                    OnValidCenterAreaDetected?.Invoke();
                    LogDebug("检测到有效中央区域目标");
                    break;

                case TargetDetectionResult.Invalid:
                    OnInvalidTargetDetected?.Invoke();
                    LogDebug("检测到无效目标");
                    break;

                case TargetDetectionResult.None:
                    OnNoTargetDetected?.Invoke();
                    break;
            }
        }

        #endregion

        #region PlayerConsole相关

        /// <summary>
        /// PlayerConsole数据结构
        /// </summary>
        private struct PlayerConsoleData
        {
            public ushort playerId;
            public GameObject console;
        }

        /// <summary>
        /// 获取所有PlayerConsole
        /// </summary>
        private List<PlayerConsoleData> GetAllPlayerConsoles()
        {
            var consoles = new List<PlayerConsoleData>();

            if (networkUI == null) return consoles;

            // 使用反射获取NetworkUI的playerUIItems
            var playerUIItemsField = typeof(NetworkUI).GetField("playerUIItems",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (playerUIItemsField != null)
            {
                var playerUIItems = playerUIItemsField.GetValue(networkUI) as System.Collections.IDictionary;
                if (playerUIItems != null)
                {
                    foreach (System.Collections.DictionaryEntry entry in playerUIItems)
                    {
                        var playerId = (ushort)entry.Key;
                        var components = entry.Value;

                        // 获取itemObject
                        var itemObjectField = components.GetType().GetField("itemObject");
                        if (itemObjectField != null)
                        {
                            var itemObject = itemObjectField.GetValue(components) as GameObject;
                            if (itemObject != null)
                            {
                                consoles.Add(new PlayerConsoleData
                                {
                                    playerId = playerId,
                                    console = itemObject
                                });
                            }
                        }
                    }
                }
            }

            return consoles;
        }

        /// <summary>
        /// 检查点是否在Console范围内
        /// </summary>
        private bool IsPointInConsole(Vector2 screenPoint, GameObject console)
        {
            RectTransform consoleRect = console.GetComponent<RectTransform>();
            if (consoleRect == null) return false;

            // 转换为本地坐标进行检测
            Vector2 localPoint;
            bool success = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                consoleRect, screenPoint, uiCamera, out localPoint);

            if (!success) return false;

            // 检查是否在矩形范围内
            Rect rect = consoleRect.rect;
            return rect.Contains(localPoint);
        }

        #endregion

        #region BeChoosen效果管理

        /// <summary>
        /// 更新BeChoosen效果
        /// </summary>
        private void UpdateBeChoosenEffects()
        {
            // 清理之前的效果
            ClearAllBeChoosenEffects();

            // 如果当前指向PlayerConsole，显示BeChoosen效果
            if (currentTargetType == TargetDetectionResult.PlayerConsole && currentTargetConsole != null)
            {
                ShowBeChoosenEffect(currentTargetConsole);
            }
        }

        /// <summary>
        /// 显示BeChoosen效果
        /// </summary>
        private void ShowBeChoosenEffect(GameObject console)
        {
            // 查找BeChoosen图像
            Transform beChoosenTransform = FindChildRecursive(console.transform, "BeChoosen");
            if (beChoosenTransform == null) return;

            GameObject beChoosenObject = beChoosenTransform.gameObject;
            beChoosenObject.SetActive(true);

            // 设置渐变效果
            CanvasGroup canvasGroup = beChoosenObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = beChoosenObject.AddComponent<CanvasGroup>();
            }

            // 记录CanvasGroup用于后续管理
            if (!beChoosenCanvasGroups.ContainsKey(beChoosenObject))
            {
                beChoosenCanvasGroups[beChoosenObject] = canvasGroup;
            }

            // 渐变到可见
            StartCoroutine(FadeBeChoosen(canvasGroup, 1f));
        }

        /// <summary>
        /// 清理所有BeChoosen效果
        /// </summary>
        private void ClearAllBeChoosenEffects()
        {
            var keysToRemove = new List<GameObject>();

            foreach (var kvp in beChoosenCanvasGroups)
            {
                if (kvp.Key == null)
                {
                    keysToRemove.Add(kvp.Key);
                    continue;
                }

                // 渐变到隐藏
                StartCoroutine(FadeBeChoosen(kvp.Value, 0f, () => {
                    if (kvp.Key != null)
                    {
                        kvp.Key.SetActive(false);
                    }
                }));
            }

            // 清理无效引用
            foreach (var key in keysToRemove)
            {
                beChoosenCanvasGroups.Remove(key);
            }

            beChoosenCanvasGroups.Clear();
        }

        /// <summary>
        /// BeChoosen渐变协程
        /// </summary>
        private System.Collections.IEnumerator FadeBeChoosen(CanvasGroup canvasGroup, float targetAlpha, System.Action onComplete = null)
        {
            if (canvasGroup == null) yield break;

            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;
            float duration = 1f / beChoosenFadeSpeed;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
            onComplete?.Invoke();
        }

        #endregion

        #region 箭头视觉更新

        /// <summary>
        /// 更新箭头视觉效果
        /// </summary>
        private void UpdateArrowVisuals()
        {
            if (currentArrowRenderer == null) return;

            // 根据目标状态设置箭头颜色
            Color targetColor = GetArrowColorForTarget();

            // TODO: 应用颜色到箭头节点
            // 这里需要根据shader实现来设置颜色
            // 目前暂时不实现，等shader完成后再添加
        }

        /// <summary>
        /// 根据目标类型获取箭头颜色
        /// </summary>
        private Color GetArrowColorForTarget()
        {
            switch (currentTargetType)
            {
                case TargetDetectionResult.PlayerConsole:
                case TargetDetectionResult.CenterArea:
                    return validTargetColor;

                case TargetDetectionResult.Invalid:
                    return invalidTargetColor;

                default:
                    return normalArrowColor;
            }
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 递归查找子对象
        /// </summary>
        private Transform FindChildRecursive(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name)
                    return child;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                Transform found = FindChildRecursive(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>
        /// 获取当前检测结果信息
        /// </summary>
        public string GetCurrentTargetInfo()
        {
            switch (currentTargetType)
            {
                case TargetDetectionResult.PlayerConsole:
                    return $"玩家目标: {currentTargetPlayerId}";
                case TargetDetectionResult.CenterArea:
                    return "中央区域目标";
                case TargetDetectionResult.Invalid:
                    return "无效目标";
                default:
                    return "无目标";
            }
        }

        #endregion

        #region 调试工具

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;

            // 绘制中央区域
            DrawCenterAreaGizmo();

            // 绘制箭头起点
            if (isArrowActive)
            {
                Gizmos.color = Color.yellow;
                Vector3 worldPos = transform.TransformPoint(arrowStartPosition);
                Gizmos.DrawWireSphere(worldPos, 20f);
            }
        }

        /// <summary>
        /// 绘制中央区域
        /// </summary>
        private void DrawCenterAreaGizmo()
        {
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);

            float leftBound = (releaseAreaCenterX - releaseAreaWidth / 2f) * screenSize.x;
            float rightBound = (releaseAreaCenterX + releaseAreaWidth / 2f) * screenSize.x;
            float bottomBound = (releaseAreaCenterY - releaseAreaHeight / 2f) * screenSize.y;
            float topBound = (releaseAreaCenterY + releaseAreaHeight / 2f) * screenSize.y;

            Gizmos.color = Color.cyan;
            Vector3 center = new Vector3((leftBound + rightBound) / 2f, (bottomBound + topBound) / 2f, 0f);
            Vector3 size = new Vector3(rightBound - leftBound, topBound - bottomBound, 1f);

            Gizmos.DrawWireCube(center, size);
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[ArrowManager] {message}");
            }
        }

        private void LogWarning(string message)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[ArrowManager] {message}");
            }
        }

        private void LogError(string message)
        {
            Debug.LogError($"[ArrowManager] {message}");
        }

        #endregion

        #region 公共属性和接口

        public bool IsArrowActive => isArrowActive;
        public TargetDetectionResult CurrentTargetType => currentTargetType;
        public ushort CurrentTargetPlayerId => currentTargetPlayerId;

        /// <summary>
        /// 静态工厂方法 - 由CardUIManager调用创建ArrowManager实例
        /// </summary>
        public static ArrowManager CreateArrowManager(Transform parent, Canvas canvas, GameObject arrowPrefab)
        {
            // 创建ArrowManager GameObject
            GameObject arrowManagerObj = new GameObject("ArrowManager");
            arrowManagerObj.transform.SetParent(parent, false);

            // 添加ArrowManager组件
            ArrowManager arrowManager = arrowManagerObj.AddComponent<ArrowManager>();

            // 初始化
            arrowManager.Initialize(canvas, arrowPrefab);

            return arrowManager;
        }

        #endregion
    }
}