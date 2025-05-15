using UnityEngine;
using TMPro;
using System;
using System.Collections;

public class TimerManager : MonoBehaviour
{
    [Header("倒计时时长（秒）")]
    public float timeLimit = 10f;

    [Header("倒计时显示文本")]
    public TMP_Text timerText;

    /// <summary>
    /// 倒计时结束时触发
    /// </summary>
    public event Action OnTimeUp;

    private Coroutine countdownCoroutine;

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
            // 显示取整秒
            timerText.text = "Countdown: "+ Mathf.CeilToInt(remaining).ToString();
            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }
        timerText.text = "0";
        OnTimeUp?.Invoke();
        countdownCoroutine = null;
    }
}
