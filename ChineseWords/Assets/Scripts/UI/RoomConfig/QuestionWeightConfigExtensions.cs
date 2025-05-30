using UnityEngine;
using System.Collections.Generic;
using Core;
using Core.Network;

namespace UI.RoomConfig
{
    /// <summary>
    /// QuestionWeightConfig����չ����
    /// ���ȱ�ٵķ����͹���
    /// </summary>
    public static class QuestionWeightConfigExtensions
    {
        /// <summary>
        /// ����һ�����ø�������
        /// </summary>
        public static void CopyFrom(this QuestionWeightConfig target, QuestionWeightConfig source)
        {
            if (target == null || source == null) return;

            try
            {
                // ��ȡ��������
                var questionTypes = System.Enum.GetValues(typeof(QuestionType));

                foreach (QuestionType type in questionTypes)
                {
                    bool enabled = source.IsEnabled(type);
                    float weight = source.GetWeight(type);

                    target.SetEnabled(type, enabled);
                    target.SetWeight(type, weight);
                }

                Debug.Log("[QuestionWeightConfig] ���ø������");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[QuestionWeightConfig] ���ø���ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// ��ȡ����ժҪ�ַ���
        /// </summary>
        public static string GetConfigSummary(this QuestionWeightConfig config)
        {
            if (config == null) return "����Ϊ��";

            try
            {
                var weights = config.GetWeights();
                float totalWeight = 0f;
                int enabledCount = 0;

                foreach (var weight in weights.Values)
                {
                    totalWeight += weight;
                    enabledCount++;
                }

                return $"��������: {enabledCount}/10, ��Ȩ��: {totalWeight:F1}";
            }
            catch (System.Exception e)
            {
                return $"���ö�ȡʧ��: {e.Message}";
            }
        }

        /// <summary>
        /// ����ΪĬ������
        /// </summary>
        public static void ResetToDefault(this QuestionWeightConfig config)
        {
            if (config == null) return;

            try
            {
                // ��ȡ��������
                var questionTypes = System.Enum.GetValues(typeof(QuestionType));

                foreach (QuestionType type in questionTypes)
                {
                    config.SetEnabled(type, true);  // Ĭ��ȫ������
                    config.SetWeight(type, 1.0f);   // Ĭ��Ȩ��Ϊ1
                }

                Debug.Log("[QuestionWeightConfig] ������ΪĬ������");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[QuestionWeightConfig] ��������ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// ��֤������Ч��
        /// </summary>
        public static bool IsValid(this QuestionWeightConfig config)
        {
            if (config == null) return false;

            try
            {
                var weights = config.GetWeights();
                return weights.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// ��ȡ��ϸ������Ϣ
        /// </summary>
        public static string GetDetailedInfo(this QuestionWeightConfig config)
        {
            if (config == null) return "����Ϊ��";

            try
            {
                var info = "=== Ȩ���������� ===\n";
                var questionTypes = System.Enum.GetValues(typeof(QuestionType));

                foreach (QuestionType type in questionTypes)
                {
                    bool enabled = config.IsEnabled(type);
                    float weight = config.GetWeight(type);
                    string displayName = GetQuestionTypeDisplayName(type);

                    info += $"{displayName}: {(enabled ? $"{weight:F1}" : "����")}\n";
                }

                var weights = config.GetWeights();
                float totalWeight = 0f;
                foreach (var w in weights.Values)
                    totalWeight += w;

                info += $"\n�ܼ�: {weights.Count}/10 ����, ��Ȩ�� {totalWeight:F1}";

                return info;
            }
            catch (System.Exception e)
            {
                return $"��ȡ������Ϣʧ��: {e.Message}";
            }
        }

        /// <summary>
        /// Ӧ��Ԥ������
        /// </summary>
        public static void ApplyPreset(this QuestionWeightConfig config, WeightPresetType presetType)
        {
            if (config == null) return;

            try
            {
                switch (presetType)
                {
                    case WeightPresetType.Balanced:
                        ApplyBalancedPreset(config);
                        break;
                    case WeightPresetType.FillFocus:
                        ApplyFillFocusPreset(config);
                        break;
                    case WeightPresetType.ChoiceFocus:
                        ApplyChoiceFocusPreset(config);
                        break;
                    case WeightPresetType.Custom:
                        // �Զ���Ԥ�費���κθ���
                        break;
                }

                Debug.Log($"[QuestionWeightConfig] ��Ӧ��Ԥ��: {presetType}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[QuestionWeightConfig] Ӧ��Ԥ��ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// ��ȡ������ʾ����
        /// </summary>
        private static string GetQuestionTypeDisplayName(QuestionType questionType)
        {
            switch (questionType)
            {
                case QuestionType.IdiomChain: return "�������";
                case QuestionType.TextPinyin: return "ƴ�����";
                case QuestionType.HardFill: return "Ӳ�����";
                case QuestionType.SoftFill: return "�������";
                case QuestionType.SentimentTorF: return "����ж�";
                case QuestionType.SimularWordChoice: return "�����ѡ��";
                case QuestionType.UsageTorF: return "�÷��ж�";
                case QuestionType.ExplanationChoice: return "�������";
                default: return questionType.ToString();
            }
        }

        /// <summary>
        /// Ӧ�þ���Ԥ��
        /// </summary>
        private static void ApplyBalancedPreset(QuestionWeightConfig config)
        {
            var questionTypes = System.Enum.GetValues(typeof(QuestionType));
            foreach (QuestionType type in questionTypes)
            {
                config.SetEnabled(type, true);
                config.SetWeight(type, 1f);
            }
        }

        /// <summary>
        /// Ӧ������ص�Ԥ��
        /// </summary>
        private static void ApplyFillFocusPreset(QuestionWeightConfig config)
        {
            // ���������Ȩ�ظ���
            config.SetEnabled(QuestionType.IdiomChain, true);
            config.SetWeight(QuestionType.IdiomChain, 2f);

            config.SetEnabled(QuestionType.TextPinyin, true);
            config.SetWeight(QuestionType.TextPinyin, 3f);

            config.SetEnabled(QuestionType.HardFill, true);
            config.SetWeight(QuestionType.HardFill, 3f);

            config.SetEnabled(QuestionType.SoftFill, true);
            config.SetWeight(QuestionType.SoftFill, 2f);

            // ��������Ȩ�ؽϵ�
            config.SetEnabled(QuestionType.SentimentTorF, true);
            config.SetWeight(QuestionType.SentimentTorF, 0.5f);

            config.SetEnabled(QuestionType.SimularWordChoice, true);
            config.SetWeight(QuestionType.SimularWordChoice, 0.5f);

            config.SetEnabled(QuestionType.UsageTorF, true);
            config.SetWeight(QuestionType.UsageTorF, 0.5f);

            config.SetEnabled(QuestionType.ExplanationChoice, true);
            config.SetWeight(QuestionType.ExplanationChoice, 0.5f);

        }

        /// <summary>
        /// Ӧ��ѡ���ص�Ԥ��
        /// </summary>
        private static void ApplyChoiceFocusPreset(QuestionWeightConfig config)
        {
            // ѡ��������Ȩ�ظ���
            config.SetEnabled(QuestionType.SimularWordChoice, true);
            config.SetWeight(QuestionType.SimularWordChoice, 3f);

            config.SetEnabled(QuestionType.ExplanationChoice, true);
            config.SetWeight(QuestionType.ExplanationChoice, 3f);

            config.SetEnabled(QuestionType.SentimentTorF, true);
            config.SetWeight(QuestionType.SentimentTorF, 2f);

            config.SetEnabled(QuestionType.UsageTorF, true);
            config.SetWeight(QuestionType.UsageTorF, 2f);

            // ���������Ȩ�ؽϵ�
            config.SetEnabled(QuestionType.IdiomChain, true);
            config.SetWeight(QuestionType.IdiomChain, 0.5f);

            config.SetEnabled(QuestionType.TextPinyin, true);
            config.SetWeight(QuestionType.TextPinyin, 0.5f);

            config.SetEnabled(QuestionType.HardFill, true);
            config.SetWeight(QuestionType.HardFill, 0.5f);

            config.SetEnabled(QuestionType.SoftFill, true);
            config.SetWeight(QuestionType.SoftFill, 0.5f);

        }
    }

    /// <summary>
    /// Ȩ��Ԥ������ö��
    /// </summary>
    public enum WeightPresetType
    {
        Balanced,    // ����ģʽ
        FillFocus,   // ����ص�
        ChoiceFocus, // ѡ���ص�
        Custom       // �Զ���
    }
}