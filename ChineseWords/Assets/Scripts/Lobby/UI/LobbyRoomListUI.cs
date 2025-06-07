using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Lobby.Core;
using Lobby.Data;

namespace Lobby.UI
{
    /// <summary>
    /// 房间列表UI控制器
    /// 负责显示和管理房间列表
    /// </summary>
    public class LobbyRoomListUI : MonoBehaviour
    {
        [Header("UI组件引用")]
        [SerializeField] private Transform roomListContent;
        [SerializeField] private GameObject roomItemPrefab;
        [SerializeField] private Button refreshButton;
        [SerializeField] private TMP_Text roomCountText;
        [SerializeField] private ScrollRect roomListScrollRect;

        [Header("UI配置")]
        [SerializeField] private int maxDisplayRooms = 20;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 房间项管理
        private List<LobbyRoomItem> roomItems = new List<LobbyRoomItem>();
        private List<LobbyRoomData> currentRoomList = new List<LobbyRoomData>();

        // UI状态
        private bool isInitialized = false;

        #region Unity生命周期

        private void Start()
        {
            InitializeUI();
            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            ClearRoomItems();
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化UI
        /// </summary>
        private void InitializeUI()
        {
            LogDebug("初始化房间列表UI");

            // 自动查找组件
            FindUIComponents();

            // 绑定按钮事件
            BindButtonEvents();

            // 初始化显示
            UpdateRoomCountDisplay();

            isInitialized = true;
            LogDebug("房间列表UI初始化完成");
        }

        /// <summary>
        /// 查找UI组件
        /// </summary>
        private void FindUIComponents()
        {
            // 查找房间列表内容容器
            if (roomListContent == null)
            {
                var contentObj = GameObject.Find("RoomListContent");
                if (contentObj != null)
                {
                    roomListContent = contentObj.transform;
                }
                else
                {
                    Debug.LogWarning("[LobbyRoomListUI] 未找到RoomListContent");
                }
            }

            // 查找刷新按钮
            if (refreshButton == null)
            {
                refreshButton = GameObject.Find("RefreshButton")?.GetComponent<Button>();
                if (refreshButton == null)
                {
                    Debug.LogWarning("[LobbyRoomListUI] 未找到RefreshButton");
                }
            }

            // 查找房间数量文本
            if (roomCountText == null)
            {
                roomCountText = GameObject.Find("RoomCountText")?.GetComponent<TMP_Text>();
                if (roomCountText == null)
                {
                    Debug.LogWarning("[LobbyRoomListUI] 未找到RoomCountText");
                }
            }

            // 查找滚动视图
            if (roomListScrollRect == null)
            {
                roomListScrollRect = GetComponentInParent<ScrollRect>();
                if (roomListScrollRect == null)
                {
                    Debug.LogWarning("[LobbyRoomListUI] 未找到ScrollRect");
                }
            }
        }

        /// <summary>
        /// 绑定按钮事件
        /// </summary>
        private void BindButtonEvents()
        {
            if (refreshButton != null)
            {
                refreshButton.onClick.AddListener(OnRefreshButtonClicked);
                LogDebug("已绑定刷新按钮事件");
            }
        }

        /// <summary>
        /// 订阅网络事件
        /// </summary>
        private void SubscribeToEvents()
        {
            if (LobbyNetworkManager.Instance != null)
            {
                LobbyNetworkManager.Instance.OnRoomListUpdated += OnRoomListUpdated;
                LobbyNetworkManager.Instance.OnConnectionStatusChanged += OnConnectionStatusChanged;
                LogDebug("已订阅网络事件");
            }
        }

        /// <summary>
        /// 取消订阅网络事件
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (LobbyNetworkManager.Instance != null)
            {
                LobbyNetworkManager.Instance.OnRoomListUpdated -= OnRoomListUpdated;
                LobbyNetworkManager.Instance.OnConnectionStatusChanged -= OnConnectionStatusChanged;
                LogDebug("已取消订阅网络事件");
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 刷新按钮点击事件
        /// </summary>
        private void OnRefreshButtonClicked()
        {
            LogDebug("点击刷新按钮");

            if (LobbyNetworkManager.Instance != null)
            {
                LobbyNetworkManager.Instance.RefreshRoomList();
            }
        }

        /// <summary>
        /// 房间列表更新事件
        /// </summary>
        private void OnRoomListUpdated(List<LobbyRoomData> roomList)
        {
            LogDebug($"房间列表更新，共 {roomList.Count} 个房间");

            currentRoomList = roomList;
            UpdateRoomListDisplay();
        }

        /// <summary>
        /// 连接状态改变事件
        /// </summary>
        private void OnConnectionStatusChanged(bool isConnected)
        {
            LogDebug($"连接状态改变: {isConnected}");

            // 更新刷新按钮状态
            if (refreshButton != null)
            {
                refreshButton.interactable = isConnected;
            }

            // 如果断开连接，清空房间列表
            if (!isConnected)
            {
                currentRoomList.Clear();
                UpdateRoomListDisplay();
            }
        }

        #endregion

        #region 房间列表显示

        /// <summary>
        /// 更新房间列表显示
        /// </summary>
        private void UpdateRoomListDisplay()
        {
            if (!isInitialized || roomListContent == null)
                return;

            LogDebug($"更新房间列表显示，当前房间数: {currentRoomList.Count}");

            // 清空现有房间项
            ClearRoomItems();

            // 限制显示数量
            int displayCount = Mathf.Min(currentRoomList.Count, maxDisplayRooms);

            // 创建房间项
            for (int i = 0; i < displayCount; i++)
            {
                CreateRoomItem(currentRoomList[i]);
            }

            // 更新房间数量显示
            UpdateRoomCountDisplay();

            // 重置滚动位置
            if (roomListScrollRect != null)
            {
                roomListScrollRect.verticalNormalizedPosition = 1f;
            }
        }

        /// <summary>
        /// 创建房间项
        /// </summary>
        private void CreateRoomItem(LobbyRoomData roomData)
        {
            if (roomItemPrefab == null || roomListContent == null)
            {
                Debug.LogWarning("[LobbyRoomListUI] 缺少房间项预制体或容器");
                return;
            }

            // 实例化房间项
            GameObject roomItemObj = Instantiate(roomItemPrefab, roomListContent);

            // 获取房间项组件
            LobbyRoomItem roomItem = roomItemObj.GetComponent<LobbyRoomItem>();
            if (roomItem == null)
            {
                roomItem = roomItemObj.AddComponent<LobbyRoomItem>();
            }

            // 初始化房间项
            roomItem.Initialize(roomData, OnRoomItemClicked);

            // 添加到列表
            roomItems.Add(roomItem);

            LogDebug($"创建房间项: {roomData.roomName}");
        }

        /// <summary>
        /// 清空房间项
        /// </summary>
        private void ClearRoomItems()
        {
            foreach (var roomItem in roomItems)
            {
                if (roomItem != null && roomItem.gameObject != null)
                {
                    Destroy(roomItem.gameObject);
                }
            }
            roomItems.Clear();
        }

        /// <summary>
        /// 更新房间数量显示
        /// </summary>
        private void UpdateRoomCountDisplay()
        {
            if (roomCountText != null)
            {
                roomCountText.text = $"房间数: {currentRoomList.Count}";
            }
        }

        /// <summary>
        /// 房间项点击事件
        /// </summary>
        private void OnRoomItemClicked(LobbyRoomData roomData)
        {
            LogDebug($"点击房间: {roomData.roomName}");

            if (LobbyNetworkManager.Instance != null)
            {
                LobbyNetworkManager.Instance.JoinRoom(roomData);
            }
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 强制刷新房间列表
        /// </summary>
        public void ForceRefresh()
        {
            OnRefreshButtonClicked();
        }

        /// <summary>
        /// 获取当前显示的房间数量
        /// </summary>
        public int GetDisplayedRoomCount()
        {
            return roomItems.Count;
        }

        /// <summary>
        /// 检查是否有可加入的房间
        /// </summary>
        public bool HasJoinableRooms()
        {
            foreach (var room in currentRoomList)
            {
                if (room.CanJoin())
                {
                    return true;
                }
            }
            return false;
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
                Debug.Log($"[LobbyRoomListUI] {message}");
            }
        }

        [ContextMenu("显示房间列表状态")]
        public void ShowRoomListStatus()
        {
            string status = "=== 房间列表UI状态 ===\n";
            status += $"UI已初始化: {isInitialized}\n";
            status += $"房间列表容器: {(roomListContent != null ? "✓" : "✗")}\n";
            status += $"房间项预制体: {(roomItemPrefab != null ? "✓" : "✗")}\n";
            status += $"当前房间数: {currentRoomList.Count}\n";
            status += $"显示房间项数: {roomItems.Count}\n";
            status += $"可加入房间: {HasJoinableRooms()}";

            LogDebug(status);
        }

        #endregion
    }
}