using TMPro;
using UnityEngine;

public class RemoteHealthManager : MonoBehaviour
{
    [Header("远端玩家血量显示文本")]
    public TMP_Text healthText;

    private int _currentHealth;

    /// <summary>
    /// 初始化远端玩家血量
    /// </summary>
    public void Initialize(int initHealth)
    {
        _currentHealth = initHealth;
        UpdateUI();
    }

    /// <summary>
    /// 根据 isCorrect 结果更新远端玩家血量
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
