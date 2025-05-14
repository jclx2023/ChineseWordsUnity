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
