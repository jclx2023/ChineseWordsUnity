using UnityEngine;
using TMPro;
using Core;  // ��� QuestionManagerBase �� Core �����ռ���
using System;
using System.Collections;

/// <summary>
/// ������ʱ�߼�����ͨ���ⲿ��������ʱ��
/// </summary>
public class TimerManager : MonoBehaviour
{
    [Header("����ʱ��ʾ�ı�")]
    public TMP_Text timerText;

    public event Action OnTimeUp;

    private float timeLimit = 10f;
    private Coroutine countdownCoroutine;

    /// <summary>
    /// ע���ⲿ���õ�ʱ������
    /// </summary>
    public void ApplyConfig(float newTimeLimit)
    {
        timeLimit = newTimeLimit;
    }

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
            timerText.text = "Countdown: " + Mathf.CeilToInt(remaining).ToString();
            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }
        timerText.text = "0";
        OnTimeUp?.Invoke();
        countdownCoroutine = null;
    }
}
