using UnityEngine;

namespace Core
{
    /// <summary>
    /// HP配置管理器 - 仿照TimerConfigManager模式
    /// 确保UI修改能正确反映到配置中
    /// </summary>
    public static class HPConfigManager
    {
        private static HPConfig config;
        private static bool initialized = false;
        private static bool enableDebugLogs = true;

        /// <summary>
        /// 当前HP配置
        /// </summary>
        public static HPConfig Config
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
                LogDebug($"HP配置引用已更新: {config?.ConfigName ?? "null"} (InstanceID: {config?.GetInstanceID()})");
            }
        }

        /// <summary>
        /// 初始化HP配置管理器
        /// </summary>
        public static void Initialize()
        {
            if (initialized)
            {
                LogDebug("HPConfigManager 已经初始化");
                return;
            }

            // 尝试加载默认配置
            LoadDefaultConfig();

            initialized = true;
            LogDebug($"HPConfigManager 初始化完成，当前配置: {config?.ConfigName ?? "未设置"} (InstanceID: {config?.GetInstanceID()})");
        }

        /// <summary>
        /// 设置HP配置 - 修复版本，确保引用正确传递
        /// </summary>
        /// <param name="newConfig">新的HP配置</param>
        public static void SetConfig(HPConfig newConfig)
        {
            // 关键修复：确保初始化标记正确，避免后续被覆盖
            initialized = true;
            Config = newConfig;

            LogDebug($"HP配置已成功设置: {newConfig.ConfigName} (InstanceID: {newConfig.GetInstanceID()})");

            // 验证设置是否成功
            var currentConfig = config;  // 直接访问私有字段，避免触发getter逻辑
            bool referenceMatch = (newConfig == currentConfig);
        }

        /// <summary>
        /// 强制设置配置（跳过所有检查）
        /// </summary>
        /// <param name="newConfig">新的HP配置</param>
        public static void ForceSetConfig(HPConfig newConfig)
        {
            LogDebug($"强制设置HP配置: {newConfig?.ConfigName ?? "null"} (ID: {newConfig?.GetInstanceID()})");

            // 关键修复：先标记为已初始化，避免后续Initialize覆盖
            initialized = true;
            config = newConfig;

            LogDebug($"强制设置完成，当前配置引用: {config?.GetInstanceID()}");
        }

        /// <summary>
        /// 获取当前生命值
        /// </summary>
        /// <returns>当前生命值</returns>
        public static float GetCurrentHealth()
        {
            try
            {
                float health = Config.GetCurrentHealth();
                LogDebug($"获取当前生命值: {health} (配置ID: {Config.GetInstanceID()})");
                return health;
            }
            catch (System.Exception e)
            {
                LogDebug($"获取生命值失败: {e.Message}，使用默认值");
                return GetDefaultHealth();
            }
        }

        /// <summary>
        /// 获取每次答错的扣血量
        /// </summary>
        /// <returns>每次答错的扣血量</returns>
        public static float GetDamagePerWrong()
        {
            try
            {
                float damage = Config.GetDamagePerWrong();
                LogDebug($"获取答错扣血量: {damage} (配置ID: {Config.GetInstanceID()})");
                return damage;
            }
            catch (System.Exception e)
            {
                LogDebug($"获取扣血量失败: {e.Message}，使用默认值");
                return GetDefaultDamage();
            }
        }

        /// <summary>
        /// 获取最多可答错次数
        /// </summary>
        /// <returns>最多可答错次数</returns>
        public static int GetMaxWrongAnswers()
        {
            try
            {
                int maxWrong = Config.GetMaxWrongAnswers();
                LogDebug($"获取最多可答错次数: {maxWrong} (配置ID: {Config.GetInstanceID()})");
                return maxWrong;
            }
            catch (System.Exception e)
            {
                LogDebug($"计算可答错次数失败: {e.Message}，使用默认计算");
                return Mathf.FloorToInt(GetDefaultHealth() / GetDefaultDamage());
            }
        }

        /// <summary>
        /// 获取HP设置信息（生命值，扣血量）
        /// </summary>
        /// <returns>HP设置元组</returns>
        public static (float currentHealth, float damagePerWrong, int maxWrongAnswers) GetHPSettings()
        {
            float health = GetCurrentHealth();
            float damage = GetDamagePerWrong();
            int maxWrong = GetMaxWrongAnswers();

            LogDebug($"获取完整HP设置: 生命值={health}, 扣血={damage}, 最多答错={maxWrong}次");
            return (health, damage, maxWrong);
        }

        /// <summary>
        /// 检查HP配置是否已设置
        /// </summary>
        /// <returns>是否已设置配置</returns>
        public static bool IsConfigured()
        {
            bool configured = initialized && Config != null;
            LogDebug($"配置状态检查: 已初始化={initialized}, 配置存在={Config != null}, 整体状态={configured}");
            return configured;
        }


        /// <summary>
        /// 加载默认配置
        /// </summary>
        private static void LoadDefaultConfig()
        {
            LogDebug("尝试加载默认HP配置");

            // 方法1：从你的具体路径加载
            var resourceConfig = Resources.Load<HPConfig>("QuestionConfigs/HPConfig");
            if (resourceConfig != null)
            {
                Config = resourceConfig;
                LogDebug($"从Resources/Questions路径加载HP配置: {resourceConfig.ConfigName} (ID: {resourceConfig.GetInstanceID()})");
                return;
            }
            // 方法2：创建运行时默认配置
            LogDebug("未找到HP配置文件，创建运行时默认配置");
            CreateRuntimeDefaultConfig();
        }

        /// <summary>
        /// 创建运行时默认配置
        /// </summary>
        private static void CreateRuntimeDefaultConfig()
        {
            var defaultConfig = ScriptableObject.CreateInstance<HPConfig>();
            defaultConfig.ResetToDefault();
            Config = defaultConfig;
            LogDebug($"运行时默认HP配置创建完成 (ID: {defaultConfig.GetInstanceID()})");
        }

        /// <summary>
        /// 获取默认生命值
        /// </summary>
        private static float GetDefaultHealth()
        {
            return 100f; // 默认100点生命值
        }

        /// <summary>
        /// 获取默认扣血量
        /// </summary>
        private static float GetDefaultDamage()
        {
            return 20f; // 默认答错扣20血
        }

        /// <summary>
        /// 调试日志输出
        /// </summary>
        private static void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[HPConfigManager] {message}");
            }
        }
    }
}