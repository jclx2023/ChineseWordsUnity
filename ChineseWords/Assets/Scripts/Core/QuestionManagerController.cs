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
/// 问题管理控制器：负责按权重选题、加载题型管理器、管理倒计时与血量交互，
/// 并支持网络广播题目与接收远程判题结果。
/// </summary>
public class QuestionManagerController : MonoBehaviour
{
    [Header("网络传输组件，可拖入 PhotonTransport/CustomTransport，否则使用 SimulatedTransport")]
    public MonoBehaviour TransportComponent;

    [Header("远端玩家血量管理")]
    public RemoteHealthManager remoteHealthManager;

    private INetworkTransport _transport;
    private string _localPlayerId;

    private QuestionManagerBase manager;
    private TimerManager timerManager;
    [SerializeField] private PlayerHealthManager hpManager;

    [Header("超时后延迟下一题时长")]
    [Tooltip("答题超时或回答后，等待多长时间再出下一题")]
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
        // 唯一标识本机玩家
        _localPlayerId = SystemInfo.deviceUniqueIdentifier;
        Debug.Log($"[QMC][Awake] LocalPlayerId={_localPlayerId}");

        // 注入或创建 Transport
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
        // --- 网络初始化 ---
        //_transport.Initialize();
        _transport.OnConnected += () => Debug.Log("[QMC] Transport connected");
        _transport.OnError += ex => Debug.LogError($"[QMC] Transport error: {ex}");
        _transport.OnMessageReceived += OnNetworkMessageReceived;

        // --- 本地逻辑初始化 ---
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
    /// 处理收到的网络消息
    /// </summary>
    private void OnNetworkMessageReceived(NetworkMessage msg)
    {
        Debug.Log($"[QMC][NetRecv] EventType={msg.EventType}, Sender={msg.SenderId}");
        if (msg.SenderId == _localPlayerId)
            return; // 忽略自己的回声

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
    /// Dispatcher 调用：处理远程判题结果
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
    /// 加载下一题：本地选题并广播给其他客户端
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

        // 广播题目
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
    /// 本地答题结果处理：停止计时、扣血、发送结果、延时出下一题
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
    /// 处理远程判题结果（复用本地逻辑）
    /// </summary>
    private void HandleRemoteAnswerResult(bool isCorrect)
    {
        Debug.Log($"[QMC] HandleRemoteAnswerResult isCorrect={isCorrect}");

        // 1. 更新远端血量
        remoteHealthManager.HandleRemoteAnswerResult(isCorrect);

        // 2. 不再更新本地血量
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
                Debug.LogError($"未实现题型：{type}");
                return null;
        }
    }
}