using UnityEngine;
using TMPro;
using Core;
using System;
using System.Collections;

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
        [SerializeField] private float defaultTimeLimit = 10f;
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

        // ��ʱ��״̬
        private bool isRunning = false;
        private bool isPaused = false;

        // ��������Ŀ����������ѡ��
        private QuestionManagerBase questionManager;

        #region �¼�����

        /// <summary>
        /// ʱ�䵽�¼�
        /// </summary>
        public event Action OnTimeUp;

        /// <summary>
        /// ʱ��仯�¼���ÿ�봥����
        /// </summary>
        public event Action<float> OnTimeChanged;

        /// <summary>
        /// ���뾯��ʱ���¼�
        /// </summary>
        public event Action OnWarningTime;

        /// <summary>
        /// ����Σ��ʱ���¼�
        /// </summary>
        public event Action OnCriticalTime;

        /// <summary>
        /// ��ʱ�������¼�
        /// </summary>
        public event Action OnTimerStarted;

        /// <summary>
        /// ��ʱ��ֹͣ�¼�
        /// </summary>
        public event Action OnTimerStopped;

        /// <summary>
        /// ��ʱ����ͣ�¼�
        /// </summary>
        public event Action OnTimerPaused;

        /// <summary>
        /// ��ʱ���ָ��¼�
        /// </summary>
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
        /// ����Ŀ����������ѡ�������Զ������̣�
        /// </summary>
        /// <param name="manager">��Ŀ������</param>
        public void BindManager(QuestionManagerBase manager)
        {
            if (manager == null)
            {
                Debug.LogWarning("[TimerManager] ���԰󶨿յ���Ŀ������");
                return;
            }

            // ��ȡ��֮ǰ�Ķ���
            UnsubscribeFromEvents();

            questionManager = manager;

            // �����������������Ŀ�������������߼�
            LogDebug($"�ɹ�����Ŀ������: {questionManager.GetType().Name}");
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
        /// <param name="customTime">�Զ��嵹��ʱʱ��</param>
        public void StartTimer(float customTime)
        {
            if (customTime <= 0)
            {
                Debug.LogWarning($"[TimerManager] �Զ���ʱ����Ч: {customTime}��ʹ��Ĭ��ʱ��");
                StartTimer();
                return;
            }

            float originalTimeLimit = timeLimit;
            timeLimit = customTime;
            StartTimer();
            timeLimit = originalTimeLimit; // �ָ�ԭʼ����

            LogDebug($"��ʼ�Զ��嵹��ʱ - ʱ��: {customTime}��");
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
        /// <param name="seconds">Ҫ���ٵ�����</param>
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

            // ����UI��ʾ
            UpdateTimerUI(true);

            // ����ʱ�䵽�¼�
            OnTimeUp?.Invoke();

            if (loopTimer)
            {
                // �����ѭ����ʱ�������¿�ʼ
                LogDebug("ѭ����ʱ�������¿�ʼ");
                StartTimer();
            }
            else
            {
                // ֹͣ��ʱ��
                isRunning = false;
                OnTimerStopped?.Invoke();
            }
        }

        #endregion

        #region UI����

        /// <summary>
        /// ���µ���ʱUI��ʾ
        /// </summary>
        /// <param name="isTimeUp">�Ƿ�ʱ�䵽</param>
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

                // ����ʣ��ʱ��������ɫ
                UpdateTimerColor();
            }
        }

        /// <summary>
        /// ��ʽ��ʱ����ʾ
        /// </summary>
        /// <param name="time">ʱ�䣨�룩</param>
        /// <returns>��ʽ�����ʱ���ַ���</returns>
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

        /// <summary>
        /// ��ȡ��ǰʣ��ʱ��
        /// </summary>
        public float CurrentTime => currentTime;

        /// <summary>
        /// ��ȡʣ��ʱ�䣨��CurrentTime��ͬ�����ּ����ԣ�
        /// </summary>
        public float RemainingTime => currentTime;

        /// <summary>
        /// ��ȡʱ������
        /// </summary>
        public float TimeLimit => timeLimit;

        /// <summary>
        /// ����ʱ���Ƿ���������
        /// </summary>
        public bool IsRunning => isRunning;

        /// <summary>
        /// ����ʱ���Ƿ���ͣ
        /// </summary>
        public bool IsPaused => isPaused;

        /// <summary>
        /// ��ȡʱ����ȣ�0-1��
        /// </summary>
        public float Progress => timeLimit > 0 ? (timeLimit - currentTime) / timeLimit : 0f;

        /// <summary>
        /// ��ȡʣ��ʱ��ٷֱȣ�0-1��
        /// </summary>
        public float RemainingPercentage => timeLimit > 0 ? currentTime / timeLimit : 0f;

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
                Debug.Log($"[TimerManager] {message}");
            }
        }

        #endregion

        #region Editor���߷��������ڱ༭����ʹ�ã�

#if UNITY_EDITOR
        /// <summary>
        /// �ڱ༭���в��Կ�ʼ��ʱ���������ڵ��ԣ�
        /// </summary>
        [ContextMenu("���Կ�ʼ��ʱ��")]
        private void TestStartTimer()
        {
            StartTimer();
        }

        /// <summary>
        /// �ڱ༭���в���ֹͣ��ʱ���������ڵ��ԣ�
        /// </summary>
        [ContextMenu("����ֹͣ��ʱ��")]
        private void TestStopTimer()
        {
            StopTimer();
        }

        /// <summary>
        /// �ڱ༭���в�����ͣ��ʱ���������ڵ��ԣ�
        /// </summary>
        [ContextMenu("������ͣ��ʱ��")]
        private void TestPauseTimer()
        {
            PauseTimer();
        }

        /// <summary>
        /// �ڱ༭���в��Իָ���ʱ���������ڵ��ԣ�
        /// </summary>
        [ContextMenu("���Իָ���ʱ��")]
        private void TestResumeTimer()
        {
            ResumeTimer();
        }

        /// <summary>
        /// �ڱ༭���в������ʱ�䣨�����ڵ��ԣ�
        /// </summary>
        [ContextMenu("�������5��")]
        private void TestAddTime()
        {
            AddTime(5f);
        }
#endif

        #endregion
    }
}