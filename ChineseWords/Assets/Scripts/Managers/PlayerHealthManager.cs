using UnityEngine;
using TMPro;
using Core;  // ��� QuestionManagerBase �� Core �����ռ���

/// <summary>
/// �������Ѫ��������Ѫ��Ѫ���ľ�ʱ��ʾ����Ϸʧ�ܡ�
/// �ҵ��� QuestionManagerController ͬһ�� GameObject �ϼ����Զ����Ĵ������¼�
/// </summary>
public class PlayerHealthManager : MonoBehaviour
{
    [Header("��ʼѪ�� (a)")]
    [Tooltip("��ҳ�ʼѪ��")]
    public int initialHealth = 5;

    [Header("ÿ�δ���Ѫ (b)")]
    [Tooltip("ÿ�λش����ʱ�۳���Ѫ��")]
    public int damagePerWrong = 1;

    [Header("Ѫ����ʾ (TextMeshPro)")]
    [Tooltip("������ʾ��ǰѪ���� TMP_Text")]
    public TMP_Text healthText;

    [Header("��Ϸ�������")]
    [Tooltip("Ѫ��<=0 ʱ����� GameOver ���")]
    public GameObject gameOverPanel;

    private int currentHealth;
    private QuestionManagerBase questionManager;

    void Awake()
    {
        // 1. ��ʼ��Ѫ��
        currentHealth = initialHealth;
        UpdateHealthUI();

        // 2. ȷ�� Game Over ���һ��ʼ����
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    void Start()
    {
        // 3. ��ȡ��Ŀ�������������Ĵ�����
        questionManager = GetComponent<QuestionManagerBase>();
        if (questionManager != null)
        {
            //questionManager.OnAnswerResult += HPHandleAnswerResult;
        }
        else
        {
            Debug.LogError("PlayerHealthManager���Ҳ��� QuestionManagerBase����ȷ���˽ű��� QuestionManagerController ����ͬһ GameObject ��");
        }
    }

    public void HPHandleAnswerResult(bool isCorrect)
    {
        // 4. ���������Ѫ������ UI
        if (!isCorrect)
        {
            Debug.LogError("��Ѫ��");
            currentHealth -= damagePerWrong;
            UpdateHealthUI();

            // 5. Ѫ��<=0 ʱ������Ϸ����
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
