using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Core.Network
{
    /// <summary>
    /// 统一的题目权重配置系统
    /// 解决NQMC和HostGameManager中权重配置重复的问题
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
            [Tooltip("是否启用此题型")]
            public bool enabled = true;
        }

        [Header("题目类型权重配置")]
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

        [Header("调试设置")]
        [SerializeField] private bool showDebugLogs = false;

        // 缓存的权重字典
        private Dictionary<QuestionType, float> cachedWeights;
        private bool isDirty = true;

        /// <summary>
        /// 获取权重字典（只包含启用的题型）
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
        /// 刷新权重缓存
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
                Debug.Log($"[QuestionWeightConfig] 权重已刷新，启用题型数: {cachedWeights.Count}");
            }
        }

        /// <summary>
        /// 根据权重选择随机题目类型
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

            // 回退到第一个启用的题型
            var firstType = weights.Keys.First();
            return firstType;
        }

        /// <summary>
        /// 设置题型权重
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
        /// 启用/禁用题型
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
        /// 获取题型权重
        /// </summary>
        public float GetWeight(QuestionType questionType)
        {
            var config = System.Array.Find(questionWeights, w => w.questionType == questionType);
            return config?.weight ?? 0f;
        }

        /// <summary>
        /// 检查题型是否启用
        /// </summary>
        public bool IsEnabled(QuestionType questionType)
        {
            var config = System.Array.Find(questionWeights, w => w.questionType == questionType);
            return config?.enabled ?? false;
        }

        /// <summary>
        /// 重置为默认权重
        /// </summary>
        [ContextMenu("重置为默认权重")]
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
                Debug.Log("[QuestionWeightConfig] 已重置为默认权重");
            }
        }

        /// <summary>
        /// 验证配置有效性
        /// </summary>
        private void OnValidate()
        {
            isDirty = true;

            // 确保权重不为负数
            foreach (var weight in questionWeights)
            {
                if (weight.weight < 0f)
                    weight.weight = 0f;
            }
        }
    }

    /// <summary>
    /// 权重配置管理器 - 提供全局访问
    /// </summary>
    public static class QuestionWeightManager
    {
        private static QuestionWeightConfig _config;

        /// <summary>
        /// 获取权重配置实例
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
                        Debug.LogWarning("[QuestionWeightManager] 未找到权重配置文件，创建默认配置");
                        _config = ScriptableObject.CreateInstance<QuestionWeightConfig>();
                    }
                }
                return _config;
            }
        }

        /// <summary>
        /// 选择随机题目类型
        /// </summary>
        public static QuestionType SelectRandomQuestionType()
        {
            return Config.SelectRandomType();
        }

        /// <summary>
        /// 设置权重配置
        /// </summary>
        public static void SetConfig(QuestionWeightConfig config)
        {
            _config = config;
        }
    }
}