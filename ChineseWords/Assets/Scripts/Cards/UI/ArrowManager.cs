﻿using UnityEngine;
using Cards.Core;
using UI;

namespace Cards.UI
{
    /// <summary>
    /// 箭头管理器 - 负责贝塞尔箭头的生命周期和目标检测
    /// 使用NetworkUI的公共接口简化PlayerConsole访问
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
        private NetworkUI.PlayerConsoleInfo currentTargetConsole = null;
        private CardData currentCardData = null;

        // 依赖引用
        private Canvas parentCanvas;
        private NetworkUI networkUI;
        private Camera uiCamera;

        private ushort currentHighlightedPlayerId = 0;

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
            parentCanvas = canvas;
            arrowPrefab = arrowPrefabRef;

            // 根据Canvas模式设置摄像机
            if (parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                uiCamera = null; // Overlay模式不需要摄像机
            }
            else
            {
                uiCamera = parentCanvas.worldCamera ?? Camera.main;
            }

            networkUI = FindObjectOfType<NetworkUI>();

            LogDebug($"ArrowManager初始化 - Canvas模式: {parentCanvas.renderMode}, 摄像机: {(uiCamera?.name ?? "null")}");
        }

        /// <summary>
        /// 由CardUIManager调用，设置箭头预制体引用
        /// </summary>
        public void SetArrowPrefab(GameObject prefab)
        {
            arrowPrefab = prefab;
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

            // 实例化箭头预制体，直接在Canvas下而不是在ArrowManager下
            currentArrowInstance = Instantiate(arrowPrefab, parentCanvas.transform);
            currentArrowInstance.name = "ActiveArrow";

            currentArrowRenderer = currentArrowInstance.GetComponent<BezierArrowRenderer>();
            RectTransform arrowRect = currentArrowInstance.GetComponent<RectTransform>();
            // 启用外部控制模式
            currentArrowRenderer.EnableExternalControl(arrowStartPosition, arrowStartPosition);

            LogDebug($"箭头实例创建成功，父对象: {currentArrowInstance.transform.parent.name}");
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
            arrowStartPosition = position;

            if (currentArrowRenderer != null)
            {
                // 使用BezierArrowRenderer的新接口设置起点
                currentArrowRenderer.SetStartPosition(position);
                LogDebug($"箭头起点位置已设置: {position}");
            }
        }

        /// <summary>
        /// 清理箭头相关状态
        /// </summary>
        private void CleanupArrow()
        {
            // 清理BeChoosen效果
            ClearBeChoosenEffect();

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
        private bool IsMousePositionValid(Vector2 mousePos)
        {
            return mousePos.x >= 0 && mousePos.x <= Screen.width &&
                   mousePos.y >= 0 && mousePos.y <= Screen.height;
        }
        /// <summary>
        /// 更新目标检测
        /// </summary>
        private void UpdateTargetDetection()
        {
            // 获取鼠标位置
            Vector2 mouseScreenPos = Input.mousePosition;

            // 边界检查
            if (!IsMousePositionValid(mouseScreenPos))
            {
                UpdateTargetState(TargetDetectionResult.None, 0, null);
                return;
            }

            // 更新箭头终点位置
            UpdateArrowEndPosition(mouseScreenPos);

            // 检测目标
            TargetDetectionResult newTargetType = TargetDetectionResult.None;
            ushort newTargetPlayerId = 0;
            NetworkUI.PlayerConsoleInfo newTargetConsole = null;

            // 检测PlayerConsole (使用back图像)
            bool playerDetected = DetectPlayerConsoleTarget(mouseScreenPos, out newTargetPlayerId, out newTargetConsole);

            if (playerDetected)
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
        /// 更新箭头终点位置
        /// </summary>
        private void UpdateArrowEndPosition(Vector2 mouseScreenPos)
        {
            if (currentArrowRenderer == null) return;

            // 转换鼠标位置为Canvas坐标
            Vector2 mouseCanvasPos;
            RectTransform canvasRect = parentCanvas.GetComponent<RectTransform>();
            bool success = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, mouseScreenPos, parentCanvas.worldCamera, out mouseCanvasPos);

            if (success)
            {
                // 使用BezierArrowRenderer的新接口设置终点
                currentArrowRenderer.SetEndPosition(mouseCanvasPos);
            }
        }

        /// <summary>
        /// 检测PlayerConsole目标 - 直接使用back图像检测
        /// </summary>
        private bool DetectPlayerConsoleTarget(Vector2 screenPosition, out ushort playerId, out NetworkUI.PlayerConsoleInfo consoleInfo)
        {
            playerId = 0;
            consoleInfo = null;

            if (networkUI == null) return false;

            // 直接使用back图像区域检测
            playerId = networkUI.GetPlayerConsoleAtPoint(screenPosition, uiCamera);

            if (playerId > 0)
            {
                consoleInfo = networkUI.GetPlayerConsoleInfo(playerId);
                return consoleInfo != null;
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
        private void UpdateTargetState(TargetDetectionResult newTargetType, ushort newPlayerId, NetworkUI.PlayerConsoleInfo newConsole)
        {
            // 检查是否有变化
            bool targetChanged = currentTargetType != newTargetType ||
                                currentTargetPlayerId != newPlayerId;

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

        #region BeChoosen效果管理

        /// <summary>
        /// 更新BeChoosen效果 - 使用NetworkUI的公共接口
        /// </summary>
        private void UpdateBeChoosenEffects()
        {
            // 如果当前指向PlayerConsole，显示BeChoosen效果
            if (currentTargetType == TargetDetectionResult.PlayerConsole && currentTargetPlayerId > 0)
            {
                ShowBeChoosenEffect(currentTargetPlayerId);
            }
            else
            {
                // 清理高亮效果
                ClearBeChoosenEffect();
            }
        }

        /// <summary>
        /// 显示BeChoosen效果
        /// </summary>
        private void ShowBeChoosenEffect(ushort playerId)
        {
            if (networkUI == null) return;

            // 如果当前高亮的玩家不同，先清理旧的
            if (currentHighlightedPlayerId != 0 && currentHighlightedPlayerId != playerId)
            {
                networkUI.HidePlayerBeChosenEffect(currentHighlightedPlayerId);
            }

            // 显示新的高亮
            if (networkUI.ShowPlayerBeChosenEffect(playerId))
            {
                currentHighlightedPlayerId = playerId;
            }
            else
            {
            }
        }

        /// <summary>
        /// 清理BeChoosen效果
        /// </summary>
        private void ClearBeChoosenEffect()
        {
            if (networkUI == null || currentHighlightedPlayerId == 0) return;

            networkUI.HidePlayerBeChosenEffect(currentHighlightedPlayerId);
            currentHighlightedPlayerId = 0;
        }

        #endregion

        #region 箭头视觉更新

        /// <summary>
        /// 更新箭头视觉效果
        /// </summary>
        private void UpdateArrowVisuals()
        {
            if (currentArrowRenderer == null) return;

            // 根据目标状态设置箭头描边
            switch (currentTargetType)
            {
                case TargetDetectionResult.PlayerConsole:
                case TargetDetectionResult.CenterArea:
                    // 有效目标 - 显示绿色描边
                    currentArrowRenderer.SetOutlineEnabled(true);
                    currentArrowRenderer.SetOutlineColor(validTargetColor);
                    currentArrowRenderer.SetOutlineWidth(2.0f);
                    LogDebug($"箭头描边设置为有效状态: {validTargetColor}");
                    break;

                case TargetDetectionResult.Invalid:
                    // 无效目标 - 显示红色/灰色描边
                    currentArrowRenderer.SetOutlineEnabled(true);
                    currentArrowRenderer.SetOutlineColor(invalidTargetColor);
                    currentArrowRenderer.SetOutlineWidth(2.0f);
                    LogDebug($"箭头描边设置为无效状态: {invalidTargetColor}");
                    break;

                case TargetDetectionResult.None:
                default:
                    // 无目标 - 关闭描边
                    currentArrowRenderer.SetOutlineEnabled(false);
                    LogDebug("箭头描边已关闭");
                    break;
            }
        }

        #endregion

        #region 调试工具

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                //Debug.Log($"[ArrowManager] {message}");
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