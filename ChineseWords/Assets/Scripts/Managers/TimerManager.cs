using UnityEngine;
using TMPro;
using Core;
using System;
using System.Collections;

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
        [SerializeField] private float defaultTimeLimit = 10f;
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

        // 计时器状态
        private bool isRunning = false;
        private bool isPaused = false;

        // 关联的题目管理器（可选）
        private QuestionManagerBase questionManager;

        #region 事件定义

        /// <summary>
        /// 时间到事件
        /// </summary>
        public event Action OnTimeUp;

        /// <summary>
        /// 时间变化事件（每秒触发）
        /// </summary>
        public event Action<float> OnTimeChanged;

        /// <summary>
        /// 进入警告时间事件
        /// </summary>
        public event Action OnWarningTime;

        /// <summary>
        /// 进入危险时间事件
        /// </summary>
        public event Action OnCriticalTime;

        /// <summary>
        /// 计时器启动事件
        /// </summary>
        public event Action OnTimerStarted;

        /// <summary>
        /// 计时器停止事件
        /// </summary>
        public event Action OnTimerStopped;

        /// <summary>
        /// 计时器暂停事件
        /// </summary>
        public event Action OnTimerPaused;

        /// <summary>
        /// 计时器恢复事件
        /// </summary>
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
        /// 绑定题目管理器（可选，用于自动化流程）
        /// </summary>
        /// <param name="manager">题目管理器</param>
        public void BindManager(QuestionManagerBase manager)
        {
            if (manager == null)
            {
                Debug.LogWarning("[TimerManager] 尝试绑定空的题目管理器");
                return;
            }

            // 先取消之前的订阅
            UnsubscribeFromEvents();

            questionManager = manager;

            // 可以在这里添加与题目管理器的联动逻辑
            LogDebug($"成功绑定题目管理器: {questionManager.GetType().Name}");
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
        /// <param name="customTime">自定义倒计时时间</param>
        public void StartTimer(float customTime)
        {
            if (customTime <= 0)
            {
                Debug.LogWarning($"[TimerManager] 自定义时间无效: {customTime}，使用默认时间");
                StartTimer();
                return;
            }

            float originalTimeLimit = timeLimit;
            timeLimit = customTime;
            StartTimer();
            timeLimit = originalTimeLimit; // 恢复原始配置

            LogDebug($"开始自定义倒计时 - 时长: {customTime}秒");
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
        /// <param name="seconds">要减少的秒数</param>
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

            // 更新UI显示
            UpdateTimerUI(true);

            // 触发时间到事件
            OnTimeUp?.Invoke();

            if (loopTimer)
            {
                // 如果是循环计时器，重新开始
                LogDebug("循环计时器，重新开始");
                StartTimer();
            }
            else
            {
                // 停止计时器
                isRunning = false;
                OnTimerStopped?.Invoke();
            }
        }

        #endregion

        #region UI更新

        /// <summary>
        /// 更新倒计时UI显示
        /// </summary>
        /// <param name="isTimeUp">是否时间到</param>
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

                // 根据剩余时间设置颜色
                UpdateTimerColor();
            }
        }

        /// <summary>
        /// 格式化时间显示
        /// </summary>
        /// <param name="time">时间（秒）</param>
        /// <returns>格式化后的时间字符串</returns>
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

        /// <summary>
        /// 获取当前剩余时间
        /// </summary>
        public float CurrentTime => currentTime;

        /// <summary>
        /// 获取剩余时间（与CurrentTime相同，保持兼容性）
        /// </summary>
        public float RemainingTime => currentTime;

        /// <summary>
        /// 获取时间限制
        /// </summary>
        public float TimeLimit => timeLimit;

        /// <summary>
        /// 检查计时器是否正在运行
        /// </summary>
        public bool IsRunning => isRunning;

        /// <summary>
        /// 检查计时器是否暂停
        /// </summary>
        public bool IsPaused => isPaused;

        /// <summary>
        /// 获取时间进度（0-1）
        /// </summary>
        public float Progress => timeLimit > 0 ? (timeLimit - currentTime) / timeLimit : 0f;

        /// <summary>
        /// 获取剩余时间百分比（0-1）
        /// </summary>
        public float RemainingPercentage => timeLimit > 0 ? currentTime / timeLimit : 0f;

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
                Debug.Log($"[TimerManager] {message}");
            }
        }

        #endregion

        #region Editor工具方法（仅在编辑器中使用）

#if UNITY_EDITOR
        /// <summary>
        /// 在编辑器中测试开始计时器（仅用于调试）
        /// </summary>
        [ContextMenu("测试开始计时器")]
        private void TestStartTimer()
        {
            StartTimer();
        }

        /// <summary>
        /// 在编辑器中测试停止计时器（仅用于调试）
        /// </summary>
        [ContextMenu("测试停止计时器")]
        private void TestStopTimer()
        {
            StopTimer();
        }

        /// <summary>
        /// 在编辑器中测试暂停计时器（仅用于调试）
        /// </summary>
        [ContextMenu("测试暂停计时器")]
        private void TestPauseTimer()
        {
            PauseTimer();
        }

        /// <summary>
        /// 在编辑器中测试恢复计时器（仅用于调试）
        /// </summary>
        [ContextMenu("测试恢复计时器")]
        private void TestResumeTimer()
        {
            ResumeTimer();
        }

        /// <summary>
        /// 在编辑器中测试添加时间（仅用于调试）
        /// </summary>
        [ContextMenu("测试添加5秒")]
        private void TestAddTime()
        {
            AddTime(5f);
        }
#endif

        #endregion
    }
}