using UnityEngine;
using System;
using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using UnityEngine.SceneManagement;
using Core.Network;

namespace Core.Network
{
    /// <summary>
    /// 房间管理器 - 简化解耦版
    /// 职责：管理房间状态、玩家准备控制、游戏开始控制
    /// 通过NetworkManager与Photon交互，不直接依赖Photon API
    /// </summary>
    public class RoomManager : MonoBehaviourPun
    {
        [Header("游戏开始控制")]
        [SerializeField] private float gameStartDelay = 2f;
        [SerializeField] private int minPlayersToStart = 2;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        public static RoomManager Instance { get; private set; }

        // 房间属性键名常量
        private const string ROOM_STATE_KEY = "roomState";
        private const string GAME_STARTED_KEY = "gameStarted";

        // 事件系统
        public static event Action OnRoomEntered;
        public static event Action<Player> OnPlayerJoinedRoom;
        public static event Action<Player> OnPlayerLeftRoom;
        public static event Action<Player, bool> OnPlayerReadyChanged;
        public static event Action OnAllPlayersReady;
        public static event Action OnGameStarting;
        public static event Action OnReturnToLobby;

        // 通过NetworkManager访问的属性
        public bool IsHost => NetworkManager.Instance?.IsHost ?? false;
        public bool IsInRoom => NetworkManager.Instance?.IsConnected ?? false;
        public string RoomName => NetworkManager.Instance?.RoomName ?? "";
        public int PlayerCount => NetworkManager.Instance?.PlayerCount ?? 0;
        public int MaxPlayers => NetworkManager.Instance?.MaxPlayers ?? 0;
        public bool IsInitialized => isInitialized;

        // 内部状态
        private bool isInitialized = false;
        private bool hasGameStarted = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                LogDebug("RoomManager 初始化（解耦版）");
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            StartCoroutine(InitializeRoomManager());
        }

        private void OnDestroy()
        {
            UnsubscribeFromNetworkEvents();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #region 初始化

        /// <summary>
        /// 初始化房间管理器
        /// </summary>
        private IEnumerator InitializeRoomManager()
        {
            // 等待NetworkManager准备就绪
            while (NetworkManager.Instance == null)
            {
                LogDebug("等待NetworkManager初始化...");
                yield return new WaitForSeconds(0.1f);
            }

            // 等待确保在房间中
            while (!IsInRoom)
            {
                LogDebug("等待进入房间...");
                yield return new WaitForSeconds(0.1f);
            }

            // 订阅NetworkManager事件
            SubscribeToNetworkEvents();

            // 验证房间状态
            if (!ValidateRoomState())
            {
                Debug.LogError("[RoomManager] 房间状态验证失败");
                yield break;
            }

            // 检查并处理游戏结束后的状态
            HandlePostGameState();

            // 如果是房主，确保房间处于等待状态
            if (IsHost && !hasGameStarted)
            {
                SetRoomWaitingState();
            }

            isInitialized = true;
            LogDebug($"RoomManager初始化完成 - 房间: {RoomName}, 玩家数: {PlayerCount}, 是否房主: {IsHost}");

            OnRoomEntered?.Invoke();
        }

        /// <summary>
        /// 处理游戏结束后的状态
        /// </summary>
        private void HandlePostGameState()
        {
            bool gameEnded = NetworkManager.Instance.GetRoomProperty("gameEnded", false);
            hasGameStarted = NetworkManager.Instance.GetRoomProperty(GAME_STARTED_KEY, false);

            if (gameEnded && IsHost)
            {
                LogDebug("检测到游戏已结束，房主将重置房间状态");
                // 通过NetworkManager重置状态，这会触发相应的事件
                NetworkManager.Instance.ResetRoomStateAfterGame();
                hasGameStarted = false;
            }
        }

        /// <summary>
        /// 验证房间状态
        /// </summary>
        private bool ValidateRoomState()
        {
            if (!IsInRoom)
            {
                Debug.LogError("[RoomManager] 不在房间中");
                return false;
            }

            if (PlayerCount == 0)
            {
                Debug.LogError("[RoomManager] 房间中没有玩家");
                return false;
            }

            LogDebug("房间状态验证通过");
            return true;
        }

        #endregion

        #region 事件管理

        /// <summary>
        /// 订阅NetworkManager事件
        /// </summary>
        private void SubscribeToNetworkEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerJoined += OnNetworkPlayerJoined;
                NetworkManager.OnPlayerLeft += OnNetworkPlayerLeft;
                NetworkManager.OnPlayerReadyChanged += OnNetworkPlayerReadyChanged;
                NetworkManager.OnAllPlayersReady += OnNetworkAllPlayersReady;
                NetworkManager.OnRoomStateReset += OnNetworkRoomStateReset;
                LogDebug("已订阅NetworkManager事件");
            }
        }

        /// <summary>
        /// 取消订阅NetworkManager事件
        /// </summary>
        private void UnsubscribeFromNetworkEvents()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.OnPlayerJoined -= OnNetworkPlayerJoined;
                NetworkManager.OnPlayerLeft -= OnNetworkPlayerLeft;
                NetworkManager.OnPlayerReadyChanged -= OnNetworkPlayerReadyChanged;
                NetworkManager.OnAllPlayersReady -= OnNetworkAllPlayersReady;
                NetworkManager.OnRoomStateReset -= OnNetworkRoomStateReset;
                LogDebug("已取消订阅NetworkManager事件");
            }
        }

        // NetworkManager事件处理
        private void OnNetworkPlayerJoined(ushort playerId)
        {
            var player = GetPhotonPlayerById(playerId);
            if (player != null)
            {
                LogDebug($"玩家加入房间: {player.NickName} (ID: {playerId})");
                OnPlayerJoinedRoom?.Invoke(player);
            }
        }

        private void OnNetworkPlayerLeft(ushort playerId)
        {
            LogDebug($"玩家离开房间: ID {playerId}");
            // 创建一个临时的Player对象用于事件通知
            // 注意：此时玩家已经离开，无法获取完整信息
            OnPlayerLeftRoom?.Invoke(null);
        }

        private void OnNetworkPlayerReadyChanged(ushort playerId, bool isReady)
        {
            var player = GetPhotonPlayerById(playerId);
            if (player != null)
            {
                LogDebug($"玩家准备状态变化: {player.NickName} -> {isReady}");
                OnPlayerReadyChanged?.Invoke(player, isReady);
            }
        }

        private void OnNetworkAllPlayersReady()
        {
            LogDebug("所有玩家都已准备就绪");
            OnAllPlayersReady?.Invoke();
        }

        private void OnNetworkRoomStateReset()
        {
            LogDebug("收到房间状态重置通知");
            hasGameStarted = false;
        }

        #endregion

        #region 玩家准备状态管理

        /// <summary>
        /// 设置本地玩家准备状态
        /// </summary>
        public bool SetPlayerReady(bool ready)
        {
            if (!IsInRoom || !isInitialized)
            {
                LogDebug("房间未初始化，无法设置准备状态");
                return false;
            }

            if (IsHost)
            {
                LogDebug("房主无需设置准备状态");
                return false;
            }

            if (hasGameStarted)
            {
                LogDebug("游戏已开始，无法更改准备状态");
                return false;
            }

            LogDebug($"设置准备状态: {ready}");
            NetworkManager.Instance?.SetPlayerReady(ready);
            return true;
        }

        /// <summary>
        /// 获取指定玩家的准备状态
        /// </summary>
        public bool GetPlayerReady(Player player)
        {
            if (player == null || NetworkManager.Instance == null) return false;
            return NetworkManager.Instance.GetPlayerReady((ushort)player.ActorNumber);
        }

        /// <summary>
        /// 获取本地玩家的准备状态
        /// </summary>
        public bool GetMyReadyState()
        {
            return NetworkManager.Instance?.GetMyReadyState() ?? false;
        }

        /// <summary>
        /// 获取准备就绪的玩家数量（不包括房主）
        /// </summary>
        public int GetReadyPlayerCount()
        {
            return NetworkManager.Instance?.GetReadyPlayerCount() ?? 0;
        }

        /// <summary>
        /// 获取非房主玩家数量
        /// </summary>
        public int GetNonHostPlayerCount()
        {
            return NetworkManager.Instance?.GetNonHostPlayerCount() ?? 0;
        }

        /// <summary>
        /// 检查所有非房主玩家是否都已准备
        /// </summary>
        public bool AreAllPlayersReady()
        {
            return NetworkManager.Instance?.AreAllPlayersReady() ?? false;
        }

        #endregion

        #region 游戏开始控制

        /// <summary>
        /// 启动游戏 - 房主专用
        /// </summary>
        public void StartGame()
        {
            if (!IsHost)
            {
                Debug.LogError("[RoomManager] 只有房主可以启动游戏");
                return;
            }

            if (!CanStartGame())
            {
                Debug.LogError("[RoomManager] 游戏启动条件不满足");
                LogDebug($"启动条件检查: {GetGameStartConditions()}");
                return;
            }

            LogDebug("房主启动游戏");

            // 防止重复启动
            hasGameStarted = true;

            // 设置房间属性
            NetworkManager.Instance?.SetRoomProperty(ROOM_STATE_KEY, (int)RoomState.Starting);
            NetworkManager.Instance?.SetRoomProperty(GAME_STARTED_KEY, true);

            // 延迟触发游戏开始事件
            StartCoroutine(DelayedGameStart());
        }

        /// <summary>
        /// 延迟触发游戏开始事件
        /// </summary>
        private IEnumerator DelayedGameStart()
        {
            LogDebug($"游戏将在 {gameStartDelay} 秒后开始");
            yield return new WaitForSeconds(gameStartDelay);

            LogDebug("触发游戏开始事件 - 准备切换到游戏场景");
            OnGameStarting?.Invoke();
        }

        /// <summary>
        /// 检查是否可以开始游戏
        /// </summary>
        public bool CanStartGame()
        {
            return NetworkManager.Instance?.CanStartGame(minPlayersToStart) ?? false;
        }

        /// <summary>
        /// 获取游戏开始条件检查详情
        /// </summary>
        public string GetGameStartConditions()
        {
            return NetworkManager.Instance?.GetGameStartConditions(minPlayersToStart) ?? "NetworkManager未初始化";
        }

        #endregion

        #region 房间状态管理

        /// <summary>
        /// 设置房间为等待状态
        /// </summary>
        private void SetRoomWaitingState()
        {
            if (!IsHost || NetworkManager.Instance == null) return;

            NetworkManager.Instance.SetRoomProperty(ROOM_STATE_KEY, (int)RoomState.Waiting);
            NetworkManager.Instance.SetRoomProperty(GAME_STARTED_KEY, false);
            LogDebug("房间状态设置为等待中");
        }

        /// <summary>
        /// 获取房间状态
        /// </summary>
        public RoomState GetRoomState()
        {
            if (NetworkManager.Instance == null) return RoomState.Waiting;
            return (RoomState)NetworkManager.Instance.GetRoomProperty(ROOM_STATE_KEY, 0);
        }

        /// <summary>
        /// 获取游戏是否已开始
        /// </summary>
        public bool GetGameStarted()
        {
            return NetworkManager.Instance?.GetRoomProperty(GAME_STARTED_KEY, false) ?? false;
        }

        #endregion

        #region 房间操作

        /// <summary>
        /// 离开房间并返回大厅
        /// </summary>
        public void LeaveRoomAndReturnToLobby()
        {
            if (!IsInRoom)
            {
                LogDebug("不在房间中，直接返回大厅");
                OnReturnToLobby?.Invoke();
                return;
            }

            LogDebug("离开房间并返回大厅");
            PhotonNetwork.LeaveRoom();
            ReturnToLobbyDelayed();
        }

        private void ReturnToLobbyDelayed()
        {
            LogDebug("执行返回大厅");

            bool switchSuccess = SceneTransitionManager.ReturnToMainMenu("RoomSceneController");

            if (!switchSuccess)
            {
                Debug.LogWarning("[RoomManager] SceneTransitionManager返回失败，使用备用方案");
                SceneManager.LoadScene("LobbyScene");
            }
        }

        /// <summary>
        /// 踢出玩家（房主专用）
        /// </summary>
        public bool KickPlayer(Player player)
        {
            if (!IsHost)
            {
                Debug.LogError("[RoomManager] 只有房主可以踢出玩家");
                return false;
            }

            if (player.IsMasterClient)
            {
                Debug.LogError("[RoomManager] 不能踢出房主");
                return false;
            }

            LogDebug($"房主踢出玩家: {player.NickName}");
            photonView.RPC("OnKickedFromRoom", player);
            return true;
        }

        [PunRPC]
        void OnKickedFromRoom()
        {
            LogDebug("被房主踢出房间");
            PhotonNetwork.LeaveRoom();
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 通过玩家ID获取Photon Player对象
        /// </summary>
        private Player GetPhotonPlayerById(ushort playerId)
        {
            if (!PhotonNetwork.InRoom) return null;
            foreach (var player in PhotonNetwork.PlayerList)
            {
                if (player.ActorNumber == playerId)
                    return player;
            }
            return null;
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[RoomManager] {message}");
            }
        }

        #endregion
    }

    #region 数据结构定义

    /// <summary>
    /// 房间状态枚举
    /// </summary>
    public enum RoomState
    {
        Waiting = 0,    // 等待玩家准备
        Starting = 1,   // 准备开始游戏
        InGame = 2,     // 游戏进行中
        Ended = 3       // 游戏结束
    }

    #endregion
}