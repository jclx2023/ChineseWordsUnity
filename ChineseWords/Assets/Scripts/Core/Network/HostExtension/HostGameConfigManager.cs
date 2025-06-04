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
        private bool enableDebugLogs = true;

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
        /// <param name="timerCfg">Timer����</param>
        /// <param name="weightCfg">Ȩ������</param>
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
        /// <param name="questionType">��Ŀ����</param>
        /// <returns>ʱ�����ƣ��룩</returns>
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
        /// <param name="questionType">��Ŀ����</param>
        /// <returns>ʱ�����ƣ��룩</returns>
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
        /// <param name="questionType">��Ŀ����</param>
        /// <returns>Ĭ��ʱ�����ƣ��룩</returns>
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
        /// ����Timer����
        /// </summary>
        /// <param name="newTimerConfig">�µ�Timer����</param>
        public void SetTimerConfig(TimerConfig newTimerConfig)
        {
            var oldConfig = timerConfig;
            timerConfig = newTimerConfig;

            // ��ջ��棬ǿ�����¼���
            cachedTimeLimits.Clear();

            LogDebug($"Timer�����Ѹ���: {oldConfig?.ConfigName ?? "null"} -> {newTimerConfig?.ConfigName ?? "null"}");

            // �������ñ���¼�
            OnTimerConfigChanged?.Invoke(newTimerConfig);
            OnConfigurationChanged?.Invoke();
        }

        /// <summary>
        /// ��ȡTimer����Դ��Ϣ
        /// </summary>
        /// <returns>����Դ����</returns>
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
        /// <returns>ѡ�е���Ŀ����</returns>
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
        /// ��ȡ��ǰȨ������
        /// </summary>
        /// <returns>Ȩ���ֵ�</returns>
        public Dictionary<QuestionType, float> GetCurrentWeights()
        {
            try
            {
                if (questionWeightConfig != null)
                {
                    return questionWeightConfig.GetWeights();
                }

                return QuestionWeightManager.GetWeights();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameConfigManager] ��ȡȨ������ʧ��: {e.Message}");
                return GetDefaultWeights();
            }
        }

        /// <summary>
        /// ��ȡĬ��Ȩ������
        /// </summary>
        /// <returns>Ĭ��Ȩ���ֵ�</returns>
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
        /// ����Ȩ������
        /// </summary>
        /// <param name="newWeightConfig">�µ�Ȩ������</param>
        public void SetWeightConfig(QuestionWeightConfig newWeightConfig)
        {
            var oldConfig = questionWeightConfig;
            questionWeightConfig = newWeightConfig;

            LogDebug($"Ȩ�������Ѹ���");

            // �������ñ���¼�
            OnWeightConfigChanged?.Invoke(newWeightConfig);
            OnConfigurationChanged?.Invoke();
        }

        /// <summary>
        /// ��ȡȨ������Դ��Ϣ
        /// </summary>
        /// <returns>����Դ����</returns>
        public string GetWeightConfigSource()
        {
            if (!isWeightConfigInitialized)
                return "δ��ʼ��";

            if (questionWeightConfig == null)
                return "Ĭ��Ȩ��";

            return $"Ȩ������";
        }

        /// <summary>
        /// ��������Ƿ�����
        /// </summary>
        /// <param name="questionType">��Ŀ����</param>
        /// <returns>�Ƿ�����</returns>
        public bool IsQuestionTypeEnabled(QuestionType questionType)
        {
            try
            {
                if (questionWeightConfig != null)
                {
                    return questionWeightConfig.IsEnabled(questionType);
                }

                // ���ȫ������
                var weights = QuestionWeightManager.GetWeights();
                return weights.ContainsKey(questionType) && weights[questionType] > 0;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameConfigManager] �����������״̬ʧ��: {e.Message}");
                return true; // Ĭ������
            }
        }

        /// <summary>
        /// ��ȡ����Ȩ��
        /// </summary>
        /// <param name="questionType">��Ŀ����</param>
        /// <returns>����Ȩ��</returns>
        public float GetQuestionTypeWeight(QuestionType questionType)
        {
            try
            {
                if (questionWeightConfig != null)
                {
                    return questionWeightConfig.GetWeight(questionType);
                }

                var weights = QuestionWeightManager.GetWeights();
                return weights.ContainsKey(questionType) ? weights[questionType] : 1.0f;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameConfigManager] ��ȡ����Ȩ��ʧ��: {e.Message}");
                return 1.0f;
            }
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

        #region ������֤

        /// <summary>
        /// ��֤��ǰ������Ч��
        /// </summary>
        /// <returns>��֤���</returns>
        public bool ValidateCurrentConfiguration()
        {
            bool isValid = true;
            List<string> issues = new List<string>();

            // ��֤Timer����
            if (timerConfig != null)
            {
                try
                {
                    // ��鼸���������͵�ʱ������
                    var testTypes = new QuestionType[] {
                        QuestionType.HardFill,
                        QuestionType.ExplanationChoice
                    };

                    foreach (var testType in testTypes)
                    {
                        float timeLimit = timerConfig.GetTimeLimitForQuestionType(testType);
                        if (timeLimit <= 0)
                        {
                            issues.Add($"Timer�����쳣: {testType} ʱ������Ϊ {timeLimit}");
                            isValid = false;
                        }
                    }
                }
                catch (System.Exception e)
                {
                    issues.Add($"Timer������֤ʧ��: {e.Message}");
                    isValid = false;
                }
            }

            // ��֤Ȩ������
            if (questionWeightConfig != null)
            {
                try
                {
                    var weights = questionWeightConfig.GetWeights();
                    if (weights.Count == 0)
                    {
                        issues.Add("Ȩ�������쳣: û�����õ�����");
                        isValid = false;
                    }

                    float totalWeight = 0f;
                    foreach (var weight in weights.Values)
                    {
                        totalWeight += weight;
                    }

                    if (totalWeight <= 0)
                    {
                        issues.Add($"Ȩ�������쳣: ��Ȩ��Ϊ {totalWeight}");
                        isValid = false;
                    }
                }
                catch (System.Exception e)
                {
                    issues.Add($"Ȩ��������֤ʧ��: {e.Message}");
                    isValid = false;
                }
            }

            if (!isValid)
            {
                Debug.LogWarning($"[GameConfigManager] ������֤ʧ��:\n{string.Join("\n", issues)}");
            }
            else
            {
                LogDebug("������֤ͨ��");
            }

            return isValid;
        }

        #endregion

        #region ״̬��Ϣ

        /// <summary>
        /// ��ȡ���ù�����״̬��Ϣ
        /// </summary>
        /// <returns>״̬��Ϣ�ַ���</returns>
        public string GetStatusInfo()
        {
            var status = "=== GameConfigManager״̬ ===\n";
            status += $"Timer�����ѳ�ʼ��: {isTimerConfigInitialized}\n";
            status += $"Ȩ�������ѳ�ʼ��: {isWeightConfigInitialized}\n";
            status += $"Timer����Դ: {GetTimerConfigSource()}\n";
            status += $"Ȩ������Դ: {GetWeightConfigSource()}\n";
            status += $"ʱ�����ƻ�������: {cachedTimeLimits.Count}\n";

            // ��ʾ�����ʱ������
            if (cachedTimeLimits.Count > 0)
            {
                status += "�����ʱ������:\n";
                foreach (var kvp in cachedTimeLimits)
                {
                    status += $"  {kvp.Key}: {kvp.Value}��\n";
                }
            }

            // ��ʾȨ����Ϣ
            try
            {
                var weights = GetCurrentWeights();
                status += $"������������: {weights.Count}\n";
                if (weights.Count > 0)
                {
                    status += "����Ȩ��:\n";
                    foreach (var kvp in weights)
                    {
                        status += $"  {kvp.Key}: {kvp.Value}\n";
                    }
                }
            }
            catch (System.Exception e)
            {
                status += $"Ȩ����Ϣ��ȡʧ��: {e.Message}\n";
            }

            return status;
        }

        /// <summary>
        /// ��ȡ����ժҪ��Ϣ
        /// </summary>
        /// <returns>����ժҪ�ַ���</returns>
        public string GetConfigSummary()
        {
            var summary = "����ժҪ: ";

            if (timerConfig != null)
            {
                summary += $"Timer({timerConfig.ConfigName}) ";
            }
            else
            {
                summary += "Timer(Ĭ��) ";
            }

            if (questionWeightConfig != null)
            {
                var weights = questionWeightConfig.GetWeights();
                summary += $"Ȩ��({weights.Count}������)";
            }
            else
            {
                summary += "Ȩ��(Ĭ��)";
            }

            return summary;
        }

        #endregion

        #region ����ͬ��

        /// <summary>
        /// �ӷ������ù�����ͬ������
        /// </summary>
        public void SyncFromRoomConfigManager()
        {
            LogDebug("�ӷ������ù�����ͬ������");

            try
            {
                if (RoomConfigManager.Instance != null)
                {
                    // ͬ��Timer����
                    var roomTimerConfig = RoomConfigManager.Instance.GetCurrentTimerConfig();
                    if (roomTimerConfig != null)
                    {
                        SetTimerConfig(roomTimerConfig);
                        LogDebug($"�ӷ���ͬ��Timer����: {roomTimerConfig.ConfigName}");
                    }

                    LogDebug("��������ͬ�����");
                }
                else
                {
                    LogDebug("�������ù����������ڣ�����ͬ��");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameConfigManager] ��������ͬ��ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// ��ȫ�����ù�����ˢ������
        /// </summary>
        public void RefreshFromGlobalManagers()
        {
            LogDebug("��ȫ�����ù�����ˢ������");

            try
            {
                // ˢ��Timer����
                if (TimerConfigManager.Config != null)
                {
                    SetTimerConfig(TimerConfigManager.Config);
                }

                // ˢ��Ȩ������
                if (QuestionWeightManager.Config != null)
                {
                    SetWeightConfig(QuestionWeightManager.Config);
                }

                LogDebug("ȫ������ˢ�����");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameConfigManager] ȫ������ˢ��ʧ��: {e.Message}");
            }
        }

        #endregion

        #region ���߷���

        /// <summary>
        /// ���õ�����־����
        /// </summary>
        /// <param name="enabled">�Ƿ����õ�����־</param>
        public void SetDebugLogs(bool enabled)
        {
            enableDebugLogs = enabled;
            LogDebug($"������־��{(enabled ? "����" : "����")}");
        }

        /// <summary>
        /// ������־���
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[GameConfigManager] {message}");
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