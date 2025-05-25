using UnityEngine;
using TMPro;
using Core;

namespace Managers
{
    /// <summary>
    /// 玩家血量管理器
    /// - 管理玩家血量：答错扣血，血量耗尽时显示"游戏失败"
    /// - 支持配置化的血量设置
    /// - 自动订阅答题结果事件
    /// - 提供血量变化事件通知
    /// </summary>
    public class PlayerHealthManager : MonoBehaviour
    {
        [Header("血量配置")]
        [SerializeField] private int initialHealth = 3;
        [SerializeField] private int damagePerWrong = 1;
        [SerializeField] private bool allowNegativeHealth = false;

        [Header("UI组件")]
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private string healthTextFormat = "血量：{0}";

        [Header("调试信息")]
        [SerializeField] private bool enableDebugLog = true;

        // 当前血量
        private int currentHealth;

        // 关联的题目管理器
        private QuestionManagerBase questionManager;

        // 游戏是否已结束
        private bool isGameOver = false;

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
            // 如果没有通过外部配置，则使用默认配置
            if (currentHealth == 0)
            {
                ApplyConfig(initialHealth, damagePerWrong);
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
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

        /// <summary>
        /// 应用配置
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
            damagePerWrong = damage;
            isGameOver = false;

            UpdateHealthUI();
            LogDebug($"配置应用完成 - 初始血量: {initialHP}, 每次扣血: {damage}");
        }

        /// <summary>
        /// 绑定题目管理器并订阅事件
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

            // 订阅答题结果事件
            if (questionManager.OnAnswerResult != null)
            {
                questionManager.OnAnswerResult += HandleAnswerResult;
                LogDebug($"成功绑定题目管理器: {questionManager.GetType().Name}");
            }
            else
            {
                Debug.LogWarning("[PlayerHealthManager] 题目管理器的 OnAnswerResult 事件为空");
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

        #region 血量管理

        /// <summary>
        /// 处理答题结果
        /// </summary>
        /// <param name="isCorrect">是否答对</param>
        public void HandleAnswerResult(bool isCorrect)
        {
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

            // 答错扣血
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
        /// 扣除血量
        /// </summary>
        /// <param name="damage">扣除的血量</param>
        public void TakeDamage(int damage)
        {
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

            LogDebug($"扣血 {damage} 点，血量从 {previousHealth} 变为 {currentHealth}");

            // 更新UI
            UpdateHealthUI();

            // 触发血量变化事件
            OnHealthChanged?.Invoke(currentHealth);

            // 检查是否游戏结束
            if (currentHealth <= 0)
            {
                TriggerGameOver();
            }
        }

        /// <summary>
        /// 恢复血量（可用于道具等功能）
        /// </summary>
        /// <param name="amount">恢复的血量</param>
        public void RestoreHealth(int amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"[PlayerHealthManager] 恢复血量数值无效: {amount}");
                return;
            }

            int previousHealth = currentHealth;
            currentHealth += amount;

            LogDebug($"恢复血量 {amount} 点，血量从 {previousHealth} 变为 {currentHealth}");

            // 更新UI
            UpdateHealthUI();

            // 触发血量变化事件
            OnHealthChanged?.Invoke(currentHealth);
        }

        /// <summary>
        /// 设置血量（直接设置，用于特殊情况）
        /// </summary>
        /// <param name="health">新的血量值</param>
        public void SetHealth(int health)
        {
            int previousHealth = currentHealth;
            currentHealth = health;

            if (!allowNegativeHealth && currentHealth < 0)
                currentHealth = 0;

            LogDebug($"直接设置血量从 {previousHealth} 变为 {currentHealth}");

            // 更新UI
            UpdateHealthUI();

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
            LogDebug("血量耗尽——触发游戏结束");

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
        /// 重新开始游戏（重置血量和状态）
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
            UpdateHealthUI();

            // 触发血量变化事件
            OnHealthChanged?.Invoke(currentHealth);

            LogDebug("游戏重新开始");
        }

        #endregion

        #region UI更新

        /// <summary>
        /// 更新血量UI显示
        /// </summary>
        private void UpdateHealthUI()
        {
            if (healthText != null)
            {
                healthText.text = string.Format(healthTextFormat, currentHealth);
            }
            else
            {
                Debug.LogWarning("[PlayerHealthManager] 血量文本组件未设置");
            }
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 获取当前血量
        /// </summary>
        public int CurrentHealth => currentHealth;

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

        #region Editor工具方法（仅在编辑器中使用）

#if UNITY_EDITOR
        /// <summary>
        /// 在编辑器中测试扣血（仅用于调试）
        /// </summary>
        [ContextMenu("测试扣血")]
        private void TestTakeDamage()
        {
            TakeDamage(1);
        }

        /// <summary>
        /// 在编辑器中测试恢复血量（仅用于调试）
        /// </summary>
        [ContextMenu("测试恢复血量")]
        private void TestRestoreHealth()
        {
            RestoreHealth(1);
        }

        /// <summary>
        /// 在编辑器中测试游戏结束（仅用于调试）
        /// </summary>
        [ContextMenu("测试游戏结束")]
        private void TestGameOver()
        {
            TriggerGameOver();
        }
#endif

        #endregion
    }
}