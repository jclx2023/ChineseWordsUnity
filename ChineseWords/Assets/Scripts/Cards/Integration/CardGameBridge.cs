using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Core;
using Core.Network;
using Cards.Core;
using Cards.Effects;
using Cards.Player;
using Managers;

namespace Cards.Integration
{
    /// <summary>
    /// ������Ϸ�Ž���
    /// �������ӿ���ϵͳ��������Ϸϵͳ��ʵ�ֿ���Ч���ľ���ִ��
    /// </summary>
    public class CardGameBridge : MonoBehaviour
    {
        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        // ����ʵ��
        public static CardGameBridge Instance { get; private set; }

        #region ϵͳ����

        private PlayerHPManager hpManager;
        private TimerManager timerManager;
        private HostGameManager hostGameManager;
        private NetworkManager networkManager;
        private CardConfig cardConfig;
        private CardEffectSystem effectSystem;
        private PlayerCardManager playerCardManager;

        #endregion

        #region Ч��״̬����

        // ������Ч��״̬
        private Dictionary<int, float> playerTimeBonuses;      // ���ID -> ʱ��ӳ�
        private Dictionary<int, float> playerTimePenalties;    // ���ID -> ʱ�����
        private Dictionary<int, bool> playerSkipFlags;         // ���ID -> �������
        private Dictionary<int, string> playerQuestionTypes;   // ���ID -> ָ����Ŀ����
        private Dictionary<int, int> playerAnswerDelegates;    // ���ID -> ���������ID
        private Dictionary<int, bool> playerExtraHints;        // ���ID -> ������ʾ���
        private float globalDamageMultiplier = 1.0f;           // ȫ���˺�����

        #endregion

        #region Unity��������

        private void Awake()
        {
            // ����ģʽ
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                InitializeStateCaches();
                LogDebug("CardGameBridgeʵ���Ѵ���");
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
        /// ��ʼ��״̬����
        /// </summary>
        private void InitializeStateCaches()
        {
            playerTimeBonuses = new Dictionary<int, float>();
            playerTimePenalties = new Dictionary<int, float>();
            playerSkipFlags = new Dictionary<int, bool>();
            playerQuestionTypes = new Dictionary<int, string>();
            playerAnswerDelegates = new Dictionary<int, int>();
            playerExtraHints = new Dictionary<int, bool>();
        }

        /// <summary>
        /// ��ʼ���Ž���
        /// </summary>
        public void Initialize()
        {
            LogDebug("��ʼ��ʼ��CardGameBridge");

            // ��ȡϵͳ����
            if (!AcquireSystemReferences())
            {
                Debug.LogError("[CardGameBridge] ϵͳ���û�ȡʧ��");
                return;
            }

            // ע��Ч����Ч��ϵͳ
            RegisterEffectsToSystem();

            // �����¼�
            SubscribeToEvents();

            LogDebug("CardGameBridge��ʼ�����");
        }

        /// <summary>
        /// ��ȡϵͳ����
        /// </summary>
        private bool AcquireSystemReferences()
        {
            bool allReferencesAcquired = true;

            // ��ȡPlayerCardManager
            playerCardManager = PlayerCardManager.Instance;
            if (playerCardManager == null)
            {
                Debug.LogError("[CardGameBridge] �޷���ȡPlayerCardManagerʵ��");
                allReferencesAcquired = false;
            }

            // ��ȡCardEffectSystem
            effectSystem = CardEffectSystem.Instance;
            if (effectSystem == null)
            {
                Debug.LogError("[CardGameBridge] �޷���ȡCardEffectSystemʵ��");
                allReferencesAcquired = false;
            }

            // ��ȡCardConfig
            cardConfig = playerCardManager?.Config;
            if (cardConfig == null)
            {
                cardConfig = Resources.Load<CardConfig>("CardConfig");
                if (cardConfig == null)
                {
                    Debug.LogError("[CardGameBridge] �޷���ȡCardConfig");
                    allReferencesAcquired = false;
                }
            }

            // ��ȡ����������������Ϊ�գ�����ʱ��ȡ��
            // PlayerHPManager����ͨ�࣬ͨ��HostGameManager��ȡ
            hostGameManager = HostGameManager.Instance;
            if (hostGameManager != null)
            {
                // ͨ�������ȡHostGameManager��hpManager�ֶ�
                var hpManagerField = hostGameManager.GetType().GetField("hpManager",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (hpManagerField != null)
                {
                    hpManager = hpManagerField.GetValue(hostGameManager) as PlayerHPManager;
                }
            }

            timerManager = FindObjectOfType<TimerManager>();
            networkManager = NetworkManager.Instance;

            LogDebug($"ϵͳ���û�ȡ��� - HP:{hpManager != null}, Timer:{timerManager != null}, Host:{hostGameManager != null}, Network:{networkManager != null}");
            return allReferencesAcquired;
        }

        /// <summary>
        /// ע��Ч����Ч��ϵͳ
        /// </summary>
        private void RegisterEffectsToSystem()
        {
            if (effectSystem == null)
            {
                Debug.LogError("[CardGameBridge] Ч��ϵͳδ��ʼ�����޷�ע��Ч��");
                return;
            }

            // ʹ��CardEffectRegistrarע������Ч��
            CardEffectRegistrar.RegisterAllEffects(effectSystem);
            LogDebug("���п���Ч����ע�ᵽЧ��ϵͳ");
        }

        /// <summary>
        /// �����¼�
        /// </summary>
        private void SubscribeToEvents()
        {
            // ���Ļغ�����¼����������ҵ���ʱЧ��
            if (playerCardManager != null)
            {
                playerCardManager.OnUsageOpportunityReset += OnPlayerTurnCompleted;
            }

            LogDebug("�Ѷ�������¼�");
        }

        /// <summary>
        /// ȡ�������¼�
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (playerCardManager != null)
            {
                playerCardManager.OnUsageOpportunityReset -= OnPlayerTurnCompleted;
            }

            LogDebug("��ȡ���¼�����");
        }

        #endregion

        #region ϵͳ���ʴ��� - ��CardEffectsʹ��

        /// <summary>
        /// �޸��������ֵ
        /// </summary>
        public static bool ModifyPlayerHealth(int playerId, int healthChange)
        {
            if (Instance?.hpManager == null)
            {
                Instance?.LogDebug("PlayerHPManager�����ã��޷��޸�����ֵ");
                return false;
            }

            bool success = false;
            if (healthChange > 0)
            {
                // ��Ѫ
                success = Instance.hpManager.HealPlayer((ushort)playerId, healthChange, out int newHealth);
                Instance?.LogDebug($"���{playerId}��Ѫ{healthChange}�㣬��Ѫ��:{newHealth}���ɹ�:{success}");
            }
            else if (healthChange < 0)
            {
                // ��Ѫ
                success = Instance.hpManager.ApplyDamage((ushort)playerId, out int newHealth, out bool isDead, -healthChange);
                Instance?.LogDebug($"���{playerId}��Ѫ{-healthChange}�㣬��Ѫ��:{newHealth}������:{isDead}���ɹ�:{success}");
            }

            return success;
        }

        /// <summary>
        /// �������ʱ��ӳ�
        /// </summary>
        public static void SetPlayerTimeBonus(int playerId, float bonusTime)
        {
            if (Instance == null) return;

            Instance.playerTimeBonuses[playerId] = bonusTime;
            Instance.LogDebug($"�������{playerId}ʱ��ӳ�:{bonusTime}��");
        }

        /// <summary>
        /// �������ʱ�����
        /// </summary>
        public static void SetPlayerTimePenalty(int playerId, float penaltyTime)
        {
            if (Instance == null) return;

            Instance.playerTimePenalties[playerId] = penaltyTime;
            Instance.LogDebug($"�������{playerId}ʱ�����:{penaltyTime}��");
        }

        /// <summary>
        /// ��������������
        /// </summary>
        public static void SetPlayerSkipFlag(int playerId, bool shouldSkip = true)
        {
            if (Instance == null) return;

            Instance.playerSkipFlags[playerId] = shouldSkip;
            Instance.LogDebug($"�������{playerId}�������:{shouldSkip}");
        }

        /// <summary>
        /// ��������´���Ŀ����
        /// </summary>
        public static void SetPlayerNextQuestionType(int playerId, string questionType)
        {
            if (Instance == null) return;

            Instance.playerQuestionTypes[playerId] = questionType;
            Instance.LogDebug($"�������{playerId}�´���Ŀ����:{questionType}");
        }

        /// <summary>
        /// ���ô������
        /// </summary>
        public static void SetAnswerDelegate(int originalPlayerId, int delegatePlayerId)
        {
            if (Instance == null) return;

            Instance.playerAnswerDelegates[originalPlayerId] = delegatePlayerId;
            Instance.LogDebug($"�������{originalPlayerId}�Ĵ������Ϊ���{delegatePlayerId}");
        }

        /// <summary>
        /// ������Ҷ�����ʾ
        /// </summary>
        public static void SetPlayerExtraHint(int playerId, bool hasExtraHint = true)
        {
            if (Instance == null) return;

            Instance.playerExtraHints[playerId] = hasExtraHint;
            Instance.LogDebug($"�������{playerId}������ʾ:{hasExtraHint}");
        }

        /// <summary>
        /// ����ȫ���˺�����
        /// </summary>
        public static void SetGlobalDamageMultiplier(float multiplier)
        {
            if (Instance == null) return;

            Instance.globalDamageMultiplier = multiplier;
            Instance.LogDebug($"����ȫ���˺�����:{multiplier}");
        }

        /// <summary>
        /// �������ӿ���
        /// </summary>
        public static bool GiveCardToPlayer(int playerId, int cardId = 0, int count = 1)
        {
            if (Instance?.playerCardManager == null) return false;

            bool success = true;
            for (int i = 0; i < count; i++)
            {
                if (!Instance.playerCardManager.GiveCardToPlayer(playerId, cardId))
                {
                    success = false;
                    break;
                }
            }

            Instance?.LogDebug($"�����{playerId}���{count}�ſ���(ID:{cardId})���ɹ�:{success}");
            return success;
        }

        /// <summary>
        /// ͵ȡ��ҿ���
        /// </summary>
        public static int? StealRandomCardFromPlayer(int fromPlayerId, int toPlayerId)
        {
            if (Instance?.playerCardManager == null) return null;

            var fromPlayerHand = Instance.playerCardManager.GetPlayerHand(fromPlayerId);
            if (fromPlayerHand.Count == 0)
            {
                Instance?.LogDebug($"���{fromPlayerId}û�п�͵ȡ�Ŀ���");
                return null;
            }

            // ���ѡ��һ�ſ���
            int randomIndex = Random.Range(0, fromPlayerHand.Count);
            int stolenCardId = fromPlayerHand[randomIndex];

            // ִ��ת��
            bool success = Instance.playerCardManager.TransferCard(fromPlayerId, toPlayerId, stolenCardId);

            if (success)
            {
                Instance?.LogDebug($"�ɹ������{fromPlayerId}͵ȡ����{stolenCardId}�����{toPlayerId}");
                return stolenCardId;
            }
            else
            {
                Instance?.LogDebug($"͵ȡ����ʧ��");
                return null;
            }
        }

        #endregion

        #region Ч��״̬��ѯ - ����Ϸϵͳʹ��

        /// <summary>
        /// ��ȡ���ʱ��������ӳ�-���ɣ�
        /// </summary>
        public float GetPlayerTimeAdjustment(int playerId)
        {
            float bonus = playerTimeBonuses.ContainsKey(playerId) ? playerTimeBonuses[playerId] : 0f;
            float penalty = playerTimePenalties.ContainsKey(playerId) ? playerTimePenalties[playerId] : 0f;
            return bonus - penalty;
        }

        /// <summary>
        /// �������Ƿ�Ӧ������
        /// </summary>
        public bool ShouldPlayerSkip(int playerId)
        {
            return playerSkipFlags.ContainsKey(playerId) && playerSkipFlags[playerId];
        }

        /// <summary>
        /// ��ȡ���ָ������Ŀ����
        /// </summary>
        public string GetPlayerSpecifiedQuestionType(int playerId)
        {
            return playerQuestionTypes.ContainsKey(playerId) ? playerQuestionTypes[playerId] : null;
        }

        /// <summary>
        /// ��ȡ��ҵĴ������
        /// </summary>
        public int? GetPlayerAnswerDelegate(int playerId)
        {
            return playerAnswerDelegates.ContainsKey(playerId) ? playerAnswerDelegates[playerId] : null;
        }

        /// <summary>
        /// �������Ƿ��ж�����ʾ
        /// </summary>
        public bool HasPlayerExtraHint(int playerId)
        {
            return playerExtraHints.ContainsKey(playerId) && playerExtraHints[playerId];
        }

        /// <summary>
        /// ��ȡ��ǰ�˺�����
        /// </summary>
        public float GetCurrentDamageMultiplier()
        {
            return globalDamageMultiplier;
        }

        #endregion

        #region ��Ϸ���̼��ɹ���

        /// <summary>
        /// �ڼ�ʱ������ǰ���ã�Ӧ��ʱ�����
        /// </summary>
        public void OnTimerStarting(int playerId, ref float timeLimit)
        {
            float adjustment = GetPlayerTimeAdjustment(playerId);
            if (adjustment != 0)
            {
                timeLimit += adjustment;
                LogDebug($"���{playerId}ʱ�����:{adjustment}�룬����ʱ��:{timeLimit}��");

                // ���ʱ��Ч����һ������Ч��
                playerTimeBonuses.Remove(playerId);
                playerTimePenalties.Remove(playerId);
            }
        }

        /// <summary>
        /// �ڴ��⿪ʼǰ���ã���������ʹ���
        /// </summary>
        public bool OnQuestionStarting(int playerId, out int actualAnswerPlayerId)
        {
            actualAnswerPlayerId = playerId;

            // �������
            if (ShouldPlayerSkip(playerId))
            {
                LogDebug($"���{playerId}��������");
                playerSkipFlags.Remove(playerId); // ����������
                return false; // ����false��ʾ����
            }

            // ���������
            var delegatePlayer = GetPlayerAnswerDelegate(playerId);
            if (delegatePlayer.HasValue)
            {
                actualAnswerPlayerId = delegatePlayer.Value;
                LogDebug($"���{playerId}�Ĵ������Ϊ���{actualAnswerPlayerId}");
                playerAnswerDelegates.Remove(playerId); // ���������
            }

            return true; // ����true��ʾ��������
        }

        /// <summary>
        /// ���˺�����ǰ���ã�Ӧ���˺�����
        /// </summary>
        public int OnDamageCalculating(int originalDamage)
        {
            if (globalDamageMultiplier != 1.0f)
            {
                int modifiedDamage = Mathf.RoundToInt(originalDamage * globalDamageMultiplier);
                LogDebug($"�˺�����Ӧ��:{originalDamage} x {globalDamageMultiplier} = {modifiedDamage}");

                // �����˺�������һ������Ч��
                globalDamageMultiplier = 1.0f;
                return modifiedDamage;
            }

            return originalDamage;
        }

        /// <summary>
        /// ����Ŀ����ǰ���ã����ָ����Ŀ����
        /// </summary>
        public string OnQuestionTypeSelecting(int playerId)
        {
            string specifiedType = GetPlayerSpecifiedQuestionType(playerId);
            if (!string.IsNullOrEmpty(specifiedType))
            {
                LogDebug($"���{playerId}ָ����Ŀ����:{specifiedType}");
                playerQuestionTypes.Remove(playerId); // ���ָ�����ͣ�һ������Ч��
                return specifiedType;
            }

            return null; // ����null��ʾ�������ѡ��
        }

        #endregion

        #region ���״̬��ѯ - ��CardEffectSystemʹ��

        /// <summary>
        /// ��ȡ���д�����ID
        /// </summary>
        public static List<int> GetAllAlivePlayerIds()
        {
            if (Instance?.hpManager == null)
            {
                Instance?.LogDebug("PlayerHPManager�����ã����ؿ��б�");
                return new List<int>();
            }

            var alivePlayerIds = Instance.hpManager.GetAlivePlayerIds();
            var result = new List<int>();

            foreach (var playerId in alivePlayerIds)
            {
                result.Add((int)playerId);
            }

            Instance?.LogDebug($"��ȡ��{result.Count}��������");
            return result;
        }

        /// <summary>
        /// ����ID��ȡ��������
        /// </summary>
        public static CardData GetCardDataById(int cardId)
        {
            if (Instance?.cardConfig == null)
            {
                Instance?.LogDebug("CardConfig�����ã��޷���ȡ��������");
                return null;
            }

            var cardData = Instance.cardConfig.GetCardById(cardId);
            Instance?.LogDebug($"��ȡ��������:{cardId} -> {cardData?.cardName ?? "δ�ҵ�"}");
            return cardData;
        }

        /// <summary>
        /// �������Ƿ���
        /// </summary>
        public static bool IsPlayerAlive(int playerId)
        {
            if (Instance?.hpManager == null) return false;
            return Instance.hpManager.IsPlayerAlive((ushort)playerId);
        }

        /// <summary>
        /// ��ȡ�������ID�����������ģ�
        /// </summary>
        public static List<int> GetAllPlayerIds()
        {
            if (Instance?.playerCardManager == null) return new List<int>();

            var playerStates = Instance.playerCardManager.GetAllPlayerCardSummaries();
            return playerStates.Keys.ToList();
        }

        #endregion

        #region �¼�����

        /// <summary>
        /// ��һغ���ɴ���
        /// </summary>
        private void OnPlayerTurnCompleted(int playerId)
        {
            // �������ҵ�һ����Ч��������еĻ���
            // ע�⣺�󲿷�Ч��Ӧ����ʹ��ʱ����������ֻ�Ǳ���
            LogDebug($"���{playerId}�غ���ɣ��������״̬");
        }

        #endregion

        #region ����ͬ��֧��

        /// <summary>
        /// �㲥����ʹ����Ϣ����"��ʦ����"����ʹ�ã�
        /// </summary>
        public static void BroadcastCardUsage(int playerId, int cardId, int targetPlayerId, string cardName)
        {
            if (Instance?.networkManager == null) return;

            string message = $"���{playerId}ʹ����{cardName}";
            if (targetPlayerId > 0)
            {
                message += $"��Ŀ�������{targetPlayerId}";
            }

            Instance.LogDebug($"�㲥����ʹ����Ϣ: {message}");
            // TODO: ͨ��NetworkManager�㲥����ʹ����Ϣ
            // Instance.networkManager.BroadcastCardUsageMessage(playerId, cardId, targetPlayerId, message);
        }

        #endregion

        #region ���������

        /// <summary>
        /// ��������Ч��״̬
        /// </summary>
        public void ClearAllEffectStates()
        {
            playerTimeBonuses.Clear();
            playerTimePenalties.Clear();
            playerSkipFlags.Clear();
            playerQuestionTypes.Clear();
            playerAnswerDelegates.Clear();
            playerExtraHints.Clear();
            globalDamageMultiplier = 1.0f;

            LogDebug("����Ч��״̬������");
        }

        /// <summary>
        /// ����ָ����ҵ�Ч��״̬
        /// </summary>
        public void ClearPlayerEffectStates(int playerId)
        {
            playerTimeBonuses.Remove(playerId);
            playerTimePenalties.Remove(playerId);
            playerSkipFlags.Remove(playerId);
            playerQuestionTypes.Remove(playerId);
            playerAnswerDelegates.Remove(playerId);
            playerExtraHints.Remove(playerId);

            LogDebug($"���{playerId}��Ч��״̬������");
        }

        #endregion

        #region ���Ժ�״̬��ѯ

        /// <summary>
        /// ��ȡ��ǰЧ��״̬ժҪ
        /// </summary>
        public string GetEffectStatesSummary()
        {
            var summary = "=== ����Ч��״̬ ===\n";
            summary += $"ʱ��ӳ�: {playerTimeBonuses.Count}��\n";
            summary += $"ʱ�����: {playerTimePenalties.Count}��\n";
            summary += $"�������: {playerSkipFlags.Count}��\n";
            summary += $"��Ŀ����ָ��: {playerQuestionTypes.Count}��\n";
            summary += $"�������: {playerAnswerDelegates.Count}��\n";
            summary += $"������ʾ: {playerExtraHints.Count}��\n";
            summary += $"ȫ���˺�����: {globalDamageMultiplier}\n";

            return summary;
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[CardGameBridge] {message}");
            }
        }

        #endregion

        #region �����ӿ�

        /// <summary>
        /// ǿ��ˢ��ϵͳ���ã�����ʱʹ�ã�
        /// </summary>
        public void RefreshSystemReferences()
        {
            LogDebug("ˢ��ϵͳ����");
            AcquireSystemReferences();
        }

        /// <summary>
        /// ����Ž����Ƿ�׼������
        /// </summary>
        public bool IsReady()
        {
            return playerCardManager != null && effectSystem != null && cardConfig != null;
        }

        #endregion
    }
}