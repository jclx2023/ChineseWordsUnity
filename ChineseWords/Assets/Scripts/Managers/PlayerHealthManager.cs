using UnityEngine;
using TMPro;
using Core;
using UI;
using System.Collections;

namespace Managers
{
    /// <summary>
    /// ���Ѫ����ʾ�������������԰汾��
    /// - ��Ҫ������ʾ��������յ�Ѫ������
    /// - ����ԭ�нӿ���ά����������
    /// - �Զ���������ͱ���ģʽ���л�
    /// </summary>
    public class PlayerHealthManager : MonoBehaviour
    {
        [Header("Ѫ�����ã������ԣ�")]
        [SerializeField] private int initialHealth = 3;
        [SerializeField] private int damagePerWrong = 1;
        [SerializeField] private bool allowNegativeHealth = false;

        [Header("UI���")]
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private string healthTextFormat = "Ѫ����{0}";
        [SerializeField] private string healthWithMaxFormat = "Ѫ����{0}/{1}";

        [Header("��ʾ����")]
        [SerializeField] private bool showMaxHealth = false;
        [SerializeField] private bool showHealthPercentage = false;

        [Header("������Ϣ")]
        [SerializeField] private bool enableDebugLog = true;

        // ��ǰѪ������
        private int currentHealth;
        private int maxHealth;
        private bool isGameOver = false;

        // ��������Ŀ�������������ԣ�
        private QuestionManagerBase questionManager;

        // ����ģʽ
        private bool isNetworkMode = false;

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
            // �������ģʽ
            DetectGameMode();

            // **�޸ģ�����ģʽ�²�����Ӧ�ñ�������**
            if (!isNetworkMode)
            {
                // ֻ�ڱ���ģʽ������Ӧ������
                if (currentHealth == 0)
                {
                    ApplyConfig(initialHealth, damagePerWrong);
                }
            }
            else
            {
                // ����ģʽ�����õȴ�״̬
                SetWaitingForNetworkState();
            }

            // ���������¼������������ģʽ��
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

        private void DetectGameMode()
        {

            // ����2������Ƿ����NetworkManager
            var networkManager = FindObjectOfType<Core.Network.NetworkManager>();
            if (networkManager == null)
            {
                isNetworkMode = false;
                LogDebug("δ�ҵ�NetworkManager��ʹ�ñ���ģʽ");
                return;
            }

            // ����3�������������״̬��������Ҫ�ӳټ�飩
            isNetworkMode = networkManager.IsConnected || networkManager.IsHost;

            LogDebug($"��⵽��Ϸģʽ: {(isNetworkMode ? "����ģʽ" : "����ģʽ")}");

            // �������״̬��ȷ�����Ժ����¼��
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
                LogDebug("�ӳټ�飺�л�������ģʽ");
            }
        }

        private IEnumerator RequestInitialHealthStatus()
        {
            // �ȴ����������ȶ�
            yield return new WaitForSeconds(0.5f);

            var networkManager = FindObjectOfType<Core.Network.NetworkManager>();
            if (networkManager != null && networkManager.IsConnected)
            {
                // ���NetworkManager��RequestHealthStatus������������
                try
                {
                    var requestMethod = typeof(Core.Network.NetworkManager).GetMethod("RequestHealthStatus");
                    if (requestMethod != null)
                    {
                        requestMethod.Invoke(networkManager, null);
                        LogDebug("�������ʼѪ��״̬");
                    }
                    else
                    {
                        LogDebug("NetworkManagerû��RequestHealthStatus����������Ѫ������");
                    }
                }
                catch (System.Exception e)
                {
                    LogDebug($"����Ѫ��״̬ʧ��: {e.Message}");
                }
            }
            else
            {
                LogDebug("����δ���ӣ��޷�����Ѫ��״̬");
            }
        }

        private void SetWaitingForNetworkState()
        {
            LogDebug("����ģʽ���ȴ�������Ѫ������...");

            // ������ʱ��ʾ
            if (healthText != null)
            {
                healthText.text = "Ѫ�����ȴ���...";
                healthText.color = Color.gray;
            }

            // ���ó�ʱ��飬��ֹ��Զ�ȴ�
            StartCoroutine(NetworkHealthTimeout());
        }
        private IEnumerator NetworkHealthTimeout()
        {
            yield return new WaitForSeconds(5f); // 5�볬ʱ

            if (isNetworkMode && currentHealth == 0)
            {
                Debug.LogWarning("[PlayerHealthManager] �ȴ�����Ѫ����ʱ��ʹ��Ĭ������");
                ApplyConfig(initialHealth, damagePerWrong);
            }
        }
        /// <summary>
        /// Ӧ�����ã������Է�����
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
            maxHealth = initialHP; // �������Ѫ�����ڳ�ʼѪ��
            damagePerWrong = damage;
            isGameOver = false;

            UpdateHealthDisplay();
            LogDebug($"����Ӧ����� - ��ʼѪ��: {initialHP}, ÿ�ο�Ѫ: {damage}, ģʽ: {(isNetworkMode ? "����" : "����")}");
        }

        /// <summary>
        /// ����Ŀ�������������¼��������Է�����
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

            // ֻ�ڱ���ģʽ�¶��Ĵ������¼�
            if (!isNetworkMode)
            {
                if (questionManager.OnAnswerResult != null)
                {
                    questionManager.OnAnswerResult += HandleAnswerResult;
                    LogDebug($"����ģʽ���ɹ�����Ŀ������ {questionManager.GetType().Name}");
                }
                else
                {
                    Debug.LogWarning("[PlayerHealthManager] ��Ŀ�������� OnAnswerResult �¼�Ϊ��");
                }
            }
            else
            {
                LogDebug($"����ģʽ������Ŀ������ {questionManager.GetType().Name}���������ı����¼�");
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

        #region �����¼�����

        /// <summary>
        /// ��������Ѫ�������¼�
        /// </summary>
        private void SubscribeToNetworkEvents()
        {
            // ������Զ���NetworkManager��Ѫ�������¼�
            // ����ʵ��ȡ��������ܹ�
            LogDebug("��������Ѫ�������¼�");
        }

        /// <summary>
        /// ȡ�������¼�����
        /// </summary>
        private void UnsubscribeFromNetworkEvents()
        {
            LogDebug("ȡ�������¼�����");
        }

        /// <summary>
        /// �������������Ѫ�����£�����������
        /// </summary>
        /// <param name="newHealth">��Ѫ��</param>
        /// <param name="newMaxHealth">���Ѫ������ѡ��</param>
        public void OnNetworkHealthUpdate(int newHealth, int newMaxHealth = -1)
        {
            LogDebug($"�յ�����Ѫ������: {newHealth}" + (newMaxHealth > 0 ? $"/{newMaxHealth}" : ""));

            int previousHealth = currentHealth;
            currentHealth = newHealth;

            if (newMaxHealth > 0)
            {
                maxHealth = newMaxHealth;
            }

            // ������Ϸ����״̬
            bool wasGameOver = isGameOver;
            isGameOver = currentHealth <= 0;

            // ����UI
            UpdateHealthDisplay();

            // ����Ѫ���仯�¼�
            OnHealthChanged?.Invoke(currentHealth);

            // ������Ϸ״̬�仯
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
        /// ������Ϸ��������磩
        /// </summary>
        private void HandleGameRevive()
        {
            LogDebug("�������յ�������Ϣ");

            // ������Ϸ�������
            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);

            // ����������Ŀ������
            if (questionManager != null)
                questionManager.enabled = true;
        }

        #endregion

        #region Ѫ�����������Է�����

        /// <summary>
        /// ����������������Է��������ڱ���ģʽʹ�ã�
        /// </summary>
        /// <param name="isCorrect">�Ƿ���</param>
        public void HandleAnswerResult(bool isCorrect)
        {
            if (isNetworkMode)
            {
                LogDebug("����ģʽ�º��Ա��ش��������ȴ��������");
                return;
            }

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

            // ����Ѫ��������ģʽ��
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
        /// �۳�Ѫ���������Է�������Ҫ���ڱ���ģʽ��
        /// </summary>
        /// <param name="damage">�۳���Ѫ��</param>
        public void TakeDamage(int damage)
        {
            if (isNetworkMode)
            {
                LogDebug("����ģʽ�²������ؿ�Ѫ���ȴ��������");
                return;
            }

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

            LogDebug($"����ģʽ��Ѫ {damage} �㣬Ѫ���� {previousHealth} ��Ϊ {currentHealth}");

            // ����UI
            UpdateHealthDisplay();

            // ����Ѫ���仯�¼�
            OnHealthChanged?.Invoke(currentHealth);

            // ����Ƿ���Ϸ����
            if (currentHealth <= 0)
            {
                TriggerGameOver();
            }
        }

        /// <summary>
        /// �ָ�Ѫ���������Է�����
        /// </summary>
        /// <param name="amount">�ָ���Ѫ��</param>
        public void RestoreHealth(int amount)
        {
            if (isNetworkMode)
            {
                LogDebug("����ģʽ�²������ػ�Ѫ���ȴ��������");
                return;
            }

            if (amount <= 0)
            {
                Debug.LogWarning($"[PlayerHealthManager] �ָ�Ѫ����ֵ��Ч: {amount}");
                return;
            }

            int previousHealth = currentHealth;
            currentHealth += amount;

            LogDebug($"����ģʽ�ָ�Ѫ�� {amount} �㣬Ѫ���� {previousHealth} ��Ϊ {currentHealth}");

            // ����UI
            UpdateHealthDisplay();

            // ����Ѫ���仯�¼�
            OnHealthChanged?.Invoke(currentHealth);
        }

        /// <summary>
        /// ����Ѫ���������Է�����
        /// </summary>
        /// <param name="health">�µ�Ѫ��ֵ</param>
        public void SetHealth(int health)
        {
            if (isNetworkMode)
            {
                LogDebug("����ģʽ�²�������Ѫ�����ã��ȴ��������");
                return;
            }

            int previousHealth = currentHealth;
            currentHealth = health;

            if (!allowNegativeHealth && currentHealth < 0)
                currentHealth = 0;

            LogDebug($"����ģʽֱ������Ѫ���� {previousHealth} ��Ϊ {currentHealth}");

            // ����UI
            UpdateHealthDisplay();

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
            LogDebug($"{(isNetworkMode ? "����ģʽ" : "����ģʽ")}��Ѫ���ľ�����������Ϸ����");

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
        /// ���¿�ʼ��Ϸ�������Է�����
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
            UpdateHealthDisplay();

            // ����Ѫ���仯�¼�
            OnHealthChanged?.Invoke(currentHealth);

            LogDebug($"{(isNetworkMode ? "����ģʽ" : "����ģʽ")}����Ϸ���¿�ʼ");
        }

        #endregion

        #region UI����

        /// <summary>
        /// ����Ѫ��UI��ʾ
        /// </summary>
        private void UpdateHealthDisplay()
        {
            if (healthText == null)
            {
                Debug.LogWarning("[PlayerHealthManager] Ѫ���ı����δ����");
                return;
            }

            string displayText;

            if (showMaxHealth && maxHealth > 0)
            {
                // ��ʾ "Ѫ����50/100" ��ʽ
                displayText = string.Format(healthWithMaxFormat, currentHealth, maxHealth);
            }
            else
            {
                // ��ʾ "Ѫ����50" ��ʽ
                displayText = string.Format(healthTextFormat, currentHealth);
            }

            // �����ʾ�ٷֱ�
            if (showHealthPercentage && maxHealth > 0)
            {
                float percentage = (float)currentHealth / maxHealth * 100f;
                displayText += $" ({percentage:F1}%)";
            }

            healthText.text = displayText;

            // ����Ѫ��״̬����������ɫ
            UpdateHealthTextColor();
        }

        /// <summary>
        /// ����Ѫ��״̬����������ɫ
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
                // ��������ɫ
                healthText.color = Color.red;
            }
            else if (healthPercentage <= 0.25f)
            {
                // Σ�գ����ɫ
                healthText.color = new Color(0.8f, 0.2f, 0.2f);
            }
            else if (healthPercentage <= 0.5f)
            {
                // ���棺��ɫ
                healthText.color = new Color(1f, 0.6f, 0f);
            }
            else
            {
                // ��������ɫ
                healthText.color = Color.white;
            }
        }

        #endregion

        #region �����ӿڣ������ԣ�

        /// <summary>
        /// ��ȡ��ǰѪ��
        /// </summary>
        public int CurrentHealth => currentHealth;

        /// <summary>
        /// ��ȡ���Ѫ��
        /// </summary>
        public int MaxHealth => maxHealth;

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

        /// <summary>
        /// ��ȡѪ���ٷֱ�
        /// </summary>
        public float HealthPercentage => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;

        /// <summary>
        /// ����Ƿ�Ϊ��Ѫ��״̬
        /// </summary>
        public bool IsLowHealth => HealthPercentage <= 0.3f;

        /// <summary>
        /// ��鵱ǰ����ģʽ
        /// </summary>
        public bool IsNetworkMode => isNetworkMode;

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

        #region Editor���߷���

#if UNITY_EDITOR
        /// <summary>
        /// ���Կ�Ѫ������ģʽ��
        /// </summary>
        [ContextMenu("���Կ�Ѫ")]
        private void TestTakeDamage()
        {
            TakeDamage(1);
        }

        /// <summary>
        /// ��������Ѫ������
        /// </summary>
        [ContextMenu("��������Ѫ������")]
        private void TestNetworkHealthUpdate()
        {
            OnNetworkHealthUpdate(currentHealth - 20, maxHealth);
        }

        /// <summary>
        /// ���Իָ�Ѫ��
        /// </summary>
        [ContextMenu("���Իָ�Ѫ��")]
        private void TestRestoreHealth()
        {
            RestoreHealth(1);
        }

        /// <summary>
        /// ������Ϸ����
        /// </summary>
        [ContextMenu("������Ϸ����")]
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
        /// �л�ģʽ����
        /// </summary>
        [ContextMenu("�л�����/����ģʽ")]
        private void ToggleMode()
        {
            isNetworkMode = !isNetworkMode;
            LogDebug($"ģʽ���л�Ϊ: {(isNetworkMode ? "����ģʽ" : "����ģʽ")}");
        }
#endif

        #endregion
    }
}