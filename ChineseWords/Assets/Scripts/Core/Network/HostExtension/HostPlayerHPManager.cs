using UnityEngine;
using System.Collections.Generic;
using Core;

namespace Core.Network
{
    /// <summary>
    /// ���HP������
    /// ר�Ÿ������Ѫ���ĳ�ʼ�����˺����㡢״̬����
    /// </summary>
    public class PlayerHPManager
    {
        [Header("��������")]
        private HPConfig customHPConfig;
        private bool useCustomConfig = false;

        [Header("��������")]
        private bool enableDebugLogs = false;

        // HP���û���
        private HPConfig currentHPConfig;
        private int cachedInitialHealth = -1;
        private int cachedDamageAmount = -1;
        private bool hpConfigInitialized = false;

        // ���HP״̬����
        private Dictionary<ushort, PlayerHPState> playerHPStates;

        // �¼�����
        public System.Action<ushort, int, int> OnHealthChanged; // playerId, newHealth, maxHealth
        public System.Action<ushort> OnPlayerDied;             // playerId
        public System.Action<ushort, float> OnHealthPercentageChanged; // playerId, healthPercentage

        /// <summary>
        /// ���HP״̬���ݽṹ
        /// </summary>
        private class PlayerHPState
        {
            public ushort playerId;
            public int currentHealth;
            public int maxHealth;
            public bool isAlive;
            public int damageCount; // �ܵ��˺��Ĵ���
            public float lastDamageTime; // ���һ������ʱ��

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
        /// ���캯��
        /// </summary>
        public PlayerHPManager()
        {
            playerHPStates = new Dictionary<ushort, PlayerHPState>();
            LogDebug("PlayerHPManager ʵ���Ѵ���");
        }

        #region ��ʼ��

        /// <summary>
        /// ��ʼ��HP������
        /// </summary>
        /// <param name="customConfig">�Զ���HP���ã���ѡ��</param>
        /// <param name="useCustom">�Ƿ�ʹ���Զ�������</param>
        public void Initialize(HPConfig customConfig = null, bool useCustom = false)
        {
            LogDebug("��ʼ��PlayerHPManager...");

            customHPConfig = customConfig;
            useCustomConfig = useCustom;

            try
            {
                // ȷ��HPConfigManager�ѳ�ʼ��
                if (!HPConfigManager.IsConfigured())
                {
                    HPConfigManager.Initialize();
                }

                // ���õ�ǰHP����
                SetupHPConfiguration();

                // ��֤������Ч��
                ValidateHPConfiguration();

                // Ԥ���㲢��������ֵ
                RefreshHPConfigCache();
                hpConfigInitialized = true;

                LogDebug($"PlayerHPManager��ʼ����� - ��ʼѪ��: {cachedInitialHealth}, ����Ѫ: {cachedDamageAmount}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PlayerHPManager] ��ʼ��ʧ��: {e.Message}");

                // ʧ��ʱʹ��Ĭ��ֵ
                SetDefaultConfiguration();
                hpConfigInitialized = true;
            }
        }

        /// <summary>
        /// ����HP����
        /// </summary>
        private void SetupHPConfiguration()
        {
            if (useCustomConfig && customHPConfig != null)
            {
                currentHPConfig = customHPConfig;
                LogDebug($"ʹ���Զ���HP����: {currentHPConfig.ConfigName}");
            }
            else if (HPConfigManager.Config != null)
            {
                currentHPConfig = HPConfigManager.Config;
                LogDebug($"ʹ��ȫ��HP����: {currentHPConfig.ConfigName}");
            }
            else
            {
                LogDebug("δ�ҵ�HP���ã���ʹ��Ĭ��ֵ");
                currentHPConfig = null;
            }
        }

        /// <summary>
        /// ��֤HP����
        /// </summary>
        private void ValidateHPConfiguration()
        {
            if (currentHPConfig != null && !currentHPConfig.ValidateConfig())
            {
                Debug.LogWarning("[PlayerHPManager] HP������֤ʧ�ܣ���ʹ��Ĭ��ֵ");
                currentHPConfig = null;
            }
        }

        /// <summary>
        /// ����Ĭ������
        /// </summary>
        private void SetDefaultConfiguration()
        {
            currentHPConfig = null;
            RefreshHPConfigCache();
            LogDebug("������ΪĬ������");
        }

        #endregion

        #region ���ù���

        /// <summary>
        /// ˢ��HP���û���
        /// </summary>
        private void RefreshHPConfigCache()
        {
            cachedInitialHealth = CalculateEffectiveInitialHealth();
            cachedDamageAmount = CalculateEffectiveDamageAmount();

            LogDebug($"HP���û�����ˢ�� - ��ʼѪ��: {cachedInitialHealth}, ��Ѫ��: {cachedDamageAmount}");
        }

        /// <summary>
        /// ������Ч�ĳ�ʼѪ��
        /// </summary>
        private int CalculateEffectiveInitialHealth()
        {
            try
            {
                if (currentHPConfig != null)
                {
                    float configHealth = currentHPConfig.GetCurrentHealth();
                    int healthValue = Mathf.RoundToInt(configHealth);
                    LogDebug($"ʹ��HP���õĳ�ʼѪ��: {configHealth} -> {healthValue}");
                    return healthValue;
                }

                // Ĭ��ֵ
                int defaultHealth = 100;
                LogDebug($"ʹ��Ĭ�ϳ�ʼѪ��: {defaultHealth}");
                return defaultHealth;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PlayerHPManager] �����ʼѪ��ʧ��: {e.Message}��ʹ��Ĭ��ֵ");
                return 100;
            }
        }

        /// <summary>
        /// ������Ч�Ŀ�Ѫ��
        /// </summary>
        private int CalculateEffectiveDamageAmount()
        {
            try
            {
                if (currentHPConfig != null)
                {
                    float configDamage = currentHPConfig.GetDamagePerWrong();
                    int damageValue = Mathf.RoundToInt(configDamage);
                    LogDebug($"ʹ��HP���õĿ�Ѫ��: {configDamage} -> {damageValue}");
                    return damageValue;
                }

                // Ĭ��ֵ
                int defaultDamage = 20;
                LogDebug($"ʹ��Ĭ�Ͽ�Ѫ��: {defaultDamage}");
                return defaultDamage;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PlayerHPManager] �����Ѫ��ʧ��: {e.Message}��ʹ��Ĭ��ֵ");
                return 20;
            }
        }

        /// <summary>
        /// ��ȡ��Ч�ĳ�ʼѪ��
        /// </summary>
        public int GetEffectiveInitialHealth()
        {
            if (!hpConfigInitialized)
            {
                LogDebug("HP����δ��ʼ����ʹ��Ĭ�ϳ�ʼѪ��");
                return 100;
            }

            return cachedInitialHealth;
        }

        /// <summary>
        /// ��ȡ��Ч�Ŀ�Ѫ��
        /// </summary>
        public int GetEffectiveDamageAmount()
        {
            if (!hpConfigInitialized)
            {
                LogDebug("HP����δ��ʼ����ʹ��Ĭ�Ͽ�Ѫ��");
                return 20;
            }

            return cachedDamageAmount;
        }

        /// <summary>
        /// ��ȡ���ɴ�����
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

        #region ���HP����

        /// <summary>
        /// ��ʼ�����HP״̬
        /// </summary>
        /// <param name="playerId">���ID</param>
        /// <param name="customInitialHealth">�Զ����ʼѪ������ѡ��-1��ʾʹ������ֵ��</param>
        public void InitializePlayer(ushort playerId, int customInitialHealth = -1)
        {
            if (!hpConfigInitialized)
            {
                Debug.LogError("[PlayerHPManager] HP����δ��ʼ�����޷���ʼ�����");
                return;
            }

            if (playerHPStates.ContainsKey(playerId))
            {
                LogDebug($"��� {playerId} ��HP״̬�Ѵ��ڣ������ظ���ʼ��");
                return;
            }

            // ȷ����ʼѪ��
            int initialHealth = customInitialHealth > 0 ? customInitialHealth : GetEffectiveInitialHealth();

            // �������HP״̬
            var hpState = new PlayerHPState(playerId, initialHealth);
            playerHPStates[playerId] = hpState;

            LogDebug($"��� {playerId} HP״̬��ʼ����� - Ѫ��: {initialHealth}/{initialHealth}");

            // ����Ѫ������¼�
            TriggerHealthChanged(playerId, initialHealth, initialHealth);
        }

        /// <summary>
        /// �Ƴ����HP״̬
        /// </summary>
        /// <param name="playerId">���ID</param>
        public void RemovePlayer(ushort playerId)
        {
            if (playerHPStates.ContainsKey(playerId))
            {
                playerHPStates.Remove(playerId);
                LogDebug($"��� {playerId} ��HP״̬���Ƴ�");
            }
        }

        /// <summary>
        /// ���������˺�
        /// </summary>
        public bool ApplyDamage(ushort playerId, out int newHealth, out bool isDead, int damageAmount = -1)
        {
            newHealth = 0;
            isDead = false;

            if (!playerHPStates.ContainsKey(playerId))
            {
                LogDebug($"��� {playerId} ��HP״̬�����ڣ��޷�����˺�");
                return false;
            }

            var hpState = playerHPStates[playerId];

            if (!hpState.isAlive)
            {
                LogDebug($"��� {playerId} ���������޷���ɸ����˺�");
                newHealth = hpState.currentHealth;
                isDead = true;
                return false;
            }

            // ȷ���˺���
            int effectiveDamage = damageAmount > 0 ? damageAmount : GetEffectiveDamageAmount();

            // ������Ѫ��
            int oldHealth = hpState.currentHealth;
            hpState.currentHealth = Mathf.Max(0, hpState.currentHealth - effectiveDamage);
            hpState.damageCount++;
            hpState.lastDamageTime = Time.time;

            newHealth = hpState.currentHealth;

            // ����Ƿ�����
            if (hpState.currentHealth <= 0)
            {
                hpState.isAlive = false;
                isDead = true;
                LogDebug($"��� {playerId} ���� - Ѫ��: {oldHealth} -> {newHealth} (��Ѫ: {effectiveDamage})");

                // ���������¼�
                TriggerPlayerDied(playerId);
            }
            else
            {
                LogDebug($"��� {playerId} �ܵ��˺� - Ѫ��: {oldHealth} -> {newHealth} (��Ѫ: {effectiveDamage})");
            }

            // ����Ѫ������¼�
            TriggerHealthChanged(playerId, newHealth, hpState.maxHealth);

            return true;
        }

        /// <summary>
        /// �ָ����Ѫ��
        /// </summary>
        public bool HealPlayer(ushort playerId, int healAmount, out int newHealth)
        {
            newHealth = 0;

            if (!playerHPStates.ContainsKey(playerId))
            {
                LogDebug($"��� {playerId} ��HP״̬�����ڣ��޷��ָ�Ѫ��");
                return false;
            }

            var hpState = playerHPStates[playerId];

            if (!hpState.isAlive)
            {
                LogDebug($"��� {playerId} ���������޷��ָ�Ѫ��");
                newHealth = hpState.currentHealth;
                return false;
            }

            // ������Ѫ��
            int oldHealth = hpState.currentHealth;
            hpState.currentHealth = Mathf.Min(hpState.maxHealth, hpState.currentHealth + healAmount);
            newHealth = hpState.currentHealth;

            if (newHealth != oldHealth)
            {
                LogDebug($"��� {playerId} �ָ�Ѫ�� - Ѫ��: {oldHealth} -> {newHealth} (�ָ�: {healAmount})");

                // ����Ѫ������¼�
                TriggerHealthChanged(playerId, newHealth, hpState.maxHealth);
            }

            return true;
        }

        #endregion

        #region ��ѯ����
        /// <summary>
        /// ��ȡ���Ѫ����Ϣ
        /// </summary>
        public (int currentHealth, int maxHealth) GetPlayerHP(ushort playerId)
        {
            if (playerHPStates.ContainsKey(playerId))
            {
                var hpState = playerHPStates[playerId];
                return (hpState.currentHealth, hpState.maxHealth);
            }

            LogDebug($"��� {playerId} ��HP״̬������");
            return (0, 0);
        }

        /// <summary>
        /// �������Ƿ���
        /// </summary>
        public bool IsPlayerAlive(ushort playerId)
        {
            if (playerHPStates.ContainsKey(playerId))
            {
                return playerHPStates[playerId].isAlive;
            }

            return false;
        }

        /// <summary>
        /// ��ȡ���Ѫ���ٷֱ�
        /// </summary>
        public float GetPlayerHealthPercentage(ushort playerId)
        {
            if (playerHPStates.ContainsKey(playerId))
            {
                return playerHPStates[playerId].GetHealthPercentage();
            }

            return 0f;
        }

        /// <summary>
        /// �������Ƿ�Ϊ��Ѫ��״̬
        /// </summary>
        public bool IsPlayerLowHealth(ushort playerId, float threshold = 0.3f)
        {
            if (playerHPStates.ContainsKey(playerId))
            {
                return playerHPStates[playerId].IsLowHealth(threshold);
            }

            return false;
        }

        /// <summary>
        /// ��ȡ����������
        /// </summary>
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
        /// ��ȡ���д�����ID
        /// </summary>
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
        /// ��ȡ������˴���
        /// </summary>
        public int GetPlayerDamageCount(ushort playerId)
        {
            if (playerHPStates.ContainsKey(playerId))
            {
                return playerHPStates[playerId].damageCount;
            }

            return 0;
        }

        #endregion

        #region �¼�����

        /// <summary>
        /// ����Ѫ������¼�
        /// </summary>
        private void TriggerHealthChanged(ushort playerId, int newHealth, int maxHealth)
        {
            try
            {
                OnHealthChanged?.Invoke(playerId, newHealth, maxHealth);

                // ͬʱ����Ѫ���ٷֱȱ���¼�
                float percentage = maxHealth > 0 ? (float)newHealth / maxHealth : 0f;
                OnHealthPercentageChanged?.Invoke(playerId, percentage);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PlayerHPManager] ����Ѫ������¼�ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// ������������¼�
        /// </summary>
        private void TriggerPlayerDied(ushort playerId)
        {
            try
            {
                OnPlayerDied?.Invoke(playerId);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PlayerHPManager] ������������¼�ʧ��: {e.Message}");
            }
        }

        #endregion

        #region ״̬��Ϣ
        /// <summary>
        /// ��ȡ����ժҪ��Ϣ
        /// </summary>
        public string GetConfigSummary()
        {
            if (currentHPConfig != null)
            {
                return currentHPConfig.GetConfigSummary();
            }

            return "ʹ��Ĭ��HP����";
        }

        #endregion

        #region ���߷���

        /// <summary>
        /// �����������HP״̬
        /// </summary>
        public void ClearAllPlayers()
        {
            playerHPStates.Clear();
            LogDebug("�������HP״̬������");
        }

        /// <summary>
        /// ���¼���HP����
        /// </summary>
        public void RefreshConfiguration()
        {
            LogDebug("���¼���HP����");

            SetupHPConfiguration();
            ValidateHPConfiguration();
            RefreshHPConfigCache();

            LogDebug($"HP�������¼������ - ��ʼѪ��: {cachedInitialHealth}, ��Ѫ��: {cachedDamageAmount}");
        }

        /// <summary>
        /// ������־���
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerHPManager] {message}");
            }
        }

        #endregion

        #region ���ٺ�����

        /// <summary>
        /// ����HP������
        /// </summary>
        public void Dispose()
        {
            // �����¼�
            OnHealthChanged = null;
            OnPlayerDied = null;
            OnHealthPercentageChanged = null;

            // �������״̬
            ClearAllPlayers();

            // ��������
            currentHPConfig = null;
            customHPConfig = null;
            hpConfigInitialized = false;

            LogDebug("PlayerHPManager������");
        }

        #endregion
    }
}