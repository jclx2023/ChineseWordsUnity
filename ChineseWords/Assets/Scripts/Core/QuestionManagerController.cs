using UnityEngine;
using Core;
using GameLogic;
using GameLogic.FillBlank;
using GameLogic.TorF;
using GameLogic.Choice;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class QuestionManagerController : MonoBehaviour
{
    private QuestionManagerBase manager;
    private TimerManager timerManager;
    [SerializeField] private PlayerHealthManager hpManager;

    [Header("超时后延迟下一题时长")]
    [Tooltip("答题超时或回答后，等待多长时间再出下一题")]
    public float timeUpDelay = 1f;

    void Start()
    {
        timerManager = GetComponent<TimerManager>();
        hpManager = GetComponent<PlayerHealthManager>();

        var cfg = ConfigManager.Instance.Config;

        timerManager.ApplyConfig(cfg.timeLimit);
        hpManager.ApplyConfig(cfg.initialHealth, cfg.damagePerWrong);

        timerManager.OnTimeUp += HandleTimeUp;
        StartCoroutine(DelayedFirstQuestion());
    }


    private IEnumerator DelayedFirstQuestion()
    {
        yield return null;
        LoadNextQuestion();
    }

    private void LoadNextQuestion()
    {
        if (manager != null)
            Destroy(manager);

        var selectedType = SelectRandomTypeByWeight();
        manager = CreateManager(selectedType);
        hpManager.BindManager(manager); // 绑定血量处理

        manager.OnAnswerResult += HandleAnswerResult;

        StartCoroutine(DelayedLoadQuestion());
    }

    private IEnumerator DelayedLoadQuestion()
    {
        yield return null;
        manager.LoadQuestion();
        timerManager.StartTimer();
    }


    private void HandleAnswerResult(bool isCorrect)
    {
        Debug.Log($"[QMC] HandleAnswerResult 收到 isCorrect={isCorrect}");
        timerManager.StopTimer();
        if (!isCorrect) hpManager.HPHandleAnswerResult(false);
        Invoke(nameof(LoadNextQuestion), timeUpDelay);
    }

    private void HandleTimeUp()
    {
        timerManager.StopTimer();
        manager.OnAnswerResult?.Invoke(false);
        Invoke(nameof(LoadNextQuestion), timeUpDelay);
    }

    private QuestionType SelectRandomTypeByWeight()
    {
        var typeWeights = TypeWeights;
        float total = typeWeights.Values.Sum();
        float r = Random.Range(0, total);
        float acc = 0f;
        foreach (var pair in typeWeights)
        {
            acc += pair.Value;
            if (r <= acc)
                return pair.Key;
        }
        return typeWeights.Keys.First();
    }

    private QuestionManagerBase CreateManager(QuestionType type)
    {
        switch (type)
        {
            //case QuestionType.HandWriting: return gameObject.AddComponent<HandWritingQuestionManager>();
            case QuestionType.IdiomChain: return gameObject.AddComponent<IdiomChainQuestionManager>();
            case QuestionType.TextPinyin: return gameObject.AddComponent<TextPinyinQuestionManager>();
            case QuestionType.HardFill: return gameObject.AddComponent<HardFillQuestionManager>();
            case QuestionType.SoftFill: return gameObject.AddComponent<SoftFillQuestionManager>();
            //case QuestionType.AbbrFill: return gameObject.AddComponent<AbbrFillQuestionManager>();
            case QuestionType.SentimentTorF: return gameObject.AddComponent<SentimentTorFQuestionManager>();
            case QuestionType.SimularWordChoice: return gameObject.AddComponent<SimularWordChoiceQuestionManager>();
            case QuestionType.UsageTorF: return gameObject.AddComponent<UsageTorFQuestionManager>();
            case QuestionType.ExplanationChoice: return gameObject.AddComponent<ExplanationChoiceQuestionManager>();
            default:
                Debug.LogError("未实现的题型：" + type);
                return null;
        }
    }
    public Dictionary<QuestionType, float> TypeWeights = new Dictionary<QuestionType, float>()
        {
            //{ QuestionType.HandWriting, 0.5f },
            { QuestionType.IdiomChain, 10f },
            { QuestionType.TextPinyin, 1f },
            { QuestionType.HardFill, 1f },
            { QuestionType.SoftFill, 1f },
            //{ QuestionType.AbbrFill, 1f },
            { QuestionType.SentimentTorF, 1f },
            { QuestionType.SimularWordChoice, 1f },
            { QuestionType.UsageTorF, 1f },
            { QuestionType.ExplanationChoice, 1f },
        };
}
