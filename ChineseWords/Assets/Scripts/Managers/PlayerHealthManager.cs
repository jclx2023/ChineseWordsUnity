using UnityEngine;
using TMPro;
using Core;  // ��� QuestionManagerBase �� Core �����ռ���

/// <summary>
/// �������Ѫ��������Ѫ��Ѫ���ľ�ʱ��ʾ����Ϸʧ�ܡ�
/// �ҵ��� QuestionManagerController ͬһ�� GameObject �ϼ����Զ����Ĵ������¼�
/// </summary>
public class PlayerHealthManager : MonoBehaviour
{
    [Header("Ѫ����ʾ (TextMeshPro)")]
    [Tooltip("������ʾ��ǰѪ���� TMP_Text")]
    public TMP_Text healthText;

    [Header("��Ϸ�������")]
    [Tooltip("Ѫ��<=0 ʱ����� GameOver ���")]
    public GameObject gameOverPanel;

    private int currentHealth;
    private int damagePerWrong;
    private QuestionManagerBase questionManager;

    void Awake()
    {
        // ȷ�� Game Over ���һ��ʼ����
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
        // ���������Ѫ������ UI
        if (!isCorrect)
        {
            Debug.LogError("��Ѫ��");
            currentHealth -= damagePerWrong;
            UpdateHealthUI();

            // Ѫ��<=0 ʱ������Ϸ����
            if (currentHealth <= 0)
                GameOver();
        }
        return;
    }

    private void UpdateHealthUI()
    {
        if (healthText != null)
            healthText.text = $"Ѫ����{currentHealth}";
    }

    private void GameOver()
    {
        Debug.Log("Ѫ���ľ�������Ϸʧ��");
        // ��ʾ Game Over ���
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        // ������Ŀ��������ֹͣ����
        if (questionManager != null)
            questionManager.enabled = false;
    }
}