using UnityEngine;
using TMPro;
using System;
using System.Collections;

public class TimerManager : MonoBehaviour
{
    [Header("����ʱʱ�����룩")]
    public float timeLimit = 10f;

    [Header("����ʱ��ʾ�ı�")]
    public TMP_Text timerText;

    /// <summary>
    /// ����ʱ����ʱ����
    /// </summary>
    public event Action OnTimeUp;

    private Coroutine countdownCoroutine;

    /// <summary>
    /// ��ʼ�����õ���ʱ
    /// </summary>
    public void StartTimer()
    {
        StopTimer();
        countdownCoroutine = StartCoroutine(Countdown());
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
    }

    private IEnumerator Countdown()
    {
        float remaining = timeLimit;
        while (remaining > 0f)
        {
            // ��ʾȡ����
            timerText.text = "Countdown: "+ Mathf.CeilToInt(remaining).ToString();
            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }
        timerText.text = "0";
        OnTimeUp?.Invoke();
        countdownCoroutine = null;
    }
}
