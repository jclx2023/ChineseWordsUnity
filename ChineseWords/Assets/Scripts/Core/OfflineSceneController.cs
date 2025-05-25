using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core
{
    /// <summary>
    /// ��������������
    /// ר�Ź�������Ϸ�ĳ�ʼ���Ϳ���
    /// </summary>
    public class OfflineSceneController : MonoBehaviour
    {
        [Header("��Ϸ������")]
        [SerializeField] private QuestionManagerController questionController;
        [SerializeField] private TimerManager timerManager;
        [SerializeField] private PlayerHealthManager healthManager;

        [Header("UI���")]
        [SerializeField] private GameObject gameCanvas;
        [SerializeField] private GameObject offlineUICanvas;

        [Header("��Ϸ����")]
        [SerializeField] private bool autoStartGame = true;
        [SerializeField] private float gameStartDelay = 1f;

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        public static OfflineSceneController Instance { get; private set; }

        // ��Ϸ״̬
        private bool gameStarted = false;
        private bool gamePaused = false;

        // ����
        public bool IsGameStarted => gameStarted;
        public bool IsGamePaused => gamePaused;
        public QuestionManagerController QuestionController => questionController;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            InitializeOfflineGame();
        }

        /// <summary>
        /// ��ʼ��������Ϸ
        /// </summary>
        private void InitializeOfflineGame()
        {
            LogDebug("��ʼ��������Ϸ");

            // ��֤�������
            if (!ValidateComponents())
            {
                Debug.LogError("�����֤ʧ�ܣ��޷�������Ϸ");
                return;
            }

            // ����UI
            ConfigureUI();

            // �Զ���ʼ��Ϸ
            if (autoStartGame)
            {
                Invoke(nameof(StartGame), gameStartDelay);
            }

            LogDebug("������Ϸ��ʼ�����");
        }

        /// <summary>
        /// ��֤��Ҫ���
        /// </summary>
        private bool ValidateComponents()
        {
            bool isValid = true;

            if (questionController == null)
            {
                Debug.LogError("QuestionManagerController δ����");
                isValid = false;
            }

            if (timerManager == null)
            {
                Debug.LogError("TimerManager δ����");
                isValid = false;
            }

            if (healthManager == null)
            {
                Debug.LogError("PlayerHealthManager δ����");
                isValid = false;
            }

            return isValid;
        }

        /// <summary>
        /// ����UI���
        /// </summary>
        private void ConfigureUI()
        {
            // ȷ����ϷUI�ɼ�
            if (gameCanvas != null)
                gameCanvas.SetActive(true);

            if (offlineUICanvas != null)
                offlineUICanvas.SetActive(true);

            LogDebug("UI�������");
        }

        /// <summary>
        /// ��ʼ��Ϸ
        /// </summary>
        public void StartGame()
        {
            if (gameStarted)
            {
                LogDebug("��Ϸ�Ѿ���ʼ");
                return;
            }

            LogDebug("��ʼ������Ϸ");
            gameStarted = true;
            gamePaused = false;

            // ������������Ϸ��ʼ����Ч����Ч

            // QuestionManagerController ���Զ���ʼ���ص�һ��
        }

        /// <summary>
        /// ��ͣ��Ϸ
        /// </summary>
        public void PauseGame()
        {
            if (!gameStarted || gamePaused)
                return;

            LogDebug("��ͣ��Ϸ");
            gamePaused = true;

            // ��ͣ��ʱ��
            if (timerManager != null)
                timerManager.PauseTimer();

            // ����ʱ������
            Time.timeScale = 0f;
        }

        /// <summary>
        /// �ָ���Ϸ
        /// </summary>
        public void ResumeGame()
        {
            if (!gameStarted || !gamePaused)
                return;

            LogDebug("�ָ���Ϸ");
            gamePaused = false;

            // �ָ���ʱ��
            if (timerManager != null)
                timerManager.ResumeTimer();

            // �ָ�ʱ������
            Time.timeScale = 1f;
        }

        /// <summary>
        /// ���¿�ʼ��Ϸ
        /// </summary>
        public void RestartGame()
        {
            LogDebug("���¿�ʼ��Ϸ");

            // �ָ�ʱ������
            Time.timeScale = 1f;

            // ���¼��ص�ǰ����
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>
        /// ������Ϸ
        /// </summary>
        public void EndGame()
        {
            if (!gameStarted)
                return;

            LogDebug("������Ϸ");
            gameStarted = false;
            gamePaused = false;

            // �ָ�ʱ������
            Time.timeScale = 1f;

            // ֹͣ��ʱ��
            if (timerManager != null)
                timerManager.StopTimer();

            // ���������ʾ��Ϸ���������ͳ����Ϣ
        }

        /// <summary>
        /// �������˵�
        /// </summary>
        public void BackToMainMenu()
        {
            LogDebug("�������˵�");

            // �ָ�ʱ������
            Time.timeScale = 1f;

            // �������˵�����
            SceneManager.LoadScene("MainMenuScene");
        }

        /// <summary>
        /// ��ȡ��Ϸͳ����Ϣ
        /// </summary>
        public string GetGameStats()
        {
            var stats = "=== ��Ϸͳ�� ===\n";
            stats += $"��Ϸ״̬: {(gameStarted ? (gamePaused ? "����ͣ" : "������") : "δ��ʼ")}\n";

            if (healthManager != null)
            {
                // ����HealthManager�л�ȡ��ǰѪ���ķ���
                stats += $"��ǰѪ��: {healthManager.CurrentHealth}\n";
            }

            if (timerManager != null)
            {
                // ����TimerManager�л�ȡʣ��ʱ��ķ���
                stats += $"ʣ��ʱ��: {timerManager.RemainingTime:F1}��\n";
            }

            return stats;
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[OfflineSceneController] {message}");
            }
        }

        /// <summary>
        /// Ӧ�ó��򽹵�仯ʱ�Ĵ���
        /// </summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && gameStarted && !gamePaused)
            {
                // ʧȥ����ʱ�Զ���ͣ��Ϸ
                PauseGame();
                LogDebug("Ӧ��ʧȥ���㣬�Զ���ͣ��Ϸ");
            }
        }

        /// <summary>
        /// Ӧ�ó�����ͣʱ�Ĵ���
        /// </summary>
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && gameStarted && !gamePaused)
            {
                // Ӧ����ͣʱ�Զ���ͣ��Ϸ
                PauseGame();
                LogDebug("Ӧ����ͣ���Զ���ͣ��Ϸ");
            }
        }

        private void OnDestroy()
        {
            // ȷ���ָ�ʱ������
            Time.timeScale = 1f;
        }
    }
}