using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Core.Network;
using Lobby.Data;

namespace Lobby.Core
{
    /// <summary>
    /// Lobby网络管理器
    /// 专门处理Lobby相关的Photon网络操作
    /// </summary>
    public class LobbyNetworkManager : MonoBehaviour
    {
        [Header("网络配置")]
        [SerializeField] private bool autoConnectOnStart = true;
        [SerializeField] private float roomListRefreshInterval = 5f;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        public static LobbyNetworkManager Instance { get; private set; }

        // 网络状态
        private bool isConnected = false;
        private bool isConnecting = false;

        // 房间数据
        private List<LobbyRoomData> cachedRoomList = new List<LobbyRoomData>();

        // 事件
        public System.Action<bool> OnConnectionStatusChanged;
        public System.Action<List<LobbyRoomData>> OnRoomListUpdated;
        public System.Action<string, bool> OnRoomCreated; // roomName, success
        public System.Action<string, bool> OnRoomJoined; // roomName, success

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

            // 如果已经连接，直接成功
            if (PhotonNetworkAdapter.Instance.IsPhotonConnected)
            {
                LogDebug("已连接到Photon");
                OnPhotonConnected();
                yield break;
            }

            // 尝试连接（这里暂时使用模拟连接，实际项目中调用PhotonNetworkAdapter）
            LogDebug("模拟连接到Photon服务器...");
            yield return new WaitForSeconds(2f); // 模拟连接延迟

            // 模拟连接成功
            OnPhotonConnected();
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
                PhotonNetworkAdapter.OnPhotonRoomJoined -= OnPhotonRoomJoined;
                PhotonNetworkAdapter.OnPhotonRoomLeft -= OnPhotonRoomLeft;

                LogDebug("已取消订阅Photon事件");
            }
        }

        #endregion

        #region 房间管理

        /// <summary>
        /// 创建房间
        /// </summary>
        public void CreateRoom(string roomName, int maxPlayers, string password = "")
        {
            if (!isConnected)
            {
                LogDebug("未连接到网络，无法创建房间");
                OnRoomCreated?.Invoke(roomName, false);
                return;
            }

            if (string.IsNullOrEmpty(roomName) || maxPlayers < 2)
            {
                LogDebug("无效的房间参数");
                OnRoomCreated?.Invoke(roomName, false);
                return;
            }

            LogDebug($"创建房间: {roomName}, 最大人数: {maxPlayers}");

            // 阶段1：暂时模拟房间创建
            StartCoroutine(SimulateCreateRoom(roomName, maxPlayers, password));
        }

        /// <summary>
        /// 加入房间
        /// </summary>
        public void JoinRoom(LobbyRoomData roomData)
        {
            if (!isConnected)
            {
                LogDebug("未连接到网络，无法加入房间");
                OnRoomJoined?.Invoke(roomData.roomName, false);
                return;
            }

            LogDebug($"加入房间: {roomData.roomName}");

            // 阶段1：暂时模拟房间加入
            StartCoroutine(SimulateJoinRoom(roomData));
        }

        /// <summary>
        /// 加入随机房间
        /// </summary>
        public void JoinRandomRoom()
        {
            if (!isConnected)
            {
                LogDebug("未连接到网络，无法加入随机房间");
                return;
            }

            if (cachedRoomList.Count == 0)
            {
                LogDebug("没有可用的房间");
                return;
            }

            // 选择一个有空位的房间
            foreach (var room in cachedRoomList)
            {
                if (room.currentPlayers < room.maxPlayers)
                {
                    JoinRoom(room);
                    return;
                }
            }

            LogDebug("没有可加入的房间");
        }

        /// <summary>
        /// 刷新房间列表
        /// </summary>
        public void RefreshRoomList()
        {
            if (!isConnected)
            {
                LogDebug("未连接到网络，无法刷新房间列表");
                return;
            }

            LogDebug("刷新房间列表");
            StartCoroutine(SimulateGetRoomList());
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            LogDebug("断开网络连接");

            if (PhotonNetworkAdapter.Instance != null)
            {
                PhotonNetworkAdapter.Instance.DisconnectPhoton();
            }

            OnPhotonDisconnected();
        }

        #endregion

        #region 模拟网络操作（阶段1使用）

        /// <summary>
        /// 模拟创建房间
        /// </summary>
        private IEnumerator SimulateCreateRoom(string roomName, int maxPlayers, string password)
        {
            LogDebug("模拟创建房间...");
            yield return new WaitForSeconds(1f);

            // 创建新房间数据
            var newRoom = new LobbyRoomData
            {
                roomName = roomName,
                maxPlayers = maxPlayers,
                currentPlayers = 1,
                hasPassword = !string.IsNullOrEmpty(password),
                hostPlayerName = LobbySceneManager.Instance.GetCurrentPlayerData().playerName
            };

            cachedRoomList.Add(newRoom);
            OnRoomCreated?.Invoke(roomName, true);
            OnRoomListUpdated?.Invoke(new List<LobbyRoomData>(cachedRoomList));

            LogDebug($"房间创建成功: {roomName}");
        }

        /// <summary>
        /// 模拟加入房间
        /// </summary>
        private IEnumerator SimulateJoinRoom(LobbyRoomData roomData)
        {
            LogDebug("模拟加入房间...");
            yield return new WaitForSeconds(1f);

            OnRoomJoined?.Invoke(roomData.roomName, true);
            LogDebug($"房间加入成功: {roomData.roomName}");
        }

        /// <summary>
        /// 模拟获取房间列表
        /// </summary>
        private IEnumerator SimulateGetRoomList()
        {
            LogDebug("模拟获取房间列表...");
            yield return new WaitForSeconds(1f);

            // 生成一些模拟房间数据
            if (cachedRoomList.Count == 0)
            {
                cachedRoomList.Add(new LobbyRoomData
                {
                    roomName = "测试房间1",
                    maxPlayers = 4,
                    currentPlayers = 2,
                    hasPassword = false,
                    hostPlayerName = "主机玩家1"
                });

                cachedRoomList.Add(new LobbyRoomData
                {
                    roomName = "私人房间",
                    maxPlayers = 2,
                    currentPlayers = 1,
                    hasPassword = true,
                    hostPlayerName = "主机玩家2"
                });
            }

            OnRoomListUpdated?.Invoke(new List<LobbyRoomData>(cachedRoomList));
            LogDebug($"房间列表更新完成，共 {cachedRoomList.Count} 个房间");
        }

        #endregion

        #region Photon事件处理

        private void OnPhotonConnected()
        {
            LogDebug("Photon连接成功");
            isConnecting = false;
            isConnected = true;
            OnConnectionStatusChanged?.Invoke(true);

            // 连接成功后自动刷新房间列表
            RefreshRoomList();
        }

        private void OnPhotonDisconnected()
        {
            LogDebug("Photon连接断开");
            isConnecting = false;
            isConnected = false;
            OnConnectionStatusChanged?.Invoke(false);

            // 清空房间列表
            cachedRoomList.Clear();
            OnRoomListUpdated?.Invoke(new List<LobbyRoomData>());
        }

        private void OnPhotonRoomJoined()
        {
            LogDebug("成功加入Photon房间");
        }

        private void OnPhotonRoomLeft()
        {
            LogDebug("离开Photon房间");
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
        /// 获取缓存的房间列表
        /// </summary>
        public List<LobbyRoomData> GetCachedRoomList()
        {
            return new List<LobbyRoomData>(cachedRoomList);
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
            string status = "=== Lobby网络状态 ===\n";
            status += $"已连接: {isConnected}\n";
            status += $"连接中: {isConnecting}\n";
            status += $"房间数量: {cachedRoomList.Count}";

            LogDebug(status);
        }

        [ContextMenu("强制刷新房间列表")]
        public void ForceRefreshRoomList()
        {
            RefreshRoomList();
        }

        #endregion
    }
}