using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Core.Network;
using System.Collections;

namespace UI
{
    /// <summary>
    /// 简易游戏结束界面控制器
    /// 职责：
    /// 1. 监听游戏胜利和结束事件
    /// 2. 显示获胜者信息或游戏结束状态
    /// 3. 提供返回房间功能
    /// 4. 适配所有玩家（获胜者、失败者、观战者）
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
        [SerializeField] private bool enableDebugLogs = true;

        // 私有状态
        private bool isPanelVisible = false;
        private ushort localPlayerId;
        private bool isLocalPlayerWinner = false;
        private bool isLocalPlayerAlive = false;
        private Coroutine fadeCoroutine;

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
        }

        private void OnDestroy()
        {
            // 清理事件订阅
            UnregisterNetworkEvents();

            // 停止协程
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
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
            }

            // 初始化CanvasGroup
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = 0f;
                panelCanvasGroup.interactable = false;
                panelCanvasGroup.blocksRaycasts = false;
            }

            // 绑定按钮事件
            if (returnToRoomButton != null)
            {
                returnToRoomButton.onClick.AddListener(OnReturnToRoomButtonClicked);
            }

            LogDebug("GameEndUI初始化完成");
        }

        /// <summary>
        /// 验证组件配置
        /// </summary>
        private void ValidateComponents()
        {
            bool hasErrors = false;

            if (gameEndPanel == null)
            {
                Debug.LogError("[GameEndUI] 缺少gameEndPanel引用");
                hasErrors = true;
            }

            if (titleText == null)
            {
                Debug.LogWarning("[GameEndUI] 缺少titleText引用");
            }

            if (returnToRoomButton == null)
            {
                Debug.LogWarning("[GameEndUI] 缺少returnToRoomButton引用");
            }

            if (panelCanvasGroup == null)
            {
                Debug.LogWarning("[GameEndUI] 缺少panelCanvasGroup引用，将无法使用淡入效果");
            }

            if (hasErrors)
            {
                Debug.LogError("[GameEndUI] 关键组件缺失，功能可能异常");
                this.enabled = false;
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
            LogDebug($"收到游戏胜利事件: 获胜者 {winnerName} (ID: {winnerId})");

            // 检查本地玩家状态
            CheckLocalPlayerStatus();

            // 判断本地玩家是否为获胜者
            isLocalPlayerWinner = (winnerId == localPlayerId);

            // 显示胜利界面
            ShowGameEndPanel(true, winnerId, winnerName, reason);
        }

        /// <summary>
        /// 处理游戏无胜利者结束事件
        /// </summary>
        private void OnGameEndWithoutWinnerReceived(string reason)
        {
            LogDebug($"收到游戏结束事件: {reason}");

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
            }
            else
            {
                // 如果没有PlayerDeathUI，默认认为存活
                isLocalPlayerAlive = true;
            }

            LogDebug($"本地玩家状态检查: 存活={isLocalPlayerAlive}");
        }

        /// <summary>
        /// 显示游戏结束面板
        /// </summary>
        private void ShowGameEndPanel(bool hasWinner, ushort winnerId, string winnerName, string reason)
        {
            if (isPanelVisible)
            {
                LogDebug("游戏结束面板已显示，忽略重复调用");
                return;
            }

            LogDebug($"显示游戏结束面板 - 有获胜者: {hasWinner}, 获胜者: {winnerName}");

            isPanelVisible = true;

            // ✅ 确保GameEndUI在最高层级
            EnsureTopLayer();

            // 更新面板内容
            UpdateGameEndPanelContent(hasWinner, winnerId, winnerName, reason);

            // 激活面板
            if (gameEndPanel != null)
            {
                gameEndPanel.SetActive(true);
            }

            // 播放淡入动画
            if (panelCanvasGroup != null)
            {
                if (fadeCoroutine != null)
                {
                    StopCoroutine(fadeCoroutine);
                }
                fadeCoroutine = StartCoroutine(FadeInPanel());
            }
            else
            {
                // 如果没有CanvasGroup，直接启用交互
                EnablePanelInteraction();
            }

            LogDebug("游戏结束面板显示完成");
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
            // 确定玩家状态和对应的UI样式
            PlayerEndStatus playerStatus = DeterminePlayerStatus(hasWinner, winnerId);

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
        }

        #endregion

        #region 层级管理

        /// <summary>
        /// 确保GameEndUI在最高层级
        /// </summary>
        private void EnsureTopLayer()
        {
            // 获取当前GameObject的Canvas组件
            Canvas thisCanvas = GetComponentInParent<Canvas>();
            if (thisCanvas == null)
            {
                LogDebug("未找到Canvas组件，无法设置层级");
                return;
            }

            // 查找所有Canvas，找到最高的sortingOrder
            Canvas[] allCanvases = FindObjectsOfType<Canvas>();
            int maxSortingOrder = 0;

            foreach (Canvas canvas in allCanvases)
            {
                if (canvas != thisCanvas && canvas.sortingOrder > maxSortingOrder)
                {
                    maxSortingOrder = canvas.sortingOrder;
                }
            }

            // 设置为比最高层级还要高
            int newSortingOrder = maxSortingOrder + 10;
            thisCanvas.sortingOrder = newSortingOrder;

            LogDebug($"GameEndUI层级已设置为: {newSortingOrder} (原最高层级: {maxSortingOrder})");
        }

        #endregion

        #region 动画效果

        /// <summary>
        /// 淡入动画
        /// </summary>
        private IEnumerator FadeInPanel()
        {
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
            EnablePanelInteraction();
        }

        /// <summary>
        /// 淡出动画
        /// </summary>
        private IEnumerator FadeOutPanel()
        {
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