using UnityEngine;
using System.Collections.Generic;
using Core;

namespace Core.Network
{
    /// <summary>
    /// ��Ϸ���ù�����
    /// ר�Ÿ���Timer���á�Ȩ�����á���Ŀʱ�����Ƶ����ù���
    /// </summary>
    public class GameConfigManager
    {
        [Header("��������")]
        private bool enableDebugLogs = false;

        // ��������
        private TimerConfig timerConfig;
        private QuestionWeightConfig questionWeightConfig;

        // ����״̬
        private bool isTimerConfigInitialized = false;
        private bool isWeightConfigInitialized = false;

        // ���������ֵ
        private Dictionary<QuestionType, float> cachedTimeLimits;

        // �¼�����
        public System.Action OnConfigurationChanged;
        public System.Action<TimerConfig> OnTimerConfigChanged;
        public System.Action<QuestionWeightConfig> OnWeightConfigChanged;

        /// <summary>
        /// ���캯��
        /// </summary>
        public GameConfigManager()
        {
            cachedTimeLimits = new Dictionary<QuestionType, float>();
            LogDebug("GameConfigManager ʵ���Ѵ���");
        }

        #region ��ʼ��

        /// <summary>
        /// ��ʼ�����ù�����
        /// </summary>
        public void Initialize(TimerConfig timerCfg = null, QuestionWeightConfig weightCfg = null)
        {
            LogDebug("��ʼ��GameConfigManager...");

            try
            {
                // ��ʼ��Timer����
                InitializeTimerConfig(timerCfg);

                // ��ʼ��Ȩ������
                InitializeWeightConfig(weightCfg);

                // ˢ�»���
                RefreshConfigCache();

                LogDebug($"GameConfigManager��ʼ����� - Timer: {GetTimerConfigSource()}, Weight: {GetWeightConfigSource()}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameConfigManager] ��ʼ��ʧ��: {e.Message}");
                SetDefaultConfigurations();
            }
        }

        /// <summary>
        /// ��ʼ��Timer����
        /// </summary>
        /// <param name="customTimerConfig">�Զ���Timer����</param>
        private void InitializeTimerConfig(TimerConfig customTimerConfig)
        {
            if (customTimerConfig != null)
            {
                timerConfig = customTimerConfig;
                LogDebug($"ʹ�ô����Timer����: {timerConfig.ConfigName}");
            }
            else if (TimerConfigManager.Config != null)
            {
                timerConfig = TimerConfigManager.Config;
                LogDebug($"ʹ��ȫ��Timer����: {timerConfig.ConfigName}");
            }
            else
            {
                LogDebug("δ�ҵ�Timer���ã���ʹ��Ĭ��ʱ������");
                timerConfig = null;
            }

            isTimerConfigInitialized = true;
        }

        /// <summary>
        /// ��ʼ��Ȩ������
        /// </summary>
        /// <param name="customWeightConfig">�Զ���Ȩ������</param>
        private void InitializeWeightConfig(QuestionWeightConfig customWeightConfig)
        {
            if (customWeightConfig != null)
            {
                questionWeightConfig = customWeightConfig;
                LogDebug($"ʹ�ô����Ȩ������");
            }
            else if (QuestionWeightManager.Config != null)
            {
                questionWeightConfig = QuestionWeightManager.Config;
                LogDebug($"ʹ��ȫ��Ȩ������");
            }
            else
            {
                LogDebug("δ�ҵ�Ȩ�����ã���ʹ��Ĭ��Ȩ��");
                questionWeightConfig = null;
            }

            isWeightConfigInitialized = true;
        }

        /// <summary>
        /// ����Ĭ������
        /// </summary>
        private void SetDefaultConfigurations()
        {
            timerConfig = null;
            questionWeightConfig = null;
            isTimerConfigInitialized = true;
            isWeightConfigInitialized = true;
            RefreshConfigCache();
            LogDebug("������ΪĬ������");
        }

        #endregion

        #region Timer���ù���

        /// <summary>
        /// ��ȡָ�����͵�ʱ������
        /// </summary>
        public float GetTimeLimitForQuestionType(QuestionType questionType)
        {
            // ���ȴӻ����ȡ
            if (cachedTimeLimits.ContainsKey(questionType))
            {
                return cachedTimeLimits[questionType];
            }

            float timeLimit = CalculateTimeLimitForQuestionType(questionType);

            // ������
            cachedTimeLimits[questionType] = timeLimit;

            LogDebug($"��ȡ����ʱ������: {questionType} -> {timeLimit}�� (����Դ: {GetTimerConfigSource()})");
            return timeLimit;
        }

        /// <summary>
        /// ����ָ�����͵�ʱ������
        /// </summary>
        private float CalculateTimeLimitForQuestionType(QuestionType questionType)
        {
            try
            {
                // ����ʹ��Timer����
                if (timerConfig != null)
                {
                    float configTime = timerConfig.GetTimeLimitForQuestionType(questionType);
                    LogDebug($"ʹ��Timer����: {questionType} -> {configTime}��");
                    return configTime;
                }

                // ���˵�Ĭ��ֵ
                float defaultTime = GetDefaultTimeLimitForQuestionType(questionType);
                LogDebug($"ʹ��Ĭ��ʱ������: {questionType} -> {defaultTime}��");
                return defaultTime;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameConfigManager] ����ʱ������ʧ��: {e.Message}��ʹ��Ĭ��ֵ");
                return GetDefaultTimeLimitForQuestionType(questionType);
            }
        }

        /// <summary>
        /// ��ȡ���͵�Ĭ��ʱ������
        /// </summary>
        private float GetDefaultTimeLimitForQuestionType(QuestionType questionType)
        {
            switch (questionType)
            {
                case QuestionType.ExplanationChoice:
                case QuestionType.SimularWordChoice:
                    return 20f;
                case QuestionType.HardFill:
                    return 30f;
                case QuestionType.SoftFill:
                case QuestionType.TextPinyin:
                    return 25f;
                case QuestionType.IdiomChain:
                    return 20f;
                case QuestionType.SentimentTorF:
                case QuestionType.UsageTorF:
                    return 15f;
                case QuestionType.HandWriting:
                    return 60f;
                default:
                    return 30f; // ͨ��Ĭ��ֵ
            }
        }

        /// <summary>
        /// ��ȡTimer����Դ��Ϣ
        /// </summary>
        public string GetTimerConfigSource()
        {
            if (!isTimerConfigInitialized)
                return "δ��ʼ��";

            if (timerConfig == null)
                return "Ĭ��ֵ";

            return $"Timer����({timerConfig.ConfigName})";
        }

        #endregion

        #region Ȩ�����ù���

        /// <summary>
        /// ѡ�������Ŀ���ͣ�����Ȩ�أ�
        /// </summary>
        public QuestionType SelectRandomQuestionType()
        {
            try
            {
                // ����ʹ�õ�ǰȨ������
                if (questionWeightConfig != null)
                {
                    var selectedType = questionWeightConfig.SelectRandomType();
                    LogDebug($"����Ȩ������ѡ������: {selectedType} (����)");
                    return selectedType;
                }

                // ���˵�ȫ��Ȩ�ع�����
                var globalType = QuestionWeightManager.SelectRandomQuestionType();
                LogDebug($"����ȫ��Ȩ��ѡ������: {globalType}");
                return globalType;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameConfigManager] ѡ���������ʧ��: {e.Message}��ʹ��Ĭ������");
                return QuestionType.HardFill; // Ĭ������
            }
        }

        /// <summary>
        /// ��ȡĬ��Ȩ������
        /// </summary>
        private Dictionary<QuestionType, float> GetDefaultWeights()
        {
            return new Dictionary<QuestionType, float>
            {
                { QuestionType.HardFill, 1.0f },
                { QuestionType.SoftFill, 1.0f },
                { QuestionType.TextPinyin, 1.0f },
                { QuestionType.ExplanationChoice, 1.0f },
                { QuestionType.SimularWordChoice, 1.0f },
                { QuestionType.IdiomChain, 1.0f },
                { QuestionType.SentimentTorF, 1.0f },
                { QuestionType.UsageTorF, 1.0f }
            };
        }

        /// <summary>
        /// ��ȡȨ������Դ��Ϣ
        /// </summary>
        public string GetWeightConfigSource()
        {
            if (!isWeightConfigInitialized)
                return "δ��ʼ��";

            if (questionWeightConfig == null)
                return "Ĭ��Ȩ��";

            return $"Ȩ������";
        }

        #endregion

        #region ���û������

        /// <summary>
        /// ˢ�����û���
        /// </summary>
        public void RefreshConfigCache()
        {
            LogDebug("ˢ�����û���...");

            // ���ʱ�����ƻ���
            cachedTimeLimits.Clear();

            // Ԥ���泣�����͵�ʱ������
            var commonQuestionTypes = new QuestionType[]
            {
                QuestionType.HardFill,
                QuestionType.SoftFill,
                QuestionType.TextPinyin,
                QuestionType.ExplanationChoice,
                QuestionType.SimularWordChoice,
                QuestionType.IdiomChain,
                QuestionType.SentimentTorF,
                QuestionType.UsageTorF
            };

            foreach (var questionType in commonQuestionTypes)
            {
                GetTimeLimitForQuestionType(questionType); // ��ᴥ������
            }

            LogDebug($"���û���ˢ����ɣ������� {cachedTimeLimits.Count} �����͵�ʱ������");
        }

        /// <summary>
        /// ������û���
        /// </summary>
        public void ClearConfigCache()
        {
            cachedTimeLimits.Clear();
            LogDebug("���û��������");
        }

        #endregion

        #region ���߷���
        /// <summary>
        /// ������־���
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                //Debug.Log($"[GameConfigManager] {message}");
            }
        }

        #endregion

        #region ���ٺ�����

        /// <summary>
        /// �������ù�����
        /// </summary>
        public void Dispose()
        {
            // �����¼�
            OnConfigurationChanged = null;
            OnTimerConfigChanged = null;
            OnWeightConfigChanged = null;

            // ������
            ClearConfigCache();

            // ������������
            timerConfig = null;
            questionWeightConfig = null;

            // ����״̬
            isTimerConfigInitialized = false;
            isWeightConfigInitialized = false;

            LogDebug("GameConfigManager������");
        }

        #endregion
    }
}