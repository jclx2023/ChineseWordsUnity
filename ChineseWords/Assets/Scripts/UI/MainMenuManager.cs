using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;      // 仅在 Editor 模式下用来停止播放
#endif
using TMPro;
using Core; // 引用 QuestionType

public class MainMenuManager : MonoBehaviour
{
    public void OnClick_FillBlank()
    {
        GameManager.Instance.SetQuestionType(QuestionType.FillBlank);
        SceneManager.LoadScene("Game");
    }

    public void OnClick_Choose()
    {
        GameManager.Instance.SetQuestionType(QuestionType.Choose);
        SceneManager.LoadScene("Game");
    }
    public void OnClick_TorF()
    {
        GameManager.Instance.SetQuestionType(QuestionType.TorF);
        SceneManager.LoadScene("Game");
    }
    public void OnClick_HandWriting()
    {
        GameManager.Instance.SetQuestionType(QuestionType.HandWriting);
        SceneManager.LoadScene("Game");
    }
    public void OnClick_IdiomChain()
    {
        GameManager.Instance.SetQuestionType(QuestionType.IdiomChain);
        SceneManager.LoadScene("Game");
    }
    public void OnClick_TextPinyin()
    {
        GameManager.Instance.SetQuestionType(QuestionType.TextPinyin);
        SceneManager.LoadScene("Game");
    }
    public void OnClick_HardFill()
    {
        GameManager.Instance.SetQuestionType(QuestionType.HardFill);
        SceneManager.LoadScene("Game");
    }
    public void OnClick_SoftFill()
    {
        GameManager.Instance.SetQuestionType(QuestionType.SoftFill);
        SceneManager.LoadScene("Game");
    }
    public void OnClick_AbbrFill()
    {
        GameManager.Instance.SetQuestionType(QuestionType.AbbrFill);
        SceneManager.LoadScene("Game");
    }
    public void OnClick_SentimentTorF()
    {
        GameManager.Instance.SetQuestionType(QuestionType.SentimentTorF);
        SceneManager.LoadScene("Game");
    }
    public void OnClick_SimularWordChoice()
    {
        GameManager.Instance.SetQuestionType(QuestionType.SimularWordChoice);
        SceneManager.LoadScene("Game");
    }
    public void OnClick_UsageTorF()
    {
        GameManager.Instance.SetQuestionType(QuestionType.UsageTorF);
        SceneManager.LoadScene("Game");
    }
    public void OnClick_ExplanationChoice()
    {
        GameManager.Instance.SetQuestionType(QuestionType.ExplanationChoice);
        SceneManager.LoadScene("Game");
    }

    public void OnClick_Exit()
    {
        // 如果是在编辑器里，直接停止播放
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        // 在打包后的执行文件里，调用退出
        Application.Quit();
#endif
    }
}
