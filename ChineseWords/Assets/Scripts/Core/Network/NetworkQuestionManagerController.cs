using UnityEngine;
using Core;
using Core.Network;
using GameLogic;
using GameLogic.FillBlank;
using GameLogic.TorF;
using GameLogic.Choice;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Managers;

namespace Core.Network
{
    /// <summary>
    /// ���绯��Ŀ���������
    /// ��չԭ�е�QuestionManagerController��֧�ֵ����Ͷ���ģʽ
    /// </summary>
    public class NetworkQuestionManagerController : MonoBehaviour
    {
        [Header("��Ϸ���������")]
        [SerializeField] private TimerManager timerManager;
        [SerializeField] private PlayerHealthManager hpManager;

        [Header("��Ϸ����")]
        [SerializeField] private float timeUpDelay = 1f;
        [SerializeField] private bool isMultiplayerMode = false;

        public static NetworkQuestionManagerController Instance { get; private set; }

        // ��ǰ״̬
        private QuestionManagerBase manager;
        private NetworkQuestionData currentNetworkQuestion;
        private bool isMyTurn = false;
        private bool isWaitingForNetworkQuestion = false;
        private bool gameStarted = false;

        // ��Ŀ����Ȩ�أ���ԭQMC����һ�£�
        public Dictionary<QuestionType, float> TypeWeights = new Dictionary<QuestionType, float>()
        {
            //{ QuestionType.HandWriting, 0.5f },
            { QuestionType.IdiomChain, 1f },
            { QuestionType.TextPinyin, 1f },
            { QuestionType.HardFill, 1f },
            { QuestionType.SoftFill, 1f },
            //{ QuestionType.AbbrFill, 1f },
            { QuestionType.SentimentTorF, 1f },
            { QuestionType.SimularWordChoice, 1f },
            { QuestionType.UsageTorF, 1f },
            { QuestionType.ExplanationChoice, 1f },
        };

        // ����
        public bool IsMultiplayerMode => isMultiplayerMode;
        public bool IsMyTurn => isMyTurn;
        public bool IsGameStarted => gameStarted;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            InitializeComponents();
            RegisterNetworkEvents();
        }

        private void OnDestroy()
        {
            UnregisterNetworkEvents();
        }

        private void InitializeComponents()
        {
            // ��ȡ����ӱ�Ҫ���
            if (timerManager == null)
                timerManager = GetComponent<TimerManager>() ?? gameObject.AddComponent<TimerManager>();
            if (hpManager == null)
                hpManager = GetComponent<PlayerHealthManager>() ?? gameObject.AddComponent<PlayerHealthManager>();

            // Ӧ������
            var cfg = ConfigManager.Instance?.Config;
            if (cfg != null)
            {
                timerManager.ApplyConfig(cfg.timeLimit);
                hpManager.ApplyConfig(cfg.initialHealth, cfg.damagePerWrong);
            }

            // �󶨼�ʱ���¼�
            timerManager.OnTimeUp += HandleTimeUp;
        }

        private void RegisterNetworkEvents()
        {
            NetworkManager.OnQuestionReceived += OnNetworkQuestionReceived;
            NetworkManager.OnAnswerResultReceived += OnNetworkAnswerResult;
            NetworkManager.OnPlayerTurnChanged += OnNetworkPlayerTurnChanged;
            NetworkManager.OnDisconnected += OnNetworkDisconnected;
        }

        private void UnregisterNetworkEvents()
        {
            NetworkManager.OnQuestionReceived -= OnNetworkQuestionReceived;
            NetworkManager.OnAnswerResultReceived -= OnNetworkAnswerResult;
            NetworkManager.OnPlayerTurnChanged -= OnNetworkPlayerTurnChanged;
            NetworkManager.OnDisconnected -= OnNetworkDisconnected;
        }

        /// <summary>
        /// ��ʼ��Ϸ
        /// </summary>
        /// <param name="multiplayerMode">�Ƿ�Ϊ����ģʽ</param>
        public void StartGame(bool multiplayerMode = false)
        {
            isMultiplayerMode = multiplayerMode;
            gameStarted = true;

            Debug.Log($"��ʼ��Ϸ - ģʽ: {(isMultiplayerMode ? "����" : "����")}");

            if (isMultiplayerMode)
            {
                if (NetworkManager.Instance?.IsConnected == true)
                {
                    Debug.Log("����ģʽ���ȴ�����������غ�");
                    // ����ģʽ�µȴ�������֪ͨ�ִ�
                    isWaitingForNetworkQuestion = true;
                }
                else
                {
                    Debug.LogError("δ���ӵ����������޷���ʼ������Ϸ");
                    return;
                }
            }
            else
            {
                // ����ģʽ��������ʼ��һ��
                isMyTurn = true;
                StartCoroutine(DelayedFirstQuestion());
            }
        }

        /// <summary>
        /// ֹͣ��Ϸ
        /// </summary>
        public void StopGame()
        {
            gameStarted = false;
            isMyTurn = false;
            isWaitingForNetworkQuestion = false;

            timerManager.StopTimer();

            if (manager != null)
            {
                Destroy(manager.gameObject);
                manager = null;
            }

            Debug.Log("��Ϸ��ֹͣ");
        }

        private IEnumerator DelayedFirstQuestion()
        {
            yield return null;
            LoadNextQuestion();
        }

        private void LoadNextQuestion()
        {
            if (!gameStarted)
                return;

            // ����ǰ��Ŀ������
            if (manager != null)
            {
                Destroy(manager.gameObject);
                manager = null;
            }

            if (isMultiplayerMode)
            {
                if (isMyTurn && NetworkManager.Instance?.IsConnected == true)
                {
                    // ����ģʽ�����������������Ŀ
                    RequestNetworkQuestion();
                }
                else
                {
                    Debug.Log("�����ҵĻغϻ�δ���ӷ��������ȴ�...");
                }
            }
            else
            {
                // ����ģʽ��ʹ��ԭ���߼�
                LoadLocalQuestion();
            }
        }

        private void LoadLocalQuestion()
        {
            var selectedType = SelectRandomTypeByWeight();
            manager = CreateManager(selectedType);

            if (manager != null)
            {
                hpManager.BindManager(manager);
                manager.OnAnswerResult += HandleAnswerResult;
                StartCoroutine(DelayedLoadQuestion());
            }
        }

        private void RequestNetworkQuestion()
        {
            if (NetworkManager.Instance?.IsConnected == true)
            {
                isWaitingForNetworkQuestion = true;
                NetworkManager.Instance.RequestQuestion();
                Debug.Log("����������Ŀ...");
            }
        }

        private void LoadNetworkQuestion(NetworkQuestionData networkQuestion)
        {
            if (networkQuestion == null)
                return;

            currentNetworkQuestion = networkQuestion;
            manager = CreateManager(networkQuestion.questionType);

            if (manager != null)
            {
                hpManager.BindManager(manager);
                manager.OnAnswerResult += HandleNetworkAnswerResult;

                // ����ģʽ�£��𰸼���ɷ������������������޸Ļص�
                StartCoroutine(DelayedLoadQuestion());
            }
        }

        private IEnumerator DelayedLoadQuestion()
        {
            yield return null;
            if (manager != null)
            {
                manager.LoadQuestion();
                timerManager.StartTimer();
            }
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
                    Debug.LogError("δʵ�ֵ����ͣ�" + type);
                    return null;
            }
        }

        #region ����������

        private void HandleAnswerResult(bool isCorrect)
        {
            Debug.Log($"[NetworkQMC] ����ģʽ������: {isCorrect}");
            timerManager.StopTimer();

            if (!isCorrect)
                hpManager.HPHandleAnswerResult(false);

            Invoke(nameof(LoadNextQuestion), timeUpDelay);
        }

        private void HandleNetworkAnswerResult(bool isCorrect)
        {
            Debug.Log($"[NetworkQMC] ����ģʽ���ش��⣬�ȴ�������ȷ��");
            // ����ģʽ�£����ش�������ֱ�Ӵ����ȴ����������
            timerManager.StopTimer();
        }

        private void HandleTimeUp()
        {
            Debug.Log("[NetworkQMC] ���ⳬʱ");
            timerManager.StopTimer();

            if (isMultiplayerMode)
            {
                // ����ģʽ���ύ�մ𰸱�ʾ��ʱ
                if (isMyTurn && NetworkManager.Instance?.IsConnected == true)
                {
                    NetworkManager.Instance.SubmitAnswer("");
                }
            }
            else
            {
                // ����ģʽ��ֱ�Ӵ���ʱ
                if (manager != null)
                    manager.OnAnswerResult?.Invoke(false);
                Invoke(nameof(LoadNextQuestion), timeUpDelay);
            }
        }

        #endregion

        #region �����¼�����

        private void OnNetworkQuestionReceived(NetworkQuestionData question)
        {
            if (!isMultiplayerMode || !isWaitingForNetworkQuestion)
                return;

            Debug.Log($"�յ�������Ŀ: {question.questionType}");
            isWaitingForNetworkQuestion = false;
            LoadNetworkQuestion(question);
        }

        private void OnNetworkAnswerResult(bool isCorrect, string correctAnswer)
        {
            if (!isMultiplayerMode)
                return;

            Debug.Log($"�յ�������������: {(isCorrect ? "��ȷ" : "����")}");

            // ����Ѫ���仯
            if (!isCorrect)
                hpManager.HPHandleAnswerResult(false);

            // ��ʾ�������
            ShowAnswerFeedback(isCorrect, correctAnswer);
        }

        private void OnNetworkPlayerTurnChanged(ushort playerId)
        {
            if (!isMultiplayerMode)
                return;

            bool wasMyTurn = isMyTurn;
            isMyTurn = (playerId == NetworkManager.Instance?.ClientId);

            Debug.Log($"�غϱ��: {(isMyTurn ? "�ֵ�����" : $"�ֵ����{playerId}")}");

            if (isMyTurn && !wasMyTurn)
            {
                // �ֵ��Ҵ���
                LoadNextQuestion();
            }
            else if (!isMyTurn && wasMyTurn)
            {
                // �������ҵĻغϣ�ֹͣ��ǰ��Ŀ
                timerManager.StopTimer();
            }
        }

        private void OnNetworkDisconnected()
        {
            if (isMultiplayerMode && gameStarted)
            {
                Debug.LogWarning("����Ͽ�����Ϸ����ͣ");
                timerManager.StopTimer();

                // ����ѡ���л�������ģʽ������ͣ��Ϸ
                // ������ͣ��Ϸ���ȴ��û�ѡ��
                StopGame();
            }
        }

        #endregion

        #region �����ӿ�

        /// <summary>
        /// �ύ�𰸣�����Ŀ���������ã�
        /// </summary>
        public void SubmitAnswer(string answer)
        {
            if (isMultiplayerMode && isMyTurn && NetworkManager.Instance?.IsConnected == true)
            {
                NetworkManager.Instance.SubmitAnswer(answer);
                Debug.Log($"�ύ�����: {answer}");
            }
            else if (!isMultiplayerMode && manager != null)
            {
                manager.CheckAnswer(answer);
            }
        }

        /// <summary>
        /// ��ȡ��ǰ��Ŀ���������ݣ�����Ŀ������ʹ�ã�
        /// </summary>
        public NetworkQuestionData GetCurrentNetworkQuestion()
        {
            return currentNetworkQuestion;
        }

        #endregion

        #region ��������

        private void ShowAnswerFeedback(bool isCorrect, string correctAnswer)
        {
            string feedback = isCorrect ? "�ش���ȷ��" : $"�ش������ȷ���ǣ�{correctAnswer}";
            Debug.Log($"���ⷴ��: {feedback}");

            // �������ͨ��UIϵͳ��ʾ����
            // �����õ�ǰ����Ŀ��������ʾ����
        }

        #endregion
    }
}