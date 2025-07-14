using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Photon.Realtime;
using Core.Network;
using Lobby.Data;
using Lobby.Network;
using Photon.Pun;

namespace Lobby.Core
{
    /// <summary>
    /// 修复后的LobbyNetworkManager
    /// 由PersistentNetworkManager管理连接，专注于房间管理功能
    /// </summary>
    public class LobbyNetworkManager : MonoBehaviour
    {
        [Header("网络配置")]
        [SerializeField] private bool autoConnectOnStart = false; // 默认禁用，由PersistentNetworkManager管理
        [SerializeField] private float roomListRefreshInterval = 5f;
        [SerializeField] private float connectionTimeout = 10f;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        // 通过PersistentNetworkManager访问
        public static LobbyNetworkManager Instance => PersistentNetworkManager.Instance?.LobbyNetwork;

        // 网络状态
        private bool isConnected = false;
        private bool isConnecting = false;
        private bool isInLobby = false;
        private bool hasTriggeredRoomJoined = false;
        private string lastJoinedRoomName = "";

        // 事件订阅状态
        private bool isEventsSubscribed = false;

        // 房间数据
        private List<LobbyRoomData> cachedRoomList = new List<LobbyRoomData>();
        private Coroutine connectionTimeoutCoroutine;

        // 事件
        public System.Action<bool> OnConnectionStatusChanged;
        public System.Action<List<LobbyRoomData>> OnRoomListUpdated;
        public System.Action<string, bool> OnRoomCreated;
        public System.Action<string, bool> OnRoomJoined;
        public System.Action<bool> OnLobbyStatusChanged;

        #region Unity生命周期

        private void Awake()
        {
            LogDebug("LobbyNetworkManager 已初始化（由PersistentNetworkManager管理）");
        }

        private void Start()
        {
            // 延迟初始化，确保PersistentNetworkManager准备就绪
            StartCoroutine(DelayedInitialization());
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
            UnsubscribeFromPhotonEvents();
            LogDebug("LobbyNetworkManager 已销毁");
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 延迟初始化
        /// </summary>
        private IEnumerator DelayedInitialization()
        {
            // 等待PersistentNetworkManager初始化完成
            float waitTime = 0f;
            while (PersistentNetworkManager.Instance == null && waitTime < 10f)
            {
                yield return new WaitForSeconds(0.1f);
                waitTime += 0.1f;
            }

            if (PersistentNetworkManager.Instance == null)
            {
                Debug.LogError("[LobbyNetworkManager] PersistentNetworkManager未找到！");
                yield break;
            }

            // 等待PersistentNetworkManager完全初始化
            while (!PersistentNetworkManager.Instance.IsInitialized)
            {
                yield return new WaitForSeconds(0.1f);
            }

            LogDebug("PersistentNetworkManager已就绪，开始初始化LobbyNetworkManager");

            // 订阅Photon事件
            SubscribeToPhotonEvents();

            // 检查当前状态并同步
            CheckAndSyncCurrentState();

            // 启动监控协程
            StartCoroutine(MonitorPhotonStatus());

            LogDebug("LobbyNetworkManager初始化完成");
        }

        #endregion

        #region 状态检查和同步

        /// <summary>
        /// 检查并同步当前真实状态
        /// </summary>
        private void CheckAndSyncCurrentState()
        {
            LogDebug("检查并同步当前Photon状态...");

            bool realConnected = GetRealConnectionStatus();
            bool realInLobby = GetRealLobbyStatus();
            bool realInRoom = PhotonNetwork.InRoom;

            LogDebug($"真实状态检查 - 连接: {realConnected}, 大厅: {realInLobby}, 房间: {realInRoom}");
            LogDebug($"内部状态 - 连接: {isConnected}, 大厅: {isInLobby}");

            // 同步连接状态
            if (realConnected != isConnected)
            {
                LogDebug($"同步连接状态: {isConnected} -> {realConnected}");
                isConnected = realConnected;
                OnConnectionStatusChanged?.Invoke(isConnected);
            }

            // 同步大厅状态
            if (realInLobby != isInLobby)
            {
                LogDebug($"同步大厅状态: {isInLobby} -> {realInLobby}");
                isInLobby = realInLobby;
                OnLobbyStatusChanged?.Invoke(isInLobby);

                if (isInLobby)
                {
                    LogDebug("检测到已在大厅中，立即刷新房间列表");
                    RefreshRoomList();
                }
            }

            // 重置连接中状态
            isConnecting = false;
        }

        /// <summary>
        /// 获取真实的连接状态
        /// </summary>
        private bool GetRealConnectionStatus()
        {
            return PhotonNetworkAdapter.Instance != null && PhotonNetworkAdapter.Instance.IsPhotonConnected;
        }

        /// <summary>
        /// 获取真实的大厅状态
        /// </summary>
        private bool GetRealLobbyStatus()
        {
            return PhotonNetworkAdapter.Instance != null && PhotonNetworkAdapter.Instance.IsInPhotonLobby;
        }

        /// <summary>
        /// 检查网络状态是否可以执行操作
        /// </summary>
        private bool CheckNetworkStatus(string operation)
        {
            bool realConnected = GetRealConnectionStatus();
            bool realInLobby = GetRealLobbyStatus();

            LogDebug($"检查网络状态用于 {operation}: 连接={realConnected}, 大厅={realInLobby}");

            if (!realConnected)
            {
                LogDebug($"无法执行 {operation}：未连接到网络");
                return false;
            }

            if (!realInLobby)
            {
                LogDebug($"无法执行 {operation}：未在大厅中，尝试加入大厅");
                JoinPhotonLobby();
                return false;
            }

            // 同步内部状态
            if (isConnected != realConnected)
            {
                isConnected = realConnected;
                OnConnectionStatusChanged?.Invoke(isConnected);
            }

            if (isInLobby != realInLobby)
            {
                isInLobby = realInLobby;
                OnLobbyStatusChanged?.Invoke(isInLobby);
            }

            return true;
        }

        #endregion

        #region 事件管理

        /// <summary>
        /// 订阅Photon事件
        /// </summary>
        private void SubscribeToPhotonEvents()
        {
            if (isEventsSubscribed)
            {
                LogDebug("Photon事件已经订阅过了");
                return;
            }

            if (PhotonNetworkAdapter.Instance == null)
            {
                LogDebug("PhotonNetworkAdapter.Instance 为空，稍后重试订阅");
                StartCoroutine(RetrySubscribeEvents());
                return;
            }

            try
            {
                PhotonNetworkAdapter.OnPhotonConnected += OnPhotonConnected;
                PhotonNetworkAdapter.OnPhotonDisconnected += OnPhotonDisconnected;
                PhotonNetworkAdapter.OnPhotonJoinedLobby += OnPhotonJoinedLobby;
                PhotonNetworkAdapter.OnPhotonLeftLobby += OnPhotonLeftLobby;
                PhotonNetworkAdapter.OnPhotonRoomListUpdate += OnPhotonRoomListUpdate;
                PhotonNetworkAdapter.OnPhotonRoomJoined += OnPhotonRoomJoined;
                PhotonNetworkAdapter.OnPhotonRoomLeft += OnPhotonRoomLeft;

                isEventsSubscribed = true;
                LogDebug("已订阅Photon事件");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LobbyNetworkManager] 订阅Photon事件失败: {e.Message}");
            }
        }

        /// <summary>
        /// 重试订阅事件
        /// </summary>
        private IEnumerator RetrySubscribeEvents()
        {
            int retryCount = 0;
            int maxRetries = 10;

            while (!isEventsSubscribed && retryCount < maxRetries)
            {
                yield return new WaitForSeconds(0.5f);

                if (PhotonNetworkAdapter.Instance != null)
                {
                    LogDebug($"重试订阅Photon事件 (第{retryCount + 1}次)");
                    SubscribeToPhotonEvents();
                    break;
                }

                retryCount++;
            }

            if (!isEventsSubscribed)
            {
                Debug.LogError("[LobbyNetworkManager] 无法订阅Photon事件，PhotonNetworkAdapter始终为空");
            }
        }

        /// <summary>
        /// 取消订阅Photon事件
        /// </summary>
        private void UnsubscribeFromPhotonEvents()
        {
            if (!isEventsSubscribed)
            {
                return;
            }

            if (PhotonNetworkAdapter.Instance != null)
            {
                try
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
                catch (System.Exception e)
                {
                    Debug.LogError($"[LobbyNetworkManager] 取消订阅Photon事件失败: {e.Message}");
                }
            }

            isEventsSubscribed = false;
        }

        #endregion

        #region 连接管理

        /// <summary>
        /// 监控Photon状态的协程
        /// </summary>
        private IEnumerator MonitorPhotonStatus()
        {
            while (true)
            {
                yield return new WaitForSeconds(5f);

                if (enableDebugLogs)
                {
                    bool realConnected = GetRealConnectionStatus();
                    bool realInLobby = GetRealLobbyStatus();

                    // 只在状态不一致时进行同步
                    if (realConnected != isConnected || realInLobby != isInLobby)
                    {
                        LogDebug($"[状态监控] 检测到状态不一致，进行同步");
                        LogDebug($"  内部状态: 连接={isConnected}, 大厅={isInLobby}");
                        LogDebug($"  真实状态: 连接={realConnected}, 大厅={realInLobby}");
                        CheckAndSyncCurrentState();
                    }
                }
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
            else
            {
                LogDebug("PhotonNetworkAdapter.Instance 为空，无法加入大厅");
            }
        }

        #endregion

        #region 房间管理

        public void CreateRoom(string roomName, int maxPlayers, string password = "")
        {
            LogDebug($"尝试创建房间: {roomName}, 最大人数: {maxPlayers}");

            if (!CheckNetworkStatus("创建房间"))
            {
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
            LogDebug($"尝试加入房间: {roomData?.roomName}");

            if (!CheckNetworkStatus("加入房间"))
            {
                OnRoomJoined?.Invoke(roomData?.roomName ?? "Unknown", false);
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

            if (roomData.hasPassword)
            {
                LogDebug("房间需要密码，但暂未实现密码验证");
            }

            // 调用PhotonNetworkAdapter加入房间
            if (PhotonNetworkAdapter.Instance != null)
            {
                PhotonNetworkAdapter.Instance.JoinPhotonRoomByName(roomData.roomName);
            }
        }

        /// <summary>
        /// 加入随机房间
        /// </summary>
        public void JoinRandomRoom()
        {
            LogDebug("尝试加入随机房间");

            if (!CheckNetworkStatus("加入随机房间"))
            {
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
                PhotonNetworkAdapter.Instance.JoinPhotonRoom();
            }
        }

        /// <summary>
        /// 刷新房间列表
        /// </summary>
        public void RefreshRoomList()
        {
            LogDebug("尝试刷新房间列表");

            if (!CheckNetworkStatus("刷新房间列表"))
            {
                return;
            }

            LogDebug($"房间列表刷新 - 状态检查通过");

            // Photon会自动维护房间列表，这里主要是触发更新
            if (PhotonNetworkAdapter.Instance != null)
            {
                var photonRooms = PhotonNetworkAdapter.Instance.GetPhotonRoomList();
                LogDebug($"从PhotonNetworkAdapter获取到 {photonRooms.Count} 个房间");
                OnPhotonRoomListUpdate(photonRooms);
            }
            else
            {
                LogDebug("PhotonNetworkAdapter.Instance 为空，无法刷新房间列表");
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
            if (!GetRealLobbyStatus())
            {
                //JoinPhotonLobby();
            }
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
            isConnecting = false;
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
            LogDebug("=== OnPhotonRoomJoined 被调用 ===");

            // 防重复检查
            string roomName = PhotonNetwork.CurrentRoom?.Name ?? "";
            if (hasTriggeredRoomJoined && lastJoinedRoomName == roomName)
            {
                LogDebug($"房间加入事件已触发过，跳过重复调用: {roomName}");
                return;
            }

            LogDebug("成功加入Photon房间");

            bool isInRoom = PhotonNetwork.InRoom;

            LogDebug($"房间状态检查 - PhotonNetwork.InRoom: {isInRoom}, RoomName: '{roomName}'");

            if (isInRoom && !string.IsNullOrEmpty(roomName))
            {
                LogDebug($"房间加入成功: {roomName}");

                // 设置防重复标记
                hasTriggeredRoomJoined = true;
                lastJoinedRoomName = roomName;

                OnRoomJoined?.Invoke(roomName, true);
                LogDebug("OnRoomJoined事件已触发");
            }
            else
            {
                LogDebug("房间状态未就绪，延迟0.5秒后重新检查");
                StartCoroutine(DelayedRoomJoinedCheck());
            }
        }

        /// <summary>
        /// 延迟检查房间加入状态
        /// </summary>
        private System.Collections.IEnumerator DelayedRoomJoinedCheck()
        {
            yield return new WaitForSeconds(0.5f);

            string roomName = PhotonNetwork.CurrentRoom?.Name ?? "";

            // 防重复检查
            if (hasTriggeredRoomJoined && lastJoinedRoomName == roomName)
            {
                LogDebug($"延迟检查：房间加入事件已触发过，跳过: {roomName}");
                yield break;
            }

            bool isInRoom = PhotonNetwork.InRoom;
            LogDebug($"延迟检查 - PhotonNetwork.InRoom: {isInRoom}, RoomName: '{roomName}'");

            if (isInRoom && !string.IsNullOrEmpty(roomName))
            {
                LogDebug($"延迟检查成功 - 房间加入成功: {roomName}");

                // 设置防重复标记
                hasTriggeredRoomJoined = true;
                lastJoinedRoomName = roomName;

                OnRoomJoined?.Invoke(roomName, true);
                LogDebug("OnRoomJoined事件已触发（延迟检查）");
            }
            else
            {
                Debug.LogError($"[LobbyNetworkManager] 房间加入失败 - InRoom: {isInRoom}, RoomName: '{roomName}'");
                OnRoomJoined?.Invoke("Unknown", false);
            }
        }

        private void OnPhotonRoomLeft()
        {
            LogDebug("离开Photon房间");

            // 重置防重复标记
            hasTriggeredRoomJoined = false;
            lastJoinedRoomName = "";

            // 重新加入大厅以继续浏览房间
            if (GetRealConnectionStatus() && !GetRealLobbyStatus())
            {
                JoinPhotonLobby();
            }
        }

        #endregion

        #region 辅助方法
        /// <summary>
        /// 由LobbySceneManager调用，设置玩家名称
        /// </summary>
        public void SetPlayerName(string playerName)
        {
            if (string.IsNullOrEmpty(playerName))
            {
                LogDebug("收到空的玩家名称，使用默认名称");
                playerName = "默认玩家";
            }

            // 立即设置PhotonNetwork.NickName
            PhotonNetwork.NickName = playerName;
            LogDebug($"✓ 收到LobbySceneManager通知，设置PhotonNetwork.NickName: '{playerName}'");
        }
        /// <summary>
        /// 设置玩家属性
        /// </summary>
        private void SetPlayerProperties()
        {
            // 如果PhotonNetwork.NickName已经设置（由LobbySceneManager设置），直接使用
            if (!string.IsNullOrEmpty(PhotonNetwork.NickName))
            {
                LogDebug($"使用已设置的PhotonNetwork.NickName: '{PhotonNetwork.NickName}'");
                return;
            }

            // 备用方案：如果还没设置，使用默认名称
            PhotonNetwork.NickName = "未知玩家";
            LogDebug("使用备用玩家名称: '未知玩家'");
        }

        /// <summary>
        /// 获取当前玩家名称
        /// </summary>
        private string GetCurrentPlayerName()
        {
            var lobbySceneManager = FindObjectOfType<LobbySceneManager>();
            if (lobbySceneManager != null)
            {
                var playerData = lobbySceneManager.GetCurrentPlayerData();
                if (playerData != null && !string.IsNullOrEmpty(playerData.playerName))
                {
                    return playerData.playerName;
                }
            }
            return "Unknown Player";
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 获取连接状态
        /// </summary>
        public bool IsConnected()
        {
            bool realStatus = GetRealConnectionStatus();
            if (realStatus != isConnected)
            {
                LogDebug($"连接状态不一致，同步: {isConnected} -> {realStatus}");
                isConnected = realStatus;
            }
            return realStatus;
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
            bool realStatus = GetRealLobbyStatus();
            if (realStatus != isInLobby)
            {
                LogDebug($"大厅状态不一致，同步: {isInLobby} -> {realStatus}");
                isInLobby = realStatus;
            }
            return realStatus;
        }

        /// <summary>
        /// 获取缓存的房间列表
        /// </summary>
        public List<LobbyRoomData> GetCachedRoomList()
        {
            return new List<LobbyRoomData>(cachedRoomList);
        }

        /// <summary>
        /// 强制重新连接（委托给PersistentNetworkManager）
        /// </summary>
        public void ForceReconnect()
        {
            LogDebug("委托给PersistentNetworkManager进行重连");

            if (PersistentNetworkManager.Instance != null)
            {
                PersistentNetworkManager.Instance.DisconnectFromPhoton();
                StartCoroutine(DelayedReconnect());
            }
        }

        /// <summary>
        /// 延迟重连协程
        /// </summary>
        private System.Collections.IEnumerator DelayedReconnect()
        {
            yield return new WaitForSeconds(1f);

            if (PersistentNetworkManager.Instance != null)
            {
                PersistentNetworkManager.Instance.ConnectToPhoton();
            }
        }

        /// <summary>
        /// 获取网络统计信息
        /// </summary>
        public string GetNetworkStats()
        {
            if (PhotonNetworkAdapter.Instance == null)
                return "PhotonNetworkAdapter 不可用";

            string stats = "=== LobbyNetworkManager统计 ===\n";
            stats += $"事件已订阅: {isEventsSubscribed}\n";
            stats += $"内部连接状态: {isConnected}\n";
            stats += $"内部大厅状态: {isInLobby}\n";
            stats += $"真实连接状态: {GetRealConnectionStatus()}\n";
            stats += $"真实大厅状态: {GetRealLobbyStatus()}\n";
            stats += $"房间数量: {cachedRoomList.Count}\n";

            if (PhotonNetworkAdapter.Instance.IsPhotonConnected)
            {
                stats += $"Photon房间总数: {PhotonNetworkAdapter.Instance.PhotonRoomCount}\n";
                stats += $"Photon玩家总数: {PhotonNetworkAdapter.Instance.PhotonPlayerCount}";
            }

            return stats;
        }

        /// <summary>
        /// 强制状态同步
        /// </summary>
        public void ForceSyncState()
        {
            LogDebug("强制同步状态");
            CheckAndSyncCurrentState();
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

        [ContextMenu("强制状态同步")]
        public void ForceSyncStateDebug()
        {
            ForceSyncState();
        }

        [ContextMenu("检查事件订阅状态")]
        public void CheckEventSubscriptionStatus()
        {
            LogDebug($"事件订阅状态: {isEventsSubscribed}");
            LogDebug($"PhotonNetworkAdapter可用: {PhotonNetworkAdapter.Instance != null}");
        }

        #endregion
    }
}