using UnityEngine;

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

            // 验证题目设置
            if (questionSettings.weightConfig == null)
            {
                Debug.LogWarning("[RoomGameConfig] 题型权重配置未设置");
            }

            // 验证Timer配置
            if (questionSettings.timerConfig == null)
            {
                Debug.LogWarning("[RoomGameConfig] Timer配置未设置");
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

    }
}