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
    /// ������������� - �������޸���
    /// ר�Ŵ�������ص�����ͬ�������CardSystemManager����
    /// �Ƴ�ѭ�����������ñ�����ʼ��ģʽ
    /// </summary>
    public class CardNetworkManager : MonoBehaviour
    {
        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        public static CardNetworkManager Instance { get; private set; }

        // ��ʼ��״̬
        private bool isInitialized = false;

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
            // �������� - ֧�ֳ���Ԥ�úͳ��򴴽����ַ�ʽ
            if (Instance == null)
            {
                Instance = this;
                LogDebug("CardNetworkManagerʵ���Ѵ���");
            }
            else if (Instance != this)
            {
                LogDebug("�����ظ���CardNetworkManagerʵ�������ٵ�ǰʵ��");
                Destroy(gameObject);
                return;
            }
        }

        private void OnDestroy()
        {
            // ���������¼�����
            UnsubscribeFromNetworkEvents();

            if (Instance == this)
            {
                Instance = null;
            }

            LogDebug("CardNetworkManager������");
        }

        #endregion

        #region ��ʼ���ӿ� - ��CardSystemManager����

        /// <summary>
        /// ��ʼ�����������
        /// ��CardSystemManager���ʵ�ʱ������
        /// </summary>
        public void Initialize()
        {
            if (isInitialized)
            {
                LogDebug("CardNetworkManager�Ѿ���ʼ���������ظ���ʼ��");
                return;
            }

            LogDebug("��ʼ��ʼ��CardNetworkManager");

            try
            {
                // ���������¼�
                SubscribeToNetworkEvents();

                isInitialized = true;
                LogDebug("CardNetworkManager��ʼ�����");
            }
            catch (System.Exception e)
            {
                LogError($"CardNetworkManager��ʼ��ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// ϵͳ���������������
        /// ��CardSystemManager����ȫ��ʼ�������
        /// </summary>
        public void CheckExistingPlayersAfterSystemReady()
        {
            if (!isInitialized)
            {
                LogWarning("CardNetworkManager��δ��ʼ�����޷�����������");
                return;
            }

            LogDebug("ϵͳ���������������");

            try
            {
                // ����Ѿ��ڷ����У���ʼ���������
                if (IsNetworkAvailable() && Photon.Pun.PhotonNetwork.InRoom)
                {
                    LogDebug("���ڷ����У�֪ͨCardSystemManager��ʼ�����");
                    InitializeExistingPlayers();
                }
                else
                {
                    LogDebug("���ڷ����л����粻����");
                }
            }
            catch (System.Exception e)
            {
                LogError($"����������ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// ����Ƿ��ѳ�ʼ��
        /// </summary>
        public bool IsInitialized => isInitialized;

        #endregion

        #region �����¼�����

        /// <summary>
        /// ���������¼�
        /// </summary>
        private void SubscribeToNetworkEvents()
        {
            try
            {
                if (NetworkManager.Instance != null)
                {
                    // ������Ҽ���/�뿪�¼�
                    NetworkManager.OnPlayerJoined += OnNetworkPlayerJoined;
                    NetworkManager.OnPlayerLeft += OnNetworkPlayerLeft;

                    LogDebug("�Ѷ���NetworkManager�¼�");
                }
                else
                {
                    LogWarning("NetworkManager�����ڣ��޷����������¼�");
                }
            }
            catch (System.Exception e)
            {
                LogError($"���������¼�ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// ȡ�����������¼�
        /// </summary>
        private void UnsubscribeFromNetworkEvents()
        {
            try
            {
                if (NetworkManager.Instance != null)
                {
                    NetworkManager.OnPlayerJoined -= OnNetworkPlayerJoined;
                    NetworkManager.OnPlayerLeft -= OnNetworkPlayerLeft;

                    LogDebug("��ȡ������NetworkManager�¼�");
                }
            }
            catch (System.Exception e)
            {
                LogError($"ȡ�����������¼�ʧ��: {e.Message}");
            }
        }

        #endregion

        #region �����¼�����

        /// <summary>
        /// ������Ҽ����¼�
        /// </summary>
        private void OnNetworkPlayerJoined(ushort playerId)
        {
            LogDebug($"���{playerId}���룬֪ͨCardSystemManager");

            if (CardSystemManager.Instance != null)
            {
                CardSystemManager.Instance.OnPlayerJoined(playerId, $"Player{playerId}");
            }
            else
            {
                LogWarning("CardSystemManager�����ã��޷�������Ҽ���");
            }
        }

        /// <summary>
        /// ��������뿪�¼�
        /// </summary>
        private void OnNetworkPlayerLeft(ushort playerId)
        {
            LogDebug($"���{playerId}�뿪��֪ͨCardSystemManager");

            if (CardSystemManager.Instance != null)
            {
                CardSystemManager.Instance.OnPlayerLeft(playerId);
            }
            else
            {
                LogWarning("CardSystemManager�����ã��޷���������뿪");
            }
        }

        /// <summary>
        /// ��ʼ���������
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
                        LogDebug($"֪ͨCardSystemManager��ʼ��{playerIds.Count}���������");
                        CardSystemManager.Instance.OnGameStarted(playerIds);
                    }
                }
            }
            catch (System.Exception e)
            {
                LogError($"��ʼ���������ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// ��������Ƿ����
        /// </summary>
        private bool IsNetworkAvailable()
        {
            return NetworkManager.Instance != null && Photon.Pun.PhotonNetwork.IsConnected;
        }

        #endregion

        #region ��������㲥����

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
                LogError($"�㲥����ʹ��ʧ��: {e.Message}");
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
                LogError($"�㲥����Ч��ʧ��: {e.Message}");
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
                LogError($"�㲥�������ʧ��: {e.Message}");
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
                LogError($"�㲥�����Ƴ�ʧ��: {e.Message}");
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
                LogError($"�㲥���Ʊ仯ʧ��: {e.Message}");
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
                LogError($"�㲥������Ϣʧ��: {e.Message}");
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
                LogError($"�㲥����ת��ʧ��: {e.Message}");
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
                LogError($"������ʹ���¼�ʧ��: {e.Message}");
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
                LogError($"������Ч���¼�ʧ��: {e.Message}");
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
                LogError($"����������¼�ʧ��: {e.Message}");
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
                LogError($"�������Ƴ��¼�ʧ��: {e.Message}");
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
                LogError($"�������Ʊ仯�¼�ʧ��: {e.Message}");
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
                LogError($"��������Ϣ�¼�ʧ��: {e.Message}");
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
                LogError($"������ת���¼�ʧ��: {e.Message}");
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

            if (!Photon.Pun.PhotonNetwork.InRoom)
            {
                LogDebug("���ڷ����У��޷�����RPC");
                return false;
            }

            return true;
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