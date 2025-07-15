using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

/// <summary>
/// 启动加载UI - 超级简单版本
/// </summary>
public class StartupLoadingUI : MonoBehaviour
{
    [Header("UI组件")]
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("设置")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private float successDisplayTime = 1.5f;

    private void Start()
    {
        SetStatusText("正在连接...");
    }

    /// <summary>
    /// 当连接成功时调用
    /// </summary>
    public void OnConnectionSuccess()
    {
        SetStatusText("连接成功！即将进入游戏");
        StartCoroutine(DelayedSceneTransition());
    }

    /// <summary>
    /// 当连接失败时调用
    /// </summary>
    public void OnConnectionFailed(string reason)
    {
        SetStatusText($"连接失败: {reason}");
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