using UnityEngine;
using System.Collections.Generic;
using Core;
using UI;

namespace Core.Network
{
    /// <summary>
    /// 房间游戏配置数据结构
    /// 包含所有游戏相关的配置参数
    /// </summary>
    [CreateAssetMenu(fileName = "RoomGameConfig", menuName = "Game/Room Game Config")]
    public class RoomGameConfig : ScriptableObject
    {
        [System.Serializable]
        public class TimerSettings
        {
            [Header("计时器设置")]
            [Range(5f, 120f)]
            public float questionTimeLimit = 30f;

            [Range(0f, 10f)]
            public float timeUpDelay = 1f;

            [Tooltip("是否显示倒计时警告")]
            public bool showTimeWarning = true;

            [Range(1f, 10f)]
            public float warningTimeThreshold = 5f;
        }

        [System.Serializable]
        public class HPSettings
        {
            [Header("血量设置")]
            [Range(20, 200)]
            public int initialPlayerHealth = 100;

            [Range(5, 50)]
            public int damagePerWrongAnswer = 20;
        }

        [System.Serializable]
        public class QuestionSettings
        {
            [Header("题目设置")]
            [Tooltip("题目权重配置引用")]
            public QuestionWeightConfig weightConfig;

            [Header("Timer配置")]
            [Tooltip("Timer配置引用")]
            public TimerConfig timerConfig;
        }

        [Header("配置分组")]
        public TimerSettings timerSettings = new TimerSettings();
        public HPSettings hpSettings = new HPSettings();
        public QuestionSettings questionSettings = new QuestionSettings();

        [Header("元数据")]
        [SerializeField] private string configName = "默认配置";
        [SerializeField] private string configDescription = "标准游戏配置";
        [SerializeField] private int configVersion = 1;

        // 运行时数据
        [System.NonSerialized]
        private bool isDirty = false;

        /// <summary>
        /// 配置名称
        /// </summary>
        public string ConfigName => configName;

        /// <summary>
        /// 配置描述
        /// </summary>
        public string ConfigDescription => configDescription;

        /// <summary>
        /// 配置版本
        /// </summary>
        public int ConfigVersion => configVersion;

        /// <summary>
        /// 配置是否已修改
        /// </summary>
        public bool IsDirty => isDirty;

        /// <summary>
        /// 创建配置的深拷贝
        /// </summary>
        public RoomGameConfig CreateCopy()
        {
            var copy = CreateInstance<RoomGameConfig>();

            // 深拷贝各个设置
            copy.timerSettings = JsonUtility.FromJson<TimerSettings>(JsonUtility.ToJson(timerSettings));
            copy.hpSettings = JsonUtility.FromJson<HPSettings>(JsonUtility.ToJson(hpSettings));
            copy.questionSettings = JsonUtility.FromJson<QuestionSettings>(JsonUtility.ToJson(questionSettings));

            copy.configName = configName + " (副本)";
            copy.configDescription = configDescription;
            copy.configVersion = configVersion;

            return copy;
        }

        /// <summary>
        /// 重置为默认配置
        /// </summary>
        public void ResetToDefault()
        {
            timerSettings = new TimerSettings();
            hpSettings = new HPSettings();
            questionSettings = new QuestionSettings();

            MarkDirty();

            Debug.Log("[RoomGameConfig] 配置已重置为默认值");
        }

        /// <summary>
        /// 验证配置有效性
        /// </summary>
        public bool ValidateConfig()
        {
            bool isValid = true;

            // 验证计时器设置
            if (timerSettings.questionTimeLimit <= 0)
            {
                Debug.LogError("[RoomGameConfig] 题目时间限制必须大于0");
                isValid = false;
            }

            // 验证血量设置
            if (hpSettings.initialPlayerHealth <= 0)
            {
                Debug.LogError("[RoomGameConfig] 初始血量必须大于0");
                isValid = false;
            }

            if (hpSettings.damagePerWrongAnswer <= 0)
            {
                Debug.LogError("[RoomGameConfig] 答错扣血量必须大于0");
                isValid = false;
            }

            // 验证题目设置
            if (questionSettings.weightConfig == null)
            {
                Debug.LogWarning("[RoomGameConfig] 题型权重配置未设置");
                // 这不算致命错误，可以使用默认权重
            }

            // 验证Timer配置
            if (questionSettings.timerConfig == null)
            {
                Debug.LogWarning("[RoomGameConfig] Timer配置未设置");
                // 这不算致命错误，可以使用默认Timer配置
            }
            else if (!questionSettings.timerConfig.ValidateConfig())
            {
                Debug.LogError("[RoomGameConfig] Timer配置验证失败");
                isValid = false;
            }

            return isValid;
        }

        /// <summary>
        /// 标记配置为已修改
        /// </summary>
        public void MarkDirty()
        {
            isDirty = true;
        }

        /// <summary>
        /// 清除修改标记
        /// </summary>
        public void ClearDirty()
        {
            isDirty = false;
        }

        /// <summary>
        /// 序列化为JSON
        /// </summary>
        public string ToJson()
        {
            return JsonUtility.ToJson(this, true);
        }

        /// <summary>
        /// 从JSON反序列化
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
                Debug.LogError($"[RoomGameConfig] JSON反序列化失败: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取配置摘要
        /// </summary>
        public string GetConfigSummary()
        {
            var summary = $"=== {configName} ===\n";
            summary += $"描述: {configDescription}\n";
            summary += $"版本: {configVersion}\n\n";

            summary += $"计时器: {timerSettings.questionTimeLimit}秒\n";
            summary += $"初始血量: {hpSettings.initialPlayerHealth}\n";
            summary += $"答错扣血: {hpSettings.damagePerWrongAnswer}\n";
            summary += $"权重配置: {(questionSettings.weightConfig != null ? "已设置" : "未设置")}\n";
            summary += $"Timer配置: {(questionSettings.timerConfig != null ? "已设置" : "未设置")}\n";

            // 如果有Timer配置，显示详细信息
            if (questionSettings.timerConfig != null)
            {
                summary += "\nTimer配置详情:\n";
                summary += questionSettings.timerConfig.GetConfigSummary();
            }

            return summary;
        }

        /// <summary>
        /// 获取当前有效的Timer配置
        /// </summary>
        public TimerConfig GetEffectiveTimerConfig()
        {
            // 如果设置了Timer配置，返回设置的配置
            if (questionSettings.timerConfig != null)
            {
                return questionSettings.timerConfig;
            }

            // 如果没有设置，返回TimerConfigManager的当前配置
            if (TimerConfigManager.IsConfigured())
            {
                return TimerConfigManager.Config;
            }

            // 都没有的话，创建一个默认配置
            Debug.LogWarning("[RoomGameConfig] 没有可用的Timer配置，创建临时默认配置");
            var tempConfig = ScriptableObject.CreateInstance<TimerConfig>();
            tempConfig.ResetToDefault();
            return tempConfig;
        }

        /// <summary>
        /// 设置Timer配置
        /// </summary>
        public void SetTimerConfig(TimerConfig timerConfig)
        {
            questionSettings.timerConfig = timerConfig;
            MarkDirty();
            Debug.Log($"[RoomGameConfig] Timer配置已设置: {timerConfig?.ConfigName ?? "null"}");
        }

        /// <summary>
        /// 比较两个配置是否相同
        /// </summary>
        public bool Equals(RoomGameConfig other)
        {
            if (other == null) return false;

            return JsonUtility.ToJson(this) == JsonUtility.ToJson(other);
        }

        private void OnValidate()
        {
            // 确保数值在合理范围内
            timerSettings.questionTimeLimit = Mathf.Clamp(timerSettings.questionTimeLimit, 5f, 120f);
            hpSettings.initialPlayerHealth = Mathf.Clamp(hpSettings.initialPlayerHealth, 20, 200);
            hpSettings.damagePerWrongAnswer = Mathf.Clamp(hpSettings.damagePerWrongAnswer, 5, 50);

            if (Application.isPlaying)
            {
                MarkDirty();
            }
        }

#if UNITY_EDITOR
        [ContextMenu("显示配置摘要")]
        public void ShowConfigSummary()
        {
            Debug.Log(GetConfigSummary());
        }

        [ContextMenu("验证配置")]
        public void ValidateConfigEditor()
        {
            bool isValid = ValidateConfig();
            Debug.Log($"[RoomGameConfig] 配置验证结果: {(isValid ? "通过" : "失败")}");
        }

        [ContextMenu("测试序列化")]
        public void TestSerialization()
        {
            string json = ToJson();
            Debug.Log($"[RoomGameConfig] 序列化结果:\n{json}");

            var deserialized = FromJson(json);
            bool isEqual = Equals(deserialized);
            Debug.Log($"[RoomGameConfig] 反序列化测试: {(isEqual ? "成功" : "失败")}");
        }

        [ContextMenu("测试Timer配置")]
        public void TestTimerConfig()
        {
            var timerConfig = GetEffectiveTimerConfig();
            if (timerConfig != null)
            {
                Debug.Log($"[RoomGameConfig] 当前有效Timer配置:\n{timerConfig.GetConfigSummary()}");
            }
            else
            {
                Debug.Log("[RoomGameConfig] 没有可用的Timer配置");
            }
        }
#endif
    }
}