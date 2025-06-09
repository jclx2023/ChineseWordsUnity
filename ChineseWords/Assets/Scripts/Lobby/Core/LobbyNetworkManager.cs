using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Photon.Realtime;
using Core.Network;
using Lobby.Data;
using Lobby.Network;

namespace Lobby.Core
{
    /// <summary>
    /// Lobby网络管理器 - Photon真实实现版本
    /// 专门处理Lobby相关的Photon网络操作
    /// </summary>
    public class LobbyNetworkManager : MonoBehaviour
    {
        [Header("网络配置")]
        [SerializeField] private bool autoConnectOnStart = true;
        [SerializeField] private float roomListRefreshInterval = 5f;
        [SerializeField] private float connectionTimeout = 10f;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        public static LobbyNetworkManager Instance { get; private set; }

        // 网络状态
        private bool isConnected = false;
        private bool isConnecting = false;
        private bool isInLobby = false;

        // 房间数据
        private List<LobbyRoomData> cachedRoomList = new List<LobbyRoomData>();
        private Coroutine connectionTimeoutCoroutine;

        // 事件
        public System.Action<bool> OnConnectionStatusChanged;
        public System.Action<List<LobbyRoomData>> OnRoomListUpdated;
        public System.Action<string, bool> OnRoomCreated; // roomName, success
        public System.Action<string, bool> OnRoomJoined; // roomName, success
        public System.Action<bool> OnLobbyStatusChanged; // inLobby

        #region Unity生命周期

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                LogDebug("LobbyNetworkManager 实例已创建");
            }
            else
            {
                LogDebug("销毁重复的LobbyNetworkManager实例");
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            if (autoConnectOnStart)
            {
                StartCoroutine(ConnectToPhotonCoroutine());
            }
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
            UnsubscribeFromPhotonEvents();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region 连接管理

        /// <summary>
        /// 连接到Photon的协程
        /// </summary>
        private IEnumerator ConnectToPhotonCoroutine()
        {
            LogDebug("开始连接到Photon");

            if (PhotonNetworkAdapter.Instance == null)
            {
                Debug.LogError("[LobbyNetworkManager] PhotonNetworkAdapter.Instance 为空");
                yield break;
            }

            isConnecting = true;
            OnConnectionStatusChanged?.Invoke(false);

            // 订阅Photon事件
            SubscribeToPhotonEvents();

            // 检查是否已经连接
            if (PhotonNetworkAdapter.Instance.IsPhotonConnected)
            {
                LogDebug("已连接到Photon，直接加入大厅");
                JoinPhotonLobby();
                yield break;
            }

            // 开始连接超时计时
            connectionTimeoutCoroutine = StartCoroutine(ConnectionTimeoutCoroutine());

            // 等待连接完成
            while (isConnecting && !isConnected)
            {
                yield return new WaitForSeconds(0.1f);
            }

            // 停止超时计时
            if (connectionTimeoutCoroutine != null)
            {
                StopCoroutine(connectionTimeoutCoroutine);
                connectionTimeoutCoroutine = null;
            }
        }

        /// <summary>
        /// 连接超时协程
        /// </summary>
        private IEnumerator ConnectionTimeoutCoroutine()
        {
            yield return new WaitForSeconds(connectionTimeout);

            if (isConnecting)
            {
                LogDebug("连接超时");
                isConnecting = false;
                OnConnectionStatusChanged?.Invoke(false);
            }
        }

        /// <summary>
        /// 加入Photon大厅
        /// </summary>
        private void JoinPhotonLobby()
        {
            if (PhotonNetworkAdapter.Instance != null)
            {
                PhotonNetworkAdapter.Instance.JoinPhotonLobby();
            }
        }

        /// <summary>
        /// 订阅Photon事件
        /// </summary>
        private void SubscribeToPhotonEvents()
        {
            if (PhotonNetworkAdapter.Instance != null)
            {
                PhotonNetworkAdapter.OnPhotonConnected += OnPhotonConnected;
                PhotonNetworkAdapter.OnPhotonDisconnected += OnPhotonDisconnected;
                PhotonNetworkAdapter.OnPhotonJoinedLobby += OnPhotonJoinedLobby;
                PhotonNetworkAdapter.OnPhotonLeftLobby += OnPhotonLeftLobby;
                PhotonNetworkAdapter.OnPhotonRoomListUpdate += OnPhotonRoomListUpdate;
                PhotonNetworkAdapter.OnPhotonRoomJoined += OnPhotonRoomJoined;
                PhotonNetworkAdapter.OnPhotonRoomLeft += OnPhotonRoomLeft;

                LogDebug("已订阅Photon事件");
            }
        }

        /// <summary>
        /// 取消订阅Photon事件
        /// </summary>
        private void UnsubscribeFromPhotonEvents()
        {
            if (PhotonNetworkAdapter.Instance != null)
            {
                PhotonNetworkAdapter.OnPhotonConnected -= OnPhotonConnected;
                PhotonNetworkAdapter.OnPhotonDisconnected -= OnPhotonDisconnected;
                PhotonNetworkAdapter.OnPhotonJoinedLobby -= OnPhotonJoinedLobby;
                PhotonNetworkAdapter.OnPhotonLeftLobby -= OnPhotonLeftLobby;
                PhotonNetworkAdapter.OnPhotonRoomListUpdate -= OnPhotonRoomListUpdate;
                PhotonNetworkAdapter.OnPhotonRoomJoined -= OnPhotonRoomJoined;
                PhotonNetworkAdapter.OnPhotonRoomLeft -= OnPhotonRoomLeft;

                LogDebug("已取消订阅Photon事件");
            }
        }

        #endregion

        #region 房间管理

        public void CreateRoom(string roomName, int maxPlayers, string password = "")
        {
            if (!isConnected || !isInLobby)
            {
                LogDebug("未连接到网络或未在大厅中，无法创建房间");
                OnRoomCreated?.Invoke(roomName, false);
                return;
            }

            if (string.IsNullOrEmpty(roomName) || maxPlayers < 2)
            {
                LogDebug("无效的房间参数");
                OnRoomCreated?.Invoke(roomName, false);
                return;
            }

            // 清理房间名称
            roomName = PhotonLobbyDataConverter.CleanRoomNameForPhoton(roomName);

            LogDebug($"创建房间: {roomName}, 最大人数: {maxPlayers}");

            var lobbyRoomData = LobbyRoomData.CreateNew(roomName, maxPlayers, GetCurrentPlayerName());
            lobbyRoomData.hasPassword = !string.IsNullOrEmpty(password);
            lobbyRoomData.password = password;

            var roomOptions = lobbyRoomData.ToPhotonRoomOptions();

            if (roomOptions == null)
            {
                LogDebug("房间选项转换失败");
                OnRoomCreated?.Invoke(roomName, false);
                return;
            }

            // 设置玩家属性
            SetPlayerProperties();

            // 调用PhotonNetworkAdapter创建房间
            if (PhotonNetworkAdapter.Instance != null)
            {
                PhotonNetworkAdapter.Instance.CreatePhotonRoom(roomName, maxPlayers);
            }
        }

        /// <summary>
        /// 加入房间
        /// </summary>
        public void JoinRoom(LobbyRoomData roomData)
        {
            if (!isConnected || !isInLobby)
            {
                LogDebug("未连接到网络或未在大厅中，无法加入房间");
                OnRoomJoined?.Invoke(roomData.roomName, false);
                return;
            }

            if (roomData == null || !roomData.CanJoin())
            {
                LogDebug("房间数据无效或无法加入");
                OnRoomJoined?.Invoke(roomData?.roomName ?? "Unknown", false);
                return;
            }

            LogDebug($"加入房间: {roomData.roomName}");

            // 设置玩家属性
            SetPlayerProperties();

            // TODO: 如果房间有密码，这里需要处理密码验证
            if (roomData.hasPassword)
            {
                LogDebug("房间需要密码，但暂未实现密码验证");
                // 在阶段3可以扩展密码输入功能
            }

            // 调用PhotonNetworkAdapter加入房间
            if (PhotonNetworkAdapter.Instance != null)
            {
                // 注意：这里需要修改PhotonNetworkAdapter添加按名称加入房间的方法
                PhotonNetworkAdapter.Instance.JoinPhotonRoomByName(roomData.roomName);
            }
        }

        /// <summary>
        /// 加入随机房间
        /// </summary>
        public void JoinRandomRoom()
        {
            if (!isConnected || !isInLobby)
            {
                LogDebug("未连接到网络或未在大厅中，无法加入随机房间");
                return;
            }

            if (cachedRoomList.Count == 0)
            {
                LogDebug("没有可用的房间");
                return;
            }

            // 找到第一个可加入的房间
            foreach (var room in cachedRoomList)
            {
                if (room.CanJoin())
                {
                    JoinRoom(room);
                    return;
                }
            }

            LogDebug("没有可加入的房间");

            // 使用PhotonNetwork的随机加入功能
            if (PhotonNetworkAdapter.Instance != null)
            {
                PhotonNetworkAdapter.Instance.JoinPhotonRoom(); // 这会尝试随机加入
            }
        }

        /// <summary>
        /// 刷新房间列表
        /// </summary>
        public void RefreshRoomList()
        {
            if (!isConnected || !isInLobby)
            {
                LogDebug("未连接到网络或未在大厅中，无法刷新房间列表");
                return;
            }

            LogDebug("刷新房间列表");

            // Photon会自动维护房间列表，这里主要是触发更新
            if (PhotonNetworkAdapter.Instance != null)
            {
                var photonRooms = PhotonNetworkAdapter.Instance.GetPhotonRoomList();
                OnPhotonRoomListUpdate(photonRooms);
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            LogDebug("断开网络连接");

            if (PhotonNetworkAdapter.Instance != null)
            {
                if (isInLobby)
                {
                    PhotonNetworkAdapter.Instance.LeavePhotonLobby();
                }
                PhotonNetworkAdapter.Instance.DisconnectPhoton();
            }
        }

        #endregion

        #region Photon事件处理

        private void OnPhotonConnected()
        {
            LogDebug("Photon连接成功，准备加入大厅");
            isConnecting = false;
            isConnected = true;
            OnConnectionStatusChanged?.Invoke(true);

            // 连接成功后自动加入大厅
            JoinPhotonLobby();
        }

        private void OnPhotonDisconnected()
        {
            LogDebug("Photon连接断开");
            isConnecting = false;
            isConnected = false;
            isInLobby = false;

            OnConnectionStatusChanged?.Invoke(false);
            OnLobbyStatusChanged?.Invoke(false);

            // 清空房间列表
            cachedRoomList.Clear();
            OnRoomListUpdated?.Invoke(new List<LobbyRoomData>());
        }

        private void OnPhotonJoinedLobby()
        {
            LogDebug("成功加入Photon大厅");
            isInLobby = true;
            OnLobbyStatusChanged?.Invoke(true);

            // 大厅加入成功后自动刷新房间列表
            RefreshRoomList();
        }

        private void OnPhotonLeftLobby()
        {
            LogDebug("离开Photon大厅");
            isInLobby = false;
            OnLobbyStatusChanged?.Invoke(false);

            // 清空房间列表
            cachedRoomList.Clear();
            OnRoomListUpdated?.Invoke(new List<LobbyRoomData>());
        }

        private void OnPhotonRoomListUpdate(List<RoomInfo> photonRooms)
        {
            LogDebug($"Photon房间列表更新，共 {photonRooms.Count} 个房间");

            // 转换Photon房间数据为Lobby房间数据
            cachedRoomList = PhotonLobbyDataConverter.FromPhotonRoomList(photonRooms);

            OnRoomListUpdated?.Invoke(new List<LobbyRoomData>(cachedRoomList));
            LogDebug($"转换后的房间列表：{cachedRoomList.Count} 个可用房间");
        }

        private void OnPhotonRoomJoined()
        {
            LogDebug("成功加入Photon房间");

            // 获取当前房间信息
            if (PhotonNetworkAdapter.Instance.IsInPhotonRoom)
            {
                string roomName = PhotonNetworkAdapter.Instance.CurrentRoomName;
                OnRoomJoined?.Invoke(roomName, true);
                LogDebug($"房间加入成功: {roomName}");
            }
        }

        private void OnPhotonRoomLeft()
        {
            LogDebug("离开Photon房间");
            // 重新加入大厅以继续浏览房间
            if (isConnected && !isInLobby)
            {
                JoinPhotonLobby();
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 设置玩家属性
        /// </summary>
        private void SetPlayerProperties()
        {
            if (LobbySceneManager.Instance == null)
                return;

            var playerData = LobbySceneManager.Instance.GetCurrentPlayerData();
            if (playerData == null)
                return;

            // 创建玩家自定义属性
            var playerProps = PhotonLobbyDataConverter.CreatePlayerProperties(playerData);

            // 设置到Photon
            if (PhotonNetworkAdapter.Instance != null)
            {
                // 这里需要在PhotonNetworkAdapter中添加设置玩家属性的方法
                PhotonNetworkAdapter.Instance.SetPlayerProperties(playerProps);
            }
        }

        /// <summary>
        /// 获取当前玩家名称
        /// </summary>
        private string GetCurrentPlayerName()
        {
            if (LobbySceneManager.Instance != null)
            {
                var playerData = LobbySceneManager.Instance.GetCurrentPlayerData();
                if (playerData != null && !string.IsNullOrEmpty(playerData.playerName))
                {
                    return playerData.playerName;
                }
            }
            return "Unknown Player";
        }

        /// <summary>
        /// 检查网络状态
        /// </summary>
        private bool CheckNetworkStatus(string operation)
        {
            if (!isConnected)
            {
                LogDebug($"无法执行 {operation}：未连接到网络");
                return false;
            }

            if (!isInLobby)
            {
                LogDebug($"无法执行 {operation}：未在大厅中");
                return false;
            }

            return true;
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 获取连接状态
        /// </summary>
        public bool IsConnected()
        {
            return isConnected;
        }

        /// <summary>
        /// 获取连接中状态
        /// </summary>
        public bool IsConnecting()
        {
            return isConnecting;
        }

        /// <summary>
        /// 获取大厅状态
        /// </summary>
        public bool IsInLobby()
        {
            return isInLobby;
        }

        /// <summary>
        /// 获取缓存的房间列表
        /// </summary>
        public List<LobbyRoomData> GetCachedRoomList()
        {
            return new List<LobbyRoomData>(cachedRoomList);
        }

        /// <summary>
        /// 强制重新连接
        /// </summary>
        public void ForceReconnect()
        {
            LogDebug("强制重新连接");

            // 先断开连接
            Disconnect();

            // 等待一帧后重新连接
            StartCoroutine(ReconnectCoroutine());
        }

        /// <summary>
        /// 重连协程
        /// </summary>
        private System.Collections.IEnumerator ReconnectCoroutine()
        {
            yield return new WaitForSeconds(1f);
            yield return StartCoroutine(ConnectToPhotonCoroutine());
        }

        /// <summary>
        /// 获取网络统计信息
        /// </summary>
        public string GetNetworkStats()
        {
            if (PhotonNetworkAdapter.Instance == null)
                return "PhotonNetworkAdapter 不可用";

            string stats = "=== 网络统计 ===\n";
            stats += $"连接状态: {isConnected}\n";
            stats += $"大厅状态: {isInLobby}\n";
            stats += $"房间数量: {cachedRoomList.Count}\n";

            if (PhotonNetworkAdapter.Instance.IsPhotonConnected)
            {
                stats += $"Photon房间总数: {PhotonNetworkAdapter.Instance.PhotonRoomCount}\n";
                stats += $"Photon玩家总数: {PhotonNetworkAdapter.Instance.PhotonPlayerCount}";
            }

            return stats;
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
                Debug.Log($"[LobbyNetworkManager] {message}");
            }
        }

        [ContextMenu("显示网络状态")]
        public void ShowNetworkStatus()
        {
            LogDebug(GetNetworkStats());
        }

        [ContextMenu("强制刷新房间列表")]
        public void ForceRefreshRoomList()
        {
            RefreshRoomList();
        }

        [ContextMenu("强制重新连接")]
        public void ForceReconnectDebug()
        {
            ForceReconnect();
        }

        [ContextMenu("测试创建房间")]
        public void TestCreateRoom()
        {
            CreateRoom("测试房间_" + Random.Range(1000, 9999), 4);
        }

        [ContextMenu("显示缓存房间列表")]
        public void ShowCachedRoomList()
        {
            LogDebug($"=== 缓存房间列表 ({cachedRoomList.Count}) ===");
            for (int i = 0; i < cachedRoomList.Count; i++)
            {
                var room = cachedRoomList[i];
                LogDebug($"{i + 1}. {room.roomName} ({room.currentPlayers}/{room.maxPlayers}) - {room.status}");
            }
        }

        #endregion
    }
}