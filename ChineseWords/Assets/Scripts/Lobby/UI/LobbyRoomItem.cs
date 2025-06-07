using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lobby.Data;

namespace Lobby.UI
{
    /// <summary>
    /// 房间列表项组件
    /// 用于显示单个房间的信息和处理加入房间操作
    /// </summary>
    public class LobbyRoomItem : MonoBehaviour
    {
        [Header("UI组件引用")]
        [SerializeField] private TMP_Text roomNameText;
        [SerializeField] private TMP_Text playerCountText;
        [SerializeField] private TMP_Text hostNameText;
        [SerializeField] private Image roomStatusIcon;
        [SerializeField] private Button joinRoomButton;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private GameObject passwordIcon;

        [Header("状态配置")]
        [SerializeField] private Color availableColor = Color.white;
        [SerializeField] private Color fullColor = Color.gray;
        [SerializeField] private Color passwordColor = Color.yellow;
        [SerializeField] private Color inGameColor = Color.red;
        [SerializeField] private Color selectedColor = Color.cyan;

        [Header("动画配置")]
        [SerializeField] private bool enableClickAnimation = true;
        [SerializeField] private float animationDuration = 0.1f;
        [SerializeField] private float animationScale = 0.95f;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 数据和回调
        private LobbyRoomData roomData;
        private System.Action<LobbyRoomData> onRoomClicked;

        // 状态
        private bool isInitialized = false;
        private bool isSelected = false;
        private Vector3 originalScale;

        #region Unity生命周期

        private void Awake()
        {
            // 保存原始缩放值
            originalScale = transform.localScale;

            // 自动查找组件
            FindUIComponents();
        }

        private void OnDestroy()
        {
            // 清理按钮事件
            if (joinRoomButton != null)
            {
                joinRoomButton.onClick.RemoveAllListeners();
            }
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化房间项
        /// </summary>
        public void Initialize(LobbyRoomData data, System.Action<LobbyRoomData> clickCallback)
        {
            if (data == null)
            {
                Debug.LogError("[LobbyRoomItem] 初始化失败：房间数据为空");
                return;
            }

            roomData = data;
            onRoomClicked = clickCallback;

            // 绑定按钮事件
            BindButtonEvent();

            // 更新显示
            UpdateDisplay();

            isInitialized = true;
            LogDebug($"房间项初始化完成: {roomData.roomName}");
        }

        /// <summary>
        /// 查找UI组件
        /// </summary>
        private void FindUIComponents()
        {
            // 查找房间名称文本
            if (roomNameText == null)
            {
                roomNameText = transform.Find("RoomNameText")?.GetComponent<TMP_Text>();
                if (roomNameText == null)
                {
                    // 尝试在子对象中查找第一个TMP_Text作为房间名称
                    var allTexts = GetComponentsInChildren<TMP_Text>();
                    if (allTexts.Length > 0)
                    {
                        roomNameText = allTexts[0];
                    }
                }
            }

            // 查找玩家数量文本
            if (playerCountText == null)
            {
                playerCountText = transform.Find("PlayerCountText")?.GetComponent<TMP_Text>();
                if (playerCountText == null)
                {
                    // 尝试查找包含"Count"或"Player"的文本组件
                    var allTexts = GetComponentsInChildren<TMP_Text>();
                    foreach (var text in allTexts)
                    {
                        if (text.name.Contains("Count") || text.name.Contains("Player"))
                        {
                            playerCountText = text;
                            break;
                        }
                    }
                }
            }

            // 查找主机名称文本
            if (hostNameText == null)
            {
                hostNameText = transform.Find("HostNameText")?.GetComponent<TMP_Text>();
                if (hostNameText == null)
                {
                    // 尝试查找包含"Host"的文本组件
                    var allTexts = GetComponentsInChildren<TMP_Text>();
                    foreach (var text in allTexts)
                    {
                        if (text.name.Contains("Host"))
                        {
                            hostNameText = text;
                            break;
                        }
                    }
                }
            }

            // 查找状态图标
            if (roomStatusIcon == null)
            {
                roomStatusIcon = transform.Find("RoomStatusIcon")?.GetComponent<Image>();
                if (roomStatusIcon == null)
                {
                    roomStatusIcon = transform.Find("StatusIcon")?.GetComponent<Image>();
                }
            }

            // 查找密码图标
            if (passwordIcon == null)
            {
                passwordIcon = transform.Find("PasswordIcon")?.gameObject;
                if (passwordIcon == null)
                {
                    passwordIcon = transform.Find("LockIcon")?.gameObject;
                }
            }

            // 查找加入按钮
            if (joinRoomButton == null)
            {
                joinRoomButton = transform.Find("JoinRoomButton")?.GetComponent<Button>();
                if (joinRoomButton == null)
                {
                    joinRoomButton = transform.Find("JoinButton")?.GetComponent<Button>();
                    if (joinRoomButton == null)
                    {
                        // 查找任意Button组件
                        joinRoomButton = GetComponentInChildren<Button>();
                    }
                }
            }

            // 查找背景图片
            if (backgroundImage == null)
            {
                backgroundImage = GetComponent<Image>();
                if (backgroundImage == null)
                {
                    backgroundImage = transform.Find("Background")?.GetComponent<Image>();
                }
            }

            // 输出查找结果（调试用）
            LogComponentSearchResults();
        }

        /// <summary>
        /// 绑定按钮事件
        /// </summary>
        private void BindButtonEvent()
        {
            if (joinRoomButton != null)
            {
                joinRoomButton.onClick.RemoveAllListeners();
                joinRoomButton.onClick.AddListener(OnJoinButtonClicked);
                LogDebug("按钮事件绑定成功");
            }
            else
            {
                Debug.LogWarning("[LobbyRoomItem] 加入按钮未找到，无法绑定事件");
            }
        }

        /// <summary>
        /// 输出组件查找结果
        /// </summary>
        private void LogComponentSearchResults()
        {
            LogDebug($"组件查找结果：");
            LogDebug($"  房间名称文本: {(roomNameText != null ? "✓ " + roomNameText.name : "✗")}");
            LogDebug($"  玩家数量文本: {(playerCountText != null ? "✓ " + playerCountText.name : "✗")}");
            LogDebug($"  主机名称文本: {(hostNameText != null ? "✓ " + hostNameText.name : "✗")}");
            LogDebug($"  状态图标: {(roomStatusIcon != null ? "✓ " + roomStatusIcon.name : "✗")}");
            LogDebug($"  密码图标: {(passwordIcon != null ? "✓ " + passwordIcon.name : "✗")}");
            LogDebug($"  加入按钮: {(joinRoomButton != null ? "✓ " + joinRoomButton.name : "✗")}");
            LogDebug($"  背景图片: {(backgroundImage != null ? "✓ " + backgroundImage.name : "✗")}");
        }

        #endregion

        #region 显示更新

        /// <summary>
        /// 更新显示
        /// </summary>
        private void UpdateDisplay()
        {
            if (roomData == null)
            {
                LogDebug("房间数据为空，无法更新显示");
                return;
            }

            // 更新各个UI元素
            UpdateRoomName();
            UpdatePlayerCount();
            UpdateHostName();
            UpdatePasswordIcon();
            UpdateRoomStatus();
            UpdateBackgroundColor();
            UpdateButtonState();

            LogDebug($"房间项显示已更新: {roomData.roomName}");
        }

        /// <summary>
        /// 更新房间名称
        /// </summary>
        private void UpdateRoomName()
        {
            if (roomNameText != null)
            {
                string displayName = roomData.roomName;

                // 限制显示长度
                if (displayName.Length > 15)
                {
                    displayName = displayName.Substring(0, 12) + "...";
                }

                roomNameText.text = displayName;

                // 根据房间状态设置文本颜色
                if (roomData.status == LobbyRoomData.RoomStatus.InGame)
                {
                    roomNameText.color = Color.gray;
                }
                else if (roomData.IsFull())
                {
                    roomNameText.color = Color.red;
                }
                else
                {
                    roomNameText.color = Color.white;
                }
            }
        }

        /// <summary>
        /// 更新玩家数量
        /// </summary>
        private void UpdatePlayerCount()
        {
            if (playerCountText != null)
            {
                playerCountText.text = roomData.GetPlayerCountText();

                // 根据房间状态设置颜色
                if (roomData.IsFull())
                {
                    playerCountText.color = Color.red;
                }
                else if (roomData.currentPlayers == roomData.maxPlayers - 1)
                {
                    playerCountText.color = Color.yellow; // 快满时用黄色警告
                }
                else
                {
                    playerCountText.color = Color.white;
                }
            }
        }

        /// <summary>
        /// 更新主机名称
        /// </summary>
        private void UpdateHostName()
        {
            if (hostNameText != null)
            {
                string hostName = roomData.hostPlayerName;

                // 限制显示长度
                if (hostName.Length > 10)
                {
                    hostName = hostName.Substring(0, 7) + "...";
                }

                hostNameText.text = $"主机: {hostName}";
                hostNameText.color = Color.cyan; // 主机名称用青色显示
            }
        }

        /// <summary>
        /// 更新密码图标
        /// </summary>
        private void UpdatePasswordIcon()
        {
            if (passwordIcon != null)
            {
                passwordIcon.SetActive(roomData.hasPassword);
            }
        }

        /// <summary>
        /// 更新房间状态
        /// </summary>
        private void UpdateRoomStatus()
        {
            if (roomStatusIcon != null)
            {
                Color statusColor = GetStatusColor();
                roomStatusIcon.color = statusColor;

                // 根据状态设置不同的透明度
                switch (roomData.status)
                {
                    case LobbyRoomData.RoomStatus.Waiting:
                        roomStatusIcon.color = new Color(statusColor.r, statusColor.g, statusColor.b, 1f);
                        break;
                    case LobbyRoomData.RoomStatus.Full:
                        roomStatusIcon.color = new Color(statusColor.r, statusColor.g, statusColor.b, 0.8f);
                        break;
                    case LobbyRoomData.RoomStatus.InGame:
                        roomStatusIcon.color = new Color(statusColor.r, statusColor.g, statusColor.b, 0.6f);
                        break;
                    case LobbyRoomData.RoomStatus.Closed:
                        roomStatusIcon.color = new Color(statusColor.r, statusColor.g, statusColor.b, 0.3f);
                        break;
                }
            }
        }

        /// <summary>
        /// 更新背景颜色
        /// </summary>
        private void UpdateBackgroundColor()
        {
            if (backgroundImage != null)
            {
                Color bgColor = GetBackgroundColor();

                // 如果被选中，使用选中颜色
                if (isSelected)
                {
                    bgColor = Color.Lerp(bgColor, selectedColor, 0.5f);
                }

                backgroundImage.color = bgColor;
            }
        }

        /// <summary>
        /// 更新按钮状态
        /// </summary>
        private void UpdateButtonState()
        {
            if (joinRoomButton != null)
            {
                // 只有等待中且未满的房间可以加入
                bool canJoin = roomData.CanJoin();
                joinRoomButton.interactable = canJoin;

                // 更新按钮文本和颜色
                UpdateButtonText();
                UpdateButtonColor(canJoin);
            }
        }

        /// <summary>
        /// 更新按钮文本
        /// </summary>
        private void UpdateButtonText()
        {
            var buttonText = joinRoomButton.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                string buttonTextContent = GetButtonText();
                buttonText.text = buttonTextContent;
            }
        }

        /// <summary>
        /// 更新按钮颜色
        /// </summary>
        private void UpdateButtonColor(bool canJoin)
        {
            var buttonImage = joinRoomButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                if (canJoin)
                {
                    buttonImage.color = Color.white;
                }
                else
                {
                    buttonImage.color = Color.gray;
                }
            }
        }

        #endregion

        #region 颜色和文本辅助方法

        /// <summary>
        /// 获取状态颜色
        /// </summary>
        private Color GetStatusColor()
        {
            switch (roomData.status)
            {
                case LobbyRoomData.RoomStatus.Waiting:
                    if (roomData.hasPassword)
                        return passwordColor;
                    return roomData.IsFull() ? fullColor : availableColor;

                case LobbyRoomData.RoomStatus.Full:
                    return fullColor;

                case LobbyRoomData.RoomStatus.InGame:
                    return inGameColor;

                case LobbyRoomData.RoomStatus.Closed:
                    return Color.gray;

                default:
                    return Color.white;
            }
        }

        /// <summary>
        /// 获取背景颜色
        /// </summary>
        private Color GetBackgroundColor()
        {
            Color baseColor = availableColor;
            baseColor.a = 0.1f; // 基础透明度

            switch (roomData.status)
            {
                case LobbyRoomData.RoomStatus.Waiting:
                    if (roomData.hasPassword)
                    {
                        baseColor = passwordColor;
                        baseColor.a = 0.2f;
                    }
                    else if (roomData.IsFull())
                    {
                        baseColor = fullColor;
                        baseColor.a = 0.3f;
                    }
                    break;

                case LobbyRoomData.RoomStatus.Full:
                    baseColor = fullColor;
                    baseColor.a = 0.4f;
                    break;

                case LobbyRoomData.RoomStatus.InGame:
                    baseColor = inGameColor;
                    baseColor.a = 0.3f;
                    break;

                case LobbyRoomData.RoomStatus.Closed:
                    baseColor = Color.gray;
                    baseColor.a = 0.5f;
                    break;
            }

            return baseColor;
        }

        /// <summary>
        /// 获取按钮文本
        /// </summary>
        private string GetButtonText()
        {
            switch (roomData.status)
            {
                case LobbyRoomData.RoomStatus.Waiting:
                    if (roomData.IsFull())
                        return "已满";
                    return roomData.hasPassword ? "需密码" : "加入";

                case LobbyRoomData.RoomStatus.Full:
                    return "已满";

                case LobbyRoomData.RoomStatus.InGame:
                    return "游戏中";

                case LobbyRoomData.RoomStatus.Closed:
                    return "已关闭";

                default:
                    return "加入";
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 加入按钮点击事件
        /// </summary>
        private void OnJoinButtonClicked()
        {
            if (roomData == null)
            {
                LogDebug("房间数据为空，无法加入");
                return;
            }

            if (onRoomClicked == null)
            {
                LogDebug("回调函数为空，无法处理点击");
                return;
            }

            if (!roomData.CanJoin())
            {
                LogDebug($"房间 {roomData.roomName} 无法加入 - 状态: {roomData.status}, 人数: {roomData.currentPlayers}/{roomData.maxPlayers}");
                return;
            }

            LogDebug($"尝试加入房间: {roomData.roomName}");

            // 播放点击动画
            if (enableClickAnimation)
            {
                PlayClickAnimation();
            }

            // 如果房间有密码，这里可以扩展为显示密码输入对话框
            if (roomData.hasPassword)
            {
                LogDebug("房间需要密码验证");
                // TODO: 在阶段2或3实现密码输入对话框
                // 现在暂时直接尝试加入
            }

            // 调用回调函数
            onRoomClicked.Invoke(roomData);
        }

        #endregion

        #region 动画效果

        /// <summary>
        /// 播放点击动画
        /// </summary>
        private void PlayClickAnimation()
        {
            if (transform != null)
            {
                StopAllCoroutines(); // 停止之前的动画
                StartCoroutine(ClickAnimationCoroutine());
            }
        }

        /// <summary>
        /// 点击动画协程
        /// </summary>
        private System.Collections.IEnumerator ClickAnimationCoroutine()
        {
            Vector3 pressedScale = originalScale * animationScale;

            // 缩小阶段
            float elapsed = 0f;
            while (elapsed < animationDuration)
            {
                float t = elapsed / animationDuration;
                transform.localScale = Vector3.Lerp(originalScale, pressedScale, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.localScale = pressedScale;

            // 恢复阶段
            elapsed = 0f;
            while (elapsed < animationDuration)
            {
                float t = elapsed / animationDuration;
                transform.localScale = Vector3.Lerp(pressedScale, originalScale, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.localScale = originalScale;
        }

        /// <summary>
        /// 播放悬停动画
        /// </summary>
        public void PlayHoverAnimation(bool isHovering)
        {
            StopAllCoroutines();
            StartCoroutine(HoverAnimationCoroutine(isHovering));
        }

        /// <summary>
        /// 悬停动画协程
        /// </summary>
        private System.Collections.IEnumerator HoverAnimationCoroutine(bool isHovering)
        {
            Vector3 targetScale = isHovering ? originalScale * 1.05f : originalScale;
            Vector3 startScale = transform.localScale;

            float elapsed = 0f;
            float duration = animationDuration * 2f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                transform.localScale = Vector3.Lerp(startScale, targetScale, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.localScale = targetScale;
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 更新房间数据
        /// </summary>
        public void UpdateRoomData(LobbyRoomData newData)
        {
            if (newData == null)
            {
                LogDebug("新房间数据为空，跳过更新");
                return;
            }

            roomData = newData;
            UpdateDisplay();
            LogDebug($"房间数据已更新: {roomData.roomName}");
        }

        /// <summary>
        /// 获取房间数据
        /// </summary>
        public LobbyRoomData GetRoomData()
        {
            return roomData;
        }

        /// <summary>
        /// 设置选中状态
        /// </summary>
        public void SetSelected(bool selected)
        {
            if (isSelected != selected)
            {
                isSelected = selected;
                UpdateBackgroundColor();
                LogDebug($"房间项选中状态: {selected}");
            }
        }

        /// <summary>
        /// 获取选中状态
        /// </summary>
        public bool IsSelected()
        {
            return isSelected;
        }

        /// <summary>
        /// 设置点击回调
        /// </summary>
        public void SetClickCallback(System.Action<LobbyRoomData> callback)
        {
            onRoomClicked = callback;
        }

        /// <summary>
        /// 检查是否可以加入
        /// </summary>
        public bool CanJoinRoom()
        {
            return roomData != null && roomData.CanJoin();
        }

        /// <summary>
        /// 强制刷新显示
        /// </summary>
        public void RefreshDisplay()
        {
            UpdateDisplay();
        }

        /// <summary>
        /// 获取房间ID
        /// </summary>
        public string GetRoomId()
        {
            return roomData?.roomId ?? "";
        }

        /// <summary>
        /// 检查是否为同一房间
        /// </summary>
        public bool IsSameRoom(LobbyRoomData otherRoomData)
        {
            if (roomData == null || otherRoomData == null)
                return false;

            return roomData.roomId == otherRoomData.roomId;
        }

        #endregion

        #region 调试方法

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[LobbyRoomItem] {message}");
            }
        }

        [ContextMenu("显示房间项完整信息")]
        public void ShowCompleteRoomItemInfo()
        {
            string info = "=== 房间项完整信息 ===\n";

            // 房间数据信息
            if (roomData != null)
            {
                info += $"房间数据:\n";
                info += $"  名称: {roomData.roomName}\n";
                info += $"  ID: {roomData.roomId}\n";
                info += $"  玩家: {roomData.currentPlayers}/{roomData.maxPlayers}\n";
                info += $"  主机: {roomData.hostPlayerName}\n";
                info += $"  状态: {roomData.status}\n";
                info += $"  有密码: {roomData.hasPassword}\n";
                info += $"  可加入: {roomData.CanJoin()}\n";
                info += $"  创建时间: {roomData.createTime}\n";
                info += $"  游戏模式: {roomData.gameMode}\n";
            }
            else
            {
                info += "房间数据: 为空\n";
            }

            // UI组件状态
            info += $"UI组件状态:\n";
            info += $"  初始化完成: {isInitialized}\n";
            info += $"  选中状态: {isSelected}\n";
            info += $"  房间名称文本: {(roomNameText != null ? "✓ " + roomNameText.name : "✗")}\n";
            info += $"  玩家数量文本: {(playerCountText != null ? "✓ " + playerCountText.name : "✗")}\n";
            info += $"  主机名称文本: {(hostNameText != null ? "✓ " + hostNameText.name : "✗")}\n";
            info += $"  状态图标: {(roomStatusIcon != null ? "✓ " + roomStatusIcon.name : "✗")}\n";
            info += $"  密码图标: {(passwordIcon != null ? "✓ " + passwordIcon.name : "✗")}\n";
            info += $"  加入按钮: {(joinRoomButton != null ? "✓ " + joinRoomButton.name : "✗")}\n";
            info += $"  背景图片: {(backgroundImage != null ? "✓ " + backgroundImage.name : "✗")}\n";

            // 回调和状态
            info += $"功能状态:\n";
            info += $"  点击回调: {(onRoomClicked != null ? "已设置" : "未设置")}\n";
            info += $"  点击动画: {(enableClickAnimation ? "启用" : "禁用")}\n";
            info += $"  原始缩放: {originalScale}\n";
            info += $"  当前缩放: {transform.localScale}";

            LogDebug(info);
        }

        [ContextMenu("测试加入房间")]
        public void TestJoinRoom()
        {
            LogDebug("手动测试加入房间");
            OnJoinButtonClicked();
        }

        [ContextMenu("测试点击动画")]
        public void TestClickAnimation()
        {
            LogDebug("手动测试点击动画");
            PlayClickAnimation();
        }

        [ContextMenu("测试悬停动画")]
        public void TestHoverAnimation()
        {
            LogDebug("手动测试悬停动画");
            StartCoroutine(TestHoverSequence());
        }

        private System.Collections.IEnumerator TestHoverSequence()
        {
            PlayHoverAnimation(true);
            yield return new WaitForSeconds(1f);
            PlayHoverAnimation(false);
        }

        [ContextMenu("切换选中状态")]
        public void ToggleSelected()
        {
            SetSelected(!isSelected);
        }

        [ContextMenu("强制刷新显示")]
        public void ForceRefreshDisplay()
        {
            LogDebug("手动强制刷新显示");
            RefreshDisplay();
        }

        #endregion
    }
}