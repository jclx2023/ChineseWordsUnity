using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Core.Network;
using System.Collections;
using Classroom.Player;

namespace UI
{
    /// <summary>
    /// 修复版游戏结束界面控制器
    /// 职责：
    /// 1. 监听游戏胜利和结束事件
    /// 2. 显示获胜者信息或游戏结束状态
    /// 3. 提供返回房间功能
    /// 4. 适配所有玩家（获胜者、失败者、观战者）
    /// 5. 摄像机控制：显示游戏结束UI时禁用摄像机控制，允许玩家点击UI
    /// </summary>
    public class GameEndUI : MonoBehaviour
    {
        [Header("UI面板")]
        [SerializeField] private GameObject gameEndPanel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text winnerInfoText;
        [SerializeField] private TMP_Text gameStatsText;
        [SerializeField] private TMP_Text statusMessageText;

        [Header("按钮")]
        [SerializeField] private Button returnToRoomButton;

        [Header("视觉效果")]
        [SerializeField] private CanvasGroup panelCanvasGroup;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private float fadeInDuration = 1.2f;

        [Header("颜色配置")]
        [SerializeField] private Color victoryColor = new Color(1f, 0.8f, 0.2f, 1f); // 金黄色
        [SerializeField] private Color defeatColor = new Color(0.8f, 0.8f, 0.8f, 1f); // 灰色
        [SerializeField] private Color spectatorColor = new Color(0.5f, 0.8f, 1f, 1f); // 蓝色

        [Header("文本配置")]
        [SerializeField] private string victoryTitle = "🎉 游戏胜利！";
        [SerializeField] private string defeatTitle = "🎯 游戏结束";
        [SerializeField] private string spectatorTitle = "📺 游戏结束";
        [SerializeField] private string noWinnerTitle = "⚡ 游戏结束";

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = false;

        // 私有状态
        private bool isPanelVisible = false;
        private ushort localPlayerId;
        private bool isLocalPlayerWinner = false;
        private bool isLocalPlayerAlive = false;
        private Coroutine fadeCoroutine;

        // 摄像机控制 - 新增
        private PlayerCameraController playerCameraController;
        private bool cameraControllerReady = false;

        #region Unity生命周期

        private void Awake()
        {
            // 初始化UI状态
            InitializeUI();
        }

        private void Start()
        {
            // 获取本地玩家信息
            GetLocalPlayerInfo();

            // 注册网络事件
            RegisterNetworkEvents();

            // 验证组件配置
            ValidateComponents();

            // 尝试立即查找摄像机控制器（如果已经初始化）
            if (!cameraControllerReady)
            {
                StartCoroutine(FindPlayerCameraControllerCoroutine());
            }

            LogDebug($"GameEndUI初始化完成 - 脚本启用状态: {this.enabled}");
            LogDebug($"NetworkManager.Instance存在: {NetworkManager.Instance != null}");
        }

        private void OnEnable()
        {
            SubscribeToClassroomManagerEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromClassroomManagerEvents();
        }

        private void OnDestroy()
        {
            // 清理事件订阅
            UnregisterNetworkEvents();
            UnsubscribeFromClassroomManagerEvents();

            // 停止协程
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
        }

        #endregion

        #region 事件订阅管理 - 新增

        /// <summary>
        /// 订阅ClassroomManager事件
        /// </summary>
        private void SubscribeToClassroomManagerEvents()
        {
            // 如果ClassroomManager存在，订阅其事件
            if (FindObjectOfType<Classroom.ClassroomManager>() != null)
            {
                Classroom.ClassroomManager.OnCameraSetupCompleted += OnCameraSetupCompleted;
                Classroom.ClassroomManager.OnClassroomInitialized += OnClassroomInitialized;
            }
        }

        /// <summary>
        /// 取消订阅ClassroomManager事件
        /// </summary>
        private void UnsubscribeFromClassroomManagerEvents()
        {
            // 如果ClassroomManager存在，取消订阅其事件
            if (FindObjectOfType<Classroom.ClassroomManager>() != null)
            {
                Classroom.ClassroomManager.OnCameraSetupCompleted -= OnCameraSetupCompleted;
                Classroom.ClassroomManager.OnClassroomInitialized -= OnClassroomInitialized;
            }
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

        #endregion

        #region 摄像机控制器查找 - 新增

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
                LogDebug("未能找到摄像机控制器，将使用备用方案");
                // 可以添加备用查找逻辑或错误处理
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

            // 方式3: 查找包含"Local"、"Player"等关键词的摄像机控制器
            foreach (var controller in controllers)
            {
                if (controller.gameObject.name.Contains("Local") ||
                    controller.gameObject.name.Contains("Player"))
                {
                    LogDebug($"通过关键词找到摄像机控制器: {controller.name}");
                    return controller;
                }
            }

            return null;
        }

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
                LogDebug("摄像机控制器不可用，无法控制摄像机");
            }
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 获取本地玩家信息
        /// </summary>
        private void GetLocalPlayerInfo()
        {
            if (NetworkManager.Instance != null)
            {
                localPlayerId = NetworkManager.Instance.ClientId;
                LogDebug($"本地玩家ID: {localPlayerId}");
            }
            else
            {
                Debug.LogWarning("[GameEndUI] NetworkManager未准备就绪，将延迟获取玩家ID");
                Invoke(nameof(RetryGetLocalPlayerInfo), 0.5f);
            }
        }

        /// <summary>
        /// 重试获取本地玩家信息
        /// </summary>
        private void RetryGetLocalPlayerInfo()
        {
            if (NetworkManager.Instance != null)
            {
                localPlayerId = NetworkManager.Instance.ClientId;
                LogDebug($"延迟获取本地玩家ID成功: {localPlayerId}");
            }
            else
            {
                Debug.LogError("[GameEndUI] NetworkManager仍未准备就绪");
            }
        }

        /// <summary>
        /// 初始化UI组件
        /// </summary>
        private void InitializeUI()
        {
            // 确保游戏结束面板初始隐藏
            if (gameEndPanel != null)
            {
                gameEndPanel.SetActive(false);
                LogDebug("gameEndPanel初始状态设为隐藏");
            }

            // 初始化CanvasGroup
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = 0f;
                panelCanvasGroup.interactable = false;
                panelCanvasGroup.blocksRaycasts = false;
                LogDebug("CanvasGroup初始化完成");
            }

            // 绑定按钮事件
            if (returnToRoomButton != null)
            {
                returnToRoomButton.onClick.AddListener(OnReturnToRoomButtonClicked);
                LogDebug("返回房间按钮事件绑定完成");
            }

            LogDebug("GameEndUI UI组件初始化完成");
        }

        /// <summary>
        /// 验证组件配置
        /// </summary>
        private void ValidateComponents()
        {
            bool hasErrors = false;

            if (gameEndPanel == null)
            {
                Debug.LogError("[GameEndUI] ❌ 缺少gameEndPanel引用");
                hasErrors = true;
            }
            else
            {
                LogDebug("✓ gameEndPanel引用正常");
            }

            if (titleText == null)
            {
                Debug.LogWarning("[GameEndUI] ⚠️ 缺少titleText引用");
            }
            else
            {
                LogDebug("✓ titleText引用正常");
            }

            if (returnToRoomButton == null)
            {
                Debug.LogWarning("[GameEndUI] ⚠️ 缺少returnToRoomButton引用");
            }
            else
            {
                LogDebug("✓ returnToRoomButton引用正常");
            }

            if (panelCanvasGroup == null)
            {
                Debug.LogWarning("[GameEndUI] ⚠️ 缺少panelCanvasGroup引用，将无法使用淡入效果");
            }
            else
            {
                LogDebug("✓ panelCanvasGroup引用正常");
            }

            if (hasErrors)
            {
                Debug.LogError("[GameEndUI] ❌ 关键组件缺失，禁用脚本");
                this.enabled = false;
            }
            else
            {
                LogDebug("✓ 所有关键组件验证通过");
            }
        }

        #endregion

        #region 网络事件处理

        /// <summary>
        /// 注册网络事件
        /// </summary>
        private void RegisterNetworkEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnGameVictory += OnGameVictoryReceived;
                NetworkManager.OnGameEndWithoutWinner += OnGameEndWithoutWinnerReceived;
                NetworkManager.OnForceReturnToRoom += OnForceReturnToRoomReceived;

                LogDebug("✓ 网络事件注册完成");
            }
            else
            {
                Debug.LogWarning("[GameEndUI] NetworkManager未找到，将延迟注册事件");
                Invoke(nameof(RetryRegisterEvents), 1f);
            }
        }

        /// <summary>
        /// 注销网络事件
        /// </summary>
        private void UnregisterNetworkEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnGameVictory -= OnGameVictoryReceived;
                NetworkManager.OnGameEndWithoutWinner -= OnGameEndWithoutWinnerReceived;
                NetworkManager.OnForceReturnToRoom -= OnForceReturnToRoomReceived;
                LogDebug("网络事件注销完成");
            }
        }

        /// <summary>
        /// 重试注册事件
        /// </summary>
        private void RetryRegisterEvents()
        {
            if (NetworkManager.Instance != null)
            {
                RegisterNetworkEvents();
            }
        }

        /// <summary>
        /// 处理游戏胜利事件
        /// </summary>
        private void OnGameVictoryReceived(ushort winnerId, string winnerName, string reason)
        {
            LogDebug($"★★★ OnGameVictoryReceived被调用 - 获胜者: {winnerName} (ID: {winnerId}) ★★★");

            // 检查本地玩家状态
            CheckLocalPlayerStatus();

            // 判断本地玩家是否为获胜者
            isLocalPlayerWinner = (winnerId == localPlayerId);

            LogDebug($"本地玩家ID: {localPlayerId}, 获胜者ID: {winnerId}, 是否为获胜者: {isLocalPlayerWinner}");

            // 显示胜利界面
            ShowGameEndPanel(true, winnerId, winnerName, reason);
        }

        /// <summary>
        /// 处理游戏无胜利者结束事件
        /// </summary>
        private void OnGameEndWithoutWinnerReceived(string reason)
        {
            LogDebug($"★★★ OnGameEndWithoutWinnerReceived被调用: {reason} ★★★");

            // 检查本地玩家状态
            CheckLocalPlayerStatus();

            // 显示游戏结束界面
            ShowGameEndPanel(false, 0, "", reason);
        }

        /// <summary>
        /// 处理强制返回房间事件
        /// </summary>
        private void OnForceReturnToRoomReceived(string reason)
        {
            LogDebug($"收到强制返回房间事件: {reason}");
            HideGameEndPanel();
        }

        #endregion

        #region 界面显示控制

        /// <summary>
        /// 检查本地玩家状态
        /// </summary>
        private void CheckLocalPlayerStatus()
        {
            // 检查本地玩家是否死亡（通过PlayerDeathUI判断）
            var playerDeathUI = FindObjectOfType<PlayerDeathUI>();
            if (playerDeathUI != null)
            {
                isLocalPlayerAlive = !playerDeathUI.IsLocalPlayerDead;
                LogDebug($"通过PlayerDeathUI检查本地玩家状态: 存活={isLocalPlayerAlive}");
            }
            else
            {
                // 如果没有PlayerDeathUI，默认认为存活
                isLocalPlayerAlive = true;
                LogDebug("未找到PlayerDeathUI，默认本地玩家存活");
            }

            LogDebug($"本地玩家状态检查: 存活={isLocalPlayerAlive}");
        }

        /// <summary>
        /// 显示游戏结束面板
        /// </summary>
        private void ShowGameEndPanel(bool hasWinner, ushort winnerId, string winnerName, string reason)
        {
            LogDebug($"★★★ ShowGameEndPanel被调用 - hasWinner: {hasWinner}, winnerName: {winnerName} ★★★");

            if (isPanelVisible)
            {
                LogDebug("游戏结束面板已显示，忽略重复调用");
                return;
            }

            LogDebug($"开始显示游戏结束面板");

            isPanelVisible = true;

            // 设置为最后渲染（最顶层）
            EnsureTopLayer();

            // 禁用摄像机控制 - 新增
            SetCameraControl(false);

            // 更新面板内容
            UpdateGameEndPanelContent(hasWinner, winnerId, winnerName, reason);

            // 激活面板
            LogDebug($"gameEndPanel是否为null: {gameEndPanel == null}");
            if (gameEndPanel != null)
            {
                LogDebug($"设置gameEndPanel.SetActive(true)");
                gameEndPanel.SetActive(true);
                LogDebug($"gameEndPanel当前状态: {gameEndPanel.activeSelf}");
                LogDebug($"gameEndPanel在Hierarchy中是否激活: {gameEndPanel.activeInHierarchy}");
            }

            // 播放淡入动画或直接显示
            if (panelCanvasGroup != null)
            {
                LogDebug($"开始淡入动画 - 当前alpha: {panelCanvasGroup.alpha}");
                if (fadeCoroutine != null)
                {
                    StopCoroutine(fadeCoroutine);
                }
                fadeCoroutine = StartCoroutine(FadeInPanel());
            }
            else
            {
                LogDebug("panelCanvasGroup为null，直接启用交互");
                EnablePanelInteraction();
            }

            LogDebug("★★★ 游戏结束面板显示逻辑完成 ★★★");
        }

        /// <summary>
        /// 隐藏游戏结束面板
        /// </summary>
        private void HideGameEndPanel()
        {
            if (!isPanelVisible)
            {
                return;
            }

            LogDebug("隐藏游戏结束面板");

            isPanelVisible = false;

            // 启用摄像机控制 - 新增
            SetCameraControl(true);

            // 播放淡出动画或直接隐藏
            if (panelCanvasGroup != null)
            {
                if (fadeCoroutine != null)
                {
                    StopCoroutine(fadeCoroutine);
                }
                fadeCoroutine = StartCoroutine(FadeOutPanel());
            }
            else
            {
                // 直接隐藏
                if (gameEndPanel != null)
                {
                    gameEndPanel.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 更新游戏结束面板内容
        /// </summary>
        private void UpdateGameEndPanelContent(bool hasWinner, ushort winnerId, string winnerName, string reason)
        {
            LogDebug($"更新面板内容 - hasWinner: {hasWinner}, winnerName: {winnerName}");

            // 确定玩家状态和对应的UI样式
            PlayerEndStatus playerStatus = DeterminePlayerStatus(hasWinner, winnerId);
            LogDebug($"玩家状态确定: {playerStatus}");

            // 更新标题
            UpdateTitleText(playerStatus);

            // 更新获胜者信息
            UpdateWinnerInfo(hasWinner, winnerName, reason);

            // 更新游戏统计
            UpdateGameStats();

            // 更新状态消息
            UpdateStatusMessage(playerStatus);

            // 更新视觉样式
            UpdateVisualStyle(playerStatus);

            LogDebug("面板内容更新完成");
        }

        /// <summary>
        /// 确定玩家结束状态
        /// </summary>
        private PlayerEndStatus DeterminePlayerStatus(bool hasWinner, ushort winnerId)
        {
            if (!hasWinner)
            {
                return PlayerEndStatus.NoWinner;
            }

            if (isLocalPlayerWinner)
            {
                return PlayerEndStatus.Victory;
            }

            if (isLocalPlayerAlive)
            {
                return PlayerEndStatus.Defeat;
            }
            else
            {
                return PlayerEndStatus.Spectator;
            }
        }

        /// <summary>
        /// 更新标题文本
        /// </summary>
        private void UpdateTitleText(PlayerEndStatus status)
        {
            if (titleText == null) return;

            string title = status switch
            {
                PlayerEndStatus.Victory => victoryTitle,
                PlayerEndStatus.Defeat => defeatTitle,
                PlayerEndStatus.Spectator => spectatorTitle,
                PlayerEndStatus.NoWinner => noWinnerTitle,
                _ => defeatTitle
            };

            titleText.text = title;
            LogDebug($"标题更新为: {title}");
        }

        /// <summary>
        /// 更新获胜者信息
        /// </summary>
        private void UpdateWinnerInfo(bool hasWinner, string winnerName, string reason)
        {
            if (winnerInfoText == null) return;

            if (hasWinner)
            {
                winnerInfoText.text = $"获胜者: {winnerName}";
            }
            else
            {
                winnerInfoText.text = reason; // 显示游戏结束原因
            }

            LogDebug($"获胜者信息更新为: {winnerInfoText.text}");
        }

        /// <summary>
        /// 更新游戏统计
        /// </summary>
        private void UpdateGameStats()
        {
            if (gameStatsText == null) return;

            // 获取基本的游戏统计信息
            string stats = GetGameStatistics();
            gameStatsText.text = stats;
            LogDebug($"游戏统计更新为: {stats}");
        }

        /// <summary>
        /// 更新状态消息
        /// </summary>
        private void UpdateStatusMessage(PlayerEndStatus status)
        {
            if (statusMessageText == null) return;

            string message = status switch
            {
                PlayerEndStatus.Victory => "恭喜您获得了胜利！",
                PlayerEndStatus.Defeat => "很遗憾，这次没有获胜",
                PlayerEndStatus.Spectator => "感谢您的观战",
                PlayerEndStatus.NoWinner => "本局游戏没有获胜者",
                _ => ""
            };

            statusMessageText.text = message;
            LogDebug($"状态消息更新为: {message}");
        }

        /// <summary>
        /// 更新视觉样式
        /// </summary>
        private void UpdateVisualStyle(PlayerEndStatus status)
        {
            if (backgroundImage == null) return;

            Color backgroundColor = status switch
            {
                PlayerEndStatus.Victory => victoryColor,
                PlayerEndStatus.Defeat => defeatColor,
                PlayerEndStatus.Spectator => spectatorColor,
                PlayerEndStatus.NoWinner => defeatColor,
                _ => defeatColor
            };

            // 设置背景颜色（半透明）
            backgroundColor.a = 0.8f;
            backgroundImage.color = backgroundColor;
            LogDebug($"视觉样式更新为: {status}");
        }

        #endregion

        #region 层级管理

        /// <summary>
        /// 确保GameEndUI在最高层级 - 修复版
        /// </summary>
        private void EnsureTopLayer()
        {
            // 使用Transform层级而不是Canvas sortingOrder
            if (transform != null)
            {
                // 将当前GameObject移到Transform层级的最后（最顶层）
                transform.SetAsLastSibling();
                LogDebug($"GameEndUI已移到Transform层级最顶层: {transform.GetSiblingIndex()}");
            }

            // 如果有独立的Canvas组件，也设置sortingOrder
            Canvas thisCanvas = GetComponent<Canvas>();
            if (thisCanvas != null)
            {
                thisCanvas.sortingOrder = 100; // 设置一个高优先级
                LogDebug($"GameEndUI独立Canvas层级设置为: {thisCanvas.sortingOrder}");
            }
        }

        #endregion

        #region 动画效果

        /// <summary>
        /// 淡入动画
        /// </summary>
        private IEnumerator FadeInPanel()
        {
            LogDebug($"★ FadeInPanel开始 - 初始alpha: {panelCanvasGroup.alpha}");

            float elapsedTime = 0f;
            float startAlpha = panelCanvasGroup.alpha;

            while (elapsedTime < fadeInDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / fadeInDuration;
                panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, progress);
                yield return null;
            }

            panelCanvasGroup.alpha = 1f;
            LogDebug($"★ FadeInPanel完成 - 最终alpha: {panelCanvasGroup.alpha}");
            EnablePanelInteraction();
        }

        /// <summary>
        /// 淡出动画
        /// </summary>
        private IEnumerator FadeOutPanel()
        {
            LogDebug($"FadeOutPanel开始 - 初始alpha: {panelCanvasGroup.alpha}");

            float elapsedTime = 0f;
            float startAlpha = panelCanvasGroup.alpha;

            // 先禁用交互
            DisablePanelInteraction();

            while (elapsedTime < fadeInDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / fadeInDuration;
                panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, progress);
                yield return null;
            }

            panelCanvasGroup.alpha = 0f;
            LogDebug("FadeOutPanel完成");

            // 隐藏面板
            if (gameEndPanel != null)
            {
                gameEndPanel.SetActive(false);
            }
        }

        /// <summary>
        /// 启用面板交互
        /// </summary>
        private void EnablePanelInteraction()
        {
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.interactable = true;
                panelCanvasGroup.blocksRaycasts = true;
                LogDebug("面板交互已启用");
            }
        }

        /// <summary>
        /// 禁用面板交互
        /// </summary>
        private void DisablePanelInteraction()
        {
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.interactable = false;
                panelCanvasGroup.blocksRaycasts = false;
                LogDebug("面板交互已禁用");
            }
        }

        #endregion

        #region 按钮事件处理

        /// <summary>
        /// 返回房间按钮点击
        /// </summary>
        private void OnReturnToRoomButtonClicked()
        {
            LogDebug("返回房间按钮被点击");

            // 请求返回房间
            RequestReturnToRoom();
        }

        #endregion

        #region 游戏逻辑调用

        /// <summary>
        /// 请求返回房间
        /// </summary>
        private void RequestReturnToRoom()
        {
            LogDebug($"请求返回房间 - 玩家ID: {localPlayerId}");

            // 调用HostGameManager的返回房间方法
            if (HostGameManager.Instance != null)
            {
                HostGameManager.Instance.RequestReturnToRoom(localPlayerId, "游戏结束后返回");
                LogDebug("✓ 已向HostGameManager发送返回房间请求");
            }
            else
            {
                Debug.LogError("[GameEndUI] HostGameManager未找到，无法请求返回房间");
            }

            // 隐藏游戏结束面板
            HideGameEndPanel();
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取游戏统计信息
        /// </summary>
        private string GetGameStatistics()
        {
            // 尝试从HostGameManager获取统计信息
            if (HostGameManager.Instance != null)
            {
                int questionNumber = HostGameManager.Instance.CurrentQuestionNumber;
                int playerCount = HostGameManager.Instance.PlayerCount;

                return $"总题目数: {questionNumber}\n参与玩家: {playerCount}";
            }

            // 如果HostGameManager不可用，提供基本信息
            if (NetworkManager.Instance != null)
            {
                int playerCount = NetworkManager.Instance.PlayerCount;
                return $"参与玩家: {playerCount}";
            }

            return "游戏已结束";
        }

        #endregion

        #region 玩家状态枚举

        private enum PlayerEndStatus
        {
            Victory,    // 胜利
            Defeat,     // 失败（存活但未获胜）
            Spectator,  // 观战者（已死亡）
            NoWinner    // 无获胜者
        }

        #endregion

        #region 公共接口

        public bool IsPanelVisible => isPanelVisible;

        #endregion

        #region 调试方法

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[GameEndUI] {message}");
            }
        }

        #endregion
    }
}