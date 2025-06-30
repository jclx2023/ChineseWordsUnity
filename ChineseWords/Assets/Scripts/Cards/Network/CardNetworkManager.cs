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
    /// ������������� - �򻯰�
    /// ר�Ŵ�������ص�����ͬ����ֻ�� NetworkGameScene �д���
    /// ͨ�� NetworkManager ���� RPC ͨ��
    /// </summary>
    public class CardNetworkManager : MonoBehaviour
    {
        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        public static CardNetworkManager Instance { get; private set; }

        #region ���������¼�

        // ����ʹ���¼�
        public static event Action<ushort, int, ushort, string> OnCardUsed; // ʹ����ID, ����ID, Ŀ��ID, ��������
        public static event Action<ushort, string> OnCardEffectTriggered; // ���ID, Ч������

        // ����״̬����¼�
        public static event Action<ushort, int> OnCardAdded; // ���ID, ����ID
        public static event Action<ushort, int> OnCardRemoved; // ���ID, ����ID
        public static event Action<ushort, int> OnHandSizeChanged; // ���ID, ����������

        // ������Ϣ�¼�
        public static event Action<string, ushort> OnCardMessage; // ��Ϣ����, ��Դ���ID
        public static event Action<ushort, int, ushort> OnCardTransferred; // �����ID, ����ID, �����ID

        #endregion

        #region Unity��������

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
            // ���Ŀ���ϵͳ�¼�
            SubscribeToCardEvents();

            LogDebug("CardNetworkManager��ʼ�����");
        }

        private void OnDestroy()
        {
            // ȡ������
            UnsubscribeFromCardEvents();

            // ������
            if (Instance == this)
            {
                Instance = null;
            }

            LogDebug("CardNetworkManager������");
        }

        #endregion

        #region ��ʼ������֤

        /// <summary>
        /// ���Ŀ���ϵͳ�¼�
        /// </summary>
        private void SubscribeToCardEvents()
        {
            try
            {
                // ����PlayerCardManager���¼�
                if (PlayerCardManager.Instance != null)
                {
                    // ������Զ���PlayerCardManager������¼�
                    // PlayerCardManager.OnCardUsed += HandleLocalCardUsed;
                    LogDebug("�Ѷ���PlayerCardManager�¼�");
                }
                else
                {
                    LogDebug("PlayerCardManager��δ��ʼ������������ʱ��̬����");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CardNetworkManager] ���Ŀ����¼�ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// ȡ�����Ŀ���ϵͳ�¼�
        /// </summary>
        private void UnsubscribeFromCardEvents()
        {
            try
            {
                // ȡ������PlayerCardManager���¼�
                if (PlayerCardManager.Instance != null)
                {
                    // PlayerCardManager.OnCardUsed -= HandleLocalCardUsed;
                    LogDebug("��ȡ������PlayerCardManager�¼�");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CardNetworkManager] ȡ�����Ŀ����¼�ʧ��: {e.Message}");
            }
        }

        #endregion

        #region ��������㲥���� - �򻯰�

        /// <summary>
        /// �㲥����ʹ��
        /// </summary>
        public void BroadcastCardUsed(ushort playerId, int cardId, ushort targetPlayerId)
        {
            if (!CanSendRPC()) return;

            // ��ȡ��������
            string cardName = GetCardName(cardId);

            try
            {
                NetworkManager.Instance.BroadcastCardUsed(playerId, cardId, targetPlayerId, cardName);
                LogDebug($"�㲥����ʹ��: ���{playerId}ʹ��{cardName}(ID:{cardId}), Ŀ��:{targetPlayerId}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CardNetworkManager] �㲥����ʹ��ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// �㲥����Ч������
        /// </summary>
        public void BroadcastCardEffectTriggered(ushort playerId, string effectDescription)
        {
            if (!CanSendRPC()) return;

            try
            {
                NetworkManager.Instance.BroadcastCardEffectTriggered(playerId, effectDescription);
                LogDebug($"�㲥����Ч��: ���{playerId} - {effectDescription}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CardNetworkManager] �㲥����Ч��ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// �㲥�������
        /// </summary>
        public void BroadcastCardAdded(ushort playerId, int cardId)
        {
            if (!CanSendRPC()) return;

            try
            {
                NetworkManager.Instance.BroadcastCardAdded(playerId, cardId);
                LogDebug($"�㲥�������: ���{playerId}��ÿ���{cardId}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CardNetworkManager] �㲥�������ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// �㲥�����Ƴ�
        /// </summary>
        public void BroadcastCardRemoved(ushort playerId, int cardId)
        {
            if (!CanSendRPC()) return;

            try
            {
                NetworkManager.Instance.BroadcastCardRemoved(playerId, cardId);
                LogDebug($"�㲥�����Ƴ�: ���{playerId}ʧȥ����{cardId}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CardNetworkManager] �㲥�����Ƴ�ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// �㲥���������仯
        /// </summary>
        public void BroadcastHandSizeChanged(ushort playerId, int newHandSize)
        {
            if (!CanSendRPC()) return;

            try
            {
                NetworkManager.Instance.BroadcastHandSizeChanged(playerId, newHandSize);
                LogDebug($"�㲥���Ʊ仯: ���{playerId}��������:{newHandSize}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CardNetworkManager] �㲥���Ʊ仯ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// �㲥������Ϣ
        /// </summary>
        public void BroadcastCardMessage(string message, ushort fromPlayerId)
        {
            if (!CanSendRPC()) return;

            try
            {
                NetworkManager.Instance.BroadcastCardMessage(message, fromPlayerId);
                LogDebug($"�㲥������Ϣ: {message} (�������{fromPlayerId})");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CardNetworkManager] �㲥������Ϣʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// �㲥����ת��
        /// </summary>
        public void BroadcastCardTransferred(ushort fromPlayerId, int cardId, ushort toPlayerId)
        {
            if (!CanSendRPC()) return;

            try
            {
                NetworkManager.Instance.BroadcastCardTransferred(fromPlayerId, cardId, toPlayerId);
                LogDebug($"�㲥����ת��: ����{cardId}�����{fromPlayerId}ת�Ƶ����{toPlayerId}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CardNetworkManager] �㲥����ת��ʧ��: {e.Message}");
            }
        }

        #endregion

        #region RPC���շ��� - ��NetworkManager����

        /// <summary>
        /// ���տ���ʹ���¼�
        /// </summary>
        public void OnCardUsedReceived(ushort playerId, int cardId, ushort targetPlayerId, string cardName)
        {
            LogDebug($"���տ���ʹ��: ���{playerId}ʹ��{cardName}, Ŀ��:{targetPlayerId}");

            try
            {
                OnCardUsed?.Invoke(playerId, cardId, targetPlayerId, cardName);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CardNetworkManager] ������ʹ���¼�ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// ���տ���Ч�������¼�
        /// </summary>
        public void OnCardEffectTriggeredReceived(ushort playerId, string effectDescription)
        {
            LogDebug($"���տ���Ч��: ���{playerId} - {effectDescription}");

            try
            {
                OnCardEffectTriggered?.Invoke(playerId, effectDescription);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CardNetworkManager] ������Ч���¼�ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// ���տ�������¼�
        /// </summary>
        public void OnCardAddedReceived(ushort playerId, int cardId)
        {
            LogDebug($"���տ������: ���{playerId}��ÿ���{cardId}");

            try
            {
                OnCardAdded?.Invoke(playerId, cardId);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CardNetworkManager] ����������¼�ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// ���տ����Ƴ��¼�
        /// </summary>
        public void OnCardRemovedReceived(ushort playerId, int cardId)
        {
            LogDebug($"���տ����Ƴ�: ���{playerId}ʧȥ����{cardId}");

            try
            {
                OnCardRemoved?.Invoke(playerId, cardId);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CardNetworkManager] �������Ƴ��¼�ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// �������������仯�¼�
        /// </summary>
        public void OnHandSizeChangedReceived(ushort playerId, int newHandSize)
        {
            LogDebug($"�������Ʊ仯: ���{playerId}��������:{newHandSize}");

            try
            {
                OnHandSizeChanged?.Invoke(playerId, newHandSize);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CardNetworkManager] �������Ʊ仯�¼�ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// ���տ�����Ϣ�¼�
        /// </summary>
        public void OnCardMessageReceived(string message, ushort fromPlayerId)
        {
            LogDebug($"���տ�����Ϣ: {message} (�������{fromPlayerId})");

            try
            {
                OnCardMessage?.Invoke(message, fromPlayerId);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CardNetworkManager] ��������Ϣ�¼�ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// ���տ���ת���¼�
        /// </summary>
        public void OnCardTransferredReceived(ushort fromPlayerId, int cardId, ushort toPlayerId)
        {
            LogDebug($"���տ���ת��: ����{cardId}�����{fromPlayerId}ת�Ƶ����{toPlayerId}");

            try
            {
                OnCardTransferred?.Invoke(fromPlayerId, cardId, toPlayerId);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CardNetworkManager] ������ת���¼�ʧ��: {e.Message}");
            }
        }

        #endregion

        #region �����ӿڷ���

        /// <summary>
        /// ����Ƿ���Է���RPC
        /// </summary>
        public bool CanSendRPC()
        {
            if (NetworkManager.Instance == null)
            {
                LogDebug("NetworkManager�����ڣ��޷�����RPC");
                return false;
            }

            if (!NetworkManager.Instance.IsHost)
            {
                LogDebug("����Host���޷�����RPC");
                return false;
            }

            return true;
        }

        /// <summary>
        /// ��ȡ���������״̬
        /// </summary>
        public string GetNetworkStatus()
        {
            return $"CardNetworkManager״̬: " +
                   $"ʵ������={Instance != null}, " +
                   $"�ɷ���RPC={CanSendRPC()}, " +
                   $"����={SceneManager.GetActiveScene().name}";
        }

        #endregion

        #region ��������

        /// <summary>
        /// ��ȡ��������
        /// </summary>
        private string GetCardName(int cardId)
        {
            try
            {
                if (Cards.Integration.CardGameBridge.Instance != null)
                {
                    var cardData = Cards.Integration.CardGameBridge.GetCardDataById(cardId);
                    return cardData?.cardName ?? $"����{cardId}";
                }
            }
            catch (System.Exception e)
            {
                LogDebug($"��ȡ��������ʧ��: {e.Message}");
            }

            return $"����{cardId}";
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[CardNetworkManager] {message}");
            }
        }

        #endregion

        #region ���Է���

        [ContextMenu("��ʾ��������״̬")]
        public void ShowNetworkStatus()
        {
            string status = "=== CardNetworkManager״̬ ===\n";
            status += GetNetworkStatus() + "\n";
            status += $"NetworkManager����: {NetworkManager.Instance != null}\n";
            status += $"�ڷ�����: {Photon.Pun.PhotonNetwork.InRoom}\n";
            status += $"�������: {Photon.Pun.PhotonNetwork.PlayerList?.Length ?? 0}\n";

            Debug.Log(status);
        }

        #endregion
    }
}