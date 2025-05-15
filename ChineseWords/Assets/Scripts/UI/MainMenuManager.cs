using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;      // ���� Editor ģʽ������ֹͣ����
#endif
using TMPro;
using Core; // ���� QuestionType

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
        // ������ڱ༭���ֱ��ֹͣ����
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        // �ڴ�����ִ���ļ�������˳�
        Application.Quit();
#endif
    }
}
