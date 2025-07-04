using System;
using System.Collections.Generic;
using UnityEngine;
using Cards.Core;

namespace Cards.Effects
{
    /// <summary>
    /// ����Ч��ϵͳ���ģ��򻯰棩
    /// ����Ч����ע���ִ�У�Ŀ��ѡ����UI�㴦��
    /// ���°� - ֧���µ�12�ſ���Ч��
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
            LogDebug($"{GetType().Name} ����Ѵ������ȴ���������");
            InitializeSystem();
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

            // ע���¼�
            RegisterEvents();

            LogDebug("����Ч��ϵͳ��ʼ�����");
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
            registeredEffects[effectType] = effect;
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

        /// <summary>
        /// ��ȡ������ע���Ч������
        /// </summary>
        public List<EffectType> GetRegisteredEffectTypes()
        {
            return new List<EffectType>(registeredEffects.Keys);
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
                    LogDebug($"�Է��Ϳ��ƣ�Ŀ������Ϊʹ����: {request.userId}");
                    break;

                case CardType.PlayerTarget:
                    // ָ���Ϳ��ƣ�UI��Ӧ���Ѿ�������targetPlayerId
                    if (request.targetPlayerId <= 0)
                    {
                        LogError("ָ���Ϳ���ȱ����ЧĿ��");
                        return;
                    }
                    LogDebug($"ָ���Ϳ��ƣ�Ŀ�����: {request.targetPlayerId}");
                    break;

                case CardType.Special:
                    // �����Ϳ��ƣ����ݾ���Ч������
                    LogDebug("�����Ϳ��ƣ�Ŀ����Ч���߼�����");
                    break;
            }
        }

        /// <summary>
        /// ��֤����ʹ������
        /// </summary>
        private bool ValidateCardUseRequest(CardUseRequest request, CardData cardData)
        {

            // ���Ч���Ƿ���ע��
            if (!IsEffectRegistered(cardData.effectType))
            {
                LogError($"Ч��δע��: {cardData.effectType}");
                return false;
            }

            // ��֤Ŀ�꣨����ָ���Ϳ��ƣ�
            if (cardData.cardType == CardType.PlayerTarget)
            {
                if (!IsValidTarget(request.targetPlayerId, request.userId, cardData))
                {
                    LogError($"��Ч��Ŀ�����: {request.targetPlayerId}");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// ��֤Ŀ���Ƿ���Ч�����°棬֧���¿��ƣ�
        /// </summary>
        private bool IsValidTarget(int targetId, int userId, CardData cardData)
        {
            if (targetId <= 0)
            {
                LogError("Ŀ��ID��Ч");
                return false;
            }

            // ָ���Ϳ��Ʋ���ѡ���Լ���ΪĿ��
            if (targetId == userId)
            {
                LogError($"ָ���Ϳ��Ʋ���ѡ���Լ���ΪĿ�꣺{cardData.cardName}");
                return false;
            }

            // ͨ��CardGameBridge���Ŀ������Ƿ���
            if (!Cards.Integration.CardGameBridge.IsPlayerAlive(targetId))
            {
                LogError($"Ŀ�����{targetId}�������򲻴���");
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
            LogDebug($"ִ�п���Ч��: {cardData.cardName} ({cardData.effectType})");

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
                LogWarning($"Ч����֤ʧ�ܣ��޷�ʹ�ÿ���: {cardData.cardName}");
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
            LogDebug($"����Ч��ִ�����: {cardData.cardName}, �ɹ�: {result.success}, ��Ϣ: {result.message}");

            // ����ʹ������¼�
            CardEvents.OnCardUsed?.Invoke(request, cardData, result);

            // ��ʾ�����Ϣ
            if (!string.IsNullOrEmpty(result.message))
            {
                CardEvents.OnCardMessage?.Invoke(result.message);
            }

            // ����ɹ�ʹ�ã��㲥����ʹ����Ϣ����������ͬ����
            if (result.success)
            {
                BroadcastCardUsage(request, cardData);
            }
        }

        /// <summary>
        /// �㲥����ʹ����Ϣ
        /// </summary>
        private void BroadcastCardUsage(CardUseRequest request, CardData cardData)
        {
            // ͨ��CardGameBridge�㲥����ʹ����Ϣ
            Cards.Integration.CardGameBridge.BroadcastCardUsage(
                request.userId,
                request.cardId,
                request.targetPlayerId,
                cardData.cardName
            );
        }

        #endregion

        #region ����ʹ��������

        /// <summary>
        /// ��������ʹ�������¼��ص���
        /// </summary>
        private void HandleCardUseRequest(CardUseRequest request, CardData cardData)
        {
            UseCard(request, cardData);
        }

        #endregion

        /// <summary>
        /// ���ϵͳ�Ƿ�׼������
        /// </summary>
        public bool IsSystemReady()
        {
            return registeredEffects != null && registeredEffects.Count > 0;
        }


        #region ��־����

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                CardUtilities.LogDebug(message);
            }
        }

        private void LogWarning(string message)
        {
            if (enableDebugLogs)
            {
                CardUtilities.LogError(message);
            }
        }

        private void LogError(string message)
        {
            if (enableDebugLogs)
            {
                CardUtilities.LogError(message);
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
                CardUtilities.LogDebug($"��ʼִ��Ч��: {cardData.effectType} - {cardData.cardName}");

                // ��¼ִ�п�ʼʱ��
                float startTime = Time.time;

                // ִ��Ч��
                var result = effect.Execute(request, cardData);

                // ��¼ִ��ʱ��
                float executionTime = Time.time - startTime;
                CardUtilities.LogDebug($"Ч��ִ����ɣ���ʱ: {executionTime:F3}��");

                // ������ɻص�
                onCompleted?.Invoke(request, cardData, result);
            }
            catch (System.Exception e)
            {
                CardUtilities.LogError($"Ч��ִ���쳣 [{cardData.cardName}]: {e.Message}");
                CardUtilities.LogError($"�쳣��ջ: {e.StackTrace}");

                var errorResult = new CardEffectResult(false, $"ִ��ʧ��: {e.Message}");
                onCompleted?.Invoke(request, cardData, errorResult);
            }
        }
    }

    #endregion
}