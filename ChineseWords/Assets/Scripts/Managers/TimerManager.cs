using UnityEngine;
using Core;
using System;
using System.Collections;
using Core.Network;

namespace Managers
{
    /// <summary>
    /// TimerManager - 纯时钟版本
    /// 移除了文本显示功能，只保留Sprite时钟
    /// 保持所有原有接口和网络兼容性
    /// </summary>
    public class TimerManager : MonoBehaviour
    {
        [Header("倒计时配置")]
        [SerializeField] private float defaultTimeLimit = 20f;
        [SerializeField] private bool autoStartOnEnable = false;
        [SerializeField] private bool loopTimer = false;

        [Header("Sprite时钟")]
        [SerializeField] private RectTransform clockFace;     // 拖拽Face sprite
        [SerializeField] private RectTransform clockPointer;  // 拖拽Pointer sprite
        [SerializeField] private bool enableClock = true;     // 时钟总开关

        [Header("时钟动画设置")]
        [SerializeField] private PointerRotationMode rotationMode = PointerRotationMode.CounterClockwise;

        [Header("时钟颜色反馈（可选）")]
        [SerializeField] private bool enableColorFeedback = false;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color warningColor = Color.yellow;
        [SerializeField] private Color criticalColor = Color.red;
        [SerializeField] private float warningThreshold = 5f;
        [SerializeField] private float criticalThreshold = 3f;

        [Header("调试信息")]
        [SerializeField] private bool enableDebugLog = true;

        // 计时器状态
        private float timeLimit;
        private float currentTime;
        private Coroutine countdownCoroutine;
        private bool isRunning = false;
        private bool isPaused = false;

        // 时钟相关
        private Vector3 initialPointerRotation;
        private UnityEngine.UI.Image clockPointerImage;
        private UnityEngine.UI.Image clockFaceImage;

        public enum PointerRotationMode
        {
            Clockwise,        // 顺时针（时间流逝）
            CounterClockwise  // 逆时针（倒计时）
        }

        #region 事件定义 - 保持原有接口

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
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化组件
        /// </summary>
        private void InitializeComponent()
        {
            timeLimit = defaultTimeLimit;
            currentTime = timeLimit;

            // 初始化时钟
            InitializeClock();

            // 更新显示
            UpdateClock();

            LogDebug("TimerManager（纯时钟版）初始化完成");
        }

        /// <summary>
        /// 初始化时钟
        /// </summary>
        private void InitializeClock()
        {
            if (!enableClock) return;

            // 获取Image组件
            if (clockPointer != null)
            {
                clockPointerImage = clockPointer.GetComponent<UnityEngine.UI.Image>();
                initialPointerRotation = clockPointer.localEulerAngles;
                LogDebug($"时钟指针初始化完成: {clockPointer.name}");
            }
            if (clockFace != null)
            {
                clockFaceImage = clockFace.GetComponent<UnityEngine.UI.Image>();
                LogDebug($"时钟表盘初始化完成: {clockFace.name}");
            }

            // 设置初始可见性
            UpdateClockVisibility();
        }

        /// <summary>
        /// 应用配置 - 保持原有接口
        /// </summary>
        public void ApplyConfig(float newTimeLimit)
        {
            if (newTimeLimit <= 0)
            {
                Debug.LogWarning($"[TimerManager] 时间限制无效: {newTimeLimit}，使用默认值");
                newTimeLimit = defaultTimeLimit;
            }

            timeLimit = newTimeLimit;
            currentTime = timeLimit;

            LogDebug($"配置应用完成 - 时间限制: {timeLimit}秒");

            // 重置时钟
            ResetClock();

            // 更新显示
            UpdateClock();
        }

        #endregion

        #region 计时器控制 - 保持原有接口

        /// <summary>
        /// 开始倒计时
        /// </summary>
        public void StartTimer()
        {
            StopTimer();

            currentTime = timeLimit;
            isRunning = true;
            isPaused = false;

            // 重置时钟
            ResetClock();

            countdownCoroutine = StartCoroutine(Countdown());

            LogDebug($"开始倒计时 - 时长: {timeLimit}秒");
            OnTimerStarted?.Invoke();
        }

        /// <summary>
        /// 开始倒计时（指定时间）
        /// </summary>
        public void StartTimer(float customTime)
        {
            if (customTime <= 0)
            {
                Debug.LogWarning($"[TimerManager] 自定义时间无效: {customTime}");
                StartTimer();
                return;
            }

            StopTimer();

            timeLimit = customTime;
            currentTime = customTime;
            isRunning = true;
            isPaused = false;

            ResetClock();
            countdownCoroutine = StartCoroutine(Countdown());

            LogDebug($"开始自定义倒计时 - 时长: {customTime}秒");
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
            if (!isRunning || isPaused) return;

            isPaused = true;
            LogDebug("倒计时已暂停");
            OnTimerPaused?.Invoke();
        }

        /// <summary>
        /// 恢复倒计时
        /// </summary>
        public void ResumeTimer()
        {
            if (!isRunning || !isPaused) return;

            isPaused = false;
            LogDebug("倒计时已恢复");
            OnTimerResumed?.Invoke();
        }

        /// <summary>
        /// 重置倒计时
        /// </summary>
        public void ResetTimer()
        {
            StopTimer();
            currentTime = timeLimit;
            ResetClock();
            UpdateClock();
            LogDebug("倒计时已重置");
        }

        #endregion

        #region 时钟控制

        /// <summary>
        /// 重置时钟
        /// </summary>
        private void ResetClock()
        {
            if (!enableClock || clockPointer == null) return;

            clockPointer.localEulerAngles = initialPointerRotation;

            // 重置颜色
            if (enableColorFeedback)
            {
                UpdateClockColor(false);
            }

            LogDebug("时钟已重置");
        }

        /// <summary>
        /// 更新时钟显示 - 倒计时版本
        /// </summary>
        private void UpdateClock()
        {
            if (!enableClock || clockPointer == null || timeLimit <= 0) return;

            // 计算剩余时间对应的角度：每秒6度（360度/60秒）
            float rotationAngle = currentTime * 6f;

            // 根据旋转模式调整方向
            if (rotationMode == PointerRotationMode.CounterClockwise)
            {
                rotationAngle = -rotationAngle; // 逆时针（剩余时间越少，角度越小）
            }

            // 直接设置旋转
            Vector3 rotation = initialPointerRotation;
            rotation.z += rotationAngle;
            clockPointer.localEulerAngles = rotation;

            // 更新颜色反馈
            if (enableColorFeedback)
            {
                UpdateClockColor(false);
            }

            //LogDebug($"时钟更新: 剩余时间{currentTime}秒, 旋转角度{rotationAngle}度");
        }

        /// <summary>
        /// 计算指针角度 - 倒计时版本
        /// </summary>
        private float CalculatePointerAngle(float remainingSeconds)
        {
            float angle = remainingSeconds * 6f; // 每秒6度

            switch (rotationMode)
            {
                case PointerRotationMode.CounterClockwise:
                    return -angle; // 逆时针（剩余时间对应负角度）
                case PointerRotationMode.Clockwise:
                    return angle;  // 顺时针
                default:
                    return -angle;
            }
        }

        /// <summary>
        /// 更新时钟颜色反馈
        /// </summary>
        private void UpdateClockColor(bool isTimeUp)
        {
            if (!enableColorFeedback) return;

            Color targetColor = normalColor;

            if (isTimeUp)
            {
                targetColor = criticalColor;
            }
            else if (currentTime <= criticalThreshold)
            {
                targetColor = criticalColor;
            }
            else if (currentTime <= warningThreshold)
            {
                targetColor = warningColor;
            }
            if (clockFaceImage != null)
            {
                clockFaceImage.color = Color.Lerp(Color.white, targetColor, 0.3f);
            }
        }

        /// <summary>
        /// 更新时钟可见性
        /// </summary>
        private void UpdateClockVisibility()
        {
            if (clockFace != null)
            {
                clockFace.gameObject.SetActive(enableClock);
            }

            if (clockPointer != null)
            {
                clockPointer.gameObject.SetActive(enableClock);
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
                // 暂停处理
                while (isPaused)
                {
                    yield return null;
                }

                // 更新时钟显示
                UpdateClock();

                // 检查警告阈值
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

                yield return new WaitForSeconds(1f);
                currentTime -= 1f;
            }

            // 时间到
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

            // 更新时钟显示（时间到状态）
            UpdateClock();

            // 更新颜色为危险色
            if (enableColorFeedback)
            {
                UpdateClockColor(true);
            }

            OnTimeUp?.Invoke();
        }

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