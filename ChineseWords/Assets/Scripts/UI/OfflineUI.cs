using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Core;
using Managers;

namespace UI
{
    /// <summary>
    /// ����UI������
    /// ר�Ŵ�������Ϸ��UI��ʾ�ͽ���
    /// </summary>
    public class OfflineUI : MonoBehaviour
    {
        [Header("��Ϸ״̬��ʾ")]
        [SerializeField] private GameObject gameStatusPanel;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text questionCountText;

        [Header("���ư�ť")]
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button backToMenuButton;
        [SerializeField] private Button restartButton;

        [Header("��ͣ�˵�")]
        [SerializeField] private GameObject pauseMenuPanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseMenuRestartButton;
        [SerializeField] private Button pauseMenuExitButton;

        [Header("��ʾ����")]
        [SerializeField] private bool showGameStatus = true;
        [SerializeField] private bool showTimer = true;
        [SerializeField] private bool showScore = true;

        // �������
        private PlayerHealthManager healthManager;
        private TimerManager timerManager;
        private OfflineSceneController sceneController;

        // ״̬׷��
        private int currentScore = 0;
        private int questionsAnswered = 0;

        private void Start()
        {
            InitializeUI();
            RegisterEvents();
            UpdateUI();
        }

        private void OnDestroy()
        {
            UnregisterEvents();
        }

        /// <summary>
        /// ��ʼ��UI���
        /// </summary>
        private void InitializeUI()
        {
            // ��ȡ�������
            healthManager = FindObjectOfType<PlayerHealthManager>();
            timerManager = FindObjectOfType<TimerManager>();
            sceneController = OfflineSceneController.Instance;

            // �󶨰�ť�¼�
            if (pauseButton != null)
                pauseButton.onClick.AddListener(OnPauseClicked);
            if (backToMenuButton != null)
                backToMenuButton.onClick.AddListener(OnBackToMenuClicked);
            if (restartButton != null)
                restartButton.onClick.AddListener(OnRestartClicked);

            // ��ͣ�˵���ť
            if (resumeButton != null)
                resumeButton.onClick.AddListener(OnResumeClicked);
            if (pauseMenuRestartButton != null)
                pauseMenuRestartButton.onClick.AddListener(OnRestartClicked);
            if (pauseMenuExitButton != null)
                pauseMenuExitButton.onClick.AddListener(OnBackToMenuClicked);

            // ���ó�ʼ״̬
            if (gameStatusPanel != null)
                gameStatusPanel.SetActive(showGameStatus);
            if (pauseMenuPanel != null)
                pauseMenuPanel.SetActive(false);

            // ����������ʾ/�������
            ConfigureUIVisibility();
        }

        /// <summary>
        /// ����UI����Ŀɼ���
        /// </summary>
        private void ConfigureUIVisibility()
        {
            if (timerText != null)
                timerText.gameObject.SetActive(showTimer);
            if (scoreText != null)
                scoreText.gameObject.SetActive(showScore);
        }

        /// <summary>
        /// ע���¼�
        /// </summary>
        private void RegisterEvents()
        {
            // ������Ϸ�¼�������У�
            // ���磺�����仯�������仯��
        }

        /// <summary>
        /// ע���¼�
        /// </summary>
        private void UnregisterEvents()
        {
            // ע���¼�����
        }

        private void Update()
        {
            UpdateGameStatus();
        }

        /// <summary>
        /// ������Ϸ״̬��ʾ
        /// </summary>
        private void UpdateGameStatus()
        {
            // ����Ѫ����ʾ
            if (healthText != null && healthManager != null)
            {
                healthText.text = $"Ѫ��: {healthManager.CurrentHealth}";
            }

            // ���¼�ʱ����ʾ
            if (timerText != null && timerManager != null && showTimer)
            {
                float remainingTime = timerManager.RemainingTime;
                timerText.text = $"ʱ��: {remainingTime:F1}s";

                // ʱ�䲻��ʱ���
                if (remainingTime <= 10f)
                {
                    timerText.color = Color.red;
                }
                else if (remainingTime <= 20f)
                {
                    timerText.color = Color.yellow;
                }
                else
                {
                    timerText.color = Color.white;
                }
            }

            // ���·�����ʾ
            if (scoreText != null && showScore)
            {
                scoreText.text = $"����: {currentScore}";
            }

            // ������Ŀ����
            if (questionCountText != null)
            {
                questionCountText.text = $"��Ŀ: {questionsAnswered}";
            }
        }

        /// <summary>
        /// ����UI״̬
        /// </summary>
        public void UpdateUI()
        {
            bool gameStarted = sceneController?.IsGameStarted ?? false;
            bool gamePaused = sceneController?.IsGamePaused ?? false;

            // ���°�ť״̬
            if (pauseButton != null)
            {
                pauseButton.interactable = gameStarted;
                var buttonText = pauseButton.GetComponentInChildren<TMP_Text>();
                if (buttonText != null)
                    buttonText.text = gamePaused ? "�ָ�" : "��ͣ";
            }

            // ��ʾ/������ͣ�˵�
            if (pauseMenuPanel != null)
                pauseMenuPanel.SetActive(gamePaused);
        }

        #region ��ť�¼�����

        /// <summary>
        /// ��ͣ��ť���
        /// </summary>
        private void OnPauseClicked()
        {
            if (sceneController == null)
                return;

            if (sceneController.IsGamePaused)
            {
                sceneController.ResumeGame();
            }
            else
            {
                sceneController.PauseGame();
            }

            UpdateUI();
        }

        /// <summary>
        /// �ָ���Ϸ��ť���
        /// </summary>
        private void OnResumeClicked()
        {
            if (sceneController != null)
            {
                sceneController.ResumeGame();
                UpdateUI();
            }
        }

        /// <summary>
        /// ���¿�ʼ��ť���
        /// </summary>
        private void OnRestartClicked()
        {
            if (sceneController != null)
            {
                sceneController.RestartGame();
            }
        }

        /// <summary>
        /// �������˵���ť���
        /// </summary>
        private void OnBackToMenuClicked()
        {
            if (sceneController != null)
            {
                sceneController.BackToMainMenu();
            }
        }

        #endregion

        #region �����ӿ�

        /// <summary>
        /// ���·���
        /// </summary>
        public void UpdateScore(int newScore)
        {
            currentScore = newScore;
        }

        /// <summary>
        /// ���ӷ���
        /// </summary>
        public void AddScore(int scoreToAdd)
        {
            currentScore += scoreToAdd;
        }

        /// <summary>
        /// ������Ŀ����
        /// </summary>
        public void IncrementQuestionCount()
        {
            questionsAnswered++;
        }

        /// <summary>
        /// ����ͳ������
        /// </summary>
        public void ResetStats()
        {
            currentScore = 0;
            questionsAnswered = 0;
        }

        /// <summary>
        /// ��ʾ��Ϸ״̬���
        /// </summary>
        public void ShowGameStatus()
        {
            showGameStatus = true;
            if (gameStatusPanel != null)
                gameStatusPanel.SetActive(true);
        }

        /// <summary>
        /// ������Ϸ״̬���
        /// </summary>
        public void HideGameStatus()
        {
            showGameStatus = false;
            if (gameStatusPanel != null)
                gameStatusPanel.SetActive(false);
        }

        /// <summary>
        /// �л���Ϸ״̬�����ʾ
        /// </summary>
        public void ToggleGameStatus()
        {
            showGameStatus = !showGameStatus;
            if (gameStatusPanel != null)
                gameStatusPanel.SetActive(showGameStatus);
        }

        /// <summary>
        /// ��ʾ��Ϸ������Ϣ
        /// </summary>
        public void ShowGameOverInfo(string message)
        {
            // ���������ʾ��Ϸ�����Ի������Ϣ
            Debug.Log($"��Ϸ����: {message}");
        }

        #endregion
    }
}