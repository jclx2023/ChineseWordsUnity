using UnityEngine;
using Core.Network;

namespace Core.Network
{
    /// <summary>
    /// Photon测试管理器
    /// 用于测试PhotonNetworkAdapter功能
    /// 演示如何逐步替换NetworkManager功能
    /// </summary>
    public class PhotonTestManager : MonoBehaviour
    {
        [Header("测试配置")]
        [SerializeField] private string testRoomName = "TestRoom";
        [SerializeField] private int maxPlayers = 4;
        [SerializeField] private bool autoTestHost = false;
        [SerializeField] private bool autoTestClient = false;

        [Header("UI测试按钮")]
        [SerializeField] private KeyCode hostKey = KeyCode.H;
        [SerializeField] private KeyCode clientKey = KeyCode.C;
        [SerializeField] private KeyCode leaveKey = KeyCode.L;
        [SerializeField] private KeyCode statusKey = KeyCode.S;

        private void Start()
        {
            // 订阅PhotonNetworkAdapter事件
            SubscribeToPhotonEvents();

            // 自动测试
            if (autoTestHost)
            {
                Invoke(nameof(TestHostMode), 1f);
            }
            else if (autoTestClient)
            {
                Invoke(nameof(TestClientMode), 1f);
            }
        }

        private void Update()
        {
            // 键盘测试
            if (Input.GetKeyDown(hostKey))
            {
                TestHostMode();
            }
            else if (Input.GetKeyDown(clientKey))
            {
                TestClientMode();
            }
            else if (Input.GetKeyDown(leaveKey))
            {
                TestLeaveRoom();
            }
            else if (Input.GetKeyDown(statusKey))
            {
                ShowStatus();
            }
        }

        #region 事件订阅

        /// <summary>
        /// 订阅Photon事件
        /// </summary>
        private void SubscribeToPhotonEvents()
        {
            if (PhotonNetworkAdapter.Instance != null)
            {
                PhotonNetworkAdapter.OnPhotonConnected += OnPhotonConnected;
                PhotonNetworkAdapter.OnPhotonDisconnected += OnPhotonDisconnected;
                PhotonNetworkAdapter.OnPhotonHostStarted += OnPhotonHostStarted;
                PhotonNetworkAdapter.OnPhotonPlayerJoined += OnPhotonPlayerJoined;
                PhotonNetworkAdapter.OnPhotonPlayerLeft += OnPhotonPlayerLeft;
                PhotonNetworkAdapter.OnPhotonRoomJoined += OnPhotonRoomJoined;
                PhotonNetworkAdapter.OnPhotonRoomLeft += OnPhotonRoomLeft;

                Debug.Log("[PhotonTestManager] 已订阅Photon事件");
            }
            else
            {
                Debug.LogError("[PhotonTestManager] PhotonNetworkAdapter.Instance 为空");
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
                PhotonNetworkAdapter.OnPhotonHostStarted -= OnPhotonHostStarted;
                PhotonNetworkAdapter.OnPhotonPlayerJoined -= OnPhotonPlayerJoined;
                PhotonNetworkAdapter.OnPhotonPlayerLeft -= OnPhotonPlayerLeft;
                PhotonNetworkAdapter.OnPhotonRoomJoined -= OnPhotonRoomJoined;
                PhotonNetworkAdapter.OnPhotonRoomLeft -= OnPhotonRoomLeft;

                Debug.Log("[PhotonTestManager] 已取消订阅Photon事件");
            }
        }

        #endregion

        #region 测试方法

        /// <summary>
        /// 测试Host模式
        /// </summary>
        [ContextMenu("测试Host模式")]
        public void TestHostMode()
        {
            Debug.Log("[PhotonTestManager] 测试Host模式");

            if (PhotonNetworkAdapter.Instance != null)
            {
                PhotonNetworkAdapter.Instance.CreatePhotonRoom(testRoomName, maxPlayers);
            }
            else
            {
                Debug.LogError("[PhotonTestManager] PhotonNetworkAdapter.Instance 为空");
            }
        }

        /// <summary>
        /// 测试Client模式
        /// </summary>
        [ContextMenu("测试Client模式")]
        public void TestClientMode()
        {
            Debug.Log("[PhotonTestManager] 测试Client模式");

            if (PhotonNetworkAdapter.Instance != null)
            {
                PhotonNetworkAdapter.Instance.JoinPhotonRoom();
            }
            else
            {
                Debug.LogError("[PhotonTestManager] PhotonNetworkAdapter.Instance 为空");
            }
        }

        /// <summary>
        /// 测试离开房间
        /// </summary>
        [ContextMenu("测试离开房间")]
        public void TestLeaveRoom()
        {
            Debug.Log("[PhotonTestManager] 测试离开房间");

            if (PhotonNetworkAdapter.Instance != null)
            {
                PhotonNetworkAdapter.Instance.LeavePhotonRoom();
            }
            else
            {
                Debug.LogError("[PhotonTestManager] PhotonNetworkAdapter.Instance 为空");
            }
        }

        /// <summary>
        /// 显示状态
        /// </summary>
        [ContextMenu("显示状态")]
        public void ShowStatus()
        {
            if (PhotonNetworkAdapter.Instance != null)
            {
                string status = PhotonNetworkAdapter.Instance.GetPhotonStatus();
                Debug.Log($"[PhotonTestManager] Photon状态:\n{status}");
            }
            else
            {
                Debug.LogError("[PhotonTestManager] PhotonNetworkAdapter.Instance 为空");
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// Photon连接事件
        /// </summary>
        private void OnPhotonConnected()
        {
            Debug.Log("[PhotonTestManager] ✅ Photon连接成功!");
        }

        /// <summary>
        /// Photon断开事件
        /// </summary>
        private void OnPhotonDisconnected()
        {
            Debug.Log("[PhotonTestManager] ❌ Photon连接断开");
        }

        /// <summary>
        /// Photon Host启动事件
        /// </summary>
        private void OnPhotonHostStarted()
        {
            Debug.Log("[PhotonTestManager] 🎯 成为Photon Host!");

            if (PhotonNetworkAdapter.Instance != null)
            {
                Debug.Log($"[PhotonTestManager] Host信息: 房间={PhotonNetworkAdapter.Instance.CurrentRoomName}, " +
                         $"玩家={PhotonNetworkAdapter.Instance.CurrentRoomPlayerCount}/{PhotonNetworkAdapter.Instance.CurrentRoomMaxPlayers}");
            }
        }

        /// <summary>
        /// Photon玩家加入事件
        /// </summary>
        private void OnPhotonPlayerJoined(ushort playerId)
        {
            Debug.Log($"[PhotonTestManager] 👤 玩家加入: ID={playerId}");

            if (PhotonNetworkAdapter.Instance != null)
            {
                Debug.Log($"[PhotonTestManager] 当前房间玩家数: {PhotonNetworkAdapter.Instance.CurrentRoomPlayerCount}");
            }
        }

        /// <summary>
        /// Photon玩家离开事件
        /// </summary>
        private void OnPhotonPlayerLeft(ushort playerId)
        {
            Debug.Log($"[PhotonTestManager] 👤 玩家离开: ID={playerId}");

            if (PhotonNetworkAdapter.Instance != null)
            {
                Debug.Log($"[PhotonTestManager] 当前房间玩家数: {PhotonNetworkAdapter.Instance.CurrentRoomPlayerCount}");
            }
        }

        /// <summary>
        /// Photon房间加入事件
        /// </summary>
        private void OnPhotonRoomJoined()
        {
            Debug.Log("[PhotonTestManager] 🏠 成功加入房间!");

            if (PhotonNetworkAdapter.Instance != null)
            {
                Debug.Log($"[PhotonTestManager] 房间信息: {PhotonNetworkAdapter.Instance.CurrentRoomName} " +
                         $"({PhotonNetworkAdapter.Instance.CurrentRoomPlayerCount}/{PhotonNetworkAdapter.Instance.CurrentRoomMaxPlayers})");
                Debug.Log($"[PhotonTestManager] 我的ID: {PhotonNetworkAdapter.Instance.PhotonClientId}");
                Debug.Log($"[PhotonTestManager] 是否为Master Client: {PhotonNetworkAdapter.Instance.IsPhotonMasterClient}");
            }
        }

        /// <summary>
        /// Photon房间离开事件
        /// </summary>
        private void OnPhotonRoomLeft()
        {
            Debug.Log("[PhotonTestManager] 🚪 离开房间");
        }

        #endregion

        #region Unity事件

        private void OnDestroy()
        {
            UnsubscribeFromPhotonEvents();
        }

        private void OnGUI()
        {
            // 简单的GUI测试界面
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label("Photon测试控制面板", new GUIStyle(GUI.skin.label) { fontSize = 16 });

            GUILayout.Space(10);

            if (GUILayout.Button($"创建房间 (Host) - 按 {hostKey}"))
            {
                TestHostMode();
            }

            if (GUILayout.Button($"加入房间 (Client) - 按 {clientKey}"))
            {
                TestClientMode();
            }

            if (GUILayout.Button($"离开房间 - 按 {leaveKey}"))
            {
                TestLeaveRoom();
            }

            if (GUILayout.Button($"显示状态 - 按 {statusKey}"))
            {
                ShowStatus();
            }

            GUILayout.Space(10);

            // 状态显示
            if (PhotonNetworkAdapter.Instance != null)
            {
                GUILayout.Label($"连接状态: {PhotonNetworkAdapter.Instance.IsPhotonConnected}");
                GUILayout.Label($"房间状态: {PhotonNetworkAdapter.Instance.IsInPhotonRoom}");
                if (PhotonNetworkAdapter.Instance.IsInPhotonRoom)
                {
                    GUILayout.Label($"房间: {PhotonNetworkAdapter.Instance.CurrentRoomName}");
                    GUILayout.Label($"玩家: {PhotonNetworkAdapter.Instance.CurrentRoomPlayerCount}/{PhotonNetworkAdapter.Instance.CurrentRoomMaxPlayers}");
                    GUILayout.Label($"Master Client: {PhotonNetworkAdapter.Instance.IsPhotonMasterClient}");
                }
            }

            GUILayout.EndArea();
        }

        #endregion
    }
}