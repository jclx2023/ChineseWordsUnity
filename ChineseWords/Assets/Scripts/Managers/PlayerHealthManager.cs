using UnityEngine;
using TMPro;
using Core;  // 如果 QuestionManagerBase 在 Core 命名空间下

/// <summary>
/// 管理玩家血量：答错扣血，血量耗尽时显示“游戏失败”
/// 挂到与 QuestionManagerController 同一个 GameObject 上即可自动订阅答题结果事件
/// </summary>
public class PlayerHealthManager : MonoBehaviour
{
    [Header("血量显示 (TextMeshPro)")]
    [Tooltip("用于显示当前血量的 TMP_Text")]
    public TMP_Text healthText;

    [Header("游戏结束面板")]
    [Tooltip("血量<=0 时激活的 GameOver 面板")]
    public GameObject gameOverPanel;

    private int currentHealth;
    private int damagePerWrong;
    private QuestionManagerBase questionManager;

    void Awake()
    {
        // 确保 Game Over 面板一开始隐藏
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    public void ApplyConfig(int initialHealth, int damage)
    {
        currentHealth = initialHealth;
        damagePerWrong = damage;
        UpdateHealthUI();
    }

    public void BindManager(QuestionManagerBase m)
    {
        questionManager = m;
    }

    public void HPHandleAnswerResult(bool isCorrect)
    {
        // 如果答错则扣血并更新 UI
        if (!isCorrect)
        {
            Debug.LogError("掉血了");
            currentHealth -= damagePerWrong;
            UpdateHealthUI();

            // 血量<=0 时触发游戏结束
            if (currentHealth <= 0)
                GameOver();
        }
        return;
    }

    private void UpdateHealthUI()
    {
        if (healthText != null)
            healthText.text = $"血量：{currentHealth}";
    }

    private void GameOver()
    {
        Debug.Log("血量耗尽——游戏失败");
        // 显示 Game Over 面板
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        // 禁用题目管理器，停止出题
        if (questionManager != null)
            questionManager.enabled = false;
    }
}