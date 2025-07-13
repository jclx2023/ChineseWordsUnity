using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RoomScene.Data;
using RoomScene.Manager;
using Core.Network;

namespace UI
{
    /// <summary>
    /// 玩家模型UI项 - 统一ActionButton版本
    /// 显示：3D模型预览 + 模型切换按钮 + 玩家信息 + 统一行动按钮
    /// ActionButton根据玩家类型和本地/远程显示不同内容
    /// </summary>
    public class PlayerModelItemUI : MonoBehaviour
    {
        [Header("模型预览区域")]
        [SerializeField] private Transform modelPreviewContainer;
        [SerializeField] private Button previousModelButton;
        [SerializeField] private Button nextModelButton;

        [Header("玩家信息区域")]
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Text hostIndicatorText;          // 课代表标识文本
        [SerializeField] private Image playerBackgroundImage;

        [Header("行动控制区域")]
        [SerializeField] private Button actionButton;                // 统一的行动按钮
        [SerializeField] private TMP_Text actionButtonText;          // 按钮文本

        [Header("颜色配置")]
        [SerializeField] private Color hostBackgroundColor = new Color(1f, 0.8f, 0.2f, 0.3f);
        [SerializeField] private Color readyBackgroundColor = new Color(0.2f, 1f, 0.2f, 0.2f);
        [SerializeField] private Color notReadyBackgroundColor = new Color(1f, 0.2f, 0.2f, 0.2f);

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 数据引用
        private ushort playerId;
        private RoomPlayerData playerData;
        private RoomUIController parentController;
        private GameObject currentModelPreview;
        private bool isLocalPlayer;

        // 状态
        private bool isInitialized = false;

        #region 初始化

        /// <summary>
        /// 初始化玩家UI项
        /// </summary>
        public void Initialize(ushort id, RoomPlayerData data, RoomUIController controller)
        {
            playerId = id;
            playerData = data;
            parentController = controller;
            isLocalPlayer = (playerId == NetworkManager.Instance?.ClientId);

            SetupUIComponents();
            UpdateDisplay(playerData);
            ShowModelPreview(playerData.selectedModelId);

            isInitialized = true;
            LogDebug($"初始化玩家UI项: {playerData.playerName} (本地玩家: {isLocalPlayer})");
        }

        /// <summary>
        /// 设置UI组件
        /// </summary>
        private void SetupUIComponents()
        {
            // 设置模型切换按钮（仅本地玩家可用）
            if (previousModelButton != null)
            {
                previousModelButton.onClick.RemoveAllListeners();
                previousModelButton.onClick.AddListener(OnPreviousModelClicked);
                previousModelButton.gameObject.SetActive(isLocalPlayer);
            }

            if (nextModelButton != null)
            {
                nextModelButton.onClick.RemoveAllListeners();
                nextModelButton.onClick.AddListener(OnNextModelClicked);
                nextModelButton.gameObject.SetActive(isLocalPlayer);
            }

            // 设置统一的行动按钮
            if (actionButton != null)
            {
                actionButton.onClick.RemoveAllListeners();
                actionButton.onClick.AddListener(OnActionButtonClicked);
                UpdateActionButtonState();
            }

            // 确保预览容器存在
            if (modelPreviewContainer == null)
            {
                GameObject previewObj = new GameObject("ModelPreviewContainer");
                previewObj.transform.SetParent(transform, false);
                modelPreviewContainer = previewObj.transform;
            }
        }

        #endregion

        #region 显示更新

        /// <summary>
        /// 更新显示内容
        /// </summary>
        public void UpdateDisplay(RoomPlayerData data)
        {
            if (data == null) return;

            playerData = data;

            // 更新玩家名称
            if (playerNameText != null)
            {
                string displayName = playerData.playerName;
                if (isLocalPlayer)
                {
                    displayName += " (你)";
                }
                playerNameText.text = displayName;
            }

            // 更新课代表标识
            UpdateHostIndicator();

            // 更新背景颜色
            UpdateBackgroundColor();

            // 更新统一行动按钮状态
            UpdateActionButtonState();

            // 更新模型预览（如果模型ID发生变化）
            if (currentModelPreview == null ||
                playerData.selectedModelId != GetCurrentPreviewModelId())
            {
                ShowModelPreview(playerData.selectedModelId);
            }

            LogDebug($"更新玩家显示: {playerData.playerName}, 模型: {playerData.selectedModelId}, 准备: {playerData.isReady}");
        }

        /// <summary>
        /// 更新课代表标识
        /// </summary>
        private void UpdateHostIndicator()
        {
            if (hostIndicatorText != null)
            {
                if (playerData.isHost)
                {
                    hostIndicatorText.text = "课代表";
                    hostIndicatorText.color = Color.yellow;
                    hostIndicatorText.gameObject.SetActive(true);
                }
                else
                {
                    hostIndicatorText.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 更新背景颜色
        /// </summary>
        private void UpdateBackgroundColor()
        {
            if (playerBackgroundImage == null) return;

            Color backgroundColor;

            if (playerData.isHost)
            {
                backgroundColor = hostBackgroundColor;
            }
            else if (playerData.isReady)
            {
                backgroundColor = readyBackgroundColor;
            }
            else
            {
                backgroundColor = notReadyBackgroundColor;
            }

            playerBackgroundImage.color = backgroundColor;
        }

        /// <summary>
        /// 更新统一行动按钮状态
        /// </summary>
        private void UpdateActionButtonState()
        {
            if (actionButton == null) return;

            // actionButton始终显示，但内容和交互性不同
            actionButton.gameObject.SetActive(true);

            if (isLocalPlayer)
            {
                // 本地玩家的按钮
                if (playerData.isHost)
                {
                    // 房主显示"开始游戏"
                    if (actionButtonText != null)
                    {
                        actionButtonText.text = "开始游戏";
                    }

                    // 房主按钮是否可点击取决于是否所有人都准备好了
                    actionButton.interactable = CanStartGame();
                }
                else
                {
                    // 普通玩家显示"准备"/"取消准备"
                    if (actionButtonText != null)
                    {
                        actionButtonText.text = playerData.isReady ? "取消准备" : "准备";
                    }

                    actionButton.interactable = true;
                }
            }
            else
            {
                // 其他玩家的按钮 - 仅作状态显示，不可交互
                actionButton.interactable = false;

                if (actionButtonText != null)
                {
                    if (playerData.isHost)
                    {
                        // 看其他房主时，始终显示"已准备"
                        actionButtonText.text = "已准备";
                    }
                    else
                    {
                        // 看其他普通玩家时，显示其实际准备状态
                        actionButtonText.text = playerData.isReady ? "已准备" : "未准备";
                    }
                }
            }
        }

        /// <summary>
        /// 检查是否可以开始游戏（供房主按钮使用）
        /// </summary>
        private bool CanStartGame()
        {
            if (parentController == null) return false;

            // 通过父控制器检查游戏开始条件
            return parentController.CanStartGame();
        }

        #endregion

        #region 模型预览

        /// <summary>
        /// 显示模型预览
        /// </summary>
        private void ShowModelPreview(int modelId)
        {
            if (PlayerModelManager.Instance == null) return;

            // 清除当前预览
            ClearCurrentPreview();

            // 创建新预览
            currentModelPreview = PlayerModelManager.Instance.ShowModelPreview(modelId, modelPreviewContainer);

            LogDebug($"显示模型预览: ID {modelId}");
        }

        /// <summary>
        /// 清除当前预览
        /// </summary>
        private void ClearCurrentPreview()
        {
            if (currentModelPreview != null)
            {
                currentModelPreview.SetActive(false);
                currentModelPreview = null;
            }
        }

        /// <summary>
        /// 获取当前预览的模型ID
        /// </summary>
        private int GetCurrentPreviewModelId()
        {
            return playerData?.selectedModelId ?? 0;
        }

        #endregion

        #region 按钮事件

        /// <summary>
        /// 上一个模型按钮点击
        /// </summary>
        private void OnPreviousModelClicked()
        {
            if (!isLocalPlayer || PlayerModelManager.Instance == null) return;

            int currentModelId = playerData.selectedModelId;
            int previousModelId = PlayerModelManager.Instance.GetPreviousModelId(currentModelId);

            if (previousModelId != currentModelId)
            {
                ChangeModel(previousModelId);
            }
        }

        /// <summary>
        /// 下一个模型按钮点击
        /// </summary>
        private void OnNextModelClicked()
        {
            if (!isLocalPlayer || PlayerModelManager.Instance == null) return;

            int currentModelId = playerData.selectedModelId;
            int nextModelId = PlayerModelManager.Instance.GetNextModelId(currentModelId);

            if (nextModelId != currentModelId)
            {
                ChangeModel(nextModelId);
            }
        }

        /// <summary>
        /// 统一的行动按钮点击
        /// </summary>
        private void OnActionButtonClicked()
        {
            if (!isLocalPlayer) return; // 只有本地玩家能点击自己的按钮

            if (playerData.isHost)
            {
                // 房主点击开始游戏（实际逻辑由父控制器处理）
                if (parentController != null)
                {
                    parentController.OnPlayerReadyStateChanged(playerId, true); // 传true表示开始游戏
                }
                LogDebug("房主点击开始游戏按钮");
            }
            else
            {
                // 普通玩家切换准备状态
                bool newReadyState = !playerData.isReady;

                if (parentController != null)
                {
                    parentController.OnPlayerReadyStateChanged(playerId, newReadyState);
                }

                LogDebug($"点击个人准备按钮: {playerData.isReady} -> {newReadyState}");
            }
        }

        /// <summary>
        /// 改变模型
        /// </summary>
        private void ChangeModel(int newModelId)
        {
            if (!PlayerModelManager.Instance.IsModelAvailable(newModelId))
            {
                LogDebug($"模型 {newModelId} 不可用");
                return;
            }

            // 更新本地数据
            string modelName = PlayerModelManager.Instance.GetModelName(newModelId);
            playerData.SetSelectedModel(newModelId, modelName);

            // 更新预览
            ShowModelPreview(newModelId);

            // 通知父控制器（会触发网络同步）
            if (parentController != null)
            {
                parentController.OnPlayerModelSelectionChanged(playerId, newModelId);
            }

            LogDebug($"改变模型到: {modelName} (ID: {newModelId})");
        }

        #endregion

        #region 生命周期

        private void OnDestroy()
        {
            ClearCurrentPreview();
        }

        #endregion

        #region 辅助方法

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerModelItemUI_{playerId}] {message}");
            }
        }

        #endregion
    }
}