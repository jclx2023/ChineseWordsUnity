using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Core.Network
{
    /// <summary>
    /// ͳһ����ĿȨ������ϵͳ
    /// ���NQMC��HostGameManager��Ȩ�������ظ�������
    /// </summary>
    [CreateAssetMenu(fileName = "QuestionWeightConfig", menuName = "Game/Question Weight Config")]
    public class QuestionWeightConfig : ScriptableObject
    {
        [System.Serializable]
        public class QuestionTypeWeight
        {
            public QuestionType questionType;
            [Range(0f, 10f)]
            public float weight = 1f;
            [Tooltip("�Ƿ����ô�����")]
            public bool enabled = true;
        }

        [Header("��Ŀ����Ȩ������")]
        [SerializeField]
        private QuestionTypeWeight[] questionWeights = new QuestionTypeWeight[]
        {
            new QuestionTypeWeight { questionType = QuestionType.IdiomChain, weight = 1f, enabled = true },
            new QuestionTypeWeight { questionType = QuestionType.TextPinyin, weight = 1f, enabled = true },
            new QuestionTypeWeight { questionType = QuestionType.HardFill, weight = 1f, enabled = true },
            new QuestionTypeWeight { questionType = QuestionType.SoftFill, weight = 1f, enabled = true },
            new QuestionTypeWeight { questionType = QuestionType.SentimentTorF, weight = 1f, enabled = true },
            new QuestionTypeWeight { questionType = QuestionType.SimularWordChoice, weight = 1f, enabled = true },
            new QuestionTypeWeight { questionType = QuestionType.UsageTorF, weight = 1f, enabled = true },
            new QuestionTypeWeight { questionType = QuestionType.ExplanationChoice, weight = 1f, enabled = true },
        };

        [Header("��������")]
        [SerializeField] private bool showDebugLogs = false;

        // �����Ȩ���ֵ�
        private Dictionary<QuestionType, float> cachedWeights;
        private bool isDirty = true;

        /// <summary>
        /// ��ȡȨ���ֵ䣨ֻ�������õ����ͣ�
        /// </summary>
        public Dictionary<QuestionType, float> GetWeights()
        {
            if (isDirty || cachedWeights == null)
            {
                RefreshWeights();
            }
            return new Dictionary<QuestionType, float>(cachedWeights);
        }

        /// <summary>
        /// ˢ��Ȩ�ػ���
        /// </summary>
        private void RefreshWeights()
        {
            cachedWeights = new Dictionary<QuestionType, float>();

            foreach (var weightConfig in questionWeights)
            {
                if (weightConfig.enabled && weightConfig.weight > 0f)
                {
                    cachedWeights[weightConfig.questionType] = weightConfig.weight;
                }
            }

            isDirty = false;

            if (showDebugLogs)
            {
                Debug.Log($"[QuestionWeightConfig] Ȩ����ˢ�£�����������: {cachedWeights.Count}");
            }
        }

        /// <summary>
        /// ����Ȩ��ѡ�������Ŀ����
        /// </summary>
        public QuestionType SelectRandomType()
        {
            var weights = GetWeights();

            float total = weights.Values.Sum();
            float random = Random.value * total;
            float accumulator = 0f;

            foreach (var pair in weights)
            {
                accumulator += pair.Value;
                if (random <= accumulator)
                {
                    return pair.Key;
                }
            }

            // ���˵���һ�����õ�����
            var firstType = weights.Keys.First();
            return firstType;
        }

        /// <summary>
        /// ��������Ȩ��
        /// </summary>
        public void SetWeight(QuestionType questionType, float weight)
        {
            var config = System.Array.Find(questionWeights, w => w.questionType == questionType);
            if (config != null)
            {
                config.weight = Mathf.Max(0f, weight);
                isDirty = true;
            }
        }

        /// <summary>
        /// ����/��������
        /// </summary>
        public void SetEnabled(QuestionType questionType, bool enabled)
        {
            var config = System.Array.Find(questionWeights, w => w.questionType == questionType);
            if (config != null)
            {
                config.enabled = enabled;
                isDirty = true;
            }
        }

        /// <summary>
        /// ��ȡ����Ȩ��
        /// </summary>
        public float GetWeight(QuestionType questionType)
        {
            var config = System.Array.Find(questionWeights, w => w.questionType == questionType);
            return config?.weight ?? 0f;
        }

        /// <summary>
        /// ��������Ƿ�����
        /// </summary>
        public bool IsEnabled(QuestionType questionType)
        {
            var config = System.Array.Find(questionWeights, w => w.questionType == questionType);
            return config?.enabled ?? false;
        }

        /// <summary>
        /// ����ΪĬ��Ȩ��
        /// </summary>
        [ContextMenu("����ΪĬ��Ȩ��")]
        public void ResetToDefault()
        {
            foreach (var weight in questionWeights)
            {
                weight.weight = 1f;
                weight.enabled = true;
            }
            isDirty = true;

            if (showDebugLogs)
            {
                Debug.Log("[QuestionWeightConfig] ������ΪĬ��Ȩ��");
            }
        }

        /// <summary>
        /// ��֤������Ч��
        /// </summary>
        private void OnValidate()
        {
            isDirty = true;

            // ȷ��Ȩ�ز�Ϊ����
            foreach (var weight in questionWeights)
            {
                if (weight.weight < 0f)
                    weight.weight = 0f;
            }
        }
    }

    /// <summary>
    /// Ȩ�����ù����� - �ṩȫ�ַ���
    /// </summary>
    public static class QuestionWeightManager
    {
        private static QuestionWeightConfig _config;

        /// <summary>
        /// ��ȡȨ������ʵ��
        /// </summary>
        public static QuestionWeightConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = Resources.Load<QuestionWeightConfig>("QuestionWeightConfig");
                    if (_config == null)
                    {
                        Debug.LogWarning("[QuestionWeightManager] δ�ҵ�Ȩ�������ļ�������Ĭ������");
                        _config = ScriptableObject.CreateInstance<QuestionWeightConfig>();
                    }
                }
                return _config;
            }
        }

        /// <summary>
        /// ѡ�������Ŀ����
        /// </summary>
        public static QuestionType SelectRandomQuestionType()
        {
            return Config.SelectRandomType();
        }

        /// <summary>
        /// ����Ȩ������
        /// </summary>
        public static void SetConfig(QuestionWeightConfig config)
        {
            _config = config;
        }
    }
}