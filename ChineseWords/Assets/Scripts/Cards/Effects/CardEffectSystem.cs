using System;
using System.Collections.Generic;
using UnityEngine;
using Cards.Core;

namespace Cards.Effects
{
    /// <summary>
    /// ����Ч��ϵͳ���ģ��򻯰棩
    /// ����Ч����ע���ִ�У�Ŀ��ѡ����UI�㴦��
    /// </summary>
    public class CardEffectSystem : MonoBehaviour
    {
        [Header("ϵͳ����")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private float effectExecutionTimeout = 10f;

        // Ч��ע���
        private Dictionary<EffectType, ICardEffect> registeredEffects;

        // Ч��ִ����
        private CardEffectExecutor effectExecutor;

        // ����ʵ��
        public static CardEffectSystem Instance { get; private set; }

        #region ��������

        private void Awake()
        {
            // ����ģʽ
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeSystem();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                UnregisterEvents();
                Instance = null;
            }
        }

        #endregion

        #region ϵͳ��ʼ��

        /// <summary>
        /// ��ʼ��Ч��ϵͳ
        /// </summary>
        private void InitializeSystem()
        {
            LogDebug("��ʼ������Ч��ϵͳ");

            // ��ʼ�����
            registeredEffects = new Dictionary<EffectType, ICardEffect>();
            effectExecutor = new CardEffectExecutor();

            // �������
            effectExecutor.SetTimeout(effectExecutionTimeout);

            // ע��Ĭ��Ч��������CardEffects.cs��ʵ�֣�
            RegisterDefaultEffects();

            // ע���¼�
            RegisterEvents();

            LogDebug("����Ч��ϵͳ��ʼ�����");
        }

        /// <summary>
        /// ע��Ĭ��Ч��
        /// </summary>
        private void RegisterDefaultEffects()
        {
            // ����ע������Ĭ��Ч��
            // ����Ч��ʵ�ֽ���CardEffects.cs�ж���
            LogDebug("ע��Ĭ��Ч������CardEffects.csʵ�֣�");
        }

        /// <summary>
        /// ע���¼�
        /// </summary>
        private void RegisterEvents()
        {
            CardEvents.OnCardUseRequested += HandleCardUseRequest;
        }

        /// <summary>
        /// ע���¼�
        /// </summary>
        private void UnregisterEvents()
        {
            CardEvents.OnCardUseRequested -= HandleCardUseRequest;
        }

        #endregion

        #region Ч��ע�����

        /// <summary>
        /// ע��Ч��ʵ��
        /// </summary>
        public void RegisterEffect(EffectType effectType, ICardEffect effect)
        {
            if (effect == null)
            {
                LogError($"����ע���Ч��: {effectType}");
                return;
            }

            if (registeredEffects.ContainsKey(effectType))
            {
                LogWarning($"Ч�� {effectType} �Ѵ��ڣ���������");
            }

            registeredEffects[effectType] = effect;
            LogDebug($"Ч����ע��: {effectType}");
        }

        /// <summary>
        /// ע��Ч��ʵ��
        /// </summary>
        public void UnregisterEffect(EffectType effectType)
        {
            if (registeredEffects.Remove(effectType))
            {
                LogDebug($"Ч����ע��: {effectType}");
            }
            else
            {
                LogWarning($"����ע�������ڵ�Ч��: {effectType}");
            }
        }

        /// <summary>
        /// ��ȡ��ע���Ч��
        /// </summary>
        public ICardEffect GetEffect(EffectType effectType)
        {
            registeredEffects.TryGetValue(effectType, out ICardEffect effect);
            return effect;
        }

        /// <summary>
        /// ���Ч���Ƿ���ע��
        /// </summary>
        public bool IsEffectRegistered(EffectType effectType)
        {
            return registeredEffects.ContainsKey(effectType);
        }

        #endregion

        #region Ч��ִ�����

        /// <summary>
        /// ʹ�ÿ��ƣ���Ҫ��ڣ�
        /// Ŀ����ͨ��UI��קȷ����ֱ��ִ��Ч��
        /// </summary>
        public void UseCard(CardUseRequest request, CardData cardData)
        {
            if (!ValidateCardUseRequest(request, cardData))
            {
                return;
            }

            LogDebug($"��ʼʹ�ÿ���: {cardData.cardName} (���{request.userId})");

            // ���ݿ�����������Ŀ��
            SetupCardTarget(request, cardData);

            // ֱ��ִ��Ч��
            ExecuteCardEffect(request, cardData);
        }

        /// <summary>
        /// ���ÿ���Ŀ��
        /// </summary>
        private void SetupCardTarget(CardUseRequest request, CardData cardData)
        {
            switch (cardData.cardType)
            {
                case CardType.SelfTarget:
                    // �Է��Ϳ��ƣ�Ŀ����ʹ�����Լ�
                    request.targetPlayerId = request.userId;
                    break;

                case CardType.PlayerTarget:
                    // ָ���Ϳ��ƣ�UI��Ӧ���Ѿ�������targetPlayerId
                    if (request.targetPlayerId <= 0)
                    {
                        LogError("ָ���Ϳ���ȱ����ЧĿ��");
                        return;
                    }
                    break;

                case CardType.Special:
                    // �����Ϳ��ƣ����ݾ���Ч������
                    // ĳЩ���⿨�ƿ��ܲ���ҪĿ�꣬�����������Ŀ���߼�
                    break;
            }

            LogDebug($"����Ŀ������: {request.targetPlayerId}");
        }

        /// <summary>
        /// ��֤����ʹ������
        /// </summary>
        private bool ValidateCardUseRequest(CardUseRequest request, CardData cardData)
        {
            // ������֤
            if (!CardUtilities.Validator.ValidateCardUseRequest(request, cardData))
            {
                return false;
            }

            // ���Ч���Ƿ���ע��
            if (!IsEffectRegistered(cardData.effectType))
            {
                LogError($"Ч��δע��: {cardData.effectType}");
                return false;
            }

            // ��֤Ŀ�꣨����ָ���Ϳ��ƣ�
            if (cardData.cardType == CardType.PlayerTarget)
            {
                if (!IsValidTarget(request.targetPlayerId, request.userId))
                {
                    LogError($"��Ч��Ŀ�����: {request.targetPlayerId}");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// ��֤Ŀ���Ƿ���Ч
        /// </summary>
        private bool IsValidTarget(int targetId, int userId)
        {
            if (targetId <= 0)
            {
                return false;
            }

            // ���Ŀ������Ƿ���������
            var alivePlayers = GetAllAlivePlayers();
            if (!alivePlayers.Contains(targetId))
            {
                LogError($"Ŀ�����{targetId}���ڴ���б���");
                return false;
            }

            return true;
        }

        #endregion

        #region Ч��ִ��

        /// <summary>
        /// ִ�п���Ч��
        /// </summary>
        private void ExecuteCardEffect(CardUseRequest request, CardData cardData)
        {
            LogDebug($"ִ�п���Ч��: {cardData.cardName}");

            // ��ȡЧ��ʵ��
            var effect = GetEffect(cardData.effectType);
            if (effect == null)
            {
                LogError($"Ч��ʵ��δ�ҵ�: {cardData.effectType}");
                return;
            }

            // ������֤
            if (!effect.CanUse(request, cardData))
            {
                LogWarning("Ч����֤ʧ�ܣ��޷�ʹ��");
                CardEvents.OnCardMessage?.Invoke("��ǰ�޷�ʹ�øÿ���");
                return;
            }

            // ִ��Ч��
            effectExecutor.ExecuteEffect(effect, request, cardData, OnEffectCompleted);
        }

        /// <summary>
        /// Ч��ִ����ɻص�
        /// </summary>
        private void OnEffectCompleted(CardUseRequest request, CardData cardData, CardEffectResult result)
        {
            LogDebug($"����Ч��ִ�����: {cardData.cardName}, �ɹ�: {result.success}");

            // ��¼ʹ����־
            CardUtilities.LogCardUse(request.userId, request.cardId, cardData.cardName,
                result.success ? "�ɹ�" : $"ʧ��:{result.message}");

            // ����ʹ������¼�
            CardEvents.OnCardUsed?.Invoke(request, cardData, result);

            // ��ʾ�����Ϣ
            if (!string.IsNullOrEmpty(result.message))
            {
                CardEvents.OnCardMessage?.Invoke(result.message);
            }
        }

        #endregion

        #region ����ʹ��������

        /// <summary>
        /// ������ʹ�������¼��ص���
        /// </summary>
        private void HandleCardUseRequest(CardUseRequest request, CardData cardData)
        {
            UseCard(request, cardData);
        }

        #endregion

        #region ���״̬��ѯ

        /// <summary>
        /// ��ȡ���д�����
        /// </summary>
        private List<int> GetAllAlivePlayers()
        {
            // ������Ҫ����Ϸϵͳ���ɣ���ȡ��ǰ��������б�
            // ��ʱ����ģ������
            var players = new List<int>();

            // TODO: ��PlayerManager��NetworkManager����
            // ��ȡ��ʵ������б�

            LogDebug($"��ȡ��{players.Count}��������");
            return players;
        }

        /// <summary>
        /// �������Ƿ���
        /// </summary>
        public bool IsPlayerAlive(int playerId)
        {
            return GetAllAlivePlayers().Contains(playerId);
        }

        /// <summary>
        /// ��ȡ������ң����������ģ�
        /// </summary>
        public List<int> GetAllPlayers()
        {
            // TODO: ����Ϸϵͳ����
            return new List<int>();
        }

        #endregion

        #region ��������

        /// <summary>
        /// ����ID��ȡ�������ݣ����ڵ��Ժ���֤��
        /// </summary>
        public CardData GetCardDataById(int cardId)
        {
            // ������Ҫ��CardConfig���ɻ�ȡ��������
            // ��ʱ����null��ʵ��ʵ��ʱ��Ҫע��CardConfig����
            LogDebug($"��ȡ��������: {cardId}");
            return null; // TODO: ʵ����ʵ�Ŀ������ݻ�ȡ
        }

        /// <summary>
        /// ��ȡЧ������
        /// </summary>
        public string GetEffectDescription(EffectType effectType, float effectValue)
        {
            return CardUtilities.GetEffectDescription(effectType, effectValue);
        }

        /// <summary>
        /// ���ϵͳ�Ƿ�׼������
        /// </summary>
        public bool IsSystemReady()
        {
            return registeredEffects != null && registeredEffects.Count > 0;
        }

        #endregion

        #region ��־����

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                CardUtilities.LogWithTag(CardConstants.LOG_TAG_EFFECT, message);
            }
        }

        private void LogWarning(string message)
        {
            if (enableDebugLogs)
            {
                CardUtilities.LogWithTag(CardConstants.LOG_TAG_EFFECT, message, LogType.Warning);
            }
        }

        private void LogError(string message)
        {
            if (enableDebugLogs)
            {
                CardUtilities.LogWithTag(CardConstants.LOG_TAG_EFFECT, message, LogType.Error);
            }
        }

        #endregion
    }

    #region Ч��ִ����

    /// <summary>
    /// Ч��ִ�������򻯰棩
    /// ����ȫִ�п���Ч��
    /// </summary>
    public class CardEffectExecutor
    {
        private float timeout = 10f;

        /// <summary>
        /// ���ó�ʱʱ��
        /// </summary>
        public void SetTimeout(float timeoutSeconds)
        {
            timeout = timeoutSeconds;
        }

        /// <summary>
        /// ִ��Ч��
        /// </summary>
        public void ExecuteEffect(ICardEffect effect, CardUseRequest request, CardData cardData,
            System.Action<CardUseRequest, CardData, CardEffectResult> onCompleted)
        {
            try
            {
                CardUtilities.LogDebug($"��ʼִ��Ч��: {cardData.effectType}");

                // ִ��Ч��
                var result = effect.Execute(request, cardData);

                // ������ɻص�
                onCompleted?.Invoke(request, cardData, result);
            }
            catch (System.Exception e)
            {
                CardUtilities.LogError($"Ч��ִ���쳣: {e.Message}");

                var errorResult = new CardEffectResult(false, $"ִ��ʧ��: {e.Message}");
                onCompleted?.Invoke(request, cardData, errorResult);
            }
        }
    }

    #endregion
}