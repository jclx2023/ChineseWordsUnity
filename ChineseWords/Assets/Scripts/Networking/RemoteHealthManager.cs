using TMPro;
using UnityEngine;

public class RemoteHealthManager : MonoBehaviour
{
    [Header("Զ�����Ѫ����ʾ�ı�")]
    public TMP_Text healthText;

    private int _currentHealth;

    /// <summary>
    /// ��ʼ��Զ�����Ѫ��
    /// </summary>
    public void Initialize(int initHealth)
    {
        _currentHealth = initHealth;
        UpdateUI();
    }

    /// <summary>
    /// ���� isCorrect �������Զ�����Ѫ��
    /// </summary>
    public void HandleRemoteAnswerResult(bool isCorrect)
    {
        if (!isCorrect)
            _currentHealth = Mathf.Max(0, _currentHealth - 1);
        UpdateUI();
    }

    private void UpdateUI()
    {
        healthText.text = $"Player 2: {_currentHealth}";
    }
}
