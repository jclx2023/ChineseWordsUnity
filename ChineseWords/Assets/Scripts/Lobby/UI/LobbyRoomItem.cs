using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lobby.Data;

namespace Lobby.UI
{
    /// <summary>
    /// 简化的房间列表项组件
    /// 仅显示核心信息：房间名称、房主、人数、加入按钮
    /// </summary>
    public class LobbyRoomItem : MonoBehaviour
    {
        [Header("UI组件引用")]
        [SerializeField] private TMP_Text roomNameText;
        [SerializeField] private TMP_Text hostNameText;
        [SerializeField] private TMP_Text playerCountText;
        [SerializeField] private Button joinButton;

        [Header("配置")]
        [SerializeField] private bool enableDebugLogs = false;

        // 数据和回调
        private LobbyRoomData roomData;
        private System.Action<LobbyRoomData> onJoinClicked;

        #region 初始化

        /// <summary>
        /// 初始化房间项
        /// </summary>
        public void Initialize(LobbyRoomData data, System.Action<LobbyRoomData> joinCallback)
        {
            if (data == null)
            {
                LogError("初始化失败：房间数据为空");
                return;
            }

            roomData = data;
            onJoinClicked = joinCallback;

            // 绑定按钮事件
            if (joinButton != null)
            {
                joinButton.onClick.RemoveAllListeners();
                joinButton.onClick.AddListener(OnJoinButtonClicked);
            }

            // 更新显示
            UpdateDisplay();

            LogDebug($"房间项初始化完成: {roomData.roomName}");
        }

        #endregion

        #region 显示更新

        /// <summary>
        /// 更新显示内容
        /// </summary>
        private void UpdateDisplay()
        {
            UpdateRoomName();
            UpdateHostName();
            UpdatePlayerCount();
            UpdateJoinButton();
        }

        /// <summary>
        /// 更新房间名称
        /// </summary>
        private void UpdateRoomName()
        {
            if (roomNameText != null)
            {
                string displayName = roomData.roomName;

                // 限制显示长度，适应700x70的尺寸
                if (displayName.Length > 20)
                {
                    displayName = displayName.Substring(0, 17) + "...";
                }

                roomNameText.text = displayName;
            }
        }

        /// <summary>
        /// 更新房主信息
        /// </summary>
        private void UpdateHostName()
        {
            if (hostNameText != null)
            {
                string hostName = roomData.hostPlayerName;

                // 限制显示长度
                if (hostName.Length > 12)
                {
                    hostName = hostName.Substring(0, 9) + "...";
                }

                hostNameText.text = $"房主: {hostName}";
            }
        }

        /// <summary>
        /// 更新人数显示
        /// </summary>
        private void UpdatePlayerCount()
        {
            if (playerCountText != null)
            {
                playerCountText.text = $"{roomData.currentPlayers}/{roomData.maxPlayers}";
            }
        }

        /// <summary>
        /// 更新加入按钮
        /// </summary>
        private void UpdateJoinButton()
        {
            if (joinButton != null)
            {
                bool canJoin = roomData.CanJoin();
                joinButton.interactable = canJoin;

                // 更新按钮文本
                var buttonText = joinButton.GetComponentInChildren<TMP_Text>();
                if (buttonText != null)
                {
                    buttonText.text = GetJoinButtonText();
                }
            }
        }

        /// <summary>
        /// 获取加入按钮文本
        /// </summary>
        private string GetJoinButtonText()
        {
            switch (roomData.status)
            {
                case LobbyRoomData.RoomStatus.Waiting:
                    return roomData.IsFull() ? "已满" : "加入";
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
                LogError("房间数据为空，无法加入");
                return;
            }

            if (onJoinClicked == null)
            {
                LogError("回调函数为空，无法处理点击");
                return;
            }

            if (!roomData.CanJoin())
            {
                LogDebug($"房间 {roomData.roomName} 无法加入 - 状态: {roomData.status}");
                return;
            }

            LogDebug($"尝试加入房间: {roomData.roomName}");
            onJoinClicked.Invoke(roomData);
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 刷新房间项显示
        /// </summary>
        public void RefreshDisplay()
        {
            if (roomData != null)
            {
                UpdateDisplay();
            }
        }

        /// <summary>
        /// 更新房间数据
        /// </summary>
        public void UpdateRoomData(LobbyRoomData newData)
        {
            if (newData != null)
            {
                roomData = newData;
                UpdateDisplay();
            }
        }

        /// <summary>
        /// 获取房间数据
        /// </summary>
        public LobbyRoomData GetRoomData()
        {
            return roomData;
        }

        #endregion

        #region 生命周期

        private void OnDestroy()
        {
            if (joinButton != null)
            {
                joinButton.onClick.RemoveAllListeners();
            }
        }

        #endregion

        #region 调试方法

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[LobbyRoomItem] {message}");
            }
        }

        private void LogError(string message)
        {
            Debug.LogError($"[LobbyRoomItem] {message}");
        }

        #endregion
    }
}