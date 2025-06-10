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
    /// 修复后的 Lobby网络管理器
    /// 主要修复：状态同步问题、事件订阅时机、状态检查逻辑
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

        // 网络状态 - 修复：增加状态验证方法
        private bool isConnected = false;
        private bool isConnecting = false;
        private bool isInLobby = false;
        private bool hasTriggeredRoomJoined = false;
        private string lastJoinedRoomName = "";

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
            if (Instance == null)
            {
                Instance = this;
                LogDebug("LobbyNetworkManager 实例已创建");

                // 修复1：在 Awake 中就订阅事件，确保不会错过早期事件
                SubscribeToPhotonEvents();
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
            // 修复2：先检查当前状态，再决定是否需要连接
            CheckAndSyncCurrentState();

            // 强制重新订阅事件，确保不会丢失
            Invoke(nameof(ForceResubscribeEvents), 1f);

            if (autoConnectOnStart && !GetRealConnectionStatus())
            {
                StartCoroutine(ConnectToPhotonCoroutine());
            }

            StartCoroutine(MonitorPhotonStatus());
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

        #region 修复：状态检查和同步方法

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

            // 移除手动触发房间加入事件的代码，避免重复
        }
        private void ForceResubscribeEvents()
        {
            LogDebug("强制重新订阅Photon事件");
            SubscribeToPhotonEvents();
        }
        /// <summary>
        /// 获取真实的连接状态（直接查询Photon）
        /// </summary>
        private bool GetRealConnectionStatus()
        {
            return PhotonNetworkAdapter.Instance != null && PhotonNetworkAdapter.Instance.IsPhotonConnected;
        }

        /// <summary>
        /// 获取真实的大厅状态（直接查询Photon）
        /// </summary>
        private bool GetRealLobbyStatus()
        {
            return PhotonNetworkAdapter.Instance != null && PhotonNetworkAdapter.Instance.IsInPhotonLobby;
        }

        /// <summary>
        /// 修复的网络状态检查方法
        /// </summary>
        private bool CheckNetworkStatus(string operation)
        {
            // 修复3：使用真实状态进行检查，而不是内部缓存状态
            bool realConnected = GetRealConnectionStatus();
            bool realInLobby = GetRealLobbyStatus();

            LogDebug($"检查网络状态用于 {operation}:");
            LogDebug($"  真实连接状态: {realConnected}");
            LogDebug($"  真实大厅状态: {realInLobby}");
            LogDebug($"  内部连接状态: {isConnected}");
            LogDebug($"  内部大厅状态: {isInLobby}");

            if (!realConnected)
            {
                LogDebug($"无法执行 {operation}：未连接到网络（真实状态检查）");
                return false;
            }

            if (!realInLobby)
            {
                LogDebug($"无法执行 {operation}：未在大厅中（真实状态检查）");
                return false;
            }

            // 同步内部状态
            if (isConnected != realConnected)
            {
                LogDebug("同步内部连接状态");
                isConnected = realConnected;
                OnConnectionStatusChanged?.Invoke(isConnected);
            }

            if (isInLobby != realInLobby)
            {
                LogDebug("同步内部大厅状态");
                isInLobby = realInLobby;
                OnLobbyStatusChanged?.Invoke(isInLobby);
            }

            return true;
        }

        #endregion

        #region 连接管理（保持原有逻辑，但增加状态同步）

        /// <summary>
        /// 连接到Photon的协程
        /// </summary>
        private IEnumerator ConnectToPhotonCoroutine()
        {
            LogDebug("=== 开始Photon连接流程 ===");

            // 1. 检查PhotonNetworkAdapter
            if (PhotonNetworkAdapter.Instance == null)
            {
                Debug.LogError("[LobbyNetworkManager] PhotonNetworkAdapter.Instance 为空！");
                Debug.LogError("请检查场景中是否有PhotonNetworkAdapter组件");
                yield break;
            }

            LogDebug("✓ PhotonNetworkAdapter.Instance 存在");

            // 2. 检查当前真实状态
            if (GetRealConnectionStatus())
            {
                LogDebug("检测到已连接到Photon，同步状态并尝试加入大厅");
                isConnected = true;
                OnConnectionStatusChanged?.Invoke(true);

                if (!GetRealLobbyStatus())
                {
                    JoinPhotonLobby();
                }
                else
                {
                    LogDebug("已在大厅中，刷新房间列表");
                    isInLobby = true;
                    OnLobbyStatusChanged?.Invoke(true);
                    RefreshRoomList();
                }
                yield break;
            }

            // 3. 开始连接流程
            isConnecting = true;
            OnConnectionStatusChanged?.Invoke(false);

            // 4. 尝试连接
            LogDebug("开始连接到Photon服务器...");

            try
            {
                if (!PhotonNetwork.IsConnected && PhotonNetwork.NetworkClientState == ClientState.PeerCreated)
                {
                    LogDebug("Photon未初始化，调用ConnectUsingSettings...");
                    bool connectResult = PhotonNetwork.ConnectUsingSettings();
                    LogDebug($"ConnectUsingSettings 返回结果: {connectResult}");

                    if (!connectResult)
                    {
                        Debug.LogError("[LobbyNetworkManager] PhotonNetwork.ConnectUsingSettings() 返回false");
                        isConnecting = false;
                        OnConnectionStatusChanged?.Invoke(false);
                        yield break;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LobbyNetworkManager] Photon连接异常: {e.Message}");
                isConnecting = false;
                OnConnectionStatusChanged?.Invoke(false);
                yield break;
            }

            // 5. 开始连接超时计时
            connectionTimeoutCoroutine = StartCoroutine(ConnectionTimeoutCoroutine());

            // 6. 等待连接完成
            float waitTime = 0f;
            while (isConnecting && !GetRealConnectionStatus() && waitTime < connectionTimeout)
            {
                LogDebug($"等待连接... 状态: {PhotonNetwork.NetworkClientState}, 等待时间: {waitTime:F1}s");
                yield return new WaitForSeconds(1f);
                waitTime += 1f;
            }

            // 7. 停止超时计时
            if (connectionTimeoutCoroutine != null)
            {
                StopCoroutine(connectionTimeoutCoroutine);
                connectionTimeoutCoroutine = null;
            }

            // 8. 连接结果
            bool finalConnected = GetRealConnectionStatus();
            if (finalConnected)
            {
                LogDebug("✓ Photon连接成功！");
                isConnected = true;
                isConnecting = false;
                OnConnectionStatusChanged?.Invoke(true);
            }
            else
            {
                Debug.LogError($"✗ Photon连接失败！最终状态: {PhotonNetwork.NetworkClientState}");
                isConnecting = false;
                OnConnectionStatusChanged?.Invoke(false);
            }
        }

        /// <summary>
        /// 监控Photon状态的协程（增强版）
        /// </summary>
        private IEnumerator MonitorPhotonStatus()
        {
            while (true)
            {
                yield return new WaitForSeconds(5f);

                if (enableDebugLogs)
                {
                    LogDebug($"[状态监控] Photon状态: {PhotonNetwork.NetworkClientState}, " +
                            $"连接: {PhotonNetwork.IsConnected}, " +
                            $"大厅: {PhotonNetwork.InLobby}");

                    // 修复4：定期检查状态一致性
                    bool realConnected = GetRealConnectionStatus();
                    bool realInLobby = GetRealLobbyStatus();

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
        /// 连接超时协程
        /// </summary>
        private IEnumerator ConnectionTimeoutCoroutine()
        {
            yield return new WaitForSeconds(connectionTimeout);

            if (isConnecting && !GetRealConnectionStatus())
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
            else
            {
                LogDebug("PhotonNetworkAdapter.Instance 为空，无法订阅事件");
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

        #region 房间管理（修复状态检查）

        public void CreateRoom(string roomName, int maxPlayers, string password = "")
        {
            LogDebug($"尝试创建房间: {roomName}, 最大人数: {maxPlayers}");

            // 修复5：使用修复后的状态检查方法
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

            // TODO: 如果房间有密码，这里需要处理密码验证
            if (roomData.hasPassword)
            {
                LogDebug("房间需要密码，但暂未实现密码验证");
                // 在阶段3可以扩展密码输入功能
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
        /// 刷新房间列表（修复版）
        /// </summary>
        public void RefreshRoomList()
        {
            LogDebug("尝试刷新房间列表");

            // 修复6：使用修复后的状态检查，并增加详细日志
            LogDebug($"刷新房间列表状态检查:");
            LogDebug($"  GetRealConnectionStatus(): {GetRealConnectionStatus()}");
            LogDebug($"  GetRealLobbyStatus(): {GetRealLobbyStatus()}");
            LogDebug($"  内部 isConnected: {isConnected}");
            LogDebug($"  内部 isInLobby: {isInLobby}");

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

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            LogDebug("断开网络连接");

            if (PhotonNetworkAdapter.Instance != null)
            {
                if (GetRealLobbyStatus())
                {
                    PhotonNetworkAdapter.Instance.LeavePhotonLobby();
                }
                PhotonNetworkAdapter.Instance.DisconnectPhoton();
            }
        }

        #endregion

        #region Photon事件处理（保持原有逻辑）

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

        #region 辅助方法（保持不变）

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

        #endregion

        #region 公共接口（增强版）

        /// <summary>
        /// 获取连接状态（使用真实状态）
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
        /// 获取大厅状态（使用真实状态）
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
        /// 获取网络统计信息（增强版）
        /// </summary>
        public string GetNetworkStats()
        {
            if (PhotonNetworkAdapter.Instance == null)
                return "PhotonNetworkAdapter 不可用";

            string stats = "=== 网络统计 ===\n";
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
        /// 强制状态同步（调试用）
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

        [ContextMenu("详细状态诊断")]
        public void DetailedStatusDiagnosis()
        {
            LogDebug("=== 详细状态诊断 ===");
            LogDebug($"PhotonNetworkAdapter.Instance: {(PhotonNetworkAdapter.Instance != null ? "存在" : "为空")}");

            if (PhotonNetworkAdapter.Instance != null)
            {
                LogDebug($"PhotonNetworkAdapter.IsPhotonConnected: {PhotonNetworkAdapter.Instance.IsPhotonConnected}");
                LogDebug($"PhotonNetworkAdapter.IsInPhotonLobby: {PhotonNetworkAdapter.Instance.IsInPhotonLobby}");
            }

            LogDebug($"PhotonNetwork.IsConnected: {PhotonNetwork.IsConnected}");
            LogDebug($"PhotonNetwork.InLobby: {PhotonNetwork.InLobby}");
            LogDebug($"PhotonNetwork.NetworkClientState: {PhotonNetwork.NetworkClientState}");

            LogDebug($"LobbyNetworkManager.isConnected: {isConnected}");
            LogDebug($"LobbyNetworkManager.isInLobby: {isInLobby}");
            LogDebug($"LobbyNetworkManager.isConnecting: {isConnecting}");

            LogDebug($"GetRealConnectionStatus(): {GetRealConnectionStatus()}");
            LogDebug($"GetRealLobbyStatus(): {GetRealLobbyStatus()}");

            LogDebug($"缓存房间数量: {cachedRoomList.Count}");

            if (PhotonNetworkAdapter.Instance != null)
            {
                var photonRooms = PhotonNetworkAdapter.Instance.GetPhotonRoomList();
                LogDebug($"PhotonAdapter房间数量: {photonRooms.Count}");
            }
        }

        #endregion
    }
}