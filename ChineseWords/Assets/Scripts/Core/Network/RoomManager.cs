using UnityEngine;
using System;
using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Core.Network
{
    /// <summary>
    /// 房间管理器 - RoomScene专用版
    /// 职责：管理已加入房间的玩家状态、准备控制、游戏开始
    /// 不负责房间创建/加入（由LobbyScene处理）
    /// </summary>
    public class RoomManager : MonoBehaviourPun, IInRoomCallbacks
    {
        [Header("游戏开始控制")]
        [SerializeField] private float gameStartDelay = 2f;
        [SerializeField] private int minPlayersToStart = 2;

        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        public static RoomManager Instance { get; private set; }

        // Photon房间属性键名常量
        private const string ROOM_STATE_KEY = "roomState";
        private const string GAME_STARTED_KEY = "gameStarted";

        // 玩家属性键名常量
        private const string PLAYER_READY_KEY = "isReady";

        // RoomScene专用事件
        public static event Action OnRoomEntered;           // 进入RoomScene时触发
        public static event Action<Player> OnPlayerJoinedRoom;
        public static event Action<Player> OnPlayerLeftRoom;
        public static event Action<Player, bool> OnPlayerReadyChanged;
        public static event Action OnAllPlayersReady;       // 所有玩家准备就绪
        public static event Action OnGameStarting;          // 游戏即将开始
        public static event Action OnReturnToLobby;         // 返回大厅

        // 状态属性 - 直接使用Photon API
        public bool IsHost => PhotonNetwork.IsMasterClient;
        public bool IsInRoom => PhotonNetwork.InRoom;
        public string RoomName => PhotonNetwork.CurrentRoom?.Name ?? "";
        public int PlayerCount => PhotonNetwork.CurrentRoom?.PlayerCount ?? 0;
        public int MaxPlayers => PhotonNetwork.CurrentRoom?.MaxPlayers ?? 0;
        public bool IsInitialized => isInitialized;

        // RoomScene状态
        private bool isInitialized = false;
        private bool hasGameStarted = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                LogDebug("RoomManager (RoomScene版) 初始化");
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

        /// <summary>
        /// 初始化房间管理器
        /// </summary>
        private IEnumerator InitializeRoomManager()
        {
            // 等待确保在房间中
            while (!PhotonNetwork.InRoom)
            {
                LogDebug("等待进入Photon房间...");
                yield return new WaitForSeconds(0.1f);
            }

            // 注册Photon回调
            PhotonNetwork.AddCallbackTarget(this);

            // 验证房间状态
            if (!ValidateRoomState())
            {
                Debug.LogError("[RoomManager] 房间状态验证失败");
                yield break;
            }

            // 重置游戏开始标志
            hasGameStarted = GetGameStarted();

            isInitialized = true;
            LogDebug($"RoomManager初始化完成 - 房间: {RoomName}, 玩家数: {PlayerCount}, 是否房主: {IsHost}");

            // 触发房间进入事件
            OnRoomEntered?.Invoke();

            // 如果是房主，确保房间处于等待状态
            if (IsHost && !hasGameStarted)
            {
                SetRoomWaitingState();
            }
        }

        private void OnDestroy()
        {
            // 移除Photon回调
            if (PhotonNetwork.NetworkingClient != null)
            {
                PhotonNetwork.RemoveCallbackTarget(this);
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

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

            // 使用Photon玩家属性
            var props = new Hashtable { { PLAYER_READY_KEY, ready } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);

            return true;
        }

        /// <summary>
        /// 获取指定玩家的准备状态
        /// </summary>
        public bool GetPlayerReady(Player player)
        {
            if (player?.CustomProperties?.TryGetValue(PLAYER_READY_KEY, out object isReady) == true)
            {
                return (bool)isReady;
            }
            return false;
        }

        /// <summary>
        /// 获取本地玩家的准备状态
        /// </summary>
        public bool GetMyReadyState()
        {
            return GetPlayerReady(PhotonNetwork.LocalPlayer);
        }

        /// <summary>
        /// 获取准备就绪的玩家数量（不包括房主）
        /// </summary>
        public int GetReadyPlayerCount()
        {
            if (!IsInRoom) return 0;

            int readyCount = 0;
            foreach (var player in PhotonNetwork.PlayerList)
            {
                if (!player.IsMasterClient && GetPlayerReady(player))
                {
                    readyCount++;
                }
            }
            return readyCount;
        }

        /// <summary>
        /// 获取非房主玩家数量
        /// </summary>
        public int GetNonHostPlayerCount()
        {
            if (!IsInRoom) return 0;

            int nonHostCount = 0;
            foreach (var player in PhotonNetwork.PlayerList)
            {
                if (!player.IsMasterClient)
                {
                    nonHostCount++;
                }
            }
            return nonHostCount;
        }

        /// <summary>
        /// 检查所有非房主玩家是否都已准备
        /// </summary>
        public bool AreAllPlayersReady()
        {
            if (!IsInRoom) return false;

            int nonHostPlayers = GetNonHostPlayerCount();
            if (nonHostPlayers == 0) return false;

            int readyPlayers = GetReadyPlayerCount();
            bool allReady = readyPlayers == nonHostPlayers;

            return allReady;
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

            // 使用房间属性广播游戏开始
            var roomProps = new Hashtable
            {
                { ROOM_STATE_KEY, (int)RoomState.Starting },
                { GAME_STARTED_KEY, true }
            };

            PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);

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
            if (!IsHost) return false;
            if (!IsInRoom || !isInitialized) return false;
            if (hasGameStarted) return false;

            // 检查最小玩家数
            if (PlayerCount < minPlayersToStart)
            {
                return false;
            }

            // 检查房间状态
            if (GetRoomState() != RoomState.Waiting)
            {
                return false;
            }

            // 检查所有非房主玩家是否都已准备
            return AreAllPlayersReady();
        }

        /// <summary>
        /// 获取游戏开始条件检查详情
        /// </summary>
        public string GetGameStartConditions()
        {
            if (!IsHost) return "不是房主";
            if (!IsInRoom || !isInitialized) return "房间未初始化";
            if (hasGameStarted) return "游戏已开始";

            if (PlayerCount < minPlayersToStart)
                return $"玩家数不足 ({PlayerCount}/{minPlayersToStart})";

            if (GetRoomState() != RoomState.Waiting)
                return $"房间状态错误 ({GetRoomState()})";

            int readyCount = GetReadyPlayerCount();
            int nonHostCount = GetNonHostPlayerCount();

            if (!AreAllPlayersReady())
                return $"玩家未全部准备 ({readyCount}/{nonHostCount})";

            return "满足开始条件";
        }

        #endregion

        #region 房间状态管理

        /// <summary>
        /// 设置房间为等待状态
        /// </summary>
        private void SetRoomWaitingState()
        {
            if (!IsHost) return;

            var roomProps = new Hashtable
            {
                { ROOM_STATE_KEY, (int)RoomState.Waiting },
                { GAME_STARTED_KEY, false }
            };

            PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);
            LogDebug("房间状态设置为等待中");
        }

        /// <summary>
        /// 获取房间状态
        /// </summary>
        public RoomState GetRoomState()
        {
            if (!IsInRoom) return RoomState.Waiting;

            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ROOM_STATE_KEY, out object state))
            {
                return (RoomState)state;
            }
            return RoomState.Waiting;
        }

        /// <summary>
        /// 获取游戏是否已开始
        /// </summary>
        public bool GetGameStarted()
        {
            if (!IsInRoom) return false;

            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(GAME_STARTED_KEY, out object started))
            {
                return (bool)started;
            }
            return false;
        }

        /// <summary>
        /// 验证房间状态
        /// </summary>
        private bool ValidateRoomState()
        {
            if (!PhotonNetwork.InRoom)
            {
                Debug.LogError("[RoomManager] 不在Photon房间中");
                return false;
            }

            if (PhotonNetwork.CurrentRoom == null)
            {
                Debug.LogError("[RoomManager] 当前房间为空");
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

            // Photon中踢出玩家需要通过关闭房间或其他方式
            // 这里可以使用自定义RPC通知被踢出的玩家
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

        #region Photon回调实现

        void IInRoomCallbacks.OnPlayerEnteredRoom(Player newPlayer)
        {
            LogDebug($"玩家加入房间: {newPlayer.NickName} (ID: {newPlayer.ActorNumber})");
            OnPlayerJoinedRoom?.Invoke(newPlayer);

            // 如果房主，可以发送欢迎消息或房间状态
            if (IsHost)
            {
                LogDebug($"当前房间玩家数: {PlayerCount}/{MaxPlayers}");
            }
        }

        void IInRoomCallbacks.OnPlayerLeftRoom(Player otherPlayer)
        {
            LogDebug($"玩家离开房间: {otherPlayer.NickName} (ID: {otherPlayer.ActorNumber})");
            OnPlayerLeftRoom?.Invoke(otherPlayer);

            // 检查是否还能继续游戏
            if (hasGameStarted && PlayerCount < minPlayersToStart)
            {
                LogDebug("玩家数量不足，游戏可能需要暂停或结束");
            }
        }

        void IInRoomCallbacks.OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            // 处理玩家准备状态变化
            if (changedProps.TryGetValue(PLAYER_READY_KEY, out object isReadyObj))
            {
                bool isReady = (bool)isReadyObj;
                LogDebug($"玩家准备状态更新: {targetPlayer.NickName} -> {isReady}");
                OnPlayerReadyChanged?.Invoke(targetPlayer, isReady);

                // 检查是否所有玩家都已准备
                if (AreAllPlayersReady() && GetNonHostPlayerCount() > 0)
                {
                    LogDebug("所有玩家都已准备就绪");
                    OnAllPlayersReady?.Invoke();
                }
            }
        }

        void IInRoomCallbacks.OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
            // 处理游戏开始
            if (propertiesThatChanged.TryGetValue(GAME_STARTED_KEY, out object gameStartedObj))
            {
                bool gameStarted = (bool)gameStartedObj;
                if (gameStarted && !IsHost) // 客户端收到游戏开始通知
                {
                    LogDebug("客户端收到游戏开始通知");
                    hasGameStarted = true;
                    StartCoroutine(DelayedGameStart());
                }
            }

            // 处理房间状态变化
            if (propertiesThatChanged.TryGetValue(ROOM_STATE_KEY, out object roomStateObj))
            {
                RoomState newState = (RoomState)roomStateObj;
                LogDebug($"房间状态更新: {newState}");
            }
        }

        void IInRoomCallbacks.OnMasterClientSwitched(Player newMasterClient)
        {
            LogDebug($"房主切换到: {newMasterClient.NickName} (ID: {newMasterClient.ActorNumber})");

            // 如果本地玩家成为新房主
            if (newMasterClient.IsLocal)
            {
                LogDebug("本地玩家成为新房主，重置房间状态");

                // 重置房间状态
                if (!hasGameStarted)
                {
                    SetRoomWaitingState();
                }
            }
        }

        #endregion

        #region 状态查询

        /// <summary>
        /// 获取房间状态信息
        /// </summary>
        public string GetRoomStatusInfo()
        {
            if (!IsInRoom)
                return "未在房间中";

            return $"房间: {RoomName}, " +
                   $"状态: {GetRoomState()}, " +
                   $"玩家: {PlayerCount}/{MaxPlayers}, " +
                   $"准备: {GetReadyPlayerCount()}/{GetNonHostPlayerCount()}, " +
                   $"是否房主: {IsHost}, " +
                   $"游戏已开始: {hasGameStarted}, " +
                   $"可以开始: {CanStartGame()}";
        }

        /// <summary>
        /// 获取玩家列表信息
        /// </summary>
        public string GetPlayerListInfo()
        {
            if (!IsInRoom) return "未在房间中";

            string info = "玩家列表:\n";
            foreach (var player in PhotonNetwork.PlayerList)
            {
                bool isReady = GetPlayerReady(player);
                info += $"  - {player.NickName} (ID: {player.ActorNumber}) ";
                info += $"[{(player.IsMasterClient ? "房主" : "玩家")}] ";

                if (!player.IsMasterClient)
                {
                    info += $"[{(isReady ? "已准备" : "未准备")}]";
                }

                info += "\n";
            }
            return info;
        }

        #endregion

        #region 辅助方法

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

        #region 调试方法

        [ContextMenu("显示房间状态")]
        public void ShowRoomStatus()
        {
            if (Application.isPlaying)
            {
                Debug.Log("=== RoomScene房间状态 ===");
                Debug.Log(GetRoomStatusInfo());
                Debug.Log(GetPlayerListInfo());
                Debug.Log($"游戏开始条件: {GetGameStartConditions()}");
            }
        }

        [ContextMenu("强制开始游戏")]
        public void ForceStartGame()
        {
            if (Application.isPlaying && IsHost)
            {
                LogDebug("强制开始游戏（调试用）");
                hasGameStarted = false; // 重置状态
                StartGame();
            }
        }

        [ContextMenu("切换准备状态")]
        public void ToggleReadyState()
        {
            if (Application.isPlaying && IsInRoom && !IsHost)
            {
                bool currentReady = GetMyReadyState();
                SetPlayerReady(!currentReady);
                LogDebug($"切换准备状态: {currentReady} -> {!currentReady}");
            }
        }

        [ContextMenu("重置房间状态")]
        public void ResetRoomState()
        {
            if (Application.isPlaying && IsHost)
            {
                hasGameStarted = false;
                SetRoomWaitingState();
                LogDebug("房间状态已重置");
            }
        }

        [ContextMenu("返回大厅")]
        public void ReturnToLobby()
        {
            if (Application.isPlaying)
            {
                LeaveRoomAndReturnToLobby();
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