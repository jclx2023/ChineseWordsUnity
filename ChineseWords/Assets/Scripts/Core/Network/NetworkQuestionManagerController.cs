using UnityEngine;
using Core;
using Core.Network;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Managers;

namespace Core.Network
{
    /// <summary>
    /// �Ż����������Ŀ���������
    /// רע�ڣ��ͻ�����Ŀ��ʾ + ������Ϣ���� + ���ύ
    /// �Ƴ��ˣ���Ŀ�����߼�����Host����+ �ظ��Ĺ����������߼�
    /// </summary>
    public class NetworkQuestionManagerController : MonoBehaviour
    {
        [Header("��Ϸ���������")]
        [SerializeField] private TimerManager timerManager;
        [SerializeField] private PlayerHealthManager hpManager;

        [Header("��Ϸ����")]
        [SerializeField] private float timeUpDelay = 1f;
        [SerializeField] private bool isMultiplayerMode = false;

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;

        public static NetworkQuestionManagerController Instance { get; private set; }

        // ��ǰ״̬
        private QuestionManagerBase currentManager;
        private NetworkQuestionData currentNetworkQuestion;
        private bool isMyTurn = false;
        private bool isWaitingForNetworkQuestion = false;
        private bool gameStarted = false;
        private bool isInitialized = false;

        // ��Ŀ����Ȩ�أ���Hostʹ�ã�
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

        // �¼�
        public System.Action<bool> OnGameEnded;
        public System.Action<bool> OnAnswerCompleted;

        // ����
        public bool IsMultiplayerMode => isMultiplayerMode;
        public bool IsMyTurn => isMyTurn;
        public bool IsGameStarted => gameStarted;
        public bool IsInitialized => isInitialized;
        public QuestionManagerBase CurrentManager => currentManager;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                LogDebug("NetworkQuestionManagerController �����Ѵ���");
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // ��ʼ���������������Ϸ
            InitializeComponents();
        }

        private void Start()
        {
            RegisterNetworkEvents();
            isInitialized = true;
            LogDebug("����ѳ�ʼ�����ȴ���Ϸ��ʼָ��");
        }

        private void OnDestroy()
        {
            UnregisterNetworkEvents();
            CleanupCurrentManager();

            if (Instance == this)
                Instance = null;

            LogDebug("NetworkQuestionManagerController ������");
        }

        /// <summary>
        /// ��ʼ���������
        /// </summary>
        private void InitializeComponents()
        {
            LogDebug("��ʼ���������...");

            // ��ȡ����ұ�Ҫ���
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

            LogDebug("���������ʼ�����");
        }

        /// <summary>
        /// ע�������¼�
        /// </summary>
        private void RegisterNetworkEvents()
        {
            NetworkManager.OnQuestionReceived += OnNetworkQuestionReceived;
            NetworkManager.OnAnswerResultReceived += OnNetworkAnswerResult;
            NetworkManager.OnPlayerTurnChanged += OnNetworkPlayerTurnChanged;
            NetworkManager.OnDisconnected += OnNetworkDisconnected;

            LogDebug("�����¼���ע��");
        }

        /// <summary>
        /// ȡ��ע�������¼�
        /// </summary>
        private void UnregisterNetworkEvents()
        {
            NetworkManager.OnQuestionReceived -= OnNetworkQuestionReceived;
            NetworkManager.OnAnswerResultReceived -= OnNetworkAnswerResult;
            NetworkManager.OnPlayerTurnChanged -= OnNetworkPlayerTurnChanged;
            NetworkManager.OnDisconnected -= OnNetworkDisconnected;

            LogDebug("�����¼���ȡ��ע��");
        }

        #region �����ӿ� - ���ⲿϵͳ����

        /// <summary>
        /// ��ʼ��Ϸ�����ⲿϵͳ���ã�
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

            LogDebug($"��ʼ��Ϸ - ģʽ: {(isMultiplayerMode ? "����" : "����")}");

            if (isMultiplayerMode)
            {
                StartMultiplayerGame();
            }
            else
            {
                StartSinglePlayerGame();
            }
        }

        /// <summary>
        /// ֹͣ��Ϸ�����ⲿϵͳ���ã�
        /// </summary>
        public void StopGame()
        {
            LogDebug("ֹͣ��Ϸ");
            gameStarted = false;
            isMyTurn = false;
            isWaitingForNetworkQuestion = false;

            StopTimer();
            CleanupCurrentManager();
        }

        /// <summary>
        /// ��ͣ��Ϸ�����ⲿϵͳ���ã�
        /// </summary>
        public void PauseGame()
        {
            LogDebug("��ͣ��Ϸ");
            if (timerManager != null)
                timerManager.PauseTimer();
        }

        /// <summary>
        /// �ָ���Ϸ�����ⲿϵͳ���ã�
        /// </summary>
        public void ResumeGame()
        {
            LogDebug("�ָ���Ϸ");
            if (timerManager != null)
                timerManager.ResumeTimer();
        }

        /// <summary>
        /// ǿ�ƿ�ʼ��һ�⣨���ⲿϵͳ���ã�
        /// </summary>
        public void ForceNextQuestion()
        {
            if (!gameStarted)
            {
                LogDebug("��Ϸδ��ʼ���޷�ǿ����һ��");
                return;
            }

            LogDebug("ǿ�ƿ�ʼ��һ��");

            if (isMultiplayerMode)
            {
                // ����ģʽ�£�ֻ���ֵ��Լ�����ǿ����һ��
                if (isMyTurn)
                {
                    RequestNetworkQuestion();
                }
                else
                {
                    LogDebug("�����ҵĻغϣ��޷�ǿ����һ��");
                }
            }
            else
            {
                // ����ģʽֱ�Ӽ�����һ��
                LoadNextLocalQuestion();
            }
        }

        #endregion

        #region ��Ϸģʽ����

        /// <summary>
        /// ��ʼ������Ϸ
        /// </summary>
        private void StartMultiplayerGame()
        {
            if (NetworkManager.Instance?.IsConnected == true)
            {
                LogDebug("����ģʽ���ȴ�����������غ�");
                isWaitingForNetworkQuestion = true;
            }
            else
            {
                Debug.LogError("[NQMC] δ���ӵ����������޷���ʼ������Ϸ");
                OnGameEnded?.Invoke(false);
            }
        }

        /// <summary>
        /// ��ʼ������Ϸ
        /// </summary>
        private void StartSinglePlayerGame()
        {
            LogDebug("����ģʽ��������ʼ��һ��");
            isMyTurn = true;
            StartCoroutine(DelayedFirstQuestion());
        }

        /// <summary>
        /// �ӳٿ�ʼ��һ��
        /// </summary>
        private IEnumerator DelayedFirstQuestion()
        {
            yield return null;
            LoadNextLocalQuestion();
        }

        #endregion

        #region ��Ŀ���� - �򻯰汾

        /// <summary>
        /// ������һ��������Ŀ������ģʽ��
        /// </summary>
        private void LoadNextLocalQuestion()
        {
            if (!gameStarted)
            {
                LogDebug("��Ϸ��ֹͣ������������Ŀ");
                return;
            }

            LogDebug("���ر�����Ŀ...");

            // ����ǰ������
            CleanupCurrentManager();

            // ѡ����Ŀ���Ͳ�����������
            var selectedType = SelectRandomTypeByWeight();
            currentManager = CreateQuestionManager(selectedType, false);

            if (currentManager != null)
            {
                // �󶨹�������Ѫ��ϵͳ
                if (hpManager != null)
                    hpManager.BindManager(currentManager);

                // �󶨴𰸽���¼�
                currentManager.OnAnswerResult += HandleLocalAnswerResult;

                // �ӳټ�����Ŀ
                StartCoroutine(DelayedLoadQuestion());

                LogDebug($"������Ŀ�����������ɹ�: {selectedType}");
            }
            else
            {
                Debug.LogError("[NQMC] �޷�������Ŀ������");
                OnGameEnded?.Invoke(false);
            }
        }

        /// <summary>
        /// ����������Ŀ������ģʽ��
        /// </summary>
        private void RequestNetworkQuestion()
        {
            if (NetworkManager.Instance?.IsConnected == true)
            {
                isWaitingForNetworkQuestion = true;
                NetworkManager.Instance.RequestQuestion();
                LogDebug("����������Ŀ...");
            }
            else
            {
                Debug.LogError("[NQMC] ����δ���ӣ��޷�������Ŀ");
                OnGameEnded?.Invoke(false);
            }
        }

        /// <summary>
        /// ����������Ŀ
        /// </summary>
        private void LoadNetworkQuestion(NetworkQuestionData networkQuestion)
        {
            if (networkQuestion == null)
            {
                Debug.LogError("[NQMC] ������Ŀ����Ϊ��");
                return;
            }

            LogDebug($"����������Ŀ: {networkQuestion.questionType}");

            // ����ǰ������
            CleanupCurrentManager();

            // ����������Ŀ����
            currentNetworkQuestion = networkQuestion;

            // ������Ӧ�Ĺ�����
            currentManager = CreateQuestionManager(networkQuestion.questionType, true);

            if (currentManager != null)
            {
                // �󶨹�������Ѫ��ϵͳ
                if (hpManager != null)
                    hpManager.BindManager(currentManager);

                // ������𰸽���¼�
                currentManager.OnAnswerResult += HandleNetworkAnswerSubmission;

                // �ӳټ�����Ŀ
                StartCoroutine(DelayedLoadQuestion());

                LogDebug($"������Ŀ�����������ɹ�: {networkQuestion.questionType}");
            }
            else
            {
                Debug.LogError("[NQMC] �޷�Ϊ������Ŀ����������");
            }
        }

        /// <summary>
        /// �ӳټ�����Ŀ
        /// </summary>
        private IEnumerator DelayedLoadQuestion()
        {
            yield return null;
            if (currentManager != null)
            {
                currentManager.LoadQuestion();
                StartTimer();
                LogDebug("��Ŀ�Ѽ��ز���ʼ��ʱ");
            }
        }

        /// <summary>
        /// ������Ŀ��������ʹ�ù�����
        /// </summary>
        private QuestionManagerBase CreateQuestionManager(QuestionType questionType, bool isNetworkMode)
        {
            // ʹ�ù�������������
            return QuestionManagerFactory.CreateManagerOnGameObject(
                this.gameObject,
                questionType,
                isNetworkMode,
                false // ���ǽ�����ģʽ����ҪUI
            );
        }

        /// <summary>
        /// ����ǰ��Ŀ������
        /// </summary>
        private void CleanupCurrentManager()
        {
            if (currentManager != null)
            {
                LogDebug($"������Ŀ������: {currentManager.GetType().Name}");

                // �Ƴ��¼�����
                currentManager.OnAnswerResult -= HandleLocalAnswerResult;
                currentManager.OnAnswerResult -= HandleNetworkAnswerSubmission;

                // ʹ�ù�����ȫ����
                QuestionManagerFactory.DestroyManager(currentManager);
                currentManager = null;
            }
        }

        /// <summary>
        /// ����Ȩ��ѡ�������Ŀ����
        /// </summary>
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

        #endregion

        #region ����������

        /// <summary>
        /// �����ش�����������ģʽ��
        /// </summary>
        private void HandleLocalAnswerResult(bool isCorrect)
        {
            LogDebug($"����ģʽ������: {(isCorrect ? "��ȷ" : "����")}");

            StopTimer();

            // ����Ѫ���仯
            if (!isCorrect && hpManager != null)
            {
                hpManager.HPHandleAnswerResult(false);

                // ����Ƿ���Ϸ����
                if (hpManager.CurrentHealth <= 0)
                {
                    LogDebug("Ѫ���ľ�����Ϸ����");
                    OnGameEnded?.Invoke(false);
                    return;
                }
            }

            // ֪ͨ�������
            OnAnswerCompleted?.Invoke(isCorrect);

            // �ӳټ�����һ��
            Invoke(nameof(LoadNextLocalQuestion), timeUpDelay);
        }

        /// <summary>
        /// ����������ύ������ģʽ��
        /// </summary>
        private void HandleNetworkAnswerSubmission(bool isCorrect)
        {
            LogDebug("����ģʽ���ش��⣬�ȴ�������ȷ��");
            // ����ģʽ�£����ش����������������ȴ����������
            StopTimer();
        }

        /// <summary>
        /// ����ʱ
        /// </summary>
        private void HandleTimeUp()
        {
            LogDebug("���ⳬʱ");

            StopTimer();

            if (isMultiplayerMode)
            {
                // ����ģʽ���ύ�մ𰸱�ʾ��ʱ
                if (isMyTurn && NetworkManager.Instance?.IsConnected == true)
                {
                    NetworkManager.Instance.SubmitAnswer("");
                    LogDebug("��ʱ���ύ�մ�");
                }
            }
            else
            {
                // ����ģʽ��ֱ�Ӵ���ʱ
                if (currentManager != null)
                {
                    currentManager.OnAnswerResult?.Invoke(false);
                }
                else
                {
                    Invoke(nameof(LoadNextLocalQuestion), timeUpDelay);
                }
            }
        }

        #endregion

        #region �����¼�����

        /// <summary>
        /// ���յ�������Ŀ
        /// </summary>
        private void OnNetworkQuestionReceived(NetworkQuestionData question)
        {
            if (!isMultiplayerMode || !isWaitingForNetworkQuestion)
                return;

            LogDebug($"�յ�������Ŀ: {question.questionType}");
            isWaitingForNetworkQuestion = false;
            LoadNetworkQuestion(question);
        }

        /// <summary>
        /// ���յ�����𰸽��
        /// </summary>
        private void OnNetworkAnswerResult(bool isCorrect, string correctAnswer)
        {
            if (!isMultiplayerMode)
                return;

            LogDebug($"�յ�������������: {(isCorrect ? "��ȷ" : "����")}");

            // ����Ѫ���仯
            if (!isCorrect && hpManager != null)
            {
                hpManager.HPHandleAnswerResult(false);

                // ����Ƿ���Ϸ����
                if (hpManager.CurrentHealth <= 0)
                {
                    LogDebug("Ѫ���ľ�����Ϸ����");
                    OnGameEnded?.Invoke(false);
                    return;
                }
            }

            // ��ʾ�𰸷���
            ShowAnswerFeedback(isCorrect, correctAnswer);
            OnAnswerCompleted?.Invoke(isCorrect);
        }

        /// <summary>
        /// ��һغϱ��
        /// </summary>
        private void OnNetworkPlayerTurnChanged(ushort playerId)
        {
            if (!isMultiplayerMode)
                return;

            bool wasMyTurn = isMyTurn;
            isMyTurn = (playerId == NetworkManager.Instance?.ClientId);

            LogDebug($"�غϱ��: {(isMyTurn ? "�ֵ�����" : $"�ֵ����{playerId}")}");

            if (isMyTurn && !wasMyTurn)
            {
                // �ֵ��Ҵ���
                RequestNetworkQuestion();
            }
            else if (!isMyTurn && wasMyTurn)
            {
                // �������ҵĻغϣ�ֹͣ��ǰ��Ŀ
                StopTimer();
                // ����ѡ������ǰ�������򱣳���ʾ״̬
            }
        }

        /// <summary>
        /// ����Ͽ�����
        /// </summary>
        private void OnNetworkDisconnected()
        {
            if (isMultiplayerMode && gameStarted)
            {
                Debug.LogWarning("[NQMC] ����Ͽ�����Ϸ������");
                StopTimer();
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
                LogDebug($"�ύ�����: {answer}");
            }
            else if (!isMultiplayerMode && currentManager != null)
            {
                currentManager.CheckAnswer(answer);
                LogDebug($"�ύ���ش�: {answer}");
            }
            else
            {
                LogDebug("�޷��ύ�𰸣�ģʽ��ƥ���״̬�쳣");
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

        /// <summary>
        /// ��ʼ��ʱ��
        /// </summary>
        private void StartTimer()
        {
            if (timerManager != null)
            {
                timerManager.StartTimer();
                LogDebug("��ʱ���ѿ�ʼ");
            }
        }

        /// <summary>
        /// ֹͣ��ʱ��
        /// </summary>
        private void StopTimer()
        {
            if (timerManager != null)
            {
                timerManager.StopTimer();
                LogDebug("��ʱ����ֹͣ");
            }
        }

        /// <summary>
        /// ��ʾ�𰸷���
        /// </summary>
        private void ShowAnswerFeedback(bool isCorrect, string correctAnswer)
        {
            string feedback = isCorrect ? "�ش���ȷ��" : $"�ش������ȷ���ǣ�{correctAnswer}";
            LogDebug($"���ⷴ��: {feedback}");

            // �����ǰ������֧����ʾ��������������Ӧ����
            if (currentManager != null)
            {
                // ����ͨ�������ӿڵ��ù���������ʾ����
                // ����: if (currentManager is INetworkResultDisplayer displayer)
                //           displayer.ShowNetworkResult(isCorrect, correctAnswer);
            }
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[NQMC] {message}");
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
            info += $"��ǰ������: {(currentManager != null ? currentManager.GetType().Name : "��")}\n";

            if (hpManager != null)
                info += $"��ǰѪ��: {hpManager.CurrentHealth}\n";

            if (timerManager != null)
                info += $"��ʱ��״̬: {(timerManager.IsRunning ? "������" : "��ֹͣ")}\n";

            return info;
        }

        /// <summary>
        /// �������״̬�������ã�
        /// </summary>
        [ContextMenu("����״̬")]
        public void ResetState()
        {
            if (Application.isPlaying)
            {
                LogDebug("����״̬");
                StopGame();
                CleanupCurrentManager();
                currentNetworkQuestion = null;
                isWaitingForNetworkQuestion = false;
            }
        }

        #endregion
    }
}