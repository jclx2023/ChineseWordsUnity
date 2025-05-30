using UnityEngine;
using System.Collections.Generic;
using Core;
using Core.Network;

namespace UI.RoomConfig
{
    /// <summary>
    /// QuestionWeightConfig的扩展方法
    /// 添加缺少的方法和功能
    /// </summary>
    public static class QuestionWeightConfigExtensions
    {
        /// <summary>
        /// 从另一个配置复制数据
        /// </summary>
        public static void CopyFrom(this QuestionWeightConfig target, QuestionWeightConfig source)
        {
            if (target == null || source == null) return;

            try
            {
                // 获取所有题型
                var questionTypes = System.Enum.GetValues(typeof(QuestionType));

                foreach (QuestionType type in questionTypes)
                {
                    bool enabled = source.IsEnabled(type);
                    float weight = source.GetWeight(type);

                    target.SetEnabled(type, enabled);
                    target.SetWeight(type, weight);
                }

                Debug.Log("[QuestionWeightConfig] 配置复制完成");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[QuestionWeightConfig] 配置复制失败: {e.Message}");
            }
        }

        /// <summary>
        /// 获取配置摘要字符串
        /// </summary>
        public static string GetConfigSummary(this QuestionWeightConfig config)
        {
            if (config == null) return "配置为空";

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

                return $"启用题型: {enabledCount}/10, 总权重: {totalWeight:F1}";
            }
            catch (System.Exception e)
            {
                return $"配置读取失败: {e.Message}";
            }
        }

        /// <summary>
        /// 重置为默认配置
        /// </summary>
        public static void ResetToDefault(this QuestionWeightConfig config)
        {
            if (config == null) return;

            try
            {
                // 获取所有题型
                var questionTypes = System.Enum.GetValues(typeof(QuestionType));

                foreach (QuestionType type in questionTypes)
                {
                    config.SetEnabled(type, true);  // 默认全部启用
                    config.SetWeight(type, 1.0f);   // 默认权重为1
                }

                Debug.Log("[QuestionWeightConfig] 已重置为默认配置");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[QuestionWeightConfig] 重置配置失败: {e.Message}");
            }
        }

        /// <summary>
        /// 验证配置有效性
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
        /// 获取详细配置信息
        /// </summary>
        public static string GetDetailedInfo(this QuestionWeightConfig config)
        {
            if (config == null) return "配置为空";

            try
            {
                var info = "=== 权重配置详情 ===\n";
                var questionTypes = System.Enum.GetValues(typeof(QuestionType));

                foreach (QuestionType type in questionTypes)
                {
                    bool enabled = config.IsEnabled(type);
                    float weight = config.GetWeight(type);
                    string displayName = GetQuestionTypeDisplayName(type);

                    info += $"{displayName}: {(enabled ? $"{weight:F1}" : "禁用")}\n";
                }

                var weights = config.GetWeights();
                float totalWeight = 0f;
                foreach (var w in weights.Values)
                    totalWeight += w;

                info += $"\n总计: {weights.Count}/10 启用, 总权重 {totalWeight:F1}";

                return info;
            }
            catch (System.Exception e)
            {
                return $"获取配置信息失败: {e.Message}";
            }
        }

        /// <summary>
        /// 应用预设配置
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
                        // 自定义预设不做任何更改
                        break;
                }

                Debug.Log($"[QuestionWeightConfig] 已应用预设: {presetType}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[QuestionWeightConfig] 应用预设失败: {e.Message}");
            }
        }

        /// <summary>
        /// 获取题型显示名称
        /// </summary>
        private static string GetQuestionTypeDisplayName(QuestionType questionType)
        {
            switch (questionType)
            {
                case QuestionType.IdiomChain: return "成语接龙";
                case QuestionType.TextPinyin: return "拼音填空";
                case QuestionType.HardFill: return "硬性填空";
                case QuestionType.SoftFill: return "软性填空";
                case QuestionType.SentimentTorF: return "情感判断";
                case QuestionType.SimularWordChoice: return "近义词选择";
                case QuestionType.UsageTorF: return "用法判断";
                case QuestionType.ExplanationChoice: return "词语解释";
                default: return questionType.ToString();
            }
        }

        /// <summary>
        /// 应用均衡预设
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
        /// 应用填空重点预设
        /// </summary>
        private static void ApplyFillFocusPreset(QuestionWeightConfig config)
        {
            // 填空类题型权重更高
            config.SetEnabled(QuestionType.IdiomChain, true);
            config.SetWeight(QuestionType.IdiomChain, 2f);

            config.SetEnabled(QuestionType.TextPinyin, true);
            config.SetWeight(QuestionType.TextPinyin, 3f);

            config.SetEnabled(QuestionType.HardFill, true);
            config.SetWeight(QuestionType.HardFill, 3f);

            config.SetEnabled(QuestionType.SoftFill, true);
            config.SetWeight(QuestionType.SoftFill, 2f);

            // 其他题型权重较低
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
        /// 应用选择重点预设
        /// </summary>
        private static void ApplyChoiceFocusPreset(QuestionWeightConfig config)
        {
            // 选择类题型权重更高
            config.SetEnabled(QuestionType.SimularWordChoice, true);
            config.SetWeight(QuestionType.SimularWordChoice, 3f);

            config.SetEnabled(QuestionType.ExplanationChoice, true);
            config.SetWeight(QuestionType.ExplanationChoice, 3f);

            config.SetEnabled(QuestionType.SentimentTorF, true);
            config.SetWeight(QuestionType.SentimentTorF, 2f);

            config.SetEnabled(QuestionType.UsageTorF, true);
            config.SetWeight(QuestionType.UsageTorF, 2f);

            // 填空类题型权重较低
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
    /// 权重预设类型枚举
    /// </summary>
    public enum WeightPresetType
    {
        Balanced,    // 均衡模式
        FillFocus,   // 填空重点
        ChoiceFocus, // 选择重点
        Custom       // 自定义
    }
}