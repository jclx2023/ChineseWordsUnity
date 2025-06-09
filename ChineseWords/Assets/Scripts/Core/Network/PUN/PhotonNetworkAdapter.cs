using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Core.Network;

namespace Core.Network
{
    /// <summary>
    /// Photon网络适配器
    /// 作为NetworkManager的补充，处理Photon特定功能
    /// 渐进式迁移策略：先并存，后整合
    /// </summary>
    public class PhotonNetworkAdapter : MonoBehaviourPun, IConnectionCallbacks, IMatchmakingCallbacks, ILobbyCallbacks
    {
        [Header("Photon配置")]
        [SerializeField] private string gameVersion = "1.0";
        [SerializeField] private bool enableDebugLogs = true;

        public static PhotonNetworkAdapter Instance { get; private set; }

        #region 适配器专用事件（避免与NetworkManager冲突）

        // 使用不同的事件名称避免冲突
        public static event System.Action OnPhotonConnected;
        public static event System.Action OnPhotonDisconnected;
        public static event System.Action OnPhotonHostStarted;
        public static event System.Action OnPhotonHostStopped;
        public static event System.Action<ushort> OnPhotonPlayerJoined;
        public static event System.Action<ushort> OnPhotonPlayerLeft;
        public static event System.Action OnPhotonRoomJoined;
        public static event System.Action OnPhotonRoomLeft;
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

        private List<RoomInfo> cachedRoomList = new List<RoomInfo>();

        #endregion

        #region Unity生命周期

        private void Awake()
        {
            // 单例模式
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializePhoton();
                PhotonNetwork.AddCallbackTarget(this);
                LogDebug("PhotonNetworkAdapter 单例已创建");
            }
            else
            {
                LogDebug("销毁重复的PhotonNetworkAdapter实例");
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            CleanupPhoton();
            PhotonNetwork.RemoveCallbackTarget(this);
            if (Instance == this)
            {
                Instance = null;
            }
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
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Photon适配器初始化失败: {e.Message}");
            }
        }

        /// <summary>
        /// 清理Photon资源
        /// </summary>
        private void CleanupPhoton()
        {
            if (PhotonNetwork.IsConnected)
            {
                PhotonNetwork.Disconnect();
            }
            LogDebug("Photon适配器资源已清理");
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

            if (!PhotonNetwork.IsConnected)
            {
                LogDebug("连接到Photon服务器...");
                PhotonNetwork.ConnectUsingSettings();
            }
            else
            {
                LogDebug("已连接，直接创建房间");
                CreateRoom();
            }
        }

        /// <summary>
        /// 连接到Photon并加入房间（Client模式）
        /// </summary>
        public void JoinPhotonRoom()
        {
            LogDebug("加入Photon房间");

            isPendingClient = true;

            if (!PhotonNetwork.IsConnected)
            {
                LogDebug("连接到Photon服务器...");
                PhotonNetwork.ConnectUsingSettings();
            }
            else
            {
                LogDebug("已连接，直接加入房间");
                JoinRandomRoom();
            }
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

            // 返回缓存的房间列表副本
            return new List<RoomInfo>(cachedRoomList);
        }
        public int GetPhotonRoomCount()
        {
            return cachedRoomList.Count;
        }
        public RoomInfo FindRoomByName(string roomName)
        {
            if (string.IsNullOrEmpty(roomName))
                return null;

            foreach (var room in cachedRoomList)
            {
                if (room.Name == roomName)
                    return room;
            }

            return null;
        }
        public void ClearRoomListCache()
        {
            cachedRoomList.Clear();
            LogDebug("房间列表缓存已清空");
        }
        /// <summary>
        /// 更新缓存的房间列表
        /// </summary>
        private void UpdateCachedRoomList(List<RoomInfo> roomUpdates)
        {
            foreach (var roomUpdate in roomUpdates)
            {
                // 如果房间被移除或关闭，从缓存中删除
                if (roomUpdate.RemovedFromList || !roomUpdate.IsOpen)
                {
                    RemoveRoomFromCache(roomUpdate.Name);
                    LogDebug($"从缓存中移除房间: {roomUpdate.Name}");
                }
                else
                {
                    // 更新或添加房间到缓存
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
            // 查找现有房间
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

            // 如果没找到，添加新房间
            if (!found)
            {
                cachedRoomList.Add(roomUpdate);
            }
        }
        #endregion

        #region 房间查询方法

        /// <summary>
        /// 获取可加入的房间列表
        /// </summary>
        public List<RoomInfo> GetJoinableRooms()
        {
            var joinableRooms = new List<RoomInfo>();

            foreach (var room in cachedRoomList)
            {
                if (room.IsOpen && room.IsVisible && room.PlayerCount < room.MaxPlayers)
                {
                    joinableRooms.Add(room);
                }
            }

            return joinableRooms;
        }

        /// <summary>
        /// 获取有密码的房间数量
        /// </summary>
        public int GetPasswordProtectedRoomCount()
        {
            int count = 0;
            foreach (var room in cachedRoomList)
            {
                if (room.CustomProperties.TryGetValue("hasPassword", out object hasPassword) && (bool)hasPassword)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 按游戏模式筛选房间
        /// </summary>
        public List<RoomInfo> GetRoomsByGameMode(string gameMode)
        {
            var filteredRooms = new List<RoomInfo>();

            foreach (var room in cachedRoomList)
            {
                if (room.CustomProperties.TryGetValue("gameMode", out object roomGameMode) &&
                    roomGameMode.ToString() == gameMode)
                {
                    filteredRooms.Add(room);
                }
            }

            return filteredRooms;
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
                    { "version", gameVersion }
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
            PhotonNetwork.JoinLobby();
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
        }

        public void OnDisconnected(DisconnectCause cause)
        {
            LogDebug($"与Photon服务器断开连接: {cause}");
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
        }

        public void OnCreateRoomFailed(short returnCode, string message)
        {
            Debug.LogError($"Photon房间创建失败: {message} (代码: {returnCode})");
        }

        public void OnJoinedRoom()
        {
            LogDebug($"成功加入Photon房间: {PhotonNetwork.CurrentRoom.Name}");
            LogDebug($"我的ActorNumber: {PhotonNetwork.LocalPlayer.ActorNumber}");
            LogDebug($"是否为Master Client: {PhotonNetwork.IsMasterClient}");

            OnPhotonRoomJoined?.Invoke();

            if (PhotonNetwork.IsMasterClient)
            {
                OnPhotonHostStarted?.Invoke();
            }

            OnPhotonConnected?.Invoke();
        }

        public void OnJoinRoomFailed(short returnCode, string message)
        {
            Debug.LogError($"加入Photon房间失败: {message} (代码: {returnCode})");
        }

        public void OnJoinRandomFailed(short returnCode, string message)
        {
            Debug.LogError($"加入随机Photon房间失败: {message} (代码: {returnCode})");
        }

        public void OnLeftRoom()
        {
            LogDebug("离开Photon房间");
            OnPhotonRoomLeft?.Invoke();
            OnPhotonDisconnected?.Invoke();
        }

        public void OnJoinedLobby()
        {
            LogDebug("成功加入Photon大厅");
            OnPhotonJoinedLobby?.Invoke();
        }

        public void OnLeftLobby()
        {
            LogDebug("离开Photon大厅");

            cachedRoomList.Clear();
            OnPhotonLeftLobby?.Invoke();
            // 如果是为了刷新而离开，自动重新加入
            if (shouldRejoinLobby)
            {
                shouldRejoinLobby = false;
                Invoke(nameof(JoinPhotonLobby), 0.5f); // 延迟0.5秒后重新加入
            }
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

            foreach (var lobby in lobbyStatistics)
            {
                LogDebug($"大厅: {lobby.Name} (类型: {lobby.Type}) - 房间数: {lobby.RoomCount}, 玩家数: {lobby.PlayerCount}");
            }

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
        }

        #endregion

        #endregion

        #region 调试方法

        /// <summary>
        /// 调试日志
        /// </summary>
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

        [ContextMenu("测试创建房间")]
        public void TestCreateRoom()
        {
            CreatePhotonRoom("TestRoom", 4);
        }

        [ContextMenu("测试加入房间")]
        public void TestJoinRoom()
        {
            JoinPhotonRoom();
        }

        [ContextMenu("离开房间")]
        public void TestLeaveRoom()
        {
            LeavePhotonRoom();
        }
        [ContextMenu("显示房间列表调试信息")]
        public void ShowRoomListDebugInfo()
        {
            Debug.Log(GetRoomListDebugInfo());
        }
        /// <summary>
        /// 获取房间列表的详细调试信息
        /// </summary>
        public string GetRoomListDebugInfo()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine($"=== Photon房间列表调试信息 ===");
            info.AppendLine($"大厅状态: {(PhotonNetwork.InLobby ? "已连接" : "未连接")}");
            info.AppendLine($"缓存房间数: {cachedRoomList.Count}");
            info.AppendLine($"Photon统计房间数: {PhotonNetwork.CountOfRooms}");
            info.AppendLine($"可加入房间数: {GetJoinableRooms().Count}");

            if (cachedRoomList.Count > 0)
            {
                info.AppendLine("\n房间详情:");
                for (int i = 0; i < cachedRoomList.Count; i++)
                {
                    var room = cachedRoomList[i];
                    info.AppendLine($"  {i + 1}. {room.Name} ({room.PlayerCount}/{room.MaxPlayers}) " +
                                  $"开放:{room.IsOpen} 可见:{room.IsVisible}");
                }
            }

            return info.ToString();
        }

        #endregion

        #region Lobby支持方法

        private bool shouldRejoinLobby = false;

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

            if (!PhotonNetwork.IsConnected)
            {
                LogDebug("未连接到Photon，无法加入房间");
                return;
            }

            LogDebug($"按名称加入房间: {roomName}");
            PhotonNetwork.JoinRoom(roomName);
        }

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
        public ExitGames.Client.Photon.Hashtable GetPlayerProperties(Photon.Realtime.Player player = null)
        {
            if (player == null)
                player = PhotonNetwork.LocalPlayer;

            if (player == null)
                return new ExitGames.Client.Photon.Hashtable();

            return player.CustomProperties;
        }

        /// <summary>
        /// 设置房间自定义属性（仅Master Client可用）
        /// </summary>
        public void SetRoomProperties(ExitGames.Client.Photon.Hashtable properties)
        {
            if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom)
            {
                LogDebug("未连接或未在房间中，无法设置房间属性");
                return;
            }

            if (!PhotonNetwork.IsMasterClient)
            {
                LogDebug("不是Master Client，无法设置房间属性");
                return;
            }

            if (properties == null || properties.Count == 0)
            {
                LogDebug("房间属性为空");
                return;
            }

            LogDebug($"设置房间属性，共 {properties.Count} 个");
            PhotonNetwork.CurrentRoom.SetCustomProperties(properties);
        }

        /// <summary>
        /// 获取房间自定义属性
        /// </summary>
        public ExitGames.Client.Photon.Hashtable GetRoomProperties()
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
                return new ExitGames.Client.Photon.Hashtable();

            return PhotonNetwork.CurrentRoom.CustomProperties;
        }

        /// <summary>
        /// 获取所有房间中的玩家信息
        /// </summary>
        public List<Photon.Realtime.Player> GetRoomPlayers()
        {
            var players = new List<Photon.Realtime.Player>();

            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
                return players;

            foreach (var player in PhotonNetwork.CurrentRoom.Players.Values)
            {
                players.Add(player);
            }

            return players;
        }

        /// <summary>
        /// 踢出玩家（仅Master Client可用）
        /// </summary>
        public void KickPlayer(Photon.Realtime.Player player)
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                LogDebug("不是Master Client，无法踢出玩家");
                return;
            }

            if (player == null)
            {
                LogDebug("玩家为空，无法踢出");
                return;
            }

            LogDebug($"踢出玩家: {player.NickName}");
            PhotonNetwork.CloseConnection(player);
        }

        /// <summary>
        /// 转移Master Client权限
        /// </summary>
        public void TransferMasterClient(Photon.Realtime.Player newMaster)
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                LogDebug("不是Master Client，无法转移权限");
                return;
            }

            if (newMaster == null)
            {
                LogDebug("新Master Client为空");
                return;
            }

            LogDebug($"转移Master Client权限给: {newMaster.NickName}");
            PhotonNetwork.SetMasterClient(newMaster);
        }

        /// <summary>
        /// 获取详细的Photon状态
        /// </summary>
        public string GetDetailedPhotonStatus()
        {
            string status = GetPhotonStatus() + "\n";

            status += "=== 详细状态 ===\n";
            status += $"网络状态: {PhotonNetwork.NetworkClientState}\n";
            status += $"服务器时间: {PhotonNetwork.ServerTimestamp}\n";
            status += $"延迟: {PhotonNetwork.GetPing()}ms\n";

            if (PhotonNetwork.InLobby)
            {
                status += $"大厅中房间数: {PhotonNetwork.CountOfRooms}\n";
                status += $"大厅中玩家数: {PhotonNetwork.CountOfPlayers}\n";
            }

            if (PhotonNetwork.InRoom)
            {
                status += $"房间玩家列表:\n";
                foreach (var player in PhotonNetwork.CurrentRoom.Players.Values)
                {
                    status += $"  - {player.NickName} (ID: {player.ActorNumber})\n";
                }
            }

            return status;
        }

        #endregion
    }
}