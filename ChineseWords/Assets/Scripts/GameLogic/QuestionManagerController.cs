using UnityEngine;
using Core;              // QuestionType, GameManager
using GameLogic;
using GameLogic.FillBlank;
using GameLogic.TorF;
using GameLogic.Choice;
using System.Collections;

public class QuestionManagerController : MonoBehaviour
{
    private QuestionManagerBase manager;
    private TimerManager timerManager;

    [Header("超时后延迟下一题时长")]
    [Tooltip("答题超时或回答后，等待多长时间再出下一题")]
    public float timeUpDelay = 1f;

    void Start()
    {
        Debug.Log("QuestionManagerController 启动，SelectedType = " + GameManager.Instance.SelectedType);

        // 1. 根据题型添加对应的 QuestionManager
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
            case QuestionType.SimularWordChoice:
                manager = gameObject.AddComponent<SimularWordChoiceQuestionManager>();
                break;
            default:
                Debug.LogError("未实现的题型：" + GameManager.Instance.SelectedType);
                return;
        }

        // 2. 订阅答题结果
        manager.OnAnswerResult += HandleAnswerResult;

        // 3. 获取并订阅 TimerManager 的超时事件
        timerManager = GetComponent<TimerManager>();
        if (timerManager == null)
            Debug.LogError("QuestionManagerController：缺少 TimerManager 组件，请挂在同一物体上");
        else
            timerManager.OnTimeUp += HandleTimeUp;
        StartCoroutine(DelayedFirstQuestion());
        // 4. 首次出题并启动计时
        //LoadNextQuestion();
    }
    private IEnumerator DelayedFirstQuestion()
    {
        yield return null;
        LoadNextQuestion();
    }

    /// <summary>
    /// 统一处理用户作答（或超时）的结果
    /// </summary>
    private void HandleAnswerResult(bool isCorrect)
    {
        // 回答后立刻停止计时
        timerManager.StopTimer();

        if (isCorrect)
            Debug.Log("回答正确，扣血逻辑放这里");
        else
            Debug.Log("回答错误，扣血逻辑放这里");

        // 延迟一会再出下一题
        Invoke(nameof(LoadNextQuestion), timeUpDelay);
    }

    /// <summary>
    /// 当计时器倒计时结束时，视为答错
    /// </summary>
    private void HandleTimeUp()
    {
        Debug.Log("倒计时结束，视为回答错误");
        HandleAnswerResult(false);
    }

    /// <summary>
    /// 真正的“出题 + 启动计时”封装
    /// </summary>
    private void LoadNextQuestion()
    {
        manager.LoadQuestion();
        timerManager.StartTimer();
    }
}
