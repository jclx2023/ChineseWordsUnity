using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using Core.Network;
using Cards.Core;
using Cards.Player;
using Cards.Network;

namespace Cards.Network
{
    /// <summary>
    /// 卡牌网络管理器 - 轻量化修复版
    /// 专门处理卡牌相关的网络同步，配合CardSystemManager管理
    /// 移除循环依赖，采用被动初始化模式
    /// </summary>
    public class CardNetworkManager : MonoBehaviour
    {
        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        public static CardNetworkManager Instance { get; private set; }

        // 初始化状态
        private bool isInitialized = false;

        #region 卡牌网络事件

        // 卡牌使用事件
        public static event Action<ushort, int, ushort, string> OnCardUsed; // 使用者ID, 卡牌ID, 目标ID, 卡牌名称
        public static event Action<ushort, string> OnCardEffectTriggered; // 玩家ID, 效果描述

        // 卡牌状态变更事件
        public static event Action<ushort, int> OnCardAdded; // 玩家ID, 卡牌ID
        public static event Action<ushort, int> OnCardRemoved; // 玩家ID, 卡牌ID
        public static event Action<ushort, int> OnHandSizeChanged; // 玩家ID, 新手牌数量

        // 卡牌消息事件
        public static event Action<string, ushort> OnCardMessage; // 消息内容, 来源玩家ID
        public static event Action<ushort, int, ushort> OnCardTransferred; // 从玩家ID, 卡牌ID, 到玩家ID

        #endregion

        #region Unity生命周期

        private void Awake()
        {
            // 单例设置 - 支持场景预置和程序创建两种方式
            if (Instance == null)
            {
                Instance = this;
                LogDebug("CardNetworkManager实例已创建");
            }
            else if (Instance != this)
            {
                LogDebug("发现重复的CardNetworkManager实例，销毁当前实例");
                Destroy(gameObject);
                return;
            }
        }

        private void OnDestroy()
        {
            // 清理单例和事件订阅
            UnsubscribeFromNetworkEvents();

            if (Instance == this)
            {
                Instance = null;
            }

            LogDebug("CardNetworkManager已销毁");
        }

        #endregion

        #region 初始化接口 - 供CardSystemManager调用

        /// <summary>
        /// 初始化网络管理器
        /// 由CardSystemManager在适当时机调用
        /// </summary>
        public void Initialize()
        {
            if (isInitialized)
            {
                LogDebug("CardNetworkManager已经初始化，跳过重复初始化");
                return;
            }

            LogDebug("开始初始化CardNetworkManager");

            try
            {
                // 订阅网络事件
                SubscribeToNetworkEvents();

                isInitialized = true;
                LogDebug("CardNetworkManager初始化完成");
            }
            catch (System.Exception e)
            {
                LogError($"CardNetworkManager初始化失败: {e.Message}");
            }
        }

        /// <summary>
        /// 系统就绪后检查现有玩家
        /// 由CardSystemManager在完全初始化后调用
        /// </summary>
        public void CheckExistingPlayersAfterSystemReady()
        {
            if (!isInitialized)
            {
                LogWarning("CardNetworkManager尚未初始化，无法检查现有玩家");
                return;
            }

            LogDebug("系统就绪后检查现有玩家");

            try
            {
                // 如果已经在房间中，初始化现有玩家
                if (IsNetworkAvailable() && Photon.Pun.PhotonNetwork.InRoom)
                {
                    LogDebug("已在房间中，通知CardSystemManager初始化玩家");
                    InitializeExistingPlayers();
                }
                else
                {
                    LogDebug("不在房间中或网络不可用");
                }
            }
            catch (System.Exception e)
            {
                LogError($"检查现有玩家失败: {e.Message}");
            }
        }

        /// <summary>
        /// 检查是否已初始化
        /// </summary>
        public bool IsInitialized => isInitialized;

        #endregion

        #region 网络事件订阅

        /// <summary>
        /// 订阅网络事件
        /// </summary>
        private void SubscribeToNetworkEvents()
        {
            try
            {
                if (NetworkManager.Instance != null)
                {
                    // 订阅玩家加入/离开事件
                    NetworkManager.OnPlayerJoined += OnNetworkPlayerJoined;
                    NetworkManager.OnPlayerLeft += OnNetworkPlayerLeft;

                    LogDebug("已订阅NetworkManager事件");
                }
            }
            catch (System.Exception e)
            {
                LogError($"订阅网络事件失败: {e.Message}");
            }
        }

        /// <summary>
        /// 取消订阅网络事件
        /// </summary>
        private void UnsubscribeFromNetworkEvents()
        {
            try
            {
                if (NetworkManager.Instance != null)
                {
                    NetworkManager.OnPlayerJoined -= OnNetworkPlayerJoined;
                    NetworkManager.OnPlayerLeft -= OnNetworkPlayerLeft;

                    LogDebug("已取消订阅NetworkManager事件");
                }
            }
            catch (System.Exception e)
            {
                LogError($"取消订阅网络事件失败: {e.Message}");
            }
        }

        #endregion

        #region 网络事件处理

        /// <summary>
        /// 网络玩家加入事件
        /// </summary>
        private void OnNetworkPlayerJoined(ushort playerId)
        {
            LogDebug($"玩家{playerId}加入，通知CardSystemManager");

            if (CardSystemManager.Instance != null)
            {
                CardSystemManager.Instance.OnPlayerJoined(playerId, $"Player{playerId}");
            }
        }

        /// <summary>
        /// 网络玩家离开事件
        /// </summary>
        private void OnNetworkPlayerLeft(ushort playerId)
        {
            LogDebug($"玩家{playerId}离开，通知CardSystemManager");

            if (CardSystemManager.Instance != null)
            {
                CardSystemManager.Instance.OnPlayerLeft(playerId);
            }
        }

        /// <summary>
        /// 初始化现有玩家
        /// </summary>
        private void InitializeExistingPlayers()
        {
            try
            {
                if (Photon.Pun.PhotonNetwork.InRoom && CardSystemManager.Instance != null)
                {
                    var playerIds = new List<int>();

                    foreach (var player in Photon.Pun.PhotonNetwork.PlayerList)
                    {
                        playerIds.Add(player.ActorNumber);
                    }

                    if (playerIds.Count > 0)
                    {
                        LogDebug($"通知CardSystemManager初始化{playerIds.Count}名现有玩家");
                        CardSystemManager.Instance.OnGameStarted(playerIds);
                    }
                }
            }
            catch (System.Exception e)
            {
                LogError($"初始化现有玩家失败: {e.Message}");
            }
        }

        /// <summary>
        /// 检查网络是否可用
        /// </summary>
        private bool IsNetworkAvailable()
        {
            return NetworkManager.Instance != null && Photon.Pun.PhotonNetwork.IsConnected;
        }

        #endregion

        #region 卡牌网络广播方法

        /// <summary>
        /// 广播卡牌使用
        /// </summary>
        public void BroadcastCardUsed(ushort playerId, int cardId, ushort targetPlayerId)
        {
            if (!CanSendRPC()) return;

            // 获取卡牌名称
            string cardName = GetCardName(cardId);

            try
            {
                NetworkManager.Instance.BroadcastCardUsed(playerId, cardId, targetPlayerId, cardName);
                LogDebug($"广播卡牌使用: 玩家{playerId}使用{cardName}(ID:{cardId}), 目标:{targetPlayerId}");
            }
            catch (System.Exception e)
            {
                LogError($"广播卡牌使用失败: {e.Message}");
            }
        }

        /// <summary>
        /// 广播卡牌效果触发
        /// </summary>
        public void BroadcastCardEffectTriggered(ushort playerId, string effectDescription)
        {
            if (!CanSendRPC()) return;

            try
            {
                NetworkManager.Instance.BroadcastCardEffectTriggered(playerId, effectDescription);
                LogDebug($"广播卡牌效果: 玩家{playerId} - {effectDescription}");
            }
            catch (System.Exception e)
            {
                LogError($"广播卡牌效果失败: {e.Message}");
            }
        }

        /// <summary>
        /// 广播卡牌添加
        /// </summary>
        public void BroadcastCardAdded(ushort playerId, int cardId)
        {
            if (!CanSendRPC()) return;

            try
            {
                NetworkManager.Instance.BroadcastCardAdded(playerId, cardId);
                LogDebug($"广播卡牌添加: 玩家{playerId}获得卡牌{cardId}");
            }
            catch (System.Exception e)
            {
                LogError($"广播卡牌添加失败: {e.Message}");
            }
        }

        /// <summary>
        /// 广播卡牌移除
        /// </summary>
        public void BroadcastCardRemoved(ushort playerId, int cardId)
        {
            if (!CanSendRPC()) return;

            try
            {
                NetworkManager.Instance.BroadcastCardRemoved(playerId, cardId);
                LogDebug($"广播卡牌移除: 玩家{playerId}失去卡牌{cardId}");
            }
            catch (System.Exception e)
            {
                LogError($"广播卡牌移除失败: {e.Message}");
            }
        }

        /// <summary>
        /// 广播手牌数量变化
        /// </summary>
        public void BroadcastHandSizeChanged(ushort playerId, int newHandSize)
        {
            if (!CanSendRPC()) return;

            try
            {
                NetworkManager.Instance.BroadcastHandSizeChanged(playerId, newHandSize);
                LogDebug($"广播手牌变化: 玩家{playerId}手牌数量:{newHandSize}");
            }
            catch (System.Exception e)
            {
                LogError($"广播手牌变化失败: {e.Message}");
            }
        }

        /// <summary>
        /// 广播卡牌消息
        /// </summary>
        public void BroadcastCardMessage(string message, ushort fromPlayerId)
        {
            if (!CanSendRPC()) return;

            try
            {
                NetworkManager.Instance.BroadcastCardMessage(message, fromPlayerId);
                LogDebug($"广播卡牌消息: {message} (来自玩家{fromPlayerId})");
            }
            catch (System.Exception e)
            {
                LogError($"广播卡牌消息失败: {e.Message}");
            }
        }

        /// <summary>
        /// 广播卡牌转移
        /// </summary>
        public void BroadcastCardTransferred(ushort fromPlayerId, int cardId, ushort toPlayerId)
        {
            if (!CanSendRPC()) return;

            try
            {
                NetworkManager.Instance.BroadcastCardTransferred(fromPlayerId, cardId, toPlayerId);
                LogDebug($"广播卡牌转移: 卡牌{cardId}从玩家{fromPlayerId}转移到玩家{toPlayerId}");
            }
            catch (System.Exception e)
            {
                LogError($"广播卡牌转移失败: {e.Message}");
            }
        }

        #endregion

        #region RPC接收方法 - 供NetworkManager调用

        /// <summary>
        /// 接收卡牌使用事件
        /// </summary>
        public void OnCardUsedReceived(ushort playerId, int cardId, ushort targetPlayerId, string cardName)
        {
            LogDebug($"接收卡牌使用: 玩家{playerId}使用{cardName}, 目标:{targetPlayerId}");

            try
            {
                OnCardUsed?.Invoke(playerId, cardId, targetPlayerId, cardName);
            }
            catch (System.Exception e)
            {
                LogError($"处理卡牌使用事件失败: {e.Message}");
            }
        }

        /// <summary>
        /// 接收卡牌效果触发事件
        /// </summary>
        public void OnCardEffectTriggeredReceived(ushort playerId, string effectDescription)
        {
            LogDebug($"接收卡牌效果: 玩家{playerId} - {effectDescription}");

            try
            {
                OnCardEffectTriggered?.Invoke(playerId, effectDescription);
            }
            catch (System.Exception e)
            {
                LogError($"处理卡牌效果事件失败: {e.Message}");
            }
        }

        /// <summary>
        /// 接收卡牌添加事件
        /// </summary>
        public void OnCardAddedReceived(ushort playerId, int cardId)
        {
            LogDebug($"接收卡牌添加: 玩家{playerId}获得卡牌{cardId}");

            try
            {
                OnCardAdded?.Invoke(playerId, cardId);
            }
            catch (System.Exception e)
            {
                LogError($"处理卡牌添加事件失败: {e.Message}");
            }
        }

        /// <summary>
        /// 接收卡牌移除事件
        /// </summary>
        public void OnCardRemovedReceived(ushort playerId, int cardId)
        {
            LogDebug($"接收卡牌移除: 玩家{playerId}失去卡牌{cardId}");

            try
            {
                OnCardRemoved?.Invoke(playerId, cardId);
            }
            catch (System.Exception e)
            {
                LogError($"处理卡牌移除事件失败: {e.Message}");
            }
        }

        /// <summary>
        /// 接收手牌数量变化事件
        /// </summary>
        public void OnHandSizeChangedReceived(ushort playerId, int newHandSize)
        {
            LogDebug($"接收手牌变化: 玩家{playerId}手牌数量:{newHandSize}");

            try
            {
                OnHandSizeChanged?.Invoke(playerId, newHandSize);
            }
            catch (System.Exception e)
            {
                LogError($"处理手牌变化事件失败: {e.Message}");
            }
        }

        /// <summary>
        /// 接收卡牌消息事件
        /// </summary>
        public void OnCardMessageReceived(string message, ushort fromPlayerId)
        {
            LogDebug($"接收卡牌消息: {message} (来自玩家{fromPlayerId})");

            try
            {
                OnCardMessage?.Invoke(message, fromPlayerId);
            }
            catch (System.Exception e)
            {
                LogError($"处理卡牌消息事件失败: {e.Message}");
            }
        }

        /// <summary>
        /// 接收卡牌转移事件
        /// </summary>
        public void OnCardTransferredReceived(ushort fromPlayerId, int cardId, ushort toPlayerId)
        {
            LogDebug($"接收卡牌转移: 卡牌{cardId}从玩家{fromPlayerId}转移到玩家{toPlayerId}");

            try
            {
                OnCardTransferred?.Invoke(fromPlayerId, cardId, toPlayerId);
            }
            catch (System.Exception e)
            {
                LogError($"处理卡牌转移事件失败: {e.Message}");
            }
        }

        /// <summary>
        /// 处理从NetworkManager转发的手牌分发数据
        /// </summary>
        public void OnHandCardsDistributionReceived(ushort playerId, List<int> cardIds)
        {
            LogDebug($"处理玩家{playerId}的手牌分发: {cardIds.Count}张卡牌");

            // 验证是否为本地玩家
            if (playerId != NetworkManager.Instance.ClientId)
            {
                LogDebug($"忽略其他玩家{playerId}的手牌数据（本地玩家: {NetworkManager.Instance.ClientId}）");
                return;
            }

            // 转发给PlayerCardManager处理
            if (PlayerCardManager.Instance != null)
            {
                bool success = PlayerCardManager.Instance.ReceiveHostDistributedCards((int)playerId, cardIds);

                if (success)
                {
                    LogDebug($"✓ 玩家{playerId}成功接收{cardIds.Count}张手牌");
                }
                else
                {
                    LogDebug($"✗ 玩家{playerId}手牌接收失败");
                }
            }
            else
            {
                Debug.LogError("[CardNetworkManager] PlayerCardManager实例不存在");
            }
        }

        #endregion

        #region 公共接口方法

        /// <summary>
        /// 检查是否可以发送RPC
        /// </summary>
        public bool CanSendRPC()
        {
            if (NetworkManager.Instance == null)
            {
                LogDebug("NetworkManager不存在，无法发送RPC");
                return false;
            }

            if (!NetworkManager.Instance.IsHost)
            {
                //LogDebug("不是Host，无法发送RPC");
                return false;
            }

            if (!Photon.Pun.PhotonNetwork.InRoom)
            {
                LogDebug("不在房间中，无法发送RPC");
                return false;
            }

            return true;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取卡牌名称
        /// </summary>
        private string GetCardName(int cardId)
        {
            try
            {
                if (Cards.Integration.CardGameBridge.Instance != null)
                {
                    var cardData = Cards.Integration.CardGameBridge.GetCardDataById(cardId);
                    return cardData?.cardName ?? $"卡牌{cardId}";
                }
            }
            catch (System.Exception e)
            {
                LogDebug($"获取卡牌名称失败: {e.Message}");
            }

            return $"卡牌{cardId}";
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[CardNetworkManager] {message}");
            }
        }

        private void LogWarning(string message)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[CardNetworkManager] {message}");
            }
        }

        private void LogError(string message)
        {
            Debug.LogError($"[CardNetworkManager] {message}");
        }

        #endregion
    }
}