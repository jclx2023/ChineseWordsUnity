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
    [SerializeField] private PlayerHealthManager hpManager;

    [Header("��ʱ���ӳ���һ��ʱ��")]
    [Tooltip("���ⳬʱ��ش�󣬵ȴ��೤ʱ���ٳ���һ��")]
    public float timeUpDelay = 1f;

    void Start()
    {
        Debug.Log("QuestionManagerController ������SelectedType = " + GameManager.Instance.SelectedType);

        // 1. ����������Ӷ�Ӧ�� QuestionManager
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
            case QuestionType.UsageTorF:
                manager = gameObject.AddComponent<UsageTorFQuestionManager>();
                break;
            case QuestionType.ExplanationChoice:
                manager = gameObject.AddComponent<ExplanationChoiceQuestionManager>();
                break;
            default:
                Debug.LogError("δʵ�ֵ����ͣ�" + GameManager.Instance.SelectedType);
                return;
        }

        // 2. ���Ĵ�����
        manager.OnAnswerResult += HandleAnswerResult;

        // 3. ��ȡ������ TimerManager �ĳ�ʱ�¼�
        hpManager = GetComponent<PlayerHealthManager>();
        timerManager = GetComponent<TimerManager>();
        timerManager.OnTimeUp += HandleTimeUp;            // �� ���ĳ�ʱ

        StartCoroutine(DelayedFirstQuestion());
    }
    private IEnumerator DelayedFirstQuestion()
    {
        yield return null;
        LoadNextQuestion();
    }

    /// <summary>
    /// ͳһ�����û����𣨻�ʱ���Ľ��
    /// </summary>
    private void HandleAnswerResult(bool isCorrect)
    {
        Debug.Log($"[QMC] HandleAnswerResult �յ� isCorrect={isCorrect}");
        timerManager.StopTimer();
        if (!isCorrect) hpManager.HPHandleAnswerResult(false);
        Invoke(nameof(LoadNextQuestion), timeUpDelay);
    }


    /// <summary>
    /// ����ʱ������ʱ����ʱ����Ϊ���
    /// </summary>
    private void HandleTimeUp()
    {
        timerManager.StopTimer();

        manager.OnAnswerResult?.Invoke(false);           // �ߴ�������ͳһͨ��

        Invoke(nameof(LoadNextQuestion), timeUpDelay);
    }

    /// <summary>
    /// �����ġ����� + ������ʱ����װ
    /// </summary>
    private void LoadNextQuestion()
    {
        manager.LoadQuestion();
        timerManager.StartTimer();
    }
}
