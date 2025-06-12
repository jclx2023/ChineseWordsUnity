using UnityEngine;
using TMPro;
using Core;
using System;
using System.Collections;
using Core.Network;

namespace Managers
{
    /// <summary>
    /// 倒计时管理器
    /// - 管理答题倒计时逻辑，可通过外部配置设置时长
    /// - 支持暂停、恢复、重置等功能
    /// - 提供丰富的时间事件通知
    /// - 支持多种显示格式和样式
    /// </summary>
    public class TimerManager : MonoBehaviour
    {
        [Header("倒计时配置")]
        [SerializeField] private float defaultTimeLimit = 20f;
        [SerializeField] private bool autoStartOnEnable = false;
        [SerializeField] private bool loopTimer = false;

        [Header("UI组件")]
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private string timerTextFormat = "倒计时: {0}";
        [SerializeField] private string timeUpText = "时间到！";

        [Header("颜色设置")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color warningColor = Color.yellow;
        [SerializeField] private Color criticalColor = Color.red;
        [SerializeField] private float warningThreshold = 5f;
        [SerializeField] private float criticalThreshold = 3f;

        [Header("调试信息")]
        [SerializeField] private bool enableDebugLog = true;

        // 当前配置的时间限制
        private float timeLimit;

        // 当前剩余时间
        private float currentTime;

        // 倒计时协程
        private Coroutine countdownCoroutine;
        private bool isRunning = false;
        private bool isPaused = false;


        #region 事件定义

        public event Action OnTimeUp;
        public event Action<float> OnTimeChanged;
        public event Action OnWarningTime;
        public event Action OnCriticalTime;
        public event Action OnTimerStarted;
        public event Action OnTimerStopped;
        public event Action OnTimerPaused;
        public event Action OnTimerResumed;

        #endregion

        #region Unity生命周期

        private void Awake()
        {
            InitializeComponent();
        }

        private void Start()
        {
            // 如果没有通过外部配置，则使用默认配置
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

        #region 初始化

        /// <summary>
        /// 初始化组件
        /// </summary>
        private void InitializeComponent()
        {
            // 设置默认时间限制
            timeLimit = defaultTimeLimit;
            currentTime = timeLimit;

            // 初始化UI
            UpdateTimerUI();

            LogDebug("TimerManager 初始化完成");
        }

        /// <summary>
        /// 应用配置
        /// </summary>
        /// <param name="newTimeLimit">新的时间限制</param>
        public void ApplyConfig(float newTimeLimit)
        {
            if (newTimeLimit <= 0)
            {
                Debug.LogWarning($"[TimerManager] 时间限制不能小于等于0，使用默认值{defaultTimeLimit}。传入值: {newTimeLimit}");
                newTimeLimit = defaultTimeLimit;
            }

            timeLimit = newTimeLimit;
            currentTime = timeLimit;

            LogDebug($"配置应用完成 - 时间限制: {timeLimit}秒");

            // 更新UI显示
            UpdateTimerUI();
        }


        /// <summary>
        /// 取消事件订阅
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            // 如果有与其他组件的事件订阅，在这里取消
            LogDebug("取消事件订阅");
        }

        #endregion

        #region 计时器控制

        /// <summary>
        /// 开始或重置倒计时
        /// </summary>
        public void StartTimer()
        {
            StopTimer(); // 先停止之前的计时器

            currentTime = timeLimit;
            isRunning = true;
            isPaused = false;

            countdownCoroutine = StartCoroutine(Countdown());

            LogDebug($"开始倒计时 - 时长: {timeLimit}秒");

            // 触发启动事件
            OnTimerStarted?.Invoke();
        }

        /// <summary>
        /// 开始倒计时（指定时间）
        /// </summary>
        public void StartTimer(float customTime)
        {
            if (customTime <= 0)
            {
                Debug.LogWarning($"[TimerManager] 自定义时间无效: {customTime}，使用当前配置时间{timeLimit}");
                StartTimer();
                return;
            }

            StopTimer(); // 先停止之前的计时器

            timeLimit = customTime;
            currentTime = customTime;
            isRunning = true;
            isPaused = false;

            countdownCoroutine = StartCoroutine(Countdown());

            LogDebug($"开始自定义倒计时 - 时长: {customTime}秒");

            // 触发启动事件
            OnTimerStarted?.Invoke();
        }

        /// <summary>
        /// 停止倒计时
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
                LogDebug("倒计时已停止");
                OnTimerStopped?.Invoke();
            }
        }

        /// <summary>
        /// 暂停倒计时
        /// </summary>
        public void PauseTimer()
        {
            if (!isRunning || isPaused)
            {
                LogDebug("计时器未运行或已暂停，无法暂停");
                return;
            }

            isPaused = true;
            LogDebug("倒计时已暂停");

            OnTimerPaused?.Invoke();
        }

        /// <summary>
        /// 恢复倒计时
        /// </summary>
        public void ResumeTimer()
        {
            if (!isRunning || !isPaused)
            {
                LogDebug("计时器未运行或未暂停，无法恢复");
                return;
            }

            isPaused = false;
            LogDebug("倒计时已恢复");

            OnTimerResumed?.Invoke();
        }

        /// <summary>
        /// 重置倒计时（回到初始时间但不开始）
        /// </summary>
        public void ResetTimer()
        {
            StopTimer();
            currentTime = timeLimit;
            UpdateTimerUI();

            LogDebug("倒计时已重置");
        }

        /// <summary>
        /// 添加时间
        /// </summary>
        /// <param name="seconds">要添加的秒数</param>
        public void AddTime(float seconds)
        {
            if (seconds <= 0)
            {
                Debug.LogWarning($"[TimerManager] 添加时间数值无效: {seconds}");
                return;
            }

            currentTime += seconds;
            LogDebug($"添加时间 {seconds}秒，当前剩余时间: {currentTime}秒");

            UpdateTimerUI();
            OnTimeChanged?.Invoke(currentTime);
        }

        /// <summary>
        /// 减少时间
        /// </summary>
        public void ReduceTime(float seconds)
        {
            if (seconds <= 0)
            {
                Debug.LogWarning($"[TimerManager] 减少时间数值无效: {seconds}");
                return;
            }

            currentTime -= seconds;
            if (currentTime < 0)
                currentTime = 0;

            LogDebug($"减少时间 {seconds}秒，当前剩余时间: {currentTime}秒");

            UpdateTimerUI();
            OnTimeChanged?.Invoke(currentTime);

            // 如果时间用完，触发时间到事件
            if (currentTime <= 0 && isRunning)
            {
                HandleTimeUp();
            }
        }

        #endregion

        #region 倒计时逻辑

        /// <summary>
        /// 倒计时协程
        /// </summary>
        private IEnumerator Countdown()
        {
            bool hasTriggeredWarning = false;
            bool hasTriggeredCritical = false;

            while (currentTime > 0f && isRunning)
            {
                // 如果暂停，等待恢复
                while (isPaused)
                {
                    yield return null;
                }

                // 更新UI
                UpdateTimerUI();

                // 检查警告和危险阈值
                if (!hasTriggeredWarning && currentTime <= warningThreshold && currentTime > criticalThreshold)
                {
                    hasTriggeredWarning = true;
                    OnWarningTime?.Invoke();
                    LogDebug("进入警告时间");
                }

                if (!hasTriggeredCritical && currentTime <= criticalThreshold)
                {
                    hasTriggeredCritical = true;
                    OnCriticalTime?.Invoke();
                    LogDebug("进入危险时间");
                }

                // 触发时间变化事件
                OnTimeChanged?.Invoke(currentTime);

                // 等待1秒
                yield return new WaitForSeconds(1f);

                // 减少1秒
                currentTime -= 1f;
            }

            // 时间用完
            if (isRunning)
            {
                currentTime = 0f;
                HandleTimeUp();
            }

            countdownCoroutine = null;
        }

        /// <summary>
        /// 处理时间到
        /// </summary>
        private void HandleTimeUp()
        {
            LogDebug("时间到！");
            UpdateTimerUI(true);

            OnTimeUp?.Invoke();
        }

        private void HandleTimeoutAnswerSubmission()
        {
            LogDebug("超时自动提交答案");
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

        #region UI更新

        private void UpdateTimerUI(bool isTimeUp = false)
        {
            if (timerText == null)
            {
                Debug.LogWarning("[TimerManager] 计时器文本组件未设置");
                return;
            }

            if (isTimeUp)
            {
                timerText.text = timeUpText;
                timerText.color = criticalColor;
            }
            else
            {
                // 格式化显示时间
                string timeDisplay = FormatTime(currentTime);
                timerText.text = string.Format(timerTextFormat, timeDisplay);

                UpdateTimerColor();
            }
        }

        /// <summary>
        /// 格式化时间显示
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
        /// 根据剩余时间更新颜色
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

        #region 公共接口

        public float CurrentTime => currentTime;
        public float RemainingTime => currentTime;
        public float TimeLimit => timeLimit;
        public bool IsRunning => isRunning;
        public bool IsPaused => isPaused;
        public float Progress => timeLimit > 0 ? (timeLimit - currentTime) / timeLimit : 0f;
        public float RemainingPercentage => timeLimit > 0 ? currentTime / timeLimit : 0f;

        #endregion

        #region 调试工具

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