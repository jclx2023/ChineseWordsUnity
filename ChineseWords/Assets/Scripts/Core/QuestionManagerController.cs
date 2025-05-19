using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Core;
using GameLogic;
using GameLogic.FillBlank;
using GameLogic.TorF;
using GameLogic.Choice;

/// <summary>
/// ������������������Ȩ��ѡ�⡢�������͹�������������ʱ��Ѫ��������
/// ��֧������㲥��Ŀ�����Զ����������
/// </summary>
public class QuestionManagerController : MonoBehaviour
{
    [Header("���紫������������� PhotonTransport/CustomTransport������ʹ�� SimulatedTransport")]
    public MonoBehaviour TransportComponent;

    [Header("Զ�����Ѫ������")]
    public RemoteHealthManager remoteHealthManager;

    private INetworkTransport _transport;
    private string _localPlayerId;

    private QuestionManagerBase manager;
    private TimerManager timerManager;
    [SerializeField] private PlayerHealthManager hpManager;

    [Header("��ʱ���ӳ���һ��ʱ��")]
    [Tooltip("���ⳬʱ��ش�󣬵ȴ��೤ʱ���ٳ���һ��")]
    public float timeUpDelay = 1f;

    public string LocalPlayerId => _localPlayerId;


    public Dictionary<QuestionType, float> TypeWeights = new Dictionary<QuestionType, float>()
    {
        { QuestionType.IdiomChain, 1f },
        { QuestionType.TextPinyin, 1f },
        { QuestionType.HardFill, 1f },
        { QuestionType.SoftFill, 1f },
        { QuestionType.SentimentTorF, 1f },
        { QuestionType.SimularWordChoice, 1f },
        { QuestionType.UsageTorF, 1f },
        { QuestionType.ExplanationChoice, 1f },
    };

    void Awake()
    {
        // Ψһ��ʶ�������
        _localPlayerId = SystemInfo.deviceUniqueIdentifier;
        Debug.Log($"[QMC][Awake] LocalPlayerId={_localPlayerId}");

        // ע��򴴽� Transport
        if (TransportComponent is INetworkTransport custom)
        {
            _transport = custom;
            Debug.Log($"[QMC] Using injected transport: {TransportComponent.GetType().Name}");
        }
        else
        {
            _transport = new SimulatedTransport();
            Debug.Log("[QMC] No transport injected, created SimulatedTransport");
        }
    }

    void Start()
    {
        // --- �����ʼ�� ---
        //_transport.Initialize();
        _transport.OnConnected += () => Debug.Log("[QMC] Transport connected");
        _transport.OnError += ex => Debug.LogError($"[QMC] Transport error: {ex}");
        _transport.OnMessageReceived += OnNetworkMessageReceived;

        // --- �����߼���ʼ�� ---
        timerManager = GetComponent<TimerManager>();
        hpManager = GetComponent<PlayerHealthManager>();
        var cfg = ConfigManager.Instance.Config;

        timerManager.ApplyConfig(cfg.timeLimit);
        hpManager.ApplyConfig(cfg.initialHealth, cfg.damagePerWrong);
        int initHp = ConfigManager.Instance.Config.initialHealth;
        remoteHealthManager.Initialize(initHp);
        timerManager.OnTimeUp += HandleTimeUp;

        StartCoroutine(DelayedFirstQuestion());
    }

    void OnDestroy()
    {
        _transport.Shutdown();
    }

    /// <summary>
    /// �����յ���������Ϣ
    /// </summary>
    private void OnNetworkMessageReceived(NetworkMessage msg)
    {
        Debug.Log($"[QMC][NetRecv] EventType={msg.EventType}, Sender={msg.SenderId}");
        if (msg.SenderId == _localPlayerId)
            return; // �����Լ��Ļ���

        switch (msg.EventType)
        {
            case NetworkEventType.BroadcastQuestion:
                Debug.Log("[QMC][NetRecv] BroadcastQuestion received");
                LoadNextQuestion();
                break;

            case NetworkEventType.AnswerResult:
                bool isCorrect = false;
                try { isCorrect = (bool)msg.Payload; }
                catch { Debug.LogWarning("[QMC] Cannot parse Payload as bool"); }
                Debug.Log($"[QMC][NetRecv] AnswerResult received isCorrect={isCorrect}");
                HandleRemoteAnswerResult(isCorrect);
                break;

            default:
                Debug.LogWarning($"[QMC][NetRecv] Unhandled EventType={msg.EventType}");
                break;
        }
    }

    /// <summary>
    /// Dispatcher ���ã�����Զ��������
    /// </summary>
    public void OnRemoteAnswerResult(bool isCorrect)
    {
        Debug.Log($"[QMC] OnRemoteAnswerResult invoked isCorrect={isCorrect}");
        HandleRemoteAnswerResult(isCorrect);
    }

    private IEnumerator DelayedFirstQuestion()
    {
        yield return null;
        LoadNextQuestion();
    }

    /// <summary>
    /// ������һ�⣺����ѡ�Ⲣ�㲥�������ͻ���
    /// </summary>
    public void LoadNextQuestion()
    {
        if (manager != null)
            Destroy(manager);

        var selectedType = SelectRandomTypeByWeight();
        manager = CreateManager(selectedType);
        hpManager.BindManager(manager);
        manager.OnAnswerResult += HandleAnswerResult;

        StartCoroutine(DelayedLoadAndBroadcast());
    }

    private IEnumerator DelayedLoadAndBroadcast()
    {
        yield return null;
        manager.LoadQuestion();
        timerManager.StartTimer();

        // �㲥��Ŀ
        var msg = new NetworkMessage
        {
            EventType = NetworkEventType.BroadcastQuestion,
            SenderId = _localPlayerId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Payload = null
        };
        Debug.Log("[QMC][NetSend] Broadcasting question");
        _transport.SendMessage(msg);
    }

    /// <summary>
    /// ���ش���������ֹͣ��ʱ����Ѫ�����ͽ������ʱ����һ��
    /// </summary>
    private void HandleAnswerResult(bool isCorrect)
    {
        Debug.Log($"[QMC] HandleAnswerResult isCorrect={isCorrect}");
        timerManager.StopTimer();
        if (!isCorrect)
            hpManager.HPHandleAnswerResult(false);

        var msg = new NetworkMessage
        {
            EventType = NetworkEventType.AnswerResult,
            SenderId = _localPlayerId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Payload = isCorrect
        };
        Debug.Log($"[QMC][NetSend] Sending AnswerResult isCorrect={isCorrect}");
        _transport.SendMessage(msg);

        Invoke(nameof(LoadNextQuestion), timeUpDelay);
    }

    /// <summary>
    /// ����Զ�������������ñ����߼���
    /// </summary>
    private void HandleRemoteAnswerResult(bool isCorrect)
    {
        Debug.Log($"[QMC] HandleRemoteAnswerResult isCorrect={isCorrect}");

        // 1. ����Զ��Ѫ��
        remoteHealthManager.HandleRemoteAnswerResult(isCorrect);

        // 2. ���ٸ��±���Ѫ��
        timerManager.StopTimer();
        Invoke(nameof(LoadNextQuestion), timeUpDelay);
    }



    private void HandleTimeUp()
    {
        Debug.Log("[QMC] HandleTimeUp");
        timerManager.StopTimer();
        manager.OnAnswerResult?.Invoke(false);
    }

    private QuestionType SelectRandomTypeByWeight()
    {
        float total = TypeWeights.Values.Sum();
        float r = UnityEngine.Random.Range(0f, total);
        float acc = 0f;
        foreach (var kv in TypeWeights)
        {
            acc += kv.Value;
            if (r <= acc)
                return kv.Key;
        }
        return TypeWeights.Keys.First();
    }

    private QuestionManagerBase CreateManager(QuestionType type)
    {
        switch (type)
        {
            case QuestionType.IdiomChain: return gameObject.AddComponent<IdiomChainQuestionManager>();
            case QuestionType.TextPinyin: return gameObject.AddComponent<TextPinyinQuestionManager>();
            case QuestionType.HardFill: return gameObject.AddComponent<HardFillQuestionManager>();
            case QuestionType.SoftFill: return gameObject.AddComponent<SoftFillQuestionManager>();
            case QuestionType.SentimentTorF: return gameObject.AddComponent<SentimentTorFQuestionManager>();
            case QuestionType.SimularWordChoice: return gameObject.AddComponent<SimularWordChoiceQuestionManager>();
            case QuestionType.UsageTorF: return gameObject.AddComponent<UsageTorFQuestionManager>();
            case QuestionType.ExplanationChoice: return gameObject.AddComponent<ExplanationChoiceQuestionManager>();
            default:
                Debug.LogError($"δʵ�����ͣ�{type}");
                return null;
        }
    }
}