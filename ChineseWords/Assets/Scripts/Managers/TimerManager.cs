using UnityEngine;
using TMPro;
using Core;  // 如果 QuestionManagerBase 在 Core 命名空间下
using System;
using System.Collections;

/// <summary>
/// 管理倒计时逻辑，可通过外部配置设置时长
/// </summary>
public class TimerManager : MonoBehaviour
{
    [Header("倒计时显示文本")]
    public TMP_Text timerText;

    public event Action OnTimeUp;

    private float timeLimit = 10f;
    private Coroutine countdownCoroutine;

    /// <summary>
    /// 注入外部配置的时间限制
    /// </summary>
    public void ApplyConfig(float newTimeLimit)
    {
        timeLimit = newTimeLimit;
    }

    /// <summary>
    /// 开始或重置倒计时
    /// </summary>
    public void StartTimer()
    {
        StopTimer();
        countdownCoroutine = StartCoroutine(Countdown());
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
    }

    private IEnumerator Countdown()
    {
        float remaining = timeLimit;
        while (remaining > 0f)
        {
            timerText.text = "Countdown: " + Mathf.CeilToInt(remaining).ToString();
            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }
        timerText.text = "0";
        OnTimeUp?.Invoke();
        countdownCoroutine = null;
    }
}
