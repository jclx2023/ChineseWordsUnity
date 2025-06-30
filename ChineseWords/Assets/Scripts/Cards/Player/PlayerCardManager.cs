using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Core;
using Core.Network;
using Cards.Core;
using Cards.Effects;
using System;

namespace Cards.Player
{
    /// <summary>
    /// ��ҿ��ƹ����������°棩
    /// �������ÿ����ҵĿ���״̬��ʹ��Ȩ�ޡ����Ʋ�����
    /// ��������Ϸϵͳ���ɣ�ʵ�ָ��˻غ��ƵĿ��ƻ���
    /// ֧���µ�12�ſ���ϵͳ
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

        // ��ҿ���״̬����
        private Dictionary<int, EnhancedPlayerCardState> playerCardStates;

        // ��ǰ�غ���Ϣ
        private int currentTurnPlayerId = 0;
        private bool isInitialized = false;

        #region �¼�����
        public System.Action<int, int, int> OnCardUsed;
        public System.Action<int, int, string> OnCardAcquired;
        public System.Action<int> OnUsageOpportunityReset;
        public System.Action<int, int, int> OnCardTransferred;
        public System.Action<int, int> OnHandSizeChanged;

        #endregion

        #region Unity��������

        private void Awake()
        {
            LogDebug($"{GetType().Name} ����Ѵ������ȴ���������");
            playerCardStates = new Dictionary<int, EnhancedPlayerCardState>();
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
                if (cardConfig == null)
                {
                    LogDebug("���Դ�Resources����CardConfig...");

                    // ���Լ���
                    cardConfig = Resources.Load<CardConfig>("QuestionConfigs/CardConfig");

                    if (cardConfig == null)
                    {
                        // ���ԣ��г�Resources�ļ����е�������Դ
                        var allResources = Resources.LoadAll("QuestionConfigs");
                        LogDebug($"QuestionConfigs�ļ������ҵ�{allResources.Length}����Դ:");

                        foreach (var resource in allResources)
                        {
                            LogDebug($"- {resource.name} ({resource.GetType().Name})");
                        }

                        // ����ֱ�Ӽ�������CardConfig���͵���Դ
                        var allCardConfigs = Resources.LoadAll<CardConfig>("QuestionConfigs");
                        LogDebug($"�ҵ�{allCardConfigs.Length}��CardConfig���͵���Դ");

                        throw new InvalidOperationException("�޷�����CardConfig��Դ");
                    }
                    else
                    {
                        LogDebug("CardConfig���سɹ���");
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
                LogDebug($"����: ���������={maxHandSize}, ��ʼ������={initialCardCount}");
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
            OnHandSizeChanged = null;

            // �����������״̬�е��¼�����
            foreach (var cardState in playerCardStates.Values)
            {
                if (cardState is EnhancedPlayerCardState enhancedState)
                {
                    enhancedState.OnHandSizeChanged = null;
                }
            }

            LogDebug("��ȡ�������¼�����");
        }

        #endregion

        #region ��ҹ���

        /// <summary>
        /// ��ʼ����ҿ���״̬
        /// </summary>
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

            // �������Ʊ仯�¼�����
            cardState.OnHandSizeChanged += (pId, newSize) => {
                LogDebug($"��� {pId} ���������仯: {newSize}");
                OnHandSizeChanged?.Invoke(pId, newSize);
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
        /// ʹ�ÿ��ƣ����°� - ������Ч��ִ�����̣�
        /// </summary>
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

                // ������֤��ʹ��CardUtilities����������֤
                var playerState = GetPlayerState(playerId);
                bool isMyTurn = (currentTurnPlayerId == playerId);

                if (!CardUtilities.Validator.ValidatePlayerCanUseCard(playerId, cardData, playerState, isMyTurn))
                {
                    LogDebug($"CardUtilities��֤ʧ��: ���{playerId}�޷�ʹ�ÿ���{cardData.cardName}");
                    return false;
                }

                // ����ʹ������
                var useRequest = new CardUseRequest
                {
                    userId = playerId,
                    cardId = cardId,
                    targetPlayerId = targetPlayerId,
                    timestamp = Time.time
                };

                // �����Ŀ����֤�����ָ���Ϳ��ƣ�
                if (cardData.cardType == CardType.PlayerTarget)
                {
                    var alivePlayers = GetAllAlivePlayerIds();
                    if (!CardUtilities.Validator.ValidateTargetSelection(useRequest, cardData, alivePlayers))
                    {
                        LogDebug($"Ŀ��ѡ����֤ʧ��: ����{cardData.cardName}");
                        return false;
                    }
                }

                // ������������Ƴ��������ʹ��
                var cardState = playerCardStates[playerId];
                cardState.RemoveCard(cardId);
                cardState.MarkCardUsedThisRound();

                LogDebug($"��� {playerId} ʹ�ÿ���: {cardData.cardName} -> Ŀ��: {targetPlayerId}");

                // ��������ʹ���¼�
                CardEvents.OnCardUseRequested?.Invoke(useRequest, cardData);
                OnCardUsed?.Invoke(playerId, cardId, targetPlayerId);

                // ͨ��CardEffectSystemִ�п���Ч��
                if (CardEffectSystem.Instance != null)
                {
                    CardEffectSystem.Instance.UseCard(useRequest, cardData);
                }
                else
                {
                    Debug.LogWarning("[PlayerCardManager] CardEffectSystemʵ�������ڣ��޷�ִ�п���Ч��");
                }

                LogDebug($"���� {cardData.cardName} ʹ���������");

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
        /// ��֤����ʹ���Ƿ���Ч�����°棩
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

            // �������Ƿ���
            if (!IsPlayerAlive(playerId))
            {
                LogDebug($"��� {playerId} ���������޷�ʹ�ÿ���");
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
        /// �������ӿ��ƣ����°� - ֧�ֶ��ſ��ƣ�
        /// </summary>
        /// <param name="playerId">���ID</param>
        /// <param name="cardId">����ID��0��ʾ�����ã�</param>
        /// <param name="count">���������Ĭ��1�ţ�</param>
        /// <returns>�Ƿ�ɹ����</returns>
        public bool GiveCardToPlayer(int playerId, int cardId = 0, int count = 1)
        {
            if (!playerCardStates.ContainsKey(playerId))
            {
                LogDebug($"��� {playerId} �Ŀ���״̬������");
                return false;
            }

            var cardState = playerCardStates[playerId];
            int successCount = 0;

            for (int i = 0; i < count; i++)
            {
                CardData cardData;
                if (cardId == 0)
                {
                    // �����ÿ���
                    cardData = DrawRandomCard();
                    if (cardData == null)
                    {
                        LogDebug("�����ȡ����ʧ��");
                        break;
                    }
                }
                else
                {
                    // ���ָ������
                    cardData = cardConfig.GetCardById(cardId);
                    if (cardData == null)
                    {
                        LogDebug($"δ�ҵ�ָ������: {cardId}");
                        break;
                    }
                }

                // ����Ƿ�������
                if (!cardState.CanAddSpecificCard(cardData.cardId))
                {
                    LogDebug($"��� {playerId} �޷���ӵ�{i + 1}�ſ��� {cardData.cardName}������������Υ������");
                    break;
                }

                // ��ӵ��������
                cardState.AddCard(cardData.cardId);
                successCount++;

                LogDebug($"��� {playerId} ��ÿ���: {cardData.cardName} ({successCount}/{count})");

                // �������ƻ���¼�
                OnCardAcquired?.Invoke(playerId, cardData.cardId, cardData.cardName);
                CardEvents.OnCardAddedToHand?.Invoke(playerId, cardData.cardId);
            }

            LogDebug($"��� {playerId} �ɹ���� {successCount}/{count} �ſ���");
            return successCount > 0;
        }

        /// <summary>
        /// �����ȡ���ƣ����°� - ʹ��CardUtilities��
        /// </summary>
        private CardData DrawRandomCard()
        {
            if (cardConfig == null || cardConfig.AllCards.Count == 0)
            {
                return null;
            }

            // ʹ��CardUtilities�ĳ鿨����
            return CardUtilities.DrawRandomCard(cardConfig.AllCards);
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

            // ��֤��Ҵ��״̬
            if (!IsPlayerAlive(fromPlayerId) || !IsPlayerAlive(toPlayerId))
            {
                LogDebug("Դ��һ�Ŀ��������������޷�ת�ƿ���");
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

        #region ��ѯ���������°� - ����CardGameBridge��

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
            return cardState.canUseCardThisRound && IsPlayerAlive(playerId);
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
            bool isAlive = IsPlayerAlive(playerId);
            return $"��� {cardState.playerName}: ������ {cardState.HandCount}/{maxHandSize}, " +
                   $"��ʹ��: {(cardState.canUseCardThisRound && isAlive ? "��" : "��")}, " +
                   $"���: {(isAlive ? "��" : "��")}";
        }

        /// <summary>
        /// ��ȡ���״̬
        /// </summary>
        public EnhancedPlayerCardState GetPlayerState(int playerId)
        {
            return playerCardStates.ContainsKey(playerId) ? playerCardStates[playerId] : null;
        }

        /// <summary>
        /// �������Ƿ������CardGameBridge��
        /// </summary>
        private bool IsPlayerAlive(int playerId)
        {
            // ����ʹ��CardGameBridge��ѯ
            if (Cards.Integration.CardGameBridge.Instance != null)
            {
                return Cards.Integration.CardGameBridge.IsPlayerAlive(playerId);
            }

            // ���ף�������Ҵ���û��CardGameBridge������£�
            return true;
        }

        /// <summary>
        /// ��ȡ���д�����ID�б�����CardGameBridge��
        /// </summary>
        private List<int> GetAllAlivePlayerIds()
        {
            // ����ʹ��CardGameBridge��ѯ
            if (Cards.Integration.CardGameBridge.Instance != null)
            {
                return Cards.Integration.CardGameBridge.GetAllAlivePlayerIds();
            }

            // ���ף�����������ע������ID
            return playerCardStates.Keys.ToList();
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
        /// ��ȡϵͳ״̬��Ϣ�������ã�
        /// </summary>
        public string GetSystemStatus()
        {
            var status = "=== PlayerCardManager״̬ ===\n";
            status += $"��ʼ��״̬: {isInitialized}\n";
            status += $"�������: {playerCardStates.Count}\n";
            status += $"��ǰ�غ����: {currentTurnPlayerId}\n";
            status += $"���������: {maxHandSize}\n";
            status += $"��ʼ������: {initialCardCount}\n";
            status += $"����ͬ��: {enableNetworkSync}\n";

            if (cardConfig != null)
            {
                status += $"��������: {cardConfig.AllCards.Count}�ſ���\n";
            }

            if (Cards.Integration.CardGameBridge.Instance != null)
            {
                status += "CardGameBridge: ������\n";
            }
            else
            {
                status += "CardGameBridge: δ����\n";
            }

            return status;
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

        // ����¼�ί������֪ͨ���Ʊ仯
        public System.Action<int, int> OnHandSizeChanged;

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
        /// ��ӿ��ƣ������Ʊ仯֪ͨ��
        /// </summary>
        public bool AddCard(int cardId)
        {
            if (!CanAddSpecificCard(cardId))
            {
                return false;
            }

            int oldHandSize = handCards.Count;
            handCards.Add(cardId);
            int newHandSize = handCards.Count;

            // �������������仯�¼�
            if (newHandSize != oldHandSize)
            {
                OnHandSizeChanged?.Invoke(playerId, newHandSize);
            }

            return true;
        }

        /// <summary>
        /// �Ƴ����ƣ������Ʊ仯֪ͨ��
        /// </summary>
        public bool RemoveCard(int cardId)
        {
            int oldHandSize = handCards.Count;
            bool removed = handCards.Remove(cardId);
            int newHandSize = handCards.Count;

            // �������������仯�¼�
            if (removed && newHandSize != oldHandSize)
            {
                OnHandSizeChanged?.Invoke(playerId, newHandSize);
            }

            return removed;
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