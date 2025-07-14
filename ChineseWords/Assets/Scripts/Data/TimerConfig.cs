using UnityEngine;
using Core;
using System.Collections.Generic;
using System.Linq;

namespace Core
{
    /// <summary>
    /// Timer配置数据结构
    /// 管理不同题型的时间限制和相关设置
    /// </summary>
    [CreateAssetMenu(fileName = "TimerConfig", menuName = "Game/Timer Config")]
    public class TimerConfig : ScriptableObject
    {
        /// <summary>
        /// 题型时间配置
        /// </summary>
        [System.Serializable]
        public class QuestionTypeTimer
        {
            [Header("题型设置")]
            public QuestionType questionType;

            [Header("时间配置")]
            [Range(5f, 120f)]
            [Tooltip("该题型的基础答题时间（秒）")]
            public float baseTimeLimit = 30f;

            /// <summary>
            /// 验证配置有效性
            /// </summary>
            public bool IsValid()
            {
                return baseTimeLimit > 0;
            }

            /// <summary>
            /// 获取题型显示名称
            /// </summary>
            public string GetDisplayName()
            {
                switch (questionType)
                {
                    case QuestionType.ExplanationChoice:
                    case QuestionType.SimularWordChoice:
                        return "选择题";
                    case QuestionType.HardFill:
                    case QuestionType.SoftFill:
                    case QuestionType.TextPinyin:
                    case QuestionType.IdiomChain:
                        return "填空题";
                    case QuestionType.SentimentTorF:
                    case QuestionType.UsageTorF:
                        return "判断题";
                    case QuestionType.HandWriting:
                        return "手写题";
                    default:
                        return questionType.ToString();
                }
            }
        }

        [Header("题型时间配置")]
        [SerializeField]
        private QuestionTypeTimer[] questionTypeTimers = new QuestionTypeTimer[]
        {
            new QuestionTypeTimer { questionType = QuestionType.ExplanationChoice, baseTimeLimit = 20f },
            new QuestionTypeTimer { questionType = QuestionType.SimularWordChoice, baseTimeLimit = 20f },
            new QuestionTypeTimer { questionType = QuestionType.HardFill, baseTimeLimit = 30f },
            new QuestionTypeTimer { questionType = QuestionType.SoftFill, baseTimeLimit = 25f },
            new QuestionTypeTimer { questionType = QuestionType.TextPinyin, baseTimeLimit = 25f },
            new QuestionTypeTimer { questionType = QuestionType.IdiomChain, baseTimeLimit = 20f },
            new QuestionTypeTimer { questionType = QuestionType.SentimentTorF, baseTimeLimit = 15f },
            new QuestionTypeTimer { questionType = QuestionType.UsageTorF, baseTimeLimit = 15f },
            new QuestionTypeTimer { questionType = QuestionType.HandWriting, baseTimeLimit = 60f }
        };

        [Header("全局设置")]
        [Range(0f, 5f)]
        [Tooltip("时间到后的延迟时间（秒）")]
        public float timeUpDelay = 1f;

        [Tooltip("是否显示时间警告")]
        public bool showTimeWarning = true;

        [Header("固定警告阈值")]
        [Range(1f, 10f)]
        [Tooltip("警告时间阈值（秒，固定值）")]
        public float warningThreshold = 5f;

        [Range(1f, 5f)]
        [Tooltip("危险时间阈值（秒，固定值）")]
        public float criticalThreshold = 3f;

        [Header("颜色设置")]
        [ColorUsage(false)]
        public Color normalColor = Color.white;

        [ColorUsage(false)]
        public Color warningColor = Color.yellow;

        [ColorUsage(false)]
        public Color criticalColor = Color.red;

        [Header("元数据")]
        [SerializeField] private string configName = "默认Timer配置";
        [SerializeField] private string configDescription = "标准题型时间配置";

        /// <summary>
        /// 配置名称
        /// </summary>
        public string ConfigName => configName;

        /// <summary>
        /// 配置描述
        /// </summary>
        public string ConfigDescription => configDescription;

        /// <summary>
        /// 获取所有题型时间配置
        /// </summary>
        public QuestionTypeTimer[] GetAllTimers()
        {
            return questionTypeTimers ?? new QuestionTypeTimer[0];
        }

        /// <summary>
        /// 获取指定题型的时间配置
        /// </summary>
        public QuestionTypeTimer GetTimerForQuestionType(QuestionType questionType)
        {
            var timer = questionTypeTimers?.FirstOrDefault(t => t.questionType == questionType);

            if (timer == null)
            {
                Debug.LogWarning($"[TimerConfig] 未找到题型 {questionType} 的时间配置，使用默认值");
                return GetDefaultTimerForQuestionType(questionType);
            }

            return timer;
        }

        /// <summary>
        /// 获取指定题型的答题时间限制
        /// </summary>
        public float GetTimeLimitForQuestionType(QuestionType questionType)
        {
            return GetTimerForQuestionType(questionType).baseTimeLimit;
        }

        /// <summary>
        /// 设置指定题型的时间配置
        /// </summary>
        public void SetTimerForQuestionType(QuestionType questionType, QuestionTypeTimer newTimer)
        {
            if (questionTypeTimers == null)
            {
                questionTypeTimers = new QuestionTypeTimer[0];
            }

            // 查找现有配置
            for (int i = 0; i < questionTypeTimers.Length; i++)
            {
                if (questionTypeTimers[i].questionType == questionType)
                {
                    questionTypeTimers[i] = newTimer;
                    return;
                }
            }

            // 如果没找到，添加新配置
            var newArray = new QuestionTypeTimer[questionTypeTimers.Length + 1];
            questionTypeTimers.CopyTo(newArray, 0);
            newArray[questionTypeTimers.Length] = newTimer;
            questionTypeTimers = newArray;
        }

        /// <summary>
        /// 获取默认的题型时间配置
        /// </summary>
        private QuestionTypeTimer GetDefaultTimerForQuestionType(QuestionType questionType)
        {
            var timer = new QuestionTypeTimer { questionType = questionType };

            // 根据题型设置默认时间
            switch (questionType)
            {
                case QuestionType.ExplanationChoice:
                case QuestionType.SimularWordChoice:
                    timer.baseTimeLimit = 20f;
                    break;

                case QuestionType.HardFill:
                    timer.baseTimeLimit = 30f;
                    break;

                case QuestionType.SoftFill:
                case QuestionType.TextPinyin:
                    timer.baseTimeLimit = 25f;
                    break;

                case QuestionType.IdiomChain:
                    timer.baseTimeLimit = 20f;
                    break;

                case QuestionType.SentimentTorF:
                case QuestionType.UsageTorF:
                    timer.baseTimeLimit = 15f;
                    break;

                case QuestionType.HandWriting:
                    timer.baseTimeLimit = 60f;
                    break;

                default:
                    timer.baseTimeLimit = 30f;
                    break;
            }

            return timer;
        }

        /// <summary>
        /// 重置为默认配置
        /// </summary>
        public void ResetToDefault()
        {
            questionTypeTimers = new QuestionTypeTimer[]
            {
                new QuestionTypeTimer { questionType = QuestionType.ExplanationChoice, baseTimeLimit = 20f },
                new QuestionTypeTimer { questionType = QuestionType.SimularWordChoice, baseTimeLimit = 20f },
                new QuestionTypeTimer { questionType = QuestionType.HardFill, baseTimeLimit = 30f },
                new QuestionTypeTimer { questionType = QuestionType.SoftFill, baseTimeLimit = 25f },
                new QuestionTypeTimer { questionType = QuestionType.TextPinyin, baseTimeLimit = 25f },
                new QuestionTypeTimer { questionType = QuestionType.IdiomChain, baseTimeLimit = 20f },
                new QuestionTypeTimer { questionType = QuestionType.SentimentTorF, baseTimeLimit = 15f },
                new QuestionTypeTimer { questionType = QuestionType.UsageTorF, baseTimeLimit = 15f },
                new QuestionTypeTimer { questionType = QuestionType.HandWriting, baseTimeLimit = 60f }
            };

            timeUpDelay = 1f;
            showTimeWarning = true;
            warningThreshold = 5f;
            criticalThreshold = 3f;
            normalColor = Color.white;
            warningColor = Color.yellow;
            criticalColor = Color.red;

            Debug.Log("[TimerConfig] 配置已重置为默认值");
        }

        /// <summary>
        /// 获取配置摘要
        /// </summary>
        public string GetConfigSummary()
        {
            var summary = $"=== {configName} ===\n";
            summary += $"描述: {configDescription}\n\n";

            if (questionTypeTimers != null)
            {
                summary += "题型时间配置:\n";
                var groupedTimers = questionTypeTimers.GroupBy(t => t.GetDisplayName());

                foreach (var group in groupedTimers)
                {
                    var times = group.Select(t => t.baseTimeLimit).Distinct();
                    if (times.Count() == 1)
                    {
                        summary += $"  {group.Key}: {times.First()}秒\n";
                    }
                    else
                    {
                        summary += $"  {group.Key}: {string.Join("/", times)}秒\n";
                    }
                }
            }

            summary += $"\n全局设置:\n";
            summary += $"  时间到延迟: {timeUpDelay}秒\n";
            summary += $"  显示警告: {(showTimeWarning ? "是" : "否")}\n";
            summary += $"  警告阈值: {warningThreshold}秒\n";
            summary += $"  危险阈值: {criticalThreshold}秒\n";

            return summary;
        }

        /// <summary>
        /// 创建配置的深拷贝
        /// </summary>
        public TimerConfig CreateCopy()
        {
            var copy = CreateInstance<TimerConfig>();

            // 深拷贝题型时间配置
            if (questionTypeTimers != null)
            {
                copy.questionTypeTimers = new QuestionTypeTimer[questionTypeTimers.Length];
                for (int i = 0; i < questionTypeTimers.Length; i++)
                {
                    var original = questionTypeTimers[i];
                    copy.questionTypeTimers[i] = new QuestionTypeTimer
                    {
                        questionType = original.questionType,
                        baseTimeLimit = original.baseTimeLimit
                    };
                }
            }

            // 拷贝其他设置
            copy.timeUpDelay = timeUpDelay;
            copy.showTimeWarning = showTimeWarning;
            copy.warningThreshold = warningThreshold;
            copy.criticalThreshold = criticalThreshold;
            copy.normalColor = normalColor;
            copy.warningColor = warningColor;
            copy.criticalColor = criticalColor;
            copy.configName = configName + " (副本)";
            copy.configDescription = configDescription;

            return copy;
        }

        private void OnValidate()
        {
            // 确保数值在合理范围内
            timeUpDelay = Mathf.Clamp(timeUpDelay, 0f, 5f);

            if (questionTypeTimers != null)
            {
                foreach (var timer in questionTypeTimers)
                {
                    if (timer != null)
                    {
                        timer.baseTimeLimit = Mathf.Clamp(timer.baseTimeLimit, 5f, 120f);
                    }
                }
            }

            // 确保警告阈值关系正确
            warningThreshold = Mathf.Clamp(warningThreshold, 1f, 10f);
            criticalThreshold = Mathf.Clamp(criticalThreshold, 1f, 5f);

            if (criticalThreshold > warningThreshold)
            {
                criticalThreshold = warningThreshold;
            }
        }
    }
}