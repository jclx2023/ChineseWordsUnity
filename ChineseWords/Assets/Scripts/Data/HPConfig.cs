using UnityEngine;

namespace Core
{
    /// <summary>
    /// HP配置数据结构
    /// 管理玩家生命值和扣血相关设置
    /// </summary>
    [CreateAssetMenu(fileName = "HPConfig", menuName = "Game/HP Config")]
    public class HPConfig : ScriptableObject
    {
        [Header("生命值配置")]
        [Range(10f, 500f)]
        [Tooltip("当前/初始生命值")]
        public float currentHealth = 100f;

        [Range(1f, 100f)]
        [Tooltip("每次答错扣除的血量")]
        public float damagePerWrong = 20f;

        [Header("元数据")]
        [SerializeField] private string configName = "默认HP配置";
        [SerializeField] private string configDescription = "标准生命值和扣血配置";

        /// <summary>
        /// 配置名称
        /// </summary>
        public string ConfigName => configName;

        /// <summary>
        /// 配置描述
        /// </summary>
        public string ConfigDescription => configDescription;

        /// <summary>
        /// 获取当前生命值（也是初始/最大生命值）
        /// </summary>
        public float GetCurrentHealth() => currentHealth;

        /// <summary>
        /// 获取每次答错的扣血量
        /// </summary>
        public float GetDamagePerWrong() => damagePerWrong;

        /// <summary>
        /// 获取最大生命值（等同于当前生命值）
        /// </summary>
        public float GetMaxHealth() => currentHealth;

        /// <summary>
        /// 计算最多可以答错的次数
        /// </summary>
        public int GetMaxWrongAnswers()
        {
            if (damagePerWrong <= 0) return 0;
            return Mathf.FloorToInt(currentHealth / damagePerWrong);
        }

        /// <summary>
        /// 设置生命值
        /// </summary>
        /// <param name="health">新的生命值</param>
        public void SetCurrentHealth(float health)
        {
            currentHealth = Mathf.Clamp(health, 10f, 500f);
        }

        /// <summary>
        /// 设置扣血量
        /// </summary>
        /// <param name="damage">新的扣血量</param>
        public void SetDamagePerWrong(float damage)
        {
            damagePerWrong = Mathf.Clamp(damage, 1f, 100f);
        }

        /// <summary>
        /// 验证配置的有效性
        /// </summary>
        public bool ValidateConfig()
        {
            bool isValid = true;

            // 检查生命值
            if (currentHealth <= 0)
            {
                Debug.LogError("[HPConfig] 生命值不能小于等于0");
                isValid = false;
            }

            if (currentHealth < 10f)
            {
                Debug.LogError("[HPConfig] 生命值过低，建议至少10点");
                isValid = false;
            }

            // 检查扣血量
            if (damagePerWrong <= 0)
            {
                Debug.LogError("[HPConfig] 扣血量不能小于等于0");
                isValid = false;
            }

            if (damagePerWrong > currentHealth)
            {
                Debug.LogError("[HPConfig] 扣血量不能大于生命值");
                isValid = false;
            }

            // 检查合理性
            if (GetMaxWrongAnswers() < 1)
            {
                Debug.LogError("[HPConfig] 至少要能答错1题");
                isValid = false;
            }

            if (GetMaxWrongAnswers() > 50)
            {
                Debug.LogWarning("[HPConfig] 可答错次数过多，游戏可能过于简单");
            }

            return isValid;
        }

        /// <summary>
        /// 重置为默认配置
        /// </summary>
        public void ResetToDefault()
        {
            currentHealth = 100f;
            damagePerWrong = 20f;
            configName = "默认HP配置";
            configDescription = "标准生命值和扣血配置";

            Debug.Log("[HPConfig] 配置已重置为默认值");
        }

        /// <summary>
        /// 获取配置摘要
        /// </summary>
        public string GetConfigSummary()
        {
            var summary = $"=== {configName} ===\n";
            summary += $"描述: {configDescription}\n\n";
            summary += $"生命值配置:\n";
            summary += $"  初始生命值: {currentHealth}点\n";
            summary += $"  答错扣血: {damagePerWrong}点\n";
            summary += $"  最多可答错: {GetMaxWrongAnswers()}题\n";

            // 计算生存率相关信息
            float survivalRate = (float)GetMaxWrongAnswers() / (GetMaxWrongAnswers() + 1) * 100f;
            summary += $"  容错率: {survivalRate:F1}%\n";

            return summary;
        }

        /// <summary>
        /// 创建配置的深拷贝
        /// </summary>
        public HPConfig CreateCopy()
        {
            var copy = CreateInstance<HPConfig>();

            copy.currentHealth = currentHealth;
            copy.damagePerWrong = damagePerWrong;
            copy.configName = configName + " (副本)";
            copy.configDescription = configDescription;

            return copy;
        }

        /// <summary>
        /// 从另一个配置复制数据
        /// </summary>
        /// <param name="other">源配置</param>
        public void CopyFrom(HPConfig other)
        {
            if (other == null)
            {
                Debug.LogWarning("[HPConfig] 尝试从空配置复制数据");
                return;
            }

            currentHealth = other.currentHealth;
            damagePerWrong = other.damagePerWrong;
            configDescription = other.configDescription;
            // 不复制配置名称，保持当前名称

            Debug.Log($"[HPConfig] 已从 {other.configName} 复制配置数据");
        }

        /// <summary>
        /// 检查配置是否有未保存的更改
        /// </summary>
        public bool HasUnsavedChanges()
        {
            // 这里可以添加脏标记逻辑
            // 目前简化为始终返回false
            return false;
        }

        /// <summary>
        /// 标记配置为干净状态（已保存）
        /// </summary>
        public void ClearDirty()
        {
            // 清除脏标记
            // 目前简化为空实现
        }

        /// <summary>
        /// 获取配置的详细信息（调试用）
        /// </summary>
        public string GetDetailedInfo()
        {
            var info = $"=== HP配置详细信息 ===\n";
            info += $"配置名称: {configName}\n";
            info += $"配置描述: {configDescription}\n";
            info += $"实例ID: {GetInstanceID()}\n";
            info += $"当前生命值: {currentHealth}\n";
            info += $"扣血量: {damagePerWrong}\n";
            info += $"最大答错次数: {GetMaxWrongAnswers()}\n";
            info += $"配置有效性: {(ValidateConfig() ? "有效" : "无效")}\n";
            info += $"容错率: {((float)GetMaxWrongAnswers() / (GetMaxWrongAnswers() + 1) * 100f):F1}%\n";

            return info;
        }

        private void OnValidate()
        {
            // 在Inspector中修改时自动验证和约束数值
            currentHealth = Mathf.Clamp(currentHealth, 10f, 500f);
            damagePerWrong = Mathf.Clamp(damagePerWrong, 1f, 100f);

            // 确保扣血量不超过生命值
            if (damagePerWrong > currentHealth)
            {
                damagePerWrong = currentHealth;
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
            Debug.Log($"[HPConfig] 配置验证结果: {(isValid ? "通过" : "失败")}");
        }

        [ContextMenu("重置为默认配置")]
        public void ResetToDefaultEditor()
        {
            ResetToDefault();
            Debug.Log("[HPConfig] 已重置为默认配置");
        }

        [ContextMenu("显示详细信息")]
        public void ShowDetailedInfo()
        {
            Debug.Log(GetDetailedInfo());
        }

        [ContextMenu("测试创建副本")]
        public void TestCreateCopy()
        {
            var copy = CreateCopy();
            Debug.Log($"[HPConfig] 创建副本成功: {copy.configName} (ID: {copy.GetInstanceID()})");
        }
#endif
    }
}