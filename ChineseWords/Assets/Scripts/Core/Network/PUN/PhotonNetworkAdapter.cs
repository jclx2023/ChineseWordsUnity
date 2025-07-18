using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;

namespace Core.Network
{
    /// <summary>
    /// 持久化Photon网络适配器
    /// 专注于Photon连接管理，不再使用DontDestroyOnLoad（由PersistentNetworkManager管理）
    /// </summary>
    public class PhotonNetworkAdapter : MonoBehaviourPun, IConnectionCallbacks, IMatchmakingCallbacks, ILobbyCallbacks
    {
        [Header("Photon配置")]
        [SerializeField] private string gameVersion = "0.5";
        [SerializeField] private bool enableDebugLogs = true;

        // 移除单例模式，改为通过PersistentNetworkManager访问
        public static PhotonNetworkAdapter Instance => PersistentNetworkManager.Instance?.PhotonAdapter;

        #region 事件系统

        // 连接相关事件
        public static event System.Action OnPhotonConnected;
        public static event System.Action OnPhotonDisconnected;

        // 房间相关事件
        public static event System.Action OnPhotonRoomCreated;
        public static event System.Action OnPhotonRoomJoined;
        public static event System.Action OnPhotonRoomLeft;

        // Host相关事件
        public static event System.Action OnPhotonHostStarted;
        public static event System.Action OnPhotonHostStopped;

        // 玩家相关事件
        public static event System.Action<ushort> OnPhotonPlayerJoined;
        public static event System.Action<ushort> OnPhotonPlayerLeft;

        // Lobby相关事件
        public static event System.Action OnPhotonJoinedLobby;
        public static event System.Action OnPhotonLeftLobby;
        public static event System.Action<List<RoomInfo>> OnPhotonRoomListUpdate;

        #endregion

        #region 状态属性

        // Photon专用状态
        public bool IsPhotonConnected => PhotonNetwork.IsConnected;
        public bool IsInPhotonRoom => PhotonNetwork.InRoom;
        public bool IsPhotonMasterClient => PhotonNetwork.IsMasterClient;
        public ushort PhotonClientId => PhotonNetwork.LocalPlayer?.ActorNumber != null ? (ushort)PhotonNetwork.LocalPlayer.ActorNumber : (ushort)0;

        // 房间信息
        public string CurrentRoomName => PhotonNetwork.CurrentRoom?.Name ?? "";
        public int CurrentRoomPlayerCount => PhotonNetwork.CurrentRoom?.PlayerCount ?? 0;
        public int CurrentRoomMaxPlayers => PhotonNetwork.CurrentRoom?.MaxPlayers ?? 0;
        public ushort PhotonMasterClientId => PhotonNetwork.InRoom ? (ushort)PhotonNetwork.CurrentRoom.MasterClientId : (ushort)0;
        public bool IsInPhotonLobby => PhotonNetwork.InLobby;
        public int PhotonRoomCount => PhotonNetwork.CountOfRooms;
        public int PhotonPlayerCount => PhotonNetwork.CountOfPlayers;

        // 适配器状态
        private bool isInitialized = false;
        private string pendingRoomName = "";
        private int pendingMaxPlayers = 4;
        private bool isPendingClient = false;

        // 连接状态跟踪
        private bool hasTriggeredConnectedEvent = false;
        private bool isWaitingForRoomOperation = false;

        private List<RoomInfo> cachedRoomList = new List<RoomInfo>();

        #endregion

        #region Unity生命周期

        private void Awake()
        {
            // 移除单例逻辑，改为由PersistentNetworkManager管理
            InitializePhoton();
            PhotonNetwork.AddCallbackTarget(this);
            LogDebug("PhotonNetworkAdapter 已初始化（持久化版本）");
        }

        private void Start()
        {
            // 检查当前连接状态
            CheckAndSyncConnectionStatus();
        }

        private void OnDestroy()
        {
            CleanupPhoton();
            PhotonNetwork.RemoveCallbackTarget(this);
            LogDebug("PhotonNetworkAdapter 已销毁");
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化Photon
        /// </summary>
        private void InitializePhoton()
        {
            if (isInitialized)
            {
                LogDebug("PhotonNetworkAdapter 已经初始化");
                return;
            }

            try
            {
                PhotonNetwork.AutomaticallySyncScene = false;
                PhotonNetwork.GameVersion = gameVersion;

                isInitialized = true;
                LogDebug("Photon适配器初始化完成");

                // 检查当前连接状态
                CheckAndSyncConnectionStatus();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Photon适配器初始化失败: {e.Message}");
            }
        }

        /// <summary>
        /// 检查并同步连接状态
        /// </summary>
        private void CheckAndSyncConnectionStatus()
        {
            if (PhotonNetwork.IsConnected && !hasTriggeredConnectedEvent)
            {
                LogDebug("检测到已连接到Photon，同步连接状态");
                hasTriggeredConnectedEvent = true;
                OnPhotonConnected?.Invoke();

                if (PhotonNetwork.InRoom)
                {
                    LogDebug("检测到已在房间中，同步房间状态");
                    OnPhotonRoomJoined?.Invoke();

                    if (PhotonNetwork.IsMasterClient)
                    {
                        OnPhotonHostStarted?.Invoke();
                    }
                }

                if (PhotonNetwork.InLobby)
                {
                    LogDebug("检测到已在大厅中，同步大厅状态");
                    OnPhotonJoinedLobby?.Invoke();
                }
            }
        }

        /// <summary>
        /// 清理Photon资源
        /// </summary>
        private void CleanupPhoton()
        {
            // 持久化版本不主动断开连接，由PersistentNetworkManager控制
            LogDebug("Photon适配器资源已清理（保持连接）");
        }

        #endregion

        #region 公共接口方法

        /// <summary>
        /// 连接到Photon并创建房间（Host模式）
        /// </summary>
        public void CreatePhotonRoom(string roomName, int maxPlayers)
        {
            LogDebug($"创建Photon房间: {roomName}, 最大玩家: {maxPlayers}");

            pendingRoomName = roomName;
            pendingMaxPlayers = maxPlayers;
            isPendingClient = false;
            isWaitingForRoomOperation = true;

            if (!PhotonNetwork.IsConnected)
            {
                LogDebug("连接到Photon服务器...");
                PhotonNetwork.ConnectUsingSettings();
            }
            else if (!PhotonNetwork.InLobby)
            {
                LogDebug("已连接但未在大厅，加入大厅...");
                PhotonNetwork.JoinLobby();
            }
            else
            {
                LogDebug("已连接且在大厅，直接创建房间");
                CreateRoom();
            }
        }

        /// <summary>
        /// 连接到Photon并加入房间（Client模式）
        /// </summary>
        public void JoinPhotonRoom()
        {
            LogDebug("加入随机Photon房间");

            isPendingClient = true;
            isWaitingForRoomOperation = true;

            if (!PhotonNetwork.IsConnected)
            {
                LogDebug("连接到Photon服务器...");
                PhotonNetwork.ConnectUsingSettings();
            }
            else if (!PhotonNetwork.InLobby)
            {
                LogDebug("已连接但未在大厅，加入大厅...");
                PhotonNetwork.JoinLobby();
            }
            else
            {
                LogDebug("已连接且在大厅，直接加入随机房间");
                JoinRandomRoom();
            }
        }

        /// <summary>
        /// 按房间名称加入房间
        /// </summary>
        public void JoinPhotonRoomByName(string roomName)
        {
            if (string.IsNullOrEmpty(roomName))
            {
                LogDebug("房间名称为空，无法加入");
                return;
            }

            LogDebug($"按名称加入房间: {roomName}");

            isWaitingForRoomOperation = true;

            if (!PhotonNetwork.IsConnected)
            {
                LogDebug("未连接到Photon，无法加入房间");
                InvokeRoomJoinFailed(roomName);
                return;
            }

            if (PhotonNetwork.InRoom)
            {
                LogDebug("已在其他房间中，先离开当前房间");
                PhotonNetwork.LeaveRoom();
                StartCoroutine(DelayedJoinRoom(roomName, 1f));
                return;
            }

            PhotonNetwork.JoinRoom(roomName);
        }

        /// <summary>
        /// 延迟加入房间协程
        /// </summary>
        private IEnumerator DelayedJoinRoom(string roomName, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (!PhotonNetwork.InRoom)
            {
                LogDebug($"延迟加入房间: {roomName}");
                PhotonNetwork.JoinRoom(roomName);
            }
        }

        /// <summary>
        /// 触发房间加入失败事件
        /// </summary>
        private void InvokeRoomJoinFailed(string roomName)
        {
            isWaitingForRoomOperation = false;
            LogDebug($"房间加入失败: {roomName}");
        }

        /// <summary>
        /// 离开当前Photon房间
        /// </summary>
        public void LeavePhotonRoom()
        {
            if (PhotonNetwork.InRoom)
            {
                LogDebug("离开Photon房间");
                PhotonNetwork.LeaveRoom();
            }
            else
            {
                LogDebug("不在任何房间中");
            }
        }

        /// <summary>
        /// 断开Photon连接
        /// </summary>
        public void DisconnectPhoton()
        {
            if (PhotonNetwork.IsConnected)
            {
                LogDebug("断开Photon连接");
                hasTriggeredConnectedEvent = false;
                PhotonNetwork.Disconnect();
            }
            else
            {
                LogDebug("Photon未连接");
            }
        }

        /// <summary>
        /// 获取Photon状态信息
        /// </summary>
        public string GetPhotonStatus()
        {
            return $"Connected: {IsPhotonConnected}, InRoom: {IsInPhotonRoom}, " +
                   $"IsMasterClient: {IsPhotonMasterClient}, ClientId: {PhotonClientId}, " +
                   $"RoomName: {CurrentRoomName}, Players: {CurrentRoomPlayerCount}/{CurrentRoomMaxPlayers}";
        }

        #endregion

        #region Lobby方法

        /// <summary>
        /// 加入Photon大厅
        /// </summary>
        public void JoinPhotonLobby()
        {
            if (!PhotonNetwork.IsConnected)
            {
                LogDebug("未连接到Photon，无法加入大厅");
                return;
            }

            if (PhotonNetwork.InLobby)
            {
                LogDebug("已在大厅中");
                OnPhotonJoinedLobby?.Invoke();
                return;
            }

            LogDebug("加入Photon大厅");
            PhotonNetwork.JoinLobby();
        }

        /// <summary>
        /// 离开Photon大厅
        /// </summary>
        public void LeavePhotonLobby()
        {
            if (!PhotonNetwork.InLobby)
            {
                LogDebug("不在大厅中");
                return;
            }

            LogDebug("离开Photon大厅");
            PhotonNetwork.LeaveLobby();
        }

        /// <summary>
        /// 获取缓存的房间列表
        /// </summary>
        public List<RoomInfo> GetPhotonRoomList()
        {
            if (!PhotonNetwork.InLobby)
            {
                LogDebug("不在大厅中，返回空房间列表");
                return new List<RoomInfo>();
            }

            return new List<RoomInfo>(cachedRoomList);
        }


        /// <summary>
        /// 更新缓存的房间列表
        /// </summary>
        private void UpdateCachedRoomList(List<RoomInfo> roomUpdates)
        {
            foreach (var roomUpdate in roomUpdates)
            {
                if (roomUpdate.RemovedFromList || !roomUpdate.IsOpen)
                {
                    RemoveRoomFromCache(roomUpdate.Name);
                    LogDebug($"从缓存中移除房间: {roomUpdate.Name}");
                }
                else
                {
                    UpdateRoomInCache(roomUpdate);
                    LogDebug($"更新缓存中的房间: {roomUpdate.Name} ({roomUpdate.PlayerCount}/{roomUpdate.MaxPlayers})");
                }
            }

            LogDebug($"房间列表缓存更新完成，当前有 {cachedRoomList.Count} 个房间");
        }

        /// <summary>
        /// 从缓存中移除房间
        /// </summary>
        private void RemoveRoomFromCache(string roomName)
        {
            for (int i = cachedRoomList.Count - 1; i >= 0; i--)
            {
                if (cachedRoomList[i].Name == roomName)
                {
                    cachedRoomList.RemoveAt(i);
                    break;
                }
            }
        }

        /// <summary>
        /// 更新缓存中的房间
        /// </summary>
        private void UpdateRoomInCache(RoomInfo roomUpdate)
        {
            bool found = false;
            for (int i = 0; i < cachedRoomList.Count; i++)
            {
                if (cachedRoomList[i].Name == roomUpdate.Name)
                {
                    cachedRoomList[i] = roomUpdate;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                cachedRoomList.Add(roomUpdate);
            }
        }

        #endregion

        #region 内部方法

        /// <summary>
        /// 创建房间
        /// </summary>
        private void CreateRoom()
        {
            RoomOptions roomOptions = new RoomOptions()
            {
                MaxPlayers = (byte)pendingMaxPlayers,
                IsVisible = true,
                IsOpen = true,
                CustomRoomProperties = new ExitGames.Client.Photon.Hashtable()
                {
                    { "gameMode", "ChineseWords" },
                    { "version", gameVersion },
                    { "hostName", PhotonNetwork.LocalPlayer.NickName ?? "Host" },
                    { "createTime", Time.time }
                }
            };

            LogDebug($"创建房间: {pendingRoomName}");
            PhotonNetwork.CreateRoom(pendingRoomName, roomOptions);
        }

        /// <summary>
        /// 加入随机房间
        /// </summary>
        private void JoinRandomRoom()
        {
            ExitGames.Client.Photon.Hashtable expectedProperties = new ExitGames.Client.Photon.Hashtable()
            {
                { "gameMode", "ChineseWords" }
            };

            LogDebug("加入随机房间");
            PhotonNetwork.JoinRandomRoom(expectedProperties, 0);
        }

        #endregion

        #region Photon回调实现

        #region IConnectionCallbacks

        public void OnConnected()
        {
            LogDebug("已连接到Photon网络");
        }

        public void OnConnectedToMaster()
        {
            LogDebug("已连接到Photon主服务器");

            if (!hasTriggeredConnectedEvent)
            {
                hasTriggeredConnectedEvent = true;
                OnPhotonConnected?.Invoke();
                LogDebug("触发Photon连接成功事件");
            }

            if (isWaitingForRoomOperation)
            {
                if (!PhotonNetwork.InLobby)
                {
                    LogDebug("连接成功，加入大厅以进行房间操作");
                    PhotonNetwork.JoinLobby();
                }
                else
                {
                    ProcessPendingRoomOperation();
                }
            }
            else
            {
                if (!PhotonNetwork.InLobby)
                {
                    PhotonNetwork.JoinLobby();
                }
            }
        }

        /// <summary>
        /// 处理待处理的房间操作
        /// </summary>
        private void ProcessPendingRoomOperation()
        {
            if (isPendingClient)
            {
                JoinRandomRoom();
                isPendingClient = false;
            }
            else if (!string.IsNullOrEmpty(pendingRoomName))
            {
                CreateRoom();
                pendingRoomName = "";
            }

            isWaitingForRoomOperation = false;
        }

        public void OnDisconnected(DisconnectCause cause)
        {
            LogDebug($"与Photon服务器断开连接: {cause}");

            hasTriggeredConnectedEvent = false;
            isWaitingForRoomOperation = false;
            isPendingClient = false;
            pendingRoomName = "";

            OnPhotonDisconnected?.Invoke();
        }

        public void OnRegionListReceived(RegionHandler regionHandler)
        {
            LogDebug("收到Photon区域列表");
        }

        public void OnCustomAuthenticationResponse(Dictionary<string, object> data)
        {
            LogDebug("收到Photon自定义认证响应");
        }

        public void OnCustomAuthenticationFailed(string debugMessage)
        {
            Debug.LogError($"Photon自定义认证失败: {debugMessage}");
        }

        #endregion

        #region IMatchmakingCallbacks

        public void OnFriendListUpdate(List<FriendInfo> friendList)
        {
            LogDebug("Photon好友列表更新");
        }

        public void OnCreatedRoom()
        {
            LogDebug($"Photon房间创建成功: {PhotonNetwork.CurrentRoom.Name}");

            OnPhotonRoomCreated?.Invoke();
            pendingRoomName = "";
            isWaitingForRoomOperation = false;
        }

        public void OnCreateRoomFailed(short returnCode, string message)
        {
            Debug.LogError($"Photon房间创建失败: {message} (代码: {returnCode})");

            pendingRoomName = "";
            isWaitingForRoomOperation = false;
        }

        public void OnJoinedRoom()
        {
            LogDebug($"成功加入Photon房间: {PhotonNetwork.CurrentRoom.Name}");
            LogDebug($"我的ActorNumber: {PhotonNetwork.LocalPlayer.ActorNumber}");
            LogDebug($"是否为Master Client: {PhotonNetwork.IsMasterClient}");

            OnPhotonRoomJoined?.Invoke();
            LogDebug("触发Photon房间加入成功事件");

            if (PhotonNetwork.IsMasterClient)
            {
                OnPhotonHostStarted?.Invoke();
                LogDebug("触发Host开始事件");
            }

            isPendingClient = false;
            isWaitingForRoomOperation = false;
        }

        public void OnJoinRoomFailed(short returnCode, string message)
        {
            Debug.LogError($"加入Photon房间失败: {message} (代码: {returnCode})");

            isPendingClient = false;
            isWaitingForRoomOperation = false;
        }

        public void OnJoinRandomFailed(short returnCode, string message)
        {
            Debug.LogError($"加入随机Photon房间失败: {message} (代码: {returnCode})");

            isPendingClient = false;
            isWaitingForRoomOperation = false;
        }

        public void OnLeftRoom()
        {
            LogDebug("离开Photon房间");
            OnPhotonRoomLeft?.Invoke();
            OnPhotonHostStopped?.Invoke();
        }

        public void OnJoinedLobby()
        {
            LogDebug("成功加入Photon大厅");
            OnPhotonJoinedLobby?.Invoke();

            if (isWaitingForRoomOperation)
            {
                ProcessPendingRoomOperation();
            }
        }

        public void OnLeftLobby()
        {
            LogDebug("离开Photon大厅");
            cachedRoomList.Clear();
            OnPhotonLeftLobby?.Invoke();
        }

        public void OnRoomListUpdate(List<RoomInfo> roomList)
        {
            LogDebug($"房间列表更新，共 {roomList.Count} 个房间");
            UpdateCachedRoomList(roomList);
            OnPhotonRoomListUpdate?.Invoke(roomList);
        }

        public void OnLobbyStatisticsUpdate(List<TypedLobbyInfo> lobbyStatistics)
        {
            LogDebug($"大厅统计更新，收到 {lobbyStatistics.Count} 个大厅信息");
        }

        #endregion

        #region IInRoomCallbacks

        public void OnPlayerEnteredRoom(Player newPlayer)
        {
            LogDebug($"玩家加入Photon房间: {newPlayer.NickName} (ActorNumber: {newPlayer.ActorNumber})");
            OnPhotonPlayerJoined?.Invoke((ushort)newPlayer.ActorNumber);
        }

        public void OnPlayerLeftRoom(Player otherPlayer)
        {
            LogDebug($"玩家离开Photon房间: {otherPlayer.NickName} (ActorNumber: {otherPlayer.ActorNumber})");
            OnPhotonPlayerLeft?.Invoke((ushort)otherPlayer.ActorNumber);
        }

        public void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
        {
            LogDebug("Photon房间属性更新");
        }

        public void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
        {
            LogDebug($"Photon玩家属性更新: {targetPlayer.NickName}");
        }

        public void OnMasterClientSwitched(Player newMasterClient)
        {
            LogDebug($"Photon Master Client 切换到: {newMasterClient.NickName} (ActorNumber: {newMasterClient.ActorNumber})");

            if (PhotonNetwork.IsMasterClient)
            {
                OnPhotonHostStarted?.Invoke();
            }
            else
            {
                OnPhotonHostStopped?.Invoke();
            }
        }

        #endregion

        #endregion

        #region 辅助方法

        /// <summary>
        /// 设置玩家自定义属性
        /// </summary>
        public void SetPlayerProperties(ExitGames.Client.Photon.Hashtable properties)
        {
            if (!PhotonNetwork.IsConnected)
            {
                LogDebug("未连接到Photon，无法设置玩家属性");
                return;
            }

            if (properties == null || properties.Count == 0)
            {
                LogDebug("玩家属性为空");
                return;
            }

            LogDebug($"设置玩家属性，共 {properties.Count} 个");
            PhotonNetwork.LocalPlayer.SetCustomProperties(properties);
        }

        /// <summary>
        /// 获取玩家自定义属性
        /// </summary>
        public ExitGames.Client.Photon.Hashtable GetPlayerProperties(Player player = null)
        {
            if (player == null)
                player = PhotonNetwork.LocalPlayer;

            if (player == null)
                return new ExitGames.Client.Photon.Hashtable();

            return player.CustomProperties;
        }

        #endregion

        #region 调试方法

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[PhotonNetworkAdapter] {message}");
            }
        }

        [ContextMenu("显示Photon状态")]
        public void ShowPhotonStatus()
        {
            Debug.Log($"=== Photon状态 ===\n{GetPhotonStatus()}");
        }

        #endregion
    }
}