using UnityEngine;

namespace Core.Network
{
    /// <summary>
    /// ������Ϸ�������ݽṹ
    /// ����������Ϸ��ص����ò���
    /// </summary>
    [CreateAssetMenu(fileName = "RoomGameConfig", menuName = "Game/Room Game Config")]
    public class RoomGameConfig : ScriptableObject
    {
        [System.Serializable]
        public class TimerSettings
        {
            [Header("��ʱ������")]
            [Range(5f, 120f)]
            public float questionTimeLimit = 30f;

            [Range(0f, 10f)]
            public float timeUpDelay = 1f;

            [Tooltip("�Ƿ���ʾ����ʱ����")]
            public bool showTimeWarning = true;

            [Range(1f, 10f)]
            public float warningTimeThreshold = 5f;
        }

        [System.Serializable]
        public class HPSettings
        {
            [Header("Ѫ������")]
            [Range(20, 200)]
            public int initialPlayerHealth = 100;

            [Range(5, 50)]
            public int damagePerWrongAnswer = 20;
        }

        [System.Serializable]
        public class QuestionSettings
        {
            [Header("��Ŀ����")]
            [Tooltip("��ĿȨ����������")]
            public QuestionWeightConfig weightConfig;

            [Header("Timer����")]
            [Tooltip("Timer��������")]
            public TimerConfig timerConfig;
        }

        [Header("���÷���")]
        public TimerSettings timerSettings = new TimerSettings();
        public HPSettings hpSettings = new HPSettings();
        public QuestionSettings questionSettings = new QuestionSettings();

        [Header("Ԫ����")]
        [SerializeField] private string configName = "Ĭ������";
        [SerializeField] private string configDescription = "��׼��Ϸ����";
        [SerializeField] private int configVersion = 1;

        // ����ʱ����
        [System.NonSerialized]
        private bool isDirty = false;

        /// <summary>
        /// ��������
        /// </summary>
        public string ConfigName => configName;

        /// <summary>
        /// �������õ����
        /// </summary>
        public RoomGameConfig CreateCopy()
        {
            var copy = CreateInstance<RoomGameConfig>();

            // �����������
            copy.timerSettings = JsonUtility.FromJson<TimerSettings>(JsonUtility.ToJson(timerSettings));
            copy.hpSettings = JsonUtility.FromJson<HPSettings>(JsonUtility.ToJson(hpSettings));
            copy.questionSettings = JsonUtility.FromJson<QuestionSettings>(JsonUtility.ToJson(questionSettings));

            copy.configName = configName + " (����)";
            copy.configDescription = configDescription;
            copy.configVersion = configVersion;

            return copy;
        }

        /// <summary>
        /// ����ΪĬ������
        /// </summary>
        public void ResetToDefault()
        {
            timerSettings = new TimerSettings();
            hpSettings = new HPSettings();
            questionSettings = new QuestionSettings();

            MarkDirty();

            Debug.Log("[RoomGameConfig] ����������ΪĬ��ֵ");
        }

        /// <summary>
        /// ��֤������Ч��
        /// </summary>
        public bool ValidateConfig()
        {
            bool isValid = true;

            // ��֤��Ŀ����
            if (questionSettings.weightConfig == null)
            {
                Debug.LogWarning("[RoomGameConfig] ����Ȩ������δ����");
            }

            // ��֤Timer����
            if (questionSettings.timerConfig == null)
            {
                Debug.LogWarning("[RoomGameConfig] Timer����δ����");
            }

            return isValid;
        }

        /// <summary>
        /// �������Ϊ���޸�
        /// </summary>
        public void MarkDirty()
        {
            isDirty = true;
        }

        /// <summary>
        /// ����޸ı��
        /// </summary>
        public void ClearDirty()
        {
            isDirty = false;
        }

        /// <summary>
        /// ���л�ΪJSON
        /// </summary>
        public string ToJson()
        {
            return JsonUtility.ToJson(this, true);
        }

        /// <summary>
        /// ��JSON�����л�
        /// </summary>
        public static RoomGameConfig FromJson(string json)
        {
            try
            {
                var config = CreateInstance<RoomGameConfig>();
                JsonUtility.FromJsonOverwrite(json, config);
                return config;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RoomGameConfig] JSON�����л�ʧ��: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// ��ȡ��ǰ��Ч��Timer����
        /// </summary>
        public TimerConfig GetEffectiveTimerConfig()
        {
            // ���������Timer���ã��������õ�����
            if (questionSettings.timerConfig != null)
            {
                return questionSettings.timerConfig;
            }

            // ���û�����ã�����TimerConfigManager�ĵ�ǰ����
            if (TimerConfigManager.IsConfigured())
            {
                return TimerConfigManager.Config;
            }

            // ��û�еĻ�������һ��Ĭ������
            Debug.LogWarning("[RoomGameConfig] û�п��õ�Timer���ã�������ʱĬ������");
            var tempConfig = ScriptableObject.CreateInstance<TimerConfig>();
            tempConfig.ResetToDefault();
            return tempConfig;
        }

    }
}