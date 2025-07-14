using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RoomScene.Data;
using RoomScene.Manager;
using Core.Network;

namespace UI
{
    /// <summary>
    /// 玩家模型UI项 - 图片按钮版本
    /// 显示：3D模型预览 + 模型切换按钮 + 玩家信息 + 图片状态按钮
    /// ActionButton使用图片而非文字来显示不同状态
    /// </summary>
    public class PlayerModelItemUI : MonoBehaviour
    {
        [Header("模型预览区域")]
        [SerializeField] private Transform modelPreviewContainer;
        [SerializeField] private Button previousModelButton;
        [SerializeField] private Button nextModelButton;

        [Header("玩家信息区域")]
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private Image hostIndicatorImage;           // 课代表标识图片
        [SerializeField] private Image playerBackgroundImage;

        [Header("行动控制区域")]
        [SerializeField] private Button actionButton;                // 统一的行动按钮
        [SerializeField] private Image actionButtonIconImage;        // 按钮子物体的Icon Image组件

        [Header("按钮状态图片 - 本地玩家")]
        [SerializeField] private Sprite localPlayerReadyIconSprite;          // 本地玩家：准备按钮图标
        [SerializeField] private Sprite localPlayerCancelReadyIconSprite;    // 本地玩家：取消准备按钮图标
        [SerializeField] private Sprite localHostStartGameIconSprite;        // 本地房主：开始游戏按钮图标
        [SerializeField] private Sprite localHostStartGameDisabledIconSprite;   // 本地房主：开始游戏禁用按钮图标

        [Header("按钮状态图片 - 远程玩家")]
        [SerializeField] private Sprite remotePlayerReadyIconSprite;         // 远程玩家：已准备状态图标
        [SerializeField] private Sprite remotePlayerNotReadyIconSprite;      // 远程玩家：未准备状态图标
        [SerializeField] private Sprite remoteHostReadyIconSprite;           // 远程房主：已准备状态图标

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

            // 确保显示正确的模型预览
            int modelIdToShow = playerData.selectedModelId;

            // 如果是远程玩家且模型ID是默认值，尝试从NetworkManager获取
            if (!isLocalPlayer && modelIdToShow == PlayerModelManager.Instance.GetDefaultModelId())
            {
                int networkModelId = NetworkManager.Instance.GetPlayerModelId(playerId);
                if (networkModelId != PlayerModelManager.Instance.GetDefaultModelId())
                {
                    modelIdToShow = networkModelId;
                    playerData.SetSelectedModel(modelIdToShow, PlayerModelManager.Instance.GetModelName(modelIdToShow));
                    LogDebug($"从NetworkManager获取玩家{playerId}的模型ID: {modelIdToShow}");
                }
            }

            ShowModelPreview(modelIdToShow);

            isInitialized = true;
            LogDebug($"初始化玩家UI项: {playerData.playerName} (本地玩家: {isLocalPlayer}, 模型ID: {modelIdToShow})");
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

            // 验证图片组件
            ValidateImageComponents();
        }

        /// <summary>
        /// 验证图片组件引用
        /// </summary>
        private void ValidateImageComponents()
        {
            if (actionButtonIconImage == null)
            {
                // 尝试从actionButton的第一个子物体获取Image组件
                if (actionButton != null && actionButton.transform.childCount > 0)
                {
                    actionButtonIconImage = actionButton.transform.GetChild(0).GetComponent<Image>();
                }

                if (actionButtonIconImage == null)
                {
                    LogDebug("警告：ActionButton的Icon Image组件未找到！");
                }
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

            // 保存旧的模型ID用于比较 - 修复：在赋值前保存，避免引用问题
            int oldModelId = playerData?.selectedModelId ?? -1;
            // 保存当前预览状态
            bool hadPreview = currentModelPreview != null;
            string oldPreviewName = currentModelPreview?.name ?? "null";

            // 更新数据引用
            playerData = data;
            int newModelId = playerData.selectedModelId;

            // 如果是远程玩家，尝试从NetworkManager获取最新的模型ID
            if (!isLocalPlayer)
            {
                int networkModelId = NetworkManager.Instance?.GetPlayerModelId(playerId) ?? PlayerModelManager.Instance.GetDefaultModelId();
                if (networkModelId != PlayerModelManager.Instance.GetDefaultModelId() && networkModelId != newModelId)
                {
                    LogDebug($"检测到网络模型ID不一致: 本地数据={newModelId}, 网络数据={networkModelId}");
                    newModelId = networkModelId;
                    playerData.SetSelectedModel(networkModelId, PlayerModelManager.Instance.GetModelName(networkModelId));
                }
            }

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

            // 更新统一行动按钮状态
            UpdateActionButtonState();

            // 检查是否需要更新模型预览
            bool needsPreviewUpdate = ShouldUpdateModelPreview(oldModelId, newModelId, hadPreview);

            if (needsPreviewUpdate)
            {
                LogDebug($"需要更新模型预览: {oldModelId} -> {newModelId}");
                UpdateModelPreview(newModelId);
            }
            else
            {
                LogDebug($"无需更新模型预览");
            }

            LogDebug($"UpdateDisplay完成");
        }

        private bool ShouldUpdateModelPreview(int oldModelId, int newModelId, bool hadPreview)
        {
            // 情况1: 当前没有预览对象
            if (currentModelPreview == null)
            {
                LogDebug($"需要更新原因: 当前预览为空");
                return true;
            }

            // 情况2: 模型ID发生变化
            if (oldModelId != newModelId)
            {
                LogDebug($"需要更新原因: 模型ID变化 {oldModelId} -> {newModelId}");
                return true;
            }

            // 情况3: 预览对象不活跃
            if (!currentModelPreview.activeInHierarchy)
            {
                LogDebug($"需要更新原因: 预览对象不活跃");
                return true;
            }

            // 情况4: 预览对象的父级错误
            if (currentModelPreview.transform.parent != modelPreviewContainer)
            {
                LogDebug($"需要更新原因: 预览对象父级错误");
                return true;
            }

            // 情况5: 强制检查预览对象是否与期望的模型匹配
            string expectedPreviewName = PlayerModelManager.Instance?.GetModelName(newModelId);
            if (!string.IsNullOrEmpty(expectedPreviewName) && !currentModelPreview.name.Contains(expectedPreviewName))
            {
                LogDebug($"需要更新原因: 预览对象名称不匹配，期望包含:{expectedPreviewName}, 实际:{currentModelPreview.name}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// 更新模型预览
        /// </summary>
        private void UpdateModelPreview(int modelId)
        {
            // 先清理旧预览
            ClearCurrentPreview();
            // 创建新预览
            ShowModelPreview(modelId);
        }

        /// <summary>
        /// 更新课代表标识
        /// </summary>
        private void UpdateHostIndicator()
        {
            if (hostIndicatorImage != null)
            {
                hostIndicatorImage.gameObject.SetActive(playerData.isHost);
            }
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
                UpdateLocalPlayerButtonState();
            }
            else
            {
                UpdateRemotePlayerButtonState();
            }
        }

        /// <summary>
        /// 更新本地玩家按钮状态
        /// </summary>
        private void UpdateLocalPlayerButtonState()
        {
            if (playerData.isHost)
            {
                // 房主显示"开始游戏"
                bool canStart = CanStartGame();
                actionButton.interactable = canStart;

                if (canStart)
                {
                    SetButtonIcon(localHostStartGameIconSprite);
                }
                else
                {
                    SetButtonIcon(localHostStartGameDisabledIconSprite);
                }

                LogDebug($"房主按钮状态更新: 可开始={canStart}");
            }
            else
            {
                // 普通玩家显示"准备"/"取消准备"
                actionButton.interactable = true;

                if (playerData.isReady)
                {
                    SetButtonIcon(localPlayerCancelReadyIconSprite);
                }
                else
                {
                    SetButtonIcon(localPlayerReadyIconSprite);
                }

                LogDebug($"普通玩家按钮状态更新: 准备={playerData.isReady}");
            }
        }

        /// <summary>
        /// 更新远程玩家按钮状态
        /// </summary>
        private void UpdateRemotePlayerButtonState()
        {
            // 其他玩家的按钮 - 仅作状态显示，不可交互
            actionButton.interactable = false;

            if (playerData.isHost)
            {
                // 看其他房主时，始终显示"已准备"
                SetButtonIcon(remoteHostReadyIconSprite);
            }
            else
            {
                // 看其他普通玩家时，显示其实际准备状态
                if (playerData.isReady)
                {
                    SetButtonIcon(remotePlayerReadyIconSprite);
                }
                else
                {
                    SetButtonIcon(remotePlayerNotReadyIconSprite);
                }
            }

            LogDebug($"远程玩家按钮状态更新: {playerData.playerName}, 准备={playerData.isReady}");
        }

        /// <summary>
        /// 设置按钮图标
        /// </summary>
        private void SetButtonIcon(Sprite iconSprite)
        {
            if (actionButtonIconImage != null && iconSprite != null)
            {
                actionButtonIconImage.sprite = iconSprite;
            }
            else if (iconSprite == null)
            {
                LogDebug("警告：尝试设置空的按钮图标图片");
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

            // 保存到Photon玩家属性
            NetworkManager.Instance.SetMyPlayerModelId(newModelId);

            // 通知父控制器（会触发网络同步）
            if (parentController != null)
            {
                parentController.OnPlayerModelSelectionChanged(playerId, newModelId);
            }

            LogDebug($"改变模型到: {modelName} (ID: {newModelId})");
        }


        #endregion

        #region 辅助方法

        /// <summary>
        /// 日志输出
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                //Debug.Log($"[PlayerModelItemUI_{playerId}] {message}");
            }
        }

        #endregion

        #region 生命周期

        private void OnDestroy()
        {
            ClearCurrentPreview();
        }

        #endregion
    }
}