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
    /// ������������Ϸ�й�����Ŀ���ɡ������߼�������������Ϸ��ʼ
    /// ��Ϸ��ʼ��HostGameManager����
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
        private bool isInitialized = false;

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

        // �¼�
        public System.Action<bool> OnGameEnded;
        public System.Action<bool> OnAnswerCompleted;

        // ����
        public bool IsMultiplayerMode => isMultiplayerMode;
        public bool IsMyTurn => isMyTurn;
        public bool IsGameStarted => gameStarted;
        public bool IsInitialized => isInitialized;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // ע�⣺��ҪDontDestroyOnLoad����Ϊ���ǳ����ض������
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // ��ʼ������������Ϸ
            InitializeComponents();
        }

        private void Start()
        {
            RegisterNetworkEvents();
            isInitialized = true;
            Debug.Log("[NQMC] ����ѳ�ʼ�����ȴ���Ϸ��ʼָ��");
        }

        private void OnDestroy()
        {
            UnregisterNetworkEvents();
            if (Instance == this)
                Instance = null;
        }

        private void InitializeComponents()
        {
            // ��ȡ����ӱ�Ҫ���
            if (timerManager == null)
                timerManager = GetComponent<TimerManager>() ?? FindObjectOfType<TimerManager>();
            if (hpManager == null)
                hpManager = GetComponent<PlayerHealthManager>() ?? FindObjectOfType<PlayerHealthManager>();

            if (timerManager == null)
            {
                Debug.LogError("[NQMC] �Ҳ���TimerManager���");
                return;
            }

            if (hpManager == null)
            {
                Debug.LogError("[NQMC] �Ҳ���PlayerHealthManager���");
                return;
            }

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

        #region �����ӿ� - ��HostGameManager����

        /// <summary>
        /// ��ʼ��Ϸ����HostGameManager���ã�
        /// </summary>
        /// <param name="multiplayerMode">�Ƿ�Ϊ����ģʽ</param>
        public void StartGame(bool multiplayerMode = false)
        {
            if (!isInitialized)
            {
                Debug.LogError("[NQMC] ���δ��ʼ�����޷���ʼ��Ϸ");
                return;
            }

            isMultiplayerMode = multiplayerMode;
            gameStarted = true;

            Debug.Log($"[NQMC] ��ʼ��Ϸ - ģʽ: {(isMultiplayerMode ? "����" : "����")}");

            if (isMultiplayerMode)
            {
                if (NetworkManager.Instance?.IsConnected == true)
                {
                    Debug.Log("[NQMC] ����ģʽ���ȴ�����������غ�");
                    // ����ģʽ�µȴ�������֪ͨ�ִ�
                    isWaitingForNetworkQuestion = true;
                }
                else
                {
                    Debug.LogError("[NQMC] δ���ӵ����������޷���ʼ������Ϸ");
                    OnGameEnded?.Invoke(false);
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
        /// ֹͣ��Ϸ����HostGameManager���ã�
        /// </summary>
        public void StopGame()
        {
            Debug.Log("[NQMC] ֹͣ��Ϸ");
            gameStarted = false;
            isMyTurn = false;
            isWaitingForNetworkQuestion = false;

            if (timerManager != null)
                timerManager.StopTimer();

            if (manager != null)
            {
                Destroy(manager.gameObject);
                manager = null;
            }
        }

        /// <summary>
        /// ��ͣ��Ϸ����HostGameManager���ã�
        /// </summary>
        public void PauseGame()
        {
            Debug.Log("[NQMC] ��ͣ��Ϸ");
            if (timerManager != null)
                timerManager.PauseTimer();
        }

        /// <summary>
        /// �ָ���Ϸ����HostGameManager���ã�
        /// </summary>
        public void ResumeGame()
        {
            Debug.Log("[NQMC] �ָ���Ϸ");
            if (timerManager != null)
                timerManager.ResumeTimer();
        }

        /// <summary>
        /// ǿ�ƿ�ʼ��һ�⣨��HostGameManager���ã�
        /// </summary>
        public void ForceNextQuestion()
        {
            if (!gameStarted)
            {
                Debug.LogWarning("[NQMC] ��Ϸδ��ʼ���޷�ǿ����һ��");
                return;
            }

            Debug.Log("[NQMC] ǿ�ƿ�ʼ��һ��");
            LoadNextQuestion();
        }

        #endregion

        #region �ڲ���Ŀ�����߼�

        private IEnumerator DelayedFirstQuestion()
        {
            yield return null;
            LoadNextQuestion();
        }

        private void LoadNextQuestion()
        {
            if (!gameStarted)
            {
                Debug.Log("[NQMC] ��Ϸ��ֹͣ������������Ŀ");
                return;
            }

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
                    Debug.Log("[NQMC] �����ҵĻغϻ�δ���ӷ��������ȴ�...");
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
                if (hpManager != null)
                    hpManager.BindManager(manager);

                manager.OnAnswerResult += HandleAnswerResult;
                StartCoroutine(DelayedLoadQuestion());
            }
            else
            {
                Debug.LogError("[NQMC] �޷�������Ŀ������");
                OnGameEnded?.Invoke(false);
            }
        }

        private void RequestNetworkQuestion()
        {
            if (NetworkManager.Instance?.IsConnected == true)
            {
                isWaitingForNetworkQuestion = true;
                NetworkManager.Instance.RequestQuestion();
                Debug.Log("[NQMC] ����������Ŀ...");
            }
            else
            {
                Debug.LogError("[NQMC] ����δ���ӣ��޷�������Ŀ");
                OnGameEnded?.Invoke(false);
            }
        }

        private void LoadNetworkQuestion(NetworkQuestionData networkQuestion)
        {
            if (networkQuestion == null)
            {
                Debug.LogError("[NQMC] ������Ŀ����Ϊ��");
                return;
            }

            currentNetworkQuestion = networkQuestion;
            manager = CreateManager(networkQuestion.questionType);

            if (manager != null)
            {
                if (hpManager != null)
                    hpManager.BindManager(manager);

                manager.OnAnswerResult += HandleNetworkAnswerResult;
                StartCoroutine(DelayedLoadQuestion());
            }
            else
            {
                Debug.LogError("[NQMC] �޷�Ϊ������Ŀ����������");
            }
        }

        private IEnumerator DelayedLoadQuestion()
        {
            yield return null;
            if (manager != null)
            {
                manager.LoadQuestion();
                if (timerManager != null)
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
                    Debug.LogError($"[NQMC] δʵ�ֵ����ͣ�{type}");
                    return null;
            }
        }

        #endregion

        #region ����������

        private void HandleAnswerResult(bool isCorrect)
        {
            Debug.Log($"[NQMC] ����ģʽ������: {isCorrect}");

            if (timerManager != null)
                timerManager.StopTimer();

            if (!isCorrect && hpManager != null)
            {
                hpManager.HPHandleAnswerResult(false);

                // ����Ƿ���Ϸ����
                if (hpManager.CurrentHealth <= 0)
                {
                    Debug.Log("[NQMC] Ѫ�����㣬��Ϸ����");
                    OnGameEnded?.Invoke(false);
                    return;
                }
            }

            OnAnswerCompleted?.Invoke(isCorrect);
            Invoke(nameof(LoadNextQuestion), timeUpDelay);
        }

        private void HandleNetworkAnswerResult(bool isCorrect)
        {
            Debug.Log($"[NQMC] ����ģʽ���ش��⣬�ȴ�������ȷ��");
            // ����ģʽ�£����ش�������ֱ�Ӵ����ȴ����������
            if (timerManager != null)
                timerManager.StopTimer();
        }

        private void HandleTimeUp()
        {
            Debug.Log("[NQMC] ���ⳬʱ");

            if (timerManager != null)
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
                else
                    Invoke(nameof(LoadNextQuestion), timeUpDelay);
            }
        }

        #endregion

        #region �����¼�����

        private void OnNetworkQuestionReceived(NetworkQuestionData question)
        {
            if (!isMultiplayerMode || !isWaitingForNetworkQuestion)
                return;

            Debug.Log($"[NQMC] �յ�������Ŀ: {question.questionType}");
            isWaitingForNetworkQuestion = false;
            LoadNetworkQuestion(question);
        }

        private void OnNetworkAnswerResult(bool isCorrect, string correctAnswer)
        {
            if (!isMultiplayerMode)
                return;

            Debug.Log($"[NQMC] �յ�������������: {(isCorrect ? "��ȷ" : "����")}");

            // ����Ѫ���仯
            if (!isCorrect && hpManager != null)
            {
                hpManager.HPHandleAnswerResult(false);

                // ����Ƿ���Ϸ����
                if (hpManager.CurrentHealth <= 0)
                {
                    Debug.Log("[NQMC] Ѫ�����㣬��Ϸ����");
                    OnGameEnded?.Invoke(false);
                    return;
                }
            }

            // ��ʾ�������
            ShowAnswerFeedback(isCorrect, correctAnswer);
            OnAnswerCompleted?.Invoke(isCorrect);
        }

        private void OnNetworkPlayerTurnChanged(ushort playerId)
        {
            if (!isMultiplayerMode)
                return;

            bool wasMyTurn = isMyTurn;
            isMyTurn = (playerId == NetworkManager.Instance?.ClientId);

            Debug.Log($"[NQMC] �غϱ��: {(isMyTurn ? "�ֵ�����" : $"�ֵ����{playerId}")}");

            if (isMyTurn && !wasMyTurn)
            {
                // �ֵ��Ҵ���
                LoadNextQuestion();
            }
            else if (!isMyTurn && wasMyTurn)
            {
                // �������ҵĻغϣ�ֹͣ��ǰ��Ŀ
                if (timerManager != null)
                    timerManager.StopTimer();
            }
        }

        private void OnNetworkDisconnected()
        {
            if (isMultiplayerMode && gameStarted)
            {
                Debug.LogWarning("[NQMC] ����Ͽ�����Ϸ������");

                if (timerManager != null)
                    timerManager.StopTimer();

                OnGameEnded?.Invoke(false);
            }
        }

        #endregion

        #region ��Ŀ�������ӿ�

        /// <summary>
        /// �ύ�𰸣�����Ŀ���������ã�
        /// </summary>
        public void SubmitAnswer(string answer)
        {
            if (isMultiplayerMode && isMyTurn && NetworkManager.Instance?.IsConnected == true)
            {
                NetworkManager.Instance.SubmitAnswer(answer);
                Debug.Log($"[NQMC] �ύ�����: {answer}");
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
            Debug.Log($"[NQMC] ���ⷴ��: {feedback}");

            // �������ͨ��UIϵͳ��ʾ����
            // �����õ�ǰ����Ŀ��������ʾ����
            if (manager != null)
            {
                // �����Ŀ������֧����ʾ�����������Ե�����Ӧ����
                // ����: manager.ShowNetworkResult(isCorrect, correctAnswer);
            }
        }

        #endregion

        #region ���Է���

        /// <summary>
        /// ��ȡ��ǰ״̬��Ϣ�������ã�
        /// </summary>
        public string GetStatusInfo()
        {
            var info = "=== NQMC ״̬ ===\n";
            info += $"�ѳ�ʼ��: {isInitialized}\n";
            info += $"��Ϸ�ѿ�ʼ: {gameStarted}\n";
            info += $"����ģʽ: {isMultiplayerMode}\n";
            info += $"�ҵĻغ�: {isMyTurn}\n";
            info += $"�ȴ�������Ŀ: {isWaitingForNetworkQuestion}\n";
            info += $"��ǰ������: {(manager != null ? manager.GetType().Name : "��")}\n";

            if (hpManager != null)
                info += $"��ǰѪ��: {hpManager.CurrentHealth}\n";

            if (timerManager != null)
                info += $"��ʱ��״̬: {(timerManager.IsRunning ? "������" : "��ֹͣ")}\n";

            return info;
        }

        #endregion
    }
}