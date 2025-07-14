using UnityEngine;
using Core;

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

            LogDebug("初始化 HPConfigManager");

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
            LogDebug($"尝试设置HP配置: {newConfig?.ConfigName ?? "null"} (InstanceID: {newConfig?.GetInstanceID()})");

            if (newConfig == null)
            {
                LogDebug("警告：尝试设置空的HP配置，保持当前配置不变");
                return;
            }

            // 关键修复：确保初始化标记正确，避免后续被覆盖
            initialized = true;
            Config = newConfig;

            LogDebug($"HP配置已成功设置: {newConfig.ConfigName} (InstanceID: {newConfig.GetInstanceID()})");

            // 验证设置是否成功
            var currentConfig = config;  // 直接访问私有字段，避免触发getter逻辑
            bool referenceMatch = (newConfig == currentConfig);
            LogDebug($"设置后引用验证: {(referenceMatch ? "✓ 成功" : "✗ 失败")}");

            if (!referenceMatch)
            {
                Debug.LogError($"[HPConfigManager] 配置设置失败！预期ID: {newConfig.GetInstanceID()}, 实际ID: {currentConfig?.GetInstanceID()}");
            }

            // 可选：输出配置摘要用于调试
            if (enableDebugLogs)
            {
                LogDebug($"配置摘要: {newConfig.GetConfigSummary()}");
            }
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
        /// 验证当前配置的有效性
        /// </summary>
        /// <returns>配置是否有效</returns>
        public static bool ValidateCurrentConfig()
        {
            if (Config == null)
            {
                LogDebug("配置验证失败：配置为空");
                return false;
            }

            try
            {
                bool isValid = Config.ValidateConfig();
                LogDebug($"配置验证结果: {(isValid ? "有效" : "无效")}");
                return isValid;
            }
            catch (System.Exception e)
            {
                LogDebug($"配置验证异常: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取当前生命值
        /// </summary>
        /// <returns>当前生命值</returns>
        public static float GetCurrentHealth()
        {
            if (Config == null)
            {
                LogDebug($"HP配置未设置，使用默认生命值: {GetDefaultHealth()}");
                return GetDefaultHealth();
            }

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
            if (Config == null)
            {
                LogDebug($"HP配置未设置，使用默认扣血量: {GetDefaultDamage()}");
                return GetDefaultDamage();
            }

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
        /// 获取最大生命值（等同于当前生命值）
        /// </summary>
        /// <returns>最大生命值</returns>
        public static float GetMaxHealth()
        {
            return GetCurrentHealth(); // HP系统中最大生命值等于初始生命值
        }

        /// <summary>
        /// 获取最多可答错次数
        /// </summary>
        /// <returns>最多可答错次数</returns>
        public static int GetMaxWrongAnswers()
        {
            if (Config == null)
            {
                LogDebug("HP配置未设置，计算默认可答错次数");
                return Mathf.FloorToInt(GetDefaultHealth() / GetDefaultDamage());
            }

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
        /// 获取配置摘要信息
        /// </summary>
        /// <returns>配置摘要字符串</returns>
        public static string GetConfigSummary()
        {
            if (Config == null)
            {
                return "HP配置未设置";
            }

            try
            {
                string summary = Config.GetConfigSummary();
                LogDebug($"获取配置摘要: {summary} (配置ID: {Config.GetInstanceID()})");
                return summary;
            }
            catch (System.Exception e)
            {
                LogDebug($"获取配置摘要失败: {e.Message}");
                return "配置摘要获取失败";
            }
        }

        /// <summary>
        /// 重置为默认配置
        /// </summary>
        public static void ResetToDefault()
        {
            LogDebug("重置HP配置为默认值");

            if (Config != null)
            {
                try
                {
                    // 优先重置当前配置，保持引用不变
                    Config.ResetToDefault();
                    LogDebug($"当前配置已重置 (配置ID: {Config.GetInstanceID()})");
                    return;
                }
                catch (System.Exception e)
                {
                    LogDebug($"重置当前配置失败: {e.Message}，将加载新的默认配置");
                }
            }

            // 如果重置失败或配置为空，加载新的默认配置
            LoadDefaultConfig();
        }

        /// <summary>
        /// 获取配置状态信息（调试用）
        /// </summary>
        /// <returns>详细的配置状态信息</returns>
        public static string GetConfigStatus()
        {
            var status = "=== HPConfigManager状态 ===\n";
            status += $"已初始化: {initialized}\n";
            status += $"配置实例: {(Config != null ? "存在" : "不存在")}\n";

            if (Config != null)
            {
                status += $"配置名称: {Config.ConfigName}\n";
                status += $"配置ID: {Config.GetInstanceID()}\n";
                status += $"配置有效性: {ValidateCurrentConfig()}\n";

                try
                {
                    var settings = GetHPSettings();
                    status += $"当前生命值: {settings.currentHealth}\n";
                    status += $"答错扣血: {settings.damagePerWrong}\n";
                    status += $"最多答错: {settings.maxWrongAnswers}次\n";

                    float survivalRate = (float)settings.maxWrongAnswers / (settings.maxWrongAnswers + 1) * 100f;
                    status += $"容错率: {survivalRate:F1}%\n";
                }
                catch (System.Exception e)
                {
                    status += $"获取配置详情失败: {e.Message}\n";
                }
            }

            return status;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[HPConfigManager] {message}");
#endif
            }
        }

        /// <summary>
        /// 设置调试日志开关
        /// </summary>
        public static void SetDebugLogs(bool enabled)
        {
            enableDebugLogs = enabled;
            LogDebug($"调试日志已{(enabled ? "启用" : "禁用")}");
        }

#if UNITY_EDITOR
        /// <summary>
        /// 编辑器专用：强制重新初始化
        /// </summary>
        [UnityEditor.MenuItem("Tools/HP Config/Force Reinitialize")]
        public static void ForceReinitialize()
        {
            initialized = false;
            config = null;
            Initialize();
            Debug.Log("[HPConfigManager] 强制重新初始化完成");
        }

        /// <summary>
        /// 编辑器专用：显示当前配置
        /// </summary>
        [UnityEditor.MenuItem("Tools/HP Config/Show Current Config")]
        public static void ShowCurrentConfig()
        {
            Debug.Log($"[HPConfigManager] 当前配置状态:\n{GetConfigStatus()}");
        }

        /// <summary>
        /// 编辑器专用：显示配置摘要
        /// </summary>
        [UnityEditor.MenuItem("Tools/HP Config/Show Config Summary")]
        public static void ShowConfigSummary()
        {
            Debug.Log($"[HPConfigManager] 当前配置摘要:\n{GetConfigSummary()}");
        }

        /// <summary>
        /// 编辑器专用：测试获取HP设置
        /// </summary>
        [UnityEditor.MenuItem("Tools/HP Config/Test HP Settings")]
        public static void TestHPSettings()
        {
            Debug.Log("[HPConfigManager] 测试HP设置:");

            var settings = GetHPSettings();
            Debug.Log($"  当前生命值: {settings.currentHealth}");
            Debug.Log($"  答错扣血: {settings.damagePerWrong}");
            Debug.Log($"  最多答错: {settings.maxWrongAnswers}次");

            float survivalRate = (float)settings.maxWrongAnswers / (settings.maxWrongAnswers + 1) * 100f;
            Debug.Log($"  容错率: {survivalRate:F1}%");
        }

        /// <summary>
        /// 编辑器专用：验证配置引用一致性
        /// </summary>
        [UnityEditor.MenuItem("Tools/HP Config/Validate Config References")]
        public static void ValidateConfigReferences()
        {
            Debug.Log("[HPConfigManager] 验证配置引用一致性:");

            var config1 = Config;
            var config2 = Config;

            Debug.Log($"两次获取的配置引用是否相同: {config1 == config2}");
            Debug.Log($"配置1 ID: {config1?.GetInstanceID()}");
            Debug.Log($"配置2 ID: {config2?.GetInstanceID()}");

            if (config1 != null)
            {
                var healthBefore = config1.GetCurrentHealth();
                Debug.Log($"修改前生命值: {healthBefore}");

                // 模拟修改
                config1.SetCurrentHealth(999f);

                var healthAfter = Config.GetCurrentHealth();
                Debug.Log($"修改后生命值: {healthAfter}");
                Debug.Log($"修改是否生效: {healthAfter == 999f}");

                // 恢复原值
                config1.SetCurrentHealth(healthBefore);
            }
        }

        /// <summary>
        /// 编辑器专用：测试创建默认配置
        /// </summary>
        [UnityEditor.MenuItem("Tools/HP Config/Test Create Default")]
        public static void TestCreateDefault()
        {
            CreateRuntimeDefaultConfig();
            Debug.Log("[HPConfigManager] 测试创建默认配置完成");
        }
#endif
    }
}