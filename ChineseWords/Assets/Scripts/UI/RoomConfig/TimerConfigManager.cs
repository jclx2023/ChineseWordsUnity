using UnityEngine;
using Core;

namespace Core
{
    /// <summary>
    /// Timer���ù����� - �޸�������������
    /// ȷ��UI�޸�����ȷ��ӳ��������
    /// </summary>
    public static class TimerConfigManager
    {
        private static TimerConfig config;
        private static bool initialized = false;
        private static bool enableDebugLogs = true;

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
                LogDebug($"Timer���������Ѹ���: {config?.ConfigName ?? "null"} (InstanceID: {config?.GetInstanceID()})");
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
            LogDebug($"TimerConfigManager ��ʼ����ɣ���ǰ����: {config?.ConfigName ?? "δ����"} (InstanceID: {config?.GetInstanceID()})");
        }

        /// <summary>
        /// ����Timer���� - �޸��汾��ȷ��������ȷ����
        /// </summary>
        /// <param name="newConfig">�µ�Timer����</param>
        public static void SetConfig(TimerConfig newConfig)
        {
            LogDebug($"��������Timer����: {newConfig?.ConfigName ?? "null"} (InstanceID: {newConfig?.GetInstanceID()})");

            if (newConfig == null)
            {
                LogDebug("���棺�������ÿյ�Timer���ã����ֵ�ǰ���ò���");
                return;
            }

            // ֱ���������ã���������֤
            // ��֤Ӧ����UI�����÷����У��������������滻����
            Config = newConfig;
            LogDebug($"Timer�����ѳɹ�����: {newConfig.ConfigName} (InstanceID: {newConfig.GetInstanceID()})");

            // ��ѡ���������ժҪ���ڵ���
            if (enableDebugLogs)
            {
                LogDebug($"����ժҪ: {newConfig.GetConfigSummary()}");
            }
        }

        /// <summary>
        /// ǿ���������ã��������м�飩
        /// </summary>
        /// <param name="newConfig">�µ�Timer����</param>
        public static void ForceSetConfig(TimerConfig newConfig)
        {
            LogDebug($"ǿ������Timer����: {newConfig?.ConfigName ?? "null"}");
            config = newConfig;
            LogDebug($"ǿ��������ɣ���ǰ��������: {config?.GetInstanceID()}");
        }

        /// <summary>
        /// ��֤��ǰ���õ���Ч��
        /// </summary>
        /// <returns>�����Ƿ���Ч</returns>
        public static bool ValidateCurrentConfig()
        {
            if (Config == null)
            {
                LogDebug("������֤ʧ�ܣ�����Ϊ��");
                return false;
            }

            try
            {
                bool isValid = Config.ValidateConfig();
                LogDebug($"������֤���: {(isValid ? "��Ч" : "��Ч")}");
                return isValid;
            }
            catch (System.Exception e)
            {
                LogDebug($"������֤�쳣: {e.Message}");
                return false;
            }
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
                LogDebug($"��ȡ���� {questionType} ��ʱ������: {timeLimit}�� (����ID: {Config.GetInstanceID()})");
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
                var settings = Config.GetTimerForQuestionType(questionType);
                LogDebug($"��ȡ���� {questionType} ��Timer����: {settings.baseTimeLimit}�� (����ID: {Config.GetInstanceID()})");
                return settings;
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

            var settings = (Config.timeUpDelay, Config.showTimeWarning, Config.warningThreshold, Config.criticalThreshold, Config.normalColor, Config.warningColor, Config.criticalColor);
            LogDebug($"��ȡȫ������: ������ֵ={settings.warningThreshold}��, Σ����ֵ={settings.criticalThreshold}��");
            return settings;
        }

        /// <summary>
        /// ���Timer�����Ƿ�������
        /// </summary>
        /// <returns>�Ƿ�����������</returns>
        public static bool IsConfigured()
        {
            bool configured = initialized && Config != null;
            LogDebug($"����״̬���: �ѳ�ʼ��={initialized}, ���ô���={Config != null}, ����״̬={configured}");
            return configured;
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

            try
            {
                string summary = Config.GetConfigSummary();
                LogDebug($"��ȡ����ժҪ: {summary} (����ID: {Config.GetInstanceID()})");
                return summary;
            }
            catch (System.Exception e)
            {
                LogDebug($"��ȡ����ժҪʧ��: {e.Message}");
                return "����ժҪ��ȡʧ��";
            }
        }

        /// <summary>
        /// ����ΪĬ������
        /// </summary>
        public static void ResetToDefault()
        {
            LogDebug("����Timer����ΪĬ��ֵ");

            if (Config != null)
            {
                try
                {
                    // �������õ�ǰ���ã��������ò���
                    Config.ResetToDefault();
                    LogDebug($"��ǰ���������� (����ID: {Config.GetInstanceID()})");
                    return;
                }
                catch (System.Exception e)
                {
                    LogDebug($"���õ�ǰ����ʧ��: {e.Message}���������µ�Ĭ������");
                }
            }

            // �������ʧ�ܻ�����Ϊ�գ������µ�Ĭ������
            LoadDefaultConfig();
        }

        /// <summary>
        /// ��ȡ����״̬��Ϣ�������ã�
        /// </summary>
        /// <returns>��ϸ������״̬��Ϣ</returns>
        public static string GetConfigStatus()
        {
            var status = "=== TimerConfigManager״̬ ===\n";
            status += $"�ѳ�ʼ��: {initialized}\n";
            status += $"����ʵ��: {(Config != null ? "����" : "������")}\n";

            if (Config != null)
            {
                status += $"��������: {Config.ConfigName}\n";
                status += $"����ID: {Config.GetInstanceID()}\n";
                status += $"������Ч��: {ValidateCurrentConfig()}\n";

                try
                {
                    var timers = Config.GetAllTimers();
                    status += $"���õ���������: {timers.Length}\n";

                    if (timers.Length > 0)
                    {
                        status += "����ʱ������:\n";
                        foreach (var timer in timers)
                        {
                            status += $"  {timer.questionType}: {timer.baseTimeLimit}��\n";
                        }
                    }
                }
                catch (System.Exception e)
                {
                    status += $"��ȡ��������ʧ��: {e.Message}\n";
                }
            }

            return status;
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
                LogDebug($"��Resources����Timer����: {resourceConfig.ConfigName} (ID: {resourceConfig.GetInstanceID()})");
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
                    LogDebug($"��Resources·�� {path} ����Timer����: {config.ConfigName} (ID: {config.GetInstanceID()})");
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
            LogDebug($"����ʱĬ��Timer���ô������ (ID: {defaultConfig.GetInstanceID()})");
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
            if (enableDebugLogs)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[TimerConfigManager] {message}");
#endif
            }
        }

        /// <summary>
        /// ���õ�����־����
        /// </summary>
        public static void SetDebugLogs(bool enabled)
        {
            enableDebugLogs = enabled;
            LogDebug($"������־��{(enabled ? "����" : "����")}");
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
            Debug.Log($"[TimerConfigManager] ��ǰ����״̬:\n{GetConfigStatus()}");
        }

        /// <summary>
        /// �༭��ר�ã���ʾ����ժҪ
        /// </summary>
        [UnityEditor.MenuItem("Tools/Timer Config/Show Config Summary")]
        public static void ShowConfigSummary()
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