using UnityEngine;
using Core;
using System.Collections.Generic;
using System.Linq;

namespace Core
{
    /// <summary>
    /// Timer�������ݽṹ
    /// ����ͬ���͵�ʱ�����ƺ��������
    /// </summary>
    [CreateAssetMenu(fileName = "TimerConfig", menuName = "Game/Timer Config")]
    public class TimerConfig : ScriptableObject
    {
        /// <summary>
        /// ����ʱ������
        /// </summary>
        [System.Serializable]
        public class QuestionTypeTimer
        {
            [Header("��������")]
            public QuestionType questionType;

            [Header("ʱ������")]
            [Range(5f, 120f)]
            [Tooltip("�����͵Ļ�������ʱ�䣨�룩")]
            public float baseTimeLimit = 30f;

            /// <summary>
            /// ��֤������Ч��
            /// </summary>
            public bool IsValid()
            {
                return baseTimeLimit > 0;
            }

            /// <summary>
            /// ��ȡ������ʾ����
            /// </summary>
            public string GetDisplayName()
            {
                switch (questionType)
                {
                    case QuestionType.ExplanationChoice:
                    case QuestionType.SimularWordChoice:
                        return "ѡ����";
                    case QuestionType.HardFill:
                    case QuestionType.SoftFill:
                    case QuestionType.TextPinyin:
                    case QuestionType.IdiomChain:
                        return "�����";
                    case QuestionType.SentimentTorF:
                    case QuestionType.UsageTorF:
                        return "�ж���";
                    case QuestionType.HandWriting:
                        return "��д��";
                    default:
                        return questionType.ToString();
                }
            }
        }

        [Header("����ʱ������")]
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

        [Header("ȫ������")]
        [Range(0f, 5f)]
        [Tooltip("ʱ�䵽����ӳ�ʱ�䣨�룩")]
        public float timeUpDelay = 1f;

        [Tooltip("�Ƿ���ʾʱ�侯��")]
        public bool showTimeWarning = true;

        [Header("�̶�������ֵ")]
        [Range(1f, 10f)]
        [Tooltip("����ʱ����ֵ���룬�̶�ֵ��")]
        public float warningThreshold = 5f;

        [Range(1f, 5f)]
        [Tooltip("Σ��ʱ����ֵ���룬�̶�ֵ��")]
        public float criticalThreshold = 3f;

        [Header("��ɫ����")]
        [ColorUsage(false)]
        public Color normalColor = Color.white;

        [ColorUsage(false)]
        public Color warningColor = Color.yellow;

        [ColorUsage(false)]
        public Color criticalColor = Color.red;

        [Header("Ԫ����")]
        [SerializeField] private string configName = "Ĭ��Timer����";
        [SerializeField] private string configDescription = "��׼����ʱ������";

        /// <summary>
        /// ��������
        /// </summary>
        public string ConfigName => configName;

        /// <summary>
        /// ��������
        /// </summary>
        public string ConfigDescription => configDescription;

        /// <summary>
        /// ��ȡ��������ʱ������
        /// </summary>
        public QuestionTypeTimer[] GetAllTimers()
        {
            return questionTypeTimers ?? new QuestionTypeTimer[0];
        }

        /// <summary>
        /// ��ȡָ�����͵�ʱ������
        /// </summary>
        public QuestionTypeTimer GetTimerForQuestionType(QuestionType questionType)
        {
            var timer = questionTypeTimers?.FirstOrDefault(t => t.questionType == questionType);

            if (timer == null)
            {
                Debug.LogWarning($"[TimerConfig] δ�ҵ����� {questionType} ��ʱ�����ã�ʹ��Ĭ��ֵ");
                return GetDefaultTimerForQuestionType(questionType);
            }

            return timer;
        }

        /// <summary>
        /// ��ȡָ�����͵Ĵ���ʱ������
        /// </summary>
        public float GetTimeLimitForQuestionType(QuestionType questionType)
        {
            return GetTimerForQuestionType(questionType).baseTimeLimit;
        }

        /// <summary>
        /// ����ָ�����͵�ʱ������
        /// </summary>
        public void SetTimerForQuestionType(QuestionType questionType, QuestionTypeTimer newTimer)
        {
            if (questionTypeTimers == null)
            {
                questionTypeTimers = new QuestionTypeTimer[0];
            }

            // ������������
            for (int i = 0; i < questionTypeTimers.Length; i++)
            {
                if (questionTypeTimers[i].questionType == questionType)
                {
                    questionTypeTimers[i] = newTimer;
                    return;
                }
            }

            // ���û�ҵ������������
            var newArray = new QuestionTypeTimer[questionTypeTimers.Length + 1];
            questionTypeTimers.CopyTo(newArray, 0);
            newArray[questionTypeTimers.Length] = newTimer;
            questionTypeTimers = newArray;
        }

        /// <summary>
        /// ��ȡĬ�ϵ�����ʱ������
        /// </summary>
        private QuestionTypeTimer GetDefaultTimerForQuestionType(QuestionType questionType)
        {
            var timer = new QuestionTypeTimer { questionType = questionType };

            // ������������Ĭ��ʱ��
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
        /// ����ΪĬ������
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

            Debug.Log("[TimerConfig] ����������ΪĬ��ֵ");
        }

        /// <summary>
        /// ��ȡ����ժҪ
        /// </summary>
        public string GetConfigSummary()
        {
            var summary = $"=== {configName} ===\n";
            summary += $"����: {configDescription}\n\n";

            if (questionTypeTimers != null)
            {
                summary += "����ʱ������:\n";
                var groupedTimers = questionTypeTimers.GroupBy(t => t.GetDisplayName());

                foreach (var group in groupedTimers)
                {
                    var times = group.Select(t => t.baseTimeLimit).Distinct();
                    if (times.Count() == 1)
                    {
                        summary += $"  {group.Key}: {times.First()}��\n";
                    }
                    else
                    {
                        summary += $"  {group.Key}: {string.Join("/", times)}��\n";
                    }
                }
            }

            summary += $"\nȫ������:\n";
            summary += $"  ʱ�䵽�ӳ�: {timeUpDelay}��\n";
            summary += $"  ��ʾ����: {(showTimeWarning ? "��" : "��")}\n";
            summary += $"  ������ֵ: {warningThreshold}��\n";
            summary += $"  Σ����ֵ: {criticalThreshold}��\n";

            return summary;
        }

        /// <summary>
        /// �������õ����
        /// </summary>
        public TimerConfig CreateCopy()
        {
            var copy = CreateInstance<TimerConfig>();

            // �������ʱ������
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

            // ������������
            copy.timeUpDelay = timeUpDelay;
            copy.showTimeWarning = showTimeWarning;
            copy.warningThreshold = warningThreshold;
            copy.criticalThreshold = criticalThreshold;
            copy.normalColor = normalColor;
            copy.warningColor = warningColor;
            copy.criticalColor = criticalColor;
            copy.configName = configName + " (����)";
            copy.configDescription = configDescription;

            return copy;
        }

        private void OnValidate()
        {
            // ȷ����ֵ�ں���Χ��
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

            // ȷ��������ֵ��ϵ��ȷ
            warningThreshold = Mathf.Clamp(warningThreshold, 1f, 10f);
            criticalThreshold = Mathf.Clamp(criticalThreshold, 1f, 5f);

            if (criticalThreshold > warningThreshold)
            {
                criticalThreshold = warningThreshold;
            }
        }
    }
}