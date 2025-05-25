using UnityEngine;
using TMPro;
using Core;

namespace Managers
{
    /// <summary>
    /// ���Ѫ��������
    /// - �������Ѫ��������Ѫ��Ѫ���ľ�ʱ��ʾ"��Ϸʧ��"
    /// - ֧�����û���Ѫ������
    /// - �Զ����Ĵ������¼�
    /// - �ṩѪ���仯�¼�֪ͨ
    /// </summary>
    public class PlayerHealthManager : MonoBehaviour
    {
        [Header("Ѫ������")]
        [SerializeField] private int initialHealth = 3;
        [SerializeField] private int damagePerWrong = 1;
        [SerializeField] private bool allowNegativeHealth = false;

        [Header("UI���")]
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private string healthTextFormat = "Ѫ����{0}";

        [Header("������Ϣ")]
        [SerializeField] private bool enableDebugLog = true;

        // ��ǰѪ��
        private int currentHealth;

        // ��������Ŀ������
        private QuestionManagerBase questionManager;

        // ��Ϸ�Ƿ��ѽ���
        private bool isGameOver = false;

        // Ѫ���仯�¼�
        public System.Action<int> OnHealthChanged;
        public System.Action OnGameOver;

        #region Unity��������

        private void Awake()
        {
            InitializeComponent();
        }

        private void Start()
        {
            // ���û��ͨ���ⲿ���ã���ʹ��Ĭ������
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

        #region ��ʼ��

        /// <summary>
        /// ��ʼ�����
        /// </summary>
        private void InitializeComponent()
        {
            // ȷ����Ϸ�������һ��ʼ����
            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);

            // ������Ϸ״̬
            isGameOver = false;

            LogDebug("PlayerHealthManager ��ʼ�����");
        }

        /// <summary>
        /// Ӧ������
        /// </summary>
        /// <param name="initialHP">��ʼѪ��</param>
        /// <param name="damage">ÿ�δ��۳���Ѫ��</param>
        public void ApplyConfig(int initialHP, int damage)
        {
            if (initialHP <= 0)
            {
                Debug.LogWarning($"[PlayerHealthManager] ��ʼѪ������С�ڵ���0��ʹ��Ĭ��ֵ3������ֵ: {initialHP}");
                initialHP = 3;
            }

            if (damage <= 0)
            {
                Debug.LogWarning($"[PlayerHealthManager] ÿ�ο�Ѫ����С�ڵ���0��ʹ��Ĭ��ֵ1������ֵ: {damage}");
                damage = 1;
            }

            currentHealth = initialHP;
            damagePerWrong = damage;
            isGameOver = false;

            UpdateHealthUI();
            LogDebug($"����Ӧ����� - ��ʼѪ��: {initialHP}, ÿ�ο�Ѫ: {damage}");
        }

        /// <summary>
        /// ����Ŀ�������������¼�
        /// </summary>
        /// <param name="manager">��Ŀ������</param>
        public void BindManager(QuestionManagerBase manager)
        {
            if (manager == null)
            {
                Debug.LogError("[PlayerHealthManager] ���԰󶨿յ���Ŀ������");
                return;
            }

            // ��ȡ��֮ǰ�Ķ���
            UnsubscribeFromEvents();

            questionManager = manager;

            // ���Ĵ������¼�
            if (questionManager.OnAnswerResult != null)
            {
                questionManager.OnAnswerResult += HandleAnswerResult;
                LogDebug($"�ɹ�����Ŀ������: {questionManager.GetType().Name}");
            }
            else
            {
                Debug.LogWarning("[PlayerHealthManager] ��Ŀ�������� OnAnswerResult �¼�Ϊ��");
            }
        }

        /// <summary>
        /// ȡ���¼�����
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (questionManager != null && questionManager.OnAnswerResult != null)
            {
                questionManager.OnAnswerResult -= HandleAnswerResult;
                LogDebug("ȡ����Ŀ�������¼�����");
            }
        }

        #endregion

        #region Ѫ������

        /// <summary>
        /// ���������
        /// </summary>
        /// <param name="isCorrect">�Ƿ���</param>
        public void HandleAnswerResult(bool isCorrect)
        {
            if (isGameOver)
            {
                LogDebug("��Ϸ�ѽ��������Դ�����");
                return;
            }

            if (isCorrect)
            {
                LogDebug("������ȷ��Ѫ������");
                return;
            }

            // ����Ѫ
            TakeDamage(damagePerWrong);
        }

        /// <summary>
        /// HP��������������ּ����Եķ�������
        /// </summary>
        /// <param name="isCorrect">�Ƿ���</param>
        public void HPHandleAnswerResult(bool isCorrect)
        {
            HandleAnswerResult(isCorrect);
        }

        /// <summary>
        /// �۳�Ѫ��
        /// </summary>
        /// <param name="damage">�۳���Ѫ��</param>
        public void TakeDamage(int damage)
        {
            if (isGameOver)
            {
                LogDebug("��Ϸ�ѽ��������Կ�Ѫ����");
                return;
            }

            if (damage <= 0)
            {
                Debug.LogWarning($"[PlayerHealthManager] ��Ѫ��ֵ��Ч: {damage}");
                return;
            }

            int previousHealth = currentHealth;
            currentHealth -= damage;

            // ���������Ѫ����������СֵΪ0
            if (!allowNegativeHealth && currentHealth < 0)
                currentHealth = 0;

            LogDebug($"��Ѫ {damage} �㣬Ѫ���� {previousHealth} ��Ϊ {currentHealth}");

            // ����UI
            UpdateHealthUI();

            // ����Ѫ���仯�¼�
            OnHealthChanged?.Invoke(currentHealth);

            // ����Ƿ���Ϸ����
            if (currentHealth <= 0)
            {
                TriggerGameOver();
            }
        }

        /// <summary>
        /// �ָ�Ѫ���������ڵ��ߵȹ��ܣ�
        /// </summary>
        /// <param name="amount">�ָ���Ѫ��</param>
        public void RestoreHealth(int amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"[PlayerHealthManager] �ָ�Ѫ����ֵ��Ч: {amount}");
                return;
            }

            int previousHealth = currentHealth;
            currentHealth += amount;

            LogDebug($"�ָ�Ѫ�� {amount} �㣬Ѫ���� {previousHealth} ��Ϊ {currentHealth}");

            // ����UI
            UpdateHealthUI();

            // ����Ѫ���仯�¼�
            OnHealthChanged?.Invoke(currentHealth);
        }

        /// <summary>
        /// ����Ѫ����ֱ�����ã��������������
        /// </summary>
        /// <param name="health">�µ�Ѫ��ֵ</param>
        public void SetHealth(int health)
        {
            int previousHealth = currentHealth;
            currentHealth = health;

            if (!allowNegativeHealth && currentHealth < 0)
                currentHealth = 0;

            LogDebug($"ֱ������Ѫ���� {previousHealth} ��Ϊ {currentHealth}");

            // ����UI
            UpdateHealthUI();

            // ����Ѫ���仯�¼�
            OnHealthChanged?.Invoke(currentHealth);

            // ����Ƿ���Ϸ����
            if (currentHealth <= 0)
            {
                TriggerGameOver();
            }
        }

        #endregion

        #region ��Ϸ��������

        /// <summary>
        /// ������Ϸ����
        /// </summary>
        private void TriggerGameOver()
        {
            if (isGameOver)
            {
                LogDebug("��Ϸ�Ѿ������������ظ�����");
                return;
            }

            isGameOver = true;
            LogDebug("Ѫ���ľ�����������Ϸ����");

            // ��ʾ��Ϸ�������
            ShowGameOverPanel();

            // ������Ŀ������
            DisableQuestionManager();

            // ������Ϸ�����¼�
            OnGameOver?.Invoke();
        }

        /// <summary>
        /// ��ʾ��Ϸ�������
        /// </summary>
        private void ShowGameOverPanel()
        {
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
                LogDebug("��ʾ��Ϸ�������");
            }
            else
            {
                Debug.LogWarning("[PlayerHealthManager] ��Ϸ�������δ����");
            }
        }

        /// <summary>
        /// ������Ŀ������
        /// </summary>
        private void DisableQuestionManager()
        {
            if (questionManager != null)
            {
                questionManager.enabled = false;
                LogDebug("������Ŀ������");
            }
        }

        /// <summary>
        /// ���¿�ʼ��Ϸ������Ѫ����״̬��
        /// </summary>
        public void RestartGame()
        {
            isGameOver = false;
            currentHealth = initialHealth;

            // ������Ϸ�������
            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);

            // ����������Ŀ������
            if (questionManager != null)
                questionManager.enabled = true;

            // ����UI
            UpdateHealthUI();

            // ����Ѫ���仯�¼�
            OnHealthChanged?.Invoke(currentHealth);

            LogDebug("��Ϸ���¿�ʼ");
        }

        #endregion

        #region UI����

        /// <summary>
        /// ����Ѫ��UI��ʾ
        /// </summary>
        private void UpdateHealthUI()
        {
            if (healthText != null)
            {
                healthText.text = string.Format(healthTextFormat, currentHealth);
            }
            else
            {
                Debug.LogWarning("[PlayerHealthManager] Ѫ���ı����δ����");
            }
        }

        #endregion

        #region �����ӿ�

        /// <summary>
        /// ��ȡ��ǰѪ��
        /// </summary>
        public int CurrentHealth => currentHealth;

        /// <summary>
        /// ��ȡÿ�ο�Ѫ����
        /// </summary>
        public int DamagePerWrong => damagePerWrong;

        /// <summary>
        /// �����Ϸ�Ƿ����
        /// </summary>
        public bool IsGameOver => isGameOver;

        /// <summary>
        /// ���Ѫ���Ƿ񽡿�������0��
        /// </summary>
        public bool IsHealthy => currentHealth > 0;

        #endregion

        #region ���Թ���

        /// <summary>
        /// ������־���
        /// </summary>
        /// <param name="message">��־��Ϣ</param>
        private void LogDebug(string message)
        {
            if (enableDebugLog)
            {
                Debug.Log($"[PlayerHealthManager] {message}");
            }
        }

        #endregion

        #region Editor���߷��������ڱ༭����ʹ�ã�

#if UNITY_EDITOR
        /// <summary>
        /// �ڱ༭���в��Կ�Ѫ�������ڵ��ԣ�
        /// </summary>
        [ContextMenu("���Կ�Ѫ")]
        private void TestTakeDamage()
        {
            TakeDamage(1);
        }

        /// <summary>
        /// �ڱ༭���в��Իָ�Ѫ���������ڵ��ԣ�
        /// </summary>
        [ContextMenu("���Իָ�Ѫ��")]
        private void TestRestoreHealth()
        {
            RestoreHealth(1);
        }

        /// <summary>
        /// �ڱ༭���в�����Ϸ�����������ڵ��ԣ�
        /// </summary>
        [ContextMenu("������Ϸ����")]
        private void TestGameOver()
        {
            TriggerGameOver();
        }
#endif

        #endregion
    }
}