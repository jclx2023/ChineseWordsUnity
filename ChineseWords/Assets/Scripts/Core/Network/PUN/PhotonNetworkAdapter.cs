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
    public class PhotonNetworkAdapter : MonoBehaviourPun, IConnectionCallbacks, IMatchmakingCallbacks, IInRoomCallbacks
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

        // 适配器状态
        private bool isInitialized = false;
        private string pendingRoomName = "";
        private int pendingMaxPlayers = 4;
        private bool isPendingClient = false;

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

        #region NetworkManager桥接方法

        /// <summary>
        /// 桥接到NetworkManager事件（可选）
        /// 如果需要让PhotonNetworkAdapter的事件触发NetworkManager的事件
        /// </summary>
        public void EnableNetworkManagerBridge()
        {
            LogDebug("启用NetworkManager事件桥接");

            OnPhotonConnected += () => {
                LogDebug("桥接: Photon连接 -> NetworkManager.OnConnected");
                // 这里可以触发NetworkManager的事件
                // NetworkManager.OnConnected?.Invoke();
            };

            OnPhotonDisconnected += () => {
                LogDebug("桥接: Photon断开 -> NetworkManager.OnDisconnected");
                // 这里可以触发NetworkManager的事件
                // NetworkManager.OnDisconnected?.Invoke();
            };

            OnPhotonPlayerJoined += (playerId) => {
                LogDebug($"桥接: Photon玩家加入 -> NetworkManager.OnPlayerJoined ({playerId})");
                // 这里可以触发NetworkManager的事件
                // NetworkManager.OnPlayerJoined?.Invoke(playerId);
            };

            OnPhotonPlayerLeft += (playerId) => {
                LogDebug($"桥接: Photon玩家离开 -> NetworkManager.OnPlayerLeft ({playerId})");
                // 这里可以触发NetworkManager的事件
                // NetworkManager.OnPlayerLeft?.Invoke(playerId);
            };
        }

        /// <summary>
        /// 禁用NetworkManager事件桥接
        /// </summary>
        public void DisableNetworkManagerBridge()
        {
            LogDebug("禁用NetworkManager事件桥接");
            // 可以在这里移除事件订阅
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

        #endregion
    }
}