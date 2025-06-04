using UnityEngine;
using System.Collections.Generic;
using Core;

namespace Core.Network
{
    /// <summary>
    /// 玩家HP管理器
    /// 专门负责玩家血量的初始化、伤害计算、状态管理
    /// </summary>
    public class PlayerHPManager
    {
        [Header("配置设置")]
        private HPConfig customHPConfig;
        private bool useCustomConfig = false;

        [Header("调试设置")]
        private bool enableDebugLogs = true;

        // HP配置缓存
        private HPConfig currentHPConfig;
        private int cachedInitialHealth = -1;
        private int cachedDamageAmount = -1;
        private bool hpConfigInitialized = false;

        // 玩家HP状态管理
        private Dictionary<ushort, PlayerHPState> playerHPStates;

        // 事件定义
        public System.Action<ushort, int, int> OnHealthChanged; // playerId, newHealth, maxHealth
        public System.Action<ushort> OnPlayerDied;             // playerId
        public System.Action<ushort, float> OnHealthPercentageChanged; // playerId, healthPercentage

        /// <summary>
        /// 玩家HP状态数据结构
        /// </summary>
        private class PlayerHPState
        {
            public ushort playerId;
            public int currentHealth;
            public int maxHealth;
            public bool isAlive;
            public int damageCount; // 受到伤害的次数
            public float lastDamageTime; // 最后一次受伤时间

            public PlayerHPState(ushort id, int health)
            {
                playerId = id;
                currentHealth = health;
                maxHealth = health;
                isAlive = true;
                damageCount = 0;
                lastDamageTime = 0f;
            }

            public float GetHealthPercentage()
            {
                if (maxHealth <= 0) return 0f;
                return (float)currentHealth / maxHealth;
            }

            public bool IsLowHealth(float threshold = 0.3f)
            {
                return GetHealthPercentage() <= threshold;
            }

            public bool IsCriticalHealth(float threshold = 0.1f)
            {
                return GetHealthPercentage() <= threshold;
            }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public PlayerHPManager()
        {
            playerHPStates = new Dictionary<ushort, PlayerHPState>();
            LogDebug("PlayerHPManager 实例已创建");
        }

        #region 初始化

        /// <summary>
        /// 初始化HP管理器
        /// </summary>
        /// <param name="customConfig">自定义HP配置（可选）</param>
        /// <param name="useCustom">是否使用自定义配置</param>
        public void Initialize(HPConfig customConfig = null, bool useCustom = false)
        {
            LogDebug("初始化PlayerHPManager...");

            customHPConfig = customConfig;
            useCustomConfig = useCustom;

            try
            {
                // 确保HPConfigManager已初始化
                if (!HPConfigManager.IsConfigured())
                {
                    HPConfigManager.Initialize();
                }

                // 设置当前HP配置
                SetupHPConfiguration();

                // 验证配置有效性
                ValidateHPConfiguration();

                // 预计算并缓存配置值
                RefreshHPConfigCache();
                hpConfigInitialized = true;

                LogDebug($"PlayerHPManager初始化完成 - 初始血量: {cachedInitialHealth}, 答错扣血: {cachedDamageAmount}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PlayerHPManager] 初始化失败: {e.Message}");

                // 失败时使用默认值
                SetDefaultConfiguration();
                hpConfigInitialized = true;
            }
        }

        /// <summary>
        /// 设置HP配置
        /// </summary>
        private void SetupHPConfiguration()
        {
            if (useCustomConfig && customHPConfig != null)
            {
                currentHPConfig = customHPConfig;
                LogDebug($"使用自定义HP配置: {currentHPConfig.ConfigName}");
            }
            else if (HPConfigManager.Config != null)
            {
                currentHPConfig = HPConfigManager.Config;
                LogDebug($"使用全局HP配置: {currentHPConfig.ConfigName}");
            }
            else
            {
                LogDebug("未找到HP配置，将使用默认值");
                currentHPConfig = null;
            }
        }

        /// <summary>
        /// 验证HP配置
        /// </summary>
        private void ValidateHPConfiguration()
        {
            if (currentHPConfig != null && !currentHPConfig.ValidateConfig())
            {
                Debug.LogWarning("[PlayerHPManager] HP配置验证失败，将使用默认值");
                currentHPConfig = null;
            }
        }

        /// <summary>
        /// 设置默认配置
        /// </summary>
        private void SetDefaultConfiguration()
        {
            currentHPConfig = null;
            RefreshHPConfigCache();
            LogDebug("已设置为默认配置");
        }

        #endregion

        #region 配置管理

        /// <summary>
        /// 刷新HP配置缓存
        /// </summary>
        private void RefreshHPConfigCache()
        {
            cachedInitialHealth = CalculateEffectiveInitialHealth();
            cachedDamageAmount = CalculateEffectiveDamageAmount();

            LogDebug($"HP配置缓存已刷新 - 初始血量: {cachedInitialHealth}, 扣血量: {cachedDamageAmount}");
        }

        /// <summary>
        /// 计算有效的初始血量
        /// </summary>
        private int CalculateEffectiveInitialHealth()
        {
            try
            {
                if (currentHPConfig != null)
                {
                    float configHealth = currentHPConfig.GetCurrentHealth();
                    int healthValue = Mathf.RoundToInt(configHealth);
                    LogDebug($"使用HP配置的初始血量: {configHealth} -> {healthValue}");
                    return healthValue;
                }

                // 默认值
                int defaultHealth = 100;
                LogDebug($"使用默认初始血量: {defaultHealth}");
                return defaultHealth;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PlayerHPManager] 计算初始血量失败: {e.Message}，使用默认值");
                return 100;
            }
        }

        /// <summary>
        /// 计算有效的扣血量
        /// </summary>
        private int CalculateEffectiveDamageAmount()
        {
            try
            {
                if (currentHPConfig != null)
                {
                    float configDamage = currentHPConfig.GetDamagePerWrong();
                    int damageValue = Mathf.RoundToInt(configDamage);
                    LogDebug($"使用HP配置的扣血量: {configDamage} -> {damageValue}");
                    return damageValue;
                }

                // 默认值
                int defaultDamage = 20;
                LogDebug($"使用默认扣血量: {defaultDamage}");
                return defaultDamage;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PlayerHPManager] 计算扣血量失败: {e.Message}，使用默认值");
                return 20;
            }
        }

        /// <summary>
        /// 获取有效的初始血量
        /// </summary>
        public int GetEffectiveInitialHealth()
        {
            if (!hpConfigInitialized)
            {
                LogDebug("HP配置未初始化，使用默认初始血量");
                return 100;
            }

            return cachedInitialHealth;
        }

        /// <summary>
        /// 获取有效的扣血量
        /// </summary>
        public int GetEffectiveDamageAmount()
        {
            if (!hpConfigInitialized)
            {
                LogDebug("HP配置未初始化，使用默认扣血量");
                return 20;
            }

            return cachedDamageAmount;
        }

        /// <summary>
        /// 获取最大可答错次数
        /// </summary>
        public int GetMaxWrongAnswers()
        {
            int initialHealth = GetEffectiveInitialHealth();
            int damageAmount = GetEffectiveDamageAmount();

            if (damageAmount <= 0)
                return 0;

            return Mathf.FloorToInt((float)initialHealth / damageAmount);
        }

        #endregion

        #region 玩家HP管理

        /// <summary>
        /// 初始化玩家HP状态
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="customInitialHealth">自定义初始血量（可选，-1表示使用配置值）</param>
        public void InitializePlayer(ushort playerId, int customInitialHealth = -1)
        {
            if (!hpConfigInitialized)
            {
                Debug.LogError("[PlayerHPManager] HP配置未初始化，无法初始化玩家");
                return;
            }

            if (playerHPStates.ContainsKey(playerId))
            {
                LogDebug($"玩家 {playerId} 的HP状态已存在，跳过重复初始化");
                return;
            }

            // 确定初始血量
            int initialHealth = customInitialHealth > 0 ? customInitialHealth : GetEffectiveInitialHealth();

            // 创建玩家HP状态
            var hpState = new PlayerHPState(playerId, initialHealth);
            playerHPStates[playerId] = hpState;

            LogDebug($"玩家 {playerId} HP状态初始化完成 - 血量: {initialHealth}/{initialHealth}");

            // 触发血量变更事件
            TriggerHealthChanged(playerId, initialHealth, initialHealth);
        }

        /// <summary>
        /// 移除玩家HP状态
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        public void RemovePlayer(ushort playerId)
        {
            if (playerHPStates.ContainsKey(playerId))
            {
                playerHPStates.Remove(playerId);
                LogDebug($"玩家 {playerId} 的HP状态已移除");
            }
        }

        /// <summary>
        /// 对玩家造成伤害
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="damageAmount">伤害量（可选，-1表示使用配置值）</param>
        /// <param name="newHealth">输出新的血量</param>
        /// <param name="isDead">输出是否死亡</param>
        /// <returns>是否成功造成伤害</returns>
        public bool ApplyDamage(ushort playerId, out int newHealth, out bool isDead, int damageAmount = -1)
        {
            newHealth = 0;
            isDead = false;

            if (!playerHPStates.ContainsKey(playerId))
            {
                LogDebug($"玩家 {playerId} 的HP状态不存在，无法造成伤害");
                return false;
            }

            var hpState = playerHPStates[playerId];

            if (!hpState.isAlive)
            {
                LogDebug($"玩家 {playerId} 已死亡，无法造成更多伤害");
                newHealth = hpState.currentHealth;
                isDead = true;
                return false;
            }

            // 确定伤害量
            int effectiveDamage = damageAmount > 0 ? damageAmount : GetEffectiveDamageAmount();

            // 计算新血量
            int oldHealth = hpState.currentHealth;
            hpState.currentHealth = Mathf.Max(0, hpState.currentHealth - effectiveDamage);
            hpState.damageCount++;
            hpState.lastDamageTime = Time.time;

            newHealth = hpState.currentHealth;

            // 检查是否死亡
            if (hpState.currentHealth <= 0)
            {
                hpState.isAlive = false;
                isDead = true;
                LogDebug($"玩家 {playerId} 死亡 - 血量: {oldHealth} -> {newHealth} (扣血: {effectiveDamage})");

                // 触发死亡事件
                TriggerPlayerDied(playerId);
            }
            else
            {
                LogDebug($"玩家 {playerId} 受到伤害 - 血量: {oldHealth} -> {newHealth} (扣血: {effectiveDamage})");
            }

            // 触发血量变更事件
            TriggerHealthChanged(playerId, newHealth, hpState.maxHealth);

            return true;
        }

        /// <summary>
        /// 恢复玩家血量
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="healAmount">恢复量</param>
        /// <param name="newHealth">输出新的血量</param>
        /// <returns>是否成功恢复血量</returns>
        public bool HealPlayer(ushort playerId, int healAmount, out int newHealth)
        {
            newHealth = 0;

            if (!playerHPStates.ContainsKey(playerId))
            {
                LogDebug($"玩家 {playerId} 的HP状态不存在，无法恢复血量");
                return false;
            }

            var hpState = playerHPStates[playerId];

            if (!hpState.isAlive)
            {
                LogDebug($"玩家 {playerId} 已死亡，无法恢复血量");
                newHealth = hpState.currentHealth;
                return false;
            }

            // 计算新血量
            int oldHealth = hpState.currentHealth;
            hpState.currentHealth = Mathf.Min(hpState.maxHealth, hpState.currentHealth + healAmount);
            newHealth = hpState.currentHealth;

            if (newHealth != oldHealth)
            {
                LogDebug($"玩家 {playerId} 恢复血量 - 血量: {oldHealth} -> {newHealth} (恢复: {healAmount})");

                // 触发血量变更事件
                TriggerHealthChanged(playerId, newHealth, hpState.maxHealth);
            }

            return true;
        }

        /// <summary>
        /// 复活玩家
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="reviveHealth">复活时的血量（可选，-1表示满血复活）</param>
        /// <returns>是否成功复活</returns>
        public bool RevivePlayer(ushort playerId, int reviveHealth = -1)
        {
            if (!playerHPStates.ContainsKey(playerId))
            {
                LogDebug($"玩家 {playerId} 的HP状态不存在，无法复活");
                return false;
            }

            var hpState = playerHPStates[playerId];

            if (hpState.isAlive)
            {
                LogDebug($"玩家 {playerId} 仍然存活，无需复活");
                return false;
            }

            // 确定复活血量
            int newHealth = reviveHealth > 0 ?
                Mathf.Min(reviveHealth, hpState.maxHealth) :
                hpState.maxHealth;

            hpState.currentHealth = newHealth;
            hpState.isAlive = true;

            LogDebug($"玩家 {playerId} 已复活 - 血量: {newHealth}/{hpState.maxHealth}");

            // 触发血量变更事件
            TriggerHealthChanged(playerId, newHealth, hpState.maxHealth);

            return true;
        }

        #endregion

        #region 查询方法

        /// <summary>
        /// 获取玩家血量信息
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <returns>血量信息元组 (当前血量, 最大血量)</returns>
        public (int currentHealth, int maxHealth) GetPlayerHP(ushort playerId)
        {
            if (playerHPStates.ContainsKey(playerId))
            {
                var hpState = playerHPStates[playerId];
                return (hpState.currentHealth, hpState.maxHealth);
            }

            LogDebug($"玩家 {playerId} 的HP状态不存在");
            return (0, 0);
        }

        /// <summary>
        /// 检查玩家是否存活
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <returns>是否存活</returns>
        public bool IsPlayerAlive(ushort playerId)
        {
            if (playerHPStates.ContainsKey(playerId))
            {
                return playerHPStates[playerId].isAlive;
            }

            return false;
        }

        /// <summary>
        /// 获取玩家血量百分比
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <returns>血量百分比 (0.0 - 1.0)</returns>
        public float GetPlayerHealthPercentage(ushort playerId)
        {
            if (playerHPStates.ContainsKey(playerId))
            {
                return playerHPStates[playerId].GetHealthPercentage();
            }

            return 0f;
        }

        /// <summary>
        /// 检查玩家是否为低血量状态
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <param name="threshold">低血量阈值 (0.0 - 1.0)</param>
        /// <returns>是否为低血量</returns>
        public bool IsPlayerLowHealth(ushort playerId, float threshold = 0.3f)
        {
            if (playerHPStates.ContainsKey(playerId))
            {
                return playerHPStates[playerId].IsLowHealth(threshold);
            }

            return false;
        }

        /// <summary>
        /// 获取存活玩家数量
        /// </summary>
        /// <returns>存活玩家数量</returns>
        public int GetAlivePlayerCount()
        {
            int count = 0;
            foreach (var hpState in playerHPStates.Values)
            {
                if (hpState.isAlive)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 获取所有存活玩家ID
        /// </summary>
        /// <returns>存活玩家ID列表</returns>
        public List<ushort> GetAlivePlayerIds()
        {
            var alivePlayerIds = new List<ushort>();
            foreach (var hpState in playerHPStates.Values)
            {
                if (hpState.isAlive)
                    alivePlayerIds.Add(hpState.playerId);
            }
            return alivePlayerIds;
        }

        /// <summary>
        /// 获取玩家受伤次数
        /// </summary>
        /// <param name="playerId">玩家ID</param>
        /// <returns>受伤次数</returns>
        public int GetPlayerDamageCount(ushort playerId)
        {
            if (playerHPStates.ContainsKey(playerId))
            {
                return playerHPStates[playerId].damageCount;
            }

            return 0;
        }

        #endregion

        #region 事件触发

        /// <summary>
        /// 触发血量变更事件
        /// </summary>
        private void TriggerHealthChanged(ushort playerId, int newHealth, int maxHealth)
        {
            try
            {
                OnHealthChanged?.Invoke(playerId, newHealth, maxHealth);

                // 同时触发血量百分比变更事件
                float percentage = maxHealth > 0 ? (float)newHealth / maxHealth : 0f;
                OnHealthPercentageChanged?.Invoke(playerId, percentage);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PlayerHPManager] 触发血量变更事件失败: {e.Message}");
            }
        }

        /// <summary>
        /// 触发玩家死亡事件
        /// </summary>
        private void TriggerPlayerDied(ushort playerId)
        {
            try
            {
                OnPlayerDied?.Invoke(playerId);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PlayerHPManager] 触发玩家死亡事件失败: {e.Message}");
            }
        }

        #endregion

        #region 状态信息

        /// <summary>
        /// 获取HP配置源信息（用于调试）
        /// </summary>
        public string GetHPConfigSource()
        {
            if (!hpConfigInitialized)
                return "未初始化";

            if (currentHPConfig == null)
                return "默认值";

            if (useCustomConfig && customHPConfig != null)
                return $"自定义配置({currentHPConfig.ConfigName})";

            return $"全局管理器({currentHPConfig.ConfigName})";
        }

        /// <summary>
        /// 获取HP管理器状态信息
        /// </summary>
        /// <returns>状态信息字符串</returns>
        public string GetStatusInfo()
        {
            var status = "=== PlayerHPManager状态 ===\n";
            status += $"已初始化: {hpConfigInitialized}\n";
            status += $"配置源: {GetHPConfigSource()}\n";
            status += $"初始血量: {GetEffectiveInitialHealth()}\n";
            status += $"答错扣血: {GetEffectiveDamageAmount()}\n";
            status += $"最多答错: {GetMaxWrongAnswers()}次\n";
            status += $"管理玩家数: {playerHPStates.Count}\n";
            status += $"存活玩家数: {GetAlivePlayerCount()}\n";

            if (playerHPStates.Count > 0)
            {
                status += "玩家HP状态:\n";
                foreach (var hpState in playerHPStates.Values)
                {
                    status += $"  玩家{hpState.playerId}: {hpState.currentHealth}/{hpState.maxHealth} ";
                    status += $"({hpState.GetHealthPercentage():P1}) ";
                    status += $"{(hpState.isAlive ? "存活" : "死亡")}\n";
                }
            }

            return status;
        }

        /// <summary>
        /// 获取配置摘要信息
        /// </summary>
        /// <returns>配置摘要字符串</returns>
        public string GetConfigSummary()
        {
            if (currentHPConfig != null)
            {
                return currentHPConfig.GetConfigSummary();
            }

            return "使用默认HP配置";
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 清理所有玩家HP状态
        /// </summary>
        public void ClearAllPlayers()
        {
            playerHPStates.Clear();
            LogDebug("所有玩家HP状态已清理");
        }

        /// <summary>
        /// 重新加载HP配置
        /// </summary>
        public void RefreshConfiguration()
        {
            LogDebug("重新加载HP配置");

            SetupHPConfiguration();
            ValidateHPConfiguration();
            RefreshHPConfigCache();

            LogDebug($"HP配置重新加载完成 - 初始血量: {cachedInitialHealth}, 扣血量: {cachedDamageAmount}");
        }

        /// <summary>
        /// 设置调试日志开关
        /// </summary>
        /// <param name="enabled">是否启用调试日志</param>
        public void SetDebugLogs(bool enabled)
        {
            enableDebugLogs = enabled;
            LogDebug($"调试日志已{(enabled ? "启用" : "禁用")}");
        }

        /// <summary>
        /// 调试日志输出
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerHPManager] {message}");
            }
        }

        #endregion

        #region 销毁和清理

        /// <summary>
        /// 销毁HP管理器
        /// </summary>
        public void Dispose()
        {
            // 清理事件
            OnHealthChanged = null;
            OnPlayerDied = null;
            OnHealthPercentageChanged = null;

            // 清理玩家状态
            ClearAllPlayers();

            // 重置配置
            currentHPConfig = null;
            customHPConfig = null;
            hpConfigInitialized = false;

            LogDebug("PlayerHPManager已销毁");
        }

        #endregion
    }
}