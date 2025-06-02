using UnityEngine;
using Core;

namespace Core
{
    /// <summary>
    /// Timer���ù�����
    /// �ṩȫ�ֵ�Timer���÷��ʽӿ�
    /// </summary>
    public static class TimerConfigManager
    {
        private static TimerConfig config;
        private static bool initialized = false;

        /// <summary>
        /// ��ǰTimer����
        /// </summary>
        public static TimerConfig Config
        {
            get
            {
                if (!initialized)
                {
                    Initialize();
                }
                return config;
            }
            private set
            {
                config = value;
                LogDebug($"Timer�����Ѹ���: {config?.ConfigName ?? "null"}");
            }
        }

        /// <summary>
        /// ��ʼ��Timer���ù�����
        /// </summary>
        public static void Initialize()
        {
            if (initialized)
            {
                LogDebug("TimerConfigManager �Ѿ���ʼ��");
                return;
            }

            LogDebug("��ʼ�� TimerConfigManager");

            // ���Լ���Ĭ������
            LoadDefaultConfig();

            initialized = true;
            LogDebug($"TimerConfigManager ��ʼ����ɣ���ǰ����: {config?.ConfigName ?? "δ����"}");
        }

        /// <summary>
        /// ����Timer����
        /// </summary>
        /// <param name="newConfig">�µ�Timer����</param>
        public static void SetConfig(TimerConfig newConfig)
        {
            if (newConfig == null)
            {
                LogDebug("���棺�������ÿյ�Timer����");
                return;
            }

            if (!newConfig.ValidateConfig())
            {
                LogDebug("���棺Timer������֤ʧ�ܣ�ʹ��Ĭ������");
                LoadDefaultConfig();
                return;
            }

            Config = newConfig;
            LogDebug($"Timer����������: {newConfig.ConfigName}");
        }

        /// <summary>
        /// ��ȡָ�����͵Ĵ���ʱ������
        /// </summary>
        /// <param name="questionType">��Ŀ����</param>
        /// <returns>����ʱ�����ƣ��룩</returns>
        public static float GetTimeLimitForQuestionType(QuestionType questionType)
        {
            if (Config == null)
            {
                LogDebug($"Timer����δ���ã�ʹ��Ĭ��ʱ��: {GetDefaultTimeLimit()}��");
                return GetDefaultTimeLimit();
            }

            try
            {
                float timeLimit = Config.GetTimeLimitForQuestionType(questionType);
                LogDebug($"��ȡ���� {questionType} ��ʱ������: {timeLimit}��");
                return timeLimit;
            }
            catch (System.Exception e)
            {
                LogDebug($"��ȡ����ʱ��ʧ��: {e.Message}��ʹ��Ĭ��ʱ��");
                return GetDefaultTimeLimit();
            }
        }

        /// <summary>
        /// ��ȡָ�����͵�����Timer����
        /// </summary>
        /// <param name="questionType">��Ŀ����</param>
        /// <returns>Timer����</returns>
        public static TimerConfig.QuestionTypeTimer GetTimerSettingsForQuestionType(QuestionType questionType)
        {
            if (Config == null)
            {
                LogDebug("Timer����δ���ã�����Ĭ������");
                return CreateDefaultTimerSettings(questionType);
            }

            try
            {
                return Config.GetTimerForQuestionType(questionType);
            }
            catch (System.Exception e)
            {
                LogDebug($"��ȡ����Timer����ʧ��: {e.Message}��ʹ��Ĭ������");
                return CreateDefaultTimerSettings(questionType);
            }
        }

        /// <summary>
        /// ��ȡȫ��Timer����
        /// </summary>
        /// <returns>ȫ��������Ϣ</returns>
        public static (float timeUpDelay, bool showTimeWarning, float warningThreshold, float criticalThreshold, Color normalColor, Color warningColor, Color criticalColor) GetGlobalSettings()
        {
            if (Config == null)
            {
                LogDebug("Timer����δ���ã�ʹ��Ĭ��ȫ������");
                return (1f, true, 5f, 3f, Color.white, Color.yellow, Color.red);
            }

            return (Config.timeUpDelay, Config.showTimeWarning, Config.warningThreshold, Config.criticalThreshold, Config.normalColor, Config.warningColor, Config.criticalColor);
        }

        /// <summary>
        /// ���Timer�����Ƿ�������
        /// </summary>
        /// <returns>�Ƿ�����������</returns>
        public static bool IsConfigured()
        {
            return initialized && Config != null;
        }

        /// <summary>
        /// ��ȡ����ժҪ��Ϣ
        /// </summary>
        /// <returns>����ժҪ�ַ���</returns>
        public static string GetConfigSummary()
        {
            if (Config == null)
            {
                return "Timer����δ����";
            }

            return Config.GetConfigSummary();
        }

        /// <summary>
        /// ����ΪĬ������
        /// </summary>
        public static void ResetToDefault()
        {
            LogDebug("����Timer����ΪĬ��ֵ");
            LoadDefaultConfig();
        }

        /// <summary>
        /// ����Ĭ������
        /// </summary>
        private static void LoadDefaultConfig()
        {
            LogDebug("���Լ���Ĭ��Timer����");

            // ����1����Resources����
            var resourceConfig = Resources.Load<TimerConfig>("TimerConfig");
            if (resourceConfig != null)
            {
                Config = resourceConfig;
                LogDebug($"��Resources����Timer����: {resourceConfig.ConfigName}");
                return;
            }

            // ����2�������������ܵ�·��
            string[] possiblePaths = {
                "Config/TimerConfig",
                "Configs/TimerConfig",
                "Game/TimerConfig"
            };

            foreach (string path in possiblePaths)
            {
                var config = Resources.Load<TimerConfig>(path);
                if (config != null)
                {
                    Config = config;
                    LogDebug($"��Resources·�� {path} ����Timer����: {config.ConfigName}");
                    return;
                }
            }

            // ����3����������ʱĬ������
            LogDebug("δ�ҵ�Timer�����ļ�����������ʱĬ������");
            CreateRuntimeDefaultConfig();
        }

        /// <summary>
        /// ��������ʱĬ������
        /// </summary>
        private static void CreateRuntimeDefaultConfig()
        {
            var defaultConfig = ScriptableObject.CreateInstance<TimerConfig>();
            defaultConfig.ResetToDefault();
            Config = defaultConfig;
            LogDebug("����ʱĬ��Timer���ô������");
        }

        /// <summary>
        /// ��ȡĬ��ʱ������
        /// </summary>
        private static float GetDefaultTimeLimit()
        {
            return 30f; // Ĭ��30��
        }

        /// <summary>
        /// ����Ĭ�ϵ�Timer����
        /// </summary>
        private static TimerConfig.QuestionTypeTimer CreateDefaultTimerSettings(QuestionType questionType)
        {
            var settings = new TimerConfig.QuestionTypeTimer
            {
                questionType = questionType,
                baseTimeLimit = GetDefaultTimeLimit()
            };

            // �������͵���Ĭ��ʱ��
            switch (questionType)
            {
                case QuestionType.ExplanationChoice:
                case QuestionType.SimularWordChoice:
                    settings.baseTimeLimit = 20f;
                    break;

                case QuestionType.SentimentTorF:
                case QuestionType.UsageTorF:
                    settings.baseTimeLimit = 15f;
                    break;

                case QuestionType.HandWriting:
                    settings.baseTimeLimit = 60f;
                    break;

                default:
                    // ������ʹ��Ĭ��30��
                    break;
            }

            return settings;
        }

        /// <summary>
        /// ������־���
        /// </summary>
        private static void LogDebug(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[TimerConfigManager] {message}");
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// �༭��ר�ã�ǿ�����³�ʼ��
        /// </summary>
        [UnityEditor.MenuItem("Tools/Timer Config/Force Reinitialize")]
        public static void ForceReinitialize()
        {
            initialized = false;
            config = null;
            Initialize();
            Debug.Log("[TimerConfigManager] ǿ�����³�ʼ�����");
        }

        /// <summary>
        /// �༭��ר�ã���ʾ��ǰ����
        /// </summary>
        [UnityEditor.MenuItem("Tools/Timer Config/Show Current Config")]
        public static void ShowCurrentConfig()
        {
            Debug.Log($"[TimerConfigManager] ��ǰ����ժҪ:\n{GetConfigSummary()}");
        }

        /// <summary>
        /// �༭��ר�ã����Ի�ȡ������ʱ��
        /// </summary>
        [UnityEditor.MenuItem("Tools/Timer Config/Test All Question Types")]
        public static void TestAllQuestionTypes()
        {
            Debug.Log("[TimerConfigManager] ������������ʱ������:");

            var questionTypes = System.Enum.GetValues(typeof(QuestionType));
            foreach (QuestionType questionType in questionTypes)
            {
                float timeLimit = GetTimeLimitForQuestionType(questionType);
                Debug.Log($"  {questionType}: {timeLimit}��");
            }
        }
#endif
    }
}