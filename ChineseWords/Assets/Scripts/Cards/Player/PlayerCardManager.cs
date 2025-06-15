using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Core;
using Core.Network;
using Cards.Core;

namespace Cards.Player
{
    /// <summary>
    /// ��ҿ��ƹ�����
    /// �������ÿ����ҵĿ���״̬��ʹ��Ȩ�ޡ����Ʋ�����
    /// ��������Ϸϵͳ���ɣ�ʵ�ָ��˻غ��ƵĿ��ƻ���
    /// </summary>
    public class PlayerCardManager : MonoBehaviour
    {
        [Header("��������")]
        [SerializeField] private int maxHandSize = 5;
        [SerializeField] private int initialCardCount = 3;
        [SerializeField] private bool enableDebugLogs = true;

        [Header("��������")]
        [SerializeField] private CardConfig cardConfig;

        [Header("��������")]
        [SerializeField] private bool enableNetworkSync = true;

        // ����ʵ��
        public static PlayerCardManager Instance { get; private set; }

        // ��ҿ���״̬�����򻯣�ֻʹ��һ��Dictionary��
        private Dictionary<int, EnhancedPlayerCardState> playerCardStates;

        // ��ǰ�غ���Ϣ
        private int currentTurnPlayerId = 0;
        private bool isInitialized = false;

        #region �¼�����

        /// <summary>
        /// ����ʹ���¼� - playerId, cardId, targetPlayerId������У�
        /// </summary>
        public System.Action<int, int, int> OnCardUsed;

        /// <summary>
        /// ���ƻ���¼� - playerId, cardId, cardName
        /// </summary>
        public System.Action<int, int, string> OnCardAcquired;

        /// <summary>
        /// ʹ�û��������¼� - playerId
        /// </summary>
        public System.Action<int> OnUsageOpportunityReset;

        /// <summary>
        /// ����ת���¼� - fromPlayerId, toPlayerId, cardId
        /// </summary>
        public System.Action<int, int, int> OnCardTransferred;

        #endregion

        #region Unity��������

        private void Awake()
        {
            // ����ģʽ
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                playerCardStates = new Dictionary<int, EnhancedPlayerCardState>();

                LogDebug("PlayerCardManagerʵ���Ѵ���");
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                UnsubscribeFromEvents();
                Instance = null;
            }
        }

        #endregion

        #region ��ʼ��

        /// <summary>
        /// ��ʼ�����ƹ�����
        /// </summary>
        public void Initialize()
        {
            if (isInitialized)
            {
                LogDebug("PlayerCardManager�ѳ�ʼ���������ظ���ʼ��");
                return;
            }

            try
            {
                // ��ȡ��������
                if (cardConfig == null)
                {
                    cardConfig = Resources.Load<CardConfig>("CardConfig");
                    if (cardConfig == null)
                    {
                        Debug.LogError("[PlayerCardManager] �޷�����CardConfig��Դ");
                        return;
                    }
                }

                // ��֤����
                if (!cardConfig.ValidateConfig())
                {
                    Debug.LogError("[PlayerCardManager] CardConfig��֤ʧ��");
                    return;
                }

                // �������л�ȡ����
                maxHandSize = cardConfig.SystemSettings.maxHandSize;
                initialCardCount = cardConfig.SystemSettings.startingCardCount;

                // ������Ϸ�¼�
                SubscribeToGameEvents();

                isInitialized = true;
                LogDebug("PlayerCardManager��ʼ�����");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PlayerCardManager] ��ʼ��ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// ������Ϸ�¼�
        /// </summary>
        private void SubscribeToGameEvents()
        {
            // ���Ŀ���ϵͳ�¼�
            CardEvents.OnPlayerTurnCompleted += OnPlayerAnswerCompleted;

            LogDebug("�Ѷ�����Ϸ�¼�");
        }

        /// <summary>
        /// ȡ��������Ϸ�¼�
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            // ȡ�����Ŀ���ϵͳ�¼�
            CardEvents.OnPlayerTurnCompleted -= OnPlayerAnswerCompleted;

            // ȡ���Զ����¼�����
            OnCardUsed = null;
            OnCardAcquired = null;
            OnUsageOpportunityReset = null;
            OnCardTransferred = null;

            LogDebug("��ȡ�������¼�����");
        }

        #endregion

        #region ��ҹ���

        /// <summary>
        /// ��ʼ����ҿ���״̬
        /// </summary>
        /// <param name="playerId">���ID</param>
        /// <param name="playerName">�������</param>
        public void InitializePlayer(int playerId, string playerName = "")
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[PlayerCardManager] ������δ��ʼ�����޷���ʼ�����");
                return;
            }

            if (playerCardStates.ContainsKey(playerId))
            {
                LogDebug($"��� {playerId} �Ŀ���״̬�Ѵ��ڣ������ظ���ʼ��");
                return;
            }

            // ������ǿ����ҿ���״̬
            var cardState = new EnhancedPlayerCardState(maxHandSize, cardConfig.DrawSettings)
            {
                playerId = playerId,
                playerName = string.IsNullOrEmpty(playerName) ? $"Player{playerId}" : playerName,
                canUseCardThisRound = true
            };

            // ����ҳ�ʼ����
            for (int i = 0; i < initialCardCount; i++)
            {
                var randomCard = DrawRandomCard();
                if (randomCard != null && cardState.CanAddSpecificCard(randomCard.cardId))
                {
                    cardState.AddCard(randomCard.cardId);

                    LogDebug($"Ϊ��� {playerId} ��ӳ�ʼ����: {randomCard.cardName}");

                    // �������ƻ���¼�
                    OnCardAcquired?.Invoke(playerId, randomCard.cardId, randomCard.cardName);
                    CardEvents.OnCardAddedToHand?.Invoke(playerId, randomCard.cardId);
                }
            }

            playerCardStates[playerId] = cardState;

            LogDebug($"��� {playerId} ����״̬��ʼ����� - ��ʼ������: {cardState.HandCount}");
        }

        /// <summary>
        /// �Ƴ����
        /// </summary>
        /// <param name="playerId">���ID</param>
        public void RemovePlayer(int playerId)
        {
            if (playerCardStates.ContainsKey(playerId))
            {
                playerCardStates.Remove(playerId);
                LogDebug($"��� {playerId} �Ŀ���״̬���Ƴ�");
            }
        }

        /// <summary>
        /// ���������������
        /// </summary>
        public void ClearAllPlayers()
        {
            playerCardStates.Clear();
            currentTurnPlayerId = 0;
            LogDebug("������ҿ�������������");
        }

        #endregion

        #region ����ʹ��

        /// <summary>
        /// ʹ�ÿ���
        /// </summary>
        /// <param name="playerId">ʹ����ID</param>
        /// <param name="cardId">����ID</param>
        /// <param name="targetPlayerId">Ŀ�����ID����ѡ��</param>
        /// <returns>�Ƿ�ɹ�ʹ��</returns>
        public bool UseCard(int playerId, int cardId, int targetPlayerId = -1)
        {
            if (!ValidateCardUsage(playerId, cardId))
            {
                return false;
            }

            try
            {
                // ��ȡ��������
                var cardData = cardConfig.GetCardById(cardId);
                if (cardData == null)
                {
                    LogDebug($"δ�ҵ�����: {cardId}");
                    return false;
                }

                // ������������Ƴ��������ʹ��
                var cardState = playerCardStates[playerId];
                cardState.RemoveCard(cardId);
                cardState.MarkCardUsedThisRound();

                LogDebug($"��� {playerId} ʹ�ÿ���: {cardData.cardName}");

                // ����ʹ������
                var useRequest = new CardUseRequest
                {
                    userId = playerId,
                    cardId = cardId,
                    targetPlayerId = targetPlayerId,
                    timestamp = Time.time
                };

                // ��������ʹ���¼�
                CardEvents.OnCardUseRequested?.Invoke(useRequest, cardData);
                OnCardUsed?.Invoke(playerId, cardId, targetPlayerId);

                // TODO: ִ�п���Ч������CardEffectSystemʵ�ֺ�
                // ����Ӧ�õ���CardEffectSystem��ִ��ʵ��Ч��

                LogDebug($"���� {cardData.cardName} ʹ�óɹ�");

                // �����Ƴ��¼�
                CardEvents.OnCardRemovedFromHand?.Invoke(playerId, cardId);

                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PlayerCardManager] ʹ�ÿ���ʧ��: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// ��֤����ʹ���Ƿ���Ч
        /// </summary>
        private bool ValidateCardUsage(int playerId, int cardId)
        {
            if (!playerCardStates.ContainsKey(playerId))
            {
                LogDebug($"��� {playerId} �Ŀ���״̬������");
                return false;
            }

            var cardState = playerCardStates[playerId];

            if (!cardState.canUseCardThisRound)
            {
                LogDebug($"��� {playerId} ���غ���ʹ�ù�����");
                return false;
            }

            if (!cardState.HasCard(cardId))
            {
                LogDebug($"��� {playerId} ������û�п���: {cardId}");
                return false;
            }

            // ��鿨���Ƿ������ڷ��Լ��غ�ʹ��
            var cardData = cardConfig.GetCardById(cardId);
            if (cardData != null && !cardData.canUseWhenNotMyTurn && currentTurnPlayerId != playerId)
            {
                LogDebug($"���� {cardData.cardName} �������ڷ��Լ��غ�ʹ��");
                return false;
            }

            return true;
        }

        #endregion

        #region ���ƻ��

        /// <summary>
        /// �������ӿ���
        /// </summary>
        /// <param name="playerId">���ID</param>
        /// <param name="cardId">����ID��0��ʾ�����ã�</param>
        /// <returns>�Ƿ�ɹ����</returns>
        public bool GiveCardToPlayer(int playerId, int cardId = 0)
        {
            if (!playerCardStates.ContainsKey(playerId))
            {
                LogDebug($"��� {playerId} �Ŀ���״̬������");
                return false;
            }

            var cardState = playerCardStates[playerId];

            CardData cardData;
            if (cardId == 0)
            {
                // �����ÿ���
                cardData = DrawRandomCard();
                if (cardData == null)
                {
                    LogDebug("�����ȡ����ʧ��");
                    return false;
                }
            }
            else
            {
                // ���ָ������
                cardData = cardConfig.GetCardById(cardId);
                if (cardData == null)
                {
                    LogDebug($"δ�ҵ�ָ������: {cardId}");
                    return false;
                }
            }

            // ����Ƿ������ӣ����������͹����飩
            if (!cardState.CanAddSpecificCard(cardData.cardId))
            {
                LogDebug($"��� {playerId} �޷���ӿ��� {cardData.cardName}������������Υ������");
                return false;
            }

            // ��ӵ��������
            cardState.AddCard(cardData.cardId);

            LogDebug($"��� {playerId} ��ÿ���: {cardData.cardName}");

            // �������ƻ���¼�
            OnCardAcquired?.Invoke(playerId, cardData.cardId, cardData.cardName);
            CardEvents.OnCardAddedToHand?.Invoke(playerId, cardData.cardId);

            return true;
        }

        /// <summary>
        /// �����ȡ����
        /// </summary>
        private CardData DrawRandomCard()
        {
            if (cardConfig == null || cardConfig.AllCards.Count == 0)
            {
                return null;
            }

            // ����Ȩ�ص������ȡ
            float totalWeight = 0f;
            foreach (var card in cardConfig.AllCards)
            {
                totalWeight += card.drawWeight;
            }

            float randomValue = Random.Range(0f, totalWeight);
            float currentWeight = 0f;

            foreach (var card in cardConfig.AllCards)
            {
                currentWeight += card.drawWeight;
                if (randomValue <= currentWeight)
                {
                    return card;
                }
            }

            // ���û��ѡ�У����ص�һ�ſ���
            return cardConfig.AllCards[0];
        }

        /// <summary>
        /// ת�ƿ��ƣ���һ����ҵ���һ����ң�
        /// </summary>
        public bool TransferCard(int fromPlayerId, int toPlayerId, int cardId)
        {
            if (!playerCardStates.ContainsKey(fromPlayerId) || !playerCardStates.ContainsKey(toPlayerId))
            {
                LogDebug("Դ��һ�Ŀ�����״̬������");
                return false;
            }

            var fromState = playerCardStates[fromPlayerId];
            var toState = playerCardStates[toPlayerId];

            // ��֤Դ����д˿���
            if (!fromState.HasCard(cardId))
            {
                LogDebug($"��� {fromPlayerId} û�п���: {cardId}");
                return false;
            }

            // ��֤Ŀ������Ƿ���Խ��տ���
            if (!toState.CanAddSpecificCard(cardId))
            {
                LogDebug($"��� {toPlayerId} �޷����տ���: {cardId}");
                return false;
            }

            // ִ��ת��
            fromState.RemoveCard(cardId);
            toState.AddCard(cardId);

            var cardData = cardConfig.GetCardById(cardId);
            LogDebug($"����ת�Ƴɹ�: {cardData?.cardName} �����{fromPlayerId}ת�Ƶ����{toPlayerId}");

            // ����ת���¼�
            OnCardTransferred?.Invoke(fromPlayerId, toPlayerId, cardId);
            CardEvents.OnCardRemovedFromHand?.Invoke(fromPlayerId, cardId);
            CardEvents.OnCardAddedToHand?.Invoke(toPlayerId, cardId);

            return true;
        }

        #endregion

        #region �غϹ���

        /// <summary>
        /// ���õ�ǰ�غ����
        /// </summary>
        /// <param name="playerId">��ǰ�غ����ID</param>
        public void SetCurrentTurnPlayer(int playerId)
        {
            currentTurnPlayerId = playerId;
            LogDebug($"��ǰ�غ��������Ϊ: {playerId}");
        }

        /// <summary>
        /// ������ҵĿ���ʹ�û��ᣨ���˻غ��ƣ�
        /// Ӧ����Ҵ�����ɺ����
        /// </summary>
        /// <param name="playerId">���ID</param>
        public void ResetPlayerUsageOpportunity(int playerId)
        {
            if (!playerCardStates.ContainsKey(playerId))
            {
                LogDebug($"��� {playerId} �Ŀ���״̬������");
                return;
            }

            var cardState = playerCardStates[playerId];
            cardState.ResetForNewRound();

            LogDebug($"��� {playerId} �Ŀ���ʹ�û���������");

            // ���������¼�
            OnUsageOpportunityReset?.Invoke(playerId);
            CardEvents.OnPlayerCardUsageReset?.Invoke(playerId);
        }

        /// <summary>
        /// ����������ҵĿ���ʹ�û���
        /// </summary>
        public void ResetAllPlayersUsageOpportunity()
        {
            foreach (var playerId in playerCardStates.Keys.ToList())
            {
                ResetPlayerUsageOpportunity(playerId);
            }
            LogDebug("������ҵĿ���ʹ�û���������");
        }

        #endregion

        #region ��ѯ����

        /// <summary>
        /// ��ȡ�������ID�б�
        /// </summary>
        public List<int> GetPlayerHand(int playerId)
        {
            if (playerCardStates.ContainsKey(playerId))
            {
                return playerCardStates[playerId].GetHandCards();
            }
            return new List<int>();
        }

        /// <summary>
        /// ��ȡ������Ƶ�CardData�б�
        /// </summary>
        public List<CardData> GetPlayerHandCards(int playerId)
        {
            var handCardIds = GetPlayerHand(playerId);
            var handCards = new List<CardData>();

            foreach (var cardId in handCardIds)
            {
                var cardData = cardConfig.GetCardById(cardId);
                if (cardData != null)
                {
                    handCards.Add(cardData);
                }
            }

            return handCards;
        }

        /// <summary>
        /// �������Ƿ����ʹ�ÿ���
        /// </summary>
        public bool CanPlayerUseCards(int playerId)
        {
            if (!playerCardStates.ContainsKey(playerId))
                return false;

            var cardState = playerCardStates[playerId];
            return cardState.canUseCardThisRound;
        }

        /// <summary>
        /// ��ȡ�����������
        /// </summary>
        public int GetPlayerHandCount(int playerId)
        {
            if (playerCardStates.ContainsKey(playerId))
            {
                return playerCardStates[playerId].HandCount;
            }
            return 0;
        }

        /// <summary>
        /// ��ȡ��ҿ���״̬ժҪ
        /// </summary>
        public string GetPlayerCardSummary(int playerId)
        {
            if (!playerCardStates.ContainsKey(playerId))
                return "���״̬������";

            var cardState = playerCardStates[playerId];
            return $"��� {cardState.playerName}: ������ {cardState.HandCount}/{maxHandSize}, " +
                   $"��ʹ��: {(cardState.canUseCardThisRound ? "��" : "��")}";
        }

        /// <summary>
        /// ��ȡ���״̬
        /// </summary>
        public EnhancedPlayerCardState GetPlayerState(int playerId)
        {
            return playerCardStates.ContainsKey(playerId) ? playerCardStates[playerId] : null;
        }

        #endregion

        #region ������ϵͳ����

        /// <summary>
        /// ������ɺ�Ļص������ɵ㣩
        /// Ӧ����HostGameManager.HandlePlayerAnswer�е���
        /// </summary>
        public void OnPlayerAnswerCompleted(int playerId)
        {
            // ���ø���ҵĿ���ʹ�û���
            ResetPlayerUsageOpportunity(playerId);

            LogDebug($"��� {playerId} ������ɣ�����ʹ�û���������");
        }

        #endregion

        #region ���Ժ͹��߷���

        /// <summary>
        /// ��ȡ������ҵĿ���״̬�������ã�
        /// </summary>
        public Dictionary<int, string> GetAllPlayerCardSummaries()
        {
            var summaries = new Dictionary<int, string>();
            foreach (var playerId in playerCardStates.Keys)
            {
                summaries[playerId] = GetPlayerCardSummary(playerId);
            }
            return summaries;
        }

        /// <summary>
        /// ǿ�Ƹ�������ָ�����ƣ������ã�
        /// </summary>
        public bool ForceGiveCard(int playerId, int cardId)
        {
            LogDebug($"[����] ǿ�Ƹ���� {playerId} ��ӿ���: {cardId}");
            return GiveCardToPlayer(playerId, cardId);
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerCardManager] {message}");
            }
        }

        #endregion

        #region �����ӿ�����

        public bool IsInitialized => isInitialized;
        public int PlayerCount => playerCardStates.Count;
        public int CurrentTurnPlayerId => currentTurnPlayerId;
        public CardConfig Config => cardConfig;

        #endregion
    }

    #region ��ǿ��PlayerCardState

    /// <summary>
    /// ��ǿ����ҿ���״̬
    /// ������ԭ��CardInventory�Ĺ���
    /// </summary>
    [System.Serializable]
    public class EnhancedPlayerCardState : PlayerCardState
    {
        private int maxHandSize;
        private CardDrawSettings drawSettings;

        public EnhancedPlayerCardState(int maxSize, CardDrawSettings settings)
        {
            maxHandSize = maxSize;
            drawSettings = settings;
            handCards = new List<int>();
        }

        /// <summary>
        /// ��������
        /// </summary>
        public int HandCount => handCards.Count;

        /// <summary>
        /// �Ƿ���������
        /// </summary>
        public bool IsFull => handCards.Count >= maxHandSize;

        /// <summary>
        /// ʣ������
        /// </summary>
        public int RemainingCapacity => maxHandSize - handCards.Count;

        /// <summary>
        /// ����Ƿ������ӿ��ƣ����ǻ���ļ�������飩
        /// </summary>
        public new bool CanAddCard => !IsFull;

        /// <summary>
        /// ����Ƿ�������ָ�����ƣ�����������֤��
        /// </summary>
        public bool CanAddSpecificCard(int cardId)
        {
            // �����������
            if (IsFull)
            {
                return false;
            }

            // �����ÿ���
            if (drawSettings.bannedCardIds.Contains(cardId))
            {
                return false;
            }

            // ����ظ�����
            if (!drawSettings.allowDuplicates && handCards.Contains(cardId))
            {
                return false;
            }

            // ���ͬ�ֿ�����������
            int cardCount = handCards.Count(id => id == cardId);
            if (cardCount >= drawSettings.maxSameCardInHand)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// ��ӿ���
        /// </summary>
        public bool AddCard(int cardId)
        {
            if (!CanAddSpecificCard(cardId))
            {
                return false;
            }

            handCards.Add(cardId);
            return true;
        }

        /// <summary>
        /// �Ƴ�����
        /// </summary>
        public bool RemoveCard(int cardId)
        {
            return handCards.Remove(cardId);
        }

        /// <summary>
        /// ����Ƿ�ӵ��ָ������
        /// </summary>
        public bool HasCard(int cardId)
        {
            return handCards.Contains(cardId);
        }

        /// <summary>
        /// ��ȡ���Ƹ���
        /// </summary>
        public List<int> GetHandCards()
        {
            return new List<int>(handCards);
        }

        /// <summary>
        /// ��ȡĳ�ֿ��Ƶ�����
        /// </summary>
        public int GetCardCount(int cardId)
        {
            return handCards.Count(id => id == cardId);
        }
    }

    #endregion
}