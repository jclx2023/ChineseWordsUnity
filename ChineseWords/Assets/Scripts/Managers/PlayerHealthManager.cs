using UnityEngine;
using TMPro;
using Core;
using UI;
using System.Collections;

namespace Managers
{
    /// <summary>
    /// 玩家血量显示管理器（兼容性版本）
    /// - 主要负责显示从网络接收的血量数据
    /// - 保留原有接口以维持向后兼容性
    /// - 自动处理网络和本地模式的切换
    /// </summary>
    public class PlayerHealthManager : MonoBehaviour
    {
        [Header("血量配置（兼容性）")]
        [SerializeField] private int initialHealth = 3;
        [SerializeField] private int damagePerWrong = 1;
        [SerializeField] private bool allowNegativeHealth = false;

        [Header("UI组件")]
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private string healthTextFormat = "血量：{0}";
        [SerializeField] private string healthWithMaxFormat = "血量：{0}/{1}";

        [Header("显示设置")]
        [SerializeField] private bool showMaxHealth = false;
        [SerializeField] private bool showHealthPercentage = false;

        [Header("调试信息")]
        [SerializeField] private bool enableDebugLog = true;

        // 当前血量数据
        private int currentHealth;
        private int maxHealth;
        private bool isGameOver = false;

        // 关联的题目管理器（兼容性）
        private QuestionManagerBase questionManager;

        // 运行模式
        private bool isNetworkMode = false;

        // 血量变化事件
        public System.Action<int> OnHealthChanged;
        public System.Action OnGameOver;

        #region Unity生命周期

        private void Awake()
        {
            InitializeComponent();
        }

        private void Start()
        {
            // 检测运行模式
            DetectGameMode();

            // **修改：网络模式下不立即应用本地配置**
            if (!isNetworkMode)
            {
                // 只在本地模式下立即应用配置
                if (currentHealth == 0)
                {
                    ApplyConfig(initialHealth, damagePerWrong);
                }
            }
            else
            {
                // 网络模式下设置等待状态
                SetWaitingForNetworkState();
            }

            // 订阅网络事件（如果是网络模式）
            if (isNetworkMode)
            {
                SubscribeToNetworkEvents();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            UnsubscribeFromNetworkEvents();
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化组件
        /// </summary>
        private void InitializeComponent()
        {
            // 确保游戏结束面板一开始隐藏
            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);

            // 重置游戏状态
            isGameOver = false;

            LogDebug("PlayerHealthManager 初始化完成");
        }

        private void DetectGameMode()
        {

            // 方法2：检查是否存在NetworkManager
            var networkManager = FindObjectOfType<Core.Network.NetworkManager>();
            if (networkManager == null)
            {
                isNetworkMode = false;
                LogDebug("未找到NetworkManager，使用本地模式");
                return;
            }

            // 方法3：检查网络连接状态（可能需要延迟检查）
            isNetworkMode = networkManager.IsConnected || networkManager.IsHost;

            LogDebug($"检测到游戏模式: {(isNetworkMode ? "网络模式" : "本地模式")}");

            // 如果网络状态不确定，稍后重新检查
            if (!isNetworkMode && (networkManager.IsHost || networkManager.IsConnected))
            {
                Invoke(nameof(RecheckGameMode), 1f);
            }
        }

        private void RecheckGameMode()
        {
            var networkManager = FindObjectOfType<Core.Network.NetworkManager>();
            if (networkManager != null && (networkManager.IsConnected || networkManager.IsHost))
            {
                isNetworkMode = true;
                LogDebug("延迟检查：切换到网络模式");
            }
        }

        private IEnumerator RequestInitialHealthStatus()
        {
            // 等待网络连接稳定
            yield return new WaitForSeconds(0.5f);

            var networkManager = FindObjectOfType<Core.Network.NetworkManager>();
            if (networkManager != null && networkManager.IsConnected)
            {
                // 如果NetworkManager有RequestHealthStatus方法，调用它
                try
                {
                    var requestMethod = typeof(Core.Network.NetworkManager).GetMethod("RequestHealthStatus");
                    if (requestMethod != null)
                    {
                        requestMethod.Invoke(networkManager, null);
                        LogDebug("已请求初始血量状态");
                    }
                    else
                    {
                        LogDebug("NetworkManager没有RequestHealthStatus方法，跳过血量请求");
                    }
                }
                catch (System.Exception e)
                {
                    LogDebug($"请求血量状态失败: {e.Message}");
                }
            }
            else
            {
                LogDebug("网络未连接，无法请求血量状态");
            }
        }

        private void SetWaitingForNetworkState()
        {
            LogDebug("网络模式：等待服务器血量数据...");

            // 设置临时显示
            if (healthText != null)
            {
                healthText.text = "血量：等待中...";
                healthText.color = Color.gray;
            }

            // 设置超时检查，防止永远等待
            StartCoroutine(NetworkHealthTimeout());
        }
        private IEnumerator NetworkHealthTimeout()
        {
            yield return new WaitForSeconds(5f); // 5秒超时

            if (isNetworkMode && currentHealth == 0)
            {
                Debug.LogWarning("[PlayerHealthManager] 等待网络血量超时，使用默认配置");
                ApplyConfig(initialHealth, damagePerWrong);
            }
        }
        /// <summary>
        /// 应用配置（兼容性方法）
        /// </summary>
        /// <param name="initialHP">初始血量</param>
        /// <param name="damage">每次答错扣除的血量</param>
        public void ApplyConfig(int initialHP, int damage)
        {
            if (initialHP <= 0)
            {
                Debug.LogWarning($"[PlayerHealthManager] 初始血量不能小于等于0，使用默认值3。传入值: {initialHP}");
                initialHP = 3;
            }

            if (damage <= 0)
            {
                Debug.LogWarning($"[PlayerHealthManager] 每次扣血不能小于等于0，使用默认值1。传入值: {damage}");
                damage = 1;
            }

            currentHealth = initialHP;
            maxHealth = initialHP; // 设置最大血量等于初始血量
            damagePerWrong = damage;
            isGameOver = false;

            UpdateHealthDisplay();
            LogDebug($"配置应用完成 - 初始血量: {initialHP}, 每次扣血: {damage}, 模式: {(isNetworkMode ? "网络" : "本地")}");
        }

        /// <summary>
        /// 绑定题目管理器并订阅事件（兼容性方法）
        /// </summary>
        /// <param name="manager">题目管理器</param>
        public void BindManager(QuestionManagerBase manager)
        {
            if (manager == null)
            {
                Debug.LogError("[PlayerHealthManager] 尝试绑定空的题目管理器");
                return;
            }

            // 先取消之前的订阅
            UnsubscribeFromEvents();

            questionManager = manager;

            // 只在本地模式下订阅答题结果事件
            if (!isNetworkMode)
            {
                if (questionManager.OnAnswerResult != null)
                {
                    questionManager.OnAnswerResult += HandleAnswerResult;
                    LogDebug($"本地模式：成功绑定题目管理器 {questionManager.GetType().Name}");
                }
                else
                {
                    Debug.LogWarning("[PlayerHealthManager] 题目管理器的 OnAnswerResult 事件为空");
                }
            }
            else
            {
                LogDebug($"网络模式：绑定题目管理器 {questionManager.GetType().Name}，但不订阅本地事件");
            }
        }

        /// <summary>
        /// 取消事件订阅
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (questionManager != null && questionManager.OnAnswerResult != null)
            {
                questionManager.OnAnswerResult -= HandleAnswerResult;
                LogDebug("取消题目管理器事件订阅");
            }
        }

        #endregion

        #region 网络事件处理

        /// <summary>
        /// 订阅网络血量更新事件
        /// </summary>
        private void SubscribeToNetworkEvents()
        {
            // 这里可以订阅NetworkManager的血量更新事件
            // 具体实现取决于网络架构
            LogDebug("订阅网络血量更新事件");
        }

        /// <summary>
        /// 取消网络事件订阅
        /// </summary>
        private void UnsubscribeFromNetworkEvents()
        {
            LogDebug("取消网络事件订阅");
        }

        /// <summary>
        /// 处理来自网络的血量更新（新增方法）
        /// </summary>
        /// <param name="newHealth">新血量</param>
        /// <param name="newMaxHealth">最大血量（可选）</param>
        public void OnNetworkHealthUpdate(int newHealth, int newMaxHealth = -1)
        {
            LogDebug($"收到网络血量更新: {newHealth}" + (newMaxHealth > 0 ? $"/{newMaxHealth}" : ""));

            int previousHealth = currentHealth;
            currentHealth = newHealth;

            if (newMaxHealth > 0)
            {
                maxHealth = newMaxHealth;
            }

            // 更新游戏结束状态
            bool wasGameOver = isGameOver;
            isGameOver = currentHealth <= 0;

            // 更新UI
            UpdateHealthDisplay();

            // 触发血量变化事件
            OnHealthChanged?.Invoke(currentHealth);

            // 处理游戏状态变化
            if (!wasGameOver && isGameOver)
            {
                TriggerGameOver();
            }
            else if (wasGameOver && !isGameOver)
            {
                HandleGameRevive();
            }
        }

        /// <summary>
        /// 处理游戏复活（从网络）
        /// </summary>
        private void HandleGameRevive()
        {
            LogDebug("从网络收到复活信息");

            // 隐藏游戏结束面板
            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);

            // 重新启用题目管理器
            if (questionManager != null)
                questionManager.enabled = true;
        }

        #endregion

        #region 血量管理（兼容性方法）

        /// <summary>
        /// 处理答题结果（兼容性方法，仅在本地模式使用）
        /// </summary>
        /// <param name="isCorrect">是否答对</param>
        public void HandleAnswerResult(bool isCorrect)
        {
            if (isNetworkMode)
            {
                LogDebug("网络模式下忽略本地答题结果，等待网络更新");
                return;
            }

            if (isGameOver)
            {
                LogDebug("游戏已结束，忽略答题结果");
                return;
            }

            if (isCorrect)
            {
                LogDebug("答题正确，血量不变");
                return;
            }

            // 答错扣血（仅本地模式）
            TakeDamage(damagePerWrong);
        }

        /// <summary>
        /// HP处理答题结果（保持兼容性的方法名）
        /// </summary>
        /// <param name="isCorrect">是否答对</param>
        public void HPHandleAnswerResult(bool isCorrect)
        {
            HandleAnswerResult(isCorrect);
        }

        /// <summary>
        /// 扣除血量（兼容性方法，主要用于本地模式）
        /// </summary>
        /// <param name="damage">扣除的血量</param>
        public void TakeDamage(int damage)
        {
            if (isNetworkMode)
            {
                LogDebug("网络模式下不处理本地扣血，等待网络更新");
                return;
            }

            if (isGameOver)
            {
                LogDebug("游戏已结束，忽略扣血操作");
                return;
            }

            if (damage <= 0)
            {
                Debug.LogWarning($"[PlayerHealthManager] 扣血数值无效: {damage}");
                return;
            }

            int previousHealth = currentHealth;
            currentHealth -= damage;

            // 如果不允许负血量，限制最小值为0
            if (!allowNegativeHealth && currentHealth < 0)
                currentHealth = 0;

            LogDebug($"本地模式扣血 {damage} 点，血量从 {previousHealth} 变为 {currentHealth}");

            // 更新UI
            UpdateHealthDisplay();

            // 触发血量变化事件
            OnHealthChanged?.Invoke(currentHealth);

            // 检查是否游戏结束
            if (currentHealth <= 0)
            {
                TriggerGameOver();
            }
        }

        /// <summary>
        /// 恢复血量（兼容性方法）
        /// </summary>
        /// <param name="amount">恢复的血量</param>
        public void RestoreHealth(int amount)
        {
            if (isNetworkMode)
            {
                LogDebug("网络模式下不处理本地回血，等待网络更新");
                return;
            }

            if (amount <= 0)
            {
                Debug.LogWarning($"[PlayerHealthManager] 恢复血量数值无效: {amount}");
                return;
            }

            int previousHealth = currentHealth;
            currentHealth += amount;

            LogDebug($"本地模式恢复血量 {amount} 点，血量从 {previousHealth} 变为 {currentHealth}");

            // 更新UI
            UpdateHealthDisplay();

            // 触发血量变化事件
            OnHealthChanged?.Invoke(currentHealth);
        }

        /// <summary>
        /// 设置血量（兼容性方法）
        /// </summary>
        /// <param name="health">新的血量值</param>
        public void SetHealth(int health)
        {
            if (isNetworkMode)
            {
                LogDebug("网络模式下不处理本地血量设置，等待网络更新");
                return;
            }

            int previousHealth = currentHealth;
            currentHealth = health;

            if (!allowNegativeHealth && currentHealth < 0)
                currentHealth = 0;

            LogDebug($"本地模式直接设置血量从 {previousHealth} 变为 {currentHealth}");

            // 更新UI
            UpdateHealthDisplay();

            // 触发血量变化事件
            OnHealthChanged?.Invoke(currentHealth);

            // 检查是否游戏结束
            if (currentHealth <= 0)
            {
                TriggerGameOver();
            }
        }

        #endregion

        #region 游戏结束处理

        /// <summary>
        /// 触发游戏结束
        /// </summary>
        private void TriggerGameOver()
        {
            if (isGameOver)
            {
                LogDebug("游戏已经结束，避免重复触发");
                return;
            }

            isGameOver = true;
            LogDebug($"{(isNetworkMode ? "网络模式" : "本地模式")}：血量耗尽——触发游戏结束");

            // 显示游戏结束面板
            ShowGameOverPanel();

            // 禁用题目管理器
            DisableQuestionManager();

            // 触发游戏结束事件
            OnGameOver?.Invoke();
        }

        /// <summary>
        /// 显示游戏结束面板
        /// </summary>
        private void ShowGameOverPanel()
        {
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
                LogDebug("显示游戏结束面板");
            }
            else
            {
                Debug.LogWarning("[PlayerHealthManager] 游戏结束面板未设置");
            }
        }

        /// <summary>
        /// 禁用题目管理器
        /// </summary>
        private void DisableQuestionManager()
        {
            if (questionManager != null)
            {
                questionManager.enabled = false;
                LogDebug("禁用题目管理器");
            }
        }

        /// <summary>
        /// 重新开始游戏（兼容性方法）
        /// </summary>
        public void RestartGame()
        {
            isGameOver = false;
            currentHealth = initialHealth;

            // 隐藏游戏结束面板
            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);

            // 重新启用题目管理器
            if (questionManager != null)
                questionManager.enabled = true;

            // 更新UI
            UpdateHealthDisplay();

            // 触发血量变化事件
            OnHealthChanged?.Invoke(currentHealth);

            LogDebug($"{(isNetworkMode ? "网络模式" : "本地模式")}：游戏重新开始");
        }

        #endregion

        #region UI更新

        /// <summary>
        /// 更新血量UI显示
        /// </summary>
        private void UpdateHealthDisplay()
        {
            if (healthText == null)
            {
                Debug.LogWarning("[PlayerHealthManager] 血量文本组件未设置");
                return;
            }

            string displayText;

            if (showMaxHealth && maxHealth > 0)
            {
                // 显示 "血量：50/100" 格式
                displayText = string.Format(healthWithMaxFormat, currentHealth, maxHealth);
            }
            else
            {
                // 显示 "血量：50" 格式
                displayText = string.Format(healthTextFormat, currentHealth);
            }

            // 如果显示百分比
            if (showHealthPercentage && maxHealth > 0)
            {
                float percentage = (float)currentHealth / maxHealth * 100f;
                displayText += $" ({percentage:F1}%)";
            }

            healthText.text = displayText;

            // 根据血量状态更新文字颜色
            UpdateHealthTextColor();
        }

        /// <summary>
        /// 根据血量状态更新文字颜色
        /// </summary>
        private void UpdateHealthTextColor()
        {
            if (healthText == null)
                return;

            if (maxHealth <= 0)
            {
                healthText.color = Color.white;
                return;
            }

            float healthPercentage = (float)currentHealth / maxHealth;

            if (healthPercentage <= 0f)
            {
                // 死亡：红色
                healthText.color = Color.red;
            }
            else if (healthPercentage <= 0.25f)
            {
                // 危险：深红色
                healthText.color = new Color(0.8f, 0.2f, 0.2f);
            }
            else if (healthPercentage <= 0.5f)
            {
                // 警告：橙色
                healthText.color = new Color(1f, 0.6f, 0f);
            }
            else
            {
                // 健康：白色
                healthText.color = Color.white;
            }
        }

        #endregion

        #region 公共接口（兼容性）

        /// <summary>
        /// 获取当前血量
        /// </summary>
        public int CurrentHealth => currentHealth;

        /// <summary>
        /// 获取最大血量
        /// </summary>
        public int MaxHealth => maxHealth;

        /// <summary>
        /// 获取每次扣血数量
        /// </summary>
        public int DamagePerWrong => damagePerWrong;

        /// <summary>
        /// 检查游戏是否结束
        /// </summary>
        public bool IsGameOver => isGameOver;

        /// <summary>
        /// 检查血量是否健康（大于0）
        /// </summary>
        public bool IsHealthy => currentHealth > 0;

        /// <summary>
        /// 获取血量百分比
        /// </summary>
        public float HealthPercentage => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;

        /// <summary>
        /// 检查是否为低血量状态
        /// </summary>
        public bool IsLowHealth => HealthPercentage <= 0.3f;

        /// <summary>
        /// 检查当前运行模式
        /// </summary>
        public bool IsNetworkMode => isNetworkMode;

        #endregion

        #region 调试工具

        /// <summary>
        /// 调试日志输出
        /// </summary>
        /// <param name="message">日志信息</param>
        private void LogDebug(string message)
        {
            if (enableDebugLog)
            {
                Debug.Log($"[PlayerHealthManager] {message}");
            }
        }

        #endregion

        #region Editor工具方法

#if UNITY_EDITOR
        /// <summary>
        /// 测试扣血（本地模式）
        /// </summary>
        [ContextMenu("测试扣血")]
        private void TestTakeDamage()
        {
            TakeDamage(1);
        }

        /// <summary>
        /// 测试网络血量更新
        /// </summary>
        [ContextMenu("测试网络血量更新")]
        private void TestNetworkHealthUpdate()
        {
            OnNetworkHealthUpdate(currentHealth - 20, maxHealth);
        }

        /// <summary>
        /// 测试恢复血量
        /// </summary>
        [ContextMenu("测试恢复血量")]
        private void TestRestoreHealth()
        {
            RestoreHealth(1);
        }

        /// <summary>
        /// 测试游戏结束
        /// </summary>
        [ContextMenu("测试游戏结束")]
        private void TestGameOver()
        {
            if (isNetworkMode)
            {
                OnNetworkHealthUpdate(0);
            }
            else
            {
                TriggerGameOver();
            }
        }

        /// <summary>
        /// 切换模式测试
        /// </summary>
        [ContextMenu("切换网络/本地模式")]
        private void ToggleMode()
        {
            isNetworkMode = !isNetworkMode;
            LogDebug($"模式已切换为: {(isNetworkMode ? "网络模式" : "本地模式")}");
        }
#endif

        #endregion
    }
}