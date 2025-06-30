using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using Core.Network;
using Cards.Core;
using Cards.Player;
using Cards.Network;

namespace Cards.Network
{
    /// <summary>
    /// 卡牌网络管理器 - 简化版
    /// 专门处理卡牌相关的网络同步，只在 NetworkGameScene 中存在
    /// 通过 NetworkManager 进行 RPC 通信
    /// </summary>
    public class CardNetworkManager : MonoBehaviour
    {
        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;

        public static CardNetworkManager Instance { get; private set; }

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
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // 订阅卡牌系统事件
            SubscribeToCardEvents();

            LogDebug("CardNetworkManager初始化完成");
        }

        private void OnDestroy()
        {
            // 取消订阅
            UnsubscribeFromCardEvents();

            // 清理单例
            if (Instance == this)
            {
                Instance = null;
            }

            LogDebug("CardNetworkManager已销毁");
        }

        #endregion

        #region 初始化和验证

        /// <summary>
        /// 订阅卡牌系统事件
        /// </summary>
        private void SubscribeToCardEvents()
        {
            try
            {
                // 订阅PlayerCardManager的事件
                if (PlayerCardManager.Instance != null)
                {
                    // 这里可以订阅PlayerCardManager的相关事件
                    // PlayerCardManager.OnCardUsed += HandleLocalCardUsed;
                    LogDebug("已订阅PlayerCardManager事件");
                }
                else
                {
                    LogDebug("PlayerCardManager暂未初始化，将在运行时动态订阅");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CardNetworkManager] 订阅卡牌事件失败: {e.Message}");
            }
        }

        /// <summary>
        /// 取消订阅卡牌系统事件
        /// </summary>
        private void UnsubscribeFromCardEvents()
        {
            try
            {
                // 取消订阅PlayerCardManager的事件
                if (PlayerCardManager.Instance != null)
                {
                    // PlayerCardManager.OnCardUsed -= HandleLocalCardUsed;
                    LogDebug("已取消订阅PlayerCardManager事件");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CardNetworkManager] 取消订阅卡牌事件失败: {e.Message}");
            }
        }

        #endregion

        #region 卡牌网络广播方法 - 简化版

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
                Debug.LogError($"[CardNetworkManager] 广播卡牌使用失败: {e.Message}");
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
                Debug.LogError($"[CardNetworkManager] 广播卡牌效果失败: {e.Message}");
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
                Debug.LogError($"[CardNetworkManager] 广播卡牌添加失败: {e.Message}");
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
                Debug.LogError($"[CardNetworkManager] 广播卡牌移除失败: {e.Message}");
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
                Debug.LogError($"[CardNetworkManager] 广播手牌变化失败: {e.Message}");
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
                Debug.LogError($"[CardNetworkManager] 广播卡牌消息失败: {e.Message}");
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
                Debug.LogError($"[CardNetworkManager] 广播卡牌转移失败: {e.Message}");
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
                Debug.LogError($"[CardNetworkManager] 处理卡牌使用事件失败: {e.Message}");
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
                Debug.LogError($"[CardNetworkManager] 处理卡牌效果事件失败: {e.Message}");
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
                Debug.LogError($"[CardNetworkManager] 处理卡牌添加事件失败: {e.Message}");
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
                Debug.LogError($"[CardNetworkManager] 处理卡牌移除事件失败: {e.Message}");
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
                Debug.LogError($"[CardNetworkManager] 处理手牌变化事件失败: {e.Message}");
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
                Debug.LogError($"[CardNetworkManager] 处理卡牌消息事件失败: {e.Message}");
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
                Debug.LogError($"[CardNetworkManager] 处理卡牌转移事件失败: {e.Message}");
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
                LogDebug("不是Host，无法发送RPC");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 获取网络管理器状态
        /// </summary>
        public string GetNetworkStatus()
        {
            return $"CardNetworkManager状态: " +
                   $"实例存在={Instance != null}, " +
                   $"可发送RPC={CanSendRPC()}, " +
                   $"场景={SceneManager.GetActiveScene().name}";
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

        /// <summary>
        /// 调试日志
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[CardNetworkManager] {message}");
            }
        }

        #endregion

        #region 调试方法

        [ContextMenu("显示卡牌网络状态")]
        public void ShowNetworkStatus()
        {
            string status = "=== CardNetworkManager状态 ===\n";
            status += GetNetworkStatus() + "\n";
            status += $"NetworkManager存在: {NetworkManager.Instance != null}\n";
            status += $"在房间中: {Photon.Pun.PhotonNetwork.InRoom}\n";
            status += $"玩家数量: {Photon.Pun.PhotonNetwork.PlayerList?.Length ?? 0}\n";

            Debug.Log(status);
        }

        #endregion
    }
}