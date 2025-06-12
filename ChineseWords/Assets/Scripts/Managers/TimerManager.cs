using UnityEngine;
using TMPro;
using Core;
using System;
using System.Collections;
using Core.Network;

namespace Managers
{
    /// <summary>
    /// ����ʱ������
    /// - ������⵹��ʱ�߼�����ͨ���ⲿ��������ʱ��
    /// - ֧����ͣ���ָ������õȹ���
    /// - �ṩ�ḻ��ʱ���¼�֪ͨ
    /// - ֧�ֶ�����ʾ��ʽ����ʽ
    /// </summary>
    public class TimerManager : MonoBehaviour
    {
        [Header("����ʱ����")]
        [SerializeField] private float defaultTimeLimit = 20f;
        [SerializeField] private bool autoStartOnEnable = false;
        [SerializeField] private bool loopTimer = false;

        [Header("UI���")]
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private string timerTextFormat = "����ʱ: {0}";
        [SerializeField] private string timeUpText = "ʱ�䵽��";

        [Header("��ɫ����")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color warningColor = Color.yellow;
        [SerializeField] private Color criticalColor = Color.red;
        [SerializeField] private float warningThreshold = 5f;
        [SerializeField] private float criticalThreshold = 3f;

        [Header("������Ϣ")]
        [SerializeField] private bool enableDebugLog = true;

        // ��ǰ���õ�ʱ������
        private float timeLimit;

        // ��ǰʣ��ʱ��
        private float currentTime;

        // ����ʱЭ��
        private Coroutine countdownCoroutine;
        private bool isRunning = false;
        private bool isPaused = false;


        #region �¼�����

        public event Action OnTimeUp;
        public event Action<float> OnTimeChanged;
        public event Action OnWarningTime;
        public event Action OnCriticalTime;
        public event Action OnTimerStarted;
        public event Action OnTimerStopped;
        public event Action OnTimerPaused;
        public event Action OnTimerResumed;

        #endregion

        #region Unity��������

        private void Awake()
        {
            InitializeComponent();
        }

        private void Start()
        {
            // ���û��ͨ���ⲿ���ã���ʹ��Ĭ������
            if (timeLimit == 0)
            {
                ApplyConfig(defaultTimeLimit);
            }

            if (autoStartOnEnable)
            {
                StartTimer();
            }
        }

        private void OnEnable()
        {
            if (autoStartOnEnable && timeLimit > 0)
            {
                StartTimer();
            }
        }

        private void OnDisable()
        {
            StopTimer();
        }

        private void OnDestroy()
        {
            StopTimer();
            UnsubscribeFromEvents();
        }

        #endregion

        #region ��ʼ��

        /// <summary>
        /// ��ʼ�����
        /// </summary>
        private void InitializeComponent()
        {
            // ����Ĭ��ʱ������
            timeLimit = defaultTimeLimit;
            currentTime = timeLimit;

            // ��ʼ��UI
            UpdateTimerUI();

            LogDebug("TimerManager ��ʼ�����");
        }

        /// <summary>
        /// Ӧ������
        /// </summary>
        /// <param name="newTimeLimit">�µ�ʱ������</param>
        public void ApplyConfig(float newTimeLimit)
        {
            if (newTimeLimit <= 0)
            {
                Debug.LogWarning($"[TimerManager] ʱ�����Ʋ���С�ڵ���0��ʹ��Ĭ��ֵ{defaultTimeLimit}������ֵ: {newTimeLimit}");
                newTimeLimit = defaultTimeLimit;
            }

            timeLimit = newTimeLimit;
            currentTime = timeLimit;

            LogDebug($"����Ӧ����� - ʱ������: {timeLimit}��");

            // ����UI��ʾ
            UpdateTimerUI();
        }


        /// <summary>
        /// ȡ���¼�����
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            // �����������������¼����ģ�������ȡ��
            LogDebug("ȡ���¼�����");
        }

        #endregion

        #region ��ʱ������

        /// <summary>
        /// ��ʼ�����õ���ʱ
        /// </summary>
        public void StartTimer()
        {
            StopTimer(); // ��ֹ֮ͣǰ�ļ�ʱ��

            currentTime = timeLimit;
            isRunning = true;
            isPaused = false;

            countdownCoroutine = StartCoroutine(Countdown());

            LogDebug($"��ʼ����ʱ - ʱ��: {timeLimit}��");

            // ���������¼�
            OnTimerStarted?.Invoke();
        }

        /// <summary>
        /// ��ʼ����ʱ��ָ��ʱ�䣩
        /// </summary>
        public void StartTimer(float customTime)
        {
            if (customTime <= 0)
            {
                Debug.LogWarning($"[TimerManager] �Զ���ʱ����Ч: {customTime}��ʹ�õ�ǰ����ʱ��{timeLimit}");
                StartTimer();
                return;
            }

            StopTimer(); // ��ֹ֮ͣǰ�ļ�ʱ��

            timeLimit = customTime;
            currentTime = customTime;
            isRunning = true;
            isPaused = false;

            countdownCoroutine = StartCoroutine(Countdown());

            LogDebug($"��ʼ�Զ��嵹��ʱ - ʱ��: {customTime}��");

            // ���������¼�
            OnTimerStarted?.Invoke();
        }

        /// <summary>
        /// ֹͣ����ʱ
        /// </summary>
        public void StopTimer()
        {
            if (countdownCoroutine != null)
            {
                StopCoroutine(countdownCoroutine);
                countdownCoroutine = null;
            }

            bool wasRunning = isRunning;
            isRunning = false;
            isPaused = false;

            if (wasRunning)
            {
                LogDebug("����ʱ��ֹͣ");
                OnTimerStopped?.Invoke();
            }
        }

        /// <summary>
        /// ��ͣ����ʱ
        /// </summary>
        public void PauseTimer()
        {
            if (!isRunning || isPaused)
            {
                LogDebug("��ʱ��δ���л�����ͣ���޷���ͣ");
                return;
            }

            isPaused = true;
            LogDebug("����ʱ����ͣ");

            OnTimerPaused?.Invoke();
        }

        /// <summary>
        /// �ָ�����ʱ
        /// </summary>
        public void ResumeTimer()
        {
            if (!isRunning || !isPaused)
            {
                LogDebug("��ʱ��δ���л�δ��ͣ���޷��ָ�");
                return;
            }

            isPaused = false;
            LogDebug("����ʱ�ѻָ�");

            OnTimerResumed?.Invoke();
        }

        /// <summary>
        /// ���õ���ʱ���ص���ʼʱ�䵫����ʼ��
        /// </summary>
        public void ResetTimer()
        {
            StopTimer();
            currentTime = timeLimit;
            UpdateTimerUI();

            LogDebug("����ʱ������");
        }

        /// <summary>
        /// ���ʱ��
        /// </summary>
        /// <param name="seconds">Ҫ��ӵ�����</param>
        public void AddTime(float seconds)
        {
            if (seconds <= 0)
            {
                Debug.LogWarning($"[TimerManager] ���ʱ����ֵ��Ч: {seconds}");
                return;
            }

            currentTime += seconds;
            LogDebug($"���ʱ�� {seconds}�룬��ǰʣ��ʱ��: {currentTime}��");

            UpdateTimerUI();
            OnTimeChanged?.Invoke(currentTime);
        }

        /// <summary>
        /// ����ʱ��
        /// </summary>
        public void ReduceTime(float seconds)
        {
            if (seconds <= 0)
            {
                Debug.LogWarning($"[TimerManager] ����ʱ����ֵ��Ч: {seconds}");
                return;
            }

            currentTime -= seconds;
            if (currentTime < 0)
                currentTime = 0;

            LogDebug($"����ʱ�� {seconds}�룬��ǰʣ��ʱ��: {currentTime}��");

            UpdateTimerUI();
            OnTimeChanged?.Invoke(currentTime);

            // ���ʱ�����꣬����ʱ�䵽�¼�
            if (currentTime <= 0 && isRunning)
            {
                HandleTimeUp();
            }
        }

        #endregion

        #region ����ʱ�߼�

        /// <summary>
        /// ����ʱЭ��
        /// </summary>
        private IEnumerator Countdown()
        {
            bool hasTriggeredWarning = false;
            bool hasTriggeredCritical = false;

            while (currentTime > 0f && isRunning)
            {
                // �����ͣ���ȴ��ָ�
                while (isPaused)
                {
                    yield return null;
                }

                // ����UI
                UpdateTimerUI();

                // ��龯���Σ����ֵ
                if (!hasTriggeredWarning && currentTime <= warningThreshold && currentTime > criticalThreshold)
                {
                    hasTriggeredWarning = true;
                    OnWarningTime?.Invoke();
                    LogDebug("���뾯��ʱ��");
                }

                if (!hasTriggeredCritical && currentTime <= criticalThreshold)
                {
                    hasTriggeredCritical = true;
                    OnCriticalTime?.Invoke();
                    LogDebug("����Σ��ʱ��");
                }

                // ����ʱ��仯�¼�
                OnTimeChanged?.Invoke(currentTime);

                // �ȴ�1��
                yield return new WaitForSeconds(1f);

                // ����1��
                currentTime -= 1f;
            }

            // ʱ������
            if (isRunning)
            {
                currentTime = 0f;
                HandleTimeUp();
            }

            countdownCoroutine = null;
        }

        /// <summary>
        /// ����ʱ�䵽
        /// </summary>
        private void HandleTimeUp()
        {
            LogDebug("ʱ�䵽��");
            UpdateTimerUI(true);

            OnTimeUp?.Invoke();
        }

        private void HandleTimeoutAnswerSubmission()
        {
            LogDebug("��ʱ�Զ��ύ��");
            SubmitTimeoutAnswer();
        }

        private void SubmitTimeoutAnswer()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.SubmitAnswer("");
            }
        }

        #endregion

        #region UI����

        private void UpdateTimerUI(bool isTimeUp = false)
        {
            if (timerText == null)
            {
                Debug.LogWarning("[TimerManager] ��ʱ���ı����δ����");
                return;
            }

            if (isTimeUp)
            {
                timerText.text = timeUpText;
                timerText.color = criticalColor;
            }
            else
            {
                // ��ʽ����ʾʱ��
                string timeDisplay = FormatTime(currentTime);
                timerText.text = string.Format(timerTextFormat, timeDisplay);

                UpdateTimerColor();
            }
        }

        /// <summary>
        /// ��ʽ��ʱ����ʾ
        /// </summary>
        private string FormatTime(float time)
        {
            int seconds = Mathf.CeilToInt(time);

            if (seconds >= 60)
            {
                int minutes = seconds / 60;
                int remainingSeconds = seconds % 60;
                return $"{minutes:00}:{remainingSeconds:00}";
            }
            else
            {
                return seconds.ToString();
            }
        }

        /// <summary>
        /// ����ʣ��ʱ�������ɫ
        /// </summary>
        private void UpdateTimerColor()
        {
            if (timerText == null) return;

            if (currentTime <= criticalThreshold)
            {
                timerText.color = criticalColor;
            }
            else if (currentTime <= warningThreshold)
            {
                timerText.color = warningColor;
            }
            else
            {
                timerText.color = normalColor;
            }
        }

        #endregion

        #region �����ӿ�

        public float CurrentTime => currentTime;
        public float RemainingTime => currentTime;
        public float TimeLimit => timeLimit;
        public bool IsRunning => isRunning;
        public bool IsPaused => isPaused;
        public float Progress => timeLimit > 0 ? (timeLimit - currentTime) / timeLimit : 0f;
        public float RemainingPercentage => timeLimit > 0 ? currentTime / timeLimit : 0f;

        #endregion

        #region ���Թ���

        private void LogDebug(string message)
        {
            if (enableDebugLog)
            {
                Debug.Log($"[TimerManager] {message}");
            }
        }

        #endregion
    }
}