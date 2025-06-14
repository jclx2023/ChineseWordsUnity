using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Core.Network;
using System.Collections;

namespace UI
{
    /// <summary>
    /// 简化版玩家死亡界面控制器
    /// 职责：
    /// 1. 监听玩家死亡事件，判断是否为本地玩家
    /// 2. 显示死亡信息和自动观战提示
    /// 3. 提供"退出房间"选择
    /// 4. 观战模式：死亡玩家自动移出回合池，但继续接收游戏广播
    /// </summary>
    public class PlayerDeathUI : MonoBehaviour
    {
        [Header("UI面板")]
        [SerializeField] private GameObject deathPanel;
        [SerializeField] private TMP_Text deathMessageText;
        [SerializeField] private TMP_Text deathReasonText;
        [SerializeField] private TMP_Text spectatorStatusText;
        [SerializeField] private TMP_Text instructionText;

        [Header("按钮")]
        [SerializeField] private Button exitToRoomButton;
        [SerializeField] private Button minimizeButton; // 可选：最小化死亡面板

        [Header("视觉效果")]
        [SerializeField] private CanvasGroup panelCanvasGroup;
        [SerializeField] private float fadeInDuration = 1f;
        [SerializeField] private float autoMinimizeDelay = 8f; // 8秒后自动最小化

        [Header("文本配置")]
        [SerializeField] private string defaultDeathMessage = "你已被淘汰";
        [SerializeField] private string spectatorModeText = "已自动进入观战模式";
        [SerializeField] private string spectatorInstructionText = "你可以继续观看其他玩家的游戏进程";
        [SerializeField] private string exitInstructionText = "点击下方按钮可退出到房间";

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 私有状态
        private bool isLocalPlayerDead = false;
        private bool isPanelVisible = false;
        private bool isPanelMinimized = false;
        private ushort localPlayerId;
        private string localPlayerName;
        private Coroutine fadeCoroutine;
        private Coroutine autoMinimizeCoroutine;

        #region Unity生命周期

        private void Awake()
        {
            // 初始化UI状态
            InitializeUI();
        }

        private void Start()
        {
            // 获取本地玩家信息（在NetworkManager准备好后）
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
            if (autoMinimizeCoroutine != null)
            {
                StopCoroutine(autoMinimizeCoroutine);
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
                Debug.LogWarning("[PlayerDeathUI] NetworkManager未准备就绪，将延迟获取玩家ID");
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
                Debug.LogError("[PlayerDeathUI] NetworkManager仍未准备就绪，死亡检测可能不准确");
            }
        }

        /// <summary>
        /// 初始化UI组件
        /// </summary>
        private void InitializeUI()
        {
            // 确保死亡面板初始隐藏
            if (deathPanel != null)
            {
                deathPanel.SetActive(false);
            }

            // 初始化CanvasGroup
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = 0f;
                panelCanvasGroup.interactable = false;
                panelCanvasGroup.blocksRaycasts = false;
            }

            // 绑定按钮事件
            if (exitToRoomButton != null)
            {
                exitToRoomButton.onClick.AddListener(OnExitToRoomButtonClicked);
            }

            if (minimizeButton != null)
            {
                minimizeButton.onClick.AddListener(OnMinimizeButtonClicked);
            }

            // 设置默认文本
            SetDefaultTexts();

            LogDebug("简化版PlayerDeathUI初始化完成");
        }

        /// <summary>
        /// 设置默认文本内容
        /// </summary>
        private void SetDefaultTexts()
        {
            if (deathMessageText != null)
            {
                deathMessageText.text = defaultDeathMessage;
            }

            if (spectatorStatusText != null)
            {
                spectatorStatusText.text = spectatorModeText;
            }

            if (instructionText != null)
            {
                instructionText.text = $"{spectatorInstructionText}\n{exitInstructionText}";
            }

            // 清空死亡原因（将在实际死亡时设置）
            if (deathReasonText != null)
            {
                deathReasonText.text = "";
            }
        }

        /// <summary>
        /// 验证组件配置
        /// </summary>
        private void ValidateComponents()
        {
            bool hasErrors = false;

            if (deathPanel == null)
            {
                Debug.LogError("[PlayerDeathUI] 缺少deathPanel引用");
                hasErrors = true;
            }

            if (exitToRoomButton == null)
            {
                Debug.LogWarning("[PlayerDeathUI] 缺少exitToRoomButton引用");
            }

            if (panelCanvasGroup == null)
            {
                Debug.LogWarning("[PlayerDeathUI] 缺少panelCanvasGroup引用，将无法使用淡入效果");
            }

            if (hasErrors)
            {
                Debug.LogError("[PlayerDeathUI] 关键组件缺失，功能可能异常");
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
                NetworkManager.OnPlayerDied += OnPlayerDiedReceived;
                NetworkManager.OnGameVictory += OnGameVictoryReceived;
                NetworkManager.OnGameEndWithoutWinner += OnGameEndWithoutWinnerReceived;
                NetworkManager.OnForceReturnToRoom += OnForceReturnToRoomReceived;

                LogDebug("✓ 网络事件注册完成");
            }
            else
            {
                Debug.LogWarning("[PlayerDeathUI] NetworkManager未找到，将延迟注册事件");
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
                NetworkManager.OnPlayerDied -= OnPlayerDiedReceived;
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
        /// 处理玩家死亡事件
        /// </summary>
        private void OnPlayerDiedReceived(ushort playerId, string playerName)
        {
            LogDebug($"收到玩家死亡事件: {playerName} (ID: {playerId})");

            // 如果本地玩家ID未获取到，尝试重新获取
            if (localPlayerId == 0 && NetworkManager.Instance != null)
            {
                localPlayerId = NetworkManager.Instance.ClientId;
                LogDebug($"重新获取本地玩家ID: {localPlayerId}");
            }

            // **增强调试信息**
            LogDebug($"玩家ID比较: 死亡玩家={playerId}, 本地玩家={localPlayerId}, 是否匹配={playerId == localPlayerId}");

            // 检查是否为本地玩家
            if (playerId == localPlayerId)
            {
                LogDebug($"★★★ 本地玩家死亡: {playerName}，进入观战模式 ★★★");
                localPlayerName = playerName;
                EnterSpectatorMode(playerName, "血量归零");
            }
            else
            {
                LogDebug($"其他玩家死亡: {playerName}，本地玩家继续游戏");
            }
        }

        /// <summary>
        /// 处理游戏胜利事件（延迟隐藏死亡界面）
        /// </summary>
        private void OnGameVictoryReceived(ushort winnerId, string winnerName, string reason)
        {
            LogDebug($"收到游戏胜利事件: {winnerName} 获胜");

            // 如果当前玩家死亡UI正在显示，给用户一些时间看到死亡信息
            if (isPanelVisible && isLocalPlayerDead)
            {
                LogDebug("死亡界面正在显示，延迟隐藏以显示胜利信息");

                // 更新死亡面板文本，显示胜利信息
                if (instructionText != null)
                {
                    instructionText.text = $"游戏结束！\n{winnerName} 获胜！\n\n{spectatorInstructionText}\n{exitInstructionText}";
                }

                // 延迟5秒后隐藏（让用户有时间看到信息）
                Invoke(nameof(HideDeathPanel), 5f);
            }
            else
            {
                // 如果死亡界面没有显示，立即隐藏
                HideDeathPanel();
            }
        }

        /// <summary>
        /// 处理游戏无胜利者结束事件
        /// </summary>
        private void OnGameEndWithoutWinnerReceived(string reason)
        {
            LogDebug($"收到游戏结束事件: {reason}");

            // 如果当前玩家死亡UI正在显示，显示游戏结束信息
            if (isPanelVisible && isLocalPlayerDead)
            {
                LogDebug("死亡界面正在显示，延迟隐藏以显示结束信息");

                // 更新死亡面板文本，显示结束信息
                if (instructionText != null)
                {
                    instructionText.text = $"游戏结束！\n{reason}\n\n{exitInstructionText}";
                }

                // 延迟5秒后隐藏
                Invoke(nameof(HideDeathPanel), 5f);
            }
            else
            {
                HideDeathPanel();
            }
        }

        /// <summary>
        /// 处理强制返回房间事件
        /// </summary>
        private void OnForceReturnToRoomReceived(string reason)
        {
            LogDebug($"收到强制返回房间事件: {reason}");
            HideDeathPanel();
        }

        #endregion

        #region 观战模式控制

        /// <summary>
        /// 进入观战模式（本地玩家死亡时调用）
        /// </summary>
        private void EnterSpectatorMode(string playerName = null, string deathReason = null)
        {
            if (isPanelVisible)
            {
                LogDebug("死亡面板已显示，忽略重复调用");
                return;
            }

            LogDebug($"进入观战模式 - 玩家: {playerName}, 原因: {deathReason}");

            // 标记本地玩家死亡状态
            isLocalPlayerDead = true;
            isPanelVisible = true;
            isPanelMinimized = false;

            // 更新文本内容
            UpdateDeathPanelTexts(playerName, deathReason);

            // 显示面板
            ShowDeathPanel();

            // 设置自动最小化
            if (autoMinimizeDelay > 0)
            {
                if (autoMinimizeCoroutine != null)
                {
                    StopCoroutine(autoMinimizeCoroutine);
                }
                autoMinimizeCoroutine = StartCoroutine(AutoMinimizeAfterDelay());
            }

            LogDebug("观战模式已启用，玩家现在可以观看游戏进程");
        }

        /// <summary>
        /// 自动最小化延迟协程
        /// </summary>
        private IEnumerator AutoMinimizeAfterDelay()
        {
            yield return new WaitForSeconds(autoMinimizeDelay);

            if (isPanelVisible && !isPanelMinimized)
            {
                LogDebug($"自动最小化死亡面板（延迟{autoMinimizeDelay}秒）");
                MinimizePanel();
            }
        }

        #endregion

        #region 界面显示控制

        /// <summary>
        /// 显示死亡面板
        /// </summary>
        private void ShowDeathPanel()
        {
            LogDebug("显示死亡面板");

            // ✅ 确保PlayerDeathUI在合适的层级
            EnsureProperLayer();

            // 激活面板
            if (deathPanel != null)
            {
                deathPanel.SetActive(true);
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
        }

        /// <summary>
        /// 隐藏死亡面板
        /// </summary>
        private void HideDeathPanel()
        {
            if (!isPanelVisible)
            {
                return;
            }

            LogDebug("隐藏死亡面板");

            isPanelVisible = false;
            isPanelMinimized = false;

            // 停止自动最小化协程
            if (autoMinimizeCoroutine != null)
            {
                StopCoroutine(autoMinimizeCoroutine);
                autoMinimizeCoroutine = null;
            }

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
                if (deathPanel != null)
                {
                    deathPanel.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 最小化面板
        /// </summary>
        private void MinimizePanel()
        {
            if (!isPanelVisible || isPanelMinimized)
                return;

            LogDebug("最小化死亡面板");
            isPanelMinimized = true;

            // 淡出到半透明状态，但保持可见
            if (panelCanvasGroup != null)
            {
                if (fadeCoroutine != null)
                {
                    StopCoroutine(fadeCoroutine);
                }
                fadeCoroutine = StartCoroutine(FadeToMinimized());
            }
        }

        /// <summary>
        /// 恢复面板（从最小化状态）
        /// </summary>
        private void RestorePanel()
        {
            if (!isPanelVisible || !isPanelMinimized)
                return;

            LogDebug("恢复死亡面板");
            isPanelMinimized = false;

            // 恢复到完全可见状态
            if (panelCanvasGroup != null)
            {
                if (fadeCoroutine != null)
                {
                    StopCoroutine(fadeCoroutine);
                }
                fadeCoroutine = StartCoroutine(FadeInPanel());
            }
        }

        /// <summary>
        /// 更新死亡面板文本
        /// </summary>
        private void UpdateDeathPanelTexts(string playerName, string deathReason)
        {
            // 更新死亡消息
            if (deathMessageText != null)
            {
                if (!string.IsNullOrEmpty(playerName))
                {
                    deathMessageText.text = $"{playerName} 已被淘汰";
                }
                else
                {
                    deathMessageText.text = defaultDeathMessage;
                }
            }

            // 更新死亡原因
            if (deathReasonText != null)
            {
                if (!string.IsNullOrEmpty(deathReason))
                {
                    deathReasonText.text = $"淘汰原因: {deathReason}";
                }
                else
                {
                    deathReasonText.text = "";
                }
            }

            // 更新观战状态信息
            if (spectatorStatusText != null)
            {
                spectatorStatusText.text = spectatorModeText;
            }

            // 更新指导文本
            if (instructionText != null)
            {
                instructionText.text = $"{spectatorInstructionText}\n{exitInstructionText}";
            }
        }

        #endregion

        #region 层级管理

        /// <summary>
        /// 确保PlayerDeathUI在合适的层级
        /// </summary>
        private void EnsureProperLayer()
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

            // 设置为比题型管理器高，但比GameEndUI低
            int newSortingOrder = Mathf.Max(15, maxSortingOrder + 5);
            thisCanvas.sortingOrder = newSortingOrder;

            LogDebug($"PlayerDeathUI层级已设置为: {newSortingOrder}");
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
            if (deathPanel != null)
            {
                deathPanel.SetActive(false);
            }
        }

        /// <summary>
        /// 淡化到最小化状态
        /// </summary>
        private IEnumerator FadeToMinimized()
        {
            float elapsedTime = 0f;
            float startAlpha = panelCanvasGroup.alpha;
            float targetAlpha = 0.3f; // 最小化时的透明度

            while (elapsedTime < fadeInDuration * 0.5f) // 更快的淡化
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / (fadeInDuration * 0.5f);
                panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
                yield return null;
            }

            panelCanvasGroup.alpha = targetAlpha;
            // 保持交互性，但降低可见度
        }
        private void EnablePanelInteraction()
        {
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.interactable = true;
                panelCanvasGroup.blocksRaycasts = true;
            }
        }
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
        private void OnExitToRoomButtonClicked()
        {
            LogDebug("退出房间按钮被点击");

            if (!isLocalPlayerDead)
            {
                LogDebug("本地玩家未死亡，无法退出房间");
                return;
            }

            // 请求返回房间
            RequestReturnToRoom();
        }

        private void OnMinimizeButtonClicked()
        {
            LogDebug("最小化按钮被点击");

            if (isPanelMinimized)
            {
                RestorePanel();
            }
            else
            {
                MinimizePanel();
            }
        }

        #endregion

        #region 游戏逻辑调用
        private void RequestReturnToRoom()
        {
            LogDebug($"请求返回房间 - 玩家ID: {localPlayerId}");

            // 调用HostGameManager的返回房间方法
            if (HostGameManager.Instance != null)
            {
                HostGameManager.Instance.RequestReturnToRoom(localPlayerId, "死亡玩家请求退出");
                LogDebug("✓ 已向HostGameManager发送返回房间请求");
            }
            else
            {
                Debug.LogError("[PlayerDeathUI] HostGameManager未找到，无法请求返回房间");
            }

            // 隐藏死亡面板
            HideDeathPanel();
        }

        #endregion

        #region 公共接口
        public bool IsLocalPlayerDead => isLocalPlayerDead;
        public bool IsPanelVisible => isPanelVisible;
        public bool IsPanelMinimized => isPanelMinimized;

        #endregion

        #region 调试方法
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerDeathUI] {message}");
            }
        }
        #endregion
    }
}