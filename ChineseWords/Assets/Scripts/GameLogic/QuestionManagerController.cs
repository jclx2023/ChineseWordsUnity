using UnityEngine;
using Core;    // QuestionType, GameManager
using GameLogic;

public class QuestionManagerController : MonoBehaviour
{
    private QuestionManagerBase manager;

    void Start()
    {
        Debug.Log("QuestionManagerController 启动，SelectedType = " + GameManager.Instance.SelectedType);
        // 1. 根据用户在主菜单选的题型，从 GameManager 里拿到 SelectedType
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
            default:
                Debug.LogError("未实现的题型：" + GameManager.Instance.SelectedType);
                return;
        }

        // 2. 给管理器的结果事件绑定统一回调
        manager.OnAnswerResult += HandleAnswerResult;

        // 3. 加载第一题
        manager.LoadQuestion();
    }

    private void HandleAnswerResult(bool isCorrect)
    {
        if (isCorrect)
            Debug.Log("回答正确，扣血逻辑放这里");
        else
            Debug.Log("回答错误，扣血逻辑放这里");

        // 延迟几秒后载入下一题
        Invoke(nameof(NextQuestion), 1f);
    }

    private void NextQuestion()
    {
        manager.LoadQuestion();
    }
}
