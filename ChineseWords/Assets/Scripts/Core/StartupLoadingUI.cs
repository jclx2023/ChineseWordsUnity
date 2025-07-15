using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

/// <summary>
/// ��������UI - �����򵥰汾
/// </summary>
public class StartupLoadingUI : MonoBehaviour
{
    [Header("UI���")]
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("����")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private float successDisplayTime = 1.5f;

    private void Start()
    {
        SetStatusText("��������...");
    }

    /// <summary>
    /// �����ӳɹ�ʱ����
    /// </summary>
    public void OnConnectionSuccess()
    {
        SetStatusText("���ӳɹ�������������Ϸ");
        StartCoroutine(DelayedSceneTransition());
    }

    /// <summary>
    /// ������ʧ��ʱ����
    /// </summary>
    public void OnConnectionFailed(string reason)
    {
        SetStatusText($"����ʧ��: {reason}");
    }

    private void SetStatusText(string text)
    {
        if (statusText != null)
        {
            statusText.text = text;
        }
        Debug.Log($"[StartupLoading] {text}");
    }

    private IEnumerator DelayedSceneTransition()
    {
        yield return new WaitForSeconds(successDisplayTime);
        SceneManager.LoadScene(mainMenuSceneName);
    }
}