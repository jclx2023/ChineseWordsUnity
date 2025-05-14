using UnityEngine;
using Core;    // QuestionType, GameManager
using GameLogic;
using GameLogic.FillBlank;
using GameLogic.TorF;

public class QuestionManagerController : MonoBehaviour
{
    private QuestionManagerBase manager;

    void Start()
    {
        Debug.Log("QuestionManagerController ������SelectedType = " + GameManager.Instance.SelectedType);
        // 1. �����û������˵�ѡ�����ͣ��� GameManager ���õ� SelectedType
        switch (GameManager.Instance.SelectedType)
        {
            case QuestionType.FillBlank:
                manager = gameObject.AddComponent<FillBlankQuestionManager>();
                break;
            case QuestionType.Choose:
                manager = gameObject.AddComponent<ChooseQuestionManager>();
                break;
            case QuestionType.TorF:
                manager = gameObject.AddComponent<TorFQuestionManager>();
                break;
            case QuestionType.HandWriting:
                manager = gameObject.AddComponent<HandWritingQuestionManager>();
                break;
            case QuestionType.IdiomChain:
                manager = gameObject.AddComponent<IdiomChainQuestionManager>();
                break;
            case QuestionType.TextPinyin:
                manager = gameObject.AddComponent<TextPinyinQuestionManager>();
                break;
            case QuestionType.HardFill:
                manager = gameObject.AddComponent<HardFillQuestionManager>();
                break;
            case QuestionType.SoftFill:
                manager = gameObject.AddComponent<SoftFillQuestionManager>();
                break;
            case QuestionType.AbbrFill:
                manager = gameObject.AddComponent<AbbrFillQuestionManager>();
                break;
            case QuestionType.SentimentTorF:
                manager = gameObject.AddComponent<SentimentTorFQuestionManager>();
                break;
            default:
                Debug.LogError("δʵ�ֵ����ͣ�" + GameManager.Instance.SelectedType);
                return;
        }

        // 2. ���������Ľ���¼���ͳһ�ص�
        manager.OnAnswerResult += HandleAnswerResult;

        // 3. ���ص�һ��
        manager.LoadQuestion();
    }

    private void HandleAnswerResult(bool isCorrect)
    {
        if (isCorrect)
            Debug.Log("�ش���ȷ����Ѫ�߼�������");
        else
            Debug.Log("�ش���󣬿�Ѫ�߼�������");

        // �ӳټ����������һ��
        Invoke(nameof(NextQuestion), 1f);
    }

    private void NextQuestion()
    {
        manager.LoadQuestion();
    }
}
